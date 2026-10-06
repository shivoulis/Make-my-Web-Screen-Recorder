// Webcam: a round live camera view (CameraView) with optional effects, used in the recording bubble
// (CameraBubble, part of the recording like Loom) and in the effects preview. Frames come from an
// FFmpeg process reading the camera.

using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace MakeMyWebRecorder {

class CameraView : Grid {
    readonly int px;                  // frame size in physical pixels
    readonly WriteableBitmap bitmap;
    readonly TextBlock status;
    readonly TaskCompletionSource<string> firstFrame = new TaskCompletionSource<string>();
    readonly CameraEffects effects;
    Process proc;
    volatile bool stopped;

    // Completes with null when the first frame arrives, or with an error message.
    public Task<string> Ready { get { return firstFrame.Task; } }

    public CameraView(string device, int dip, double scale, CameraEffects fx) {
        effects = fx;
        px = (int)Math.Round(dip * scale);
        px -= px % 2;
        Width = dip;
        Height = dip;
        bitmap = new WriteableBitmap(px, px, 96 * scale, 96 * scale, PixelFormats.Bgr32, null);
        Children.Add(new Ellipse { Fill = new SolidColorBrush(Color.FromRgb(24, 27, 34)) });
        status = new TextBlock {
            Text = "Starting camera...", Foreground = Brushes.White, FontSize = 12, Opacity = 0.8,
            HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center
        };
        Children.Add(status);
        Children.Add(new Ellipse { Fill = new ImageBrush(bitmap) { Stretch = Stretch.UniformToFill } });
        Children.Add(new Ellipse { Stroke = new SolidColorBrush(Color.FromArgb(200, 255, 255, 255)), StrokeThickness = 3 });
        Start(device);
    }

    void Start(string device) {
        // Square crop from the middle, scaled to the view, mirrored like a selfie view.
        string args = "-hide_banner -loglevel error -f dshow -rtbufsize 64M -i " + FF.Q("video=" + device) +
                      " -vf \"crop='min(iw,ih)':'min(iw,ih)',scale=" + px + ":" + px + ",hflip,format=bgr0\" -r 30 -f rawvideo pipe:1";
        var psi = new ProcessStartInfo(FF.FFmpeg, args);
        psi.UseShellExecute = false;
        psi.CreateNoWindow = true;
        psi.RedirectStandardOutput = true;
        psi.RedirectStandardError = true;
        psi.StandardErrorEncoding = Encoding.UTF8;
        try { proc = Process.Start(psi); }
        catch (Exception e) { firstFrame.TrySetResult("Could not start the camera: " + e.Message); return; }
        var err = proc.StandardError.ReadToEndAsync();

        var t = new Thread(delegate() {
            var stream = proc.StandardOutput.BaseStream;
            int size = px * px * 4;
            var buf = new byte[size];
            var processor = new EffectProcessor();
            try {
                while (!stopped) {
                    int read = 0;
                    while (read < size) {
                        int n = stream.Read(buf, read, size - read);
                        if (n <= 0) throw new EndOfStreamException();
                        read += n;
                    }
                    if (effects != null) processor.Apply(buf, px, effects);
                    // Copy into the bitmap on the UI thread; waiting here also paces the reader.
                    Dispatcher.Invoke(new Action(delegate {
                        if (stopped) return;
                        bitmap.WritePixels(new Int32Rect(0, 0, px, px), buf, px * 4, 0);
                        if (status.Visibility == Visibility.Visible) status.Visibility = Visibility.Collapsed;
                    }));
                    firstFrame.TrySetResult(null);
                }
            } catch {
                if (!stopped) {
                    string msg = "";
                    try { proc.WaitForExit(2000); msg = err.Result.Trim(); } catch { }
                    if (msg.IndexOf("I/O error", StringComparison.OrdinalIgnoreCase) >= 0 || msg.IndexOf("Could not run graph", StringComparison.OrdinalIgnoreCase) >= 0)
                        msg = "The camera may be in use by another app.";
                    firstFrame.TrySetResult("Could not start the camera. " + msg);
                    try { Dispatcher.BeginInvoke(new Action(delegate { status.Text = "Camera unavailable"; status.Visibility = Visibility.Visible; })); } catch { }
                }
            }
        });
        t.IsBackground = true;
        t.Start();
    }

    public void Stop() {
        stopped = true;
        try { if (proc != null && !proc.HasExited) proc.Kill(); } catch { }
    }
}

class CameraBubble : Window {
    public static int SizeFor(string size) { return size == "S" ? 170 : size == "L" ? 320 : 230; }

    readonly CameraView view;
    public Task<string> Ready { get { return view.Ready; } }

    public CameraBubble(string device, string size, double scale, CameraEffects fx) {
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        Topmost = true;
        ShowInTaskbar = false;
        ShowActivated = false;
        ResizeMode = ResizeMode.NoResize;
        SizeToContent = SizeToContent.WidthAndHeight;
        Cursor = Cursors.SizeAll;
        ToolTip = "Drag to move";
        view = new CameraView(device, SizeFor(size), scale, fx);
        view.Margin = new Thickness(6);
        view.Effect = new System.Windows.Media.Effects.DropShadowEffect { BlurRadius = 14, ShadowDepth = 2, Opacity = 0.45 };
        Content = view;
        MouseLeftButtonDown += delegate { try { DragMove(); } catch { } };
    }

    // Places the bubble in the bottom-left corner of the given screen area (physical pixels).
    public void ShowInCorner(int[] area, double scale) {
        Show();
        var h = new WindowInteropHelper(this).Handle;
        var r = Native.WindowRect(h);
        if (r == null) return;
        int margin = (int)(18 * scale);
        int x = area[0] + margin, y = area[1] + area[3] - r[3] - margin;
        Native.MoveTo(h, x, Math.Max(area[1], y));
    }

    protected override void OnClosed(EventArgs e) {
        view.Stop();
        base.OnClosed(e);
    }
}

}
