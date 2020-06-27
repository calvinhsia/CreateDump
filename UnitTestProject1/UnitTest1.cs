using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using CreateDump;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Performance.ResponseTime;

namespace UnitTestProject1
{
    [TestClass]
    public class UnitTest1
    {
        public TestContext TestContext { get; set; }
        string tempOutputFile;

        string targ32bitDll;
        readonly string TypeName = "MyType64";
        string targ64PEFile;
        string targDumpCollectorFile = @"CreateDump.exe";//@"C:\Users\calvinh\source\repos\CreateDump\CreateDump\bin\Debug\CreateDump.exe";

        [TestInitialize]
        public void Init()
        {
            var curexe = Process.GetCurrentProcess().MainModule.FileName;
            TestContext.WriteLine(curexe);
            // C:\Program Files (x86)\Microsoft Visual Studio\2019\Preview\Common7\IDE\Extensions\TestPlatform\testhost.x86.exe
            targ32bitDll = new FileInfo(Path.Combine(curexe, @"..\..\..", "Microsoft.VisualStudio.PerfWatson.dll")).FullName;

            // the output files are constant so that they can be seen via tools very quickly just by reloading
            tempOutputFile =Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), @"t.txt");// Path.ChangeExtension(Path.GetTempFileName(), "txt");
            File.Delete(tempOutputFile);
            targ64PEFile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), $@"{TypeName}.exe");
            File.Delete(targ64PEFile);
        }

#if DEBUG
        [TestMethod]
        public void TestDoSimpleMain()
        {
            //TestContext.WriteLine($"{Assembly.GetExecutingAssembly().Location}");
            //myMain();

            var procToDump = Process.GetProcessesByName("Microsoft.ServiceHub.Controller")[0];
            var oBuilder = new Create64BitDump(targ64PEFile, TypeName);
            oBuilder.CreateSimpleAsm();

            oBuilder._assemblyBuilder.SetEntryPoint(oBuilder._mainMethodBuilder, PEFileKinds.WindowApplication);
            oBuilder._assemblyBuilder.Save($"{TypeName}.exe", PortableExecutableKinds.ILOnly, ImageFileMachine.I386);
            var typ = Activator.CreateInstance(oBuilder._type);

            //"C:\Users\calvinh\source\repos\CreateDump\CreateDump\bin\Debug\CreateDump.exe" Class1 CollectDumpSimulatorNoArgs 123 "C:\Users\calvinh\Documents\t.txt"
            var args = new string[] { 
//                Assembly.GetExecutingAssembly().Location, 
                targDumpCollectorFile,
                nameof(Class1),
                "CollectDumpSimulator",
                procToDump.Id.ToString(),
                tempOutputFile,
            };
            var main = oBuilder._type.GetMethod("Main", BindingFlags.Static | BindingFlags.Public);
            main.Invoke(null, new object[] { args });
            Assert.IsTrue(File.Exists(tempOutputFile), $"Output file not found {tempOutputFile}");
            Assert.IsTrue(new FileInfo(tempOutputFile).LastWriteTime > DateTime.Now - TimeSpan.FromSeconds(1));
            var txtResults = File.ReadAllText(tempOutputFile);
            TestContext.WriteLine(txtResults);
            Assert.IsTrue(txtResults.Contains("In simple asm"), "Content not as expected");
            Assert.IsTrue(txtResults.Contains("Here i am "), "Content not as expected");
            Assert.IsTrue(txtResults.Contains("back from call"), "Content not as expected");
        }

        [TestMethod]
        public void TestGenWithException()
        {
            //TestContext.WriteLine($"{Assembly.GetExecutingAssembly().Location}");
            //myMain();
            var procToDump = Process.GetProcessesByName("Microsoft.ServiceHub.Controller")[0];
            var oBuilder = new Create64BitDump(targ64PEFile, TypeName);
            oBuilder.Create64BitExeUsingEmit(logOutput: true, CauseException: true);

            oBuilder._assemblyBuilder.SetEntryPoint(oBuilder._mainMethodBuilder, PEFileKinds.WindowApplication);
            oBuilder._assemblyBuilder.Save($"{TypeName}.exe", PortableExecutableKinds.ILOnly, ImageFileMachine.I386);
            var typ = Activator.CreateInstance(oBuilder._type);

            //"C:\Users\calvinh\source\repos\CreateDump\CreateDump\bin\Debug\CreateDump.exe" Class1 CollectDumpSimulatorNoArgs 123 "C:\Users\calvinh\Documents\t.txt"
            var args = new string[] { 
//                Assembly.GetExecutingAssembly().Location, 
                targDumpCollectorFile,
                nameof(Class1),
                "CollectDumpSimulator",
                procToDump.Id.ToString(),
                tempOutputFile,
            };
            var main = oBuilder._type.GetMethod("Main", BindingFlags.Static | BindingFlags.Public);
            main.Invoke(null, new object[] { args });
            Assert.IsTrue(File.Exists(tempOutputFile), $"Output file not found {tempOutputFile}");
            Assert.IsTrue(new FileInfo(tempOutputFile).LastWriteTime > DateTime.Now - TimeSpan.FromSeconds(1));
            var txtResults = File.ReadAllText(tempOutputFile);
            TestContext.WriteLine(txtResults);
            Assert.IsTrue(txtResults.Contains("In Generated Dynamic Assembly"), "Content not as expected");
            Assert.IsTrue(txtResults.Contains("System.NullReferenceException: Object reference not set to an instance of an object."), "Content not as expected");
        }

