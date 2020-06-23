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
        public void Test(string[] args)
        {
            //            var sb22 = new StringBuilder("asdf");
            var sb = new StringBuilder();
            var dt = DateTime.Now.ToString();
            sb.AppendLine(dt);
            sb.AppendLine(args[0]);
            sb.AppendLine("ssss");
            sb.AppendLine($"asdf{564}");

            File.WriteAllText(args[0], sb.ToString());

            Console.WriteLine(IntPtr.Size.ToString());
            Console.ReadLine();

        }
#if false
C:\VS\src\vscommon\testtools\PerfWatson2>corflags "C:\Program Files (x86)\Microsoft Visual Studio\2019\Master\Common7\IDE\PerfWatson2.exe"
Microsoft (R) .NET Framework CorFlags Conversion Tool.  Version  4.6.1055.0
Copyright (c) Microsoft Corporation.  All rights reserved.

Version   : v4.0.30319
CLR Header: 2.5
PE        : PE32
CorFlags  : 0x3
ILONLY    : 1
32BITREQ  : 1
32BITPREF : 0
Signed    : 1

C:\VS\src\vscommon\testtools\PerfWatson2>corflags "C:\Users\calvinh\source\repos\calvinhsia\CreateDump\CreateDump\bin\Debug\CreateDump.exe"
Microsoft (R) .NET Framework CorFlags Conversion Tool.  Version  4.6.1055.0
Copyright (c) Microsoft Corporation.  All rights reserved.

Version   : v4.0.30319
CLR Header: 2.5
PE        : PE32
CorFlags  : 0x20003
ILONLY    : 1
32BITREQ  : 0
32BITPREF : 1
Signed    : 0
#endif

        //public static void Main(string[] args) // simple version easy to generate via Reflection.Emit
        //{
        //    new MainProgram64(args);
        //}

        static string targ32bitDll;// = @"C:\Program Files (x86)\Microsoft Visual Studio\2019\Master\Common7\IDE\Microsoft.VisualStudio.PerfWatson.dll";
        static public void Main(string[] args) // simple version easy to generate via Reflection.Emit
        {
            // "fullnameOf32BitAsm", NameOfType,NameOfMethod,Pid, "dumpfile"
            // "C:\Users\calvinh\source\repos\CreateDump\CreateDump\bin\Debug\CreateDump.exe" MemoryDumpHelper CollectDump, 18844, "c:\users\calvinh\t.dmp"
            // "C:\Program Files (x86)\Microsoft Visual Studio\2019\Master\Common7\IDE\PerfWatson2.exe" MemoryDumpHelper CollectDump, 22520, "c:\users\calvinh\t.dmp"
            AppDomain.CurrentDomain.AssemblyResolve += CurrentDomain_AssemblyResolve;
            try
            {
                //if (args.Length < 5)
                //{
                //    args = new[] {
                //        targ32bitDll,
                //        "MemoryDumpHelper",
                //        "CollectDump",
                //        "13952",
                //        @"c:\users\calvinh\t.dmp"
                //    };
                //}
                targ32bitDll = args[0];
                //Environment.CurrentDirectory = @"C:\Program Files (x86)\Microsoft Visual Studio\2019\Master\Common7\IDE";
                var asmprog32 = Assembly.LoadFrom(args[0]);
                //                var typs = asmprog32.DefinedTypes;
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
            AppDomain.CurrentDomain.AssemblyResolve -= CurrentDomain_AssemblyResolve;
        }

        private static Assembly CurrentDomain_AssemblyResolve(object sender, ResolveEventArgs args)
        {
            Assembly asm = null;
            var privAsmDir = Path.Combine(Path.GetDirectoryName(targ32bitDll), "PrivateAssemblies");
            var requestName = args.Name.Substring(0, args.Name.IndexOf(","));
            if (requestName == "Microsoft.VisualStudio.Telemetry")
            {
                asm = Assembly.LoadFrom(Path.Combine(privAsmDir, @"Microsoft.VisualStudio.Telemetry.dll"));
            }
            else if (requestName == "Newtonsoft.Json")
            {
                asm = Assembly.LoadFrom(Path.Combine(privAsmDir, @"Newtonsoft.Json.dll"));
            }
            return asm;
        }

        [STAThread]
        public static void Mainss(string[] args) // [0] is 1st arg. array with 0 elems if no args
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
                    Environment.CurrentDirectory = @"C:\Program Files (x86)\Microsoft Visual Studio\2019\Master\Common7\IDE";
                    var src32exe = @"C:\Program Files (x86)\Microsoft Visual Studio\2019\Master\Common7\IDE\PerfWatson2.exe";
                    src32exe = @"C:\Program Files (x86)\Microsoft Visual Studio\2019\Master\Common7\IDE\Microsoft.VisualStudio.PerfWatson.dll";
                    //                    src32exe = @"C:\Users\calvinh\source\repos\CreateDump\CreateDump\bin\Debug\CreateDump.exe";
                    args = new[] { src32exe ,
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
