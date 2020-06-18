using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Xml;
using HANDLE = System.IntPtr;

namespace CreateDump
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            this.Loaded += MainWindow_Loaded;
        }

        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            await Task.Yield();
            this.Top = 0;
            this.Left = 0;
            this.Width = 100;
            this.Height = 100;
            try
            {
                // reg add hkcu\Software\Microsoft\VisualStudio\16.0_Remote\PerfWatson\InternalUser /v SatProcRuleId /d myruleid
                //using (var hKey = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\VisualStudio\16.0_Remote\PerfWatson\InternalUser", writable: true))
                //{
                //    hKey.SetValue("SatProcRuleId", "Myruleid");

                //    var x = hKey.GetValue("SatProcRuleId");
                //}

                //using (var hKey = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\VisualStudio", writable: true))
                //{
                //    hKey.DeleteSubKeyTree(@"16.0_Remote");
                //}

                var procToDump = "Microsoft.ServiceHub.Controller";
                //                procToDump = "perfwatson2";
                var procs = Process.GetProcessesByName(procToDump);
                if (procs.Length > 0)
                {
                    var proc = procs[0];
                    var dumpFilename = Path.ChangeExtension(Path.GetTempFileName(), "dmp");
                    MemoryDumpHelper.CollectDump(proc, dumpFilename, fIncludeFullHeap: false);
                }
            }
            catch (Exception ex)
            {
                this.Content = ex.ToString();
            }
        }

    }



}

