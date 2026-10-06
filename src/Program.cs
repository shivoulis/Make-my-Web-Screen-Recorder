// Make my Web Screen Recorder
// Records the screen, a window or a browser tab, plus microphone and/or system audio. Capture is
// lossless; when you stop, it is compressed to a small, visually lossless MP4 (H.264 CRF 18 + AAC).
//
// Copyright (C) 2026 shivoulis. Licensed under the GNU General Public License v3 or later; see LICENSE.
//
// This file: entry point, FFmpeg helpers, settings, Win32 helpers, system-audio capture, click effects.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.IO.Pipes;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

[assembly: AssemblyTitle("Make my Web Screen Recorder")]
[assembly: AssemblyProduct("Make my Web Screen Recorder")]
[assembly: AssemblyDescription("Screen, window and browser-tab recorder")]
[assembly: AssemblyCopyright("Copyright (C) 2026 shivoulis. Licensed under the GNU GPL v3 or later.")]
[assembly: AssemblyVersion("2.4.0.0")]
[assembly: AssemblyFileVersion("2.4.0.0")]
[assembly: TargetFramework(".NETFramework,Version=v4.8", FrameworkDisplayName = ".NET Framework 4.8")]

namespace MakeMyWebRecorder {

static class Program {
    public const string AppName = "Make my Web Screen Recorder";

