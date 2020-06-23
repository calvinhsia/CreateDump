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
        public void TestGenerate64BitDumpFromExeInResource()
        {
            var procToDump = "Microsoft.ServiceHub.Controller";
            //                procToDump = "perfwatson2";
            var dumpFilename = Path.ChangeExtension(Path.GetTempFileName(), "dmp");
            var proc = Process.GetProcessesByName(procToDump)[0];
            TestContext.WriteLine($"Dumping {procToDump} {proc}");
            var ox = new MainWindow();
            ox.Get64BitDumpFromExeInResource(proc, dumpFilename, fIncludeFullHeap: true);
            Assert.IsTrue(File.Exists(dumpFilename), $"Dump file not found {dumpFilename}");
            var dumpSize = new FileInfo(dumpFilename).Length;
            TestContext.WriteLine($"Dump Size  = {dumpSize:n0}");
            Assert.IsTrue(dumpSize > 100000000, $"Dump file size = {dumpSize:n0}");
            File.Delete(dumpFilename);
        }

        [TestMethod]
        public void TestEmitExceptionHandler()
        {
            //            (var Temp64TargetFile, var tempOutputFile) = DoBuildAsm();
            var TypeName = "MyType64";
            var targ64PEFile = $@"c:\users\calvinh\{TypeName}.exe";
            File.Delete(targ64PEFile);
            var oBuilder = new Create64Bit(targ64PEFile,TypeName);
            oBuilder.Create64BitExeUsingEmit();
            Assert.IsTrue(File.Exists(targ64PEFile), $"Built EXE not found {targ64PEFile}");
            var tempOutputFile = @"C:\Users\calvinh\Documents\t.txt";// Path.ChangeExtension(Path.GetTempFileName(), "txt");

            // try with invalid arg count

            File.Delete(tempOutputFile);
            var p64 = Process.Start(targ64PEFile, tempOutputFile);
            if (p64.WaitForExit(10 * 1000))
            {
                Assert.IsTrue(File.Exists(tempOutputFile), $"Output file not found {tempOutputFile}");
                Assert.IsTrue(new FileInfo(tempOutputFile).LastWriteTime > DateTime.Now - TimeSpan.FromSeconds(1));
                var txtResults = File.ReadAllText(tempOutputFile);
                Assert.IsTrue(txtResults.Contains("System.IndexOutOfRangeException: Index was outside the bounds of the array."),"Content not as expected");
                TestContext.WriteLine(txtResults);
            }
            else
            {
                Assert.Fail($"Process took too long {targ64PEFile}");
            }
            p64 = Process.Start(targ64PEFile, $"{tempOutputFile} 2ndArg 3rd Arg");
            File.Delete(tempOutputFile);
            if (p64.WaitForExit(10 * 1000))
            {
                Assert.IsTrue(File.Exists(tempOutputFile), $"Output file not found {tempOutputFile}");
                Assert.IsTrue(new FileInfo(tempOutputFile).LastWriteTime > DateTime.Now - TimeSpan.FromSeconds(1));
                var txtResults = File.ReadAllText(tempOutputFile);
                Assert.IsTrue(txtResults.Contains("2ndArg"), "Content not as expected");
                TestContext.WriteLine(txtResults);
            }
            else
            {
                Assert.Fail($"Process took too long {targ64PEFile}");
            }

        }
    }
}
