using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Win32.SafeHandles;
using SevenZip;

namespace TempleExtender
{
    internal class Redirection
    {
        public string From { get; set; }

        public string To { get; set; }

        public int Depth
        {
            get
            {
                var depth = 0;
                for (var i = 0; i < From.Length; ++i)
                    if (From[i] == '\\')
                        depth++;
                return depth;
            }
        }
    }

    internal class VirtualHandle
    {
        public MemoryStream Stream { get; set; }
        public string Filename { get; set; }
        public ArchiveFileInfo ArchiveInfo { get; set; }
    }

    internal class ActiveSearch
    {
        public IEnumerator<WIN32_FIND_DATA> Iterator { get; private set; }

        public ActiveSearch(IEnumerable<WIN32_FIND_DATA> results)
        {
            Iterator = results.GetEnumerator();
        }
    }

    internal class FilesystemRedirector : IDisposable
    {
        #region Delegates

        [UnmanagedFunctionPointer(CallingConvention.Winapi, CharSet = CharSet.Ansi)]
        private delegate IntPtr FindFirstFile(string filename, IntPtr findFileData);

        private FindFirstFile _findFirstFile;

        [UnmanagedFunctionPointer(CallingConvention.Winapi, CharSet = CharSet.Ansi)]
        private delegate bool FindNextFile(IntPtr handle, IntPtr findFileData);

        private FindNextFile _findNextFile;

        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        private delegate bool FindClose(IntPtr handle);

        private FindClose _findClose;

        [UnmanagedFunctionPointer(CallingConvention.Winapi, CharSet = CharSet.Ansi)]
        private delegate bool CreateDirectoryDelegate(string lpPathName, IntPtr lpSecurityAttributes);

        private CreateDirectoryDelegate _createDirectoryDelegate;

        [UnmanagedFunctionPointer(CallingConvention.Winapi, CharSet = CharSet.Ansi)]
        private delegate bool DeleteFileDelegate(string lpPathName);

        private DeleteFileDelegate _deleteFileDelegate;

        [UnmanagedFunctionPointer(CallingConvention.Winapi, CharSet = CharSet.Ansi)]
        private delegate bool RemoveDirectoryDelegate(string lpPathName);

        private RemoveDirectoryDelegate _removeDirectoryDelegate;

        [UnmanagedFunctionPointer(CallingConvention.Winapi, CharSet = CharSet.Ansi)]
        private delegate bool MoveFileDelegate(string fromPath, string toPath);

        private MoveFileDelegate _moveFileDelegate;

        private delegate bool ReadFileDelegate(IntPtr hFile, IntPtr lpBuffer, uint nNumberOfBytesToRead,
                                               IntPtr lpNumberOfBytesRead, IntPtr lpOverlapped);

        private ReadFileDelegate _readFileDelegate;

        [UnmanagedFunctionPointer(CallingConvention.Winapi, CharSet = CharSet.Ansi)]
        private delegate int GetFileAttributesDelegate(string path);

        private GetFileAttributesDelegate _getFileAttributesDelegate;

        private delegate bool CloseHandleDelegate(IntPtr hObject);

        private CloseHandleDelegate _closeHandleDelegate;

        [UnmanagedFunctionPointer(CallingConvention.Winapi, CharSet = CharSet.Ansi)]
        private delegate IntPtr CreateFileADelegate(
            string lpFileName,
            uint dwDesiredAccess,
            uint dwShareMode,
            IntPtr securityAttributes,
            uint dwCreationDisposition,
            uint dwFlagsAndAttributes,
            IntPtr hTemplateFile
            );

        private CreateFileADelegate _createFileDelegate;

        private delegate int SetFilePointerDelegate(IntPtr hFile, int lDistanceToMove, IntPtr lpDistanceToMoveHigh,
                                                    EMoveMethod dwMoveMethod);

        private SetFilePointerDelegate _setFilePointerDelegate;

        private delegate FileType GetFileTypeDelegate(IntPtr hFile);

        private GetFileTypeDelegate _getFileTypeDelegate;

