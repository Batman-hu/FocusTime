using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Microsoft.Win32;

namespace FocusTime
{
    public static class Program
    {
        [STAThread]
        public static int Main(string[] args)
        {
            bool smoke = args.Length > 0 && args[0] == "--smoke";
            bool created;
            using (var mutex = new Mutex(true, smoke ? "Local\\FocusTime.Smoke.v1" : "Local\\FocusTime.Desktop.v1", out created))
            {
                if (!created) { MessageBox.Show("专注时光已经在运行，请从任务栏打开。", "专注时光"); return 0; }
                try
                {
                    string folder = smoke ? Path.Combine(DataStore.TemporaryDirectory, "FocusTime-UI-" + Guid.NewGuid().ToString("N")) : DataStore.DefaultDirectory;
                    var store = new DataStore(folder);
                    var data = store.Load();
                    var app = new Application();
                    app.ShutdownMode = ShutdownMode.OnMainWindowClose;
                    StyleKit.Install(app);
                    var window = new MainWindow(store, data);
                    if (smoke)
                    {
                        string reportFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "verification");
                        window.Loaded += delegate
                        {
                            window.Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(delegate
                            {
                                try { window.RunSmoke(reportFolder); }
                                catch (Exception error)
                                { Directory.CreateDirectory(reportFolder); File.WriteAllText(Path.Combine(reportFolder, "ui-test-failed.txt"), error.ToString()); app.Shutdown(1); return; }
                                window.Close();
                            }));
                        };
                    }
                    int exitCode = app.Run(window);
                    if (smoke && Directory.Exists(folder)) Directory.Delete(folder, true);
                    return exitCode;
                }
                catch (Exception ex)
                {
                    MessageBox.Show("程序无法启动。\n\n" + ex.Message + "\n\n已有数据不会被清空。", "专注时光", MessageBoxButton.OK, MessageBoxImage.Error);
                    return 1;
                }
                finally { if (created) mutex.ReleaseMutex(); }
            }
        }
    }

    internal static class StyleKit
    {
        public static readonly Brush Ink = Brush("#F0F6F7");
        public static readonly Brush Muted = Brush("#A0BBC8");
        public static readonly Brush Accent = Brush("#F5A025");
        public static readonly Brush Line = Brush("#38566A78");
        public static readonly Brush Surface = Brush("#DC071C2A");
        public static readonly Brush Field = Brush("#C50D2838");
        public static readonly Brush DeepInk = Brush("#091A25");
        private static Brush ocean;
        public static Brush Ocean
        {
            get
            {
                if (ocean != null) return ocean;
                using (var stream = System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream("FocusTime.Ocean.jpg"))
                {
                    if (stream == null) throw new InvalidDataException("缺少海浪背景资源，请重新编译程序。");
                    var image = new BitmapImage(); image.BeginInit(); image.CacheOption = BitmapCacheOption.OnLoad;
                    image.StreamSource = stream; image.EndInit(); image.Freeze();
                    // Use the wave-only region; the orange typography is rendered as sharp UI text.
                    var waves = new CroppedBitmap(image, new Int32Rect(0, 0, image.PixelWidth, (int)(image.PixelHeight * .38)));
                    waves.Freeze();
                    var brush = new ImageBrush(waves) { Stretch = Stretch.UniformToFill, AlignmentY = AlignmentY.Top };
                    brush.Freeze(); ocean = brush;
                }
                return ocean;
            }
        }
        public static Brush Brush(string color)
        {
            try { var b = (SolidColorBrush)new BrushConverter().ConvertFromString(color); b.Freeze(); return b; }
            catch { return Brushes.SteelBlue; }
        }
        [System.Runtime.InteropServices.DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr window, int attribute, ref int value, int size);
        public static void DarkTitleBar(Window window)
        {
            window.SourceInitialized += delegate
            {
                try
                {
                    var handle = new System.Windows.Interop.WindowInteropHelper(window).Handle;
                    int enabled = 1, caption = 0x002A1C07, text = 0x00F7F6F0;
                    DwmSetWindowAttribute(handle, 20, ref enabled, 4);
                    DwmSetWindowAttribute(handle, 35, ref caption, 4);
                    DwmSetWindowAttribute(handle, 36, ref text, 4);
                }
                catch (DllNotFoundException) { }
                catch (EntryPointNotFoundException) { }
            };
        }
        public static void Install(Application app)
        {
            const string xaml = @"<ResourceDictionary xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml'>
<Style TargetType='Button'>
 <Setter Property='FontFamily' Value='Microsoft YaHei UI'/><Setter Property='FontSize' Value='13'/>
 <Setter Property='Foreground' Value='#D1E2E9'/><Setter Property='Background' Value='#CB153347'/>
 <Setter Property='Padding' Value='18,10'/><Setter Property='Cursor' Value='Hand'/><Setter Property='MinHeight' Value='38'/>
 <Setter Property='BorderThickness' Value='0'/><Setter Property='HorizontalContentAlignment' Value='Center'/>
 <Setter Property='Template'><Setter.Value><ControlTemplate TargetType='Button'>
  <Border x:Name='bd' CornerRadius='9' Background='{TemplateBinding Background}' BorderBrush='{TemplateBinding BorderBrush}' BorderThickness='{TemplateBinding BorderThickness}' Padding='{TemplateBinding Padding}'>
   <ContentPresenter HorizontalAlignment='{TemplateBinding HorizontalContentAlignment}' VerticalAlignment='Center'/>
  </Border><ControlTemplate.Triggers>
   <Trigger Property='IsMouseOver' Value='True'><Setter TargetName='bd' Property='Opacity' Value='0.84'/></Trigger>
   <Trigger Property='IsPressed' Value='True'><Setter TargetName='bd' Property='Opacity' Value='0.62'/></Trigger>
   <Trigger Property='IsEnabled' Value='False'><Setter TargetName='bd' Property='Opacity' Value='0.38'/></Trigger>
   <Trigger Property='IsKeyboardFocused' Value='True'><Setter TargetName='bd' Property='BorderThickness' Value='2'/><Setter TargetName='bd' Property='BorderBrush' Value='#F5A025'/></Trigger>
  </ControlTemplate.Triggers></ControlTemplate></Setter.Value></Setter>
</Style>
<Style TargetType='TextBox'>
 <Setter Property='FontFamily' Value='Microsoft YaHei UI'/><Setter Property='FontSize' Value='14'/><Setter Property='Foreground' Value='#F0F6F7'/><Setter Property='CaretBrush' Value='#F5A025'/><Setter Property='SelectionBrush' Value='#95671C'/>
 <Setter Property='Background' Value='#C50D2838'/><Setter Property='BorderBrush' Value='#43536573'/><Setter Property='BorderThickness' Value='1'/><Setter Property='Padding' Value='10,8'/><Setter Property='VerticalContentAlignment' Value='Center'/>
 <Setter Property='Template'><Setter.Value><ControlTemplate TargetType='TextBox'><Border x:Name='frame' CornerRadius='8' Background='{TemplateBinding Background}' BorderBrush='{TemplateBinding BorderBrush}' BorderThickness='{TemplateBinding BorderThickness}' Padding='{TemplateBinding Padding}'><ScrollViewer x:Name='PART_ContentHost'/></Border><ControlTemplate.Triggers><Trigger Property='IsKeyboardFocusWithin' Value='True'><Setter TargetName='frame' Property='BorderBrush' Value='#F5A025'/></Trigger><Trigger Property='IsEnabled' Value='False'><Setter Property='Opacity' Value='0.45'/></Trigger></ControlTemplate.Triggers></ControlTemplate></Setter.Value></Setter>
</Style>
<Style TargetType='ComboBoxItem'>
 <Setter Property='Foreground' Value='#E6F0F5'/><Setter Property='Padding' Value='11,9'/><Setter Property='HorizontalContentAlignment' Value='Stretch'/>
 <Setter Property='Template'><Setter.Value><ControlTemplate TargetType='ComboBoxItem'><Border x:Name='item' Padding='{TemplateBinding Padding}' Background='Transparent' CornerRadius='5'><ContentPresenter/></Border><ControlTemplate.Triggers><Trigger Property='IsHighlighted' Value='True'><Setter TargetName='item' Property='Background' Value='#25475A'/></Trigger><Trigger Property='IsSelected' Value='True'><Setter Property='Foreground' Value='#F5A025'/><Setter TargetName='item' Property='Background' Value='#1D3949'/></Trigger></ControlTemplate.Triggers></ControlTemplate></Setter.Value></Setter>
</Style>
<Style TargetType='ComboBox'>
 <Setter Property='FontFamily' Value='Microsoft YaHei UI'/><Setter Property='FontSize' Value='13'/><Setter Property='Foreground' Value='#F0F6F7'/><Setter Property='Background' Value='#C50D2838'/><Setter Property='BorderBrush' Value='#43536573'/><Setter Property='MinHeight' Value='36'/><Setter Property='Padding' Value='11,6'/><Setter Property='VerticalContentAlignment' Value='Center'/><Setter Property='ScrollViewer.CanContentScroll' Value='True'/>
 <Setter Property='Template'><Setter.Value><ControlTemplate TargetType='ComboBox'>
  <Grid>
   <ToggleButton x:Name='toggle' Focusable='False' Background='{TemplateBinding Background}' BorderBrush='{TemplateBinding BorderBrush}' IsChecked='{Binding IsDropDownOpen, Mode=TwoWay, RelativeSource={RelativeSource TemplatedParent}}' ClickMode='Press'>
    <ToggleButton.Template><ControlTemplate TargetType='ToggleButton'><Border x:Name='frame' Background='{TemplateBinding Background}' BorderBrush='{TemplateBinding BorderBrush}' BorderThickness='1' CornerRadius='8'><Path Data='M 0 0 L 4 4 L 8 0' Stroke='#A0BBC8' StrokeThickness='1.4' Width='8' Height='4' HorizontalAlignment='Right' VerticalAlignment='Center' Margin='0,0,13,0'/></Border><ControlTemplate.Triggers><Trigger Property='IsMouseOver' Value='True'><Setter TargetName='frame' Property='BorderBrush' Value='#8799A3'/></Trigger><Trigger Property='IsChecked' Value='True'><Setter TargetName='frame' Property='BorderBrush' Value='#F5A025'/></Trigger></ControlTemplate.Triggers></ControlTemplate></ToggleButton.Template>
   </ToggleButton>
   <ContentPresenter Margin='11,0,32,0' VerticalAlignment='Center' IsHitTestVisible='False' Content='{TemplateBinding SelectionBoxItem}' ContentTemplate='{TemplateBinding SelectionBoxItemTemplate}' ContentTemplateSelector='{TemplateBinding ItemTemplateSelector}'/>
   <Popup x:Name='PART_Popup' Placement='Bottom' AllowsTransparency='True' Focusable='False' IsOpen='{TemplateBinding IsDropDownOpen}' PopupAnimation='Fade'>
    <Border Background='#102A3A' BorderBrush='#466171' BorderThickness='1' CornerRadius='8' Padding='5' MinWidth='{TemplateBinding ActualWidth}' MaxHeight='280' Margin='0,4,0,0'><ScrollViewer CanContentScroll='True'><StackPanel IsItemsHost='True' KeyboardNavigation.DirectionalNavigation='Contained'/></ScrollViewer></Border>
   </Popup>
  </Grid><ControlTemplate.Triggers><Trigger Property='IsEnabled' Value='False'><Setter Property='Opacity' Value='0.45'/></Trigger><Trigger Property='IsKeyboardFocusWithin' Value='True'><Setter TargetName='toggle' Property='BorderBrush' Value='#F5A025'/></Trigger></ControlTemplate.Triggers>
 </ControlTemplate></Setter.Value></Setter>
</Style>
<Style TargetType='CheckBox'>
 <Setter Property='Foreground' Value='#E6F0F5'/><Setter Property='Cursor' Value='Hand'/><Setter Property='Template'><Setter.Value><ControlTemplate TargetType='CheckBox'><StackPanel Orientation='Horizontal'><Border x:Name='box' Width='17' Height='17' Margin='0,0,9,0' CornerRadius='4' Background='#18384A' BorderBrush='#688592' BorderThickness='1'><Path x:Name='check' Data='M 0 4 L 3 7 L 9 0' Stroke='#091A25' StrokeThickness='2' Width='9' Height='7' Visibility='Collapsed'/></Border><ContentPresenter VerticalAlignment='Center'/></StackPanel><ControlTemplate.Triggers><Trigger Property='IsChecked' Value='True'><Setter TargetName='check' Property='Visibility' Value='Visible'/><Setter TargetName='box' Property='Background' Value='#F5A025'/><Setter TargetName='box' Property='BorderBrush' Value='#F5A025'/></Trigger><Trigger Property='IsKeyboardFocused' Value='True'><Setter TargetName='box' Property='BorderBrush' Value='White'/></Trigger></ControlTemplate.Triggers></ControlTemplate></Setter.Value></Setter>
</Style>
<Style TargetType='ScrollBar'><Setter Property='Width' Value='8'/><Setter Property='MinWidth' Value='8'/><Setter Property='Background' Value='Transparent'/><Setter Property='Template'><Setter.Value><ControlTemplate TargetType='ScrollBar'><Grid Background='Transparent'><Track x:Name='PART_Track' IsDirectionReversed='True'><Track.DecreaseRepeatButton><RepeatButton Command='ScrollBar.PageUpCommand' Opacity='0' Focusable='False'/></Track.DecreaseRepeatButton><Track.Thumb><Thumb><Thumb.Template><ControlTemplate TargetType='Thumb'><Border Background='#5A638897' CornerRadius='3' Margin='2,0'/></ControlTemplate></Thumb.Template></Thumb></Track.Thumb><Track.IncreaseRepeatButton><RepeatButton Command='ScrollBar.PageDownCommand' Opacity='0' Focusable='False'/></Track.IncreaseRepeatButton></Track></Grid></ControlTemplate></Setter.Value></Setter></Style>
<Style TargetType='ToolTip'><Setter Property='Background' Value='#102A3A'/><Setter Property='Foreground' Value='#F0F6F7'/><Setter Property='BorderBrush' Value='#466171'/></Style>
<Style TargetType='ScrollViewer'><Setter Property='VerticalScrollBarVisibility' Value='Auto'/><Setter Property='HorizontalScrollBarVisibility' Value='Disabled'/></Style>
</ResourceDictionary>";
            app.Resources.MergedDictionaries.Add((ResourceDictionary)XamlReader.Parse(xaml));
        }
        public static TextBlock Text(string text, double size, Brush color)
        { return new TextBlock { Text = text, FontSize = size, Foreground = color, TextWrapping = TextWrapping.Wrap }; }
        public static Button Button(string label, Action action, bool primary)
        {
            var b = new Button { Content = label };
            if (primary) { b.Background = Accent; b.Foreground = DeepInk; b.FontWeight = FontWeights.SemiBold; }
            b.Click += delegate { action(); }; return b;
        }
        public static Border Card(UIElement content, double padding)
        {
            return new Border { Background = Surface, BorderBrush = Line, BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(16), Padding = new Thickness(padding), Child = content };
        }
        public static void Gap(Panel panel, double height) { panel.Children.Add(new Border { Height = height }); }
        public static void Add(Grid grid, UIElement element, int row, int col)
        { Grid.SetRow(element, row); Grid.SetColumn(element, col); grid.Children.Add(element); }
        public static Grid Columns(params double[] widths)
        {
            var grid = new Grid();
            foreach (var width in widths) grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(width, GridUnitType.Star) });
            return grid;
        }
    }

    internal sealed class Dial : FrameworkElement
    {
        public double Progress;
        public bool Ticks;
        public Brush Accent = StyleKit.Accent;
        protected override void OnRender(DrawingContext dc)
        {
            double size = Math.Min(ActualWidth, ActualHeight), r = size / 2 - 10;
            var center = new Point(ActualWidth / 2, ActualHeight / 2);
            var track = new Pen(StyleKit.Brush("#284657"), Ticks ? 5 : 12);
            dc.DrawEllipse(null, track, center, r, r);
            double progress = Math.Max(0, Math.Min(1, Progress));
            if (progress >= .999999) dc.DrawEllipse(null, new Pen(Accent, track.Thickness), center, r, r);
            else if (progress > 0)
            {
                double angle = progress * Math.PI * 2 - Math.PI / 2;
                var geometry = new StreamGeometry();
                using (var g = geometry.Open())
                {
                    g.BeginFigure(new Point(center.X, center.Y - r), false, false);
                    g.ArcTo(new Point(center.X + Math.Cos(angle) * r, center.Y + Math.Sin(angle) * r),
                        new Size(r, r), 0, progress > .5, SweepDirection.Clockwise, true, false);
                }
                dc.DrawGeometry(null, new Pen(Accent, track.Thickness) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round }, geometry);
            }
            if (Ticks)
            {
                for (int i = 0; i < 60; i++)
                {
                    double a = i * Math.PI / 30 - Math.PI / 2;
                    double outer = r - 12, inner = outer - (i % 5 == 0 ? 9 : 4);
                    var pen = new Pen(i < progress * 60 ? Accent : StyleKit.Brush("#426171"), i % 5 == 0 ? 3 : 1.5);
                    pen.StartLineCap = pen.EndLineCap = PenLineCap.Round;
                    dc.DrawLine(pen, new Point(center.X + Math.Cos(a) * inner, center.Y + Math.Sin(a) * inner),
                        new Point(center.X + Math.Cos(a) * outer, center.Y + Math.Sin(a) * outer));
                }
            }
        }
    }

    internal sealed class MainWindow : Window
    {
        private readonly DataStore store;
        private readonly AppData data;
        private readonly FocusEngine engine;
        private readonly Stopwatch clock = Stopwatch.StartNew();
        private readonly DispatcherTimer timer;
        private readonly Grid content = new Grid();
        private readonly TextBlock banner = StyleKit.Text("", 12, StyleKit.Muted);
        private readonly Dictionary<string, Button> nav = new Dictionary<string, Button>();
        private string page = "focus", mode;
        private ComboBox projects;
        private TextBox minutes;
        private Button startButton, finishButton, downButton, upButton;
        private Dial focusDial, goalDial;
        private TextBlock timerText, timerCaption, goalText, goalCaption, runStatus, todayValue, yesterdayValue, streakValue;
        private TextBlock allValue, weekValue, statsTodayValue;
        private ComboBox statsProject, statsRange;
        private StackPanel breakdown, history;
        private Grid weekChart;
        private string filterProject;
        private int rangeIndex;
        private double lastSave, lastStats;
        private DateTime statsDate = DateTime.Today;
        private MiniWindow mini;
        private bool closing, saveError;

        public MainWindow(DataStore store, AppData data)
        {
            this.store = store; this.data = data; engine = new FocusEngine(data); mode = data.SelectedMode == "up" ? "up" : "down";
            Title = "专注时光 · FocusTime"; Width = 1180; Height = 820; MinWidth = 1000; MinHeight = 760;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            Background = StyleKit.Ocean; FontFamily = new FontFamily("Microsoft YaHei UI"); Foreground = StyleKit.Ink;
            StyleKit.DarkTitleBar(this);
            var root = new Grid(); root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(184) });
            root.ColumnDefinitions.Add(new ColumnDefinition()); Content = root;
            var shade = new Border { Background = new LinearGradientBrush(((SolidColorBrush)StyleKit.Brush("#65030E1C")).Color, ((SolidColorBrush)StyleKit.Brush("#AE031221")).Color, 80) };
            Grid.SetColumnSpan(shade, 2); root.Children.Add(shade);
            var sidebar = new Grid { Margin = new Thickness(20, 32, 18, 22) };
            sidebar.RowDefinitions.Add(new RowDefinition()); sidebar.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var menu = new StackPanel();
            var logo = StyleKit.Text("◷  专注时光", 22, StyleKit.Accent); logo.FontWeight = FontWeights.SemiBold; menu.Children.Add(logo);
            var tag = StyleKit.Text("O C E A N   /   F O C U S", 9, StyleKit.Muted); tag.Margin = new Thickness(3, 8, 0, 38); menu.Children.Add(tag);
            Nav(menu, "focus", "◷   专注"); Nav(menu, "stats", "▥   统计"); Nav(menu, "projects", "▦   项目");
            StyleKit.Gap(menu, 26); Nav(menu, "settings", "⚙   设置");
            sidebar.Children.Add(menu);
            var foot = new StackPanel(); foot.Children.Add(StyleKit.Text("每一段投入，都有回响。", 11, StyleKit.Muted));
            StyleKit.Gap(foot, 8); foot.Children.Add(StyleKit.Text("本地保存  ·  离线可用", 10, StyleKit.Muted)); StyleKit.Add(sidebar, foot, 1, 0);
            root.Children.Add(new Border { Background = StyleKit.Brush("#DC051723"), BorderBrush = StyleKit.Line, BorderThickness = new Thickness(0, 0, 1, 0), Child = sidebar });
            var body = new Grid { Margin = new Thickness(28, 26, 28, 18) };
            body.RowDefinitions.Add(new RowDefinition()); body.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            body.Children.Add(content); banner.Margin = new Thickness(2, 13, 0, 0); StyleKit.Add(body, banner, 1, 0); StyleKit.Add(root, body, 0, 1);
            timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) }; timer.Tick += Tick; timer.Start();
            Closing += OnClosing;
            SystemEvents.PowerModeChanged += PowerChanged;
            Render();
            if (store.Recovered) Notice("已从自动备份恢复记录；受损文件已保留在数据文件夹。", false);
            else if (engine.Active != null) Notice("已恢复上次未完成的专注，当前为暂停状态。点击继续即可。", false);
            else Notice("准备好后，给自己一段不被打扰的时间。", false);
        }

        private void Nav(Panel parent, string key, string title)
        {
            var button = StyleKit.Button(title, delegate { page = key; Render(); }, false);
            button.HorizontalContentAlignment = HorizontalAlignment.Left; button.Margin = new Thickness(0, 0, 0, 9);
            button.Padding = new Thickness(17, 12, 8, 12); nav[key] = button; parent.Children.Add(button);
        }
        private void Notice(string text, bool error)
        { banner.Text = text; banner.Foreground = error ? StyleKit.Brush("#FFAB91") : StyleKit.Muted; }
        private bool Save()
        {
            try { store.Save(data); lastSave = clock.Elapsed.TotalSeconds; if (saveError) Notice("记录已重新保存成功。", false); saveError = false; return true; }
            catch (Exception ex) { saveError = true; Notice("保存失败，请勿退出：" + ex.Message, true); return false; }
        }
        private string ProjectName(string id)
        { var p = data.Projects.FirstOrDefault(x => x.Id == id); return p == null ? "未知项目" : p.Name; }
        private Brush ProjectColor(string id)
        { var p = data.Projects.FirstOrDefault(x => x.Id == id); return p == null ? StyleKit.Accent : StyleKit.Brush(p.Color); }
        private void Render()
        {
            if (engine.Active == null) Title = "专注时光 · FocusTime";
            foreach (var pair in nav)
            { pair.Value.Background = pair.Key == page ? StyleKit.Brush("#27F5A025") : Brushes.Transparent; pair.Value.Foreground = pair.Key == page ? StyleKit.Accent : StyleKit.Muted; pair.Value.FontWeight = pair.Key == page ? FontWeights.SemiBold : FontWeights.Normal; }
            content.Children.Clear(); content.RowDefinitions.Clear();
            content.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); content.RowDefinitions.Add(new RowDefinition());
            timerText = timerCaption = goalText = goalCaption = runStatus = todayValue = yesterdayValue = streakValue = null;
            allValue = weekValue = statsTodayValue = null; focusDial = goalDial = null; startButton = finishButton = null;
            projects = null; minutes = null; downButton = upButton = null; breakdown = history = null; weekChart = null;
            string title = page == "focus" ? "DO MORE." : page == "stats" ? "看见每一分积累。" : page == "projects" ? "为专注，找到方向。" : "让专注更合心意。";
            string subtitle = page == "focus" ? "一次只做一件事，慢慢来，也会很快。" : page == "stats" ? "按日期和项目回顾你的投入，累计数据包含当前专注。" : page == "projects" ? "为学习、工作和热爱，留下一份时间记录。" : "设置每日目标，管理你的本地记录。";
            var header = new StackPanel { Margin = new Thickness(0, 0, 0, 25) };
            var small = StyleKit.Text(DateTime.Now.ToString("M 月 d 日  dddd", CultureInfo.GetCultureInfo("zh-CN")), 11, StyleKit.Muted); header.Children.Add(small);
            StyleKit.Gap(header, 7); var heading = StyleKit.Text(title, page == "focus" ? 42 : 28, page == "focus" ? StyleKit.Accent : StyleKit.Ink); heading.FontWeight = page == "focus" ? FontWeights.Black : FontWeights.SemiBold; if (page == "focus") heading.FontFamily = new FontFamily("Arial Black"); header.Children.Add(heading);
            StyleKit.Gap(header, 7); header.Children.Add(StyleKit.Text(subtitle, 12, StyleKit.Muted)); content.Children.Add(header);
            UIElement view = page == "focus" ? FocusView() : page == "stats" ? StatsView() : page == "projects" ? ProjectsView() : SettingsView();
            StyleKit.Add(content, view, 1, 0); UpdateDisplay();
        }

        private UIElement FocusView()
        {
            var cols = StyleKit.Columns(1.16, .84);
            var main = new StackPanel(); var titleRow = new DockPanel();
            var miniButton = StyleKit.Button("↗  迷你窗", ShowMini, false); miniButton.Padding = new Thickness(12, 6, 12, 6); miniButton.MinHeight = 30;
            DockPanel.SetDock(miniButton, Dock.Right); titleRow.Children.Add(miniButton); titleRow.Children.Add(StyleKit.Text("专注计时", 17, StyleKit.Ink)); main.Children.Add(titleRow);
            StyleKit.Gap(main, 20);
            main.Children.Add(StyleKit.Text("这段时间，专注于", 11, StyleKit.Muted)); StyleKit.Gap(main, 7);
            projects = new ComboBox { DisplayMemberPath = "Name", ItemsSource = data.Projects.Where(p => !p.Archived || (engine.Active != null && p.Id == engine.Active.ProjectId)).ToList() };
            projects.SelectedItem = data.Projects.FirstOrDefault(p => p.Id == (engine.Active == null ? data.SelectedProjectId : engine.Active.ProjectId));
            if (projects.SelectedItem == null && projects.Items.Count > 0) projects.SelectedIndex = 0;
            projects.IsEnabled = engine.Active == null;
            projects.SelectionChanged += delegate { var p = projects.SelectedItem as FocusProject; if (p != null) { data.SelectedProjectId = p.Id; Save(); UpdateDisplay(); } };
            main.Children.Add(projects); StyleKit.Gap(main, 15);
            var segments = StyleKit.Columns(1, 1);
            downButton = StyleKit.Button("倒计时", delegate { SetMode("down"); }, mode == "down");
            upButton = StyleKit.Button("正计时", delegate { SetMode("up"); }, mode == "up");
            downButton.Margin = new Thickness(0, 0, 5, 0); upButton.Margin = new Thickness(5, 0, 0, 0);
            downButton.IsEnabled = upButton.IsEnabled = engine.Active == null;
            if (engine.Active != null)
            { downButton.Background = engine.Active.Mode == "down" ? StyleKit.Accent : StyleKit.Field; upButton.Background = engine.Active.Mode == "up" ? StyleKit.Accent : StyleKit.Field; downButton.Foreground = engine.Active.Mode == "down" ? StyleKit.DeepInk : StyleKit.Ink; upButton.Foreground = engine.Active.Mode == "up" ? StyleKit.DeepInk : StyleKit.Ink; }
            StyleKit.Add(segments, downButton, 0, 0); StyleKit.Add(segments, upButton, 0, 1); main.Children.Add(segments);
            var durationRow = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 12, 0, 0) };
            minutes = new TextBox { Text = data.CountdownMinutes.ToString(), Width = 63, MaxLength = 4, Padding = new Thickness(7, 5, 7, 5), TextAlignment = TextAlignment.Center };
            minutes.IsEnabled = engine.Active == null && mode == "down";
            minutes.LostFocus += delegate { StoreMinutes(false); UpdateDisplay(); };
            durationRow.Children.Add(minutes); var unit = StyleKit.Text("分钟", 12, StyleKit.Muted); unit.Margin = new Thickness(7, 8, 12, 0); durationRow.Children.Add(unit);
            foreach (int value in new[] { 25, 45, 60 })
            {
                int n = value; var preset = StyleKit.Button(n.ToString(), delegate { data.CountdownMinutes = n; minutes.Text = n.ToString(); Save(); UpdateDisplay(); }, false);
                preset.IsEnabled = engine.Active == null && mode == "down"; preset.Margin = new Thickness(3, 0, 0, 0); preset.Padding = new Thickness(9, 5, 9, 5); preset.MinHeight = 30; durationRow.Children.Add(preset);
            }
            main.Children.Add(durationRow);
            focusDial = new Dial { Width = 224, Height = 224, Ticks = true };
            var timerGrid = new Grid { Margin = new Thickness(0, 9, 0, 8), HorizontalAlignment = HorizontalAlignment.Center };
            timerGrid.Children.Add(focusDial); var digits = new StackPanel { VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center };
            timerText = StyleKit.Text("25:00", 42, StyleKit.Ink); timerText.FontFamily = new FontFamily("Segoe UI"); timerText.FontWeight = FontWeights.Light; timerText.HorizontalAlignment = HorizontalAlignment.Center;
            timerCaption = StyleKit.Text("准备开始", 11, StyleKit.Muted); timerCaption.HorizontalAlignment = HorizontalAlignment.Center; timerCaption.Margin = new Thickness(0, 8, 0, 0);
            digits.Children.Add(timerText); digits.Children.Add(timerCaption); timerGrid.Children.Add(digits); main.Children.Add(timerGrid);
            var actions = StyleKit.Columns(1.4, 1);
            startButton = StyleKit.Button("▶  开始专注", Toggle, true); startButton.Margin = new Thickness(0, 0, 6, 0);
            finishButton = StyleKit.Button("结束并记录", Finish, false); finishButton.Margin = new Thickness(6, 0, 0, 0);
            StyleKit.Add(actions, startButton, 0, 0); StyleKit.Add(actions, finishButton, 0, 1); main.Children.Add(actions);
            runStatus = StyleKit.Text("", 11, StyleKit.Muted); runStatus.TextAlignment = TextAlignment.Center; runStatus.Margin = new Thickness(0, 12, 0, 0); main.Children.Add(runStatus);
            var mainCard = StyleKit.Card(main, 23); mainCard.Margin = new Thickness(0, 0, 15, 0); cols.Children.Add(new ScrollViewer { Content = mainCard });
            var right = new StackPanel();
            var daily = new StackPanel(); var goalHead = new DockPanel();
            var edit = StyleKit.Button("修改", delegate { page = "settings"; Render(); }, false); edit.Padding = new Thickness(9, 3, 9, 3); edit.MinHeight = 26; DockPanel.SetDock(edit, Dock.Right); goalHead.Children.Add(edit); goalHead.Children.Add(StyleKit.Text("每日进度", 17, StyleKit.Ink)); daily.Children.Add(goalHead);
            var circle = new Grid { HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 22, 0, 16) };
            goalDial = new Dial { Width = 190, Height = 190 }; circle.Children.Add(goalDial);
            var center = new StackPanel { VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center };
            var dailyLabel = StyleKit.Text("今日专注", 11, StyleKit.Muted); dailyLabel.HorizontalAlignment = HorizontalAlignment.Center; center.Children.Add(dailyLabel);
            goalText = StyleKit.Text("0", 40, StyleKit.Ink); goalText.FontFamily = new FontFamily("Segoe UI"); goalText.HorizontalAlignment = HorizontalAlignment.Center; center.Children.Add(goalText);
            goalCaption = StyleKit.Text("", 11, StyleKit.Muted); goalCaption.HorizontalAlignment = HorizontalAlignment.Center; center.Children.Add(goalCaption); circle.Children.Add(center); daily.Children.Add(circle);
            todayValue = StyleKit.Text("", 11, StyleKit.Muted); todayValue.TextAlignment = TextAlignment.Center; daily.Children.Add(todayValue); StyleKit.Gap(daily, 20);
            daily.Children.Add(new Border { Height = 1, Background = StyleKit.Line }); StyleKit.Gap(daily, 17);
            var totals = StyleKit.Columns(1, 1); var yesterday = new StackPanel(); yesterday.Children.Add(StyleKit.Text("昨天", 11, StyleKit.Muted));
            yesterdayValue = StyleKit.Text("0 分钟", 16, StyleKit.Ink); yesterdayValue.Margin = new Thickness(0, 8, 0, 0); yesterday.Children.Add(yesterdayValue);
            var streak = new StackPanel(); streak.Children.Add(StyleKit.Text("连续达标", 11, StyleKit.Muted)); streakValue = StyleKit.Text("0 天", 16, StyleKit.Ink); streakValue.Margin = new Thickness(0, 8, 0, 0); streak.Children.Add(streakValue);
            totals.Children.Add(yesterday); StyleKit.Add(totals, streak, 0, 1); daily.Children.Add(totals); right.Children.Add(StyleKit.Card(daily, 23));
            StyleKit.Gap(right, 15); var note = new StackPanel(); var manifesto = StyleKit.Text("STAY IN\nMOTION.", 27, StyleKit.Accent); manifesto.FontFamily = new FontFamily("Arial Black"); manifesto.FontWeight = FontWeights.Black; note.Children.Add(manifesto); StyleKit.Gap(note, 10);
            note.Children.Add(StyleKit.Text("像海浪一样，持续向前。\n把这一刻，留给真正重要的事。", 12, StyleKit.Muted));
            var noteCard = StyleKit.Card(note, 22); noteCard.Background = StyleKit.Brush("#B3071E2C"); right.Children.Add(noteCard);
            StyleKit.Add(cols, new ScrollViewer { Content = right }, 0, 1); return cols;
        }

        private void SetMode(string value) { if (engine.Active != null) return; StoreMinutes(false); mode = value; data.SelectedMode = mode; Save(); Render(); }
        private bool StoreMinutes(bool strict)
        {
            if (minutes == null || !minutes.IsEnabled) return true;
            int value;
            if (!Int32.TryParse(minutes.Text, out value) || value < 1 || value > 1440)
            { if (strict) { Notice("请输入 1–1440 之间的整数分钟。", true); minutes.Focus(); return false; } minutes.Text = data.CountdownMinutes.ToString(); return true; }
            if (data.CountdownMinutes != value) { data.CountdownMinutes = value; Save(); }
            return true;
        }
        private void Toggle()
        {
            if (engine.Active == null)
            {
                if (!StoreMinutes(true)) return;
                var project = projects == null ? data.Projects.FirstOrDefault(p => p.Id == data.SelectedProjectId && !p.Archived) : projects.SelectedItem as FocusProject;
                if (project == null) { Notice("请先在项目页新建或恢复一个项目。", true); return; }
                engine.Start(project, mode, data.CountdownMinutes, DateTime.UtcNow, clock.Elapsed.TotalSeconds);
                data.SelectedProjectId = project.Id;
                Notice("正在专注于「" + project.Name + "」，加油。", false);
            }
            else if (engine.Running)
            {
                var result = engine.Pause(DateTime.UtcNow, clock.Elapsed.TotalSeconds);
                if (result == TickOutcome.Completed) { Complete(); return; }
                Notice("已暂停，休息一下也没关系。", false);
            }
            else
            {
                if (engine.Active.Mode == "down" && engine.Remaining <= 0) { Complete(); return; }
                engine.Resume(DateTime.UtcNow, clock.Elapsed.TotalSeconds); Notice("继续专注，保持自己的节奏。", false);
            }
            Save(); Render();
        }
        private void Finish()
        {
            if (engine.Active == null) return;
            var record = engine.Finish(DateTime.UtcNow, clock.Elapsed.TotalSeconds);
            bool saved = Save(); Render();
            if (saved) Notice(record.Seconds < .000001 ? "本次未产生计时时长。" : "已记录「" + ProjectName(record.ProjectId) + "」的 " + Stats.Duration(record.Seconds) + "。", false);
        }
        private void Complete()
        {
            var record = engine.Finish(DateTime.UtcNow, clock.Elapsed.TotalSeconds);
            if (record == null) return;
            bool saved = Save(); Render();
            if (saved) Notice("✓ 本次专注已完成！已记录 " + Stats.Duration(record.Seconds) + "，可以休息一下了。", false);
            if (data.Sound) System.Media.SystemSounds.Asterisk.Play();
            Title = "✓ 专注完成 · 专注时光";
            if (mini != null) mini.ShowDone();
        }
        private void Tick(object sender, EventArgs args)
        {
            var result = engine.Tick(DateTime.UtcNow, clock.Elapsed.TotalSeconds);
            if (result == TickOutcome.Completed) { Complete(); return; }
            if (result == TickOutcome.Interrupted) { Save(); Render(); if (!saveError) Notice("检测到休眠、系统时间变化或长时间无响应，已自动暂停。", false); }
            UpdateDisplay();
            if (engine.Active != null && clock.Elapsed.TotalSeconds - lastSave >= 5) Save();
            if (page == "stats" && (clock.Elapsed.TotalSeconds - lastStats >= 5 || statsDate != DateTime.Today)) { UpdateStats(); lastStats = clock.Elapsed.TotalSeconds; statsDate = DateTime.Today; }
        }
        internal void UpdateDisplay()
        {
            var active = engine.Active;
            string currentMode = active == null ? mode : active.Mode;
            double shown = active == null ? (currentMode == "down" ? data.CountdownMinutes * 60 : 0) : currentMode == "down" ? engine.Remaining : active.Seconds;
            string text = Stats.Clock(shown, currentMode == "down");
            string caption = active == null ? (currentMode == "down" ? "留一段时间，认真投入" : "从零开始，记录每一刻") : engine.Running ? "正在专注" : "已暂停 · 时间不计入";
            double progress = active == null ? 0 : currentMode == "down" ? active.Seconds / active.TargetSeconds : (active.Seconds % 3600) / 3600;
            string projectId = active == null ? data.SelectedProjectId : active.ProjectId;
            if (timerText != null) { timerText.Text = text; timerText.FontSize = text.Length > 5 ? 33 : 42; timerCaption.Text = caption; focusDial.Progress = progress; focusDial.Accent = StyleKit.Accent; focusDial.InvalidateVisual(); }
            if (startButton != null) startButton.Content = active == null ? "▶  开始专注" : engine.Running ? "Ⅱ  暂停" : "▶  继续专注";
            if (finishButton != null) finishButton.IsEnabled = active != null;
            if (runStatus != null) runStatus.Text = active == null ? "本地自动保存 · 暂停不计时" : "本次已专注 " + Stats.Duration(active.Seconds);
            if (goalText != null)
            {
                double today = Stats.Total(data, null, DateTime.Today, DateTime.Today);
                goalText.Text = Math.Floor(today / 60).ToString("0"); goalCaption.Text = "分钟 / 目标 " + data.DailyGoalMinutes + " 分钟";
                goalDial.Progress = today / (data.DailyGoalMinutes * 60); goalDial.InvalidateVisual();
                todayValue.Text = today >= data.DailyGoalMinutes * 60 ? "✓ 今日目标已达成" : "距离目标还差 " + (Math.Ceiling((data.DailyGoalMinutes * 60 - today) / 60)).ToString("0") + " 分钟";
                yesterdayValue.Text = Stats.Duration(Stats.Total(data, null, DateTime.Today.AddDays(-1), DateTime.Today.AddDays(-1)));
                streakValue.Text = Stats.Streak(data, DateTime.Today) + " 天";
            }
            if (active != null) Title = text + (engine.Running ? " · 专注中" : " · 已暂停") + " · 专注时光";
            if (mini != null) mini.Update(text, caption, active == null ? ProjectName(data.SelectedProjectId) : ProjectName(active.ProjectId), progress, active != null, engine.Running, StyleKit.Accent);
        }
        private void PowerChanged(object sender, PowerModeChangedEventArgs args)
        {
            if (args.Mode != PowerModes.Suspend) return;
            Dispatcher.BeginInvoke(new Action(delegate
            {
                if (engine.Running)
                {
                    var result = engine.Pause(DateTime.UtcNow, clock.Elapsed.TotalSeconds);
                    if (result == TickOutcome.Completed) { Complete(); return; }
                    Save(); Render(); if (!saveError) Notice("电脑进入休眠，专注已暂停。恢复后请手动继续。", false);
                }
            }));
        }
        private void OnClosing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (closing) return;
            StoreMinutes(false);
            var result = engine.Pause(DateTime.UtcNow, clock.Elapsed.TotalSeconds);
            if (result == TickOutcome.Completed) engine.Finish(DateTime.UtcNow, clock.Elapsed.TotalSeconds);
            if (!Save())
            {
                e.Cancel = true; UpdateDisplay();
                MessageBox.Show(this, "记录保存失败，窗口暂不关闭。\n请检查磁盘空间或文件权限，或在统计页导出 CSV 备份。\n\n" + store.FilePath, "请先保存记录", MessageBoxButton.OK, MessageBoxImage.Warning); return;
            }
            closing = true; timer.Stop(); SystemEvents.PowerModeChanged -= PowerChanged;
            if (mini != null) mini.Close();
        }
        private void ShowMini()
        {
            if (mini == null)
            {
                mini = new MiniWindow(Toggle, Finish);
                mini.Closed += delegate { mini = null; if (!closing) { WindowState = WindowState.Normal; Show(); Activate(); } };
                mini.Show();
            }
            UpdateDisplay(); WindowState = WindowState.Minimized; mini.Activate();
        }

        private UIElement StatsView()
        {
            var pagePanel = new StackPanel(); var summaries = StyleKit.Columns(1, 1, 1);
            statsTodayValue = StatBox(summaries, 0, "今日专注"); weekValue = StatBox(summaries, 1, "本周专注"); allValue = StatBox(summaries, 2, "累计专注"); pagePanel.Children.Add(summaries);
            StyleKit.Gap(pagePanel, 16);
            var chartPanel = new StackPanel(); var chartTitle = new DockPanel(); var export = StyleKit.Button("导出 CSV", Export, false); export.Padding = new Thickness(12, 6, 12, 6); DockPanel.SetDock(export, Dock.Right); chartTitle.Children.Add(export); chartTitle.Children.Add(StyleKit.Text("近 7 天 · 每日投入", 16, StyleKit.Ink)); chartPanel.Children.Add(chartTitle);
            weekChart = new Grid { Height = 172, Margin = new Thickness(0, 18, 0, 0) }; chartPanel.Children.Add(weekChart); pagePanel.Children.Add(StyleKit.Card(chartPanel, 22));
            StyleKit.Gap(pagePanel, 16);
            var details = new StackPanel(); var filterRow = new DockPanel();
            var filters = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            statsRange = new ComboBox { Width = 95, ItemsSource = new[] { "全部时间", "今天", "本周", "本月" }, SelectedIndex = rangeIndex, Margin = new Thickness(0, 0, 9, 0) }; filters.Children.Add(statsRange);
            var choices = new List<FocusProject> { new FocusProject { Id = "", Name = "全部项目" } }; choices.AddRange(data.Projects);
            statsProject = new ComboBox { Width = 160, DisplayMemberPath = "Name", ItemsSource = choices };
            statsProject.SelectedItem = choices.FirstOrDefault(p => p.Id == (filterProject ?? "")) ?? choices[0]; filters.Children.Add(statsProject);
            statsRange.SelectionChanged += delegate { rangeIndex = statsRange.SelectedIndex; UpdateStats(); };
            statsProject.SelectionChanged += delegate { var p = statsProject.SelectedItem as FocusProject; filterProject = p == null || p.Id == "" ? null : p.Id; UpdateStats(); };
            DockPanel.SetDock(filters, Dock.Right); filterRow.Children.Add(filters); filterRow.Children.Add(StyleKit.Text("项目分布与记录", 16, StyleKit.Ink)); details.Children.Add(filterRow);
            breakdown = new StackPanel { Margin = new Thickness(0, 16, 0, 16) }; details.Children.Add(breakdown);
            details.Children.Add(new Border { Height = 1, Background = StyleKit.Line });
            history = new StackPanel { Margin = new Thickness(0, 12, 0, 0) }; details.Children.Add(history);
            pagePanel.Children.Add(StyleKit.Card(details, 22)); UpdateStats(); return new ScrollViewer { Content = pagePanel };
        }
        private TextBlock StatBox(Grid grid, int column, string label)
        {
            var p = new StackPanel(); p.Children.Add(StyleKit.Text(label, 11, StyleKit.Muted)); StyleKit.Gap(p, 9);
            var value = StyleKit.Text("0 秒", 22, StyleKit.Ink); value.FontWeight = FontWeights.SemiBold; p.Children.Add(value);
            var card = StyleKit.Card(p, 18); card.Margin = new Thickness(column == 0 ? 0 : 5, 0, column == 2 ? 0 : 5, 0); StyleKit.Add(grid, card, 0, column); return value;
        }
        private DateTime WeekStart() { return DateTime.Today.AddDays(-((int)DateTime.Today.DayOfWeek + 6) % 7); }
        private void UpdateStats()
        {
            if (allValue == null || breakdown == null || history == null || weekChart == null) return;
            allValue.Text = Stats.Duration(Stats.Total(data, null, null, null)); statsTodayValue.Text = Stats.Duration(Stats.Total(data, null, DateTime.Today, DateTime.Today)); weekValue.Text = Stats.Duration(Stats.Total(data, null, WeekStart(), DateTime.Today));
            weekChart.Children.Clear(); weekChart.ColumnDefinitions.Clear();
            var days = Enumerable.Range(0, 7).Select(i => DateTime.Today.AddDays(i - 6)).ToArray();
            var totals = days.Select(d => Stats.Total(data, null, d, d)).ToArray(); double max = Math.Max(60, totals.Max());
            for (int i = 0; i < 7; i++)
            {
                weekChart.ColumnDefinitions.Add(new ColumnDefinition()); var column = new Grid { Margin = new Thickness(9, 0, 9, 0) };
                column.RowDefinitions.Add(new RowDefinition()); column.RowDefinitions.Add(new RowDefinition { Height = new GridLength(25) });
                var barStack = new StackPanel { VerticalAlignment = VerticalAlignment.Bottom, HorizontalAlignment = HorizontalAlignment.Stretch };
                var value = StyleKit.Text(totals[i] < 60 && totals[i] > 0 ? "<1 分" : Math.Floor(totals[i] / 60).ToString("0") + " 分", 10, StyleKit.Muted); value.TextAlignment = TextAlignment.Center; value.Margin = new Thickness(0, 0, 0, 6); barStack.Children.Add(value);
                var bar = new Border { Height = Math.Max(3, totals[i] / max * 113), MaxWidth = 44, CornerRadius = new CornerRadius(5, 5, 0, 0), Background = i == 6 ? StyleKit.Accent : StyleKit.Brush("#4B8399"), ToolTip = days[i].ToString("yyyy-MM-dd") + " · " + Stats.Duration(totals[i]) }; barStack.Children.Add(bar); column.Children.Add(barStack);
                var dayText = StyleKit.Text(i == 6 ? "今天" : days[i].ToString("M/d"), 10, StyleKit.Muted); dayText.TextAlignment = TextAlignment.Center; dayText.VerticalAlignment = VerticalAlignment.Bottom; StyleKit.Add(column, dayText, 1, 0); StyleKit.Add(weekChart, column, 0, i);
            }
            DateTime? start = rangeIndex == 1 ? DateTime.Today : rangeIndex == 2 ? WeekStart() : rangeIndex == 3 ? new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1) : (DateTime?)null;
            DateTime? end = rangeIndex == 0 ? (DateTime?)null : DateTime.Today;
            breakdown.Children.Clear(); history.Children.Clear();
            var projectTotals = data.Projects.Where(p => filterProject == null || p.Id == filterProject).Select(p => new { Project = p, Seconds = Stats.Total(data, p.Id, start, end) }).OrderByDescending(x => x.Seconds).ToList();
            double whole = projectTotals.Sum(p => p.Seconds);
            foreach (var item in projectTotals.Where(x => x.Seconds > 0))
            {
                var row = new Grid { Margin = new Thickness(0, 0, 0, 10) }; row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(150) }); row.ColumnDefinitions.Add(new ColumnDefinition()); row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(150) });
                var name = StyleKit.Text(item.Project.Name + (item.Project.Archived ? " · 已归档" : ""), 12, StyleKit.Ink); name.TextTrimming = TextTrimming.CharacterEllipsis; name.TextWrapping = TextWrapping.NoWrap; name.ToolTip = item.Project.Name; row.Children.Add(name);
                var track = new Grid { VerticalAlignment = VerticalAlignment.Center, Height = 7, Margin = new Thickness(12, 0, 15, 0) };
                track.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(item.Seconds / whole, GridUnitType.Star) }); track.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(Math.Max(0, 1 - item.Seconds / whole), GridUnitType.Star) });
                var back = new Border { Background = StyleKit.Brush("#243F50"), CornerRadius = new CornerRadius(4) }; Grid.SetColumnSpan(back, 2); track.Children.Add(back); track.Children.Add(new Border { Background = StyleKit.Brush(item.Project.Color), CornerRadius = new CornerRadius(4) }); StyleKit.Add(row, track, 0, 1);
                var amount = StyleKit.Text(Stats.Duration(item.Seconds), 11, StyleKit.Muted); amount.TextAlignment = TextAlignment.Right; StyleKit.Add(row, amount, 0, 2); breakdown.Children.Add(row);
            }
            if (whole == 0) breakdown.Children.Add(StyleKit.Text("这里还没有专注记录。开始第一段专注吧。", 13, StyleKit.Muted));
            string from = start.HasValue ? start.Value.ToString("yyyy-MM-dd") : null, to = end.HasValue ? end.Value.ToString("yyyy-MM-dd") : null;
            var records = Stats.All(data).Where(s => filterProject == null || s.ProjectId == filterProject)
                .Select(s => new { Session = s, Seconds = s.Days.Where(d => (from == null || String.CompareOrdinal(d.Day, from) >= 0) && (to == null || String.CompareOrdinal(d.Day, to) <= 0)).Sum(d => d.Seconds) })
                .Where(x => x.Seconds > 0).OrderByDescending(x => x.Session.StartedUtc).ToList();
            history.Children.Add(StyleKit.Text("专注记录 · " + records.Count + " 段（显示最近 100 段；时长按所选日期分摊）", 10, StyleKit.Muted));
            foreach (var record in records.Take(100))
            {
                var s = record.Session; var row = new DockPanel { Margin = new Thickness(0, 12, 0, 0) };
                var duration = StyleKit.Text(Stats.Duration(record.Seconds), 12, StyleKit.Ink); duration.Margin = new Thickness(14, 0, 0, 0); DockPanel.SetDock(duration, Dock.Right); row.Children.Add(duration);
                var info = new StackPanel(); var name = StyleKit.Text(ProjectName(s.ProjectId), 12, StyleKit.Ink); info.Children.Add(name);
                string status = s == data.Active ? engine.Running ? "进行中" : "暂停中" : s.Completed ? "已完成" : "手动结束";
                info.Children.Add(StyleKit.Text(s.StartedUtc.ToLocalTime().ToString("MM/dd HH:mm") + "  ·  " + (s.Mode == "up" ? "正计时" : "倒计时") + "  ·  " + status, 10, StyleKit.Muted)); row.Children.Add(info); history.Children.Add(row);
            }
        }
        private void Export()
        {
            string exportFolder = Path.Combine(Path.GetDirectoryName(DataStore.DefaultDirectory), "Exports");
            try { Directory.CreateDirectory(exportFolder); }
            catch (Exception ex) { Notice("无法创建 D 盘导出目录：" + ex.Message, true); return; }
            var dialog = new SaveFileDialog { Filter = "CSV 文件 (*.csv)|*.csv", InitialDirectory = exportFolder, FileName = "专注记录-" + DateTime.Now.ToString("yyyyMMdd-HHmm") + ".csv", AddExtension = true };
            if (dialog.ShowDialog(this) != true) return;
            try { Stats.ExportCsv(data, dialog.FileName); if (!saveError) Notice("已导出全部记录（包括当前专注），每行对应一个自然日。", false); }
            catch (Exception ex) { Notice("导出失败：" + ex.Message, true); }
        }

        private UIElement ProjectsView()
        {
            var panel = new StackPanel(); var top = new DockPanel { Margin = new Thickness(0, 0, 0, 16) };
            var create = StyleKit.Button("＋  新建项目", delegate { EditProject(null); }, true); DockPanel.SetDock(create, Dock.Right); top.Children.Add(create); top.Children.Add(StyleKit.Text("我的项目  ·  " + data.Projects.Count(p => !p.Archived), 17, StyleKit.Ink)); panel.Children.Add(top);
            foreach (var project in data.Projects.OrderBy(p => p.Archived))
            {
                var p = project; var row = new DockPanel();
                var actions = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
                var edit = StyleKit.Button("编辑", delegate { EditProject(p); }, false); edit.Margin = new Thickness(0, 0, 8, 0); actions.Children.Add(edit);
                var archive = StyleKit.Button(p.Archived ? "恢复" : "归档", delegate
                {
                    if (engine.Active != null && engine.Active.ProjectId == p.Id) { Notice("当前项目有未完成的专注，请先结束并记录。", true); return; }
                    p.Archived = !p.Archived;
                    if (p.Archived && data.SelectedProjectId == p.Id) data.SelectedProjectId = data.Projects.Where(x => !x.Archived).Select(x => x.Id).FirstOrDefault();
                    if (!p.Archived && data.SelectedProjectId == null) data.SelectedProjectId = p.Id;
                    Save(); Render(); if (!saveError) Notice(p.Archived ? "项目已归档，历史时长仍保留。" : "项目已恢复。", false);
                }, false); actions.Children.Add(archive); DockPanel.SetDock(actions, Dock.Right); row.Children.Add(actions);
                var dot = new Border { Width = 11, Height = 11, Background = StyleKit.Brush(p.Color), CornerRadius = new CornerRadius(6), Margin = new Thickness(0, 0, 15, 0), VerticalAlignment = VerticalAlignment.Center }; DockPanel.SetDock(dot, Dock.Left); row.Children.Add(dot);
                var info = new StackPanel { Margin = new Thickness(0, 0, 12, 0) }; info.Children.Add(StyleKit.Text(p.Name + (p.Archived ? "  ·  已归档" : ""), 16, StyleKit.Ink)); StyleKit.Gap(info, 7);
                info.Children.Add(StyleKit.Text("累计 " + Stats.Duration(Stats.Total(data, p.Id, null, null)) + "   /   " + data.Sessions.Count(s => s.ProjectId == p.Id) + " 段已结束", 11, StyleKit.Muted)); row.Children.Add(info);
                var card = StyleKit.Card(row, 22); card.Margin = new Thickness(0, 0, 0, 12); if (p.Archived) card.Opacity = .65; panel.Children.Add(card);
            }
            StyleKit.Gap(panel, 8); panel.Children.Add(StyleKit.Text("项目归档后不再出现在计时选择中，历史记录和累计时长会继续保留。", 11, StyleKit.Muted)); return new ScrollViewer { Content = panel };
        }
        private void EditProject(FocusProject project)
        {
            var dialog = new ProjectDialog(project) { Owner = this };
            if (dialog.ShowDialog() != true) return;
            string name = dialog.ProjectName;
            if (data.Projects.Any(p => p != project && String.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase)))
            { Notice("已经有同名项目，请换一个名称。", true); return; }
            if (project == null)
            { project = new FocusProject { Id = Guid.NewGuid().ToString("N") }; data.Projects.Add(project); if (data.SelectedProjectId == null) data.SelectedProjectId = project.Id; }
            project.Name = name; project.Color = dialog.ProjectColor;
            Save(); Render();
        }
        internal void RunSmoke(string folder)
        {
            Directory.CreateDirectory(folder);
            page = "focus"; Render();
            upButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            startButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            if (!engine.Running || engine.Active == null || engine.Active.Mode != "up") throw new Exception("UI count-up start failed.");
            Thread.Sleep(1100);
            Tick(null, EventArgs.Empty);
            if (engine.Active.Seconds < 1 || timerText.Text == "00:00") throw new Exception("Live timer did not advance.");
            startButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            if (engine.Running || engine.Active == null) throw new Exception("UI pause failed.");
            double pausedSeconds = engine.Active.Seconds;
            Thread.Sleep(250); Tick(null, EventArgs.Empty);
            if (engine.Active.Seconds != pausedSeconds) throw new Exception("Paused UI counted time.");
            if (store.Load().Active == null) throw new Exception("UI draft did not persist.");
            startButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            if (!engine.Running) throw new Exception("UI resume failed.");
            finishButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            if (engine.Active != null || data.Sessions.Count != 1) throw new Exception("UI finish failed.");
            downButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            minutes.Text = "1"; StoreMinutes(true);
            startButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            // Fixture starts one second before the target so the live UI completion path is exercised.
            engine.Active.Seconds = 59;
            engine.Active.Days.Add(new DaySlice { Day = DateTime.Today.ToString("yyyy-MM-dd"), Seconds = 59 });
            data.Sound = false;
            Thread.Sleep(1200); Tick(null, EventArgs.Empty);
            if (engine.Active != null || data.Sessions.Count != 2 || !data.Sessions.Last().Completed)
                throw new Exception("Live countdown completion did not archive exactly once.");
            if (store.Load().Sessions.Count != 2) throw new Exception("UI records did not survive reload.");
            // Statistics previews use synthetic records in the isolated test data directory only.
            Action<string> fillProjectDialog = delegate(string name)
            {
                Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(delegate
                {
                    var dialog = Application.Current.Windows.OfType<ProjectDialog>().Single();
                    var form = (StackPanel)dialog.Content;
                    form.Children.OfType<TextBox>().Single().Text = name;
                    dialog.UpdateLayout(); Capture(dialog, Path.Combine(folder, "project-dialog.png"));
                    form.Children.OfType<Button>().Single().RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                }));
            };
            fillProjectDialog("阅读"); EditProject(null);
            var reading = data.Projects.FirstOrDefault(p => p.Name == "阅读");
            if (reading == null) throw new Exception("Create project dialog failed.");
            fillProjectDialog("阅读与思考"); EditProject(reading);
            if (reading.Name != "阅读与思考") throw new Exception("Edit project dialog failed.");
            reading.Color = "#6C927B";
            for (int day = 6; day >= 0; day--)
            {
                double seconds = (day + 1) * 450;
                var date = DateTime.Today.AddDays(-day).AddHours(10).ToUniversalTime();
                data.Sessions.Add(new FocusSession { Id = Guid.NewGuid().ToString("N"), ProjectId = day % 2 == 0 ? data.Projects[0].Id : reading.Id,
                    ProjectName = day % 2 == 0 ? data.Projects[0].Name : reading.Name, Mode = "up", StartedUtc = date, EndedUtc = date.AddSeconds(seconds),
                    Seconds = seconds, Days = new List<DaySlice> { new DaySlice { Day = date.ToLocalTime().ToString("yyyy-MM-dd"), Seconds = seconds } } });
            }
            data.CountdownMinutes = 25; Save();
            page = "projects"; Render(); UpdateLayout();
            VisualDescendants<Button>(content).Where(b => Convert.ToString(b.Content) == "归档").Last().RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            if (!reading.Archived || Stats.Total(data, reading.Id, null, null) <= 0) throw new Exception("Archive project lost history.");
            UpdateLayout();
            VisualDescendants<Button>(content).Single(b => Convert.ToString(b.Content) == "恢复").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            if (reading.Archived) throw new Exception("Restore project failed.");
            page = "focus"; Render(); UpdateLayout();
            Dispatcher.Invoke(DispatcherPriority.ApplicationIdle, new Action(delegate { }));
            projects.IsDropDownOpen = true; UpdateLayout();
            Dispatcher.Invoke(DispatcherPriority.ApplicationIdle, new Action(delegate { }));
            var projectPopup = projects.Template.FindName("PART_Popup", projects) as System.Windows.Controls.Primitives.Popup;
            if (projectPopup == null || !projectPopup.IsOpen) throw new Exception("Themed project dropdown did not open.");
            projects.SelectedItem = reading;
            if (data.SelectedProjectId != reading.Id) throw new Exception("Themed project dropdown did not change selection.");
            projects.IsDropDownOpen = false;
            projects.SelectedItem = data.Projects[0];
            foreach (string target in new[] { "focus", "stats", "projects", "settings" })
            {
                page = target; Render(); UpdateLayout(); Capture(this, Path.Combine(folder, target + ".png"));
            }
            page = "stats"; Render();
            statsProject.SelectedItem = ((IEnumerable<FocusProject>)statsProject.ItemsSource).First(p => p.Id == reading.Id);
            if (Stats.Total(data, reading.Id, null, null) <= 0 || breakdown.Children.Count == 0) throw new Exception("Project filter failed.");
            statsRange.SelectedIndex = 1;
            if (history.Children.Count != 1) throw new Exception("Date/project filter failed.");
            filterProject = null; rangeIndex = 0;
            Width = MinWidth; Height = MinHeight; page = "focus"; Render(); UpdateLayout(); Capture(this, Path.Combine(folder, "focus-compact.png"));
            Width = 1180; Height = 820;
            page = "focus"; Render();
            ShowMini();
            mini.Update("20:00", "正在专注", "日常学习", .2, true, true, StyleKit.Accent); mini.UpdateLayout();
            Capture(mini, Path.Combine(folder, "mini.png")); mini.Close();
            if (mini != null || WindowState != WindowState.Normal) throw new Exception("Mini window did not restore main window.");
            Stats.ExportCsv(data, Path.Combine(folder, "sample-records.csv"));
            File.WriteAllText(Path.Combine(folder, "ui-test-passed.txt"), "PASS: real timer advance, pause exclusion, button-based start/pause/resume/finish, countdown auto-completion, persistence reload, project create/edit/archive/restore, themed dropdown popup/selection, four pages, populated statistics, filters, compact layout, mini window restoration and CSV.\r\nPreview records are synthetic and use isolated temporary storage.\r\n" + DateTime.Now.ToString("O"));
        }
        private static IEnumerable<T> VisualDescendants<T>(DependencyObject parent) where T : DependencyObject
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is T) yield return (T)child;
                foreach (var nested in VisualDescendants<T>(child)) yield return nested;
            }
        }
        private static void Capture(Window window, string path)
        {
            var view = VisualTreeHelper.GetChildrenCount(window) > 0 ? VisualTreeHelper.GetChild(window, 0) as FrameworkElement : null;
            if (view == null) view = (FrameworkElement)window.Content;
            view.UpdateLayout();
            if (view.ActualWidth <= 0 || view.ActualHeight <= 0) throw new Exception("Window layout is empty.");
            var bitmap = new RenderTargetBitmap((int)Math.Ceiling(view.ActualWidth), (int)Math.Ceiling(view.ActualHeight), 96, 96, PixelFormats.Pbgra32);
            var background = new DrawingVisual();
            using (var drawing = background.RenderOpen()) drawing.DrawRectangle(window.Background, null, new Rect(0, 0, view.ActualWidth, view.ActualHeight));
            bitmap.Render(background);
            bitmap.Render(view);
            var png = new PngBitmapEncoder(); png.Frames.Add(BitmapFrame.Create(bitmap));
            using (var file = File.Create(path)) png.Save(file);
        }
        private UIElement SettingsView()
        {
            var panel = new StackPanel(); var daily = new StackPanel(); daily.Children.Add(StyleKit.Text("每日目标", 17, StyleKit.Ink)); StyleKit.Gap(daily, 9);
            daily.Children.Add(StyleKit.Text("累计专注达到目标后，首页圆环会标记达成。连续达标按当前目标计算。", 12, StyleKit.Muted)); StyleKit.Gap(daily, 17);
            var row = new StackPanel { Orientation = Orientation.Horizontal }; var goal = new TextBox { Text = data.DailyGoalMinutes.ToString(), Width = 100, MaxLength = 4 };
            row.Children.Add(goal); var label = StyleKit.Text("分钟 / 天", 12, StyleKit.Muted); label.Margin = new Thickness(12, 12, 20, 0); row.Children.Add(label);
            row.Children.Add(StyleKit.Button("保存目标", delegate { int value; if (!Int32.TryParse(goal.Text, out value) || value < 1 || value > 1440) { Notice("每日目标请输入 1–1440 之间的整数分钟。", true); return; } data.DailyGoalMinutes = value; if (Save()) Notice("每日目标已更新为 " + value + " 分钟。", false); }, true)); daily.Children.Add(row);
            StyleKit.Gap(daily, 21); var sound = new CheckBox { Content = "倒计时结束时播放系统提示音", IsChecked = data.Sound, FontSize = 13 };
            sound.Checked += delegate { data.Sound = true; Save(); }; sound.Unchecked += delegate { data.Sound = false; Save(); }; daily.Children.Add(sound); panel.Children.Add(StyleKit.Card(daily, 25));
            StyleKit.Gap(panel, 17); var storage = new StackPanel(); storage.Children.Add(StyleKit.Text("数据与备份", 17, StyleKit.Ink)); StyleKit.Gap(storage, 11);
            storage.Children.Add(StyleKit.Text("记录仅保存在这台电脑。计时期间每 5 秒保存一次，开始、暂停和结束时立即保存；关闭程序会暂停当前专注，下次打开可继续。", 12, StyleKit.Muted));
            StyleKit.Gap(storage, 14); var path = new TextBox { Text = store.DirectoryPath, IsReadOnly = true, FontSize = 11 }; storage.Children.Add(path); StyleKit.Gap(storage, 15);
            var buttons = new StackPanel { Orientation = Orientation.Horizontal };
            var open = StyleKit.Button("打开数据文件夹", delegate { try { Process.Start("explorer.exe", "\"" + store.DirectoryPath + "\""); } catch (Exception ex) { Notice(ex.Message, true); } }, false); open.Margin = new Thickness(0, 0, 10, 0); buttons.Children.Add(open);
            buttons.Children.Add(StyleKit.Button("导出全部记录", Export, false)); storage.Children.Add(buttons); StyleKit.Gap(storage, 15);
            storage.Children.Add(StyleKit.Text("data.json 是主记录，data.json.bak 是上一次保存的备份。迁移时先关闭程序，再复制整个 FocusTime 数据文件夹。", 11, StyleKit.Muted)); panel.Children.Add(StyleKit.Card(storage, 25));
            StyleKit.Gap(panel, 17); var about = new StackPanel(); about.Children.Add(StyleKit.Text("专注时光  1.1  /  OCEAN", 16, StyleKit.Ink)); StyleKit.Gap(about, 10);
            about.Children.Add(StyleKit.Text("正计时 · 倒计时 · 项目归档 · 每日统计 · 置顶迷你窗\n\n电脑休眠或计时回调停顿超过 10 秒时会自动暂停；锁屏或切换应用仍继续计时。正计时的圆环每小时走一圈。程序不接管 Windows 通知或勿扰模式。", 12, StyleKit.Muted)); panel.Children.Add(StyleKit.Card(about, 25)); return new ScrollViewer { Content = panel };
        }
    }

    internal sealed class MiniWindow : Window
    {
        private readonly Dial dial;
        private readonly TextBlock time, state, project;
        private readonly Button toggle, finish;
        private bool done;
        public MiniWindow(Action toggleAction, Action finishAction)
        {
            Title = "专注时光 · 置顶小窗"; Width = 322; Height = 422; ResizeMode = ResizeMode.NoResize; Topmost = true;
            WindowStartupLocation = WindowStartupLocation.CenterScreen; Background = StyleKit.Ocean; FontFamily = new FontFamily("Microsoft YaHei UI"); Foreground = StyleKit.Ink; StyleKit.DarkTitleBar(this);
            var panel = new StackPanel { Margin = new Thickness(22, 17, 22, 16) }; Content = new Border { Background = StyleKit.Brush("#DC061A28"), Child = panel };
            project = StyleKit.Text("", 14, StyleKit.Ink); project.TextAlignment = TextAlignment.Center; project.TextTrimming = TextTrimming.CharacterEllipsis; project.TextWrapping = TextWrapping.NoWrap; panel.Children.Add(project);
            var face = new Grid { HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 13, 0, 8) }; dial = new Dial { Width = 218, Height = 218, Ticks = true }; face.Children.Add(dial);
            time = StyleKit.Text("00:00", 39, StyleKit.Ink); time.FontFamily = new FontFamily("Segoe UI"); time.VerticalAlignment = VerticalAlignment.Center; time.HorizontalAlignment = HorizontalAlignment.Center; face.Children.Add(time); panel.Children.Add(face);
            state = StyleKit.Text("", 11, StyleKit.Muted); state.TextAlignment = TextAlignment.Center; panel.Children.Add(state); StyleKit.Gap(panel, 16);
            var controls = StyleKit.Columns(1, 1); toggle = StyleKit.Button("开始", delegate { done = false; toggleAction(); }, true); toggle.Margin = new Thickness(0, 0, 5, 0);
            finish = StyleKit.Button("结束记录", finishAction, false); finish.Margin = new Thickness(5, 0, 0, 0); controls.Children.Add(toggle); StyleKit.Add(controls, finish, 0, 1); panel.Children.Add(controls);
        }
        public void Update(string text, string caption, string name, double progress, bool active, bool running, Brush accent)
        {
            if (active) done = false;
            time.Text = done ? "✓" : text; time.FontSize = text.Length > 5 ? 31 : 39;
            state.Text = done ? "专注完成，休息一下吧。" : caption;
            project.Text = name; project.ToolTip = name; dial.Progress = done ? 1 : progress; dial.Accent = accent; dial.InvalidateVisual();
            toggle.Content = !active ? "▶ 开始" : running ? "Ⅱ 暂停" : "▶ 继续"; finish.IsEnabled = active;
        }
        public void ShowDone() { done = true; time.Text = "✓"; state.Text = "专注完成，休息一下吧。"; }
    }

    internal sealed class ProjectDialog : Window
    {
        public string ProjectName;
        public string ProjectColor;
        public ProjectDialog(FocusProject project)
        {
            Title = project == null ? "新建项目" : "编辑项目"; Width = 410; Height = 300; ResizeMode = ResizeMode.NoResize;
            WindowStartupLocation = WindowStartupLocation.CenterOwner; Background = StyleKit.Brush("#0A202F"); FontFamily = new FontFamily("Microsoft YaHei UI"); Foreground = StyleKit.Ink; StyleKit.DarkTitleBar(this);
            ProjectColor = project == null ? "#F5A025" : project.Color;
            var panel = new StackPanel { Margin = new Thickness(25) }; Content = panel; panel.Children.Add(StyleKit.Text("项目名称", 12, StyleKit.Muted)); StyleKit.Gap(panel, 8);
            var name = new TextBox { Text = project == null ? "" : project.Name, MaxLength = 40 }; panel.Children.Add(name); StyleKit.Gap(panel, 18);
            panel.Children.Add(StyleKit.Text("选择颜色", 12, StyleKit.Muted)); StyleKit.Gap(panel, 9);
            var palette = new StackPanel { Orientation = Orientation.Horizontal }; panel.Children.Add(palette);
            var colors = new List<Button>();
            foreach (string value in new[] { "#F5A025", "#74AC95", "#699CAF", "#AA92CF", "#D08B91", "#58A6AC" })
            {
                string color = value; var button = new Button { Width = 36, Height = 34, MinHeight = 34, Padding = new Thickness(0), Margin = new Thickness(0, 0, 10, 0), Background = StyleKit.Brush(color), Foreground = Brushes.White, Content = color == ProjectColor ? "✓" : "" };
                button.Click += delegate { ProjectColor = color; foreach (var b in colors) b.Content = b == button ? "✓" : ""; }; colors.Add(button); palette.Children.Add(button);
            }
            StyleKit.Gap(panel, 22); var save = StyleKit.Button("保存项目", delegate { if (String.IsNullOrWhiteSpace(name.Text)) { name.Focus(); return; } ProjectName = name.Text.Trim(); DialogResult = true; }, true); save.IsDefault = true; panel.Children.Add(save);
            Loaded += delegate { name.Focus(); name.SelectAll(); };
        }
    }
}
