using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CreateDump
{
    public class Class1
    {
        public static void CollectDumpSimulator(int procid, string pathOutput, bool FullHeap)
        {

        }
        public static int CollectDumpSimulatorNoArgs()
        {
            Debug.WriteLine($"in {nameof(CollectDumpSimulatorNoArgs)}");
            Debug.Assert(false);
            return 1;
        }
    }
}