        private delegate bool GetFileInformationByHandleDelegate(
            IntPtr hFile, out BY_HANDLE_FILE_INFORMATION lpFileInformation);

        private GetFileInformationByHandleDelegate _getFileInformationByHandleDelegate;

        private delegate bool SetEndOfFileDelegate(IntPtr hFile);

        private SetEndOfFileDelegate _setEndOfFileDelegate;

        [UnmanagedFunctionPointer(CallingConvention.Cdecl, CharSet=CharSet.Ansi)]
        private delegate int TioPathAdd(string path);

        private TioPathAdd _tioPathAdd;

        private TioPathAdd _originalTioPathAdd;

        #endregion

        private readonly Settings _settings;

        private readonly SevenZipExtractor _extractor;

        private readonly List<Redirection> _redirections = new List<Redirection>();

        private readonly Dictionary<IntPtr, ActiveSearch> _activeSearches = new Dictionary<IntPtr, ActiveSearch>();

        private readonly Dictionary<IntPtr, VirtualHandle> _virtualHandles = new Dictionary<IntPtr, VirtualHandle>();

        private readonly Dictionary<string, int> _archiveFiles = new Dictionary<string, int>();

        public FilesystemRedirector(TempleLibrary library, Settings settings)
        {
            _settings = settings;

            LoadRedirections(settings);
            InitializeDelegates();

            var iat = library.ImportAddressTable;

            var tioHandle = Win32Api.GetModuleHandle("tio.dll");
            var tioIat = new ImportAddressTable(tioHandle);

            var binkHandle = Win32Api.GetModuleHandle("binkw32.dll");
            var binkIat = new ImportAddressTable(binkHandle);

            var msvcrHandle = Win32Api.GetModuleHandle("msvcr71.dll");
            var msvcrIat = new ImportAddressTable(msvcrHandle);

            var pythonHandle = Win32Api.GetModuleHandle("pyToEE22.dll");
            var pythonIat = new ImportAddressTable(pythonHandle);

            var pathAddImport = iat.GetImportedFunction("tio.dll", "tio_path_add");
            var tioPathAdd = Win32Api.GetProcAddress(tioHandle, "tio_path_add");
            _originalTioPathAdd = (TioPathAdd) Marshal.GetDelegateForFunctionPointer(tioPathAdd, 
                                                                                     typeof (TioPathAdd));
            pathAddImport.ReplaceFunction(_tioPathAdd);
            /*
            iat.ReplaceFunction("kernel32.dll", "CreateFileA", _createFileDelegate);
            tioIat.ReplaceFunction("kernel32.dll", "CreateFileA", _createFileDelegate);
            binkIat.ReplaceFunction("kernel32.dll", "CreateFileA", _createFileDelegate);
            msvcrIat.ReplaceFunction("kernel32.dll", "CreateFileA", _createFileDelegate);

            iat.ReplaceFunction("kernel32.dll", "CloseHandle", _closeHandleDelegate);
            tioIat.ReplaceFunction("kernel32.dll", "CloseHandle", _closeHandleDelegate);
            binkIat.ReplaceFunction("kernel32.dll", "CloseHandle", _closeHandleDelegate);
            msvcrIat.ReplaceFunction("kernel32.dll", "CloseHandle", _closeHandleDelegate);

            tioIat.ReplaceFunction("kernel32.dll", "ReadFile", _readFileDelegate);
            iat.ReplaceFunction("kernel32.dll", "ReadFile", _readFileDelegate);
            binkIat.ReplaceFunction("kernel32.dll", "ReadFile", _readFileDelegate);
            msvcrIat.ReplaceFunction("kernel32.dll", "ReadFile", _readFileDelegate);

            tioIat.ReplaceFunction("kernel32.dll", "GetFileType", _getFileTypeDelegate);
            iat.ReplaceFunction("kernel32.dll", "GetFileType", _getFileTypeDelegate);
            binkIat.ReplaceFunction("kernel32.dll", "GetFileType", _getFileTypeDelegate);
            msvcrIat.ReplaceFunction("kernel32.dll", "GetFileType", _getFileTypeDelegate);

            tioIat.ReplaceFunction("kernel32.dll", "SetFilePointer", _setFilePointerDelegate);
            iat.ReplaceFunction("kernel32.dll", "SetFilePointer", _setFilePointerDelegate);
            binkIat.ReplaceFunction("kernel32.dll", "SetFilePointer", _setFilePointerDelegate);
            msvcrIat.ReplaceFunction("kernel32.dll", "SetFilePointer", _setFilePointerDelegate);

            tioIat.ReplaceFunction("kernel32.dll", "SetEndOfFile", _setEndOfFileDelegate);
            iat.ReplaceFunction("kernel32.dll", "SetEndOfFile", _setEndOfFileDelegate);
            msvcrIat.ReplaceFunction("kernel32.dll", "SetEndOfFile", _setEndOfFileDelegate);

            tioIat.ReplaceFunction("kernel32.dll", "GetFileInformationByHandle", _getFileInformationByHandleDelegate);
            msvcrIat.ReplaceFunction("kernel32.dll", "GetFileInformationByHandle", _getFileInformationByHandleDelegate);

            iat.ReplaceFunction("kernel32.dll", "FindFirstFileA", _findFirstFile);
            tioIat.ReplaceFunction("kernel32.dll", "FindFirstFileA", _findFirstFile);
            tioIat.ReplaceFunction("kernel32.dll", "FindNextFileA", _findNextFile);
            tioIat.ReplaceFunction("kernel32.dll", "FindClose", _findClose);

            msvcrIat.ReplaceFunction("kernel32.dll", "FindFirstFileA", _findFirstFile);
            msvcrIat.ReplaceFunction("kernel32.dll", "FindNextFileA", _findNextFile);
            msvcrIat.ReplaceFunction("kernel32.dll", "FindClose", _findClose);

            pythonIat.ReplaceFunction("kernel32.dll", "FindFirstFileA", _findFirstFile);
            pythonIat.ReplaceFunction("kernel32.dll", "FindNextFileA", _findNextFile);
            pythonIat.ReplaceFunction("kernel32.dll", "FindClose", _findClose);

            tioIat.ReplaceFunction("kernel32.dll", "CreateDirectoryA", _createDirectoryDelegate);

            tioIat.ReplaceFunction("kernel32.dll", "DeleteFileA", _deleteFileDelegate);
            
            tioIat.ReplaceFunction("kernel32.dll", "RemoveDirectoryA", _removeDirectoryDelegate);

            tioIat.ReplaceFunction("kernel32.dll", "MoveFileA", _moveFileDelegate);

            tioIat.ReplaceFunction("kernel32.dll", "GetFileAttributesA", _getFileAttributesDelegate);
            msvcrIat.ReplaceFunction("kernel32.dll", "GetFileAttributesA", _getFileAttributesDelegate);*/

            /*
            var dllPath = Path.Combine(settings.InstallationDirectory, "7z.dll");
            SevenZipBase.SetLibraryPath(dllPath);

            if (settings.MountedArchives.Count > 0)
            {
                var archivePath = settings.MountedArchives[0];
                _extractor = new SevenZipExtractor(archivePath);

                foreach (var entry in _extractor.ArchiveFileData)
                {
                    var filename = NormalizeFileName(entry.FileName);
                    _archiveFiles.Add(filename, entry.Index);
                }
            }*/
        }

