using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32;

namespace TempleExtender
{
    /// <summary>
    /// Enables debugging output from ToEE by doing two things:
    /// a) Overrides the registry query
    /// </summary>
    internal class DebugOutputRedirection : IDisposable
    {
        private bool _active;

        private static readonly IntPtr VirtualKey = new IntPtr(0x7F000001);

        #region Delegates

        [UnmanagedFunctionPointer(CallingConvention.Winapi, CharSet = CharSet.Ansi)]
        private delegate int RegOpenKeyExDelegate(
            IntPtr hKey, string subKey, int ulOptions, int samDesired, out IntPtr hkresult);

        private delegate int RegQueryValueExDelegate(IntPtr hKey, string lpValueName, int lpReserved, out uint lpType,
                                                     IntPtr lpData, ref uint lpcbData);

        private delegate int RegCloseKeyDelegate(IntPtr hKey);

        private readonly RegOpenKeyExDelegate _regOpenKeyExDelegate;

        private readonly RegQueryValueExDelegate _regQueryValueExDelegate;

        private readonly RegCloseKeyDelegate _regCloseKeyDelegate;

        #endregion

        public DebugOutputRedirection(TempleLibrary library, Settings settings)
        {
            _active = settings.DebuggingOutput;

            _regOpenKeyExDelegate = new RegOpenKeyExDelegate(RegOpenKeyExHook);
            _regQueryValueExDelegate = new RegQueryValueExDelegate(RegQueryValueExHook);
            _regCloseKeyDelegate = new RegCloseKeyDelegate(RegCloseKeyHook);

            library.ImportAddressTable.ReplaceFunction("advapi32.dll", "RegOpenKeyExA", _regOpenKeyExDelegate);
            library.ImportAddressTable.ReplaceFunction("advapi32.dll", "RegQueryValueExA", _regQueryValueExDelegate);
            library.ImportAddressTable.ReplaceFunction("advapi32.dll", "RegCloseKey", _regCloseKeyDelegate);
        }

        private static int RegOpenKeyExHook(IntPtr hKey, string subkey, int uloptions, int samdesired,
                                            out IntPtr hkresult)
        {
            if (subkey.ToLowerInvariant() == @"software\troika\tig\debug output")
            {
                Console.WriteLine("OPENING REGISTRY KEY: " + subkey);
                hkresult = VirtualKey;
                return 0;
            }

            return Win32Api.RegOpenKeyEx(hKey, subkey, uloptions, samdesired, out hkresult);
        }

        private int RegQueryValueExHook(IntPtr hkey, string lpvaluename, int lpreserved, out uint lptype, IntPtr lpdata,
                                        ref uint lpcbdata)
        {
            if (hkey == VirtualKey)
            {
                Console.WriteLine("QUERYING VIRTUAL KEY: " + lpvaluename);
                if (lpvaluename == "file")
                {
                    lptype = (uint) RegistryValueKind.DWord;
                    Marshal.WriteInt32(lpdata, _active ? 1 : 0);
                    return 0;
                }

                lptype = 0;
                return 1;
            }

            return Win32Api.RegQueryValueEx(hkey, lpvaluename, lpreserved, out lptype, lpdata, ref lpcbdata);
        }

        private static int RegCloseKeyHook(IntPtr hkey)
        {
            if (hkey == VirtualKey)
            {
                Console.WriteLine("Closing virtual key.");
                return 0;
            }
            return Win32Api.RegCloseKey(hkey);
        }

        public void Dispose()
        {
            if (_active)
            {
                _active = false;
            }
        }
    }
}