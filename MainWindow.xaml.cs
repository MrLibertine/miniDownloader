using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;

using Ionic.Zip;

using QQGameMsg;

using Tools;

public partial class MainWindow
{
    private MsgHandler _msgHandler;

    public MainWindow()
    {
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        InitializeComponent();
        Loaded += (r, s) =>
        {
            MouseDown += (x, y) =>
            {
                if (y.LeftButton != MouseButtonState.Pressed)
                    return;
                DragMove();
            };
            Closing += main_FormClosing;
            _msgHandler = new MsgHandler(this);

            ////等待10秒
            //DateTime dt1 = DateTime.Now;
            //while ((DateTime.Now - dt1).TotalMilliseconds < 10000)
            //{
            //    continue;
            //}

            //_msgHandler.Update(null, null);


            DispatcherTimer timer = new DispatcherTimer
            {
                Interval = new TimeSpan(0, 0, 0, 0, 1000)
            };
            timer.Tick += _msgHandler.Update;
            timer.Start();
        };
        InitUi();
    }

    private void main_FormClosing(object sender, CancelEventArgs e)
    {
        e.Cancel = !Config.CanClose;
    }

    private void InitUi()
    {
        processLbl.Content = "0%";
        progressBar.Minimum = 0.0;
    }

    public bool InitConfig()
    {
        var str1 = CommonTools.GetRootDir() + "/config.ini";
        if (!File.Exists(str1))
        {
            LogTool.Instance.Error("not found the ini file:" + str1);
            return false;
        }

        Config.FirstUrl = IniReader.Read("dygame", "firstUrl", "", str1);
        Config.SaveFile = IniReader.Read("dygame", "saveFile", "", str1);
        Config.SaveFile = CommonTools.GetRootDir() + "/" + Config.SaveFile;
        Config.UnzipDir = IniReader.Read("dygame", "unzipDir", "", str1);
        Config.UnzipDir = CommonTools.GetRootDir() + "/" + Config.UnzipDir;
        Config.ExePath = IniReader.Read("dygame", "exePath", "", str1);
        Config.ExePath = CommonTools.GetRootDir() + "/" + Config.ExePath;
        LogTool.Instance.Info(
            $"FirstUrl:{Config.FirstUrl} SaveFile:{Config.SaveFile} UnzipDir:{Config.UnzipDir} ExePath:{Config.ExePath}");
        if (Config.FirstUrl.Length == 0 || Config.SaveFile.Length == 0 || Config.UnzipDir.Length == 0 ||
            Config.ExePath.Length == 0)
        {
            LogTool.Instance.Error("please check ini file");
            MessageBox.Show("error: please check ini file");
            return false;
        }

        var str2 = Config.FirstUrl + "?t=" + DateTime.Now.Millisecond;
        LogTool.Instance.Info(str2);
        var content = HttpUtils.Http(str2);
        LogTool.Instance.Info("远程内容：" + content);
        if (content != null)
            if (content.Length != 0)
            {
                try
                {
                    Config.DownloadInfo.ParseFromStr(content);
                    if (!string.IsNullOrEmpty(Config.DownloadInfo.BaseInfo.EventUrl))
                        Config.EventUrl = Config.DownloadInfo.BaseInfo.EventUrl;
                    Config.NewConnectRequesting = Config.DownloadInfo.BaseInfo.TimeoutAutoRequest;
                }
                catch (Exception ex)
                {
                    LogTool.Instance.Error("resStr: " + content);
                    LogTool.Instance.Error("parse downloadinfo failed, " + ex.StackTrace);
                    return false;
                }

                return true;
            }

        LogTool.Instance.Error("request downloadinfo failed");
        return true;
    }

    public bool TryOpenUnityDemoExe()
    {
        LogTool.Instance.Info(nameof(TryOpenUnityDemoExe));
        if (!File.Exists(Config.SaveFile) || !File.Exists(Config.ExePath))
        {
            LogTool.Instance.Info("找不到文件" + Config.SaveFile + "  " + Config.ExePath);
            return false;
        }

        var md5 = DownloadThread.GetMd5FromFile(Config.SaveFile);
        if (!Config.DownloadInfo.Md5CanUse(Config.OsBit, md5))
        {
            LogTool.Instance.Info("MD5异常" + md5);
            return false;
        }

        _msgHandler.RunApp(Config.ExePath);
        return true;
    }

    public void UpdateProgressMaxValue(int maxValue)
    {
        progressBar.Maximum = maxValue;
        UpdateTipsLbl("正在下载，请耐心等待");
    }

    public void UpdateProgressUnzipMaxValue(int maxValue)
    {
        progressBar.Maximum = maxValue;
        progressBar.Value = 0.0;
        UpdateTipsLbl("正在安装，请稍等");
    }

    public void UpdateTipsLbl(string txt)
    {
        tipsLbl.Content = txt;
    }

