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
        public TestContext TestContext { get; set; }
        [TestMethod]
        public void TestInvoke()
        {
            var tempExeName = Path.ChangeExtension(Path.GetTempFileName(), ".exe");
            tempExeName = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "MyTestAsm.exe");
            var tempOutputFileName = Path.ChangeExtension(tempExeName, ".log");
            File.Delete(tempExeName);
            File.Delete(tempOutputFileName);

            // first call the target code directly, then create an asm to call the code
            // 
//            TargetStaticClass.MyStaticMethodWithNoParams();

            var oCreator = new AssemblyCreator();
            InvokeMethodViaReflection(
                typeof(AssemblyCreator).Assembly.Location,
                nameof(TargetStaticClass),
                nameof(TargetStaticClass.MyStaticMethodWithNoParams),
                targArgs: new string[] { tempOutputFileName });
            var result = File.ReadAllText(tempOutputFileName);
            TestContext.WriteLine(result);
            Assert.IsTrue(result.Contains("Here I am in TargetStaticClass MyStaticMethodWithNoParams"), "Test content expected");

            Assert.IsTrue(result.Contains("IntPtr.Size == 4"), "Test content expected");
        }
        public void InvokeMethodViaReflection(string fullPathAsmName, string typeName, string methodName, string[] targArgs)
        {// write this very simply: we're going to use Reflection.Emit to create this code
            var outputFile = @"C:\Users\calvinh\Documents\MyTestAsm.log";
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
                            File.AppendAllText(outputFile, $"{parm.Name} {parm.ParameterType}");
                        }
                        object typInstance = null;
                        if (!type.IsAbstract) // static class
                        {
                            typInstance = Activator.CreateInstance(type);
                        }
                        var oparms = new object[parms.Length];
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
