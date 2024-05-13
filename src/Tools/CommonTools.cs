using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;

namespace Tools
{
    public static class CommonTools
    {
        [DllImport("kernel32.dll")]
        public static extern int WinExec(string exeName, int operType);

        [DllImport("user32.dll")]
        public static extern IntPtr GetActiveWindow();

        [DllImport("user32.dll", SetLastError = true)]
        public static extern void SwitchToThisWindow(IntPtr hWnd, bool fAltTab);

        public static string GetRootDir()
        {
            ProcessModule processModule = Process.GetCurrentProcess().MainModule;
            return processModule != null ? Path.GetDirectoryName(processModule.FileName) : "";
        }

        public static string GetInstallDir()
        {
            var rootDir = GetRootDir();
            var pathRoot = Path.GetPathRoot(rootDir);
            return pathRoot != null && pathRoot.ToLower().ElementAt(0) == 'c' && Directory.Exists("D:")
                ? "D:/Program Files"
                : Path.GetPathRoot(rootDir) + "/Program Files";
        }

        public static string GetDeviceId()
        {
            var str1 = "";
            try
            {
                foreach (ManagementBaseObject instance in new ManagementClass("Win32_BaseBoard").GetInstances())
                    str1 = instance.Properties["SerialNumber"].Value.ToString();
            }
            catch
            {
                // ignored
            }

            var str2 = "";
            try
            {
                using (ManagementObjectCollection.ManagementObjectEnumerator enumerator =
                       new ManagementClass("Win32_BIOS").GetInstances().GetEnumerator())
                {
                    if (enumerator.MoveNext())
                        str2 = enumerator.Current.Properties["SerialNumber"].Value.ToString();
                }
            }
            catch
            {
                // ignored
            }

            var str3 = "";
            try
            {
                using (ManagementObjectCollection.ManagementObjectEnumerator enumerator =
                       new ManagementClass("Win32_Processor").GetInstances().GetEnumerator())
                {
                    if (enumerator.MoveNext())
                        str3 = enumerator.Current.Properties["UniqueId"].Value.ToString();
                }
            }
            catch
            {
                // ignored
            }

            var str4 = "";
            try
            {
                using (ManagementObjectCollection.ManagementObjectEnumerator enumerator =
                       new ManagementClass("Win32_DiskDrive").GetInstances().GetEnumerator())
                {
                    if (enumerator.MoveNext())
                        str4 = enumerator.Current.Properties["SerialNumber"].Value.ToString();
                }
            }
            catch
            {
                // ignored
            }

            const string str5 = "";
            try
            {
                using (ManagementObjectCollection.ManagementObjectEnumerator enumerator =
                       new ManagementClass("Win32_OperatingSystem").GetInstances().GetEnumerator())
                {
                    if (enumerator.MoveNext())
                        str4 = enumerator.Current.Properties["SerialNumber"].Value.ToString();
                }
            }
            catch
            {
                // ignored
            }

            var str6 = str1.GetHashCode().ToString("x8");
            var hashCode = str2.GetHashCode();
            var str7 = hashCode.ToString("x8");
            hashCode = str3.GetHashCode();
            var str8 = hashCode.ToString("x8");
            var str9 = str4.GetHashCode().ToString("x8");
            var str10 = str5.GetHashCode().ToString("x8");
            return str6 + str7 + str8 + str9 + str10;
        }

        public static string Md5Sum(string ori)
        {
            return BitConverter.ToString(new MD5CryptoServiceProvider().ComputeHash(Encoding.Default.GetBytes(ori)))
                .Replace("-", "");
        }

        public static string PushEvent(string url, string actionId, string args = null)
        {
            try
            {
                if (string.IsNullOrEmpty(url))
                    return "";
                var num = (DateTime.UtcNow.Ticks - new DateTime(1970, 1, 1).Ticks) / 10000000L;
                var format =
                    "?action_id={0}&dev_identity={1}&account_id=0&role_id=0&client_second={2}&game_id=0&gateway_id=0&ch_id=qqdt&openid={3}&os={4}&bit={5}";
                url += string.Format(format, actionId, Config.DeviceId, num, Config.Id, Config.Os, Config.OsBit);
                if (args != null)
                    url = url + "&" + args;
                return HttpUtils.Http(url);
            }
            catch (Exception)
            {
                return "pusherr";
            }
        }

        public static void Exit()
        {
            Config.CanClose = true;
            Environment.Exit(0);
        }
    }
}