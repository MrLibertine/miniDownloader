using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using HANDLE = System.IntPtr;

/*
 * 这个类主要是和大厅建立连接的管道相关代码，
 * 那些private函数主要是收发数据的一些逻辑，无需关注
 * 外面用到的就是几个public函数，Connect(),Disconnect(),UpdateMsg(), SendData()
 * 
 */
namespace QQGameMsg
{
    //收到消息的回调接口
    public interface INamedPipeClientCallBack
    {
        void OnReceiveMsg(int cmd, byte[] data);
        void OnConnectionBroken(object obj);
    }

    public class NamedPipeClient
    {
        private const uint GENERIC_READ = 0x80000000;
        private const uint GENERIC_WRITE = 0x40000000;

        private const uint FILE_SHARE_READ = 0x00000001;
        private const uint FILE_SHARE_WRITE = 0x00000002;

        private const uint OPEN_EXISTING = 3;

        private const uint FILE_FLAG_OVERLAPPED = 0x40000000;

        private const int ERROR_IO_PENDING = 997;

        private const int ERROR_MORE_DATA = 234;

        private const int ERROR_PIPE_NOT_CONNECTED = 233;

        private const int WAIT_OBJECT_0 = 0;

        private readonly HANDLE INVALID_HANDLE_VALUE = (HANDLE) (-1);
        private readonly INamedPipeClientCallBack m_callback;

        private HANDLE m_pipeHandle;
        private string m_pipeName;
        private bool m_readyForNewMsg;

        public NamedPipeClient(INamedPipeClientCallBack callback)
        {
            m_callback = callback;
            m_pipeHandle = INVALID_HANDLE_VALUE;
        }

        [DllImport("kernel32", CallingConvention = CallingConvention.StdCall, SetLastError = true,
            CharSet = CharSet.Unicode)]
        private static extern HANDLE CreateFile
        (
            string fileName,
            uint desiredAccess,
            uint shareMode,
            uint securityAttributes,
            uint creationDisposition,
            uint flagsAndAttributes,
            HANDLE templateFile
        );

        [DllImport("kernel32", CallingConvention = CallingConvention.StdCall, SetLastError = true,
            CharSet = CharSet.Unicode)]
        private static extern HANDLE CreateEvent
        (
            int lpEventAttributes,
            int bManualReset,
            int bInitialState,
            int lpName
        );

        [DllImport("kernel32", CallingConvention = CallingConvention.StdCall, SetLastError = true)]
        private static extern int ReadFile
        (
            HANDLE hFile,
            [Out] [MarshalAs(UnmanagedType.LPArray)]
            byte[] pBuffer,
            int NumberOfBytesToRead,
            ref int pNumberOfBytesRead,
            ref OVERLAPPED lpOverlapped
        );

        [DllImport("kernel32", CallingConvention = CallingConvention.StdCall, SetLastError = true)]
        private static extern int WriteFile
        (
            HANDLE hFile,
            byte[] lpBuffer,
            int nNumberOfBytesToWrite,
            ref int lpNumberOfBytesWritten,
            ref OVERLAPPED lpOverlapped
        );

        [DllImport("kernel32", CallingConvention = CallingConvention.StdCall, SetLastError = true)]
        private static extern int FlushFileBuffers(HANDLE hFile);

        [DllImport("kernel32", CallingConvention = CallingConvention.StdCall, SetLastError = true)]
        private static extern int CancelIo(HANDLE hFile);

        [DllImport("kernel32", CallingConvention = CallingConvention.StdCall, SetLastError = true)]
        private static extern int PeekNamedPipe
        (
            HANDLE hNamedPipe,
            HANDLE lpBuffer,
            int nBufferSize,
            HANDLE lpBytesRead,
            ref int lpTotalBytesAvail,
            HANDLE lpBytesLeftThisMessage
        );

        [DllImport("kernel32", CallingConvention = CallingConvention.StdCall, SetLastError = true)]
        private static extern int GetOverlappedResult
        (
            HANDLE hFile,
            ref OVERLAPPED lpOverlapped,
            ref int lpNumberOfBytesTransferred,
            int bWait
        );

        [DllImport("kernel32", CallingConvention = CallingConvention.StdCall, SetLastError = true)]
        private static extern int CloseHandle(HANDLE hObject);

        [DllImport("kernel32", CallingConvention = CallingConvention.StdCall)]
        private static extern void Sleep(int dwMilliseconds);

        [DllImport("kernel32", CallingConvention = CallingConvention.StdCall, SetLastError = true)]
        private static extern int WaitForSingleObject(HANDLE hHandle, uint dwMilliseconds);

        [DllImport("kernel32", CallingConvention = CallingConvention.StdCall)]
        private static extern uint GetTickCount();