    private void UpdateProgressCurValue(int curValue)
    {
        progressBar.Value = curValue;
        processLbl.Content =
            string.Format("{0:F}", curValue / progressBar.Maximum * 100.0) +
            "%";
    }

    private void UpdateProgressUnzipCurValue(string text, int curValue)
    {
        progressBar.Value = curValue;
        if (text.Length > 18)
            text = "..." + text.Substring(text.Length - 18);
        processLbl.Content = text + " " + curValue + "%";
    }

    public void StartDownload()
    {
        tipsLbl.Content = "正在下载中，请耐心等待";
        processLbl.Visibility = Visibility.Visible;
        DownloadThread parameter = new DownloadThread();
        parameter.EventDownloadStart += OnEventDownloadStart;
        parameter.EventDownloadIng += OnEventDownloadIng;
        parameter.EventCheckingMd5 += OnEventCheckMd5;
        parameter.EventDownloadDone += OnEventDownloadDone;
        CommonTools.PushEvent(Config.EventUrl, "BeginDownload");
        new Thread(StartThread).Start(parameter);
    }

    private void StartThread(object obj)
    {
        ((DownloadThread)obj).RunDownload(Config.DownloadInfo.GetNewestDownloadUrl(Config.OsBit),
            Config.SaveFile);
    }

    private void UnZipFile(string file, int retrycnt = 0)
    {
        LogTool.Instance.Info(nameof(UnZipFile));
        using (ZipFile zipFile = new ZipFile(file))
        {
            zipFile.ExtractProgress += OnEventExtractProgress;
            zipFile.ExtractAll(Config.UnzipDir, ExtractExistingFileAction.OverwriteSilently);
        }

        CommonTools.PushEvent(Config.EventUrl, "EndInstallGame");
        LogTool.Instance.Info("UnZipFile end");
    }

    private void OnEventExtractProgress(object sender, ExtractProgressEventArgs e)
    {
        if (e.TotalBytesToTransfer <= 0L)
            return;
        OnEventUnzipIng(e.CurrentEntry.FileName, 100L * e.BytesTransferred / e.TotalBytesToTransfer);
    }

    private void OnEventReadProgress(object sender, ReadProgressEventArgs e)
    {
        e.ToString();
    }

    private void OnEventDownloadStart(long totalSize)
    {
        LogTool.Instance.Info("in OnEventDownloadStart");
        Dispatcher.Invoke(new Action<int>(UpdateProgressMaxValue),
            (int)(totalSize / 100L));
    }

    private void OnEventDownloadIng(long curDownloadSize)
    {
        Dispatcher.Invoke(new Action<int>(UpdateProgressCurValue), (int)(curDownloadSize / 100L));
    }

    private void OnEventCheckMd5()
    {
        Dispatcher.Invoke(new Action<string>(UpdateTipsLbl), "正在校验文件，请稍等");
    }

    private void OnEventUnzipStart()
    {
        Dispatcher.Invoke(new Action<int>(UpdateProgressUnzipMaxValue), 100);
    }

    private void OnEventUnzipIng(string text, long curSize)
    {
        Dispatcher.Invoke(new Action<string, int>(UpdateProgressUnzipCurValue), text,
            (int)curSize);
    }

    private void OnEventDownloadDone()
    {
        LogTool.Instance.Info("in OnEventDownloadDone");
        OnEventUnzipStart();
        UnZipFile(Config.SaveFile);
        TryOpenUnityDemoExe();
    }

    [DllImport("kernel32.dll")]
    public static extern int WinExec(string exeName, int operType);

    private void contactBtn_Click(object sender, RoutedEventArgs e)
    {
        Process.Start("https://hodogame.udesk.cn/im_client/?web_plugin_id=116041");
    }

    private void addqqBtn_Click(object sender, RoutedEventArgs e)
    {
        Process.Start("https://jq.qq.com/?_wv=1027&k=FMIcacvI");
    }

    private void progressBar_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {

    }

    //[DebuggerNonUserCode]
    //[GeneratedCode("PresentationBuildTasks", "4.0.0.0")]
    //public void InitializeComponent()
    //{
    //    if (this._contentLoaded)
    //        return;
    //    this._contentLoaded = true;
    //    Application.LoadComponent((object)this, new Uri("/miniDownloader;component/mainwindow.xaml", UriKind.Relative));
    //}

    /* void IComponentConnector.Connect(int connectionId, object target)
         {
             switch (connectionId)
             {
                 case 1:
                     this.contactBtn = (Button)target;
                     this.contactBtn.Click += new RoutedEventHandler(this.contactBtn_Click);
                     break;
                 case 2:
                     this.addqqBtn = (Button)target;
                     this.addqqBtn.Click += new RoutedEventHandler(this.addqqBtn_Click);
                     break;
                 case 3:
                     this.tipsLbl = (Label)target;
                     break;
                 case 4:
                     this.progressBar = (ProgressBar)target;
                     break;
                 case 5:
                     this.processLbl = (Label)target;
                     break;
                 default:
                     this._contentLoaded = true;
                     break;
             }
         }*/
}