using CreateDump;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace CreateDump64
{
    public class MainProgram64
    {
        void test()
        {
            Console.WriteLine(IntPtr.Size.ToString());
            Console.ReadLine();

        }

        public static void Main(string[] args) // simple version easy to generate via Reflection.Emit
        {
            // "C:\Users\calvinh\source\repos\CreateDump\CreateDump\bin\Debug\CreateDump.exe" MemoryDumpHelper CollectDump, 18844, "c:\users\calvinh\t.dmp"
            // "fullnameOfAsm", NameOfType,NameOfMethod,Pid, "dumpfile"
            try
            {
                var asmprog32 = Assembly.LoadFrom(args[0]);
                foreach (var type in asmprog32.GetExportedTypes())
                {
                    if (type.Name == args[1])
                    {
                        var methCollectDump = type.GetMethod(args[2]);
                        var memdumpHelper = Activator.CreateInstance(type);
                        methCollectDump.Invoke(memdumpHelper, new object[] { int.Parse(args[3]), args[4], true });
                        break;
                    }
                }
            }
            catch (Exception)
            {
            }
        }

        [STAThread]
        public static void MainOrig(string[] args) // [0] is 1st arg. array with 0 elems if no args
        {
            try
            {
                Debug.Assert(IntPtr.Size == 8);
                // "fullnameOfAsm", NameOfType,NameOfMethod,Pid, "dumpfile"
                // <fullpath>, MemoryDumpHelper, CollectDump,
                var prog32 = string.Empty;
                var typeName = string.Empty;
                var methName = string.Empty;
                var pidToDump = 0;
                var dumpFileName = string.Empty;

                if (args.Length == 0) // for testing
                {
                    var procNameToDump = "Microsoft.ServiceHub.Controller";
                    //                procNameToDump = "perfwatson2";
                    var procs = Process.GetProcessesByName(procNameToDump);
                    var dumpFilename = Path.ChangeExtension(Path.GetTempFileName(), "dmp");
                    args = new[] { @"C:\Users\calvinh\source\repos\CreateDump\CreateDump\bin\Debug\CreateDump.exe",
                                    "MemoryDumpHelper",
                                    "CollectDump",
                                    procs[0].Id.ToString(),
                                    dumpFilename };
                }
                if (args.Length == 5)
                {
                    prog32 = args[0];
                    typeName = args[1];
                    methName = args[2];
                    pidToDump = int.Parse(args[3]);
                    dumpFileName = args[4];
                }


                // if using linked source file
                //                new MemoryDumpHelper().CollectDump(procToDump, args[1], fIncludeFullHeap: true);



                var asmprog32 = Assembly.LoadFrom(prog32);
                var typMemoryDumpHelper = asmprog32.GetExportedTypes().Where(t => t.Name == typeName).Single();
                var methCollectDump = typMemoryDumpHelper.GetMethod(methName);
                var memdumpHelper = Activator.CreateInstance(typMemoryDumpHelper);
                methCollectDump.Invoke(memdumpHelper, new object[] { pidToDump, dumpFileName, true });

                //var x = new Window();
                //var sb = new StringBuilder();
                //sb.AppendLine($"in 64 bit process  IntPtr.Size = {IntPtr.Size} {Process.GetCurrentProcess().ProcessName}");
                //sb.AppendLine(args.Length == 0 ? "No args" : string.Join(" ", args));
                //x.Content = new TextBlock() { Text = sb.ToString() };
                //x.ShowDialog();

            }
            catch (Exception)
            {
            }
        }
    }
}
