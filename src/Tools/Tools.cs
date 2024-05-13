using System.Runtime.InteropServices;
using System.Text;

namespace Tools
{
    internal static class IniReader
    {
        [DllImport("kernel32.dll")]
        private static extern int GetPrivateProfileString(
            string lpAppName,
            string lpKeyName,
            string lpDefault,
            StringBuilder lpReturnedString,
            int nSize,
            string lpFileName);

        public static string Read(string section, string key, string def, string filePath)
        {
            StringBuilder lpReturnedString = new StringBuilder(1024);
            GetPrivateProfileString(section, key, def, lpReturnedString, 1024, filePath);
            return lpReturnedString.ToString();
        }
    }
}