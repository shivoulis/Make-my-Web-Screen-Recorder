// On-screen overlays shown while recording. All of them are excluded from screen capture, so they are
// visible to you but never appear in the video.
//  - ControlBar: floating pill with timer, pause, mic mute, discard and stop
//  - Countdown:  3-2-1 before recording starts
//  - RegionFrame: coloured border around the area being recorded

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;

namespace MakeMyWebRecorder {

class ControlBar : Window {
    public event Action PauseClicked, MicClicked, StopClicked, DiscardClicked;

    readonly TextBlock time, state;
    readonly Ellipse dot;
    readonly Button pause, mic;
    readonly Line slash;
    bool lastPaused;
    DateTime discardArmedAt = DateTime.MinValue;

    const string Xaml = @"
<Border xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'
        xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml'
        Margin='12' Background='#F2161920' CornerRadius='16' BorderBrush='#30FFFFFF' BorderThickness='1' Padding='6' Cursor='SizeAll'>
  <Border.Effect><DropShadowEffect BlurRadius='20' ShadowDepth='3' Opacity='0.5'/></Border.Effect>
  <StackPanel Orientation='Horizontal'>
    <Ellipse x:Name='Dot' Width='10' Height='10' Fill='{StaticResource Accent}' Margin='10,0,9,0' VerticalAlignment='Center'/>
    <StackPanel VerticalAlignment='Center' Width='64'>
      <TextBlock x:Name='Time' Text='00:00' Foreground='{StaticResource Text}' FontSize='15' FontWeight='SemiBold'
                 Typography.NumeralAlignment='Tabular'/>
      <TextBlock x:Name='State' Text='Recording' Foreground='{StaticResource Muted}' FontSize='10.5' Margin='0,-2,0,0'/>
    </StackPanel>
    <Border Width='1' Background='#30FFFFFF' Margin='6,6,6,6'/>
    <Button x:Name='Pause' Style='{StaticResource IconButton}' Content='&#xE769;' ToolTip='Pause' AutomationProperties.Name='Pause'/>
    <Grid>
      <Button x:Name='Mic' Style='{StaticResource IconButton}' Content='&#xE720;' ToolTip='Mute microphone' AutomationProperties.Name='Mute microphone'/>
      <Line x:Name='Slash' X1='10' Y1='10' X2='24' Y2='24' Stroke='{StaticResource Accent}' StrokeThickness='2'
            StrokeStartLineCap='Round' StrokeEndLineCap='Round' IsHitTestVisible='False' Visibility='Collapsed'/>
    </Grid>
    <Button x:Name='Discard' Style='{StaticResource IconButton}' Content='&#xE74D;' ToolTip='Discard recording' AutomationProperties.Name='Discard recording'/>
    <Button x:Name='Stop' Width='38' Height='34' Margin='4,0,0,0' Cursor='Hand' ToolTip='Stop and save' AutomationProperties.Name='Stop and save' Focusable='False'>
      <Button.Template>
        <ControlTemplate TargetType='Button'>
          <Border x:Name='b' Background='{StaticResource Accent}' CornerRadius='10'>
            <Border Width='12' Height='12' CornerRadius='2.5' Background='White'/>
          </Border>
          <ControlTemplate.Triggers>
            <Trigger Property='IsMouseOver' Value='True'>
              <Setter TargetName='b' Property='Background' Value='{StaticResource AccentHover}'/>
            </Trigger>
          </ControlTemplate.Triggers>
        </ControlTemplate>
      </Button.Template>
    </Button>
  </StackPanel>
</Border>";

