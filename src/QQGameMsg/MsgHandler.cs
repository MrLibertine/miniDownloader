using System;
using System.Text;
using System.Threading;

using Tools;
#pragma warning disable CS0162

namespace QQGameMsg
{
    internal class MsgHandler : INamedPipeClientCallBack
    {
#if DEBUG
        private readonly MainWindow _mWind;
        private bool _first = true;
        private NamedPipeClient _pipe;
        private string _startExePath = "";
#else
        private readonly MainWindow _mWind;
        private bool _first = true;
        private NamedPipeClient _pipe;
        private string _startExePath = "";
#endif
        public MsgHandler(MainWindow wind)
        {
            _mWind = wind;
        }

        public void OnReceiveMsg(int cmd, byte[] data)
        {
            if (cmd != 11)
            {
                if (cmd != 12)
                    return;
                OnNewConnectionRefused();
            }
            else
            {
                OnNewConnectionAccepted(data);
            }
        }

        public void OnConnectionBroken(object obj)
        {
            if (_pipe != obj)
                return;
            _pipe = null;
            CommonTools.Exit();
        }

        public void Update(object source, EventArgs e)
        {
            if (_first)
            {
                _first = false;
                var commandLineArgs = Environment.GetCommandLineArgs();
                LogTool.Instance.Info("传入参数：" + string.Join("\n", commandLineArgs));
                if (commandLineArgs.Length < 2)
                {
                    LogTool.Instance.Error("命令行参数缺失");
                    CommonTools.Exit();
                }
                else
                {
                    var str1 = commandLineArgs[1];
                    var chArray1 = new[] { ',' };
                    foreach (var str2 in str1.Split(chArray1))
                    {
                        var chArray2 = new[] { '=' };
                        var strArray = str2.Split(chArray2);
                        if (strArray.Length != 2) continue;
                        switch (strArray[0])
                        {
                            case "ID":
                                Config.Id = strArray[1];
                                break;
                            case "Key":
                                Config.Key = strArray[1];
                                break;
                            case "PROCPARA":
                                Config.PROCPARA = strArray[1];
                                break;
                            case "pfkey":
                            case "PfKey":
                                Config.PfKey = strArray[1];
                                break;
                        }
                    }

                    if (Config.Id == "" || Config.Key == "" || Config.PROCPARA == "")
                    {
                        CommonTools.Exit();
                    }

                    _pipe = new NamedPipeClient(this);
                    if (!_pipe.Connect(Config.PROCPARA))
                    {
                        LogTool.Instance.Error("跟大厅建立管道连接失败");
                        CommonTools.Exit();
                    }
                }

                Config.Os = CheckWinVersion.GetOsInfo();
                Config.OsBit = CheckWinVersion.GetOsBit();
                LogTool.Instance.Info(Config.Os + "_" + Config.OsBit);
                if (!_mWind.InitConfig())
                    CommonTools.Exit();
                if (!_mWind.TryOpenUnityDemoExe())
                    _mWind.StartDownload();
            }

            _pipe?.UpdateMsg();
            if (!Config.NewConnectRequesting || (DateTime.Now - Config.NewConnectRequestTime).TotalSeconds <= 5.0)
                return;
            CommonTools.PushEvent(Config.EventUrl, "NewConnectionMaybeTimeOut");
            LogTool.Instance.Info("NewConnectionMaybeTimeOut");
            if (Config.TIME_OUT_AUTO_REQUEST)
                RequestNewConnection();
            Config.NewConnectRequestTime = DateTime.Now;
        }

        public void RequestNewConnection()
        {
            if (Config.StartingGame)
                return;
            CommonTools.PushEvent(Config.EventUrl, nameof(RequestNewConnection));
            LogTool.Instance.Info(nameof(RequestNewConnection));
            Config.NewConnectRequestTime = DateTime.Now;
            Config.NewConnectRequesting = true;
            _pipe.SendMsg(11, new byte[0]);
            LogTool.Instance.Info("RequestNewConnectionEnd");
        }

        public void GameExit()
        {
            _pipe.SendMsg(1, new byte[0]);
            _pipe.Disconnect();
        }

        private void OnNewConnectionAccepted(byte[] data)
        {
            LogTool.Instance.Info(nameof(OnNewConnectionAccepted));
            try
            {
                var str1 = Encoding.ASCII.GetString(data);
                if (str1.Trim().Length == 0)
                {
                    LogTool.Instance.Info("ErrorPipeName");
                    if (Config.ErrorPipeReportCnt == 0)
                        CommonTools.PushEvent(Config.EventUrl, "ErrorPipeName");
                    ++Config.ErrorPipeReportCnt;
                }
                else
                {
                    LogTool.Instance.Info("OnNewConnectionAcceptedSuccess ##" + str1 + "##");
                    CommonTools.PushEvent(Config.EventUrl, "OnNewConnectionAcceptedSuccess");
                    var str2 = $"ID={Config.Id},Key={Config.Key}";
                    if (Config.PfKey.Length > 0)
                        str2 = str2 + ",pfkey=" + Config.PfKey;
                    var str3 = str2 + ",PROCPARA=" + str1.Trim();
                    var eventUrl = Config.EventUrl;
                    if (Config.Id.Length == 0 || Config.Key.Length == 0)
                    {
                        CommonTools.PushEvent(Config.EventUrl, "ReceiveErrorOpenID");
                        LogTool.Instance.Info("ReceiveErrorOpenID");
                        CommonTools.Exit();
                    }

                    if (!string.IsNullOrEmpty(eventUrl))
                        eventUrl += "?";
                    Config.CanClose = true;
                    Config.StartingGame = true;
                    Config.CanClose = true;
                    var num = CommonTools.WinExec(_startExePath + " " + str3 + " " + eventUrl, 1);
                    LogTool.Instance.Info("ExeGame ret=" + num);
                    CommonTools.PushEvent(Config.EventUrl, "ExeGame", "_ret=" + num);
                }
            }
            catch (Exception ex)
            {
                var str = ex.Message + "_" + ex.StackTrace.Replace('\r', '_').Replace('\n', '|');
                LogTool.Instance.Info("OnNewConnectionAcceptedException " + str);
                CommonTools.PushEvent(Config.EventUrl, "OnNewConnectionAcceptedException", "_errmsg=" + str);
                Thread.Sleep(500);
                RequestNewConnection();
            }
        }

        private void OnNewConnectionRefused()
        {
            LogTool.Instance.Error(nameof(OnNewConnectionRefused));
            if (Config.RefuseReportCnt == 0)
                CommonTools.PushEvent(Config.EventUrl, nameof(OnNewConnectionRefused));
            ++Config.RefuseReportCnt;
            Thread.Sleep(500);
            RequestNewConnection();
        }

        public void RunApp(string path)
        {
            _startExePath = path;
            RequestNewConnection();
        }
    }
}