using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace UnitTestProject1
{
    public class TestBaseClass
    {

        public TestContext TestContext { get; set; }
        public MyTraceListener myTracelistener;

        [TestInitialize]
        public void Inittest()
        {
            myTracelistener = new MyTraceListener(this.TestContext);
            if (File.Exists(myTracelistener.LogfileName))
            {
                // init log to be empty
                myTracelistener.OutputToLogFileWithRetry(() =>
                {
                    File.WriteAllText(myTracelistener.LogfileName, string.Empty);
                });
            }

            Trace.Listeners.Add(myTracelistener);
            Trace.WriteLine($"Starting test {TestContext.TestName} IntPtr.Size = {IntPtr.Size}");
        }
        [TestCleanup]
        public void CleanupTest()
        {
            myTracelistener?.Dispose();
            myTracelistener = null;
        }
        protected void VerifyLogStrings(string strings, bool caseSensitive = false)
        {
            var strs = strings.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            Trace.WriteLine($"Test {TestContext.TestName} done. Now verifying Log Strings");
            // if we're using a TraceListenerAsync, then not all the strings have been written to the file yet, so we get the strings from the array
            var logstrs = myTracelistener.lstLoggedStrings.ToArray(); // 

            List<string> missingStrings = null;

            int nfailed = 0;
            Array.ForEach<string>(strs, str =>
            {
                string res = logstrs.Where(logstr => (caseSensitive ? logstr.Contains(str) : logstr.ToLower().Contains(str.ToLower()))).FirstOrDefault();
                if (string.IsNullOrEmpty(res))
                {
                    if (missingStrings == null)
                    {
                        missingStrings = new List<string>();
                    }

                    var errmsg = string.Format("ERROR: Expected '{0}' in log\n", str);
                    Trace.WriteLine(errmsg);
                    missingStrings.Add(str);
                    nfailed++;
                }
                else
                {
                    Trace.WriteLine(string.Format("Found '{0}': '{1}'", str, res));
                }
            });

            Assert.IsTrue(nfailed == 0, $"Expected strings not found in log.  {nfailed} Errors " + myTracelistener.LogfileName);
        }

        public async Task RunWinDbgWithCmdAsync(string dumpFileName, string cmds, bool ShutDownWinDbgWhenDone = true, int timeoutMins = 4, bool fIs64bit = false)
        {
            var workFolder = Path.GetDirectoryName(dumpFileName);
            var scriptFileName = Path.Combine(workFolder, "WinDbgScript.txt");
            File.Delete(scriptFileName); // del prior one if exists
            var windbgFilename = Path.Combine(@"c:\debuggers" + (fIs64bit ? string.Empty : @"\Wow64"), "windbg.exe");
            if (!File.Exists(windbgFilename))
            {
                throw new FileNotFoundException(windbgFilename);
            }
//            var logFileName = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "VsDbgExt.log");
            var logFileName = myTracelistener.LogfileName;

            //           logFileName = @"c:\t.log"; // on Cloud PC desktop  path = "C:\Users\calvinh\OneDrive - Microsoft\Desktop" which has embedded space. VSAnalyze /log calls dbgengine parser, whcih doesn't allow quotes.
            // easier to change Desktop Location to path with no space:Desktop=> Properties=>Location
            //C:\Users\calvinh\source\repos\VSDbg\out\Debug\TestVsDbg64\bin\
            var pathVsDbg = Path.GetDirectoryName(Path.GetDirectoryName(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)));
            pathVsDbg = Path.Combine(pathVsDbg, (fIs64bit ? @"VSDbg64\bin\x64\VSDbg64.dll" : @"VSDbg\bin\x86\VSDbg.dll"));
            if (fIs64bit)
            {
                pathVsDbg = @"C:\Users\calvinh\source\repos\VSDbg\out\Release\VSDbg64\bin\x64\VSDbg64.dll";
            }
            else
            {
                pathVsDbg = @"C:\Users\calvinh\source\repos\VSDbg\out\Release\vsdbg\bin\x86\vsdbg.dll";
            }
            if (!File.Exists(pathVsDbg))
            {
                throw new FileNotFoundException(pathVsDbg);
            }
            //var args = $"-c \"$<..\\..\\{WinDbgDumpAnalysisScriptsFolderName}\\{winDbgDumpAnalysisScriptFileName}\" -z \"{dumpFilePath}\"";
            var args = $"-c $<{scriptFileName} -z \"{dumpFileName}\"";
            Trace.WriteLine($"Starting {windbgFilename} {args}");
            var WindbgScript = $@"
.load {pathVsDbg}
!vsanalyze /log {logFileName}
{cmds}
";
            if (ShutDownWinDbgWhenDone) // for interactive debugging/testing
            {
                WindbgScript += @"
!vsanalyze /ShutDown
";
            }

            File.WriteAllText(scriptFileName, WindbgScript);
            var startinfo = new ProcessStartInfo(windbgFilename);
            startinfo.Arguments = args;
            var sw = Stopwatch.StartNew();
            var proc = Process.Start(startinfo);
            if (ShutDownWinDbgWhenDone)
            {
                while (!proc.HasExited)
                {
                    await Task.Delay(TimeSpan.FromSeconds(3));
                    if (sw.Elapsed.TotalMinutes > timeoutMins)
                    {
                        throw new InvalidOperationException($"WindDbg took longer than {timeoutMins} mins");
                    }
                }
            }
        }
    }

    public class MyTraceListener : TextWriterTraceListener
    {
        public List<string> lstLoggedStrings = new List<string>();
        bool IsInTraceListener = false;
        private TestContext _testcontext;
        public string LogfileName = Environment.ExpandEnvironmentVariables(@"%USERPROFILE%\Desktop\TestOutput.txt");

        public MyTraceListener(TestContext testContext)
        {
            this._testcontext = testContext;
        }
        public override void WriteLine(string str)
        {
            if (!IsInTraceListener)
            {
                IsInTraceListener = true;
                var dt = string.Format("[{0}],",
                     DateTime.Now.ToString("hh:mm:ss:fff")
                     ) + $"{Thread.CurrentThread.ManagedThreadId,2} ";
                lstLoggedStrings.Add(dt + str);
                if (Debugger.IsAttached)
                {
//                    Debug.WriteLine(dt + str);
                }
                _testcontext.WriteLine(dt + str);
                IsInTraceListener = false;
            }
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            var leftovers = string.Join("\r\n     ", lstLoggedStrings);

            ForceAddToLog("LeftOverLogs\r\n     " + leftovers + "\r\n");
            Trace.Listeners.Remove(this);
        }

        internal void ForceAddToLog(string str)
        {
            var leftovers = string.Join("\r\n     ", lstLoggedStrings) + "\r\n" + str;
            lstLoggedStrings.Clear();
            OutputToLogFileWithRetry(() =>
            {
                File.AppendAllText(LogfileName, str + "\r\n");
            });
        }
        public void OutputToLogFileWithRetry(Action actWrite)
        {
            var nRetry = 0;
            var success = false;
            while (nRetry++ < 10)
            {
                try
                {
                    actWrite();
                    success = true;
                    break;
                }
                catch (IOException)
                {
                }

                Task.Delay(TimeSpan.FromSeconds(0.3)).Wait();
            }
            if (!success)
            {
                Trace.WriteLine($"Error writing to log #retries ={nRetry}");
            }
        }
    }

}