    public ControlBar(bool hasMic) {
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        Topmost = true;
        ShowInTaskbar = false;
        ShowActivated = false;
        ResizeMode = ResizeMode.NoResize;
        SizeToContent = SizeToContent.WidthAndHeight;
        FontFamily = (FontFamily)Application.Current.Resources["UiFont"];

        var root = (FrameworkElement)XamlReader.Parse(Xaml);
        Content = root;
        time = (TextBlock)root.FindName("Time");
        state = (TextBlock)root.FindName("State");
        dot = (Ellipse)root.FindName("Dot");
        pause = (Button)root.FindName("Pause");
        mic = (Button)root.FindName("Mic");
        slash = (Line)root.FindName("Slash");
        if (!hasMic) ((FrameworkElement)mic.Parent).Visibility = Visibility.Collapsed;

        pause.Click += delegate { if (PauseClicked != null) PauseClicked(); };
        mic.Click += delegate { if (MicClicked != null) MicClicked(); };
        ((Button)root.FindName("Stop")).Click += delegate { if (StopClicked != null) StopClicked(); };
        // Discard needs two clicks: the first one arms it (red icon), a second within 3 seconds confirms.
        var discard = (Button)root.FindName("Discard");
        var disarm = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
        disarm.Tick += delegate {
            disarm.Stop();
            discardArmedAt = DateTime.MinValue;
            discard.ClearValue(ForegroundProperty);
            discard.ToolTip = "Discard recording";
        };
        discard.Click += delegate {
            if ((DateTime.Now - discardArmedAt).TotalSeconds < 3) {
                disarm.Stop();
                if (DiscardClicked != null) DiscardClicked();
                return;
            }
            discardArmedAt = DateTime.Now;
            discard.Foreground = (Brush)Application.Current.Resources["Accent"];
            var tip = new ToolTip { Content = "Click again to discard", PlacementTarget = discard };
            discard.ToolTip = tip;
            tip.IsOpen = true;
            disarm.Start();
        };
        root.MouseLeftButtonDown += delegate(object s, MouseButtonEventArgs e) { try { DragMove(); } catch { } };
        SourceInitialized += delegate { Native.ExcludeFromCapture(new WindowInteropHelper(this).Handle); };
        Pulse(true);
    }

    void Pulse(bool on) {
        if (!on) { dot.BeginAnimation(OpacityProperty, null); dot.Opacity = 1; return; }
        var a = new DoubleAnimation(1, 0.25, TimeSpan.FromSeconds(0.8));
        a.AutoReverse = true;
        a.RepeatBehavior = RepeatBehavior.Forever;
        dot.BeginAnimation(OpacityProperty, a);
    }

    // Shows the bar at the top centre of the given monitor (physical pixels).
    public void ShowOn(SrcItem monitor) {
        Show();
        var h = new WindowInteropHelper(this).Handle;
        var r = Native.WindowRect(h);
        if (r != null && monitor != null)
            Native.MoveTo(h, monitor.X + (monitor.W - r[2]) / 2, monitor.Y + (int)(8 * Overlay.Scale));
    }

    public void Update(TimeSpan t, bool paused, bool muted) {
        time.Text = t.TotalHours >= 1 ? t.ToString(@"h\:mm\:ss") : t.ToString(@"mm\:ss");
        if (paused != lastPaused) {
            lastPaused = paused;
            state.Text = paused ? "Paused" : "Recording";
            dot.Fill = (Brush)Application.Current.Resources[paused ? "Amber" : "Accent"];
            Pulse(!paused);
            pause.Content = paused ? "" : "";
            pause.ToolTip = paused ? "Resume" : "Pause";
            System.Windows.Automation.AutomationProperties.SetName(pause, paused ? "Resume" : "Pause");
        }
        slash.Visibility = muted ? Visibility.Visible : Visibility.Collapsed;
        mic.ToolTip = muted ? "Unmute microphone" : "Mute microphone";
        System.Windows.Automation.AutomationProperties.SetName(mic, muted ? "Unmute microphone" : "Mute microphone");
    }
}

static class Overlay {
    public static double Scale = 1;    // physical pixels per DIP

