using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.InteropServices;
using System.Security;
using System.Threading;

namespace CreateDump
{
    /// <summary>
    /// Provides the ability to collect a memory dump of a process.
    /// </summary>
    internal static class MemoryDumpHelper // note: this is called from 64 bit too so be careful about changing modifiers like internal, static
    {
        static internal bool fUseSnapshot;
        static void DoGCs()
        {
            for (int i = 0; i < 100; i ++)
            {
                GC.Collect();
                Marshal.CleanupUnusedObjectsInCurrentContext();
            }
        }
        /// <summary>
        /// Collects a mini dump (optionally with full memory) for the given process and writes it to the given file path
        /// </summary>
        public static void CollectDump(int ProcessId, string dumpFilePath, bool fIncludeFullHeap, bool UseSnapshot = true)
        {
            fUseSnapshot = UseSnapshot;
            Process process;
            IntPtr processHandle = IntPtr.Zero;
            IntPtr snapshotHandle = IntPtr.Zero;
            int hr;

            try
            {
                process = Process.GetProcessById(ProcessId);
            }
            catch (Exception)
            {
                return; // process is not around anymore
            }

            IntPtr pCallbackInfo = IntPtr.Zero;
            if (!UseSnapshot)
            {
                processHandle = process.Handle;
            }
            else
            {
                pCallbackInfo = Marshal.AllocHGlobal(Marshal.SizeOf<NativeMethods.MINIDUMP_CALLBACK_INFORMATION>());
                NativeMethods.MINIDUMP_CALLBACK_INFORMATION callbackInfo;
                callbackInfo.CallbackParam = IntPtr.Zero;
                callbackInfo.CallbackRoutine = Marshal.GetFunctionPointerForDelegate<NativeMethods.MinidumpCallbackRoutine>(MinidumpCallBackForSnapshot);
//                callbackInfo.CallbackRoutine = new IntPtr(100);
                DoGCs();

                Marshal.StructureToPtr<NativeMethods.MINIDUMP_CALLBACK_INFORMATION>(callbackInfo, pCallbackInfo, fDeleteOld: false);
                DoGCs();

                IntPtr cloneHandle = IntPtr.Zero;
                NativeMethods.PSS_CAPTURE_FLAGS CaptureFlags = NativeMethods.PSS_CAPTURE_FLAGS.PSS_CAPTURE_HANDLES
                        | NativeMethods.PSS_CAPTURE_FLAGS.PSS_CAPTURE_HANDLE_NAME_INFORMATION
                        | NativeMethods.PSS_CAPTURE_FLAGS.PSS_CAPTURE_HANDLE_BASIC_INFORMATION
                        | NativeMethods.PSS_CAPTURE_FLAGS.PSS_CAPTURE_HANDLE_TYPE_SPECIFIC_INFORMATION
                        | NativeMethods.PSS_CAPTURE_FLAGS.PSS_CAPTURE_HANDLE_TRACE
                        | NativeMethods.PSS_CAPTURE_FLAGS.PSS_CAPTURE_THREADS
                        | NativeMethods.PSS_CAPTURE_FLAGS.PSS_CAPTURE_THREAD_CONTEXT
                        | NativeMethods.PSS_CAPTURE_FLAGS.PSS_CAPTURE_THREAD_CONTEXT_EXTENDED
                        | NativeMethods.PSS_CAPTURE_FLAGS.PSS_CAPTURE_IPT_TRACE
                        | NativeMethods.PSS_CAPTURE_FLAGS.PSS_CREATE_BREAKAWAY
                        | NativeMethods.PSS_CAPTURE_FLAGS.PSS_CREATE_BREAKAWAY_OPTIONAL
                        | NativeMethods.PSS_CAPTURE_FLAGS.PSS_CREATE_USE_VM_ALLOCATIONS
                        | NativeMethods.PSS_CAPTURE_FLAGS.PSS_CREATE_RELEASE_SECTION;

                if (fIncludeFullHeap)
                {
                    CaptureFlags |= NativeMethods.PSS_CAPTURE_FLAGS.PSS_CAPTURE_VA_CLONE;
                }
                var threadFlags = (int)NativeMethods.CONTEXT.CONTEXT_ALL;

                hr = NativeMethods.PssCaptureSnapshot(process.Handle, CaptureFlags, threadFlags, out snapshotHandle);
//                hr = NativeMethods.PssCaptureSnapshot(process.Handle, CaptureFlags, IntPtr.Size == 8 ? 0x00nGCs001F : 0x000nGCs03F, out snapshotHandle);
                if (hr != 0)
                {
                    Trace.WriteLine($"Could not create snapshot to process. Error {hr}.");
                    return;
                }

                processHandle = snapshotHandle;
            }
            DoGCs();

            IntPtr hFile = IntPtr.Zero;
            try
            {
                Trace.WriteLine($"Dump collection started. FullHeap= {fIncludeFullHeap} {dumpFilePath}");
                hFile = NativeMethods.CreateFile(
                        lpFileName: dumpFilePath,
                        dwDesiredAccess: NativeMethods.EFileAccess.GenericWrite,
                        dwShareMode: NativeMethods.EFileShare.None,
                        lpSecurityAttributes: IntPtr.Zero,
                        dwCreationDisposition: NativeMethods.ECreationDisposition.CreateAlways,
                        dwFlagsAndAttributes: NativeMethods.EFileAttributes.Normal,
                        hTemplateFile: IntPtr.Zero
                    );

                if (hFile == NativeMethods.INVALID_HANDLE_VALUE)
                {
                    int hresult = Marshal.GetHRForLastWin32Error();
                    Exception hresultException = Marshal.GetExceptionForHR(hresult);
                    throw hresultException;
                }

                // Ensure the dump file will contain all the info needed (full memory, handle, threads)
                NativeMethods.MINIDUMP_TYPE dumpType = NativeMethods.MINIDUMP_TYPE.MiniDumpNormal
                                                      | NativeMethods.MINIDUMP_TYPE.MiniDumpWithHandleData
                                                      | NativeMethods.MINIDUMP_TYPE.MiniDumpWithThreadInfo;
                if (fIncludeFullHeap)
                {
                    dumpType |= NativeMethods.MINIDUMP_TYPE.MiniDumpWithFullMemory
                             | NativeMethods.MINIDUMP_TYPE.MiniDumpWithFullMemoryInfo;

                }

                if (processHandle == NativeMethods.INVALID_HANDLE_VALUE)
                {
                    throw new InvalidOperationException($"The Handle is invalid. UseSnapshot = {UseSnapshot}");
                }
                NativeMethods.MINIDUMP_EXCEPTION_INFORMATION exceptionInfo = new NativeMethods.MINIDUMP_EXCEPTION_INFORMATION();

                DoGCs();

                bool result = NativeMethods.MiniDumpWriteDump(
                          hProcess: processHandle,
                          ProcessId: ProcessId,
                          hFile: hFile,
                          DumpType: dumpType,
                          ExceptionParam: ref exceptionInfo,
                          UserStreamParam: IntPtr.Zero,
                          CallbackParam: pCallbackInfo
                    );

                if (result == false)
                {
                    int hresult = Marshal.GetHRForLastWin32Error();
                    Exception hresultException = Marshal.GetExceptionForHR(hresult);
                    Trace.WriteLine($"Got false from MiniDumpWriteDump{hresultException}");
                    throw hresultException;
                }
            }
            finally
            {
                NativeMethods.CloseHandle(hFile);
                if (snapshotHandle != IntPtr.Zero)
                {
                    hr = NativeMethods.PssFreeSnapshot(Process.GetCurrentProcess().Handle, snapshotHandle);
                    if (hr != 0)
                    {
                        Trace.WriteLine($"Unable to free the snapshot of the process we took: hr={hr}");
                    }
                }
            }

            Trace.WriteLine("Dump collection complete.");
        }
        ///// <summary>
        ///// Exceptions will be caught and added to the Diagsession
        ///// </summary>
        //internal static bool CollectDump32Or64(int id, string dumpPath, bool includeFullHeap, int collectionTimeOutInMins = 10)
        //{
        //    var is32bit = false;
        //    var isMatchingPtrSize = is32bit ? IntPtr.Size == 4 : IntPtr.Size == 8;