        public bool Connect(string PipeName, uint Timeout = 5000)
        {
            try
            {
                m_pipeName = "\\\\.\\PIPE\\QQGAME" + PipeName;

                var begin = GetTickCount();
                do
                {
                    //一般来说，这里不会出现等待。但是大厅的管道是异步创建的，所以这里加个重试机制。
                    m_pipeHandle = CreateFile(m_pipeName, GENERIC_READ | GENERIC_WRITE,
                        FILE_SHARE_READ | FILE_SHARE_WRITE, 0, OPEN_EXISTING, FILE_FLAG_OVERLAPPED, IntPtr.Zero);

                    if (m_pipeHandle != INVALID_HANDLE_VALUE) break;
                    Sleep(10);
                } while (GetTickCount() - begin < Timeout);

                if (m_pipeHandle == INVALID_HANDLE_VALUE) throw new Exception("管道连接失败");

                //发送一个握手信息给大厅
                SendData(Encoding.ASCII.GetBytes("HELLO QQGAME"));

                m_readyForNewMsg = true;

                Debug.WriteLine(m_pipeName + ":管道连接成功");
            }
            catch (Exception oEX)
            {
                OnException(oEX);
                return false;
            }

            return true;
        }

        public void Disconnect()
        {
            if (m_pipeHandle != INVALID_HANDLE_VALUE)
            {
                m_readyForNewMsg = false;
                CloseHandle(m_pipeHandle);
                m_pipeHandle = INVALID_HANDLE_VALUE;
                Debug.WriteLine(m_pipeName + ":管道由游戏侧主动断开");
                m_callback.OnConnectionBroken(this);
            }
        }

        public void UpdateMsg()
        {
            if (!m_readyForNewMsg)
                return;

            try
            {
                var bytesAvail = 0;
                var rt = PeekNamedPipe(m_pipeHandle, IntPtr.Zero, 0, IntPtr.Zero, ref bytesAvail, IntPtr.Zero);
                if (rt == 0)
                {
                    var error = GetLastError();
                    if (error == ERROR_PIPE_NOT_CONNECTED)
                        throw new Exception("管道由大厅侧断开");
                    throw new Exception("PeekNamedPipe failed, error=" + error);
                }

                if (bytesAvail > 0)
                {
                    m_readyForNewMsg = false;
                    ReceiveDataHeader();
                    m_readyForNewMsg = true;
                }
            }
            catch (Exception oEX)
            {
                OnException(oEX);
            }
        }

        public void SendMsg(int cmd, byte[] data)
        {
            /*
            #define MAX_PROCMSG_DATABUF_LEN 64*1024
            typedef struct stProcMsgData
            {
                int nCommandID; //协议ID
                int nDataLen;   //buffer长度
                BYTE abyData[MAX_PROCMSG_DATABUF_LEN]; //数据区，如果有需要传递的数据，全部放这里
            }PROCMSG_DATA;
            */
            try
            {
                //以下代码将cmd和data数据存入tmp数组，符合c++ PROCMSG_DATA结构体的二进制数据排列
                var destArray = new byte[12 + data.Length];
                var destinationIndex = 0;
                var end = 0;

                Array.Copy(BitConverter.GetBytes(cmd), destArray, 4);
                destinationIndex += 4;
                Array.Copy(BitConverter.GetBytes(data.Length), 0, destArray, destinationIndex, 4);
                destinationIndex += 4;
                Array.Copy(data, 0, destArray, destinationIndex, data.Length);
                destinationIndex += data.Length;
                Array.Copy(BitConverter.GetBytes(end), 0, destArray, destinationIndex,
                    4); //这里多存入一段结尾为0的数据，防止c++那边字符串读溢出。

                SendData(destArray);
            }
            catch (Exception oEX)
            {
                OnException(oEX);
            }
        }

        private static int GetLastError()
        {
            return Marshal.GetLastWin32Error();
        }

        private void OnException(Exception oEX)
        {
            m_readyForNewMsg = false;
            Debug.WriteLine(m_pipeName + ":" + oEX.Message);
            if (m_pipeHandle != INVALID_HANDLE_VALUE)
            {
                CloseHandle(m_pipeHandle);
                m_pipeHandle = INVALID_HANDLE_VALUE;
                m_callback.OnConnectionBroken(this);
            }
        }

