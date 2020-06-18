using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CreateDump64
{
    public class MainProgram
    {
        [STAThread]
        public static void Main(string[] args)
        {
            var x = new MainWindow();
            x.ShowDialog();
        }
    }
}
