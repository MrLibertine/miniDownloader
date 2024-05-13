using System;
using System.CodeDom.Compiler;
using System.Diagnostics;
using System.Windows;
using System.Windows.Threading;
using Tools;

public class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        DispatcherUnhandledException += Application_DispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnUnhandledExceptionEventHandler;
        base.OnStartup(e);
    }

    private static void OnUnhandledExceptionEventHandler(object sender, UnhandledExceptionEventArgs e)
    {
        LogTool.Instance.Error(e.ExceptionObject.ToString());
        if (string.IsNullOrEmpty(Config.EventUrl))
            return;
        var str = e.ExceptionObject.ToString();
        CommonTools.PushEvent(Config.EventUrl, "UnhandledException", "_errmsg=" + str);
        CommonTools.Exit();
    }

    private void Application_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        var text = e.Exception.Message + "_" + e.Exception.StackTrace.Replace('\r', '_').Replace('\n', '|');
        LogTool.Instance.Error(text);
        if (string.IsNullOrEmpty(Config.EventUrl))
            return;
        CommonTools.PushEvent(Config.EventUrl, "UnhandledException", "_errmsg=" + text);
        CommonTools.Exit();
    }

    [DebuggerNonUserCode]
    [GeneratedCode("PresentationBuildTasks", "4.0.0.0")]
    public void InitializeComponent()
    {
        StartupUri = new Uri("MainWindow.xaml", UriKind.Relative);
    }

    [STAThread]
    [DebuggerNonUserCode]
    [GeneratedCode("PresentationBuildTasks", "4.0.0.0")]
    public static void Main()
    {
        App app = new App();
        app.InitializeComponent();
        app.Run();
    }
}