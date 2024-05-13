using System;
using System.IO;
using System.Text;

namespace Tools
{
    public class LogTool
    {
        private static LogTool _log;
        private const string Format = "[yyyy-MM-dd_HH:mm:ss_fff] ";
        private string _logFile = "";

        // ReSharper disable once MemberCanBePrivate.Global
        public LogTool(string logName)
        {
            Init(AppDomain.CurrentDomain.BaseDirectory + "/log/" + logName + ".txt");
        }

        public static LogTool Instance
        {
            get
            {
                if (_log == null)
                    _log = new LogTool("log");
                return _log;
            }
        }

        private void Init(string logFile)
        {
            _logFile = logFile;
            var directoryName = Path.GetDirectoryName(logFile);
            if (!Directory.Exists(directoryName))
                Directory.CreateDirectory(directoryName ?? throw new InvalidOperationException());
            if (File.Exists(logFile))
                return;
            File.Create(logFile).Close();
        }

        // ReSharper disable once UnusedMember.Local
        private void Write(string text)
        {
            using (StreamWriter streamWriter = new StreamWriter(_logFile, true, Encoding.UTF8))
            {
                streamWriter.Write(DateTime.Now.ToString(Format) + text);
            }
        }

        private void WriteLine(string text)
        {
            try
            {
                text += "\r\n";
                using (StreamWriter streamWriter = new StreamWriter(_logFile, true, Encoding.UTF8))
                {
                    streamWriter.Write(DateTime.Now.ToString(Format) + text);
                    streamWriter.Flush();
                }
            }
            catch (Exception)
            {
                // ignored
            }
        }

        public void Info(string text)
        {
            WriteLine("INFO:" + text);
        }

        public void Error(string text)
        {
            WriteLine("ERROR:" + text);
        }
    }
}