        private void InitializeDelegates()
        {
            _createFileDelegate = new CreateFileADelegate(CreateFileHook);
            _createDirectoryDelegate = new CreateDirectoryDelegate(CreateDirectoryHook);
            _deleteFileDelegate = new DeleteFileDelegate(DeleteFileHook);
            _removeDirectoryDelegate = new RemoveDirectoryDelegate(RemoveDirectoryHook);
            _moveFileDelegate = new MoveFileDelegate(MoveFileHook);
            _readFileDelegate = new ReadFileDelegate(ReadFileHook);
            _closeHandleDelegate = new CloseHandleDelegate(CloseHandleHook);
            _getFileAttributesDelegate = new GetFileAttributesDelegate(GetFileAttributesHook);
            _getFileTypeDelegate = new GetFileTypeDelegate(GetFileTypeHook);
            _setFilePointerDelegate = new SetFilePointerDelegate(SetFilePointerHook);
            _getFileInformationByHandleDelegate = new GetFileInformationByHandleDelegate(GetFileInformationByHandleHook);
            _setEndOfFileDelegate = new SetEndOfFileDelegate(SetEndOfFileHook);
            _findFirstFile = new FindFirstFile(FindFirstFileHook);
            _findNextFile = new FindNextFile(FindNextFileHook);
            _findClose = new FindClose(FindCloseHook);
            _tioPathAdd = new TioPathAdd(TioPathAddHook);
        }

