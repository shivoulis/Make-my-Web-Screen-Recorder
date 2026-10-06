// A recording session (lossless capture with FFmpeg) and the compression step that turns it into the final MP4.
//
// Pause and microphone mute never touch the running capture: the session just remembers when they
// happened, and the compression step cuts the paused parts out and silences the muted parts. Mic and
// system audio are therefore captured on separate tracks and only mixed when compressing.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace MakeMyWebRecorder {

class RecordOptions {
    public SrcItem Screen;      // monitor to capture from
    public int[] Region;        // capture area relative to that monitor (x, y, w, h); null = whole monitor
    public string Mic;          // dshow device name, or null
    public bool SystemAudio, Cursor;
    public int Fps;
    public string Folder;
}

class Session {
    public string RawFile;
    public bool HasMic, HasSys;
    public int Fps;
    public bool Paused, MicMuted;
    public readonly List<double[]> Pauses = new List<double[]>();
    public readonly List<double[]> Mutes = new List<double[]>();

    Job job;
    LoopbackCapture loop;
    readonly Stopwatch active = new Stopwatch();   // recorded time, excluding pauses
    readonly Stopwatch wall = new Stopwatch();
    double pauseAt, muteAt;

    public TimeSpan Elapsed { get { return active.Elapsed; } }
    public bool Exited { get { return job.Proc.HasExited; } }
    public string ErrorTail() { return job.ErrorTail(); }

    public static Session Start(RecordOptions o, out string error) {
        error = null;
        var s = new Session();
        s.Fps = o.Fps;
        Directory.CreateDirectory(o.Folder);
        s.RawFile = Path.Combine(o.Folder, "Recording " + DateTime.Now.ToString("yyyy-MM-dd HH-mm-ss") + " (uncompressed).mkv");

        string video;
        var scr = o.Screen;
        if (scr.Kind == "desktop") {
            // No Desktop Duplication available: slower GDI capture of the whole desktop.
            var vs = System.Windows.Forms.SystemInformation.VirtualScreen;
            video = "-f gdigrab -thread_queue_size 1024 -framerate " + o.Fps + " -draw_mouse " + (o.Cursor ? 1 : 0) +
                    " -offset_x " + vs.X + " -offset_y " + vs.Y + " -video_size " + (vs.Width - vs.Width % 2) + "x" + (vs.Height - vs.Height % 2) + " -i desktop";
        } else {
            // Desktop Duplication (GPU) capture: keeps full frame rate at high resolutions.
            int[] r = o.Region ?? new[] { 0, 0, scr.W, scr.H };
            string crop = ":offset_x=" + r[0] + ":offset_y=" + r[1] + ":video_size=" + (r[2] - r[2] % 2) + "x" + (r[3] - r[3] % 2);
            video = "-f lavfi -thread_queue_size 1024 -i " +
                    FF.Q("ddagrab=output_idx=" + scr.Idx + ":framerate=" + o.Fps + ":draw_mouse=" + (o.Cursor ? 1 : 0) + crop + ",hwdownload,format=bgra");
        }

        string audio = "", maps = "-map 0:v";
        int input = 1;
        if (o.Mic != null) {
            audio += " -f dshow -thread_queue_size 1024 -rtbufsize 256M -audio_buffer_size 50 -i " + FF.Q("audio=" + o.Mic);
            maps += " -map " + input++ + ":a";
            s.HasMic = true;
        }
        if (o.SystemAudio) {
            s.loop = new LoopbackCapture();
            if (!s.loop.Prepare("mmwsr-" + Guid.NewGuid())) {
                s.loop.Stop();
                error = "Could not capture system audio:\n" + s.loop.Error;
                return null;
            }
            audio += " -f " + s.loop.Format + " -ar " + s.loop.Rate + " -ac " + s.loop.Channels + " -thread_queue_size 1024 -i " + FF.Q(s.loop.PipePath);
            maps += " -map " + input++ + ":a";
            s.HasSys = true;
        }

        string progress = Path.Combine(Path.GetTempPath(), "mmwsr-rec-" + Guid.NewGuid() + ".txt");
        // Lossless: H.264 in RGB with quantizer 0 (bit-exact pixels), FLAC audio, MKV survives crashes.
        string args = "-hide_banner -nostats -loglevel error -stats_period 0.2 -progress " + FF.Q(progress) + " -y " +
                      video + audio + " " + maps + " -c:v libx264rgb -preset ultrafast -qp 0 -c:a flac " + FF.Q(s.RawFile);
        try {
            s.job = FF.Start(args);
        } catch (Exception e) {
            if (s.loop != null) s.loop.Stop();
            error = "Could not start FFmpeg:\n" + e.Message;
            return null;
        }
        s.job.ProgressFile = progress;
        s.active.Start();
        s.wall.Start();
        return s;
    }

