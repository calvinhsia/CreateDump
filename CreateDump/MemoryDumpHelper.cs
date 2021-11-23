using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security;
namespace CreateDump
{
    using static MemoryDumpHelper.NativeMethods;
    public class MemoryDumpHelper
    {
        /// <summary>
        /// Collects a mini dump (optionally with full memory) for the given process and writes it to the given file path
        /// </summary>
        public static void CollectDump(int ProcessId, string dumpFilePath, bool fIncludeFullHeap, bool UseSnapshot = false)
        {
            var process = Process.GetProcessById(ProcessId);
            //LoggerBase.WriteInformation($"Dump collection started. FullHeap= {fIncludeFullHeap} {dumpFilePath}");

            IntPtr hFile = IntPtr.Zero;

            try
            {
                hFile = NativeMethods.CreateFile( // will overwrite file if exists.
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
                bool resultMiniDumpWriteDump = false;
                // Ensure the dump file will contain all the info needed (full memory, handle, threads)
                NativeMethods.MINIDUMP_TYPE dumpType = NativeMethods.MINIDUMP_TYPE.MiniDumpNormal
                                                      | NativeMethods.MINIDUMP_TYPE.MiniDumpWithHandleData
                                                      | NativeMethods.MINIDUMP_TYPE.MiniDumpWithThreadInfo;
                if (fIncludeFullHeap)
                {
                    dumpType |= NativeMethods.MINIDUMP_TYPE.MiniDumpWithFullMemory
                             | NativeMethods.MINIDUMP_TYPE.MiniDumpWithFullMemoryInfo;

                }


                MINIDUMP_EXCEPTION_INFORMATION exceptionInfo = new NativeMethods.MINIDUMP_EXCEPTION_INFORMATION();

                if (UseSnapshot)
                {
                    var CaptureFlags = PssCaptureFlags.PSS_CAPTURE_VA_CLONE
                                    | PssCaptureFlags.PSS_CAPTURE_HANDLES
                                    | PssCaptureFlags.PSS_CAPTURE_HANDLE_NAME_INFORMATION
                                    | PssCaptureFlags.PSS_CAPTURE_HANDLE_BASIC_INFORMATION
                                    | PssCaptureFlags.PSS_CAPTURE_HANDLE_TYPE_SPECIFIC_INFORMATION
                                    | PssCaptureFlags.PSS_CAPTURE_HANDLE_TRACE
                                    | PssCaptureFlags.PSS_CAPTURE_THREADS
                                    | PssCaptureFlags.PSS_CAPTURE_THREAD_CONTEXT
                                    | PssCaptureFlags.PSS_CAPTURE_THREAD_CONTEXT_EXTENDED
                                    | PssCaptureFlags.PSS_CREATE_BREAKAWAY
                                    | PssCaptureFlags.PSS_CREATE_BREAKAWAY_OPTIONAL
                                    | PssCaptureFlags.PSS_CREATE_USE_VM_ALLOCATIONS
                                    | PssCaptureFlags.PSS_CREATE_RELEASE_SECTION;
                    if (PssCaptureSnapshot(process.Handle, CaptureFlags, 0, out var snapshotHandle) == 0)
                    {

                        resultMiniDumpWriteDump = MiniDumpWriteDump(
                              hProcess: process.Handle,
                              ProcessId: process.Id,
                              hFile: hFile,
                              DumpType: dumpType,
                              ExceptionParam: ref exceptionInfo,
                              UserStreamParam: IntPtr.Zero,
                              callback: MinidumpCallBackForSnapshot
                            );
                        if (PssFreeSnapshot(process.Handle, snapshotHandle) == 0)
                        {
                            //                            TestContext.WriteLine($"Error free snapshot");
                        }

                    }
                    else
                    {
                        //                      TestContext.WriteLine($"Error create snapshot");
                    }

                }
                else
                {

                    resultMiniDumpWriteDump = NativeMethods.MiniDumpWriteDump(
                              hProcess: process.Handle,
                              ProcessId: process.Id,
                              hFile: hFile,
                              DumpType: dumpType,
                              ExceptionParam: ref exceptionInfo,
                              UserStreamParam: IntPtr.Zero,
                              callback: null
                        );

                }

                if (resultMiniDumpWriteDump == false)
                {
                    int hresult = Marshal.GetHRForLastWin32Error();
                    Exception hresultException = Marshal.GetExceptionForHR(hresult);
                    throw hresultException;
                }
            }
            finally
            {
                NativeMethods.CloseHandle(hFile);
            }

            //LoggerBase.WriteInformation("Dump collection complete.");
        }
        static bool MinidumpCallBackForSnapshot(IntPtr CallBackParam, MINIDUMP_CALLBACK_INPUT input, out MINIDUMP_CALLBACK_OUTPUT output)
        {
            output.Status = 0;
            switch (input.CallbackType)
            {
                case 16: //IsProcessSnapshotCallback
                    output.Status = 1;//S_FALSE
                    break;

            }
            return true;
        }
        internal class NativeMethods
        {
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
            [Flags]
            public enum PssCaptureFlags
            {
                PSS_CAPTURE_NONE,
                PSS_CAPTURE_VA_CLONE,
                PSS_CAPTURE_RESERVED_00000002,
                PSS_CAPTURE_HANDLES,
                PSS_CAPTURE_HANDLE_NAME_INFORMATION,
                PSS_CAPTURE_HANDLE_BASIC_INFORMATION,
                PSS_CAPTURE_HANDLE_TYPE_SPECIFIC_INFORMATION,
                PSS_CAPTURE_HANDLE_TRACE,
                PSS_CAPTURE_THREADS,
                PSS_CAPTURE_THREAD_CONTEXT,
                PSS_CAPTURE_THREAD_CONTEXT_EXTENDED,
                PSS_CAPTURE_RESERVED_00000400,
                PSS_CAPTURE_VA_SPACE,
                PSS_CAPTURE_VA_SPACE_SECTION_INFORMATION,
                PSS_CAPTURE_IPT_TRACE,
                PSS_CAPTURE_RESERVED_00004000,
                PSS_CREATE_BREAKAWAY_OPTIONAL,
                PSS_CREATE_BREAKAWAY,
                PSS_CREATE_FORCE_BREAKAWAY,
                PSS_CREATE_USE_VM_ALLOCATIONS,
                PSS_CREATE_MEASURE_PERFORMANCE,
                PSS_CREATE_RELEASE_SECTION
            }
            // https://docs.microsoft.com/en-us/windows/win32/api/processsnapshot/nf-processsnapshot-psscapturesnapshot
            [DllImport("kernel32.dll")]
            public static extern int PssCaptureSnapshot(IntPtr ProcessHandle, PssCaptureFlags capture, uint ThreadContextFlags, out IntPtr SnapshotHandle);
            [DllImport("kernel32.dll")]
            public static extern int PssFreeSnapshot(IntPtr ProcessHandle, IntPtr SnapshotHandle);

            public struct MINIDUMP_CALLBACK_INPUT
            {
                public uint processId;
                public IntPtr ProcessHandle;
                public uint CallbackType;
                public IntPtr union;
            }
            public struct MINIDUMP_CALLBACK_OUTPUT
            {

                public uint Status;
            }
            //C:\Program Files (x86)\Windows Kits\10\Include\10.0.18362.0\um\minidumpapiset.h
            public delegate bool MinidumpCallbackRoutine(IntPtr CallBackParam, MINIDUMP_CALLBACK_INPUT callbackInput, out MINIDUMP_CALLBACK_OUTPUT mINIDUMP_CALLBACK_OUTPUT);


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

            public static readonly IntPtr INVALID_HANDLE_VALUE = new IntPtr(-1);

            [StructLayout(LayoutKind.Sequential, Pack = 4)]
            public struct MINIDUMP_EXCEPTION_INFORMATION
            {
                public uint ThreadId;
                public IntPtr ExceptionPointers;
                // public int padding;
                //[MarshalAs(UnmanagedType.Bool)]
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
                    MinidumpCallbackRoutine callback
                );

            // I explicitly DONT caputure GetLastError information on this call because it is often used to
            // clean up and it is cleaner if GetLastError still points at the orginal error, and not the failure
            // in CloseHandle.  If we ever care about exact errors of CloseHandle, we can make another entry
            // point 
            [DllImport("kernel32.dll"), SuppressUnmanagedCodeSecurityAttribute]
            internal static extern bool CloseHandle([In] IntPtr hHandle);
        }
    }
}