        private int TioPathAddHook(string path)
        {
            var pathLower = path.ToLowerInvariant();

            Console.Write("Adding TIO Path: " + path + "...");
            var result = _originalTioPathAdd(path);
            Console.WriteLine(" Result=" + result);

            if (pathLower == @"data")
            {
                Console.WriteLine("--------------- [Begin] Custom Data Archives ---------------------");
                foreach (var archive in _settings.DataArchives)
                {
                    Console.Write("Adding archive " + archive + "...");
                    var customResult = _originalTioPathAdd(archive);
                    Console.WriteLine(" Result=" + customResult);
                }
                Console.WriteLine("--------------- [End] Custom Data Archives ---------------------");
            }
            else if (pathLower == @".\modules\toee.dat")
            {
                Console.WriteLine("--------------- [Begin] Custom Module Archives ---------------------");
                foreach (var archive in _settings.ModuleArchives)
                {
                    Console.Write("Adding archive " + archive + "...");
                    var customResult = _originalTioPathAdd(archive);
                    Console.WriteLine(" Result=" + customResult);
                }
                Console.WriteLine("--------------- [End] Custom Module Archives ---------------------");
            }

            
            return result;
        }

        private bool SetEndOfFileHook(IntPtr hfile)
        {
            VirtualHandle vHandle;

            if (_virtualHandles.TryGetValue(hfile, out vHandle))
            {
#if TRACE
                Console.WriteLine("Setting end of virtual file: " + vHandle.Filename);
#endif
            }

            return Win32Api.SetEndOfFile(hfile);
        }

        private bool GetFileInformationByHandleHook(IntPtr hfile, out BY_HANDLE_FILE_INFORMATION lpfileinformation)
        {
            VirtualHandle vHandle;

            if (_virtualHandles.TryGetValue(hfile, out vHandle))
            {
                var archiveInfo = vHandle.ArchiveInfo;

                var creationTime = archiveInfo.CreationTime.ToFileTime();
                var accessTime = archiveInfo.LastWriteTime.ToFileTime();
                var modificationTime = archiveInfo.LastWriteTime.ToFileTime();
                var fileSize = archiveInfo.Size;

                lpfileinformation = new BY_HANDLE_FILE_INFORMATION
                                        {
                                            FileAttributes = archiveInfo.Attributes,
                                            CreationTime =
                                                {
                                                    dwHighDateTime = (int) (creationTime >> 32),
                                                    dwLowDateTime = (int) (creationTime & 0xFFFFFFFF)
                                                },
                                            LastAccessTime =
                                                {
                                                    dwHighDateTime = (int) (accessTime >> 32),
                                                    dwLowDateTime = (int) (accessTime & 0xFFFFFFFF)
                                                },
                                            LastWriteTime =
                                                {
                                                    dwHighDateTime = (int) (modificationTime >> 32),
                                                    dwLowDateTime = (int) (modificationTime & 0xFFFFFFFF)
                                                },
                                            VolumeSerialNumber = 123445,
                                            FileSizeHigh = (uint) (fileSize >> 32),
                                            FileSizeLow = (uint) (fileSize & 0xFFFFFFFF),
                                            NumberOfLinks = 1,
                                            FileIndexHigh = 0,
                                            FileIndexLow = (uint) archiveInfo.Index
                                        };

#if TRACE
                Console.WriteLine("Get file information by handle for virtual file: " + vHandle.Filename);
#endif
                return true;
            }

            return Win32Api.GetFileInformationByHandle(hfile, out lpfileinformation);
        }

