using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CreateDump
{
    internal class Class1
    {
        internal static void CollectDumpSimulator(int procid, string pathOutput, bool FullHeap)
        {
            File.WriteAllText(@"C:\Users\calvinh\Documents\t.txt", $"Here i am {procid} {pathOutput} {FullHeap}");
            Debug.WriteLine($"in {nameof(CollectDumpSimulatorNoArgs)}");
            //            Debug.Assert(false);
        }
        internal static int CollectDumpSimulatorNoArgs()
        {
            Debug.WriteLine($"in {nameof(CollectDumpSimulatorNoArgs)}");
//            Debug.Assert(false);
            return 1;
        }
    }
}
