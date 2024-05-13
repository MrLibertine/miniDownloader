using System;

public static class Config
{
#if DEBUG
    public const string M_INI_FILE = "config.ini";
    public const bool TIME_OUT_AUTO_REQUEST = false;
    public static string DeviceId = "";
    public static string Id = "F804434CE8ECDD8282ECB7B3D6B523BF";
    public static string Key = "41DB9AB57199179B305A18A94F6A97F4";
    public static string PfKey = "475BA28E61FAA84ECB9ADECF86DF103D";
    public static string PROCPARA = "ThirdGame_4_22916_690545921";
    public static string Os = "";
    public static int OsBit = 64;
    public static string FirstUrl = "";
    public static readonly DownloadInfo DownloadInfo = new DownloadInfo();
    public static string EventUrl = "";
    public static string SaveFile = "";
    public static string UnzipDir = "";
    public static string ExePath = "";
    public static bool CanClose = false;
    public static bool StartingGame = false;
    public static DateTime NewConnectRequestTime;
    public static bool NewConnectRequesting = false;
    public static int ErrorPipeReportCnt = 0;
    public static int RefuseReportCnt = 0;
#else
    public const string M_INI_FILE = "config.ini";
    public const bool TIME_OUT_AUTO_REQUEST = false;
    public static string DeviceId = "";
    public static string Key = "";
    public static string PfKey = "";
    public static string PROCPARA = "";
    public static string Id = "";
    public static string Os = "";
    public static int OsBit = 64;
    public static string FirstUrl = "";
    public static readonly DownloadInfo DownloadInfo = new DownloadInfo();
    public static string EventUrl = "";
    public static string SaveFile = "";
    public static string UnzipDir = "";
    public static string ExePath = "";
    public static bool CanClose = false;
    public static bool StartingGame = false;
    public static DateTime NewConnectRequestTime;
    public static bool NewConnectRequesting = false;
    public static int ErrorPipeReportCnt = 0;
    public static int RefuseReportCnt = 0;
#endif

}