    // Current position in the recorded file, in seconds.
    double Position() {
        double p = job.ProgressSeconds();
        return p >= 0 ? p : wall.Elapsed.TotalSeconds;
    }

    public void Pause() {
        if (Paused) return;
        pauseAt = Position();
        Paused = true;
        active.Stop();
    }

    public void Resume() {
        if (!Paused) return;
        Pauses.Add(new[] { pauseAt, Position() });
        Paused = false;
        active.Start();
    }

    public void SetMicMuted(bool muted) {
        if (!HasMic || muted == MicMuted) return;
        if (muted) muteAt = Position(); else Mutes.Add(new[] { muteAt, Position() });
        MicMuted = muted;
    }

    // Stops the capture. Returns true if a usable file was written.
    public bool Stop() {
        double end = Position() + 60;
        if (Paused) { Pauses.Add(new[] { pauseAt, end }); Paused = false; }
        if (MicMuted) { Mutes.Add(new[] { muteAt, end }); MicMuted = false; }
        var p = job.Proc;
        if (!p.HasExited) {
            try { p.StandardInput.Write("q"); p.StandardInput.Flush(); } catch { }
            if (!p.WaitForExit(20000)) try { p.Kill(); } catch { }
        }
        Cleanup();
        return File.Exists(RawFile) && new FileInfo(RawFile).Length > 0 && wall.Elapsed.TotalSeconds > 1;
    }

    public void Discard() {
        try { if (!job.Proc.HasExited) { job.Proc.Kill(); job.Proc.WaitForExit(5000); } } catch { }
        Cleanup();
        try { File.Delete(RawFile); } catch { }
    }

    void Cleanup() {
        active.Stop();
        if (loop != null) loop.Stop();
        try { File.Delete(job.ProgressFile); } catch { }
    }
}

// Builds FFmpeg arguments for compressing/exporting a capture.
static class Encode {
    public const string Best = "best", Small = "small", Lossless = "lossless";

    // audioTracks: number of audio tracks in the source. If micFirst, track 0 is the microphone
    // (mutes apply to it). Pauses are cut out, mutes silenced. Returns the output duration via outDuration.
    public static string Args(string src, string dst, string quality, int audioTracks, bool micFirst,
                              List<double[]> pauses, List<double[]> mutes, int fps, double srcDuration,
                              string progressFile, out double outDuration) {
        pauses = pauses ?? new List<double[]>();
        mutes = mutes ?? new List<double[]>();
        outDuration = srcDuration;
        foreach (var p in pauses) outDuration -= Math.Max(0, Math.Min(p[1], srcDuration) - Math.Min(p[0], srcDuration));

        string head = "-hide_banner -nostats -loglevel error -y -i " + FF.Q(src) + " ";
        string tail = " -progress " + FF.Q(progressFile) + " " + FF.Q(dst);

        if (quality == Lossless)
            return head + "-map 0 -c:v libx264rgb -preset veryslow -qp 0 -c:a flac" + tail;

        string keep = pauses.Count == 0 ? null : "not(" + string.Join("+", pauses.Select(p => Between(p)).ToArray()) + ")";
        var graph = new List<string>();

        // Video: even size, cut pauses, 4:2:0 for universal playback.
        string v = "[0:v]crop=trunc(iw/2)*2:trunc(ih/2)*2";
        if (keep != null) v += ",select='" + keep + "',setpts=N/(" + fps + "*TB)";
        graph.Add(v + ",format=yuv420p[v]");

        // Audio: silence mic mutes, mix mic + system, cut pauses.
        if (audioTracks > 0) {
            string first = "[0:a:0]";
            if (micFirst && mutes.Count > 0)
                first += "volume=0:enable='" + string.Join("+", mutes.Select(m => Between(m)).ToArray()) + "'";
            else
                first += "anull";
            string a;
            if (audioTracks > 1) {
                graph.Add(first + "[a0]");
                a = "[a0][0:a:1]amix=inputs=2:duration=longest:normalize=0";
            } else {
                a = first;
            }
            if (keep != null) a += ",aselect='" + keep + "',asetpts=N/SR/TB";
            graph.Add(a + "[a]");
        }

        string opts = quality == Small
            ? "-c:v libx264 -preset slow -crf 24 -c:a aac -b:a 128k"
            : "-c:v libx264 -preset medium -crf 18 -c:a aac -b:a 192k";
        return head + "-filter_complex " + FF.Q(string.Join(";", graph.ToArray())) + " -map \"[v]\"" +
               (audioTracks > 0 ? " -map \"[a]\"" : "") + " " + opts + " -movflags +faststart" + tail;
    }

    static string Between(double[] range) { return "between(t," + FF.Num(range[0]) + "," + FF.Num(range[1]) + ")"; }
}

}