    [STAThread]
    static void Main() {
        // Work in real pixels so capture coordinates match the screen on scaled (HiDPI) displays.
        try { if (!Native.SetProcessDpiAwarenessContext(new IntPtr(-4))) Native.SetProcessDPIAware(); } catch { }
        System.Windows.Forms.Application.EnableVisualStyles();

        FF.FFmpeg = FF.Locate("ffmpeg.exe");
        FF.FFplay = FF.Locate("ffplay.exe");
        if (FF.FFmpeg == null) {
            System.Windows.MessageBox.Show("FFmpeg was not found. Please reinstall " + AppName + ".", AppName,
                System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            return;
        }

        AppDomain.CurrentDomain.UnhandledException += delegate(object s, UnhandledExceptionEventArgs e) {
            try {
                string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MakeMyWebScreenRecorder");
                Directory.CreateDirectory(dir);
                File.AppendAllText(Path.Combine(dir, "error.log"), DateTime.Now + "\r\n" + e.ExceptionObject + "\r\n\r\n");
            } catch { }
        };

        // Lets "await" in the window code resume on the UI thread even before the message loop starts.
        SynchronizationContext.SetSynchronizationContext(
            new System.Windows.Threading.DispatcherSynchronizationContext(System.Windows.Threading.Dispatcher.CurrentDispatcher));

        var app = new System.Windows.Application();
        app.ShutdownMode = System.Windows.ShutdownMode.OnMainWindowClose;
        app.Resources.MergedDictionaries.Add(Theme.Load());
        app.DispatcherUnhandledException += delegate(object s, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e) {
            try {
                string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MakeMyWebScreenRecorder");
                Directory.CreateDirectory(dir);
                File.AppendAllText(Path.Combine(dir, "error.log"), DateTime.Now + "\r\n" + e.Exception + "\r\n\r\n");
            } catch { }
            System.Windows.MessageBox.Show("Something went wrong:\n\n" + e.Exception.Message, AppName,
                System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            e.Handled = true;
        };
        app.Run(new MainWindow());
    }
}

// ---------------------------------------------------------------------------
// FFmpeg helpers

class Job {
    public Process Proc;
    public Task<string> Out, Err;
    public string File;
    public double Duration;
    public string ProgressFile;
    public string Verb = "Exporting";
    public string DeleteWhenDone;    // lossless source to remove after a successful compress
    public Action Then;              // next step to run after this one succeeds

    public string ErrorTail() {
        var lines = new List<string>();
        foreach (var l in Err.Result.Split('\n')) if (l.Trim().Length > 0) lines.Add(l.Trim());
        return string.Join("\n", lines.GetRange(Math.Max(0, lines.Count - 8), Math.Min(8, lines.Count)).ToArray());
    }

    // Latest output position (seconds) that FFmpeg wrote to its -progress file, or -1.
    public double ProgressSeconds() {
        try {
            using (var fs = new FileStream(ProgressFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete)) {
                fs.Seek(Math.Max(0, fs.Length - 4096), SeekOrigin.Begin);
                string text = new StreamReader(fs).ReadToEnd();
                var m = Regex.Matches(text, @"out_time_us=(\d+)");
                if (m.Count > 0) return double.Parse(m[m.Count - 1].Groups[1].Value, CultureInfo.InvariantCulture) / 1e6;
            }
        } catch { }
        return -1;
    }
}

static class FF {
    public static string FFmpeg, FFplay;

    public static string Locate(string exe) {
        foreach (var d in new[] { Path.Combine(System.Windows.Forms.Application.StartupPath, "ffmpeg"), System.Windows.Forms.Application.StartupPath }) {
            var p = Path.Combine(d, exe);
            if (File.Exists(p)) return p;
        }
        foreach (var d in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(';')) {
            try {
                if (d.Trim().Length == 0) continue;
                var p = Path.Combine(d.Trim().Trim('"'), exe);
                if (File.Exists(p)) return p;
            } catch { }
        }
        return null;
    }

    public static string Q(string s) { return "\"" + s.Replace("\"", "\\\"") + "\""; }

    public static string Num(double v) { return v.ToString("0.###", CultureInfo.InvariantCulture); }

    public static Job Start(string args) { return Start(args, null); }

    // workDir lets filters refer to files by plain name, avoiding FFmpeg's escaping rules for Windows paths.
    public static Job Start(string args, string workDir) {
        var psi = new ProcessStartInfo(FFmpeg, args);
        if (workDir != null) psi.WorkingDirectory = workDir;
        psi.UseShellExecute = false;
        psi.CreateNoWindow = true;
        psi.RedirectStandardInput = true;
        psi.RedirectStandardOutput = true;
        psi.RedirectStandardError = true;
        psi.StandardOutputEncoding = Encoding.UTF8;
        psi.StandardErrorEncoding = Encoding.UTF8;
        var job = new Job();
        job.Proc = Process.Start(psi);
        // Drain both pipes in the background so FFmpeg never blocks on a full buffer.
        job.Out = job.Proc.StandardOutput.ReadToEndAsync();
        job.Err = job.Proc.StandardError.ReadToEndAsync();
        return job;
    }

    public static string Run(string args) {
        var job = Start(args);
        job.Proc.WaitForExit();
        return job.Out.Result + job.Err.Result;
    }

    // DirectShow microphones and cameras.
    public static void Devices(out List<string> mics, out List<string> cams) {
        mics = new List<string>();
        cams = new List<string>();
        foreach (Match m in Regex.Matches(Run("-hide_banner -list_devices true -f dshow -i dummy"), "\"([^\"]+)\" \\((audio|video)\\)"))
            (m.Groups[2].Value == "audio" ? mics : cams).Add(m.Groups[1].Value);
    }

    // When capture starts, the first screen frame is followed by a pause of about a second while the audio
    // devices open, which shows as a frozen start. Returns the time of the first frame after that gap (or 0).
    public static double LeadIn(string file) {
        var times = new List<double>();
        foreach (Match m in Regex.Matches(Run("-hide_banner -t 4 -i " + Q(file) + " -map 0:v:0 -vf showinfo -f null -"), @"pts_time:\s*([\d.]+)"))
            times.Add(double.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture));
        double start = 0;
        for (int i = 1; i < times.Count; i++)
            if (times[i] - times[i - 1] > 0.3) start = times[i];
        return start;
    }

    // Duration in seconds and number of audio streams of a media file.
    public static double Probe(string file, out int audioStreams) {
        string info = Run("-hide_banner -i " + Q(file));
        audioStreams = Regex.Matches(info, @"Stream #0:\d+.*?: Audio:").Count;
        var m = Regex.Match(info, @"Duration: (\d+):(\d+):(\d+(?:\.\d+)?)");
        if (!m.Success) return 0;
        return int.Parse(m.Groups[1].Value) * 3600 + int.Parse(m.Groups[2].Value) * 60
             + double.Parse(m.Groups[3].Value, CultureInfo.InvariantCulture);
    }
}

// ---------------------------------------------------------------------------
// Settings (simple key=value file in %APPDATA%)

class Settings {
    public string Folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyVideos), "Recordings");
    public string Mode = "screen";
    public string Fps = "30";
    public string Mic = "";
    public string Area = "";        // last selected area: x,y,w,h in physical pixels
    public bool MicOn = true, SystemAudio = true, Cursor = true, Clicks = true, Countdown = true, KeepLossless = false, AutoUpdate = false, UpdateAsked = false;
    public bool Camera = false, Subtitles = false;
    public string CameraDevice = "", CameraSize = "M", SubStyle = "track", SubModel = "accurate";
    public string CamBackground = "none", CamScene = "ocean", CamImage = "", CamFilter = "none";