        private int SetFilePointerHook(IntPtr hfile, int ldistancetomove, IntPtr lpdistancetomovehigh,
                                       EMoveMethod dwmovemethod)
        {
            VirtualHandle handle;

            if (_virtualHandles.TryGetValue(hfile, out handle))
            {
#if TRACE
                Console.WriteLine("Seeking in virtual file " + handle.Filename + " Dir: " + dwmovemethod + " Dist: " + ldistancetomove + " High: " + lpdistancetomovehigh);
#endif

                switch (dwmovemethod)
                {
                    case EMoveMethod.Begin:
                        handle.Stream.Seek(ldistancetomove, SeekOrigin.Begin);
                        break;
                    case EMoveMethod.Current:
                        handle.Stream.Seek(ldistancetomove, SeekOrigin.Current);
                        break;
                    case EMoveMethod.End:
                        handle.Stream.Seek(ldistancetomove, SeekOrigin.End);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException("dwmovemethod");
                }

                return (int) handle.Stream.Position;
            }

            return Win32Api.SetFilePointer(hfile, ldistancetomove, lpdistancetomovehigh, dwmovemethod);
        }

        private FileType GetFileTypeHook(IntPtr hFile)
        {
            if (_virtualHandles.ContainsKey(hFile))
                return FileType.FileTypeDisk;

            return Win32Api.GetFileType(hFile);
        }

        private bool CloseHandleHook(IntPtr hobject)
        {
            VirtualHandle vHandle;

            if (_virtualHandles.TryGetValue(hobject, out vHandle))
            {
#if TRACE
                Console.WriteLine("Closing virtual file: " + vHandle.Filename);
#endif

                vHandle.Stream.Dispose();
                _virtualHandles.Remove(hobject);
                return true;
            }

            return Win32Api.CloseHandle(hobject);
        }

        private int GetFileAttributesHook(string path)
        {
            Console.WriteLine("Get file attributes: " + path);
            return Win32Api.GetFileAttributes(path);
        }

        private bool ReadFileHook(IntPtr hFile, IntPtr lpBuffer, uint nNumberOfBytesToRead,
                                  IntPtr lpNumberOfBytesRead, IntPtr lpOverlapped)
        {
            if (lpOverlapped != IntPtr.Zero)
                Console.WriteLine("[WARN] Overlapped I/O");

            VirtualHandle vHandle;

            if (_virtualHandles.TryGetValue(hFile, out vHandle))
            {
                var buffer = new byte[nNumberOfBytesToRead];
                var actualRead = vHandle.Stream.Read(buffer, 0, (int) nNumberOfBytesToRead);

                Marshal.Copy(buffer, 0, lpBuffer, actualRead);
                if (lpNumberOfBytesRead != IntPtr.Zero)
                    Marshal.WriteInt32(lpNumberOfBytesRead, actualRead);

                return true;
            }

            return Win32Api.ReadFile(hFile, lpBuffer, nNumberOfBytesToRead, lpNumberOfBytesRead, lpOverlapped);
        }

        private bool MoveFileHook(string fromPath, string toPath)
        {
            fromPath = RewriteFilename(fromPath, "MoveFile");
            toPath = RewriteFilename(toPath, "MoveFile");

            if (_settings.WriteAccessVirtualization && !Path.IsPathRooted(fromPath) && !Path.IsPathRooted(toPath))
            {
                var newPath = Path.Combine(_settings.VirtualWriteDirectory, fromPath);
                Console.WriteLine("Redirecting MoveFile {0} -> {1}", fromPath, newPath);
                fromPath = newPath;

                newPath = Path.Combine(_settings.VirtualWriteDirectory, toPath);
                Console.WriteLine("Redirecting MoveFile {0} -> {1}", toPath, newPath);
                toPath = newPath;
            }

            return Win32Api.MoveFile(fromPath, toPath);
        }

