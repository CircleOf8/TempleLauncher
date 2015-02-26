using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;

namespace TempleExtender
{
    /// <summary>
    /// Wraps the unmanaged temple.dll library.
    /// </summary>
    internal class TempleLibrary : IDisposable
    {
        private readonly string _previousCurrentDirectory;

        public TempleLibrary(string filename)
        {
            _previousCurrentDirectory = Directory.GetCurrentDirectory();

            var directory = Path.GetDirectoryName(filename);
            if (directory != null)
                Directory.SetCurrentDirectory(directory);

            Filename = filename;

            Handle = Win32Api.LoadLibrary(filename);
            
            if (!Valid)
            {
                Error = new Win32Exception().Message;
            }
            else
            {
                ImportAddressTable = new ImportAddressTable(Handle);
            }
        }

        public string Filename { get; private set; }

        public IntPtr Handle { get; private set; }

        public bool Valid
        {
            get { return Handle != IntPtr.Zero; }
        }

        public string Error { get; private set; }

        public ImportAddressTable ImportAddressTable { get; private set; }

        #region IDisposable Members

        public void Dispose()
        {
            if (!Valid) return;

            Win32Api.FreeLibrary(Handle);
            Handle = IntPtr.Zero;

            Directory.SetCurrentDirectory(_previousCurrentDirectory);
        }

        #endregion

        // typedef int (__cdecl *TempleMainFn)(HINSTANCE hInstance, HINSTANCE hPrevInstance, LPSTR lpCmdLine, int nCmdShow);

        /// <summary>
        /// Starts the game and returns only after the game has been closed.
        /// </summary>
        /// <returns>The return code of the game's main function.</returns>
        public int StartGame(Settings settings)
        {
            var funcPtr = Win32Api.GetProcAddress(Handle, "temple_main");

            var mainFunction = (TempleMain) Marshal.GetDelegateForFunctionPointer(funcPtr, typeof (TempleMain));

            if (mainFunction == null)
                throw new Exception("Unable to find temple_main method in the library.");

            // Get the HINSTANCE of the main executing module.
            var hInstance = Win32Api.GetModuleHandle(null);

            var commandLine = "";

            if (settings.Windowed)
                commandLine = "-windowed";

            const int showNormal = 0xA;

            return mainFunction(hInstance, IntPtr.Zero, commandLine, showNormal);
        }
        
        /// <summary>
        /// Reads a portion of memory in relation to the library start.
        /// </summary>
        /// <param name="offset"></param>
        /// <param name="length"></param>
        /// <returns></returns>
        public byte[] ReadMemory(int offset, int length)
        {
            if (!Valid)
                throw new InvalidOperationException("The library is not loaded.");

            var ptr = IntPtrUtil.Add(Handle, offset);

            var result = new byte[length];

            Marshal.Copy(ptr, result, 0, length);

            return result;
        }

        /// <summary>
        /// Reads a portion of memory in relation to the library start.
        /// </summary>
        /// <param name="offset"></param>
        /// <param name="length"></param>
        /// <returns></returns>
        public void WriteMemory(int offset, byte[] data)
        {
            if (!Valid)
                throw new InvalidOperationException("The library is not loaded.");

            var ptr = IntPtrUtil.Add(Handle, offset);

            Protection oldProtection;
            Win32Api.VirtualProtect(ptr, (uint) data.Length, Protection.ReadWrite, out oldProtection);
            Marshal.Copy(data, 0, ptr, data.Length);
            Win32Api.VirtualProtect(ptr, (uint) data.Length, oldProtection, out oldProtection);
        }

        #region Nested type: TempleMain

        [UnmanagedFunctionPointer(CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        private delegate int TempleMain(IntPtr hInstance, IntPtr hPrevInstance, string lpCommandLine, int nCmdShow);

        #endregion
    }

}