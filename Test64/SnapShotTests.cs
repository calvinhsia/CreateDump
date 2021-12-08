using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using CreateDump;
using System.Diagnostics;
using System.Linq;
using System.IO;
using static CreateDump.MemoryDumpHelper.NativeMethods;
using System.Collections.Generic;
using System.Threading.Tasks;
using Test64;

namespace UnitTestProject1
{
    [TestClass]
    public class SnapShotTests: TestBaseClass
    {

        [TestMethod]
        public async Task TestPssSnapshotJustDumpWithNoSnapshot()
        {
            var dumpFilename = GetDumpFileNameAndProcToDump(out var procDevEnv, "2022");
            MemoryDumpHelper.CollectDump(procDevEnv.Id, dumpFilename, fIncludeFullHeap: true, UseSnapshot: false);
            await VerifyDumpFileAsync(dumpFilename, startWinDbg: true);
        }

        [TestMethod ]
        public async Task TestPssSnapshotJustTriageDumpWithSnapshot()
        {
            var dumpFilename = GetDumpFileNameAndProcToDump(out var procDevEnv, "2022");
            MemoryDumpHelper.CollectDump(procDevEnv.Id, dumpFilename, fIncludeFullHeap: false, UseSnapshot: true);
            await VerifyDumpFileAsync(dumpFilename, startWinDbg: true);
        }

        [TestMethod]
        public async Task TestPssSnapshotJustDumpWithSnapshot()
        {
            var dumpFilename = GetDumpFileNameAndProcToDump(out var procDevEnv);
            MemoryDumpHelper.CollectDump(procDevEnv.Id, dumpFilename, fIncludeFullHeap: true, UseSnapshot: true);
            await VerifyDumpFileAsync(dumpFilename, startWinDbg: true);
        }

        [TestMethod]
        public void TestPssSnapshotTiming()
        {
            if (IntPtr.Size == 4)
            {
                return;
            }
            var dumpFilename = GetDumpFileNameAndProcToDump(out var procDevEnv);

            var CaptureFlags =
                   PssCaptureFlags.PSS_CAPTURE_VA_CLONE
                | PssCaptureFlags.PSS_CAPTURE_HANDLES
                | PssCaptureFlags.PSS_CAPTURE_HANDLE_NAME_INFORMATION
                | PssCaptureFlags.PSS_CAPTURE_HANDLE_BASIC_INFORMATION
                | PssCaptureFlags.PSS_CAPTURE_HANDLE_TYPE_SPECIFIC_INFORMATION
                | PssCaptureFlags.PSS_CAPTURE_HANDLE_TRACE
                | PssCaptureFlags.PSS_CAPTURE_THREADS
                | PssCaptureFlags.PSS_CAPTURE_THREAD_CONTEXT
                | PssCaptureFlags.PSS_CAPTURE_THREAD_CONTEXT_EXTENDED
                | PssCaptureFlags.PSS_CAPTURE_IPT_TRACE
                | PssCaptureFlags.PSS_CREATE_BREAKAWAY
                | PssCaptureFlags.PSS_CREATE_BREAKAWAY_OPTIONAL
                | PssCaptureFlags.PSS_CREATE_USE_VM_ALLOCATIONS
                | PssCaptureFlags.PSS_CREATE_RELEASE_SECTION
                | PssCaptureFlags.PSS_CREATE_MEASURE_PERFORMANCE
                ;
            ;
            var threadFlags = (uint)CONTEXT.CONTEXT_ALL;
            var safephandle = new Microsoft.Win32.SafeHandles.SafeProcessHandle(procDevEnv.Handle, ownsHandle: true);
            var sw = Stopwatch.StartNew();
            var nIter = 40;
            var lst = new List<IntPtr>();
            for (int i = 0; i < nIter; i++)
            {
                IntPtr snapshotHandle = IntPtr.Zero;
                if (PssCaptureSnapshot(safephandle.DangerousGetHandle(), CaptureFlags, threadFlags, ref snapshotHandle) == 0)
                {
                    lst.Add(snapshotHandle);
                }
            }
            Trace.WriteLine($"done #iter ={nIter} {sw.Elapsed.TotalSeconds:n0} secs   Secs/iter = {sw.Elapsed.TotalSeconds / nIter:n2}");
            lst.ForEach(s => PssFreeSnapshot(GetCurrentProcess(), s));
        }

