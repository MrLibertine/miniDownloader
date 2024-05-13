using System;
using System.Collections.Generic;
using System.Xml;

public class BaseInfo
{
    public string DownloadMd5 = "";
    public string DownloadUrl = "";
    public string EventUrl = "";
    public bool TimeoutAutoRequest;
}

public class Downloads
{
    public readonly List<DownloadItem> Download = new List<DownloadItem>();
}

public class DownloadInfo
{
    public readonly BaseInfo BaseInfo = new BaseInfo();

    // ReSharper disable once MemberCanBePrivate.Global
    public readonly Downloads Downloads = new Downloads();

    public void ParseFromStr(string content)
    {
        XmlDocument xmlDocument = new XmlDocument();
        xmlDocument.LoadXml(content);
        XmlNode xmlNode1 = xmlDocument.SelectSingleNode("root");
        XmlNode xmlNode2 = xmlNode1?.SelectSingleNode("base_info");
        if (xmlNode2?.Attributes != null)
        {
            BaseInfo.DownloadUrl = xmlNode2.Attributes["download_url"].Value;
            BaseInfo.DownloadMd5 = xmlNode2.Attributes["download_md5"].Value;
            if (xmlNode2.Attributes["event_url"] != null)
                BaseInfo.EventUrl = xmlNode2.Attributes["event_url"].Value;
            if (xmlNode2.Attributes["timeout_auto_request"] != null)
                try
                {
                    BaseInfo.TimeoutAutoRequest = bool.Parse(xmlNode2.Attributes["timeout_auto_request"].Value);
                }
                catch (Exception)
                {
                    // ignored
                }
        }

        XmlNode xmlNode3 = xmlNode1?.SelectSingleNode("downloads");
        if (xmlNode3 == null)
            return;
        foreach (XmlNode childNode in xmlNode3.ChildNodes)
        {
            DownloadItem downloadItem = new DownloadItem();
            if (childNode.Attributes?["download_url"] != null)
                downloadItem.DownloadUrl = childNode.Attributes["download_url"].Value;
            if (childNode.Attributes?["download_md5"] != null)
                downloadItem.DownloadMd5 = childNode.Attributes["download_md5"].Value;
            if (childNode.Attributes?["newest"] != null)
                downloadItem.Newest = bool.Parse(childNode.Attributes["newest"].Value);
            if (childNode.Attributes?["bit"] != null)
                downloadItem.Bit = int.Parse(childNode.Attributes["bit"].Value);
            Downloads.Download.Add(downloadItem);
        }
    }

    public bool Md5CanUse(int bit, string md5)
    {
        if (string.IsNullOrEmpty(md5))
            return false;
        if (!string.IsNullOrEmpty(BaseInfo.DownloadMd5) && BaseInfo.DownloadMd5.Equals(md5, StringComparison.OrdinalIgnoreCase) && bit == 64)
            return true;
        foreach (DownloadItem downloadItem in Downloads.Download)
            if (downloadItem.Bit == bit && downloadItem.DownloadMd5.Equals(md5, StringComparison.OrdinalIgnoreCase))
                return true;
        return false;
    }

    public string GetNewestDownloadUrl(int bit)
    {
        if (bit != 32 && bit != 64)
            bit = 64;
        foreach (DownloadItem downloadItem in Downloads.Download)
            if (downloadItem.Bit == bit && downloadItem.Newest && !string.IsNullOrEmpty(downloadItem.DownloadUrl))
                return downloadItem.DownloadUrl;
        foreach (DownloadItem downloadItem in Downloads.Download)
            if (downloadItem.Bit == bit && !string.IsNullOrEmpty(downloadItem.DownloadUrl))
                return downloadItem.DownloadUrl;
        return BaseInfo.DownloadUrl;
    }
}