        private bool RemoveDirectoryHook(string path)
        {
            path = RewriteFilename(path, "RemoveDirectory");

            if (_settings.WriteAccessVirtualization && !Path.IsPathRooted(path))
            {
                var newPath = Path.Combine(_settings.VirtualWriteDirectory, path);
                Console.WriteLine("Redirecting RemoveDirectory {0} -> {1}", path, newPath);
                path = newPath;
            }

            return Win32Api.RemoveDirectory(path);
        }

        private bool DeleteFileHook(string path)
        {
            path = RewriteFilename(path, "DeleteFile");

            if (_settings.WriteAccessVirtualization && !Path.IsPathRooted(path))
            {
                var newPath = Path.Combine(_settings.VirtualWriteDirectory, path);
                Console.WriteLine("Redirecting DeleteFile {0} -> {1}", path, newPath);
                path = newPath;
            }

            return Win32Api.DeleteFile(path);
        }

        private static WIN32_FIND_DATA ArchiveInfoToFindData(ArchiveFileInfo fileInfo)
        {
            var creationTime = fileInfo.CreationTime.ToFileTime();
            var writeTime = fileInfo.LastWriteTime.ToFileTime();

            var result = new WIN32_FIND_DATA()
                             {
                                 dwFileAttributes = fileInfo.Attributes,
                                 cFileName = Path.GetFileName(fileInfo.FileName),
                                 ftCreationTime =
                                     {
                                         dwHighDateTime = (int) (creationTime >> 32),
                                         dwLowDateTime = (int) (creationTime & 0xFFFFFFFF)
                                     },
                                 ftLastWriteTime =
                                     {
                                         dwHighDateTime = (int) (writeTime >> 32),
                                         dwLowDateTime = (int) (writeTime & 0xFFFFFFFF)
                                     },
                                 nFileSizeHigh = (uint) (fileInfo.Size >> 32),
                                 nFileSizeLow = (uint) (fileInfo.Size & 0xFFFFFFFF),
                             };

            return result;
        }

        public static string WildcardToRegex(string pattern)
        {
            return "^" + Regex.Escape(pattern).Replace("\\*", ".*").Replace("\\?", ".") + "$";
        }

        private IntPtr FindFirstFileHook(string fileName, IntPtr findFileDataPtr)
        {
#if TRACE
            Console.WriteLine("Find File: " + fileName);
#endif

            fileName = RewriteFilename(fileName, "FindFirstFile");

            var key = NormalizeFileName(fileName);

            var results = new List<WIN32_FIND_DATA>();

            /*
             * Search in directory overlays
             */
            if (!Path.IsPathRooted(fileName))
            {
                // Search for files in the virtual write directory
                if (_settings.WriteAccessVirtualization)
                {
                    var path = Path.Combine(_settings.VirtualWriteDirectory, fileName);
                    FindFilesWin32(path, results);
                }

                // Search for files in other directories
                foreach (var overlay in _settings.DirectoryOverlays)
                {
                    var path = Path.Combine(overlay, fileName);
                    FindFilesWin32(path, results);
                }

                if (_archiveFiles.ContainsKey(key))
                {
                    var index = _archiveFiles[key];
                    results.Add(ArchiveInfoToFindData(_extractor.ArchiveFileData[index]));
                } else if (key.Contains("*")) {
                    var regex = new Regex(WildcardToRegex(NormalizeFileName(key)));

                    foreach (var entry in _archiveFiles)
                    {
                        if (regex.IsMatch(entry.Key))
                        {
                            var archiveFileInfo = _extractor.ArchiveFileData[entry.Value];
                            results.Add(ArchiveInfoToFindData(archiveFileInfo));
#if TRACE
                            Console.WriteLine("Adding match from archive: " + archiveFileInfo.FileName);
#endif
                        }
                    }
                }
            }

            FindFilesWin32(fileName, results);

            if (results.Count == 0)
                return Win32Api.InvalidHandle;

            var handle = new IntPtr(1);
            while (_activeSearches.ContainsKey(handle))
                handle = IntPtrUtil.Add(handle, 1);

            var search = new ActiveSearch(results);
            _activeSearches[handle] = search;

            search.Iterator.MoveNext();
            Marshal.StructureToPtr(search.Iterator.Current, findFileDataPtr, false);

            return handle;
        }

