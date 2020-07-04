using Microsoft.Performance.ResponseTime;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;
namespace UnitTestProject1

{
    [TestClass]
    public class CreateAsmTests
    {
        private string _tempExeName;
        private string _targ32bitPWDll;
        private string _tempOutputFile;
        private string _typeName;

        public TestContext TestContext { get; set; }
        [TestInitialize]
        public void TestInit()
        {
            TestContext.WriteLine($"{DateTime.Now} Starting test {TestContext.TestName}");
            _tempExeName = Path.ChangeExtension(Path.GetTempFileName(), ".exe");
            // for debugging, use fixed file locations: tools can just reload/refresh
            _tempExeName = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "MyTestAsm.exe");
            var curexe = Process.GetCurrentProcess().MainModule.FileName;
            _targ32bitPWDll = new FileInfo(Path.Combine(curexe, @"..\..\..", "Microsoft.VisualStudio.PerfWatson.dll")).FullName;

            _tempOutputFile = Path.ChangeExtension(_tempExeName, ".log");
            File.Delete(_tempExeName);
            File.Delete(_tempOutputFile);
            _typeName = Path.GetFileNameWithoutExtension(_tempExeName);
            File.AppendAllText(_tempOutputFile, $"{DateTime.Now} Starting {TestContext.TestName}\r\n");
            _additionalDirs = string.Empty;
        }
        [TestMethod]
        public void TestInvokeDirectly()
        {
            InvokeMethodViaReflection(
                typeof(AssemblyCreator).Assembly.Location,
                "TargetStaticClass",
                "MyStaticMethodWithNoParams",
                additionalDirs: string.Empty,
                targArgs: null);

            InvokeMethodViaReflection(
                typeof(AssemblyCreator).Assembly.Location,
                "TargetClass",
                "MyPrivateMethodWith1Param", // nameof doesn't work for private
                additionalDirs: string.Empty,
                targArgs: new string[] { _tempOutputFile });

            InvokeMethodViaReflection(
                typeof(AssemblyCreator).Assembly.Location,
                "TargetStaticClass",
                "MyStaticMethodWith3Param",
                additionalDirs: string.Empty,
                targArgs: new string[] { "123", _tempOutputFile, "true" });


            InvokeMethodViaReflection(
                typeof(AssemblyCreator).Assembly.Location,
                "TargetClass",
                "MyMethodWith3Param",
                additionalDirs: string.Empty,
                targArgs: new string[] { "123", _tempOutputFile, "true" });


            var result = File.ReadAllText(_tempOutputFile);
            TestContext.WriteLine(result);
            Assert.IsTrue(result.Contains("Here I am in TargetStaticClass MyStaticMethodWithNoParams"), "Test content expected");

            Assert.IsTrue(result.Contains("Here I am in TargetClass MyPrivateMethodWith1Param"), "Test content expected");

            Assert.IsTrue(result.Contains("parm1 = 123"), "Test content expected");

            Assert.IsTrue(result.Contains("parm3 = True"), "Test content expected");

            Assert.IsTrue(result.Contains("IntPtr.Size == 4"), "Test content expected");
        }

        [TestMethod]
        public void TestInvokeViaCreatedAssemblyUsingTempFile()
        {
            _tempExeName = Path.ChangeExtension(Path.GetTempFileName(), ".exe");
            TestInvokeViaCreatedAssembly();
        }