        //    // if the target process is a Windows (32) on Win64 process (a 32 bit process on a 64 bit OS)
        //    if (isMatchingPtrSize)
        //    {
        //        CollectDump(id, dumpPath, includeFullHeap);
        //    }
        //    else
        //    {
        //        // create an EXE that loads Microsoft.VisualStudio.PerfWatson.dll and calls the MemoryDumpHelper.CollectDump method
        //        // matching the bitness of the target process
        //        var targPEFile = Path.ChangeExtension(Path.GetTempFileName(), "exe");
        //        ImageFileMachine imageBitness = is32bit ? ImageFileMachine.AMD64 : ImageFileMachine.I386;

        //        var oBuilder = new AssemblyCreator().CreateAssembly(
        //            targPEFile,
        //            PortableExecutableKinds.PE32Plus,
        //            imageBitness,
        //            additionalAssemblyPaths: Path.Combine(
        //                    Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location),
        //                    "PrivateAssemblies"),
        //            logOutput: false
        //            );
        //        var targDll = Assembly.GetExecutingAssembly().Location; //Microsoft.VisualStudio.PerfWatson.dll
        //                                                                // start the 64 bit process with params:
        //                                                                // PathToPWatsonDll  TypeNameWithDumpCode  MethodNameToCall  ProcIdToDump  DumpPath  FullHeap
        //        var proc = Process.Start(
        //            targPEFile,
        //            $@"""{targDll}"" MemoryDumpHelper CollectDump {id} ""{dumpPath}"" {includeFullHeap}");
        //        proc.WaitForExit(collectionTimeOutInMins * 60 * 1000); // Typically <10 secs, but for full heap dumps this could take longer
        //        try
        //        {
        //            File.Delete(targPEFile);
        //        }
        //        catch (Exception)
        //        {
        //        }
        //    }
        //    return is32bit;
        //}

