using CreateAsm;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
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
        private string _tempOutputFileName;
        private string _typeName;

        public TestContext TestContext { get; set; }
        [TestInitialize]
        public void TestInit()
        {
            _tempExeName = Path.ChangeExtension(Path.GetTempFileName(), ".exe");
            // for debugging, use fixed file locations: tools can just reload/refresh
            _tempExeName = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "MyTestAsm.exe");

            _tempOutputFileName = Path.ChangeExtension(_tempExeName, ".log");
            File.Delete(_tempExeName);
            File.Delete(_tempOutputFileName);
            _typeName = Path.GetFileNameWithoutExtension(_tempExeName);
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
                nameof(TargetClass),
                nameof(TargetClass.MyMethodWith1Param),
                targArgs: new string[] { _tempOutputFileName });

            InvokeMethodViaReflection(
                typeof(AssemblyCreator).Assembly.Location,
                nameof(TargetStaticClass),
                nameof(TargetStaticClass.MyStaticMethodWith3Param),
                targArgs: new object[] { _tempOutputFileName, 123, true });

            var result = File.ReadAllText(_tempOutputFileName);
            TestContext.WriteLine(result);
            Assert.IsTrue(result.Contains("Here I am in TargetStaticClass MyStaticMethodWithNoParams"), "Test content expected");

            Assert.IsTrue(result.Contains("Here I am in TargetClass MyMethodWith1Param"), "Test content expected");

            Assert.IsTrue(result.Contains("parm2 = 123 parm3=True"), "Test content expected");

            Assert.IsTrue(result.Contains("IntPtr.Size == 4"), "Test content expected");
        }

        [TestMethod]
        public void TestInvokeViaAssembly()
        {
            var ocreateAsm = new AssemblyCreator(_tempExeName);
            ocreateAsm.CreateAssembly();
        }

        /// <summary>
        /// Want to invoke this assembly, type, method with these args.
        /// The output log is solely for debugging.
        /// note: may need AssemblyResolve event for dependencies. See GitHub samples https://github.com/calvinhsia/CreateDump
        /// </summary>
        /// <param name="fullPathAsmName">assembly with code to execute</param>
        /// <param name="typeName">the type name. Can be static or not</param>
        /// <param name="methodName"></param>
        /// <param name="targArgs">can be simple types, like int, bool,string</param>
        public void InvokeMethodViaReflection(string fullPathAsmName, string typeName, string methodName, object[] targArgs)
        {// write this very simply: we're going to use Reflection.Emit to create this code
            var outputFile = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "MyTestAsm.log");
            try
            {
                var targAsm = Assembly.LoadFrom(fullPathAsmName);
                File.AppendAllText(outputFile, $"Attempting to invoke via reflection: {typeName} {methodName}" + Environment.NewLine);
                foreach (var type in targAsm.GetTypes())
                {
                    if (type.Name == typeName)
                    {
                        var methinfo = type.GetMethod(methodName);
                        var parms = methinfo.GetParameters();
                        File.AppendAllText(outputFile, $"{typeName} {methodName} parms.Length={parms.Length}" + Environment.NewLine);
                        foreach (var parm in parms)
                        {
                            File.AppendAllText(outputFile, $"{parm.Name} {parm.ParameterType}" + Environment.NewLine);
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
                            oparms[i] = targArgs[i];
                        }
                        methinfo.Invoke(typInstance, oparms);
                    }
                }
            }
            catch (Exception ex)
            {
                File.AppendAllText(outputFile, $"Exception: {typeName} {methodName} {ex.ToString()}" + Environment.NewLine);
            }
        }

    }
}
