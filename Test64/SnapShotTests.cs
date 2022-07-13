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
                   PSS_CAPTURE_FLAGS.PSS_CAPTURE_VA_CLONE
                | PSS_CAPTURE_FLAGS.PSS_CAPTURE_HANDLES
                | PSS_CAPTURE_FLAGS.PSS_CAPTURE_HANDLE_NAME_INFORMATION
                | PSS_CAPTURE_FLAGS.PSS_CAPTURE_HANDLE_BASIC_INFORMATION
                | PSS_CAPTURE_FLAGS.PSS_CAPTURE_HANDLE_TYPE_SPECIFIC_INFORMATION
                | PSS_CAPTURE_FLAGS.PSS_CAPTURE_HANDLE_TRACE
                | PSS_CAPTURE_FLAGS.PSS_CAPTURE_THREADS
                | PSS_CAPTURE_FLAGS.PSS_CAPTURE_THREAD_CONTEXT
                | PSS_CAPTURE_FLAGS.PSS_CAPTURE_THREAD_CONTEXT_EXTENDED
                | PSS_CAPTURE_FLAGS.PSS_CAPTURE_IPT_TRACE
                | PSS_CAPTURE_FLAGS.PSS_CREATE_BREAKAWAY
                | PSS_CAPTURE_FLAGS.PSS_CREATE_BREAKAWAY_OPTIONAL
                | PSS_CAPTURE_FLAGS.PSS_CREATE_USE_VM_ALLOCATIONS
                | PSS_CAPTURE_FLAGS.PSS_CREATE_RELEASE_SECTION
                | PSS_CAPTURE_FLAGS.PSS_CREATE_MEASURE_PERFORMANCE
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
                if (PssCaptureSnapshot(safephandle.DangerousGetHandle(), CaptureFlags, (int)threadFlags, out snapshotHandle) == 0)
                {
                    lst.Add(snapshotHandle);
                }
            }
            Trace.WriteLine($"done #iter ={nIter} {sw.Elapsed.TotalSeconds:n0} secs   Secs/iter = {sw.Elapsed.TotalSeconds / nIter:n2}");
            lst.ForEach(s => PssFreeSnapshot(GetCurrentProcess(), s));
        }
        [TestMethod]
        public async Task TestManySnapshotDumps()
        {
            await Task.Yield();
            var dumpFilename = GetDumpFileNameAndProcToDump(out var procDevEnv);
            var nIter = 40;
            var IsDone = false;
            var taskGCs = Task.Run(async () =>
            {
                while (!IsDone)
                {
                    GC.Collect();
                    await Task.Delay(TimeSpan.FromMilliseconds(10));
                }
            });
            try
            {
                for (int i = 0; i < nIter; i++)
                {
                    Trace.WriteLine($"Taking dump {i}");
                    MemoryDumpHelper.CollectDump(procDevEnv.Id, dumpFilename, fIncludeFullHeap: true, UseSnapshot: true);
                    Assert.IsTrue(File.Exists(dumpFilename), "dump not found");
                    File.Delete(dumpFilename);
                }
            }
            finally
            {
                IsDone = true;
            }
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
                   PSS_CAPTURE_FLAGS.PSS_CAPTURE_VA_CLONE
                | PSS_CAPTURE_FLAGS.PSS_CAPTURE_HANDLES
                | PSS_CAPTURE_FLAGS.PSS_CAPTURE_HANDLE_NAME_INFORMATION
                | PSS_CAPTURE_FLAGS.PSS_CAPTURE_HANDLE_BASIC_INFORMATION
                | PSS_CAPTURE_FLAGS.PSS_CAPTURE_HANDLE_TYPE_SPECIFIC_INFORMATION
                | PSS_CAPTURE_FLAGS.PSS_CAPTURE_HANDLE_TRACE
                | PSS_CAPTURE_FLAGS.PSS_CAPTURE_THREADS
                | PSS_CAPTURE_FLAGS.PSS_CAPTURE_THREAD_CONTEXT
                | PSS_CAPTURE_FLAGS.PSS_CAPTURE_THREAD_CONTEXT_EXTENDED
                | PSS_CAPTURE_FLAGS.PSS_CAPTURE_IPT_TRACE
                | PSS_CAPTURE_FLAGS.PSS_CREATE_BREAKAWAY
                | PSS_CAPTURE_FLAGS.PSS_CREATE_BREAKAWAY_OPTIONAL
                | PSS_CAPTURE_FLAGS.PSS_CREATE_USE_VM_ALLOCATIONS
                | PSS_CAPTURE_FLAGS.PSS_CREATE_RELEASE_SECTION;
            ;
            var threadFlags = (uint)CONTEXT.CONTEXT_ALL;
            IntPtr snapshotHandle = IntPtr.Zero;
            var safephandle = new Microsoft.Win32.SafeHandles.SafeProcessHandle(procDevEnv.Handle, ownsHandle: true);
            if (PssCaptureSnapshot(safephandle.DangerousGetHandle(), CaptureFlags, (int)threadFlags, out snapshotHandle) == 0)
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
00 000000a9`10f4eed8 00007ff9`4ee0d20e     win32u!ZwUserMsgWaitForMultipleObjectsEx+0x14 [onecoreuap\windows\core\umode\moderncore\objfre\amd64\usrstubs.asm @ 9909] 
01 000000a9`10f4eee0 00007ff8`e723149e     user32!RealMsgWaitForMultipleObjectsEx+0x1e [clientcore\windows\core\ntuser\client\daytona\objfre\amd64\client.cxx @ 1726] 
02 000000a9`10f4ef20 00007ff8`e4cdb07e     VsLog!VSResponsiveness::Detours::DetourMsgWaitForMultipleObjectsEx+0x6e [D:\dbs\sh\ddvsm\0626_160812\cmd\k\src\vscommon\testtools\vslog\ResponseTime\VSResponsiveness.cpp @ 960] 
03 (Inline Function) --------`--------     msenv!MainMessageLoop::BlockingWait+0x27 [D:\dbs\sh\ddvsm\0626_220631\cmd\1b\src\env\msenv\core\main.cpp @ 2346] 
04 000000a9`10f4ef60 00007ff8`e4c79fa5     msenv!CMsoCMHandler::EnvironmentMsgLoop+0x1fa [D:\dbs\sh\ddvsm\0626_220631\cmd\1b\src\env\msenv\core\msocm.cpp @ 503] 
05 000000a9`10f4efe0 00007ff8`e4c7a285     msenv!CMsoCMHandler::FPushMessageLoop+0x65 [D:\dbs\sh\ddvsm\0626_220631\cmd\1b\src\env\msenv\core\msocm.cpp @ 366] 
06 000000a9`10f4f040 00007ff8`e4c7a15f     msenv!SCM::FPushMessageLoop+0xf5 [D:\dbs\sh\ddvsm\0624_220840\cmd\4\src\env\msenv\mso\core\cistdmgr.cpp @ 2284] 
07 000000a9`10f4f0b0 00007ff8`e4c7a105     msenv!SCM_MsoCompMgr::FPushMessageLoop+0x3f [D:\dbs\sh\ddvsm\0624_220840\cmd\4\src\env\msenv\mso\core\cistdmgr.cpp @ 3020] 
08 000000a9`10f4f0e0 00007ff8`e4c79e2a     msenv!CMsoComponent::PushMsgLoop+0x3d [D:\dbs\sh\ddvsm\0626_220631\cmd\1b\src\env\msenv\core\msocm.cpp @ 714] 
09 000000a9`10f4f110 00007ff8`e4cc7788     msenv!VStudioMainLogged+0x8fe [D:\dbs\sh\ddvsm\0626_220631\cmd\1b\src\env\msenv\core\main.cpp @ 1503] 
0a 000000a9`10f4f230 00007ff6`17b3d5e4     msenv!VStudioMain+0xc8 [D:\dbs\sh\ddvsm\0626_220631\cmd\1b\src\env\msenv\core\main.cpp @ 1901] 
0b 000000a9`10f4f260 00007ff6`17b387c0     devenv!util_CallVsMain+0x5c [D:\dbs\sh\ddvsm\0624_220840\cmd\a\src\appid\lib\utils.cpp @ 1172] 
0c 000000a9`10f4f290 00007ff6`17b32fd0     devenv!CDevEnvAppId::Run+0x226c [Q:\src\appid\devenv\stub\devenv.cpp @ 1021] 
0d 000000a9`10f4fa60 00007ff6`17b80b0a     devenv!WinMain+0xd0 [Q:\src\appid\devenv\stub\winmain.cpp @ 70] 
0e (Inline Function) --------`--------     devenv!invoke_main+0x21 [d:\a01\_work\3\s\src\vctools\crt\vcstartup\src\startup\exe_common.inl @ 102] 
0f 000000a9`10f4fad0 00007ff9`4e4554e0     devenv!__scrt_common_main_seh+0x106 [d:\a01\_work\3\s\src\vctools\crt\vcstartup\src\startup\exe_common.inl @ 288] 
10 000000a9`10f4fb10 00007ff9`4f5a485b     kernel32!BaseThreadInitThunk+0x10 [clientcore\base\win32\client\thread.c @ 75] 
11 000000a9`10f4fb40 00000000`00000000     ntdll!RtlUserThreadStart+0x2b [minkernel\ntdll\rtlstrt.c @ 1152] 
");
            }
        }

    }
}
