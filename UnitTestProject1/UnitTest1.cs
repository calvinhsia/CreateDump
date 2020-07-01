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
        string _tempOutputFile;

        string _targ32bitPWDll;
        string _TypeName;
        string _targ64PEFile;
        string _targSimDumpCollectorFile = @"CreateDump.exe";//@"C:\Users\calvinh\source\repos\CreateDump\CreateDump\bin\Debug\CreateDump.exe";

        [TestInitialize]
        public void Init()
        {
            var curexe = Process.GetCurrentProcess().MainModule.FileName;
            TestContext.WriteLine(curexe);
            // C:\Program Files (x86)\Microsoft Visual Studio\2019\Preview\Common7\IDE\Extensions\TestPlatform\testhost.x86.exe
            _targ32bitPWDll = new FileInfo(Path.Combine(curexe, @"..\..\..", "Microsoft.VisualStudio.PerfWatson.dll")).FullName;
            _TypeName = "MyType64";
            // the output files are constant so that they can be seen via tools very quickly just by reloading
            _tempOutputFile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), @"t.txt");// Path.ChangeExtension(Path.GetTempFileName(), "txt");
            File.Delete(_tempOutputFile);
            _targ64PEFile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), $@"{_TypeName}.exe");
            File.Delete(_targ64PEFile);
        }

#if DEBUG
        [TestMethod]
        public void TestDoSimpleMain()
        {
            //TestContext.WriteLine($"{Assembly.GetExecutingAssembly().Location}");
            //myMain();

            var procToDump = Process.GetProcessesByName("Microsoft.ServiceHub.Controller")[0];
            var oBuilder = new Create64BitDump();
            var type = oBuilder.CreateSimpleAsm(_targ64PEFile, PortableExecutableKinds.ILOnly, ImageFileMachine.I386);

            var typInstance = Activator.CreateInstance(type);

            //"C:\Users\calvinh\source\repos\CreateDump\CreateDump\bin\Debug\CreateDump.exe" Class1 CollectDumpSimulatorNoArgs 123 "C:\Users\calvinh\Documents\t.txt"
            var args = new string[] { 
//                Assembly.GetExecutingAssembly().Location, 
                _targSimDumpCollectorFile,
                nameof(Simulator),
                "CollectDumpSimulator",
                procToDump.Id.ToString(),
                _tempOutputFile,
            };
            var main = type.GetMethod("Main", BindingFlags.Static | BindingFlags.Public);
            main.Invoke(null, new object[] { args });
            Assert.IsTrue(File.Exists(_tempOutputFile), $"Output file not found {_tempOutputFile}");
            Assert.IsTrue(new FileInfo(_tempOutputFile).LastWriteTime > DateTime.Now - TimeSpan.FromSeconds(1));
            var txtResults = File.ReadAllText(_tempOutputFile);
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
            var oBuilder = new Create64BitDump();
            var type = oBuilder.Create64BitExeUsingEmit(
                _targ64PEFile,
                PortableExecutableKinds.ILOnly,
                ImageFileMachine.I386,
                AdditionalAssemblyPath: "PrivateAssemblies",
                logOutput: true,
                CauseException: true);

            var typ = Activator.CreateInstance(type);

            //"C:\Users\calvinh\source\repos\CreateDump\CreateDump\bin\Debug\CreateDump.exe" Class1 CollectDumpSimulatorNoArgs 123 "C:\Users\calvinh\Documents\t.txt"
            var args = new string[] { 
//                Assembly.GetExecutingAssembly().Location, 
                _targSimDumpCollectorFile,
                nameof(Simulator),
                "CollectDumpSimulator",
                procToDump.Id.ToString(),
                _tempOutputFile,
            };
            var main = type.GetMethod("Main", BindingFlags.Static | BindingFlags.Public);
            main.Invoke(null, new object[] { args });
            Assert.IsTrue(File.Exists(_tempOutputFile), $"Output file not found {_tempOutputFile}");
            Assert.IsTrue(new FileInfo(_tempOutputFile).LastWriteTime > DateTime.Now - TimeSpan.FromSeconds(1));
            var txtResults = File.ReadAllText(_tempOutputFile);
            TestContext.WriteLine(txtResults);
            Assert.IsTrue(txtResults.Contains("In Generated Dynamic Assembly"), "Content not as expected");
            Assert.IsTrue(txtResults.Contains("System.NullReferenceException: Object reference not set to an instance of an object."), "Content not as expected");
        }

