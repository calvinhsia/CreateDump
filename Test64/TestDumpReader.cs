﻿using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnitTestProject1;
using CreateDump;
using static CreateDump.MemoryDumpHelper.NativeMethods;
using static DumpUtilities.DumpReader.NativeMethods;
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
            dumpfilename = @"C:\TodoRajesh\Todo.exe_210806_114200.dmp";
            //dumpfilename = @"c:\TestPssSnapshotJustDumpWithSnapshot.dmp";
            //            dumpfilename = @"c:\ToDoRajesh\TodoLocal.dmp";
            //            dumpfilename = @"C:\TodoRajesh\Todo.exe_210806_114200.dmp";
            //            dumpfilename = @"C:\VSDbgTestDumps\MSSln22611\MSSln22611.dmp";
            Trace.WriteLine($"Dump Reader {dumpfilename}");

            ShowMiniDumpReaderData(dumpfilename);

        }

        public static void ShowMiniDumpReaderData(string dumpfilename)
        {
            using (var dumpReader = new DumpUtilities.DumpReader(dumpfilename))
            {
                Trace.WriteLine($"{dumpReader._minidumpFileSize:n0} ({dumpReader._minidumpFileSize:x8})");
                var arch = dumpReader.GetMinidumpStream<MINIDUMP_SYSTEM_INFO>(MINIDUMP_STREAM_TYPE.SystemInfoStream);
                Trace.WriteLine(arch);
                var misc = dumpReader.GetMinidumpStream<_MINIDUMP_MISC_INFO>(MINIDUMP_STREAM_TYPE.MiscInfoStream);
                Trace.WriteLine(misc);
                var lstStreamDirs = new List<MINIDUMP_DIRECTORY>();

                foreach (MINIDUMP_STREAM_TYPE strmtype in Enum.GetValues(typeof(MINIDUMP_STREAM_TYPE)))
                {
                    var dir = dumpReader.ReadMinidumpDirectoryForStreamType(strmtype);
                    if (dir.Location.Rva != 0)
                    {
                        lstStreamDirs.Add(dir);
                        if (strmtype == MINIDUMP_STREAM_TYPE.CommentStreamW)
                        {
                            var dirComment = dumpReader.ReadMinidumpDirectoryForStreamType(strmtype);
                            if (dirComment.Location.Rva != 0)
                            {
                                var ptrData = dumpReader.MapRvaLocation(dirComment.Location);
                                var str = Marshal.PtrToStringUni(ptrData);
                                Trace.WriteLine($"Comment: {str}");
                            }
                        }
                    }
                }

                foreach (var dir in lstStreamDirs.OrderBy(d => d.Location.Rva))
                {
                    Trace.WriteLine($"   Offset={dir.Location.Rva:x8}  Sz={dir.Location.DataSize:x8}  {dir.StreamType}");
                }

                int i = 0;


                dumpReader.EnumerateStreamData<MINIDUMP_THREAD_LIST, MINIDUMP_THREAD>(MINIDUMP_STREAM_TYPE.ThreadListStream, item =>
                {
                    Trace.WriteLine($"{i++,3} {item.ToString()}");
                });
                dumpReader.EnumerateStreamData<MINIDUMP_THREAD_INFO_LIST, MINIDUMP_THREAD_INFO>(MINIDUMP_STREAM_TYPE.ThreadInfoListStream, item =>
                {
                    Trace.WriteLine($" {item.ToString()}");
                });


                //foreach (var threaddata in dumpReader.EnumerateThreads())
                //{
                //    Trace.WriteLine($" {i++,3} TID {threaddata.ThreadId:x8} SusCnt: {threaddata.SuspendCount} TEB: {threaddata.Teb:x16}  StackStart{threaddata.Stack.StartOfMemoryRange:x16} StackSize ={threaddata.Stack.MemoryLocDesc.DataSize}");
                //}
                i = 0;
                dumpReader.EnumerateStreamData<MINIDUMP_MODULE_LIST, MINIDUMP_MODULE>(MINIDUMP_STREAM_TYPE.ModuleListStream, item =>
                {
                    var modname = dumpReader.GetNameFromRva(item.ModuleNameRva);
                    Trace.WriteLine($"  {i++,3} Modules ImgSz={item.SizeOfImage,10:n0} Addr= {item.BaseOfImage:x8}   {modname}");
                });
            }
        }
    }
}