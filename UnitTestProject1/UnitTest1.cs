using System;
using System.Diagnostics;
using System.IO;
using CreateDump;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace UnitTestProject1
{
    [TestClass]
    public class UnitTest1
    {
        public TestContext TestContext { get; set; }
        [TestMethod]
        public void TestGenerate64BitDump()
        {
            var procToDump = "Microsoft.ServiceHub.Controller";
            //                procToDump = "perfwatson2";
            var dumpFilename = Path.ChangeExtension(Path.GetTempFileName(), "dmp");
            var proc = Process.GetProcessesByName(procToDump)[0];
            TestContext.WriteLine($"Dumping {procToDump} {proc}");
            var ox = new MainWindow();
            ox.Get64BitDump(proc, dumpFilename, fIncludeFullHeap: true);
            Assert.IsTrue(File.Exists(dumpFilename), $"Dump file not found {dumpFilename}");
            var dumpSize = new FileInfo(dumpFilename).Length;
            TestContext.WriteLine($"Dump Size  = {dumpSize:n0}");
            Assert.IsTrue(dumpSize > 100000000, $"Dump file size = {dumpSize:n0}");
            File.Delete(dumpFilename);
        }

        [TestMethod]
        public void TestCreate64BitExecutable()
        {
            var oBuilder = new Create64Bit();
            //            oBuilder.CreateAssembly();
            var asmName = @"c:\users\calvinh\t.asm";
            oBuilder.Create64BitExe(asmName);
        }
    }
}
