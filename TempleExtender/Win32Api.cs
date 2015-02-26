using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace TempleExtender
{
    internal class Win32Api
    {
        [DllImport("kernel32", SetLastError = true)]
        public static extern IntPtr LoadLibrary(string dllName);

        [DllImport("kernel32", CharSet = CharSet.Ansi, ExactSpelling = true, SetLastError = true)]
        internal static extern IntPtr GetProcAddress(IntPtr hModule, string procName);

        [DllImport("kernel32", SetLastError = true)]
        public static extern bool FreeLibrary(IntPtr handle);

        [DllImport("kernel32", CharSet = CharSet.Auto)]
        public static extern IntPtr GetModuleHandle(string lpModuleName);

        [DllImport("kernel32", CharSet = CharSet.Auto)]
        public static extern string GetCommandLine();

        [DllImport("kernel32", SetLastError = true)]
        public static extern bool VirtualProtect(IntPtr lpAddress, uint dwSize, Protection flNewProtect,
                                                 out Protection lpflOldProtect);

        [DllImport("kernel32")]
        public static extern void SetLastError(ErrorCode dwErrCode);

        [DllImport("kernel32.dll", CharSet = CharSet.Ansi)]
        public static extern IntPtr FindFirstFileA(string lpFileName, IntPtr lpFindFileData);
        
        [DllImport("kernel32.dll", CharSet = CharSet.Ansi)]
        public static extern bool FindNextFileA(IntPtr handle, IntPtr lpFindFileData);

        [DllImport("kernel32.dll")]
        public static extern bool FindClose(IntPtr handle);

        [DllImport("kernel32.dll", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall,
            SetLastError = true)]
        public static extern IntPtr CreateFileA(
            string lpFileName,
            uint dwDesiredAccess,
            uint dwShareMode,
            IntPtr securityAttributes,
            uint dwCreationDisposition,
            uint dwFlagsAndAttributes,
            IntPtr hTemplateFile
            );

        [DllImport("kernel32")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool CreateDirectory(string lpPathName, IntPtr lpSecurityAttributes);

        [DllImport("kernel32", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool DeleteFile(string lpFileName);

        [DllImport("kernel32")]
        public static extern bool RemoveDirectory(string lpPathName);

        [DllImport("kernel32")]
        public static extern bool MoveFile(string lpExistingFileName, string lpNewFileName);

        [DllImport("kernel32")]
        public static extern bool ReadFile(IntPtr hFile, IntPtr lpBuffer, uint nNumberOfBytesToRead,
                                           IntPtr lpNumberOfBytesRead, IntPtr lpOverlapped);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool CloseHandle(IntPtr hObject);

        [DllImport("kernel32.dll")]
        public static extern FileType GetFileType(IntPtr hFile);

        [DllImport("kernel32.dll")]
        public static extern bool SetEndOfFile(IntPtr hFile);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern int GetFileAttributes(string lpFileName);

        [DllImport("Kernel32.dll", SetLastError = true)]
        public static extern int SetFilePointer(IntPtr hFile, int lDistanceToMove, IntPtr lpDistanceToMoveHigh,
                                                 EMoveMethod dwMoveMethod);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool GetFileInformationByHandle(IntPtr hFile, out BY_HANDLE_FILE_INFORMATION lpFileInformation);
        
        [DllImport("advapi32.dll", CharSet = CharSet.Auto)]
        public static extern int RegOpenKeyEx(IntPtr hKey, string subKey, int ulOptions, int samDesired,
          out IntPtr hkResult);

        // This signature will not get an entire REG_BINARY value. It will stop at the first null byte.
        [DllImport("advapi32.dll", SetLastError = true)]
        public static extern int RegQueryValueEx(IntPtr hKey, string lpValueName, int lpReserved, out uint lpType,
            IntPtr lpData, ref uint lpcbData);

        [DllImport("advapi32.dll", SetLastError = true)]
        public static extern int RegCloseKey(IntPtr hKey);

        public static readonly IntPtr InvalidHandle = new IntPtr(-1);
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
    struct WIN32_FIND_DATA
    {
        public uint dwFileAttributes;
        public System.Runtime.InteropServices.ComTypes.FILETIME ftCreationTime;
        public System.Runtime.InteropServices.ComTypes.FILETIME ftLastAccessTime;
        public System.Runtime.InteropServices.ComTypes.FILETIME ftLastWriteTime;
        public uint nFileSizeHigh;
        public uint nFileSizeLow;
        public uint dwReserved0;
        public uint dwReserved1;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string cFileName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 14)]
        public string cAlternateFileName;
    }

    struct BY_HANDLE_FILE_INFORMATION
    {
        public uint FileAttributes;
        public System.Runtime.InteropServices.ComTypes.FILETIME CreationTime;
        public System.Runtime.InteropServices.ComTypes.FILETIME LastAccessTime;
        public System.Runtime.InteropServices.ComTypes.FILETIME LastWriteTime;
        public uint VolumeSerialNumber;
        public uint FileSizeHigh;
        public uint FileSizeLow;
        public uint NumberOfLinks;
        public uint FileIndexHigh;
        public uint FileIndexLow;
    }

    public enum EMoveMethod : uint
    {
        Begin = 0,
        Current = 1,
        End = 2
    }

    internal enum FileType : uint
    {
        FileTypeChar = 0x0002,
        FileTypeDisk = 0x0001,
        FileTypePipe = 0x0003,
        FileTypeRemote = 0x8000,
        FileTypeUnknown = 0x0000,
    }

    internal enum ErrorCode : uint
    {
        FileNotFound = 2
    }

    internal enum Protection : uint
    {
        NoAccess = 0x01,
        ReadOnly = 0x02,
        ReadWrite = 0x04,
        WriteCopy = 0x08,
        Execute = 0x10,
        ExecuteRead = 0x20,
        ExecuteReadWrite = 0x40,
        ExecuteWriteCopy = 0x80,
        Guard = 0x100,
        NoCache = 0x200,
        WriteCombine = 0x400
    }
}