        internal static bool MinidumpCallBackForSnapshot(IntPtr CallBackParam, IntPtr pinput, IntPtr poutput)
        {
            if (IntPtr.Size == 8)
            {
                var input = Marshal.PtrToStructure<NativeMethods.MINIDUMP_CALLBACK_INPUT64>(pinput);
                var output = Marshal.PtrToStructure<NativeMethods.MINIDUMP_CALLBACK_OUTPUT>(poutput);
                switch (input.CallbackType)
                {
                    case NativeMethods.IsProcessSnapshotCallback:
                        output.Status = NativeMethods.S_FALSE;
                        Marshal.StructureToPtr<NativeMethods.MINIDUMP_CALLBACK_OUTPUT>(output, poutput, fDeleteOld: true);
                        break;
                }
            }
            else
            {
                var input = Marshal.PtrToStructure<NativeMethods.MINIDUMP_CALLBACK_INPUT32>(pinput);
                var output = Marshal.PtrToStructure<NativeMethods.MINIDUMP_CALLBACK_OUTPUT>(poutput);
                switch (input.CallbackType)
                {
                    case NativeMethods.IsProcessSnapshotCallback:
                        output.Status = NativeMethods.S_FALSE;
                        break;
                }
            }
            return true;
        }

        internal class NativeMethods
        {

            [DllImport("kernel32.dll", SetLastError = true)]
            public static extern IntPtr GetCurrentProcess();
            
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

