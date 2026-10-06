// Automatic subtitles: speech-to-text with Whisper (whisper.cpp, built into FFmpeg). The language is
// detected automatically. Models are downloaded once from Hugging Face on first use.

using System;
using System.IO;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace MakeMyWebRecorder {

static class Subtitles {
    public const string Fast = "fast", Accurate = "accurate";
    public const string Track = "track", Burn = "burn";

    const string VadFile = "ggml-silero-v5.1.2.bin";   // voice detection: stops Whisper inventing text in silences
    const string VadUrl = "https://huggingface.co/ggml-org/whisper-vad/resolve/main/" + VadFile;
    const string ModelUrl = "https://huggingface.co/ggerganov/whisper.cpp/resolve/main/";

    public static string ModelsDir {
        get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MakeMyWebScreenRecorder", "models"); }
    }

    public static string ModelFile(string quality) { return quality == Fast ? "ggml-base.bin" : "ggml-large-v3-turbo-q5_0.bin"; }
    public static int ModelMB(string quality) { return quality == Fast ? 142 : 547; }

    public static bool HasModel(string quality) {
        return File.Exists(Path.Combine(ModelsDir, ModelFile(quality))) && File.Exists(Path.Combine(ModelsDir, VadFile));
    }

    // Downloads the speech model (and the small voice-detection model), reporting progress 0-100.
    public static async Task Download(string quality, Action<int> progress) {
        ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
        Directory.CreateDirectory(ModelsDir);
        foreach (var item in new[] { new[] { VadFile, VadUrl }, new[] { ModelFile(quality), ModelUrl + ModelFile(quality) } }) {
            string dst = Path.Combine(ModelsDir, item[0]);
            if (File.Exists(dst)) continue;
            string part = dst + ".part";
            using (var wc = new WebClient()) {
                wc.Headers[HttpRequestHeader.UserAgent] = "MakeMyWebScreenRecorder";
                if (item[0] != VadFile)
                    wc.DownloadProgressChanged += delegate(object s, DownloadProgressChangedEventArgs e) { progress(e.ProgressPercentage); };
                await wc.DownloadFileTaskAsync(new Uri(item[1]), part);
            }
            if (File.Exists(dst)) File.Delete(dst);
            File.Move(part, dst);
        }
    }

    // FFmpeg's filter syntax needs ':' escaped and forward slashes in paths.
    static string FilterPath(string path) { return path.Replace('\\', '/').Replace(":", "\\:"); }

    // Transcribes the audio of 'video' into 'srtName' (a file name in the video's folder; run with that folder as working directory).
    public static string TranscribeArgs(string video, string srtName, string quality, string progressFile) {
        string model = FilterPath(Path.Combine(ModelsDir, ModelFile(quality)));
        string vad = FilterPath(Path.Combine(ModelsDir, VadFile));
        return "-hide_banner -nostats -loglevel error -y -i " + FF.Q(video) + " -vn -af \"aresample=16000,whisper=model='" + model +
               "':language=auto:queue=20:vad_model='" + vad + "':destination=" + srtName + ":format=srt\" -progress " + FF.Q(progressFile) + " -f null -";
    }

    // Adds the subtitles as a track viewers can switch on/off (no re-encoding). Replaces any existing subtitle track.
    public static string EmbedArgs(string video, string srt, string output, string progressFile) {
        return "-hide_banner -nostats -loglevel error -y -i " + FF.Q(video) + " -i " + FF.Q(srt) +
               " -map 0:v -map 0:a? -map 1 -c copy -c:s mov_text -metadata:s:s:0 title=Subtitles -movflags +faststart -progress " + FF.Q(progressFile) + " " + FF.Q(output);
    }

    // Draws the subtitles onto the picture (re-encodes the video). srtName is relative to the working directory.
    public static string BurnArgs(string video, string srtName, string output, string progressFile) {
        const string style = "FontName=Segoe UI,FontSize=20,PrimaryColour=&H00FFFFFF,OutlineColour=&H99000000,BorderStyle=3,Outline=6,Shadow=0,MarginV=28";
        return "-hide_banner -nostats -loglevel error -y -i " + FF.Q(video) + " -map 0:v -map 0:a? -vf \"subtitles=" + srtName + ":force_style='" + style +
               "'\" -c:v libx264 -preset medium -crf 18 -c:a copy -movflags +faststart -progress " + FF.Q(progressFile) + " " + FF.Q(output);
    }

    // Renumbers cues from 1 (whisper.cpp starts at 0, which some players reject). Returns false if there is no speech.
    public static bool Tidy(string srt) {
        if (!File.Exists(srt)) return false;
        var blocks = Regex.Split(File.ReadAllText(srt, Encoding.UTF8).Replace("\r\n", "\n").Trim(), @"\n\s*\n");
        var sb = new StringBuilder();
        int n = 0;
        foreach (var b in blocks) {
            var lines = b.Trim().Split('\n');
            if (lines.Length < 3 || !lines[1].Contains("-->")) continue;
            string text = string.Join("\r\n", lines, 2, lines.Length - 2).Trim();
            if (text.Length == 0) continue;
            sb.Append(++n).Append("\r\n").Append(lines[1].Trim()).Append("\r\n").Append(text).Append("\r\n\r\n");
        }
        File.WriteAllText(srt, sb.ToString(), new UTF8Encoding(false));
        return n > 0;
    }
}

}