        [TestMethod]
        public void TestInvokeViaCreatedAssembly()
        {
            var type = new AssemblyCreator().CreateAssembly(
                _tempExeName,
                PortableExecutableKinds.PE32Plus,
                ImageFileMachine.AMD64,
                AdditionalAssemblyPaths: Path.Combine(Path.GetDirectoryName(_targ32bitPWDll), "PrivateAssemblies"),
                logOutput: true
            );
            Assert.IsTrue(File.Exists(_tempExeName), "generated asm not found");

            var procToDump = Process.GetProcessesByName("Microsoft.ServiceHub.Controller")[0];

            var targDllToRun = _targ32bitPWDll;
            targDllToRun = typeof(AssemblyCreator).Assembly.Location;
            var parm1 = procToDump.Id;
            var parm2 = _tempOutputFile;
            var parm3 = true;
            var p64 = Process.Start(
                _tempExeName,
                $@"""{targDllToRun}"" TargetStaticClass MyStaticMethodWith3Param {parm1} ""{parm2}"" {parm3}");
            if (p64.WaitForExit(10 * 1000))
            {
                Assert.IsTrue(File.Exists(_tempOutputFile), $"Output file not found {_tempOutputFile}");
                var finfo = new FileInfo(_tempOutputFile);
                Assert.IsTrue(finfo.LastWriteTime > DateTime.Now - TimeSpan.FromSeconds(1));
                var result = File.ReadAllText(_tempOutputFile);
                TestContext.WriteLine(result);
                Assert.IsTrue(result.Contains("InMyTestAsm!!!"), "Test content expected");
                Assert.IsTrue(result.Contains("Asm ResolveEvents events subscribed"), "Test content expected");
                Assert.IsTrue(result.Contains("Here I am in TargetStaticClass MyStaticMethodWith3Param"), "Test content expected");
                Assert.IsTrue(result.Contains($"StaticParm1 = {parm1}"), "Test content expected");
                Assert.IsTrue(result.Contains($"StaticParm2 = {parm2}"), "Test content expected");
                Assert.IsTrue(result.Contains($"StaticParm3 = {parm3}"), "Test content expected");
                Assert.IsTrue(IntPtr.Size == 4, "We're in a 32 bit process");
                Assert.IsTrue(result.Contains("IntPtr.Size == 8"), "we executed code in a 64 bit process");
                Assert.IsTrue(result.Contains("back from call"), "Test content expected");

                Assert.IsTrue(result.Contains("Asm ResolveEvents events Unsubscribed"), "Test content expected");
            }
            else
            {
                Assert.Fail($"Process took too long {_tempExeName}");
            }

            File.AppendAllText(_tempOutputFile, $"\r\nNow test non-static\r\n");

            p64 = Process.Start(
                _tempExeName,
                $@"""{targDllToRun}"" TargetClass MyMethodWith3Param {parm1} ""{parm2}"" {parm3}");
            if (p64.WaitForExit(10 * 1000))
            {
                Assert.IsTrue(File.Exists(_tempOutputFile), $"Output file not found {_tempOutputFile}");
                var finfo = new FileInfo(_tempOutputFile);
                Assert.IsTrue(finfo.LastWriteTime > DateTime.Now - TimeSpan.FromSeconds(1));
                var result = File.ReadAllText(_tempOutputFile);
                TestContext.WriteLine(result);
                Assert.IsTrue(result.Contains("isNotStatic"), "Test content expected");
                Assert.IsTrue(result.Contains($"parm1 = {parm1}"), "Test content expected");
                Assert.IsTrue(result.Contains($"parm2 = {parm2}"), "Test content expected");
                Assert.IsTrue(result.Contains($"parm3 = {parm3}"), "Test content expected");
            }
            else
            {
                Assert.Fail($"Process took too long {_tempExeName}");
            }
        }
        [TestMethod]
        public void TestInvokePWDirectly()
        {
            var procToDump = Process.GetProcessesByName("PerfWatson2")[0]; // 32 bit
            var dumpFile = Path.ChangeExtension(_tempOutputFile, ".dmp");
            InvokeMethodViaReflection(
                _targ32bitPWDll,
                "MemoryDumpHelper",
                "CollectDump",
                additionalDirs: @"c:\dummy;"+Path.Combine(Path.GetDirectoryName(_targ32bitPWDll), "PrivateAssemblies"),
                targArgs: new[] { $"{procToDump.Id}", dumpFile, "true" });

            var result = File.ReadAllText(_tempOutputFile);
            TestContext.WriteLine(result);

            Assert.IsTrue(File.Exists(_tempOutputFile), $"Output file not found {_tempOutputFile}");
            var finfo = new FileInfo(dumpFile);
            Assert.IsTrue(finfo.LastWriteTime > DateTime.Now - TimeSpan.FromSeconds(1));
            Assert.IsTrue(finfo.Length > 100 * 1000 * 1000, $"Dump should be big {finfo.Length} {dumpFile}");
            TestContext.WriteLine($"Got results dump file len = {finfo.Length:n0} {_tempOutputFile}");

            File.Delete(dumpFile);
        }


        [TestMethod]
        public void TestInvokePWViaCreatedAssembly()
        {
            var dumpFile = Path.ChangeExtension(_tempOutputFile, ".dmp");
            var addDirs = Path.Combine(Path.GetDirectoryName(_targ32bitPWDll), "PrivateAssemblies");
            var type = new AssemblyCreator().CreateAssembly(
                _tempExeName,
                PortableExecutableKinds.PE32Plus,
                ImageFileMachine.AMD64,
                @"c:\dummy;" + addDirs, // test that multiple folders work too
                logOutput: true
            );
            Assert.IsTrue(File.Exists(_tempExeName), "generated asm not found");

            var procToDump = Process.GetProcessesByName("Microsoft.ServiceHub.Controller")[0];

            var targDllToRun = _targ32bitPWDll;
            var p64 = Process.Start(
                _tempExeName,
                $@"""{targDllToRun}"" MemoryDumpHelper CollectDump {procToDump.Id} ""{dumpFile}"" true");
            if (p64.WaitForExit(10 * 1000))
            {
                Assert.IsTrue(File.Exists(_tempOutputFile), $"Log file not found {dumpFile}");
                Assert.IsTrue(new FileInfo(_tempOutputFile).LastWriteTime > DateTime.Now - TimeSpan.FromSeconds(1), $"new Log file not found {_tempOutputFile}");
                var result = File.ReadAllText(_tempOutputFile);
                TestContext.WriteLine(result);
                Assert.IsTrue(File.Exists(dumpFile), $"Dump file not found {dumpFile}");
                var finfo = new FileInfo(dumpFile);
                Assert.IsTrue(finfo.LastWriteTime > DateTime.Now - TimeSpan.FromSeconds(1));
                Assert.IsTrue(finfo.Length > 100 * 1000 * 1000, $"Dump should be big {finfo.Length}");
                TestContext.WriteLine($"Got results dump file len = {finfo.Length:n0} {dumpFile}");
                File.Delete(dumpFile);
            }
            else
            {
                Assert.Fail($"Process took too long {_tempExeName}");
            }
        }

        string _additionalDirs = string.Empty;
        /// <summary>
        /// Want to start an executable with this assembly with these args.
        /// The output log is solely for debugging.
        /// note: may need AssemblyResolve event for dependencies. See GitHub samples https://github.com/calvinhsia/CreateDump
        /// </summary>
        /// <param name="fullPathAsmName">existing assembly with target code to execute</param>
        /// <param name="typeName">the type name of the code to execute. Can be static or not</param>
        /// <param name="methodName">the method name fo the code to execute. It's return value if any is ignored. After all, we're running an EXE and returning</param>
        /// <param name="targArgs">as string, passed to the generated EXE. Can be simple types, like int, bool,string. The Exe will instantiate the type, invoke the method with these params</param>
        public void InvokeMethodViaReflection(
            string fullPathAsmName,
            string typeName,
            string methodName,
            string additionalDirs,
            string[] targArgs)
        {// write this very simply: we're going to use Reflection.Emit to create this code
            var logFile = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "MyTestAsm.log");
            var sb = new StringBuilder();
            try
            {
                if (!string.IsNullOrEmpty(additionalDirs))
                {
                    _additionalDirs = additionalDirs;
                    AppDomain.CurrentDomain.AssemblyResolve += CurrentDomain_AssemblyResolve;
                }

                var targAsm = Assembly.LoadFrom(fullPathAsmName);
                sb.AppendLine($"Attempting to invoke via reflection: {typeName} {methodName}");
                foreach (var type in targAsm.GetTypes())
                {
                    if (type.Name == typeName)
                    {
                        var methinfo = type.GetMethod(methodName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance);
                        var parms = methinfo.GetParameters();
                        sb.AppendLine($"{typeName} {methodName} parms.Length={parms.Length}");
                        foreach (var parm in parms)
                        {
                            sb.AppendLine($"{parm.Name} {parm.ParameterType}");
                        }
                        object typInstance = null;
                        if (!type.IsAbstract) // static class
                        {
                            typInstance = Activator.CreateInstance(type);
                        }
                        var oparms = new object[parms.Length];
                        if (targArgs == null && parms.Length > 0 || targArgs != null && targArgs.Length != parms.Length)
                        {
                            throw new ArgumentException($"Method {typeName}{methodName} requires #parms= {parms.Length}, but only {targArgs?.Length} provided");
                        }
                        for (int i = 0; i < parms.Length; i++)
                        {
                            var pname = parms[i].ParameterType.Name;
                            if (pname == "String")
                            {
                                oparms[i] = targArgs[i];
                            }
                            else if (pname == "Int32")
                            {
                                oparms[i] = int.Parse(targArgs[i]);
                            }
                            else if (pname == "Boolean")
                            {
                                oparms[i] = bool.Parse(targArgs[i]);
                            }
                        }
                        methinfo.Invoke(typInstance, oparms);
                    }
                }
            }
            catch (ReflectionTypeLoadException ex)
            {
                sb.AppendLine($"LoaderException: {ex.LoaderExceptions[0]}");
            }
            catch (Exception ex)
            {
                sb.AppendLine($"Exception: {typeName} {methodName} {ex.ToString()}");
            }
            AppDomain.CurrentDomain.AssemblyResolve -= CurrentDomain_AssemblyResolve;
            File.AppendAllText(logFile, sb.ToString());
        }
        private Assembly CurrentDomain_AssemblyResolve(object sender, ResolveEventArgs args)
        {
            Assembly asm = null;
            var requestName = args.Name.Substring(0, args.Name.IndexOf(",")) + ".dll"; // Microsoft.VisualStudio.Telemetry, Version=16.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
            var split = _additionalDirs.Split(new[] { ';' });
            foreach (var dir in split)
            {
                var trypath = Path.Combine(dir, requestName);
                if (File.Exists(trypath))
                {
                    asm = Assembly.LoadFrom(trypath);
                    if (asm != null)
                    {
                        break;
                    }
                }
            }
            return asm;
        }

    }
}