            //https://msdn.microsoft.com/en-us/library/windows/desktop/ms680519%28v=vs.85%29.aspx?f=255&MSPPError=-2147217396
            [Flags]
            public enum MINIDUMP_TYPE
            {
                MiniDumpNormal = 0x00000000,
                MiniDumpWithDataSegs = 0x00000001,
                MiniDumpWithFullMemory = 0x00000002,
                MiniDumpWithHandleData = 0x00000004,
                MiniDumpFilterMemory = 0x00000008,
                MiniDumpScanMemory = 0x00000010,
                MiniDumpWithUnloadedModules = 0x00000020,
                MiniDumpWithIndirectlyReferencedMemory = 0x00000040,
                MiniDumpFilterModulePaths = 0x00000080,
                MiniDumpWithProcessThreadData = 0x00000100,
                MiniDumpWithPrivateReadWriteMemory = 0x00000200,
                MiniDumpWithoutOptionalData = 0x00000400,
                MiniDumpWithFullMemoryInfo = 0x00000800,
                MiniDumpWithThreadInfo = 0x00001000,
                MiniDumpWithCodeSegs = 0x00002000,
                MiniDumpWithoutAuxiliaryState = 0x00004000,
                MiniDumpWithFullAuxiliaryState = 0x00008000,
                MiniDumpWithPrivateWriteCopyMemory = 0x00010000,
                MiniDumpIgnoreInaccessibleMemory = 0x00020000,
                MiniDumpWithTokenInformation = 0x00040000,
                MiniDumpWithModuleHeaders = 0x00080000,
                MiniDumpFilterTriage = 0x00100000,
                MiniDumpValidTypeFlags = 0x001fffff,
            };

            [UnmanagedFunctionPointer(CallingConvention.Winapi)]
            public delegate bool MinidumpCallbackRoutine(IntPtr CallBackParam, IntPtr pcallbackInput, IntPtr pMINIDUMP_CALLBACK_OUTPUT);

            public const int IsProcessSnapshotCallback = 16;

            [StructLayout(LayoutKind.Sequential)]
            public struct MINIDUMP_CALLBACK_INFORMATION
            {
                public IntPtr CallbackRoutine;
                public IntPtr CallbackParam;
            }
            [StructLayout(LayoutKind.Explicit)]
            public struct MINIDUMP_CALLBACK_INPUT32
            {
                [FieldOffset(0)]
                public uint processId;
                [FieldOffset(4)]
                public IntPtr ProcessHandle;
                [FieldOffset(8)]
                public uint CallbackType;
                [FieldOffset(12)]
                public IntPtr union;
            }
            [StructLayout(LayoutKind.Explicit)]
            public struct MINIDUMP_CALLBACK_INPUT64
            {
                [FieldOffset(0)]
                public uint processId;
                [FieldOffset(4)]
                public IntPtr ProcessHandle;
                [FieldOffset(12)]
                public uint CallbackType;
                [FieldOffset(16)]
                public IntPtr union;
            }
            [StructLayout(LayoutKind.Explicit)]
            public struct MINIDUMP_CALLBACK_OUTPUT
            {
                [FieldOffset(0)]
                public uint Status;
                [FieldOffset(0)]
                public IntPtr handle;
                [FieldOffset(0)]
                public uint ModuleWriteFlags;

            }
            [Flags]
            public enum CONTEXT
            {
                CONTEXT_i386 = 0x00010000,    // this assumes that i386 and
                CONTEXT_i486 = 0x00010000,    // i486 have identical context records
                CONTEXT_ARM = 0x00200000,
                CONTEXT_AMD64 = 0x00100000,

                CONTEXT_CONTROL = (CONTEXT_AMD64 | 0x00000001),
                CONTEXT_INTEGER = (CONTEXT_AMD64 | 0x00000002),
                CONTEXT_SEGMENTS = (CONTEXT_AMD64 | 0x00000004),
                CONTEXT_FLOATING_POINT = (CONTEXT_AMD64 | 0x00000008),
                CONTEXT_DEBUG_REGISTERS = (CONTEXT_AMD64 | 0x00000010),
                CONTEXT_FULL = (CONTEXT_CONTROL | CONTEXT_INTEGER | CONTEXT_FLOATING_POINT),
                CONTEXT_ALL = (CONTEXT_CONTROL | CONTEXT_INTEGER | CONTEXT_SEGMENTS | CONTEXT_FLOATING_POINT | CONTEXT_DEBUG_REGISTERS)
            }
            [Flags]
            internal enum PSS_CAPTURE_FLAGS : uint
            {
                PSS_CAPTURE_NONE = 0x00000000,
                PSS_CAPTURE_VA_CLONE = 0x00000001,
                PSS_CAPTURE_RESERVED_00000002 = 0x00000002,
                PSS_CAPTURE_HANDLES = 0x00000004,
                PSS_CAPTURE_HANDLE_NAME_INFORMATION = 0x00000008,
                PSS_CAPTURE_HANDLE_BASIC_INFORMATION = 0x00000010,
                PSS_CAPTURE_HANDLE_TYPE_SPECIFIC_INFORMATION = 0x00000020,
                PSS_CAPTURE_HANDLE_TRACE = 0x00000040,
                PSS_CAPTURE_THREADS = 0x00000080,
                PSS_CAPTURE_THREAD_CONTEXT = 0x00000100,
                PSS_CAPTURE_THREAD_CONTEXT_EXTENDED = 0x00000200,
                PSS_CAPTURE_RESERVED_00000400 = 0x00000400,
                PSS_CAPTURE_VA_SPACE = 0x00000800,
                PSS_CAPTURE_VA_SPACE_SECTION_INFORMATION = 0x00001000,
                PSS_CAPTURE_IPT_TRACE = 0x00002000,
                PSS_CREATE_BREAKAWAY_OPTIONAL = 0x04000000,
                PSS_CREATE_BREAKAWAY = 0x08000000,
                PSS_CREATE_FORCE_BREAKAWAY = 0x10000000,
                PSS_CREATE_USE_VM_ALLOCATIONS = 0x20000000,
                PSS_CREATE_MEASURE_PERFORMANCE = 0x40000000,
                PSS_CREATE_RELEASE_SECTION = 0x80000000
            };

