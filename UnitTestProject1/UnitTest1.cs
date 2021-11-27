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
    public class UnitTest1: TestBaseClass
    {
        string _tempOutputFile;

        string _targ32bitPWDll;
        string _TypeName;
        string _targ64PEFile;

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
        [Ignore]// once PW is loaded with asm resolver, will load again with no exception
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
            var privAsmDir = Path.Combine(Path.GetDirectoryName(_targ32bitPWDll), "PrivateAssemblies");
            var requestName = args.Name.Substring(0, args.Name.IndexOf(",")); // Microsoft.VisualStudio.Telemetry, Version=16.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
            Assembly asm = Assembly.LoadFrom(Path.Combine(privAsmDir, $"{requestName}.dll"));
            return asm;

        }

    }
}
