using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CreateDump
{
    // these are same as MemoryDumpHelper
    internal static class Class1
    {
        public static void CollectDumpSimulator(int procid, string pathOutput, bool FullHeap, StringBuilder sb)
        {
            sb.AppendLine($"Here i am {DateTime.Now} {procid} {pathOutput} {FullHeap}");
            sb.AppendLine($"This is coming from {Process.GetCurrentProcess().MainModule.FileName}");
            sb.AppendLine($"Intptr.Size == { IntPtr.Size}");
            //            Debug.Assert(false);
        }
    }
}
