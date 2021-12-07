using DumpUtilities;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Xml;
using static DumpUtilities.DumpReader.NativeMethods;

namespace DumpExplorer
{
    public class DumpExplorerMain : Window
    {
        [STAThread]
        public static void Main(string[] args)
        {
            var omain = new DumpExplorerMain();
            omain.ShowDialog();
        }
        public DumpExplorerMain()
        {
            var dumpfilename = @"c:\TestPssSnapshotJustTriageDumpWithSnapshot.dmp";
            //dumpfilename = @"C:\TodoRajesh\Todo.exe_210806_114200.dmp";
            dumpfilename = @"C:\Users\calvinh\Downloads\Project_OpenCloseSolution_Managed_Wait_29_0.dmp";
            //dumpfilename = @"C:\VSDbgTestDumps\MSSln22611\MSSln22611.dmp";
            var ctrl = new MiniDumpControl(dumpfilename);
            Content = ctrl;
            this.Closing += (o, e) =>
              {
                  ctrl.Dispose();
              };
        }
    }
    public class MiniDumpControl : UserControl, INotifyPropertyChanged, IDisposable
    {
        public event PropertyChangedEventHandler PropertyChanged;
        void RaisePropChanged([CallerMemberName] string propName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));
        }
        public ObservableCollection<string> lstMinidumpStreamTypes { get; set; } = new ObservableCollection<string>();
        DumpReader _dumpReader;
        private readonly string dumpFileName;

        public MiniDumpControl(string dumpfilename)
        {
            this.dumpFileName = dumpfilename;
            this.DataContext = this;
            _dumpReader = new DumpReader(dumpFileName);
            this.ShowMiniDumpReaderData();

        }
        public void ShowMiniDumpReaderData()
        {
            // Make a namespace referring to our namespace and assembly
            // using the prefix "l:"
            //xmlns:l=""clr-namespace:Fish;assembly=Fish"""
            var nameSpace = this.GetType().Namespace;
            var asm = System.IO.Path.GetFileNameWithoutExtension(
                Assembly.GetExecutingAssembly().Location);

            var xmlns = string.Format(
@"xmlns:l=""clr-namespace:{0};assembly={1}""", nameSpace, asm);
            //there are a lot of quotes (and braces) in XAML
            //and the C# string requires quotes to be doubled
            var strxaml =
@"<Grid
xmlns=""http://schemas.microsoft.com/winfx/2006/xaml/presentation""
xmlns:x=""http://schemas.microsoft.com/winfx/2006/xaml""
" + xmlns + // add our xaml namespace
@" Margin=""5,5,5,5"">
    <Grid.RowDefinitions>
        <RowDefinition Height=""30"" />
        <RowDefinition/>
    </Grid.RowDefinitions>
    <Grid.ColumnDefinitions>
        <ColumnDefinition Width = ""220""/>
        <ColumnDefinition Width = ""*""/>
    </Grid.ColumnDefinitions>
    <Label Content=""Streams found in dump"" ToolTip = ""Click on a stream to show details""/>
    <ListView x:Name = ""lvStreamTypes"" Grid.Row = ""1"" Grid.Column = ""0"" ItemsSource = ""{Binding lstMinidumpStreamTypes}"" FontFamily=""Consolas""/>
    <DockPanel x:Name = ""dpStream"" Grid.Row = ""1"" Grid.Column = ""1""/>
</Grid>
";
            var strReader = new System.IO.StringReader(strxaml);
            var xamlreader = XmlReader.Create(strReader);
            var grid = (Grid)(XamlReader.Load(xamlreader));
            var lvStreamTypes = (ListView)grid.FindName("lvStreamTypes");
            var dpStream = (DockPanel)grid.FindName("dpStream");
            dpStream.Children.Add(new Label() { Content = "init" });
            lvStreamTypes.SelectionChanged += (o, e) =>
            {
                if (lvStreamTypes.SelectedItem != null)
                {
                    try
                    {
                        if (Enum.TryParse<MINIDUMP_STREAM_TYPE>(lvStreamTypes.SelectedItem.ToString().Split()[0], out var stype))
                        {
                            var newui = GetContentForStreamType(stype);
                            dpStream.Children.Clear();
                            dpStream.Children.Add(newui);
                        }
                    }
                    catch (Exception)
                    {
                    }
                }
            };
            this.Content = grid;
            Trace.WriteLine($"{_dumpReader._minidumpFileSize:n0} ({_dumpReader._minidumpFileSize:x8})");
            var arch = _dumpReader.GetMinidumpStream<MINIDUMP_SYSTEM_INFO>(MINIDUMP_STREAM_TYPE.SystemInfoStream);
            Trace.WriteLine(arch);
            var misc = _dumpReader.GetMinidumpStream<_MINIDUMP_MISC_INFO>(MINIDUMP_STREAM_TYPE.MiscInfoStream);
            Trace.WriteLine(misc);
            var lstStreamDirs = new List<MINIDUMP_DIRECTORY>();
            foreach (var strmtype in Enum.GetValues(typeof(MINIDUMP_STREAM_TYPE)))
            {
                var dir = _dumpReader.ReadMinidumpDirectoryForStreamType((MINIDUMP_STREAM_TYPE)strmtype);
                if (dir.Location.Rva != 0)
                {
                    lstStreamDirs.Add(dir);
                }
            }
            foreach (var dir in lstStreamDirs.OrderBy(d => d.Location.Rva))
            {
                var itmstr = $"{dir.StreamType,-30} Offset={dir.Location.Rva:x8}  Sz={dir.Location.DataSize:x8} ({dir.Location.DataSize:n0})";
                Trace.WriteLine($"   {itmstr}");
                lstMinidumpStreamTypes.Add(itmstr);
            }

            //int i = 0;
            //foreach (var threaddata in dumpReader.EnumerateMinidumpStreamData<MINIDUMP_THREAD_LIST, MINIDUMP_THREAD>(MINIDUMP_STREAM_TYPE.ThreadListStream))
            //{
            //    Trace.WriteLine($" {i++,3} TID {threaddata.ThreadId:x8} SusCnt: {threaddata.SuspendCount} TEB: {threaddata.Teb:x16}  StackStart{threaddata.Stack.StartOfMemoryRange:x16} StackSize ={threaddata.Stack.MemoryLocDesc.DataSize}");
            //}
            //i = 0;
            //foreach (var moddata in dumpReader.EnumerateMinidumpStreamData<MINIDUMP_MODULE_LIST, MINIDUMP_MODULE>(MINIDUMP_STREAM_TYPE.ModuleListStream))
            //{
            //    var modname = dumpReader.GetNameFromRva(moddata.ModuleNameRva);
            //    Trace.WriteLine($"  {i++,3} Modules ImgSz={moddata.SizeOfImage,10:n0} Addr= {moddata.BaseOfImage:x8}   {modname}");
            //}
        }

        private UIElement GetContentForStreamType(MINIDUMP_STREAM_TYPE streamType)
        {
            UIElement res = null;
            switch (streamType)
            {
                case MINIDUMP_STREAM_TYPE.SystemInfoStream:
                    var arch = _dumpReader.GetMinidumpStream<MINIDUMP_SYSTEM_INFO>(MINIDUMP_STREAM_TYPE.SystemInfoStream);
                    res = new TextBlock() { Text = arch.ToString() };
                    break;
                case MINIDUMP_STREAM_TYPE.MiscInfoStream:
                    var misc = _dumpReader.GetMinidumpStream<_MINIDUMP_MISC_INFO>(MINIDUMP_STREAM_TYPE.MiscInfoStream);
                    res = new TextBlock() { Text = misc.ToString() };
                    break;
                case MINIDUMP_STREAM_TYPE.ModuleListStream: // MINIDUMP_MODULE
                    {
                        var lv = new ListView();
                        _dumpReader.EnumerateStreamData<MINIDUMP_MODULE_LIST, MINIDUMP_MODULE>(MINIDUMP_STREAM_TYPE.ModuleListStream, item =>
                        {
                            var modName = _dumpReader.GetNameFromRva(item.ModuleNameRva);
                            lv.Items.Add(new TextBlock() { Text = $"ImgSz={item.SizeOfImage,10:n0} Addr= {item.BaseOfImage:x8}  {item.VersionInfo.GetVersion()}  {modName}" });
                        });
                        res = lv;
                    }
                    break;
                case MINIDUMP_STREAM_TYPE.UnloadedModuleListStream:
                    {
                        var lv = new ListView();
                        _dumpReader.EnumerateStreamData<MINIDUMP_UNLOADED_MODULE_LIST, MINIDUMP_UNLOADED_MODULE>(MINIDUMP_STREAM_TYPE.UnloadedModuleListStream, item =>
                        {
                            var modName = _dumpReader.GetNameFromRva(item.ModuleNameRva);
                            lv.Items.Add(new TextBlock() { Text = $"ImgSz={item.SizeOfImage,10:n0} Addr= {item.BaseOfImage:x8}   {modName}" });
                        });
                        res = lv;
                    }
                    break;
                case MINIDUMP_STREAM_TYPE.ThreadListStream: // MINIDUMP_THREAD
                    {
                        var lv = new ListView();
                        _dumpReader.EnumerateStreamData<MINIDUMP_THREAD_LIST, MINIDUMP_THREAD>(MINIDUMP_STREAM_TYPE.ThreadListStream, item =>
                        {
                            lv.Items.Add(new TextBlock() { Text = $"{item}" });
                        });
                        res = lv;
                    }
                    break;
                case MINIDUMP_STREAM_TYPE.ThreadInfoListStream: //MINIDUMP_THREAD_INFO
                    {
                        var lv = new ListView();
                        _dumpReader.EnumerateStreamData<MINIDUMP_THREAD_INFO_LIST, MINIDUMP_THREAD_INFO>(MINIDUMP_STREAM_TYPE.ThreadInfoListStream, item =>
                        {
                            lv.Items.Add(new TextBlock() { Text = $"{item}" });
                        });
                        res = lv;
                    }
                    break;
                case MINIDUMP_STREAM_TYPE.CommentStreamA:
                    {
                        var dir = _dumpReader.ReadMinidumpDirectoryForStreamType(streamType);
                        if (dir.Location.Rva != 0)
                        {
                            var ptrData = _dumpReader.MapRvaLocation(dir.Location);
                            var str = Marshal.PtrToStringAnsi(ptrData);
                            res = new TextBox() { Text = str, AcceptsReturn = true };
                        }
                    }
                    break;
                case MINIDUMP_STREAM_TYPE.CommentStreamW:
                    {
                        var dir = _dumpReader.ReadMinidumpDirectoryForStreamType(streamType);
                        if (dir.Location.Rva != 0)
                        {
                            var ptrData = _dumpReader.MapRvaLocation(dir.Location);
                            var str = Marshal.PtrToStringUni(ptrData);
                            res = new TextBox() { Text = str, AcceptsReturn = true };
                        }
                    }
                    break;
                case MINIDUMP_STREAM_TYPE.MemoryInfoListStream: // virtualalloc info
                    {
                        var lv = new ListView();
                        _dumpReader.EnumerateStreamData<MINIDUMP_MEMORY_INFO_LIST, MINIDUMP_MEMORY_INFO>(MINIDUMP_STREAM_TYPE.MemoryInfoListStream, item =>
                        {
                            lv.Items.Add(new TextBlock() { Text = $"{item}" });
                        });
                        res = lv;

                        var lst = new List<MINIDUMP_MEMORY_INFO>();
                        var brAggState = from va in lst
                                         group va by va.State
                                         into grp
                                         select new
                                         {
                                             State = grp.Key,
                                             Sum = grp.Sum(v => v.RegionSize),
                                             Count = grp.Count()
                                         };
                        var brAggType = from va in lst
                                        group va by va.Type
                                        into grp
                                        select new
                                        {
                                            State = grp.Key,
                                            Sum = grp.Sum(v => v.RegionSize),
                                            Count = grp.Count()
                                        };


                    }
                    break;
                case MINIDUMP_STREAM_TYPE.MemoryListStream:
                    {
                        var lv = new ListView();
                        _dumpReader.EnumerateStreamData<MINIDUMP_MEMORY_LIST, MINIDUMP_MEMORY_DESCRIPTOR>(MINIDUMP_STREAM_TYPE.MemoryListStream, item =>
                        {
                            lv.Items.Add(new TextBlock() { Text = $"{item}" });
                        });
                        res = lv;
                    }
                    break;
                case MINIDUMP_STREAM_TYPE.Memory64ListStream:
                    {
                        var lv = new ListView();
                        _dumpReader.EnumerateStreamData<MINIDUMP_MEMORY64_LIST, MINIDUMP_MEMORY_DESCRIPTOR64>(MINIDUMP_STREAM_TYPE.Memory64ListStream, item =>
                         {
                             lv.Items.Add(new TextBlock() { Text = $"{item}" });
                         });
                        res = lv;
                    }
                    break;
                case MINIDUMP_STREAM_TYPE.HandleDataStream:
                    {
                        var lv = new ListView();
                        _dumpReader.EnumerateStreamData<MINIDUMP_HANDLE_DATA_STREAM, MINIDUMP_HANDLE_DESCRIPTOR>(MINIDUMP_STREAM_TYPE.HandleDataStream, item =>
                        {
                            var TypeName = string.Empty;
                            if (item.TypeNameRva != 0)
                            {
                                TypeName = _dumpReader.GetNameFromRva(item.TypeNameRva);
                            }
                            var ObjectName = string.Empty;
                            if (item.ObjectNameRva != 0)
                            {
                                ObjectName = _dumpReader.GetNameFromRva(item.ObjectNameRva);
                            }
                            lv.Items.Add(new TextBlock() { Text = $"{item} TypeName={TypeName} ObjectName={ObjectName}" });
                        });
                        res = lv;
                    }
                    break;
                case MINIDUMP_STREAM_TYPE.FunctionTableStream:
                    {
                        var lv = new ListView();
                        _dumpReader.EnumerateStreamData<_MINIDUMP_FUNCTION_TABLE_STREAM, _MINIDUMP_FUNCTION_TABLE_DESCRIPTOR>(MINIDUMP_STREAM_TYPE.FunctionTableStream, item =>
                         {
                             lv.Items.Add(new TextBlock() { Text = $"{item}" });
                         });
                        res = lv;
                    }
                    break;
            }
            if (res == null)
            {
                res = new TextBlock() { Text = $" {streamType} Not implemented" };
            }
            return res;
        }

        public void Dispose()
        {
            _dumpReader?.Dispose();
        }
    }
    public class MyTreeViewBase : TreeView
    {
        public MyTreeViewBase() : base()
        {
            ContextMenu = new ContextMenu();
            this.ContextMenu.AddMenuItem((o, e) =>
            {
                OnExpandAllStart((MyTreeViewItem)SelectedItem);
                ((MyTreeViewItem)SelectedItem).ExpandAll();
                OnExpandAllEnd();
            }, "_Expand SubTree", "Expand tree branch from here: warning: try small branches first!");
            this.ContextMenu.AddMenuItem((o, e) =>
            {
                var sb = new StringBuilder();
                sb.AppendLine($"Children of '{SelectedItem.ToString()}'");
                ((MyTreeViewItem)SelectedItem).DumpChildren(sb, 0);
                MyStatics.WriteOutputToTempFile(sb.ToString());
            }, "_DumpChildren To Notepad", "Dump expanded children to notepad");
            //var style = new Style();
            //style.TargetType = typeof(TreeViewItem);
            //style.Setters.Add(new Setter(TreeViewItem.FontFamilyProperty, new FontFamily("Segoe UI")));
            //style.Setters.Add(new Setter(TreeViewItem.FontSizeProperty, 9.0));
            //style.Setters.Add(new Setter(TreeViewItem.ForegroundProperty, Brushes.Blue));
            //style.Resources.Add(SystemColors.HighlightTextBrushKey, Brushes.Green);
            //style.Resources.Add(SystemColors.ControlBrushKey, Brushes.Purple);
            //var trigger = new Trigger();
            //trigger.Property = TreeViewItem.IsSelectedProperty;
            //trigger.Value = true;
            //trigger.Setters.Add(new Setter()
            //{
            //    Property= TreeViewItem.ForegroundProperty,
            //    Value = Brushes.Pink
            //});
            ////var ctemplate = new ControlTemplate(typeof(TreeViewItem));
            ////var ss = new Setter();
            ////ss.Value = ctemplate;
            ////style.Setters.Add(ss);
            //style.Triggers.Add(trigger);

            //this.ItemContainerStyle = style;
            this.PreviewMouseRightButtonDown += (o, e) =>
            {
                try
                {
                    // we want to select the item you rt click on so context menu knows which item is selected
                    if (o is MyTreeViewBase tvb && tvb != null)
                    {
                        var pt = e.GetPosition(tvb);
                        var elem = tvb.InputHitTest(pt);
                        elem = MyStatics.GetAncestor<MyTreeViewItem>((DependencyObject)elem);
                        elem.Focus();
                    }
                }
                catch (Exception)
                {
                }
            };
        }

        private void OnExpandAllStart(MyTreeViewItem selectedItem)
        {
        }
        private void OnExpandAllEnd()
        {
        }
    }

    public class MyTreeViewItem : TreeViewItem
    {
        private Boolean fDidAddMenuItems;

        public MyTreeViewItem()
        {
            FontFamily = new FontFamily("Segoe Ui");
            Foreground = Brushes.Blue;
            //            FontSize = 10.0;
        }
        internal void AddDummyItem()
        {
            var dummy = new MyTreeViewItem() { Tag = 1 };
            this.Items.Add(dummy);
        }
        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonDown(e);
            this.AddContextMenuItems();
        }
        protected override void OnMouseRightButtonDown(MouseButtonEventArgs e)
        {
            base.OnMouseRightButtonDown(e);
            this.AddContextMenuItems();
        }

        internal void DumpChildren(StringBuilder sb, Int32 nDepth)
        {
            var sIndent = new string(' ', nDepth);
            sb.AppendLine($"{sIndent}{this.ToString()}");
            if (this.IsExpanded)
            {
                foreach (MyTreeViewItem item in this.Items)
                {
                    item.DumpChildren(sb, nDepth + 1); // recur
                }
            }
        }

        internal void ExpandAll()
        {
            this.IsExpanded = true;
            foreach (MyTreeViewItem itm in this.Items)
            {
                itm.ExpandAll();
            }

        }

        private void AddContextMenuItems()
        {
            if (!fDidAddMenuItems)
            {
                fDidAddMenuItems = true;

            }
        }
    }
    // a textbox that selects all when focused:
    public class MyTextBox : TextBox
    {
        public MyTextBox()
        {
            this.GotFocus += (o, e) =>
            {
                this.SelectAll();
            };
        }
    }
    public static class MyStatics
    {
        public static string WriteOutputToTempFile(string strToOutput, string fExt = "txt", bool fStartIt = true)
        {
            var tmpFileName = System.IO.Path.GetTempFileName(); //"C:\Users\calvinh\AppData\Local\Temp\tmp8509.tmp"
            File.WriteAllText(tmpFileName, strToOutput, new UnicodeEncoding(bigEndian: false, byteOrderMark: true));
            var filename = System.IO.Path.ChangeExtension(tmpFileName, fExt);

            File.Move(tmpFileName, filename); // rename
            if (fStartIt)
            {
                Process.Start(filename);
            }
            return filename;
        }

        public static T GetAncestor<T>(DependencyObject element) where T : DependencyObject
        {
            while (element != null && !(element is T))
            {
                element = VisualTreeHelper.GetParent(element);
            }
            return (T)element;
        }

        public static MenuItem AddMenuItem(this ContextMenu menu, RoutedEventHandler handler, string menuItemContent, string tooltip, int InsertPos = -1)
        {
            var newItem = new MenuItem()
            {
                Header = menuItemContent,
                ToolTip = tooltip
            };
            newItem.Click += handler;
            if (InsertPos == -1)
            {
                menu.Items.Add(newItem);
            }
            else
            {
                menu.Items.Insert(InsertPos, newItem);
            }
            return newItem;
        }
    }
}
