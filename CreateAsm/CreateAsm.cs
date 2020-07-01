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
    /// Taking a process dump of a 64 bit process from a 32 bit process doesn't work. Even from 32 bit task manager.
    /// This code emits an Asm that can be made into a 64 bit executable
    /// The goal is to call a static method in 32 bit PerfWatson in a static class MemoryDumpHelper with the signature:
    ///           public static void CollectDump(int procid, string pathOutput, bool FullHeap)
    /// The generated asm can be saved as an exe on disk, then started from 32 bit code. 
    ///  A little wrinkle: in order to enumerate the types in the DLL, the Appdomain AsemblyResolver needs to find the dependencies
    /// The 64 bit process will then load the 32 bit PW IL (using the assembly resolver, then invoke the method via reflection)
    /// the parameters are pased to the 64 bit exe on the commandline.
    /// This code logs output to the output file (which is the dump file when called with logging false)
    /// The code generates a static Main (string[] args) method.
    ///  see https://github.com/calvinhsia/CreateDump
    /// </summary>
    public class AssemblyCreator
    {
        public Type CreateAssembly(
                string FullPathAsmToCreate,
                PortableExecutableKinds portableExecutableKinds,
                ImageFileMachine imageFileMachine,
                string AdditionalAssemblyPath,
                string fullPathAsmName,
                string typeName,
                string methodName,
                string[] targArgs
            )
        {

            return null;
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

    internal class TargetClass
    {
        //        string outputFile = @"C:\Users\calvinh\Documents\MyTestAsm.log";
        string outputFile => TargetStaticClass.outputFile;
        StringBuilder sb = new StringBuilder();
        private void MyPrivateMethodWith1Param(string param1)
        {
            sb.AppendLine($"{DateTime.Now}");
            sb.AppendLine($"Here I am in {nameof(TargetClass)} {nameof(MyPrivateMethodWith1Param)}");
            sb.AppendLine($"Assembly = {Assembly.GetExecutingAssembly().Location}");
            sb.AppendLine($"IntPtr.Size == {IntPtr.Size}");
            File.AppendAllText(outputFile, sb.ToString());
        }
    }
}