    static string FilePath {
        get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MakeMyWebScreenRecorder", "settings.ini"); }
    }

    public static Settings Load() {
        var s = new Settings();
        try {
            if (!File.Exists(FilePath)) return s;
            foreach (var line in File.ReadAllLines(FilePath, Encoding.UTF8)) {
                int i = line.IndexOf('=');
                if (i < 0) continue;
                string k = line.Substring(0, i), v = line.Substring(i + 1);
                bool b = v == "1";
                switch (k) {
                    case "Folder": s.Folder = v; break;
                    case "Mode": s.Mode = v; break;
                    case "Fps": s.Fps = v; break;
                    case "Mic": s.Mic = v; break;
                    case "Area": s.Area = v; break;
                    case "MicOn": s.MicOn = b; break;
                    case "SystemAudio": s.SystemAudio = b; break;
                    case "Mouse": case "Cursor": s.Cursor = b; break;
                    case "Clicks": s.Clicks = b; break;
                    case "Countdown": s.Countdown = b; break;
                    case "KeepLossless": s.KeepLossless = b; break;
                    case "AutoUpdate": s.AutoUpdate = b; break;
                    case "UpdateAsked": s.UpdateAsked = b; break;
                    case "Camera": s.Camera = b; break;
                    case "CameraDevice": s.CameraDevice = v; break;
                    case "CameraSize": s.CameraSize = v; break;
                    case "Subtitles": s.Subtitles = b; break;
                    case "SubStyle": s.SubStyle = v; break;
                    case "SubModel": s.SubModel = v; break;
                    case "CamBackground": s.CamBackground = v; break;
                    case "CamScene": s.CamScene = v; break;
                    case "CamImage": s.CamImage = v; break;
                    case "CamFilter": s.CamFilter = v; break;
                }
            }
        } catch { }
        return s;
    }

    public void Save() {
        try {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
            File.WriteAllLines(FilePath, new[] {
                "Folder=" + Folder, "Mode=" + Mode, "Fps=" + Fps, "Mic=" + Mic, "Area=" + Area,
                "MicOn=" + B(MicOn), "SystemAudio=" + B(SystemAudio), "Cursor=" + B(Cursor),
                "Clicks=" + B(Clicks), "Countdown=" + B(Countdown), "KeepLossless=" + B(KeepLossless), "AutoUpdate=" + B(AutoUpdate), "UpdateAsked=" + B(UpdateAsked),
                "Camera=" + B(Camera), "CameraDevice=" + CameraDevice, "CameraSize=" + CameraSize,
                "Subtitles=" + B(Subtitles), "SubStyle=" + SubStyle, "SubModel=" + SubModel,
                "CamBackground=" + CamBackground, "CamScene=" + CamScene, "CamImage=" + CamImage, "CamFilter=" + CamFilter
            }, Encoding.UTF8);
        } catch { }
    }

    static string B(bool v) { return v ? "1" : "0"; }
}

// ---------------------------------------------------------------------------
// Capture sources: monitors, windows and browser tabs

class SrcItem {
    public string Kind { get; set; }          // "screen", "window", "tab", "area" or "desktop" (GDI fallback)
    public IntPtr Handle { get; set; }        // window (or browser window for tabs)
    public int Idx { get; set; }              // ddagrab output index for screens
    public int X { get; set; }
    public int Y { get; set; }
    public int W { get; set; }
    public int H { get; set; }
    public string Label { get; set; }
    public string Detail { get; set; }
    public string Proc { get; set; }
    public object Tab { get; set; }           // UI Automation element of a browser tab
    public override string ToString() { return Label; }
}