    // Shows 3-2-1 in the middle of the monitor. Clicking it skips the countdown.
    public static async Task Countdown(SrcItem monitor) {
        var w = new Window();
        w.WindowStyle = WindowStyle.None;
        w.AllowsTransparency = true;
        w.Background = Brushes.Transparent;
        w.Topmost = true;
        w.ShowInTaskbar = false;
        w.ShowActivated = false;
        w.ResizeMode = ResizeMode.NoResize;
        w.SizeToContent = SizeToContent.WidthAndHeight;
        var number = new TextBlock {
            Foreground = Brushes.White, FontSize = 72, FontWeight = FontWeights.SemiBold,
            HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center,
            FontFamily = (FontFamily)Application.Current.Resources["UiFont"]
        };
        var label = new TextBlock {
            Text = "Recording starts in", Foreground = new SolidColorBrush(Color.FromArgb(200, 255, 255, 255)), FontSize = 13,
            HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 0, 0, 2),
            FontFamily = (FontFamily)Application.Current.Resources["UiFont"]
        };
        var panel = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        panel.Children.Add(label);
        panel.Children.Add(number);
        var circle = new Border {
            Width = 190, Height = 190, CornerRadius = new CornerRadius(95), Margin = new Thickness(20),
            Background = new SolidColorBrush(Color.FromArgb(225, 22, 25, 32)),
            BorderBrush = (Brush)Application.Current.Resources["Accent"], BorderThickness = new Thickness(3),
            Child = panel, Cursor = Cursors.Hand,
            Effect = new System.Windows.Media.Effects.DropShadowEffect { BlurRadius = 30, ShadowDepth = 0, Opacity = 0.6 }
        };
        w.Content = circle;
        bool skip = false;
        circle.MouseLeftButtonDown += delegate { skip = true; };
        w.SourceInitialized += delegate { Native.ExcludeFromCapture(new WindowInteropHelper(w).Handle); };
        w.Show();
        var h = new WindowInteropHelper(w).Handle;
        var r = Native.WindowRect(h);
        if (r != null && monitor != null) Native.MoveTo(h, monitor.X + (monitor.W - r[2]) / 2, monitor.Y + (monitor.H - r[3]) / 2);
        for (int i = 3; i >= 1 && !skip; i--) {
            number.Text = i.ToString();
            var grow = new DoubleAnimation(0.6, 1, TimeSpan.FromMilliseconds(250));
            number.RenderTransformOrigin = new Point(0.5, 0.5);
            var st = new ScaleTransform();
            number.RenderTransform = st;
            st.BeginAnimation(ScaleTransform.ScaleXProperty, grow);
            st.BeginAnimation(ScaleTransform.ScaleYProperty, grow);
            for (int t = 0; t < 10 && !skip; t++) await Task.Delay(100);
        }
        w.Close();
    }
}

// Thin coloured border around the recorded area, built from four click-through strips.
class RegionFrame {
    readonly List<Strip> strips = new List<Strip>();

    class Strip : System.Windows.Forms.Form {
        public Strip(System.Drawing.Rectangle r) {
            FormBorderStyle = System.Windows.Forms.FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = System.Windows.Forms.FormStartPosition.Manual;
            BackColor = System.Drawing.Color.FromArgb(255, 61, 94);
            Bounds = r;
            Opacity = 0.9;
        }
        protected override bool ShowWithoutActivation { get { return true; } }
        protected override System.Windows.Forms.CreateParams CreateParams {
            get {
                var cp = base.CreateParams;
                cp.ExStyle |= 0x80000 | 0x20 | 0x80 | 0x8000000 | 0x8;   // layered, click-through, tool, no-activate, topmost
                return cp;
            }
        }
        protected override void OnHandleCreated(EventArgs e) {
            base.OnHandleCreated(e);
            Native.ExcludeFromCapture(Handle);
        }
    }

    // r = recorded area (x, y, w, h). For a full screen the border sits just inside the edges, otherwise just outside.
    public RegionFrame(int[] r, bool fullScreen) {
        int t = Math.Max(2, (int)Math.Round(3 * Overlay.Scale));
        int x = r[0], y = r[1], w = r[2], h = r[3];
        if (!fullScreen) { x -= t; y -= t; w += 2 * t; h += 2 * t; }
        foreach (var rect in new[] {
            new System.Drawing.Rectangle(x, y, w, t), new System.Drawing.Rectangle(x, y + h - t, w, t),
            new System.Drawing.Rectangle(x, y, t, h), new System.Drawing.Rectangle(x + w - t, y, t, h) }) {
            var s = new Strip(rect);
            s.Show();
            strips.Add(s);
        }
    }

    public void SetPaused(bool paused) {
        var c = paused ? System.Drawing.Color.FromArgb(245, 184, 61) : System.Drawing.Color.FromArgb(255, 61, 94);
        foreach (var s in strips) s.BackColor = c;
    }

    public void Close() {
        foreach (var s in strips) { s.Close(); s.Dispose(); }
        strips.Clear();
    }
}

}
