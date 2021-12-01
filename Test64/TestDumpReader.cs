using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnitTestProject1;
using CreateDump;
using static CreateDump.MemoryDumpHelper.NativeMethods;
using static DumpUtilities.DumpUtilities.NativeMethods;
using System.Runtime.InteropServices;
using System.ComponentModel;
using System.IO;
using DumpUtilities;

namespace Test64
{
    [TestClass]
    public class TestDumpReader : TestBaseClass
    {
        [TestMethod]
        public async Task DumpReaderTest()
        {
            await Task.Yield();
            var dumpfilename = @"c:\TestPssSnapshotJustTriageDumpWithSnapshot.dmp";
            //dumpfilename = @"c:\TestPssSnapshotJustDumpWithSnapshot.dmp";
//            dumpfilename = @"c:\ToDoRajesh\TodoLocal.dmp";
            //            dumpfilename = @"C:\TodoRajesh\Todo.exe_210806_114200.dmp";
//            dumpfilename = @"C:\VSDbgTestDumps\MSSln22611\MSSln22611.dmp";
            Trace.WriteLine($"Dump Reader {dumpfilename}");

            ShowMiniDumpReaderData(dumpfilename);

        }

        public static void ShowMiniDumpReaderData(string dumpfilename)
        {
            using (var dumpReader = new DumpUtilities.DumpUtilities(dumpfilename))
            {
                Trace.WriteLine($"{dumpReader._minidumpFileSize:n0} ({dumpReader._minidumpFileSize:x8})");
                var arch = dumpReader.GetMinidumpStream<MINIDUMP_SYSTEM_INFO>(MINIDUMP_STREAM_TYPE.SystemInfoStream);
                Trace.WriteLine(arch);
                var misc = dumpReader.GetMinidumpStream<_MINIDUMP_MISC_INFO>(MINIDUMP_STREAM_TYPE.MiscInfoStream);
                Trace.WriteLine(misc);
                var lstStreamDirs = new List<MINIDUMP_DIRECTORY>();
                foreach (var strmtype in Enum.GetValues(typeof(MINIDUMP_STREAM_TYPE)))
                {
                    var dir = dumpReader.ReadMinidumpDirectoryForStreamType((MINIDUMP_STREAM_TYPE)strmtype);
                    if (dir.Location.Rva != 0)
                    {
                        lstStreamDirs.Add(dir);
                    }
                }
                foreach (var dir in lstStreamDirs.OrderBy(d => d.Location.Rva))
                {
                    Trace.WriteLine($"   Offset={dir.Location.Rva:x8}  Sz={dir.Location.DataSize:x8}  {dir.StreamType}");
                }

                int i = 0;
                foreach (var threaddata in dumpReader.EnumerateThreads())
                {
                    Trace.WriteLine($" {i++,3} TID {threaddata.ThreadId:x8} SusCnt: {threaddata.SuspendCount} TEB: {threaddata.Teb:x16}  StackStart{threaddata.Stack.StartOfMemoryRange:x16} StackSize ={threaddata.Stack.MemoryLocDesc.DataSize}");
                }
                i = 0;
                foreach (var moddata in dumpReader.EnumerateModules())
                {
                    Trace.WriteLine($"  {i++,3} Modules ImgSz={moddata.moduleInfo.SizeOfImage,10:n0} Addr= {moddata.moduleInfo.BaseOfImage:x8}   {moddata.ModuleName}");
                }
            }
        }
    }
}