static class Native {
    delegate bool EnumProc(IntPtr h, IntPtr l);
    [DllImport("user32.dll")] static extern bool EnumWindows(EnumProc cb, IntPtr l);
    [DllImport("user32.dll")] static extern bool EnumChildWindows(IntPtr parent, EnumProc cb, IntPtr l);
    [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr h);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetWindowText(IntPtr h, StringBuilder s, int n);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetClassName(IntPtr h, StringBuilder s, int n);
    [DllImport("user32.dll")] static extern int GetWindowTextLength(IntPtr h);
    [DllImport("user32.dll")] static extern int GetWindowLong(IntPtr h, int i);
    [DllImport("user32.dll")] static extern IntPtr GetWindow(IntPtr h, uint cmd);
    [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr h, out RECT r);
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
    [DllImport("dwmapi.dll", EntryPoint = "DwmGetWindowAttribute")] static extern int DwmRect(IntPtr h, int a, out RECT r, int s);
    [DllImport("dwmapi.dll", EntryPoint = "DwmGetWindowAttribute")] static extern int DwmInt(IntPtr h, int a, out int v, int s);
    [DllImport("user32.dll")] static extern bool IsIconic(IntPtr h);
    [DllImport("user32.dll")] static extern bool ShowWindow(IntPtr h, int c);
    [DllImport("user32.dll")] static extern bool SetForegroundWindow(IntPtr h);
    [DllImport("user32.dll")] public static extern bool IsWindow(IntPtr h);
    [DllImport("user32.dll")] public static extern bool SetProcessDpiAwarenessContext(IntPtr v);
    [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
    [DllImport("user32.dll")] public static extern bool SetWindowDisplayAffinity(IntPtr h, uint affinity);
    [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr h, IntPtr after, int x, int y, int cx, int cy, uint flags);

    struct RECT { public int L, T, R, B; }

    // Hides a window from screen capture (it stays visible on screen) so overlays never end up in the video.
    public static void ExcludeFromCapture(IntPtr h) {
        if (Environment.GetEnvironmentVariable("MMWSR_SHOW_OVERLAYS") == "1") return;   // for screenshots while testing
        try { SetWindowDisplayAffinity(h, 0x11); } catch { }
    }

    public static int[] WindowRect(IntPtr h) {
        RECT r;
        if (!GetWindowRect(h, out r)) return null;
        return new int[] { r.L, r.T, r.R - r.L, r.B - r.T };
    }

    // Moves a window to physical-pixel coordinates without resizing or activating it.
    public static void MoveTo(IntPtr h, int x, int y) { SetWindowPos(h, IntPtr.Zero, x, y, 0, 0, 0x0001 | 0x0004 | 0x0010); }

    static readonly Dictionary<string, string> FriendlyNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) {
        { "chrome", "Google Chrome" }, { "msedge", "Microsoft Edge" }, { "brave", "Brave" }, { "opera", "Opera" },
        { "vivaldi", "Vivaldi" }, { "firefox", "Firefox" }, { "explorer", "File Explorer" }, { "Code", "VS Code" },
    };

    public static string Friendly(string proc) {
        string n;
        return FriendlyNames.TryGetValue(proc, out n) ? n : proc;
    }

    public static List<SrcItem> Windows() {
        var list = new List<SrcItem>();
        uint self = (uint)Process.GetCurrentProcess().Id;
        EnumWindows(delegate(IntPtr h, IntPtr l) {
            if (!IsWindowVisible(h)) return true;
            if (GetWindow(h, 4) != IntPtr.Zero) return true;            // owned popups
            if ((GetWindowLong(h, -20) & 0x80) != 0) return true;        // tool windows
            int cloaked;
            if (DwmInt(h, 14, out cloaked, 4) == 0 && cloaked != 0) return true;
            int len = GetWindowTextLength(h);
            if (len == 0) return true;
            uint pid;
            GetWindowThreadProcessId(h, out pid);
            if (pid == self) return true;
            var sb = new StringBuilder(len + 1);
            GetWindowText(h, sb, sb.Capacity);
            string title = sb.ToString();
            if (title == "Program Manager") return true;
            string proc = "";
            try { proc = Process.GetProcessById((int)pid).ProcessName; } catch { }
            var it = new SrcItem();
            it.Kind = "window"; it.Handle = h; it.Label = title; it.Proc = proc; it.Detail = Friendly(proc);
            list.Add(it);
            return true;
        }, IntPtr.Zero);
        return list;
    }

    public static string ClassName(IntPtr h) {
        var sb = new StringBuilder(128);
        GetClassName(h, sb, sb.Capacity);
        return sb.ToString();
    }

    public static void BringToFront(IntPtr h) {
        if (IsIconic(h)) ShowWindow(h, 9);
        SetForegroundWindow(h);
    }

