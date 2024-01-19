using System;
using Microsoft.Win32;

namespace MongoDB.Embedded
{
    internal class libz
    {
        public static object ReadSubKeyValue(string subKey, string keyName)
        {
            using (var rkSubKey = Registry.LocalMachine.OpenSubKey(subKey))
            {
                if (rkSubKey == null)
                    throw new Exception(
                        string.Format(
                            @"Error while reading registry key: {0}\{1} does not exist!",
                            subKey,
                            keyName
                        )
                    );

                try
                {
                    var result = rkSubKey.GetValue(keyName);
                    rkSubKey.Close();
                    return result;
                }
                catch (Exception ex) //This exception is thrown
                {
                    throw new Exception(
                        string.Format(
                            "Error while reading registry key: {0} param: {1}. ErrorMessage: {2}",
                            subKey,
                            keyName,
                            ex.Message
                        )
                    );
                }
            }
        }

        internal static int GetWindowsBuildNumber()
        {
            return Convert.ToInt32(
                ReadSubKeyValue(
                    @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\",
                    "CurrentBuildNumber"
                )
            );
        }
    }
}
