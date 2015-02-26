using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace TempleExtender
{
    // ReSharper disable InconsistentNaming

    [StructLayout(LayoutKind.Sequential)]
    public struct IMAGE_DOS_HEADER
    {
        public ushort e_magic; // Magic number
        public ushort e_cblp; // Bytes on last page of file
        public ushort e_cp; // Pages in file
        public ushort e_crlc; // Relocations
        public ushort e_cparhdr; // Size of header in paragraphs
        public ushort e_minalloc; // Minimum extra paragraphs needed
        public ushort e_maxalloc; // Maximum extra paragraphs needed
        public ushort e_ss; // Initial (relative) SS value
        public ushort e_sp; // Initial SP value
        public ushort e_csum; // Checksum
        public ushort e_ip; // Initial IP value
        public ushort e_cs; // Initial (relative) CS value
        public ushort e_lfarlc; // File address of relocation table
        public ushort e_ovno; // Overlay number
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
        public ushort[] e_res1; // Reserved words
        public ushort e_oemid; // OEM identifier (for e_oeminfo)
        public ushort e_oeminfo; // OEM information; e_oemid specific
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 10)]
        public ushort[] e_res2; // Reserved words
        public int e_lfanew; // File address of new exe header
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct IMAGE_FILE_HEADER
    {
        public ushort Machine;
        public ushort NumberOfSections;
        public uint TimeDateStamp;
        public uint PointerToSymbolTable;
        public uint NumberOfSymbols;
        public ushort SizeOfOptionalHeader;
        public ushort Characteristics;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct IMAGE_DATA_DIRECTORY
    {
        public int VirtualAddress;
        public int Size;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct IMAGE_OPTIONAL_HEADER32
    {
        //
        // Standard fields.
        //
        public ushort Magic;
        public byte MajorLinkerVersion;
        public byte MinorLinkerVersion;
        public uint SizeOfCode;
        public uint SizeOfInitializedData;
        public uint SizeOfUninitializedData;
        public uint AddressOfEntryPoint;
        public uint BaseOfCode;
        public uint BaseOfData;
        //
        // NT additional fields.
        //
        public uint ImageBase;
        public uint SectionAlignment;
        public uint FileAlignment;
        public ushort MajorOperatingSystemVersion;
        public ushort MinorOperatingSystemVersion;
        public ushort MajorImageVersion;
        public ushort MinorImageVersion;
        public ushort MajorSubsystemVersion;
        public ushort MinorSubsystemVersion;
        public uint Win32VersionValue;
        public uint SizeOfImage;
        public uint SizeOfHeaders;
        public uint CheckSum;
        public ushort Subsystem;
        public ushort DllCharacteristics;
        public uint SizeOfStackReserve;
        public uint SizeOfStackCommit;
        public uint SizeOfHeapReserve;
        public uint SizeOfHeapCommit;
        public uint LoaderFlags;
        public uint NumberOfRvaAndSizes;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
        public IMAGE_DATA_DIRECTORY[] DataDirectory;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct IMAGE_NT_HEADERS
    {
        public uint Signature;
        public IMAGE_FILE_HEADER FileHeader;
        public IMAGE_OPTIONAL_HEADER32 OptionalHeader;
    }

    enum DataDictionaryId
    {
        ExportSymbols = 0,
        ImportSymbols,
        Resources,
        Exception,
        Security,
        BaseRelocation,
        Debug,
        CopyrightString,
        Unknown,
        ThreadLocalStorage,
        LoadConfiguration,
        BoundImport,
        ImportAddressTable,
        DelayImport,
        ComDescriptor
    }

    [StructLayout(LayoutKind.Explicit)]
    public struct IMAGE_IMPORT_DESCRIPTOR
    {
        [FieldOffset(0)]
        public uint Characteristics;
        [FieldOffset(0)]
        public int OriginalFirstThunk;
        [FieldOffset(4)]
        public uint TimeDateStamp;
        [FieldOffset(8)]
        public uint ForwarderChain;
        [FieldOffset(12)]
        public int Name1;
        [FieldOffset(16)]
        public int FirstThunk;
    }

    public class PeHeader
    {
        public IMAGE_DOS_HEADER DosHeader { get; private set; }

        public IMAGE_NT_HEADERS NtHeader { get; private set; }

        public PeHeader(IntPtr imageHandle)
        {
            DosHeader = (IMAGE_DOS_HEADER)Marshal.PtrToStructure(imageHandle, typeof(IMAGE_DOS_HEADER));

            var optionalHeaderPtr = IntPtrUtil.Add(imageHandle, DosHeader.e_lfanew);

            NtHeader = (IMAGE_NT_HEADERS)Marshal.PtrToStructure(optionalHeaderPtr,
                typeof(IMAGE_NT_HEADERS));
        }
    }

    public class ImportedFunction
    {
        public string Name { get; set; }
        public int Ordinal { get; set; }
        public bool ImportedByOrdinal { get; set; }
        public IntPtr FunctionPtr { get; set; }

        /// <summary>
        /// Replaces the function with a new function and returns the current function pointer.
        /// </summary>
        /// <param name="newFunction"></param>
        /// <returns></returns>
        public IntPtr ReplaceFunction(Delegate newFunction)
        {
            var oldPtr = Marshal.ReadIntPtr(FunctionPtr);

            Protection oldProtection;
            Win32Api.VirtualProtect(FunctionPtr, 4096, Protection.ReadWrite, out oldProtection);
            Marshal.WriteIntPtr(FunctionPtr, Marshal.GetFunctionPointerForDelegate(newFunction));
            Win32Api.VirtualProtect(FunctionPtr, 4096, oldProtection, out oldProtection);

            return oldPtr;
        }
    }

    class ImportedDll
    {
        public string Name { get; set; }

        public List<ImportedFunction> Imports { get; set; }

    }

    public class ImportAddressTable
    {
        private readonly IntPtr _handle;

        public ImportAddressTable(IntPtr imageHandle)
        {
            _handle = imageHandle;
            Load();
        }

        private readonly Dictionary<string, List<ImportedFunction>> Table = 
            new Dictionary<string, List<ImportedFunction>>();

        /// <summary>
        /// Retrieves an imported function from the import address table.
        /// </summary>
        /// <param name="dllName">The name of the DLL the function was imported from. Not case sensitive.</param>
        /// <param name="function">The name of the imported function.</param>
        /// <returns>The imported function or null.</returns>
        public ImportedFunction GetImportedFunction(string dllName, string function)
        {
            List<ImportedFunction> list;

            if (!Table.TryGetValue(dllName.ToLowerInvariant(), out list))
                return null;

            return list.Find(f => f.Name == function);
        }

        public IntPtr ReplaceFunction(string dllName, string function, Delegate newFunction)
        {
            var func = GetImportedFunction(dllName, function);

            if (func != null)
                return func.ReplaceFunction(newFunction);
            
            throw new ArgumentOutOfRangeException("Unknown DLL or function name: " + dllName + ", " + function);
        }

        private IEnumerable<ImportedFunction> ReadImportedFunctions(IntPtr originalFirstThunk, IntPtr firstThunk)
        {
            var result = new List<ImportedFunction>();

            var thunkData = Marshal.ReadInt32(originalFirstThunk);

            while (thunkData != 0)
            {
                var import = new ImportedFunction
                                 {
                                     ImportedByOrdinal = (thunkData & 0x80000000) != 0,
                                     FunctionPtr = firstThunk
                                 };

                if (import.ImportedByOrdinal)
                {
                    Console.WriteLine("Imported by ordinal.");
                    import.Ordinal = thunkData & (~0x7FFFFFFF);
                }
                else
                {
                    // We ignore the hint stored in the structure since it's not relevant for us
                    var importByNamePtr = IntPtrUtil.Add(_handle, thunkData);
                    import.Name = Marshal.PtrToStringAnsi(IntPtrUtil.Add(importByNamePtr, 2));
                }

                result.Add(import);

                firstThunk = IntPtrUtil.Add(firstThunk, 4);
                originalFirstThunk = IntPtrUtil.Add(originalFirstThunk, 4);
                thunkData = Marshal.ReadInt32(originalFirstThunk);
            }

            return result;
        }

        private void Load()
        {
            var header = new PeHeader(_handle);

            var imageDataDirectory = header.NtHeader.OptionalHeader.DataDirectory[(int) DataDictionaryId.ImportSymbols];

            Console.WriteLine("IAT @ " + imageDataDirectory.VirtualAddress);

            var iatStart = IntPtrUtil.Add(_handle, imageDataDirectory.VirtualAddress);

            var importDescriptor = (IMAGE_IMPORT_DESCRIPTOR) Marshal.PtrToStructure(iatStart, typeof (IMAGE_IMPORT_DESCRIPTOR));
            var importDescriptorSize = Marshal.SizeOf(typeof(IMAGE_IMPORT_DESCRIPTOR));

            while (importDescriptor.Characteristics != 0)
            {
                var namePtr = IntPtrUtil.Add(_handle, importDescriptor.Name1);
                var dllName = Marshal.PtrToStringAnsi(namePtr);
                
                if (dllName == null)
                    throw new NullReferenceException();

                dllName = dllName.ToLower();

                if (!Table.ContainsKey(dllName))
                    Table.Add(dllName, new List<ImportedFunction>());

                var orgFirstThunk = IntPtrUtil.Add(_handle, importDescriptor.OriginalFirstThunk);
                var firstThunk = IntPtrUtil.Add(_handle, importDescriptor.FirstThunk);

                Table[dllName].AddRange(ReadImportedFunctions(orgFirstThunk, firstThunk));

                // Read next IAT entry
                iatStart = IntPtrUtil.Add(iatStart, importDescriptorSize);
                importDescriptor = (IMAGE_IMPORT_DESCRIPTOR)Marshal.PtrToStructure(iatStart, typeof(IMAGE_IMPORT_DESCRIPTOR));
            }

        }
    }

    // ReSharper restore InconsistentNaming
}