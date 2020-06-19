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
            this.Width = 600;
            this.Height = 500;
            try
            {
                // use 32 bit task manager to dump 64 bit process. Result is quick exit with file length ==0
                Debug.Assert(IntPtr.Size == 4, "in 32 bit proc");
                var procToDump = "Microsoft.ServiceHub.Controller";
                //                procToDump = "perfwatson2";
                var procs = Process.GetProcessesByName(procToDump);
                if (procs.Length > 0)
                {
                    var proc = procs[0];
                    var dumpFilename = Path.ChangeExtension(Path.GetTempFileName(), "dmp");
                    try
                    {
                        MemoryDumpHelper.CollectDump(proc, dumpFilename, fIncludeFullHeap: true);
                    }
                    catch (Exception ex)
                    {
                        // PerfWatson is a 32 bit process, which can't get a dump of a 64 bit process
                        // we don't know for sure that a particular process is 64 bit (on 32 bit Windows, it may run in 32 bit mode)
                        // Capture a 64 bit dump on 32 bit windows
                        Get64BitDump(proc, dumpFilename, fIncludeFullHeap: true);
                    }
                }
            }
            catch (Exception ex)
            {
                this.Content = ex.ToString();
            }
        }

        private void Get64BitDump(Process proc, string dumpFilename, bool fIncludeFullHeap)
        {
            try
            {
                var tempExeFileName = Path.ChangeExtension(Path.GetTempFileName(), "exe");
                File.WriteAllBytes(tempExeFileName, Properties.Resources.CreateDump64);
                var procDump64 = Process.Start(tempExeFileName, $"{proc.Id} {dumpFilename}");
                procDump64.Exited += (o, e) =>
                {
                    this.Content = "procexited";

                };
                while (!procDump64.HasExited)
                {
                    Thread.Sleep(TimeSpan.FromSeconds(1));
                }
                this.Content = "procexited";

            }
            catch (Exception)
            {
            }
        }
    }



}

