using System;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using Tools;

public delegate void EventDownloadDone();

public delegate void EventCheckingMd5();

public delegate void EventDownloadIng(long curDownloadSize);

public delegate void EventDownloadStart(long totalSize);

internal class DownloadThread
{
    public event EventDownloadStart EventDownloadStart;

    public event EventDownloadIng EventDownloadIng;

    public event EventCheckingMd5 EventCheckingMd5;

    public event EventDownloadDone EventDownloadDone;

    private static long GetFileSize(string filename)
    {
        try
        {
            using (FileStream fileStream = new FileStream(filename, FileMode.Open))
            {
                return fileStream.Length;
            }
        }
        catch (Exception)
        {
            return 0;
        }
    }

    public static string GetMd5FromFile(string filename)
    {
        FileStream fs = new FileStream(filename, FileMode.Open);
        MD5CryptoServiceProvider md5Helper = new MD5CryptoServiceProvider();
        var data = md5Helper.ComputeHash(fs);
        fs.Close();
        StringBuilder sbr = new StringBuilder();
        for (var i = 0; i < data.Length; ++i) sbr.Append(data[i].ToString("X2"));
        var md5Str = sbr.ToString();
        return md5Str;
    }

    private long GetHttpLength(string url)
    {
        long length = 0;
        try
        {
            HttpWebRequest req = (HttpWebRequest) WebRequest.CreateDefault(new Uri(url));
            req.Method = "HEAD";
            req.Timeout = 5000;
            HttpWebResponse res = (HttpWebResponse) req.GetResponse();
            if (res.StatusCode == HttpStatusCode.OK) length = res.ContentLength;
            res.Close();
            return length;
        }
        catch (WebException)
        {
            return 0;
        }
    }

    public void RunDownload(string url, string filename)
    {
        Stream stream1 = null;
        Stream stream2 = null;
        var len = GetHttpLength(url);
        try
        {
            HttpWebRequest httpWebRequest = (HttpWebRequest) WebRequest.Create(url);
            httpWebRequest.Timeout = 10000;

            if (File.Exists(filename))
            {
                if (Config.DownloadInfo.Md5CanUse(Config.OsBit, GetMd5FromFile(filename)))
                {
                    var fileSize = GetFileSize(filename);
                    EventDownloadStart?.Invoke(fileSize);
                    EventDownloadIng?.Invoke(fileSize);
                    EventDownloadDone?.Invoke();
                    return;
                }

                //续传
                stream2 = File.OpenWrite(filename);
                if (stream2.Length < len)
                {
                    stream2.Seek(stream2.Length, SeekOrigin.Current);
                    httpWebRequest.AddRange((int) stream2.Length);
                }
            }

            if (stream2 == null) stream2 = new FileStream(filename, FileMode.Create);

            EventDownloadStart?.Invoke(len + stream2.Length);
            stream1 = httpWebRequest.GetResponse().GetResponseStream();
            var buffer = new byte[1024];
            var length = stream2.Length;
            if (stream1 != null)
            {
                var count = stream1.Read(buffer, 0, buffer.Length);
                while (count > 0)
                {
                    length += count;
                    stream2.Write(buffer, 0, count);
                    count = stream1.Read(buffer, 0, buffer.Length);
                    EventDownloadIng?.Invoke(length);
                }

                stream2.Close();
                stream1.Close();
                CommonTools.PushEvent(Config.EventUrl, "EndDownload", "_downloadedbyte=" + length);
            }
        }
        catch (Exception)
        {
            stream2?.Close();
            stream1?.Close();
            File.Delete(filename);
            RunDownload(url, filename);
            return;
        }

        EventCheckingMd5?.Invoke();
        if (!Config.DownloadInfo.Md5CanUse(Config.OsBit, GetMd5FromFile(filename)))
            RunDownload(url, filename);
        else
            EventDownloadDone?.Invoke();
    }
}