using System;
using System.Collections;
using System.IO;
using System.Net;
using System.Text;

namespace Tools
{
    internal static class HttpUtils
    {
        public static string Http(string url, string method = "GET", string content = "application/json;charset=utf-8", Hashtable header = null, string data = null)
        {
            try
            {
                HttpWebRequest httpWebRequest = (HttpWebRequest)WebRequest.Create(url);
                httpWebRequest.Method = string.IsNullOrEmpty(method) ? "GET" : method;
                httpWebRequest.ContentType = string.IsNullOrEmpty(content) ? "application/json;charset=utf-8" : content;
                if (header != null)
                    foreach (var key in header.Keys)
                        httpWebRequest.Headers.Add(key.ToString(), header[key].ToString());
                if (!string.IsNullOrEmpty(data))
                {
                    Stream requestStream = httpWebRequest.GetRequestStream();
                    var bytes = Encoding.UTF8.GetBytes(data);
                    requestStream.Write(bytes, 0, bytes.Length);
                    requestStream.Close();
                }

                Stream responseStream = httpWebRequest.GetResponse().GetResponseStream();
                if (responseStream == null) return string.Empty;
                StreamReader streamReader = new StreamReader(responseStream, Encoding.GetEncoding("utf-8"));
                var end = streamReader.ReadToEnd();
                streamReader.Close();
                responseStream.Close();
                return end;
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}