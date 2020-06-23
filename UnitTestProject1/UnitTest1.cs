using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using CreateDump;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace UnitTestProject1
{
    [TestClass]
    public class UnitTest1
    {
        public TestContext TestContext { get; set; }

        string targ32bitDll;
        [TestInitialize]
        public void Init()
        {
            var curexe = Process.GetCurrentProcess().MainModule.FileName;
            TestContext.WriteLine(curexe);
            // C:\Program Files (x86)\Microsoft Visual Studio\2019\Preview\Common7\IDE\Extensions\TestPlatform\testhost.x86.exe
            targ32bitDll = new FileInfo(Path.Combine(curexe, @"..\..\..", "Microsoft.VisualStudio.PerfWatson.dll")).FullName;
        }

        [TestMethod]
        public void TestLoad64()
        {
            var TypeName = "MyType64";
            var targ64PEFile = $@"c:\users\calvinh\{TypeName}.exe";
            File.Delete(targ64PEFile);
            var oBuilder = new Create64Bit(targ64PEFile, TypeName);
            oBuilder.Create64BitExeUsingEmit();
            Assert.IsTrue(File.Exists(targ64PEFile), $"Built EXE not found {targ64PEFile}");
            var tempOutputFile = @"C:\Users\calvinh\Documents\t.txt";// Path.ChangeExtension(Path.GetTempFileName(), "txt");

            // try with invalid arg count

            File.Delete(tempOutputFile);
            var procToDump = Process.GetProcessesByName("Microsoft.ServiceHub.Controller")[0];
            var p64 = Process.Start(targ64PEFile, $@"""{targ32bitDll}"" MemoryDumpHelper CollectDump {procToDump.Id} ""{tempOutputFile}""");
            if (p64.WaitForExit(10 * 1000))
            {
                Assert.IsTrue(File.Exists(tempOutputFile), $"Output file not found {tempOutputFile}");
                Assert.IsTrue(new FileInfo(tempOutputFile).LastWriteTime > DateTime.Now - TimeSpan.FromSeconds(1));
                var txtResults = File.ReadAllText(tempOutputFile);
                TestContext.WriteLine(txtResults);
                Assert.IsTrue(txtResults.Contains("In 64 bit exe"), "Content not as expected");
                Assert.IsTrue(txtResults.Contains("Asm ResolveEvents"), "Content not as expected");
                Assert.IsTrue(txtResults.Contains("PrivateAssemblies"), "Content not as expected");

                Assert.IsTrue(txtResults.Contains("IsVsTelem"), "Content not as expected");
                Assert.IsTrue(txtResults.Contains("IsJson"), "Content not as expected");
            }
            else
            {
                Assert.Fail($"Process took too long {targ64PEFile}");
            }
        }




        [TestMethod]
        public void TestEmit()
        {
            var TypeName = "MyType64";
            var targ64PEFile = $@"c:\users\calvinh\{TypeName}.exe";
            File.Delete(targ64PEFile);
            var oBuilder = new Create64Bit(targ64PEFile, TypeName);
            oBuilder.TestCreate64BitExeUsingEmit();
            Assert.IsTrue(File.Exists(targ64PEFile), $"Built EXE not found {targ64PEFile}");
            var tempOutputFile = @"C:\Users\calvinh\Documents\t.txt";// Path.ChangeExtension(Path.GetTempFileName(), "txt");

            // try with invalid arg count

            File.Delete(tempOutputFile);
            var p64 = Process.Start(targ64PEFile, $@"{tempOutputFile} 2ndArg ""Microsoft.VisualStudio.Telemetry, Version=16.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a""");
            if (p64.WaitForExit(10 * 1000))
            {
                Assert.IsTrue(File.Exists(tempOutputFile), $"Output file not found {tempOutputFile}");
                Assert.IsTrue(new FileInfo(tempOutputFile).LastWriteTime > DateTime.Now - TimeSpan.FromSeconds(1));
                var txtResults = File.ReadAllText(tempOutputFile);
                TestContext.WriteLine(txtResults);
                Assert.IsTrue(txtResults.Contains("2ndArg"), "Content not as expected");
                Assert.IsTrue(txtResults.Contains("IsVsTelem"), "Content not as expected");

                Assert.IsTrue(txtResults.Contains("string in static field"), "Content not as expected");
            }
            else
            {
                Assert.Fail($"Process took too long {targ64PEFile}");
            }
        }


        [TestMethod]
        public void TestEmitExceptionHandler()
        {
            //            (var Temp64TargetFile, var tempOutputFile) = DoBuildAsm();
            var TypeName = "MyType64";
            var targ64PEFile = $@"c:\users\calvinh\{TypeName}.exe";
            File.Delete(targ64PEFile);
            var oBuilder = new Create64Bit(targ64PEFile, TypeName);
            oBuilder.TestCreate64BitExeUsingEmit();
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
                TestContext.WriteLine(txtResults);
                Assert.IsTrue(txtResults.Contains("System.IndexOutOfRangeException: Index was outside the bounds of the array."), "Content not as expected");
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
                TestContext.WriteLine(txtResults);
                Assert.IsTrue(txtResults.Contains("2ndArg"), "Content not as expected");
            }
            else
            {
                Assert.Fail($"Process took too long {targ64PEFile}");
            }
        }

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
        public void TestAsmLoadPW()
        {
            //targ32bitDll = @"c:\Microsoft.VisualStudio.PerfWatson.dll";
            TestContext.WriteLine(targ32bitDll);
            Assert.IsTrue(File.Exists(targ32bitDll));
            Exception exception = null;
            var procToDump = "ServiceHub.VSDetouredHost"; // must be 32 bit process when invoking directly
//            procToDump = "devenv";
            //                procToDump = "perfwatson2";
            var dumpFilename = Path.ChangeExtension(Path.GetTempFileName(), "dmp");
            var proc = Process.GetProcessesByName(procToDump)[0];
            var asm = Assembly.LoadFrom(targ32bitDll);
            try
            {
                var tps = asm.GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                exception = ex;
                TestContext.WriteLine($"Got expected {nameof(ReflectionTypeLoadException)}");
            }
            Assert.IsNotNull(exception);
            AppDomain.CurrentDomain.AssemblyResolve += MyAsmResolver;
            var tps2 = asm.GetTypes();
            foreach (var typ in tps2)
            {
                if (typ.Name == "MemoryDumpHelper")
                {
                    var methCollectDump = typ.GetMethod("CollectDump");
                    var memdumpHelper = Activator.CreateInstance(typ);
                    methCollectDump.Invoke(memdumpHelper, new object[] {proc.Id, dumpFilename , true});
                    break;
                }
            }
            Assert.IsTrue(File.Exists(dumpFilename), $"Dump file not found {dumpFilename}");
            var dumpSize = new FileInfo(dumpFilename).Length;
            TestContext.WriteLine($"Dump Size  = {dumpSize:n0}");
            Assert.IsTrue(dumpSize > 100000000, $"Dump file size = {dumpSize:n0}");
            File.Delete(dumpFilename);

            AppDomain.CurrentDomain.AssemblyResolve -= MyAsmResolver;


        }
        Assembly MyAsmResolver(object sender, ResolveEventArgs args)
        {
            Assembly asm = null;
            var privAsmDir = Path.Combine(Path.GetDirectoryName(targ32bitDll), "PrivateAssemblies");
            var requestName = args.Name.Substring(0, args.Name.IndexOf(",")); // Microsoft.VisualStudio.Telemetry, Version=16.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
            if (requestName == "Microsoft.VisualStudio.Telemetry")
            {
                asm = Assembly.LoadFrom(Path.Combine(privAsmDir, @"Microsoft.VisualStudio.Telemetry.dll"));
            }
            else if (requestName == "Newtonsoft.Json")
            {
                asm = Assembly.LoadFrom(Path.Combine(privAsmDir, @"Newtonsoft.Json.dll"));
            }
            return asm;

        }

    }
}
