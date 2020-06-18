using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using CreateDump;
namespace UnitTestProject1
{
    [TestClass]
    public class UnitTest1
    {
        public TestContext TestContext { get; set; }

        [TestMethod]
        public void TestMethod1()
        {
            Assert.AreEqual(IntPtr.Size, 8);
            TestContext.WriteLine(Process.GetCurrentProcess().Id.ToString() + " " + Process.GetCurrentProcess().MainModule.FileName);
            try
            {

                var procToDump = "Microsoft.ServiceHub.Controller";
                //                procToDump = "perfwatson2";
                var procs = Process.GetProcessesByName(procToDump);
                if (procs.Length > 0)
                {
                    var proc = procs[0];
                    var dumpFilename = Path.ChangeExtension(Path.GetTempFileName(), "dmp");
                    TestContext.WriteLine($"Creating dump of {procToDump}");
                    MemoryDumpHelper.CollectDump(proc, dumpFilename, fIncludeFullHeap: false);
                    TestContext.WriteLine($"Dumped to {dumpFilename}");
                }
            }
            catch (Exception ex)
            {
                TestContext.WriteLine(ex.ToString());
            }
            Thread.Sleep(TimeSpan.FromSeconds(10));
        }
    }
}