        private static void FindFilesWin32(string fileName, ICollection<WIN32_FIND_DATA> results)
        {
            var findFileDataPtr = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(WIN32_FIND_DATA)));

            var orgHandle = Win32Api.FindFirstFileA(fileName, findFileDataPtr);
            if (orgHandle != Win32Api.InvalidHandle)
            {
                var findData = (WIN32_FIND_DATA) Marshal.PtrToStructure(findFileDataPtr, typeof (WIN32_FIND_DATA));
                results.Add(findData);

                while (Win32Api.FindNextFileA(orgHandle, findFileDataPtr))
                {
                    findData = (WIN32_FIND_DATA) Marshal.PtrToStructure(findFileDataPtr, typeof (WIN32_FIND_DATA));

                    // Prevent duplicates
                    var found = false;
                    foreach (var entry in results)
                        if (entry.cFileName == findData.cFileName)
                        {
                            found = true;
                            break;
                        }

                    if (!found)
                        results.Add(findData);
                    else
                    {
#if TRACE
                        Console.WriteLine("Prevented double entry: " + findData.cFileName);
#endif
                    }
                }

                Win32Api.FindClose(orgHandle);
            }

            Marshal.FreeHGlobal(findFileDataPtr);

        }

        private bool FindNextFileHook(IntPtr handle, IntPtr findFileDataPtr)
        {
            ActiveSearch search;

            if (!_activeSearches.TryGetValue(handle, out search))
                return false;

            if (!search.Iterator.MoveNext())
                return false;

            Marshal.StructureToPtr(search.Iterator.Current, findFileDataPtr, false);
            return true;
        }

        private bool FindCloseHook(IntPtr handle)
        {
            return _activeSearches.Remove(handle);
        }

        private IntPtr CreateFileHook(string fileName, uint dwDesiredAccess, uint b, IntPtr c, uint d, uint e, IntPtr f)
        {
            if (_settings.DisableIntroMovies && IsIntroMovie(fileName))
            {
                var blankBikPath = Path.Combine(_settings.InstallationDirectory, "blank.bik");
                fileName = blankBikPath;
            }
            else
            {
                fileName = RewriteFilename(fileName, "CreateFile");
            }

            // If filename rewriting lead to an absolute path, we'll use CreateFile directly.
            if (!Path.IsPathRooted(fileName))
            {
                // Writing to files will only involve filename rewriting, but never virtual handles
                if ((dwDesiredAccess & 0x40000000) != 0)
                {
                    if (_settings.WriteAccessVirtualization)
                    {
                        var newFileName = Path.Combine(_settings.VirtualWriteDirectory, fileName);
                        Console.WriteLine("Redirecting write-access {0} -> {1}", fileName, newFileName);
                        fileName = newFileName;

                        /*
                         * The game may attempt to write to a file that is in a read-only directory. We
                         * need to re-create the directory structure so this doesn't lead to errors.
                         */
                        var path = Path.GetDirectoryName(fileName);
                        if (path != null)
                            Directory.CreateDirectory(path);
                    }
                } else {
                    var key = NormalizeFileName(fileName);

                    int archiveIndex;
                    if (_archiveFiles.TryGetValue(key, out archiveIndex))
                    {
#if TRACE
                        Console.WriteLine("Opening file that is in archive: " + key);
#endif

                        return CreateVirtualHandle(key, archiveIndex);
                    }
                }
            }

            return Win32Api.CreateFileA(fileName, dwDesiredAccess, b, c, d, e, f);
        }

        private IntPtr CreateVirtualHandle(string key, int archiveIndex)
        {
            var handle = new IntPtr(0x7FFF0000);

            while (_virtualHandles.ContainsKey(handle))
                handle = IntPtrUtil.Add(handle, 1);

            var archiveInfo = _extractor.ArchiveFileData[archiveIndex];
            var stream = new MemoryStream((int) archiveInfo.Size);
            _extractor.ExtractFile(archiveIndex, stream);
            stream.Seek(0, SeekOrigin.Begin);

            _virtualHandles[handle] = new VirtualHandle {Filename = key, Stream = stream, ArchiveInfo = archiveInfo};

            return handle;
        }

        private bool CreateDirectoryHook(string path, IntPtr lpSecurityAttributes)
        {
            var key = NormalizeFileName(path);

            foreach (var redirection in _redirections)
            {
                if (key.StartsWith(redirection.From))
                {
                    path = redirection.To + key.Substring(redirection.From.Length);
                    break;
                }
            }

            Console.WriteLine("CreateDirectory: " + path);

            if (_settings.WriteAccessVirtualization && !Path.IsPathRooted(path))
            {
                var newPath = Path.Combine(_settings.VirtualWriteDirectory, path);
                Console.WriteLine("Redirecting CreateDirectory {0} -> {1}", path, newPath);
                path = newPath;
            }

            return Win32Api.CreateDirectory(path, lpSecurityAttributes);
        }

        private string RewriteFilename(string fileName, string methodName)
        {
            var key = NormalizeFileName(fileName);

            foreach (var redirection in _redirections)
            {
                if (key.StartsWith(redirection.From))
                {
                    var rewriteFilename = redirection.To + key.Substring(redirection.From.Length);
#if TRACE
                    Console.WriteLine(methodName + " REWRITES " + fileName + " -> " + rewriteFilename);
#endif
                    return rewriteFilename;
                }
            }

            /*
             * Overlays work slightly different from redirections, they are only used in case the
             * target filename exists in the overlay directory.
             */
            foreach (var overlay in _settings.DirectoryOverlays)
            {
                var targetPath = Path.Combine(overlay, key);
                if (Directory.Exists(targetPath) || File.Exists(targetPath))
                {
                    return targetPath;
                }
            }

            return fileName;
        }

        private static string NormalizeFileName(string fileName)
        {
            var normalized = fileName.ToLowerInvariant();

            // / -> \ conversion
            normalized = normalized.Replace('/', '\\');

            // Some strings start with .\ to indicate it's a relative path, strip this as well
            if (normalized.StartsWith(@".\"))
                normalized = normalized.Substring(2);

            // Strip the current working directory from the path
            var cwd = Directory.GetCurrentDirectory();

            if (normalized.StartsWith(cwd))
                normalized = normalized.Substring(cwd.Length);


            return normalized;
        }

        private void LoadRedirections(Settings settings)
        {
            foreach (var mountPoint in settings.MountedDirectories)
            {
                _redirections.Add(new Redirection
                                      {
                                          From = mountPoint.Key,
                                          To = mountPoint.Value
                                      });
            }

            // Sort redirections by depth (highest first)
            _redirections.Sort((a, b) => b.Depth.CompareTo(a.Depth));
        }


        private static bool IsIntroMovie(string filename)
        {
            filename = filename.ToLowerInvariant();

            if (!filename.StartsWith(@"data\movies\"))
                return false;

            return filename.EndsWith("atarilogo.bik")
                   || filename.EndsWith("troikalogo.bik")
                   || filename.EndsWith("introcinematic.bik")
                   || filename.EndsWith("wotclogo.bik");
        }

        public void Dispose()
        {
            _findFirstFile = null;
            _createDirectoryDelegate = null;
            _createFileDelegate = null;
            _moveFileDelegate = null;
            _removeDirectoryDelegate = null;
            _readFileDelegate = null;
            _deleteFileDelegate = null;
        }
    }
}