using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace CreateAsm
{
    internal static class TargetStaticClass
    {
        public static string outputFile = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "MyTestAsm.log");
        static StringBuilder sb = new StringBuilder();
        public static void MyStaticMethodWithNoParams()
        {
            sb.AppendLine($"{DateTime.Now}");
            sb.AppendLine($"Here I am in {nameof(TargetStaticClass)} {nameof(MyStaticMethodWithNoParams)} Pid={Process.GetCurrentProcess().Id} {Process.GetCurrentProcess().MainModule.FileName}");
            sb.AppendLine($"Assembly = {Assembly.GetExecutingAssembly().Location}");
            sb.AppendLine($"IntPtr.Size == {IntPtr.Size}");
            File.AppendAllText(outputFile, sb.ToString());
        }
        public static void MyStaticMethodWith3Param(int param1, string param2, bool param3)
        {
            sb.AppendLine($"{DateTime.Now}");
            sb.AppendLine($"Here I am in {nameof(TargetStaticClass)} {nameof(MyStaticMethodWith3Param)} Pid={Process.GetCurrentProcess().Id} {Process.GetCurrentProcess().MainModule.FileName}");
            sb.AppendLine($"Assembly = {Assembly.GetExecutingAssembly().Location}");
            sb.AppendLine($"StaticParm1 = {param1} StaticParm2 = {param2} StaticParm3 = {param3}");
            sb.AppendLine($"IntPtr.Size == {IntPtr.Size}");
            File.AppendAllText(outputFile, sb.ToString());
        }
    }

    internal class TargetClass
    {
        //        string outputFile = @"C:\Users\calvinh\Documents\MyTestAsm.log";
        string outputFile => TargetStaticClass.outputFile;
        StringBuilder sb = new StringBuilder();
        private void MyPrivateMethodWith1Param(string param1)
        {
            sb.AppendLine($"{DateTime.Now}");
            sb.AppendLine($"Here I am in {nameof(TargetClass)} {nameof(MyPrivateMethodWith1Param)} Pid={Process.GetCurrentProcess().Id} {Process.GetCurrentProcess().MainModule.FileName}");
            sb.AppendLine($"Assembly = {Assembly.GetExecutingAssembly().Location}");
            sb.AppendLine($"IntPtr.Size == {IntPtr.Size}");
            File.AppendAllText(outputFile, sb.ToString());
        }
        private void MyMethodWith3Param(int param1, string param2, bool param3)
        {
            sb.AppendLine($"{DateTime.Now}");
            sb.AppendLine($"Here I am in {nameof(TargetClass)} {nameof(MyMethodWith3Param)} Pid={Process.GetCurrentProcess().Id} {Process.GetCurrentProcess().MainModule.FileName}");
            sb.AppendLine($"Assembly = {Assembly.GetExecutingAssembly().Location}");
            sb.AppendLine($"parm1 = {param1} parm2 = {param2} parm3 = {param3}");
            sb.AppendLine($"IntPtr.Size == {IntPtr.Size}");
            File.AppendAllText(outputFile, sb.ToString());
        }
    }
}
