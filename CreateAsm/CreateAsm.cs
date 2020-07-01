using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace CreateAsm
{
    /// <summary>
    /// We want to create an assembly that will be loaded in an exe (perhaps 64 bit) that will load and call a target method (could be static or non-static)
    /// Because it can be an external process, it will communicate back by writing to a text file
    /// </summary>
    public class AssemblyCreator
    {
        public AssemblyCreator(string _tempExeName)
        {

        }

        public void CreateAssembly()
        {
//            throw new NotImplementedException();
        }
    }

    public static class TargetStaticClass
    {
        public static string outputFile = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "MyTestAsm.log");
        static StringBuilder sb = new StringBuilder();
        public static void MyStaticMethodWithNoParams()
        {
            sb.AppendLine($"{DateTime.Now}");
            sb.AppendLine($"Here I am in {nameof(TargetStaticClass)} {nameof(MyStaticMethodWithNoParams)}");
            sb.AppendLine($"Assembly = {Assembly.GetExecutingAssembly().Location}");
            sb.AppendLine($"IntPtr.Size == {IntPtr.Size}");
            File.AppendAllText(outputFile, sb.ToString());
        }
        public static void MyStaticMethodWith1Param(string param1)
        {
            sb.AppendLine($"{DateTime.Now}");
            sb.AppendLine($"Here I am in {nameof(TargetStaticClass)} {nameof(MyStaticMethodWith1Param)}");
            sb.AppendLine($"Assembly = {Assembly.GetExecutingAssembly().Location}");
            sb.AppendLine($"IntPtr.Size == {IntPtr.Size}");
            File.AppendAllText(outputFile, sb.ToString());
        }
        public static void MyStaticMethodWith3Param(string param1, int param2, bool param3)
        {
            sb.AppendLine($"{DateTime.Now}");
            sb.AppendLine($"Here I am in {nameof(TargetStaticClass)} {nameof(MyStaticMethodWith3Param)}");
            sb.AppendLine($"Assembly = {Assembly.GetExecutingAssembly().Location}");
            sb.AppendLine($"parm1== {param1} parm2 = {param2} parm3={param3}");
            sb.AppendLine($"IntPtr.Size == {IntPtr.Size}");
            File.AppendAllText(outputFile, sb.ToString());
        }
    }

    public class TargetClass
    {
        //        string outputFile = @"C:\Users\calvinh\Documents\MyTestAsm.log";
        string outputFile => TargetStaticClass.outputFile;
        StringBuilder sb = new StringBuilder();
        public void MyMethodWith1Param(string param1)
        {
            sb.AppendLine($"{DateTime.Now}");
            sb.AppendLine($"Here I am in {nameof(TargetClass)} {nameof(MyMethodWith1Param)}");
            sb.AppendLine($"Assembly = {Assembly.GetExecutingAssembly().Location}");
            sb.AppendLine($"IntPtr.Size == {IntPtr.Size}");
            File.AppendAllText(outputFile, sb.ToString());
        }
    }
}
