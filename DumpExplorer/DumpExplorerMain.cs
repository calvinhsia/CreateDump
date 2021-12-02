using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Xml;
using static DumpUtilities.DumpUtilities.NativeMethods;

namespace DumpExplorer
{
    public class DumpExplorerMain: Window, INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        void RaisePropChanged([CallerMemberName] string propName = "")
        {
            if (PropertyChanged != null)
            {
                PropertyChanged(this, new PropertyChangedEventArgs(propName));
            }
        }
        public ObservableCollection<string> lstMinidumpStreamTypes { get; set; } = new ObservableCollection<string>();

        [STAThread]
        public static void Main(string[] args)
        {
            var dumpfilename = @"c:\TestPssSnapshotJustTriageDumpWithSnapshot.dmp";
                        
            var omain = new DumpExplorerMain();
            omain.ShowMiniDumpReaderData(dumpfilename);
            omain.ShowDialog();
        }
        public DumpExplorerMain()
        {
            this.DataContext = this;
        }
        public void ShowMiniDumpReaderData(string dumpfilename)
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
        <RowDefinition Height=""125"" />
        <RowDefinition/>
    </Grid.RowDefinitions>
    <Grid.ColumnDefinitions>
        <ColumnDefinition Width = ""120""/>
        <ColumnDefinition Width = ""*""/>
    </Grid.ColumnDefinitions>
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
            lvStreamTypes.SelectionChanged += (o,e)=>
            {
                dpStream.Children.Clear();
                dpStream.Children.Add(new Label() { Content = lvStreamTypes.SelectedItem });

            };
            this.Content = grid;
            using (var dumpReader = new DumpUtilities.DumpUtilities(dumpfilename))
            {
                Trace.WriteLine($"{dumpReader._minidumpFileSize:n0} ({dumpReader._minidumpFileSize:x8})");
                var arch = dumpReader.GetMinidumpStream<MINIDUMP_SYSTEM_INFO>(MINIDUMP_STREAM_TYPE.SystemInfoStream);
                Trace.WriteLine(arch);
                var misc = dumpReader.GetMinidumpStream<_MINIDUMP_MISC_INFO>(MINIDUMP_STREAM_TYPE.MiscInfoStream);
                Trace.WriteLine(misc);
                var lstStreamDirs = new List<MINIDUMP_DIRECTORY>();
                foreach (var strmtype in Enum.GetValues(typeof(MINIDUMP_STREAM_TYPE)))
                {
                    var dir = dumpReader.ReadMinidumpDirectoryForStreamType((MINIDUMP_STREAM_TYPE)strmtype);
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

                int i = 0;
                foreach (var threaddata in dumpReader.EnumerateThreads())
                {
                    Trace.WriteLine($" {i++,3} TID {threaddata.ThreadId:x8} SusCnt: {threaddata.SuspendCount} TEB: {threaddata.Teb:x16}  StackStart{threaddata.Stack.StartOfMemoryRange:x16} StackSize ={threaddata.Stack.MemoryLocDesc.DataSize}");
                }
                i = 0;
                foreach (var moddata in dumpReader.EnumerateModules())
                {
                    Trace.WriteLine($"  {i++,3} Modules ImgSz={moddata.moduleInfo.SizeOfImage,10:n0} Addr= {moddata.moduleInfo.BaseOfImage:x8}   {moddata.ModuleName}");
                }
            }
        }

        private void LvStreamTypes_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            throw new NotImplementedException();
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
                        elem =MyStatics.GetAncestor<MyTreeViewItem>((DependencyObject)elem);
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