#endif

        [TestMethod]
        public void TestMakeAsm()
        {
            // make it work with tempfilenames
            //            var targ64PEFile = $@"c:\users\calvinh\{TypeName}.exe";
            var targ64PEFile = Path.ChangeExtension(Path.GetTempFileName(), "exe");
            var TypeName = Path.GetFileNameWithoutExtension(targ64PEFile);

            var procToDump = Process.GetProcessesByName("Microsoft.ServiceHub.Controller")[0];
            var oBuilder = new Create64BitDump(targ64PEFile, TypeName);
            oBuilder.Create64BitExeUsingEmit(logOutput: true);

            var testInProc = false;
            if (testInProc)
            {
                var typ = Activator.CreateInstance(oBuilder._type);

                var args = new string[] {
                    targDumpCollectorFile,
                    nameof(Class1),
                    "CollectDumpSimulator",
                    procToDump.Id.ToString(),
                    tempOutputFile,
                };
                var main = oBuilder._type.GetMethod("Main", BindingFlags.Static | BindingFlags.Public);
                main.Invoke(null, new object[] { args });
            }
            else
            {
                oBuilder._assemblyBuilder.SetEntryPoint(oBuilder._mainMethodBuilder, PEFileKinds.WindowApplication);
                oBuilder._assemblyBuilder.Save($"{TypeName}.exe", PortableExecutableKinds.PE32Plus, ImageFileMachine.AMD64);
                var p64 = Process.Start(
                    targ64PEFile,
                    $@"""{targDumpCollectorFile}"" {nameof(Class1)} CollectDumpSimulator {procToDump.Id} ""{tempOutputFile}""");
                if (p64.WaitForExit(10 * 1000))
                {

                }
                else
                {
                    Assert.Fail($"Process took too long {targ64PEFile}");
                }
            }
            Assert.IsTrue(File.Exists(tempOutputFile), $"Output file not found {tempOutputFile}");
            Assert.IsTrue(new FileInfo(tempOutputFile).LastWriteTime > DateTime.Now - TimeSpan.FromSeconds(1));
            var txtResults = File.ReadAllText(tempOutputFile);
            TestContext.WriteLine(txtResults);
            Assert.IsTrue(txtResults.Contains("In Generated Dynamic Assembly"), "Content not as expected");
            Assert.IsTrue(txtResults.Contains("Asm ResolveEvents events subscribed"), "Content not as expected");
            Assert.IsTrue(txtResults.Contains("Here i am "), "Content not as expected");
            Assert.IsTrue(IntPtr.Size == 4, "Running on 32 bit process");
            if (!testInProc)
            {
                Assert.IsTrue(txtResults.Contains("Intptr.Size == 8"), "Content not as expected");
                Assert.IsTrue(txtResults.Contains("Running in 64 bit Generated Assembly"), "Content not as expected");
            }
            else
            {
                Assert.IsTrue(txtResults.Contains("Intptr.Size == 4"), "Content not as expected");
            }
            Assert.IsTrue(txtResults.Contains("back from call"), "Content not as expected");
            Assert.IsTrue(txtResults.Contains("Asm ResolveEvents events Unsubscribed"), "Content not as expected");
        }

        [TestMethod]
        public void TestGet64BitDump()
        {
            var oBuilder = new Create64BitDump(targ64PEFile, TypeName);
            oBuilder.Create64BitExeUsingEmit(logOutput: false);
            oBuilder._assemblyBuilder.SetEntryPoint(oBuilder._mainMethodBuilder, PEFileKinds.WindowApplication);
            oBuilder._assemblyBuilder.Save($"{TypeName}.exe", PortableExecutableKinds.PE32Plus, ImageFileMachine.AMD64);

            Assert.IsTrue(File.Exists(targ64PEFile), $"Built EXE not found {targ64PEFile}");
            tempOutputFile = Path.ChangeExtension(tempOutputFile, ".dmp");

            File.Delete(tempOutputFile);
            var procToDump = Process.GetProcessesByName("Microsoft.ServiceHub.Controller")[0];
            var p64 = Process.Start(
                targ64PEFile,
                $@"""{targ32bitDll}"" MemoryDumpHelper CollectDump {procToDump.Id} ""{tempOutputFile}""");
            if (p64.WaitForExit(10 * 1000))
            {
                Assert.IsTrue(File.Exists(tempOutputFile), $"Output file not found {tempOutputFile}");
                var finfo = new FileInfo(tempOutputFile);
                Assert.IsTrue(finfo.LastWriteTime > DateTime.Now - TimeSpan.FromSeconds(1));
                TestContext.WriteLine($"Got results dump file len = {finfo.Length:n0} {tempOutputFile}");
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
            MainWindow.Get64BitDumpFromExeInResource(proc, dumpFilename, fIncludeFullHeap: true);
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
                    //                    var memdumpHelper = Activator.CreateInstance(typ); static, so don't need to create
                    methCollectDump.Invoke(null, new object[] { proc.Id, dumpFilename, true });
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