        [TestMethod]
//        [Ignore]
        public async Task TestPssUseComNoSnapshot()
        {
            if (IntPtr.Size == 4)
            {
                return;
            }
            var dumpFilename = GetDumpFileNameAndProcToDump(out var procDevEnv);
            var ocomcall = new CallCom();
            var iCreateDump = ocomcall.GetInterfaceICreateDump();
            var hr = iCreateDump.CreateDump(procDevEnv.Id, UseSnapshot: 0, pathDumpFileName: dumpFilename);
            Trace.WriteLine($"Got CreateDump hr = {hr}");
            await VerifyDumpFileAsync(dumpFilename, startWinDbg: true);
        }

        [TestMethod]
//        [Ignore]
        public async Task TestPssUseComWithCSSnapshot()
        {
            if (IntPtr.Size == 4)
            {
                return;
            }
            var dumpFilename = GetDumpFileNameAndProcToDump(out var procDevEnv);
            var ocomcall = new CallCom();
            var iCreateDump = ocomcall.GetInterfaceICreateDump();

            var CaptureFlags =
                   PssCaptureFlags.PSS_CAPTURE_VA_CLONE
                | PssCaptureFlags.PSS_CAPTURE_HANDLES
                | PssCaptureFlags.PSS_CAPTURE_HANDLE_NAME_INFORMATION
                | PssCaptureFlags.PSS_CAPTURE_HANDLE_BASIC_INFORMATION
                | PssCaptureFlags.PSS_CAPTURE_HANDLE_TYPE_SPECIFIC_INFORMATION
                | PssCaptureFlags.PSS_CAPTURE_HANDLE_TRACE
                | PssCaptureFlags.PSS_CAPTURE_THREADS
                | PssCaptureFlags.PSS_CAPTURE_THREAD_CONTEXT
                | PssCaptureFlags.PSS_CAPTURE_THREAD_CONTEXT_EXTENDED
                | PssCaptureFlags.PSS_CAPTURE_IPT_TRACE
                | PssCaptureFlags.PSS_CREATE_BREAKAWAY
                | PssCaptureFlags.PSS_CREATE_BREAKAWAY_OPTIONAL
                | PssCaptureFlags.PSS_CREATE_USE_VM_ALLOCATIONS
                | PssCaptureFlags.PSS_CREATE_RELEASE_SECTION;
            ;
            var threadFlags = (uint)CONTEXT.CONTEXT_ALL;
            IntPtr snapshotHandle = IntPtr.Zero;
            var safephandle = new Microsoft.Win32.SafeHandles.SafeProcessHandle(procDevEnv.Handle, ownsHandle: true);
            if (PssCaptureSnapshot(safephandle.DangerousGetHandle(), CaptureFlags, threadFlags, ref snapshotHandle) == 0)
            {
                var hr = iCreateDump.CreateDumpFromPSSSnapshot(procDevEnv.Id, hSnapshot: snapshotHandle, pathDumpFileName: dumpFilename);
                Trace.WriteLine($"Got CreateDump hr = {hr}");
            }
            await VerifyDumpFileAsync(dumpFilename, startWinDbg: true);
        }