            internal enum PSS_QUERY_INFORMATION_CLASS
            {
                PSS_QUERY_PROCESS_INFORMATION = 0,
                PSS_QUERY_VA_CLONE_INFORMATION = 1,
                PSS_QUERY_AUXILIARY_PAGES_INFORMATION = 2,
                PSS_QUERY_VA_SPACE_INFORMATION = 3,
                PSS_QUERY_HANDLE_INFORMATION = 4,
                PSS_QUERY_THREAD_INFORMATION = 5,
                PSS_QUERY_HANDLE_TRACE_INFORMATION = 6,
                PSS_QUERY_PERFORMANCE_COUNTERS = 7
            }

            public static readonly IntPtr INVALID_HANDLE_VALUE = new IntPtr(-1);

            public const int S_FALSE = 1;
            public const int S_OK = 0;

            [StructLayout(LayoutKind.Sequential, Pack = 4)]
            public struct MINIDUMP_EXCEPTION_INFORMATION
            {
                public uint ThreadId;
                public IntPtr ExceptionPointers;
                public int ClientPointers;
            }

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

            //https://msdn.microsoft.com/en-us/library/windows/desktop/bb513622(v=vs.85).aspx
            [DllImport("Dbghelp.dll", SetLastError = true)]
            public static extern bool MiniDumpWriteDump(
                    IntPtr hProcess,
                    int ProcessId,
                    IntPtr hFile,
                    MINIDUMP_TYPE DumpType,
                    ref MINIDUMP_EXCEPTION_INFORMATION ExceptionParam,
                    IntPtr UserStreamParam,
                    IntPtr CallbackParam
                );

            // I explicitly DONT caputure GetLastError information on this call because it is often used to
            // clean up and it is cleaner if GetLastError still points at the orginal error, and not the failure
            // in CloseHandle.  If we ever care about exact errors of CloseHandle, we can make another entry
            // point 
            [DllImport("kernel32.dll"), SuppressUnmanagedCodeSecurityAttribute]
            internal static extern bool CloseHandle([In] IntPtr hHandle);

            [DllImport("kernel32.dll")]
            internal static extern int PssCaptureSnapshot(IntPtr ProcessHandle, PSS_CAPTURE_FLAGS CaptureFlags, int ThreadContextFlags, out IntPtr SnapshotHandle);

            [DllImport("kernel32.dll")]
            internal static extern int PssFreeSnapshot(IntPtr ProcessHandle, IntPtr SnapshotHandle);

            [DllImport("kernel32.dll")]
            internal static extern int PssQuerySnapshot(IntPtr SnapshotHandle, PSS_QUERY_INFORMATION_CLASS InformationClass, out IntPtr Buffer, int BufferLength);

            [DllImport("kernel32.dll")]
            internal static extern int GetProcessId(IntPtr hObject);
        }

    }
}
