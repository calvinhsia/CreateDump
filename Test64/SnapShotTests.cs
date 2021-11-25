using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using CreateDump;
using System.Diagnostics;
using System.Linq;
using System.IO;
using static CreateDump.MemoryDumpHelper.NativeMethods;
using System.Collections.Generic;

namespace UnitTestProject1
{
    [TestClass]
    public class SnapShotTests
    {
        public TestContext TestContext { get; set; }

        [TestInitialize]
        public void Inittest()
        {
            TestContext.WriteLine($"Starting test IntPtr.Size = {IntPtr.Size}");
        }

        [TestMethod]
        public void TestPssSnapshotJustDumpWithNoSnapshot()
        {
            var dumpFilename = GetDumpFileNameAndProcToDump(out var procDevEnv);
            MemoryDumpHelper.CollectDump(procDevEnv.Id, dumpFilename, fIncludeFullHeap: true, UseSnapshot: false);
            VerifyDumpFile(dumpFilename);
        }
        [TestMethod]
        public void TestPssSnapshotJustDumpWithSnapshot()
        {
            var dumpFilename = GetDumpFileNameAndProcToDump(out var procDevEnv);
            MemoryDumpHelper.CollectDump(procDevEnv.Id, dumpFilename, fIncludeFullHeap: true, UseSnapshot: true);
            VerifyDumpFile(dumpFilename);
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
                | PssCaptureFlags.PSS_CREATE_RELEASE_SECTION;
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
            TestContext.WriteLine($"done #iter ={nIter} {sw.Elapsed.TotalSeconds:n0} secs   Secs/iter = {sw.Elapsed.TotalSeconds / nIter:n2}");
            lst.ForEach(s => PssFreeSnapshot(GetCurrentProcess(), s));
        }

        [TestMethod]
        [Ignore]
        public void TestPssUseComNoSnapshot()
        {
            if (IntPtr.Size == 4)
            {
                return;
            }
            var dumpFilename = GetDumpFileNameAndProcToDump(out var procDevEnv);
            var ocomcall = new CallCom();
            var iCreateDump = ocomcall.GetInterfaceICreateDump();
            var hr = iCreateDump.CreateDump(procDevEnv.Id, UseSnapshot: 0, pathDumpFileName: dumpFilename);
            TestContext.WriteLine($"Got CreateDump hr = {hr}");
            VerifyDumpFile(dumpFilename);
        }

        [TestMethod]
        [Ignore]
        public void TestPssUseComWithCSSnapshot()
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
                TestContext.WriteLine($"Got CreateDump hr = {hr}");
            }
            VerifyDumpFile(dumpFilename);
        }

        private string GetDumpFileNameAndProcToDump(out Process procDevEnv)
        {
            procDevEnv = Process.GetProcessesByName("devenv").Where(p => p.MainWindowTitle.IndexOf("hWndHost", StringComparison.OrdinalIgnoreCase) >= 0).First();
            TestContext.WriteLine($"proc {procDevEnv.MainWindowTitle}, {procDevEnv.Handle:x8}");
            var dumpFilename = Path.ChangeExtension(Path.GetTempFileName(), "dmp");
            //            dumpFilename = @"c:\t2.dmp";
            dumpFilename = $@"c:\{TestContext.TestName}.dmp";
            File.Delete(dumpFilename);
            return dumpFilename;
        }

        void VerifyDumpFile(string dumpFilename)
        {
            Assert.IsTrue(File.Exists(dumpFilename), $"Dump file not found {dumpFilename}");
            var dumpSize = new FileInfo(dumpFilename).Length;
            TestContext.WriteLine($"Dump Size  = {dumpSize:n0}");
            Assert.IsTrue(dumpSize > 100000000, $"Dump file size = {dumpSize:n0}");
        }

    }
}
