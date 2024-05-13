using System;

namespace Tools
{
    internal static class CheckWinVersion
    {
        public static int GetOsBit()
        {
            var environmentVariable = Environment.GetEnvironmentVariable("PROCESSOR_ARCHITECTURE");
            return !string.IsNullOrEmpty(environmentVariable) &&
                   string.Compare(environmentVariable, 0, "x86", 0, 3, true) != 0
                ? 64
                : 32;
        }

        public static string GetOsInfo()
        {
            try
            {
                OperatingSystem osVersion = Environment.OSVersion;
                LogTool.Instance.Info("os:" + osVersion);
                Version version = osVersion.Version;
                var str = "";
                if (osVersion.Platform == PlatformID.Win32Windows)
                    switch (version.Minor)
                    {
                        case 0:
                            str = "95";
                            break;
                        case 10:
                            str = version.Revision.ToString() != "2222A" ? "98" : "98SE";
                            break;
                        case 90:
                            str = "Me";
                            break;
                    }
                else if (osVersion.Platform == PlatformID.Win32NT)
                    switch (version.Major)
                    {
                        case 3:
                            str = "NT 3.51";
                            break;
                        case 4:
                            str = "NT 4.0";
                            break;
                        case 5:
                            str = version.Minor != 0 ? "XP" : "2000";
                            break;
                        case 6:
                            str = version.Minor != 0 ? "7" : "Vista";
                            break;
                        case 10:
                            if (version.Minor == 0) str = "10";

                            break;
                    }

                if (str != "")
                {
                    str = "Windows " + str;
                    if (osVersion.ServicePack != "")
                        str = str + " " + osVersion.ServicePack;
                }

                return str.Replace(' ', '_');
            }
            catch (Exception)
            {
                return "unknown";
            }
        }
    }
}