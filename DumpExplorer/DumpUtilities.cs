using System;
using static DumpUtilities.DumpReader.NativeMethods;
using System.IO;
using System.Runtime.InteropServices;
using System.ComponentModel;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace DumpUtilities
{
    public class DumpReader : IDisposable
    {
        private readonly IntPtr _hFileHndleMiniDump;
        public readonly string dumpfilename;
        public readonly ulong _minidumpFileSize;
        MappingData _mappingDataCurrent;

        public DumpReader(string dumpfilename)
        {
            this.dumpfilename = dumpfilename;
            _hFileHndleMiniDump = CreateFile(
                dumpfilename,
                EFileAccess.FILE_GENERIC_READ,
                EFileShare.Read,
                lpSecurityAttributes: IntPtr.Zero,
                ECreationDisposition.OpenExisting,
                dwFlagsAndAttributes: EFileAttributes.Readonly,
                hTemplateFile: IntPtr.Zero
                );
            if (_hFileHndleMiniDump == INVALID_HANDLE_VALUE)
            {
                throw new InvalidOperationException($"Cannot open file {dumpfilename}");
            }
            _minidumpFileSize = (ulong)new FileInfo(dumpfilename).Length;
            _mappingDataCurrent._hFileMapping = CreateFileMapping(
                _hFileHndleMiniDump,
                lpFileMappingAttributes: IntPtr.Zero,
                flProtect: FileMapProtection.PageReadonly,
                dwMaximumSizeHigh: 0,
                dwMaximumSizeLow: 0,
                lpName: null
                );
        }
        public IntPtr MapRvaLocation(MINIDUMP_LOCATION_DESCRIPTOR loc)
        {
            ulong newBaseOffset = (loc.Rva / AllocationGranularity) * AllocationGranularity;
            var nLeftOver = loc.Rva - newBaseOffset;
            uint mapViewSize = AllocationGranularity * 4;
            if (newBaseOffset > uint.MaxValue)
            {
                throw new InvalidOperationException("newbase out of range");
            }
            var preferredAddress = _mappingDataCurrent._addrFileMapping;
            var fFits = loc.Rva >= _mappingDataCurrent._mapOffset &&
                        loc.Rva + loc.DataSize < _mappingDataCurrent._mapOffset + _mappingDataCurrent._mappedSize;
            if (!fFits)
            {
                if (preferredAddress != IntPtr.Zero)
                {
                    var res = UnmapViewOfFile(preferredAddress);
                    if (!res)
                    {
                        throw new InvalidOperationException("UnmapView failed");
                    }
                }
                uint hiPart = (uint)(newBaseOffset >> 32) & uint.MaxValue;
                uint loPart = (uint)newBaseOffset;
                if (loc.DataSize + nLeftOver > mapViewSize)
                {
                    mapViewSize = (uint)(loc.DataSize + nLeftOver);
                }
                if (newBaseOffset + mapViewSize >= _minidumpFileSize)
                {
                    //                    mapViewSize = Math.Min((uint)(loc.DataSize + nLeftOver), (uint)_minidumpFileSize);
                    mapViewSize = (uint)(_minidumpFileSize - newBaseOffset);
                }
                _mappingDataCurrent._addrFileMapping = MapViewOfFileEx(
                    _mappingDataCurrent._hFileMapping,
                    FILE_MAP_READ,
                    hiPart,
                    loPart,
                    mapViewSize,
                    preferredAddress);
                if (_mappingDataCurrent._addrFileMapping == IntPtr.Zero)
                {
                    if (preferredAddress != IntPtr.Zero)
                    {
                        preferredAddress = IntPtr.Zero;
                        _mappingDataCurrent._addrFileMapping = MapViewOfFileEx(
                            _mappingDataCurrent._hFileMapping,
                            FILE_MAP_READ,
                            hiPart,
                            loPart,
                            mapViewSize,
                            preferredAddress);
                    }
                    if (_mappingDataCurrent._addrFileMapping == IntPtr.Zero)
                    {
                        var lerr = Marshal.GetLastWin32Error();
                        throw new Win32Exception(lerr, $"MapViewOfFileFailed {newBaseOffset:x8} {loc.DataSize} LastErr = {lerr:x8}");
                    }
                }
                _mappingDataCurrent._mapOffset = newBaseOffset;
                _mappingDataCurrent._mappedSize = mapViewSize;
            }
            IntPtr intptrRetVal = IntPtr.Add(_mappingDataCurrent._addrFileMapping, (int)(newBaseOffset - _mappingDataCurrent._mapOffset + nLeftOver));
            return intptrRetVal;
        }
        public MINIDUMP_DIRECTORY ReadMinidumpDirectoryForStreamType(MINIDUMP_STREAM_TYPE strmType)
        {
            var initloc = new MINIDUMP_LOCATION_DESCRIPTOR() { Rva = 0, DataSize = AllocationGranularity };
            if (MapRvaLocation(initloc) == IntPtr.Zero)
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), "MapViewOfFile failed");
            }
            MINIDUMP_DIRECTORY dir = default;
            IntPtr DirPtr = IntPtr.Zero;
            var _strmPtr = IntPtr.Zero;
            var _strmSize = 0u;
            if (MiniDumpReadDumpStream(
                _mappingDataCurrent._addrFileMapping,
                strmType,
                ref DirPtr,
                ref _strmPtr,
                ref _strmSize
                ))
            {
                dir = Marshal.PtrToStructure<MINIDUMP_DIRECTORY>(DirPtr);
            }
            else
            {
                var hr = Marshal.GetHRForLastWin32Error();
            }
            return dir;
        }
        public T GetMinidumpStream<T>(MINIDUMP_STREAM_TYPE streamType)
        {
            T data = default;
            var dir = ReadMinidumpDirectoryForStreamType(streamType);
            if (dir.Location.Rva != 0)
            {
                var ptrData = MapRvaLocation(dir.Location);
                data = Marshal.PtrToStructure<T>(ptrData);
            }
            return data;
        }
        public THeader EnumerateStreamData<THeader, TData>(MINIDUMP_STREAM_TYPE strmType, Action<TData> actData)
        {
            TData entry = default;
            THeader lstHeader = default;
            var lstDir = ReadMinidumpDirectoryForStreamType(strmType);
            if (lstDir.Location.Rva != 0)
            {
                var lstPtr = MapRvaLocation(lstDir.Location);
                lstHeader = Marshal.PtrToStructure<THeader>(lstPtr);
                var nSize = (uint)Marshal.SizeOf(typeof(TData));
                // MINIDUMP_HANDLE_DATA_STREAM has 'SizeOfDescriptor'
                var descSizefld = typeof(THeader).GetFields().Where(f => f.Name == "SizeOfDescriptor").SingleOrDefault();
                if (descSizefld != null)
                {
                    nSize = (uint)descSizefld.GetValue(lstHeader);
                }
                var locrva = new MINIDUMP_LOCATION_DESCRIPTOR()
                {
                    Rva = lstDir.Location.Rva + (uint)Marshal.SizeOf(typeof(THeader)),
                    DataSize = nSize
                };
                // we need to get the "NumberOfEntries", "NumberOfThreads", etc... so we'll use Reflection for "NumberOf*" 
                var typeentry = typeof(THeader).GetFields().Where(f => f.Name.StartsWith("NumberOf")).Single();
                var numEntriesObj = typeentry.GetValue(lstHeader);
                var numEntries = 0ul;
                if (typeentry.FieldType.Name == "UInt32")
                {
                    numEntries = (uint)numEntriesObj;
                }
                else
                {
                    if (typeentry.FieldType.Name == "UInt64")
                    {
                        numEntries = (UInt64)(numEntriesObj);
                    }
                }
                for (uint i = 0; i < numEntries; i++)
                {
                    var ptr = MapRvaLocation(locrva);
                    entry = Marshal.PtrToStructure<TData>(ptr);
                    actData(entry);
                    locrva.Rva += (uint)nSize;
                    //                    yield return entry;
                }
            }
            return lstHeader;
        }

        public string GetNameFromRva(uint moduleNameRva, uint MaxLength = 600)
        {
            var str = string.Empty;
            if (moduleNameRva != 0)
            {
                var locNamePtr = MapRvaLocation(new MINIDUMP_LOCATION_DESCRIPTOR() { Rva = moduleNameRva, DataSize = MaxLength });
                str = Marshal.PtrToStringUni(IntPtr.Add(locNamePtr, 4));// skip len
            }
            return str;
        }

        public void Dispose()
        {
            UnmapViewOfFile(_mappingDataCurrent._hFileMapping);
            CloseHandle(_hFileHndleMiniDump);
        }

        struct MappingData
        {
            public IntPtr _hFileMapping;
            public IntPtr _addrFileMapping;
            public ulong _mapOffset;
            public uint _mappedSize;
            public override string ToString() => $"Addr={_addrFileMapping:x8} Off={_mapOffset:x8} Sz={_mappedSize:x8}";
        }
        public static class NativeMethods
        {
            public const int FILE_MAP_READ = 4;
            public const int FILE_MAP_WRITE = 2;
            public static readonly IntPtr INVALID_HANDLE_VALUE = new IntPtr(-1);
            public const int AllocationGranularity = 0x10000; // 64k

            [Flags]
            public enum EFileShare : uint
            {
                /// <summary>
                /// 
                /// </summary>
                None = 0x00000000,
                /// <summary>
                /// Enables subsequent open operations on an object to request read access. 
                /// Otherwise, other processes cannot open the object if they request read access. 
                /// If this flag is not specified, but the object has been opened for read access, the function fails.
                /// </summary>
                Read = 0x00000001,
                /// <summary>
                /// Enables subsequent open operations on an object to request write access. 
                /// Otherwise, other processes cannot open the object if they request write access. 
                /// If this flag is not specified, but the object has been opened for write access, the function fails.
                /// </summary>
                Write = 0x00000002,
                /// <summary>
                /// Enables subsequent open operations on an object to request delete access. 
                /// Otherwise, other processes cannot open the object if they request delete access.
                /// If this flag is not specified, but the object has been opened for delete access, the function fails.
                /// </summary>
                Delete = 0x00000004
            }

            [Flags]
            public enum EFileAccess : uint
            {
                //
                // Standard Section
                //

                AccessSystemSecurity = 0x1000000,   // AccessSystemAcl access type
                MaximumAllowed = 0x2000000,     // MaximumAllowed access type

                Delete = 0x10000,
                ReadControl = 0x20000,
                WriteDAC = 0x40000,
                WriteOwner = 0x80000,
                Synchronize = 0x100000,

                StandardRightsRequired = 0xF0000,
                StandardRightsRead = ReadControl,
                StandardRightsWrite = ReadControl,
                StandardRightsExecute = ReadControl,
                StandardRightsAll = 0x1F0000,
                SpecificRightsAll = 0xFFFF,

                FILE_READ_DATA = 0x0001,        // file & pipe
                FILE_LIST_DIRECTORY = 0x0001,       // directory
                FILE_WRITE_DATA = 0x0002,       // file & pipe
                FILE_ADD_FILE = 0x0002,         // directory
                FILE_APPEND_DATA = 0x0004,      // file
                FILE_ADD_SUBDIRECTORY = 0x0004,     // directory
                FILE_CREATE_PIPE_INSTANCE = 0x0004, // named pipe
                FILE_READ_EA = 0x0008,          // file & directory
                FILE_WRITE_EA = 0x0010,         // file & directory
                FILE_EXECUTE = 0x0020,          // file
                FILE_TRAVERSE = 0x0020,         // directory
                FILE_DELETE_CHILD = 0x0040,     // directory
                FILE_READ_ATTRIBUTES = 0x0080,      // all
                FILE_WRITE_ATTRIBUTES = 0x0100,     // all

                //
                // Generic Section
                //

                GenericRead = 0x80000000,
                GenericWrite = 0x40000000,
                GenericExecute = 0x20000000,
                GenericAll = 0x10000000,

                SPECIFIC_RIGHTS_ALL = 0x00FFFF,
                FILE_ALL_ACCESS =
                StandardRightsRequired |
                Synchronize |
                0x1FF,

                FILE_GENERIC_READ =
                StandardRightsRead |
                FILE_READ_DATA |
                FILE_READ_ATTRIBUTES |
                FILE_READ_EA |
                Synchronize,

                FILE_GENERIC_WRITE =
                StandardRightsWrite |
                FILE_WRITE_DATA |
                FILE_WRITE_ATTRIBUTES |
                FILE_WRITE_EA |
                FILE_APPEND_DATA |
                Synchronize,

                FILE_GENERIC_EXECUTE =
                StandardRightsExecute |
                  FILE_READ_ATTRIBUTES |
                  FILE_EXECUTE |
                  Synchronize
            }

            public enum ECreationDisposition : uint
            {
                /// <summary>
                /// Creates a new file. The function fails if a specified file exists.
                /// </summary>
                New = 1,
                /// <summary>
                /// Creates a new file, always. 
                /// If a file exists, the function overwrites the file, clears the existing attributes, combines the specified file attributes, 
                /// and flags with FILE_ATTRIBUTE_ARCHIVE, but does not set the security descriptor that the SECURITY_ATTRIBUTES structure specifies.
                /// </summary>
                CreateAlways = 2,
                /// <summary>
                /// Opens a file. The function fails if the file does not exist. 
                /// </summary>
                OpenExisting = 3,
                /// <summary>
                /// Opens a file, always. 
                /// If a file does not exist, the function creates a file as if dwCreationDisposition is CREATE_NEW.
                /// </summary>
                OpenAlways = 4,
                /// <summary>
                /// Opens a file and truncates it so that its size is 0 (zero) bytes. The function fails if the file does not exist.
                /// The calling process must open the file with the GENERIC_WRITE access right. 
                /// </summary>
                TruncateExisting = 5
            }

            [Flags]
            public enum EFileAttributes : uint
            {
                Readonly = 0x00000001,
                Hidden = 0x00000002,
                System = 0x00000004,
                Directory = 0x00000010,
                Archive = 0x00000020,
                Device = 0x00000040,
                Normal = 0x00000080,
                Temporary = 0x00000100,
                SparseFile = 0x00000200,
                ReparsePoint = 0x00000400,
                Compressed = 0x00000800,
                Offline = 0x00001000,
                NotContentIndexed = 0x00002000,
                Encrypted = 0x00004000,
                Write_Through = 0x80000000,
                Overlapped = 0x40000000,
                NoBuffering = 0x20000000,
                RandomAccess = 0x10000000,
                SequentialScan = 0x08000000,
                DeleteOnClose = 0x04000000,
                BackupSemantics = 0x02000000,
                PosixSemantics = 0x01000000,
                OpenReparsePoint = 0x00200000,
                OpenNoRecall = 0x00100000,
                FirstPipeInstance = 0x00080000
            }
            public const int S_FALSE = 1;
            public const int S_OK = 0;

            [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
            public static extern IntPtr CreateFile(
                    string lpFileName,
                    EFileAccess dwDesiredAccess,
                    EFileShare dwShareMode,
                    IntPtr lpSecurityAttributes,
                    ECreationDisposition dwCreationDisposition,
                    EFileAttributes dwFlagsAndAttributes,
                    IntPtr hTemplateFile
                );
            [DllImport("kernel32.dll")]
            public static extern bool CloseHandle([In] IntPtr hHandle);

            [DllImport("dbghelp.dll", SetLastError = true)]
            public static extern bool MiniDumpReadDumpStream(
                            IntPtr BaseOfDump,
                            MINIDUMP_STREAM_TYPE StreamNumber,
                            ref IntPtr DirPtr,
                            ref IntPtr StreamPointer,
                            ref uint StreamSize
                    );


            [DllImport("dbghelp.dll", SetLastError = true)]
            public static extern int SymSetHomeDirectory(
                    IntPtr hProcess,
                    string dir
                  );

            [Flags]
            public enum FileMapProtection : uint
            {
                PageReadonly = 0x02,
                PageReadWrite = 0x04,
                PageWriteCopy = 0x08,
                PageExecuteRead = 0x20,
                PageExecuteReadWrite = 0x40,
                SectionCommit = 0x8000000,
                SectionImage = 0x1000000,
                SectionNoCache = 0x10000000,
                SectionReserve = 0x4000000,
            }
            [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
            public static extern IntPtr CreateFileMapping(
                IntPtr hFile,
                IntPtr lpFileMappingAttributes,
                FileMapProtection flProtect,
                uint dwMaximumSizeHigh,
                uint dwMaximumSizeLow,
                [MarshalAs(UnmanagedType.LPStr)] string lpName);

            [DllImport("kernel32.dll")]
            public static extern IntPtr MapViewOfFileEx(
               IntPtr hFileMappingObject,
               uint dwDesiredAccess,
               uint dwFileOffsetHigh,
               uint dwFileOffsetLow,
               uint dwNumberOfBytesToMap,
               IntPtr lpBaseAddress);

            [DllImport("kernel32.dll", SetLastError = true)]
            public static extern bool UnmapViewOfFile(IntPtr lpBaseAddress);

            public enum ProcessorArchitecture : ushort
            {
                PROCESSOR_ARCHITECTURE_INTEL = 0, // x86
                PROCESSOR_ARCHITECTURE_MIPS = 1,
                PROCESSOR_ARCHITECTURE_ALPHA = 2,
                PROCESSOR_ARCHITECTURE_PPC = 3,
                PROCESSOR_ARCHITECTURE_SHX = 4,
                PROCESSOR_ARCHITECTURE_ARM = 5,
                PROCESSOR_ARCHITECTURE_IA64 = 6,
                PROCESSOR_ARCHITECTURE_ALPHA64 = 7,
                PROCESSOR_ARCHITECTURE_MSIL = 8,
                PROCESSOR_ARCHITECTURE_AMD64 = 9,
                PROCESSOR_ARCHITECTURE_IA32_ON_WIN64 = 10
            }

            [StructLayout(LayoutKind.Sequential)]
            public struct MINIDUMP_SYSTEM_INFO
            {
                public ProcessorArchitecture processorArchitecture;
                public ushort processorLevel;
                public ushort processorRevision;
                public byte NumberOfProcessors;
                public byte ProductType;
                public uint MajorVersion;
                public uint MinorVersion;
                public uint BuildNumber;
                public PlatformID PlatformID;
                // RVA to a CSDVersion string in the string table.
                // This would be a string like "Service Pack 1".
                public uint CSDVersionRva;
                public override string ToString() => $"Arch = {processorArchitecture} ProcLev = {processorLevel} ProcRev={processorRevision} NumProc ={NumberOfProcessors} ProdType={ProductType} VerMaj = {MajorVersion} VerMin = {MinorVersion} BuildNum = {BuildNumber} PlatId = {PlatformID}";
            }

            [Flags]
            public enum _MINIDUMP_TYPE
            {
                MiniDumpNormal = 0x0,
                MiniDumpWithDataSegs = 0x1,
                MiniDumpWithFullMemory = 0x2,
                MiniDumpWithHandleData = 0x4,
                MiniDumpFilterMemory = 0x8,
                MiniDumpScanMemory = 0x10,
                MiniDumpWithUnloadedModules = 0x20,
                MiniDumpWithIndirectlyReferencedMemory = 0x40,
                MiniDumpFilterModulePaths = 0x80,
                MiniDumpWithProcessThreadData = 0x100,
                MiniDumpWithPrivateReadWriteMemory = 0x200,
                MiniDumpWithoutOptionalData = 0x400,
                MiniDumpWithFullMemoryInfo = 0x800,
                MiniDumpWithThreadInfo = 0x1000,
                MiniDumpWithCodeSegs = 0x2000,
                MiniDumpWithoutAuxiliaryState = 0x4000,
                MiniDumpWithFullAuxiliaryState = 0x8000,
                MiniDumpWithPrivateWriteCopyMemory = 0x10000,
                MiniDumpIgnoreInaccessibleMemory = 0x20000,
                MiniDumpWithTokenInformation = 0x40000,
            }
            public enum MINIDUMP_STREAM_TYPE
            {
                UnusedStream = 0,
                ReservedStream0 = 1,
                ReservedStream1 = 2,
                ThreadListStream = 3,
                ModuleListStream = 4,
                MemoryListStream = 5,
                ExceptionStream = 6,
                SystemInfoStream = 7,
                ThreadExListStream = 8,
                Memory64ListStream = 9,
                CommentStreamA = 10,
                CommentStreamW = 11,
                HandleDataStream = 12,
                FunctionTableStream = 13,
                UnloadedModuleListStream = 14,
                MiscInfoStream = 15,
                MemoryInfoListStream = 16,// '  like VirtualQuery
                ThreadInfoListStream = 17,
                HandleOperationListStream = 18,
                LastReservedStream = 0xFFFF
            }
            [StructLayout(LayoutKind.Sequential)]
            public struct MINIDUMP_LOCATION_DESCRIPTOR
            {
                public uint DataSize;
                public uint Rva; //' relative byte offset
                public override string ToString() => $"Off={Rva:x8} Sz= {DataSize:x8}";
            }
            [StructLayout(LayoutKind.Sequential)]
            public struct MINIDUMP_DIRECTORY
            {
                public MINIDUMP_STREAM_TYPE StreamType;
                public MINIDUMP_LOCATION_DESCRIPTOR Location;
                public override string ToString() => $"{StreamType} {Location}";
            }

            [StructLayout(LayoutKind.Sequential)]
            public struct MINIDUMP_MEMORY_DESCRIPTOR
            {
                public Int64 StartOfMemoryRange;
                public MINIDUMP_LOCATION_DESCRIPTOR MemoryLocDesc;
                public override string ToString() => $"StartOfMemoryRange={StartOfMemoryRange:x16} DataSize={MemoryLocDesc.DataSize:x8}";
            }
            [StructLayout(LayoutKind.Sequential)]
            public struct MINIDUMP_MEMORY_DESCRIPTOR64
            {
                public Int64 StartOfMemoryRange;
                public ulong DataSize;
                //'MINIDUMP_MEMORY_DESCRIPTOR64 is used for full-memory minidumps where all of the raw memory is sequential 
                //'   at the end of the minidump. There is no need for individual relative virtual addresses (RVAs), 
                //'   because the RVA is the base RVA plus the sum of the preceding data blocks
                public override string ToString() => $"StartOfMemoryRange={StartOfMemoryRange:x16} DataSize={DataSize:x8}";
            }

            [StructLayout(LayoutKind.Sequential)]
            public struct MINIDUMP_MEMORY_LIST
            {
                public uint NumberOfMemoryRanges;
                // array of MINIDUMP_MEMORY_DESCRIPTOR
                public override string ToString() => $"NumberOfMemoryRanges={NumberOfMemoryRanges}";
            }
            [StructLayout(LayoutKind.Sequential)]
            public struct MINIDUMP_MEMORY64_LIST
            {
                public ulong NumberOfMemoryRanges;
                public Int64 BaseRva;
                //'Note that BaseRva is the overall base RVA for the memory list. 
                //            'To locate the data for a particular descriptor, start at BaseRva and increment 
                //            '   by the size of a descriptor until you reach the descriptor.
                //            'MINIDUMP_MEMORY_DESCRIPTOR64 MemoryRanges [0];   
                public override string ToString() => $"NumberOfMemoryRanges={NumberOfMemoryRanges} BaseRva={BaseRva}";
            }

            [StructLayout(LayoutKind.Sequential)]
            public struct MINIDUMP_MEMORY_INFO_LIST
            {
                public uint SizeOfHeader;
                public uint SizeOfEntry;
                public ulong NumberOfEntries;
                public override string ToString() => $"NumberOfEntries={NumberOfEntries} SizeOfHeader={SizeOfHeader} SizeOfEntry={SizeOfEntry}";
            }
            public enum AllocationProtectEnum : uint
            {
                PAGE_EXECUTE = 0x00000010,
                PAGE_EXECUTE_READ = 0x00000020,
                PAGE_EXECUTE_READWRITE = 0x00000040,
                PAGE_EXECUTE_WRITECOPY = 0x00000080,
                PAGE_NOACCESS = 0x00000001,
                PAGE_READONLY = 0x00000002,
                PAGE_READWRITE = 0x00000004,
                PAGE_WRITECOPY = 0x00000008,
                PAGE_GUARD = 0x00000100,
                PAGE_NOCACHE = 0x00000200,
                PAGE_WRITECOMBINE = 0x00000400
            }

            public enum StateEnum : uint
            {
                MEM_COMMIT = 0x1000,
                MEM_FREE = 0x10000,
                MEM_RESERVE = 0x2000
            }

            public enum TypeEnum : uint
            {
                MEM_IMAGE = 0x1000000,
                MEM_MAPPED = 0x40000,
                MEM_PRIVATE = 0x20000
            }
            [StructLayout(LayoutKind.Sequential)]
            public struct MINIDUMP_MEMORY_INFO
            {
                public ulong BaseAddress;
                public ulong AllocationBase;
                public AllocationProtectEnum AllocationProtect;
                public uint __alignment1;
                public long RegionSize;
                public StateEnum State;
                public AllocationProtectEnum Protect;
                public TypeEnum Type;
                public uint __alignment2;
                public override string ToString() => $"BaseAddress={BaseAddress:x16} AllocationBase={AllocationBase:x16} AllocationProtect={AllocationProtect:x8} __alignment1={__alignment1} RegionSize={RegionSize:x16} State={State:x8} Protect={Protect:x8} Type={Type:x8} __alignment2={__alignment2}";
            }

            [StructLayout(LayoutKind.Sequential)]
            public struct MINIDUMP_MODULE_LIST
            {
                public uint NumberOfModules;
                //'MINIDUMP_MODULE Modules[];
                public override string ToString() => $"NumberOfModules={NumberOfModules}";
            }
            [StructLayout(LayoutKind.Sequential)]
            public struct VS_FIXEDFILEINFO
            {
                public uint dwSignature;
                public uint dwStrucVersion;
                public ushort dwFileVersionMSLo;
                public ushort dwFileVersionMSHi;
                public ushort dwFileVersionLSLo;
                public ushort dwFileVersionLSHi;
                public ushort dwProductVersionMSLo;
                public ushort dwProductVersionMSHi;
                public ushort dwProductVersionLSLo;
                public ushort dwProductVersionLSHi;
                public uint dwFileFlagsMask;
                public uint dwFileFlags;
                public uint dwFileOS;
                public uint dwFileType;
                public uint dwFileSubtype;
                public uint dwFileDateMS;
                public uint dwFileDateLS;
                public string GetVersion() => $"FileVer={dwFileVersionMSHi}.{dwFileVersionMSLo}.{dwFileVersionLSHi}.{dwFileVersionLSLo} ProdVer={dwProductVersionMSHi}.{dwProductVersionMSLo}.{dwProductVersionLSHi}.{dwProductVersionLSLo}";
            }

            [StructLayout(LayoutKind.Sequential, Pack = 4)] //, Pack = 4
            public struct MINIDUMP_MODULE
            {
                public long BaseOfImage;
                public uint SizeOfImage;
                public uint CheckSum;
                public uint TimeDateStamp;//WinDbg: Timestamp:        5AD1D42D (This is a reproducible build file hash, not a timestamp)
                public uint ModuleNameRva;
                public VS_FIXEDFILEINFO VersionInfo;
                public MINIDUMP_LOCATION_DESCRIPTOR CvRecord;
                public MINIDUMP_LOCATION_DESCRIPTOR MiscRecord;
                public long Reserved0;
                public long Reserved1;
                /// <summary>
                /// Gets TimeDateStamp as a DateTime. This is based off a 32-bit value and will overflow in 2038.
                /// This is not the same as the timestamps on the file.
                /// </summary>
                public DateTime Timestamp
                {
                    get
                    {
                        // TimeDateStamp is a unix time_t structure (32-bit value).
                        // UNIX timestamps are in seconds since January 1, 1970 UTC. It is a 32-bit number
                        // Win32 FileTimes represents the number of 100-nanosecond intervals since January 1, 1601 UTC.
                        // We can create a System.DateTime from a FileTime.
                        // 
                        // See explanation here: http://blogs.msdn.com/oldnewthing/archive/2003/09/05/54806.aspx
                        // and here http://support.microsoft.com/default.aspx?scid=KB;en-us;q167296
                        long win32FileTime = 10000000 * (long)TimeDateStamp + 116444736000000000;
                        return DateTime.FromFileTimeUtc(win32FileTime);
                    }
                }
            }

            [StructLayout(LayoutKind.Sequential)]
            public struct MINIDUMP_UNLOADED_MODULE_LIST
            {
                public uint SizeOfHeader;
                public uint SizeOfEntry;
                public uint NumberOfEntries;
                public override string ToString() => $"NumberOfEntries={NumberOfEntries} SizeOfHeader={SizeOfHeader} SizeOfEntry={SizeOfEntry}";
            }
            [StructLayout(LayoutKind.Sequential)]
            public struct MINIDUMP_UNLOADED_MODULE
            {
                public ulong BaseOfImage;
                public uint SizeOfImage;
                public uint Checksum;
                public uint TimeDateStamp;
                public uint ModuleNameRva;
                public override string ToString() => $"BaseOfImage={BaseOfImage:x16} SizeOfImage={SizeOfImage} CheckSum={Checksum} TimeDateStamp={ToDateTime(TimeDateStamp)}";
            }

            [StructLayout(LayoutKind.Sequential)]
            public struct MINIDUMP_THREAD_LIST
            {
                public uint NumberOfThreads;
                // MINIDUMP_THREAD Threads[]
                public override string ToString() => $"NumberOfThreads={NumberOfThreads}";
            }

            [StructLayout(LayoutKind.Sequential, Pack = 0)]
            public struct MINIDUMP_THREAD
            {
                public int ThreadId;
                public int SuspendCount;
                public int PriorityClass;
                public int Priority;
                public ulong Teb;
                public MINIDUMP_MEMORY_DESCRIPTOR Stack;
                public MINIDUMP_LOCATION_DESCRIPTOR ThreadContext;
                public override string ToString() => $"TID {ThreadId:x8} SusCnt: {SuspendCount} TEB: {Teb:x16}  StackStart{Stack.StartOfMemoryRange:x16} StackSize ={Stack.MemoryLocDesc.DataSize}";
            }

            [StructLayout(LayoutKind.Sequential)]
            public struct MINIDUMP_THREAD_INFO_LIST
            {
                public uint SizeOfHeader;
                public uint SizeOfEntry;
                public uint NumberOfEntries;
                public override string ToString() => $"SizeOfHeader = {SizeOfHeader} SizeOfEntry={SizeOfEntry} NumberOfEntries={NumberOfEntries}";
            }

            [StructLayout(LayoutKind.Sequential)]
            public struct MINIDUMP_THREAD_INFO
            {
                public uint ThreadId;
                public uint DumpFlags;
                public uint DumpError;
                public uint ExitStatus;
                public ulong ExitTime; //
                public ulong CreateTime;//The time when the thread was created, in 100-nanosecond intervals since January 1, 1601
                public ulong KernelTime;
                public ulong UserTime;
                public ulong StartAddress;
                public ulong Affinity;
                public override string ToString() => $"TID={ThreadId:x8} DumpFlags={DumpFlags} DumpError={DumpError} ExitStatus={ExitStatus} CreateTime={ToTimeSpan(CreateTime)} KernelTime= {ToTimeSpan(KernelTime)} UserTime = {ToTimeSpan(UserTime)} StartAddress={StartAddress:x16} Affinity={Affinity}";
            }


            [StructLayout(LayoutKind.Sequential)]
            public struct MINIDUMP_HANDLE_DATA_STREAM
            {
                public uint SizeOfHeader;
                public uint SizeOfDescriptor;
                public uint NumberOfDescriptors;
                public uint Reserved;
                public override string ToString() => $"SizeOfHeader={SizeOfHeader} SizeOfDescriptor={SizeOfDescriptor} NumberOfDescriptors={NumberOfDescriptors}";
            }
            [StructLayout(LayoutKind.Sequential)]
            public struct MINIDUMP_HANDLE_DESCRIPTOR
            {
                public ulong Handle;
                public uint TypeNameRva;
                public uint ObjectNameRva;
                public uint Attributes;
                public uint GrantedAccess;
                public uint HandleCount;
                public uint PointerCount;
                public override string ToString() => $"Handle={Handle:x16} TypeNameRva={TypeNameRva} ObjectNameRva={ObjectNameRva} Attributes={Attributes:x8} GrantedAccess={GrantedAccess} HandleCount={HandleCount} PointerCount={PointerCount}";
            }
            [StructLayout(LayoutKind.Sequential)]
            public struct MINIDUMP_HANDLE_DESCRIPTOR_2
            {
                public long Handle;
                public uint TypeNameRva;
                public uint ObjectNameRva;
                public int Attributes;
                public int GrantedAccess;
                public int HandleCount;
                public int PointerCount;
                public int ObjectInfoRva;
                public int Reserved0;
            }

            [StructLayout(LayoutKind.Sequential)]
            public struct _MINIDUMP_MISC_INFO
            {
                uint SizeOfInfo;
                uint Flags1;
                uint ProcessId;
                uint ProcessCreateTime;
                uint ProcessUserTime;
                uint ProcessKernelTime;
                public override string ToString() => $"SizeOfInfo= {SizeOfInfo} Flags1={Flags1:x8} ProcessId={ProcessId} CreateTime={ToDateTime(ProcessCreateTime)} UserTime={ToTimeSpan(ProcessUserTime)} KernelTime={ToTimeSpan(ProcessKernelTime)} ";
            }
            [StructLayout(LayoutKind.Sequential)]
            public struct _MINIDUMP_FUNCTION_TABLE_STREAM
            {
                public uint SizeOfHeader;
                public uint SizeOfDescriptor;
                public uint SizeOfNativeDescriptor;
                public uint SizeOfFunctionEntry;
                public uint NumberOfDescriptors;
                public uint SizeOfAlignPad;
                public override string ToString() => $"SizeOfHeader={SizeOfHeader} SizeOfDescriptor={SizeOfDescriptor} SizeOfNativeDescriptor={SizeOfNativeDescriptor} NumberOfDescriptors={NumberOfDescriptors} SizeOfAlignPad={SizeOfAlignPad}";
            }
            [StructLayout(LayoutKind.Sequential)]
            public struct _MINIDUMP_FUNCTION_TABLE_DESCRIPTOR
            {
                public ulong MinimumAddress;
                public ulong MaximumAddress;
                public ulong BaseAddress;
                public uint EntryCount;
                public uint SizeOfAlignPad;
                public override string ToString() => $"MinimumAddress={MinimumAddress:x16} MaximumAddress={MaximumAddress:x16} BaseAddress={BaseAddress:x16} EntryCount={EntryCount}, SizeOfAlignPad={SizeOfAlignPad}";
            }

            public static DateTime ToDateTime(ulong time)
            {
                // TimeDateStamp is a unix time_t structure (32-bit value).
                // UNIX timestamps are in seconds since January 1, 1970 UTC. It is a 32-bit number
                // Win32 FileTimes represents the number of 100-nanosecond intervals since January 1, 1601 UTC.
                // We can create a System.DateTime from a FileTime.
                // 
                // See explanation here: http://blogs.msdn.com/oldnewthing/archive/2003/09/05/54806.aspx
                // and here http://support.microsoft.com/default.aspx?scid=KB;en-us;q167296
                long win32FileTime = 10000000 * (long)time + 116444736000000000;
                return DateTime.FromFileTimeUtc(win32FileTime);
            }
            public static TimeSpan ToTimeSpan(ulong time)
            {
                // TimeDateStamp is a unix time_t structure (32-bit value).
                // UNIX timestamps are in seconds since January 1, 1970 UTC. It is a 32-bit number
                // Win32 FileTimes represents the number of 100-nanosecond intervals since January 1, 1601 UTC.
                // We can create a System.DateTime from a FileTime.
                // 
                // See explanation here: http://blogs.msdn.com/oldnewthing/archive/2003/09/05/54806.aspx
                // and here http://support.microsoft.com/default.aspx?scid=KB;en-us;q167296
                long win32FileTime = 10000000 * (long)time + 116444736000000000;
                return DateTime.FromFileTimeUtc(win32FileTime) - new DateTime(1970, 1, 1, 0, 0, 0);
            }
        }
    }
}