#endif

        [TestMethod]
        public void TestMakeSimpleAsm()
        {
            // make it work with tempfilenames
            //            var targ64PEFile = $@"c:\users\calvinh\{TypeName}.exe";
            var targ64PEFile = Path.ChangeExtension(Path.GetTempFileName(), "exe");
            _TypeName = Path.GetFileNameWithoutExtension(targ64PEFile);

            makeAsmHelper(targ64PEFile, _TypeName, _targSimDumpCollectorFile, nameof(Simulator), "CollectDumpSimulator", testInProc: false, AdditionalAsserts: (txtResults) =>
             {
                 Assert.IsTrue(txtResults.Contains("Here i am "), "Content not as expected");
                 Assert.IsTrue(txtResults.Contains("Intptr.Size == 8"), "Content not as expected");
                 Assert.IsTrue(txtResults.Contains("Running in 64 bit Generated Assembly"), "Content not as expected");
                 Assert.IsTrue(txtResults.Contains("back from call"), "Content not as expected");
             });
        }

        [TestMethod]
        public void TestMakeAsmWithPW()
        {
            makeAsmHelper(_targ64PEFile, _TypeName, _targ32bitPWDll, "MemoryDumpHelper", "CollectDump", testInProc: false, AdditionalAsserts: (txtResults) =>
              {
                  Assert.IsTrue(txtResults.Contains("InResolveMethod"), "Content not as expected");
                  Assert.IsTrue(txtResults.Contains("Microsoft.VisualStudio.Telemetry"), "Content not as expected");
                  Assert.IsTrue(txtResults.Contains("Newtonsoft.Json"), "Content not as expected");
                  Assert.IsTrue(txtResults.Contains("System.Reflection.TargetParameterCountException: Parameter count mismatch"), "PW doesn't have extra stringbuilder parameter");
              });
        }

        private void makeAsmHelper(
            string targ64PEFile,
            string typeName,
            string targSimDumpCollectorFile,
            string className,
            string methodName,
            bool testInProc,
            Action<string> AdditionalAsserts)
        {
            var procToDump = Process.GetProcessesByName("Microsoft.ServiceHub.Controller")[0];
            var oBuilder = new Create64BitDump();
            var type = oBuilder.Create64BitExeUsingEmit(
                targ64PEFile, 
                PortableExecutableKinds.PE32Plus, 
                ImageFileMachine.AMD64,
                AdditionalAssemblyPath: "PrivateAssemblies",
                logOutput: true);

            if (testInProc)
            {
                var typInstance = Activator.CreateInstance(type);

                var args = new string[] {
                    targSimDumpCollectorFile,
                    className,
                    methodName,
                    procToDump.Id.ToString(),
                    _tempOutputFile,
                };
                var main = type.GetMethod("Main", BindingFlags.Static | BindingFlags.Public);
                main.Invoke(null, new object[] { args });
            }
            else
            {
                var p64 = Process.Start(
                    targ64PEFile,
                    $@"""{targSimDumpCollectorFile}"" {className} {methodName} {procToDump.Id} ""{_tempOutputFile}""");
                if (p64.WaitForExit(10 * 1000))
                {

                }
                else
                {
                    Assert.Fail($"Process took too long {targ64PEFile}");
                }
            }
            Assert.IsTrue(File.Exists(_tempOutputFile), $"Output file not found {_tempOutputFile}");
            Assert.IsTrue(new FileInfo(_tempOutputFile).LastWriteTime > DateTime.Now - TimeSpan.FromSeconds(1));
            var txtResults = File.ReadAllText(_tempOutputFile);
            TestContext.WriteLine(txtResults);
            Assert.IsTrue(txtResults.Contains("In Generated Dynamic Assembly"), "Content not as expected");
            Assert.IsTrue(txtResults.Contains("Asm ResolveEvents events subscribed"), "Content not as expected");
            Assert.IsTrue(txtResults.Contains("GotOurType"), "Content not as expected");
            Assert.IsTrue(txtResults.Contains("GotOurMethod"), "Content not as expected");
            AdditionalAsserts(txtResults);
            Assert.IsTrue(IntPtr.Size == 4, "Running on 32 bit process");
            if (!testInProc)
            {
            }
            else
            {
                Assert.IsTrue(txtResults.Contains("Intptr.Size == 4"), "Content not as expected");
            }
            Assert.IsTrue(txtResults.Contains("Asm ResolveEvents events Unsubscribed"), "Content not as expected");
        }

        [TestMethod]
        public void TestGet64BitDump()
        {
            var oBuilder = new Create64BitDump();
            var type = oBuilder.Create64BitExeUsingEmit(
                _targ64PEFile, 
                PortableExecutableKinds.PE32Plus, 
                ImageFileMachine.AMD64,
                AdditionalAssemblyPath: "PrivateAssemblies",
                logOutput: false);

            Assert.IsTrue(File.Exists(_targ64PEFile), $"Built EXE not found {_targ64PEFile}");
            _tempOutputFile = Path.ChangeExtension(_tempOutputFile, ".dmp");

            File.Delete(_tempOutputFile);
            var procToDump = Process.GetProcessesByName("Microsoft.ServiceHub.Controller")[0];
            var p64 = Process.Start(
                _targ64PEFile,
                $@"""{_targ32bitPWDll}"" MemoryDumpHelper CollectDump {procToDump.Id} ""{_tempOutputFile}""");
            if (p64.WaitForExit(10 * 1000))
            {
                Assert.IsTrue(File.Exists(_tempOutputFile), $"Output file not found {_tempOutputFile}");
                var finfo = new FileInfo(_tempOutputFile);
                Assert.IsTrue(finfo.LastWriteTime > DateTime.Now - TimeSpan.FromSeconds(1));
                Assert.IsTrue(finfo.Length > 200 * 1000 * 1000, "Dump should be big");
                TestContext.WriteLine($"Got results dump file len = {finfo.Length:n0} {_tempOutputFile}");
            }
            else
            {
                Assert.Fail($"Process took too long {_targ64PEFile}");
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
            TestContext.WriteLine(_targ32bitPWDll);
            Assert.IsTrue(File.Exists(_targ32bitPWDll));
            Exception exception = null;
            var procToDump = "ServiceHub.VSDetouredHost"; // must be 32 bit process when invoking directly
                                                          //            procToDump = "devenv";
                                                          //                procToDump = "perfwatson2";
            var dumpFilename = Path.ChangeExtension(Path.GetTempFileName(), "dmp");
            var proc = Process.GetProcessesByName(procToDump)[0];
            var asm = Assembly.LoadFrom(_targ32bitPWDll);
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
            var privAsmDir = Path.Combine(Path.GetDirectoryName(_targ32bitPWDll), "PrivateAssemblies");
            var requestName = args.Name.Substring(0, args.Name.IndexOf(",")); // Microsoft.VisualStudio.Telemetry, Version=16.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
            asm = Assembly.LoadFrom(Path.Combine(privAsmDir, $"{requestName}.dll"));
            return asm;

        }

    }
}