        private string GetDumpFileNameAndProcToDump(out Process procDevEnv, string VSVersion="2022")
        {
            procDevEnv = Process.GetProcessesByName("devenv").Where(p => p.MainModule.FileName.IndexOf($@"Visual Studio\{VSVersion}", StringComparison.OrdinalIgnoreCase) >= 0).First();
//            procDevEnv = Process.GetProcessesByName("devenv").Where(p => p.MainWindowTitle.IndexOf("hWndHost", StringComparison.OrdinalIgnoreCase) >= 0).First();

            Trace.WriteLine($"proc id={procDevEnv.Id} fname={procDevEnv.MainModule.FileName} {procDevEnv.MainWindowTitle}");
            var dumpFilename = Path.ChangeExtension(Path.GetTempFileName(), "dmp");
            //            dumpFilename = @"c:\t2.dmp";
            dumpFilename = $@"c:\{TestContext.TestName}.dmp";
            File.Delete(dumpFilename);
            return dumpFilename;
        }

        async Task VerifyDumpFileAsync(string dumpFilename, bool startWinDbg = false)
        {
            Assert.IsTrue(File.Exists(dumpFilename), $"Dump file not found {dumpFilename}");
            var dumpSize = new FileInfo(dumpFilename).Length;
            Trace.WriteLine($"Dump Size  = {dumpSize:n0}");
            Assert.IsTrue(dumpSize > 5000000, $"Dump file size = {dumpSize:n0}");
            if (startWinDbg)
            {
                var tmplog = Path.GetTempFileName();
                var cmds = $@"
.logopen {tmplog}
k
.logclose
";
                await RunWinDbgWithCmdAsync(dumpFilename, cmds, ShutDownWinDbgWhenDone: true, fIs64bit: IntPtr.Size == 8);
                File.ReadAllLines(tmplog).ToList().ForEach((l => Trace.WriteLine(l)));

                TestDumpReader.ShowMiniDumpReaderData(dumpFilename);


                VerifyLogStrings(
                    @"
msenv!CMsoCMHandler::FPushMessageLoop+0x62 [d:\dbs\sh\ddvsm\1109_220516\cmd\r\src\env\msenv\core\msocm.cpp @ 366] 
msenv!SCM::FPushMessageLoop+0xf3 [d:\dbs\sh\ddvsm\1109_220516\cmd\1b\src\env\msenv\mso\core\cistdmgr.cpp @ 2284] 
msenv!SCM_MsoCompMgr::FPushMessageLoop+0x3f [d:\dbs\sh\ddvsm\1109_220516\cmd\1b\src\env\msenv\mso\core\cistdmgr.cpp @ 3020] 
msenv!CMsoComponent::PushMsgLoop+0x3d [d:\dbs\sh\ddvsm\1109_220516\cmd\r\src\env\msenv\core\msocm.cpp @ 714] 
msenv!VStudioMainLogged+0x723 [d:\dbs\sh\ddvsm\1109_220516\cmd\r\src\env\msenv\core\main.cpp @ 1479] 
msenv!VStudioMain+0xc8 [d:\dbs\sh\ddvsm\1109_220516\cmd\r\src\env\msenv\core\main.cpp @ 1877] 
devenv!util_CallVsMain+0x5c [d:\dbs\sh\ddvsm\1109_220516\cmd\1x\src\appid\lib\utils.cpp @ 1172] 
devenv!CDevEnvAppId::Run+0x2265 [Q:\src\appid\devenv\stub\devenv.cpp @ 1022] 
devenv!WinMain+0xd5 [Q:\src\appid\devenv\stub\winmain.cpp @ 70] 
devenv!invoke_main+0x21 [D:\agent\_work\10\s\src\vctools\crt\vcstartup\src\startup\exe_common.inl @ 102] 
devenv!__scrt_common_main_seh+0x106 [D:\agent\_work\10\s\src\vctools\crt\vcstartup\src\startup\exe_common.inl @ 288] 
kernel32!BaseThreadInitThunk+0x10 [clientcore\base\win32\client\thread.c @ 75] 
ntdll!RtlUserThreadStart+0x2b [minkernel\ntdll\rtlstrt.c @ 1152] 
");
            }
        }

    }
}
