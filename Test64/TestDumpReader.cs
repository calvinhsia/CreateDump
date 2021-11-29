using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnitTestProject1;
using CreateDump;
using static CreateDump.MemoryDumpHelper.NativeMethods;
using static Test64.MiniDumpReader.NativeMethods;
using System.Runtime.InteropServices;
using System.ComponentModel;
using System.IO;

namespace Test64
{
    [TestClass]
    public class TestDumpReader : TestBaseClass
    {
        [TestMethod]
        public async Task DumpReaderTest()
        {
            await Task.Yield();
            var dumpfilename = @"c:\TestPssSnapshotJustTriageDumpWithSnapshot.dmp";
            //dumpfilename = @"c:\TestPssSnapshotJustDumpWithSnapshot.dmp";
            using (var dumpReader = new MiniDumpReader(dumpfilename))
            {
                Trace.WriteLine($"{dumpReader._minidumpFileSize:n0} ({dumpReader._minidumpFileSize:x8})");
                var lst = new List<MINIDUMP_DIRECTORY>();
                foreach (var strmtype in Enum.GetValues(typeof(MINIDUMP_STREAM_TYPE)))
                {
                    var dir = dumpReader.ReadStreamType((MINIDUMP_STREAM_TYPE)strmtype);
                    if (dir.Location.Rva != 0)
                    {
                        lst.Add(dir);
                    }
                }
                foreach (var dir in lst.OrderBy(d => d.Location.Rva))
                {
                    Trace.WriteLine($"   Offset={dir.Location.Rva:x8}  Sz={dir.Location.DataSize:x8}  {dir.StreamType}");

                }
                var arch = dumpReader.GetMinidumpSystemInfo();
                Trace.WriteLine(arch);
                foreach (var moddata in dumpReader.EnumerateModules())
                {
                    //                    Trace.WriteLine($"Base={moddata.moduleInfo.BaseOfImage:x8}, {moddata.ModuleName}");
                }
            }
        }
    }
    public class MiniDumpReader : IDisposable
    {
        private readonly IntPtr _hFileHndleMiniDump;
        public readonly string dumpfilename;
        public readonly ulong _minidumpFileSize;
        IntPtr _strmPtr;
        uint _strmSize;
        MappingData _mappingDataCurrent;

