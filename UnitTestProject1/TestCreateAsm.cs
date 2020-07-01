using CreateAsm;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
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
            File.AppendAllText(_tempOutputFile, $"{DateTime.Now} Starting {nameof(TestInvokeViaCreatedAssembly)}\r\n");
        }
        [TestMethod]
        public void TestInvokeDirectly()
        {
            InvokeMethodViaReflection(
                typeof(AssemblyCreator).Assembly.Location,
                nameof(TargetStaticClass),
                nameof(TargetStaticClass.MyStaticMethodWithNoParams),
                targArgs: null);

            InvokeMethodViaReflection(
                typeof(AssemblyCreator).Assembly.Location,
                "TargetClass",
                "MyPrivateMethodWith1Param", // nameof doesn't work for private
                targArgs: new string[] { _tempOutputFile });

            InvokeMethodViaReflection(
                typeof(AssemblyCreator).Assembly.Location,
                nameof(TargetStaticClass),
                nameof(TargetStaticClass.MyStaticMethodWith3Param),
                targArgs: new string[] { "123", _tempOutputFile, "true" });

            var result = File.ReadAllText(_tempOutputFile);
            TestContext.WriteLine(result);
            Assert.IsTrue(result.Contains("Here I am in TargetStaticClass MyStaticMethodWithNoParams"), "Test content expected");

            Assert.IsTrue(result.Contains("Here I am in TargetClass MyPrivateMethodWith1Param"), "Test content expected");

            Assert.IsTrue(result.Contains("parm1== 123"), "Test content expected");

            Assert.IsTrue(result.Contains("parm3=True"), "Test content expected");

            Assert.IsTrue(result.Contains("IntPtr.Size == 4"), "Test content expected");
        }

        [TestMethod]
        public void TestInvokePWViaCreatedAssembly()
        {
            _tempOutputFile = Path.ChangeExtension(_tempOutputFile, ".dmp");

            var type = new AssemblyCreator().CreateAssembly(
                _tempExeName,
                PortableExecutableKinds.PE32Plus,
                ImageFileMachine.AMD64,
                "PrivateAssemblies",
                logOutput: false
            );
            Assert.IsTrue(File.Exists(_tempExeName), "generated asm not found");

            var procToDump = Process.GetProcessesByName("Microsoft.ServiceHub.Controller")[0];

            var targDllToRun = _targ32bitPWDll;
            var p64 = Process.Start(
                _tempExeName,
                $@"""{targDllToRun}"" MemoryDumpHelper CollectDump {procToDump.Id} ""{_tempOutputFile}"" true");
            if (p64.WaitForExit(10 * 1000))
            {
                Assert.IsTrue(File.Exists(_tempOutputFile), $"Output file not found {_tempOutputFile}");
                var finfo = new FileInfo(_tempOutputFile);
                Assert.IsTrue(finfo.LastWriteTime > DateTime.Now - TimeSpan.FromSeconds(1));
                Assert.IsTrue(finfo.Length > 200 * 1000 * 1000, "Dump should be big");
                TestContext.WriteLine($"Got results dump file len = {finfo.Length:n0} {_tempOutputFile}");



                //Assert.IsTrue(File.Exists(_tempOutputFile), $"Output file not found {_tempOutputFile}");
                //var result = File.ReadAllText(_tempOutputFile);
                //TestContext.WriteLine(result);
                //Assert.IsTrue(result.Contains("InMyTestAsm!!!"), "Test content expected");
                //Assert.IsTrue(result.Contains("Asm ResolveEvents events subscribed"), "Test content expected");
                //Assert.IsTrue(result.Contains("Here I am in TargetStaticClass MyStaticMethodWith3Param"), "Test content expected");
                //Assert.IsTrue(IntPtr.Size == 4, "We're in a 32 bit process");
                //Assert.IsTrue(result.Contains("IntPtr.Size == 8"), "we're in a 64 bit process");
                //Assert.IsTrue(result.Contains("back from call"), "Test content expected");

                //Assert.IsTrue(result.Contains("Asm ResolveEvents events Unsubscribed"), "Test content expected");
            }
            else
            {
                Assert.Fail($"Process took too long {_tempExeName}");
            }
        }

        [TestMethod]
        public void TestInvokeViaCreatedAssembly()
        {
            var type = new AssemblyCreator().CreateAssembly(
                _tempExeName,
                PortableExecutableKinds.PE32Plus,
                ImageFileMachine.AMD64,
                "PrivateAssemblies",
                logOutput:true
            );
            Assert.IsTrue(File.Exists(_tempExeName),"generated asm not found");

            var procToDump = Process.GetProcessesByName("Microsoft.ServiceHub.Controller")[0];

            var targDllToRun = _targ32bitPWDll;
            targDllToRun = typeof(AssemblyCreator).Assembly.Location;
            var p64 = Process.Start(
                _tempExeName,
                $@"""{targDllToRun}"" {nameof(TargetStaticClass)} {nameof(TargetStaticClass.MyStaticMethodWith3Param)} {procToDump.Id} ""{_tempOutputFile}"" true");
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
                Assert.IsTrue(IntPtr.Size == 4, "We're in a 32 bit process");
                Assert.IsTrue(result.Contains("IntPtr.Size == 8"), "we're in a 64 bit process");
                Assert.IsTrue(result.Contains("back from call"), "Test content expected");

                Assert.IsTrue(result.Contains("Asm ResolveEvents events Unsubscribed"), "Test content expected");
            }
            else
            {
                Assert.Fail($"Process took too long {_tempExeName}");
            }
        }

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
            string[] targArgs)
        {// write this very simply: we're going to use Reflection.Emit to create this code
            var logFile = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "MyTestAsm.log");
            var sb = new StringBuilder();
            try
            {
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
                            switch (parms[i].ParameterType.Name)
                            {
                                case "String":
                                    oparms[i] = targArgs[i];
                                    break;
                                case "Int32":
                                    oparms[i] = int.Parse(targArgs[i]);
                                    break;
                                case "Boolean":
                                    oparms[i] = bool.Parse(targArgs[i]);
                                    break;
                            }
                        }
                        methinfo.Invoke(typInstance, oparms);
                    }
                }
            }
            catch (Exception ex)
            {
                sb.AppendLine($"Exception: {typeName} {methodName} {ex.ToString()}");
            }
            File.AppendAllText(logFile, sb.ToString());
        }

    }
}
