using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using CreateDump;
using System.Diagnostics;
using System.Linq;
using System.IO;

namespace UnitTestProject1
{
    [TestClass]
    public class SnapShotTests
    {
        public TestContext TestContext { get; set; }

        [TestInitialize]
        public void inittest()
        {
            TestContext.WriteLine($"Starting test IntPtr.Size = {IntPtr.Size}");
        }

        [TestMethod]
        public void TestPssSnapshot()
        {
            var dumpFilename = GetDumpFileNameAndProcToDump(out var procDevEnv);
            MemoryDumpHelper.CollectDump(procDevEnv.Id, dumpFilename, fIncludeFullHeap: true, UseSnapshot: true);
            VerifyDumpFile(dumpFilename);
        }

        [TestMethod]
        public void TestPssCreateDumpViaCPPDll()
        {
            if (IntPtr.Size == 4)
            {
                return;
            }
            var dumpFilename = GetDumpFileNameAndProcToDump(out var procDevEnv);
            var ocomcall = new CallCom();
            var iCreateDump = ocomcall.GetInterfaceICreateDump();
            var hr = iCreateDump.CreateDump(procDevEnv.Id, UseSnapshot: 1, pathDumpFileName: dumpFilename);
            TestContext.WriteLine($"Got CreateDump hr = {hr}");
            VerifyDumpFile(dumpFilename);
        }

        private string GetDumpFileNameAndProcToDump( out Process procDevEnv)
        {
            procDevEnv = Process.GetProcessesByName("devenv").Where(p => p.MainWindowTitle.IndexOf("hWndHost", StringComparison.OrdinalIgnoreCase) >= 0).First();
            TestContext.WriteLine($"proc {procDevEnv.MainWindowTitle}, {procDevEnv.Handle:x8}");
            var dumpFilename = Path.ChangeExtension(Path.GetTempFileName(), "dmp");
            dumpFilename = @"c:\t2.dmp";
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
