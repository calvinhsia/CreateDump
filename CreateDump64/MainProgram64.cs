using CreateDump;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace CreateDump64
{
    public class MainProgram64
    {
        [STAThread]
        public static void Main(string[] args) // [0] is 1st arg. array with 0 elems if no args
        {
            try
            {
                var arg2 = Environment.CommandLine; // entire cmd line as string
                var arg3 = Environment.GetCommandLineArgs(); // [0] = fullpath to cur exe
                var procToDump = Process.GetProcessById(int.Parse(args[0]));
                MemoryDumpHelper.CollectDump(procToDump, args[1], fIncludeFullHeap: true);
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