    public static int[] Bounds(IntPtr h) {
        RECT r;
        if (DwmRect(h, 9, out r, 16) != 0) return WindowRect(h);
        return new int[] { r.L, r.T, r.R - r.L, r.B - r.T };
    }

    // The visible page area of a Chromium browser window (legacy render widget child window), or null.
    public static int[] ChromiumContentRect(IntPtr top) {
        int[] best = null;
        long bestArea = 0;
        EnumChildWindows(top, delegate(IntPtr h, IntPtr l) {
            if (IsWindowVisible(h) && ClassName(h) == "Chrome_RenderWidgetHostHWND") {
                var r = WindowRect(h);
                long area = r == null ? 0 : (long)r[2] * r[3];
                if (area > bestArea) { bestArea = area; best = r; }
            }
            return true;
        }, IntPtr.Zero);
        return best;
    }

    // Monitors in the same order FFmpeg's ddagrab numbers them (outputs of the default GPU).
    // The placeholder methods only keep the COM vtable slots in the right order.
    [ComImport, Guid("7b7166ec-21c7-44ae-b21a-c9ae321ae369"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IDXGIFactory { void P0(); void P1(); void P2(); void P3(); [PreserveSig] int EnumAdapters(uint i, out IDXGIAdapter a); }
    [ComImport, Guid("2411e7e1-12ac-4ccf-bd14-9798e8534dc0"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IDXGIAdapter { void P0(); void P1(); void P2(); void P3(); [PreserveSig] int EnumOutputs(uint i, out IDXGIOutput o); }
    [ComImport, Guid("ae02eedb-c735-4690-8d52-5a8dc20213aa"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IDXGIOutput { void P0(); void P1(); void P2(); void P3(); [PreserveSig] int GetDesc(out OUTPUT_DESC d); }
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    struct OUTPUT_DESC {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string DeviceName;
        public RECT Desktop; public int Attached; public int Rotation; public IntPtr Monitor;
    }
    [DllImport("dxgi.dll")] static extern int CreateDXGIFactory(ref Guid riid, out IDXGIFactory f);

    public static List<SrcItem> Outputs() {
        var list = new List<SrcItem>();
        try {
            Guid iid = typeof(IDXGIFactory).GUID;
            IDXGIFactory f;
            if (CreateDXGIFactory(ref iid, out f) != 0) return list;
            IDXGIAdapter a;
            if (f.EnumAdapters(0, out a) != 0) return list;
            for (uint i = 0; i < 16; i++) {
                IDXGIOutput o;
                if (a.EnumOutputs(i, out o) != 0) break;
                OUTPUT_DESC d;
                o.GetDesc(out d);
                var it = new SrcItem();
                it.Kind = "screen"; it.Idx = (int)i;
                it.X = d.Desktop.L; it.Y = d.Desktop.T; it.W = d.Desktop.R - d.Desktop.L; it.H = d.Desktop.B - d.Desktop.T;
                list.Add(it);
            }
        } catch { }
        return list;
    }
}

// ---------------------------------------------------------------------------
// Captures "what you hear" from the default speakers (WASAPI loopback) and streams the raw
// samples into a named pipe that FFmpeg reads as an audio input.

class LoopbackCapture {
    [ComImport, Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")] class MMDeviceEnumeratorCo { }
    [ComImport, Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IMMDeviceEnumerator { void P0(); [PreserveSig] int GetDefaultAudioEndpoint(int flow, int role, out IMMDevice d); }
    [ComImport, Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IMMDevice { [PreserveSig] int Activate(ref Guid iid, int ctx, IntPtr p, [MarshalAs(UnmanagedType.IUnknown)] out object o); }
    [ComImport, Guid("1CB9AD4C-DBFA-4c32-B178-C2F568A703B2"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IAudioClient {
        [PreserveSig] int Initialize(int share, int flags, long duration, long period, IntPtr format, IntPtr session);
        void P1(); void P2(); void P3(); void P4();
        [PreserveSig] int GetMixFormat(out IntPtr format);
        void P6();
        [PreserveSig] int Start();
        [PreserveSig] int Stop();
        void P9(); void P10();
        [PreserveSig] int GetService(ref Guid iid, [MarshalAs(UnmanagedType.IUnknown)] out object o);
    }
    [ComImport, Guid("C8ADBD64-E71E-48a0-A4DE-185C395CD317"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IAudioCaptureClient {
        [PreserveSig] int GetBuffer(out IntPtr data, out int frames, out int flags, out long pos, out long qpc);
        [PreserveSig] int ReleaseBuffer(int frames);
        [PreserveSig] int GetNextPacketSize(out int frames);
    }

    public string Format;   // FFmpeg raw sample format, e.g. f32le
    public int Rate, Channels;
    public string Error;
    public string PipePath { get { return @"\\.\pipe\" + pipeName; } }

    string pipeName;
    volatile bool stop;
    Thread thread;
    ManualResetEvent ready = new ManualResetEvent(false);

    public bool Prepare(string name) {
        pipeName = name;
        thread = new Thread(Run);
        thread.IsBackground = true;
        thread.SetApartmentState(ApartmentState.MTA);
        thread.Start();
        ready.WaitOne(5000);
        return Error == null && Format != null;
    }

    public void Stop() {
        stop = true;
        if (thread != null) thread.Join(3000);
    }

    static void Check(int hr, string what) {
        if (hr != 0) throw new Exception(what + " failed (0x" + hr.ToString("X8") + ")");
    }

    void Run() {
        NamedPipeServerStream pipe = null;
        IAudioClient client = null;
        try {
            var en = (IMMDeviceEnumerator)new MMDeviceEnumeratorCo();
            IMMDevice dev;
            Check(en.GetDefaultAudioEndpoint(0, 0, out dev), "Finding the speakers");
            object o;
            Guid iid = typeof(IAudioClient).GUID;
            Check(dev.Activate(ref iid, 23, IntPtr.Zero, out o), "Opening the speakers");
            client = (IAudioClient)o;
            IntPtr fmt;
            Check(client.GetMixFormat(out fmt), "Reading the audio format");
            int tag = (ushort)Marshal.ReadInt16(fmt, 0);
            Channels = Marshal.ReadInt16(fmt, 2);
            Rate = Marshal.ReadInt32(fmt, 4);
            int block = Marshal.ReadInt16(fmt, 12);
            int bits = Marshal.ReadInt16(fmt, 14);
            bool isFloat = tag == 3;
            if (tag == 0xFFFE) {
                byte[] g = new byte[16];
                Marshal.Copy(new IntPtr(fmt.ToInt64() + 24), g, 0, 16);
                isFloat = new Guid(g) == new Guid("00000003-0000-0010-8000-00aa00389b71");
            }
            Format = isFloat ? (bits == 64 ? "f64le" : "f32le") : (bits == 16 ? "s16le" : bits == 24 ? "s24le" : "s32le");
            Check(client.Initialize(0, 0x20000, 10000000, 0, fmt, IntPtr.Zero), "Starting loopback capture");
            Marshal.FreeCoTaskMem(fmt);
            Guid ciid = typeof(IAudioCaptureClient).GUID;
            Check(client.GetService(ref ciid, out o), "Opening the capture stream");
            var cap = (IAudioCaptureClient)o;

            pipe = new NamedPipeServerStream(pipeName, PipeDirection.Out, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous, 0, 1 << 20);
            ready.Set();
            var ar = pipe.BeginWaitForConnection(null, null);
            while (!ar.IsCompleted) { if (stop) return; Thread.Sleep(10); }
            pipe.EndWaitForConnection(ar);

            Check(client.Start(), "Starting the capture");
            var clock = Stopwatch.StartNew();
            long written = 0;
            byte[] buf = new byte[0];
            byte[] zeros = new byte[block * (Rate / 10)];
            while (!stop) {
                int frames;
                bool got = false;
                while (cap.GetNextPacketSize(out frames) == 0 && frames > 0) {
                    IntPtr data; int n, flags; long p1, p2;
                    if (cap.GetBuffer(out data, out n, out flags, out p1, out p2) != 0) break;
                    int bytes = n * block;
                    if (buf.Length < bytes) buf = new byte[bytes];
                    if ((flags & 2) != 0) Array.Clear(buf, 0, bytes); else Marshal.Copy(data, buf, 0, bytes);
                    cap.ReleaseBuffer(n);
                    pipe.Write(buf, 0, bytes);
                    written += n;
                    got = true;
                }
                // Loopback delivers nothing while nothing is playing, so fill those gaps with silence
                // to keep the audio in step with the video.
                long behind = (long)(clock.Elapsed.TotalSeconds * Rate) - written;
                if (!got && behind > Rate / 20) {
                    int n = (int)Math.Min(behind, zeros.Length / block);
                    pipe.Write(zeros, 0, n * block);
                    written += n;
                }
                Thread.Sleep(5);
            }
        } catch (Exception e) {
            if (Error == null && !stop) Error = e.Message;
        } finally {
            ready.Set();
            try { if (client != null) client.Stop(); } catch { }
            try { if (pipe != null) pipe.Dispose(); } catch { }
        }
    }
}

// ---------------------------------------------------------------------------
// Draws a short expanding ring on screen wherever the mouse is clicked, so clicks show up in the video.

static class ClickEffects {
    delegate IntPtr HookProc(int code, IntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll")] static extern IntPtr SetWindowsHookEx(int id, HookProc proc, IntPtr mod, uint thread);
    [DllImport("user32.dll")] static extern bool UnhookWindowsHookEx(IntPtr h);
    [DllImport("user32.dll")] static extern IntPtr CallNextHookEx(IntPtr h, int code, IntPtr wParam, IntPtr lParam);
    [DllImport("kernel32.dll")] static extern IntPtr GetModuleHandle(string name);

    static Thread thread;
    static Form host;
    static HookProc proc;
    static double scale = 1;

    public static void Start(double dpiScale) {
        if (thread != null) return;
        scale = dpiScale;
        var started = new ManualResetEvent(false);
        thread = new Thread(delegate() {
            host = new Form();
            var unused = host.Handle;
            proc = Hook;
            IntPtr hook = SetWindowsHookEx(14, proc, GetModuleHandle(null), 0);
            started.Set();
            System.Windows.Forms.Application.Run();
            UnhookWindowsHookEx(hook);
            host.Dispose();
        });
        thread.IsBackground = true;
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        started.WaitOne(3000);
    }

    public static void Stop() {
        if (thread == null) return;
        try { host.BeginInvoke(new MethodInvoker(System.Windows.Forms.Application.ExitThread)); } catch { }
        thread.Join(2000);
        thread = null;
    }

    static IntPtr Hook(int code, IntPtr wParam, IntPtr lParam) {
        if (code >= 0) {
            int msg = wParam.ToInt32();
            Color c = Color.Empty;
            if (msg == 0x201) c = Color.Gold;                 // left button
            else if (msg == 0x204) c = Color.DeepSkyBlue;     // right button
            else if (msg == 0x207) c = Color.LimeGreen;       // middle button
            if (c != Color.Empty) {
                var pt = new Point(Marshal.ReadInt32(lParam, 0), Marshal.ReadInt32(lParam, 4));
                host.BeginInvoke(new MethodInvoker(delegate { new Ripple(pt, c, scale).Show(); }));
            }
        }
        return CallNextHookEx(IntPtr.Zero, code, wParam, lParam);
    }

    class Ripple : Form {
        const int Steps = 22;
        int age;
        Color color;
        float pen;
        System.Windows.Forms.Timer timer;

        public Ripple(Point p, Color c, double scale) {
            color = c;
            pen = (float)(4 * scale);
            int size = (int)(70 * scale);
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            BackColor = Color.Magenta;
            TransparencyKey = Color.Magenta;
            Bounds = new Rectangle(p.X - size / 2, p.Y - size / 2, size, size);
            DoubleBuffered = true;
            Opacity = 0.9;
            timer = new System.Windows.Forms.Timer();
            timer.Interval = 16;
            timer.Tick += delegate {
                age++;
                if (age >= Steps) { timer.Stop(); timer.Dispose(); Close(); return; }
                Opacity = 0.9 * (1 - (double)age / Steps);
                Invalidate();
            };
            timer.Start();
        }

        protected override bool ShowWithoutActivation { get { return true; } }

        protected override CreateParams CreateParams {
            get {
                var cp = base.CreateParams;
                // layered, click-through, no taskbar/alt-tab, never takes focus, always on top
                cp.ExStyle |= 0x80000 | 0x20 | 0x80 | 0x8000000 | 0x8;
                return cp;
            }
        }

        protected override void OnPaint(PaintEventArgs e) {
            float t = (float)age / Steps;
            float r = (Width / 2f - pen) * (0.35f + 0.65f * t);
            float cx = Width / 2f, cy = Height / 2f;
            using (var p = new Pen(color, pen))
                e.Graphics.DrawEllipse(p, cx - r, cy - r, r * 2, r * 2);
        }
    }
}

}