        private void SendData(byte[] data)
        {
            /*  
              *  #define MAX_PROCMSG_DATABUF_LEN 64*1024
              *  struct encrypt_msg 
              *  {
              *      int body_len;
              *      int check;
              *      int reserved2;
              *      char encrypt_data[MAX_PROCMSG_DATABUF_LEN];
              *  };
              *  需要按这个格式封装数据
            */
            var encrypt_data = Crypter.Encrypt(data, Encoding.ASCII.GetBytes("QQGAMEPIPECONNECTION"));
            var body_len = encrypt_data.Length;
            var check = body_len ^ 0x12345678;
            var reserved2 = 0;

            var destArray = new byte[12 + encrypt_data.Length];

            var destinationIndex = 0;
            Array.Copy(BitConverter.GetBytes(body_len), destArray, 4);
            destinationIndex += 4;
            Array.Copy(BitConverter.GetBytes(check), 0, destArray, destinationIndex, 4);
            destinationIndex += 4;
            Array.Copy(BitConverter.GetBytes(reserved2), 0, destArray, destinationIndex, 4);
            destinationIndex += 4;
            Array.Copy(encrypt_data, 0, destArray, destinationIndex, encrypt_data.Length);

            OVERLAPPED ov;
            ov.hEvent = CreateEvent(0, 0, 0, 0);
            ov.Internal = ov.InternalHigh = UIntPtr.Zero;
            ov.offset = ov.OffsetHigh = 0;
            try
            {
                var write_num = 0;
                if (0 == WriteFile(m_pipeHandle, destArray, destArray.Length, ref write_num, ref ov))
                {
                    var error = GetLastError();
                    if (error != ERROR_IO_PENDING)
                        throw new Exception("发送数据失败(WriteFile), error=" + error);

                    var rt = WaitForSingleObject(ov.hEvent, 5 * 1000);
                    if (rt != WAIT_OBJECT_0)
                    {
                        CancelIo(m_pipeHandle);
                        throw new Exception("发送数据超时(WaitForSingleObject), rt=" + rt);
                    }


                    rt = GetOverlappedResult(m_pipeHandle, ref ov, ref write_num, 0);
                    if (rt == 0)
                    {
                        error = GetLastError();
                        throw new Exception("发送数据失败(GetOverlappedResult), error=" + error);
                    }
                }
                else
                {
                    if (0 == FlushFileBuffers(m_pipeHandle))
                    {
                        var error = GetLastError();
                        throw new Exception("发送数据失败(FlushFileBuffers), error=" + error);
                    }
                }

                if (write_num != destArray.Length) throw new Exception("发送数据长度有误");
            }
            finally
            {
                CloseHandle(ov.hEvent);
            }
        }

        private void ReceiveDataBuffer(byte[] buffer)
        {
            var num_of_bytes_readed = 0;
            OVERLAPPED ov;
            ov.hEvent = CreateEvent(0, 0, 0, 0);
            ov.Internal = ov.InternalHigh = UIntPtr.Zero;
            ov.offset = ov.OffsetHigh = 0;

            try
            {
                if (0 == ReadFile(m_pipeHandle, buffer, buffer.Length, ref num_of_bytes_readed, ref ov))
                {
                    var error = GetLastError();
                    if (ERROR_IO_PENDING == error)
                    {
                        var rt = WaitForSingleObject(ov.hEvent, 5 * 1000);
                        if (rt != WAIT_OBJECT_0)
                        {
                            CancelIo(m_pipeHandle);
                            throw new Exception("接收数据超时(WaitForSingleObject), rt=" + rt);
                        }

                        rt = GetOverlappedResult(m_pipeHandle, ref ov, ref num_of_bytes_readed, 0);
                        if (rt == 0)
                        {
                            error = GetLastError();
                            throw new Exception("接收数据失败(GetOverlappedResult), error=" + error);
                        }
                    }
                    else if (error != ERROR_MORE_DATA)
                    {
                        throw new Exception("接收数据失败(ReadFile), error=" + error);
                    }
                }

                if (num_of_bytes_readed != buffer.Length) throw new Exception("接收数据长度有误");
            }
            finally
            {
                CloseHandle(ov.hEvent);
            }
        }

        private void ReceiveDataHeader()
        {
            var headerBuffer = new byte[12];
            ReceiveDataBuffer(headerBuffer);
            OnReceiveDataHeader(headerBuffer);
        }

        private void OnReceiveDataHeader(byte[] headerBuffer)
        {
            var body_len = BitConverter.ToInt32(headerBuffer, 0);
            var check = BitConverter.ToInt32(headerBuffer, 4);

            if (body_len > 64 * 1024 || body_len < 16 || check != 0 && check != (body_len ^ 0x12345678))
                //加密数据块至少16个字节,最大 64K
                throw new Exception("接收到的加密数据错误！");
            //第二步读取数据的body部分
            ReceiveDataBody(body_len);
        }

        private void ReceiveDataBody(int bodylen)
        {
            var bodyBuffer = new byte[bodylen];
            ReceiveDataBuffer(bodyBuffer);
            OnReceiveDataBody(bodyBuffer);
        }

        private void OnReceiveDataBody(byte[] bodyBuffer)
        {
            var decrypt_data = Crypter.Decrypt(bodyBuffer, Encoding.ASCII.GetBytes("QQGAMEPIPECONNECTION"));
            var cmd = BitConverter.ToInt32(decrypt_data, 0);
            var len = BitConverter.ToInt32(decrypt_data, 4);
            if (len >= 0 && len + 8 <= decrypt_data.Length)
            {
                var data = new byte[len];
                Array.Copy(decrypt_data, 8, data, 0, data.Length);
                m_callback.OnReceiveMsg(cmd, data);
            }
            else
            {
                throw new Exception("解密后的数据格式错误！");
            }
        }

        private struct OVERLAPPED
        {
            public UIntPtr Internal;
            public UIntPtr InternalHigh;
            public uint offset;
            public uint OffsetHigh;
            public HANDLE hEvent;
        }
    }
}