        public MiniDumpReader(string dumpfilename)
        {
            this.dumpfilename = dumpfilename;
            Trace.WriteLine($"Dump Reader {dumpfilename}");
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
        public IntPtr MapStream(MINIDUMP_LOCATION_DESCRIPTOR loc)
        {
            var intptrRetVal = IntPtr.Zero;
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
                    mapViewSize = Math.Min((uint)(loc.DataSize + nLeftOver), (uint)_minidumpFileSize);
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
            intptrRetVal = IntPtr.Add(_mappingDataCurrent._addrFileMapping, (int)(newBaseOffset - _mappingDataCurrent._mapOffset + nLeftOver)); // must fit in 32 bits!
            return intptrRetVal;
        }
        public MINIDUMP_DIRECTORY ReadStreamType(MINIDUMP_STREAM_TYPE strmType)
        {
            var initloc = new MINIDUMP_LOCATION_DESCRIPTOR() { Rva = 0, DataSize = AllocationGranularity };
            if (MapStream(initloc) == IntPtr.Zero)
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), "MapViewOfFile failed");
            }
            MINIDUMP_DIRECTORY dir = default;
            IntPtr DirPtr = IntPtr.Zero;
            _strmPtr = IntPtr.Zero;
            _strmSize = 0;
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
            return dir;
        }
        public MINIDUMP_SYSTEM_INFO GetMinidumpSystemInfo()
        {
            MINIDUMP_SYSTEM_INFO sysinfo = default;
            var dir = ReadStreamType(MINIDUMP_STREAM_TYPE.SystemInfoStream);
            if (dir.Location.Rva != 0)
            {
                var sysInfoStrm = MapStream(dir.Location);
                sysinfo = Marshal.PtrToStructure<MINIDUMP_SYSTEM_INFO>(sysInfoStrm);
            }
            return sysinfo;
        }
        public struct ModuleData
        {
            public string ModuleName;
            public MINIDUMP_MODULE moduleInfo;
        }
        public IEnumerable<ModuleData> EnumerateModules()
        {
            var strmDesc = ReadStreamType(MINIDUMP_STREAM_TYPE.ModuleListStream);
            var modStrm = MapStream(strmDesc.Location);
            var lst = new List<ModuleData>();

            Trace.WriteLine($"  Modules {strmDesc.Location}  {modStrm.ToInt64():x16}");
            var modulelist = Marshal.PtrToStructure<MINIDUMP_MODULE_LIST>(modStrm);
            var nDescSize = Marshal.SizeOf(typeof(MINIDUMP_MODULE)) - 4;
            var locrva = new MINIDUMP_LOCATION_DESCRIPTOR()
            {
                Rva = strmDesc.Location.Rva + (uint)Marshal.SizeOf(typeof(MINIDUMP_MODULE_LIST)),
                DataSize = (uint)nDescSize + 4
            };
            Trace.WriteLine($" # Modules = {modulelist.NumberOfModules}");
            for (uint i = 0; i < modulelist.NumberOfModules - 1; i++)
            {
                locrva.Rva += (uint)(nDescSize);
                var ptr = MapStream(locrva);
                var moduleInfo = Marshal.PtrToStructure<MINIDUMP_MODULE>(ptr);
                var moduleName = GetNameFromRva(moduleInfo.ModuleNameRva);
                Trace.WriteLine($"  {i,3} Modules {locrva}  {ptr.ToInt64():x16} ImgSz={moduleInfo.SizeOfImage:n0,10} Addr= {moduleInfo.BaseOfImage:x8}   {moduleName}");
                if (moduleName.Contains("DesignTools.RuntimeHostBase."))
                {
                    "".ToString();
                }
                var moddata = new ModuleData() { ModuleName = moduleName, moduleInfo = moduleInfo };
                lst.Add(moddata);
                //                Trace.WriteLine($"Base={moddata.moduleInfo.BaseOfImage:x8}, {}"); 
                yield return moddata;
                //                yield return new ModuleData() { ModuleName = moduleName, moduleInfo = moduleInfo };
            }
            //            return lst;
        }

        public string GetNameFromRva(uint moduleNameRva, uint MaxLength = 600)
        {
            var str = string.Empty;
            if (moduleNameRva != 0)
            {
                var locName = MapStream(new MINIDUMP_LOCATION_DESCRIPTOR() { Rva = moduleNameRva, DataSize = MaxLength });
                str = Marshal.PtrToStringUni(IntPtr.Add(locName, 4));// skip len
                                                                     //                Trace.WriteLine($"     Name {moduleNameRva:x8}  {locName.ToInt64():x16}  {str}");
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
            public const int AllocationGranularity = 0x10000; // 64k

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
                PROCESSOR_ARCHITECTURE_INTEL = 0,
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
            }
            [StructLayout(LayoutKind.Sequential)]
            public struct MINIDUMP_MEMORY_DESCRIPTOR64
            {
                public Int64 StartOfMemoryRange;
                public ulong DataSize;
                public MINIDUMP_LOCATION_DESCRIPTOR MemoryLocDesc;
                //'MINIDUMP_MEMORY_DESCRIPTOR64 is used for full-memory minidumps where all of the raw memory is sequential 
                //'   at the end of the minidump. There is no need for individual relative virtual addresses (RVAs), 
                //'   because the RVA is the base RVA plus the sum of the preceding data blocks
            }

            [StructLayout(LayoutKind.Sequential)]
            public struct MINIDUMP_MEMORY_LIST
            {
                public uint NumberOfMemoryRanges;
                // array of MINIDUMP_MEMORY_DESCRIPTOR
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
            }

            [StructLayout(LayoutKind.Sequential)]
            public struct MINIDUMP_MEMORY_INFO_LIST
            {
                public uint SizeOfHeader;
                public uint SizeOfEntry;
                public ulong NumberOfEntries;
            }
            [StructLayout(LayoutKind.Sequential)]
            public struct MINIDUMP_MEMORY_INFO
            {
                public long BaseAddress;
                public long AllocationBase;
                public uint AllocationProtect;
                public uint __alignment1;
                public long RegionSize;
                public uint State;
                public uint Protect;
                public uint Type;
                public uint __alignment2;
            }

            [StructLayout(LayoutKind.Sequential)]
            public struct MINIDUMP_MODULE_LIST
            {
                public uint NumberOfModules;
                //'MINIDUMP_MODULE Modules[];
            }
            [StructLayout(LayoutKind.Sequential)]
            public struct VS_FIXEDFILEINFO
            {
                public uint dwSignature;
                public uint dwStrucVersion;
                public uint dwFileVersionMS;
                public uint dwFileVersionLS;
                public uint dwProductVersionMS;
                public uint dwProductVersionLS;
                public uint dwFileFlagsMask;
                public uint dwFileFlags;
                public uint dwFileOS;
                public uint dwFileType;
                public uint dwFileSubtype;
                public uint dwFileDateMS;
                public uint dwFileDateLS;
            }

            [StructLayout(LayoutKind.Sequential)] //, Pack = 4
            public struct MINIDUMP_MODULE
            {
                public long BaseOfImage;
                public uint SizeOfImage;
                public uint CheckSum;
                public uint TimeDateStamp;
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
            public struct MINIDUMP_THREAD_LIST
            {
                public int NumberOfThreads;
                // MINIDUMP_THREAD Threads[]
            }

            [StructLayout(LayoutKind.Sequential)]
            public struct MINIDUMP_THREAD
            {
                public int ThreadId;
                public int SuspendCount;
                public int PriorityClass;
                public int Priority;
                public long Teb;
                public MINIDUMP_MEMORY_DESCRIPTOR Stack;
                public MINIDUMP_MEMORY_DESCRIPTOR ThreadContext;
            }

            [StructLayout(LayoutKind.Sequential)]
            public struct MINIDUMP_HANDLE_DATA_STREAM
            {
                public int SizeOfHeader;
                public int SizeOfDescriptor;
                public int NumberOfDescriptors;
                public int Reserved;
            }
            [StructLayout(LayoutKind.Sequential)]
            public struct MINIDUMP_HANDLE_DESCRIPTOR
            {
                public long Handle;
                public uint TypeNameRva;
                public uint ObjectNameRva;
                public int Attributes;
                public int GrantedAccess;
                public int HandleCount;
                public int PointerCount;
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


        }
    }
}