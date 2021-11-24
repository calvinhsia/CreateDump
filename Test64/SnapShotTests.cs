using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using CreateDump;
using System.Diagnostics;
using static CreateDump.MemoryDumpHelper.NativeMethods;
using System.Linq;
using System.IO;

namespace UnitTestProject1
{
    [TestClass]
    public class SnapShotTests
    {
        public TestContext TestContext { get; set; }

        [TestMethod]
        public void TestPssSnapshot()
        {
            TestContext.WriteLine($"Starting test IntPtr.Size = {IntPtr.Size}");
            var procDevEnv = Process.GetProcessesByName("devenv").Where(p => p.MainWindowTitle.Contains("Kusto")).First();
            TestContext.WriteLine($"proc {procDevEnv.MainWindowTitle}, {procDevEnv.Handle:x8}");
            var dumpFilename = Path.ChangeExtension(Path.GetTempFileName(), "dmp");
            dumpFilename = @"c:\t2.dmp";
            File.Delete(dumpFilename);

            MemoryDumpHelper.CollectDump(procDevEnv.Id, dumpFilename, fIncludeFullHeap: true, UseSnapshot: true);

            Assert.IsTrue(File.Exists(dumpFilename), $"Dump file not found {dumpFilename}");
            var dumpSize = new FileInfo(dumpFilename).Length;
            TestContext.WriteLine($"Dump Size  = {dumpSize:n0}");
            Assert.IsTrue(dumpSize > 100000000, $"Dump file size = {dumpSize:n0}");

        }
    }
}
