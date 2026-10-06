// Self-update via GitHub Releases: checks the latest release, downloads its installer, verifies it
// against the SHA-256 digest GitHub publishes, and runs it silently (the installer restarts the app).

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Reflection;
using System.Security.Cryptography;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace MakeMyWebRecorder {

class UpdateInfo {
    public Version Version;
    public string PageUrl, DownloadUrl, Sha256, FileName;
    public long Size;
}

static class Updater {
    public const string Repo = "shivoulis/Make-my-Web-Screen-Recorder";

    public static Version Current { get { return Normalize(Assembly.GetExecutingAssembly().GetName().Version); } }

    public static string Pretty(Version v) { return v.Major + "." + v.Minor + "." + v.Build; }

    static Version Normalize(Version v) { return new Version(v.Major, v.Minor, Math.Max(0, v.Build), Math.Max(0, v.Revision)); }

    // Returns the latest release if it is newer than this version, otherwise null. Throws on network errors.
    public static UpdateInfo Check() {
        ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
        var req = (HttpWebRequest)WebRequest.Create("https://api.github.com/repos/" + Repo + "/releases/latest");
        req.UserAgent = "MakeMyWebScreenRecorder/" + Pretty(Current);
        req.Accept = "application/vnd.github+json";
        req.Timeout = 15000;
        string json;
        using (var resp = req.GetResponse())
        using (var sr = new StreamReader(resp.GetResponseStream())) json = sr.ReadToEnd();

        var d = (Dictionary<string, object>)new JavaScriptSerializer().DeserializeObject(json);
        if (true.Equals(d["draft"]) || true.Equals(d["prerelease"])) return null;
        Version v;
        if (!Version.TryParse(((string)d["tag_name"]).TrimStart('v', 'V'), out v)) return null;
        v = Normalize(v);
        if (v <= Current) return null;

        foreach (Dictionary<string, object> a in (object[])d["assets"]) {
            string name = (string)a["name"];
            if (!name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) || name.IndexOf("Setup", StringComparison.OrdinalIgnoreCase) < 0) continue;
            var u = new UpdateInfo();
            u.Version = v;
            u.PageUrl = (string)d["html_url"];
            u.DownloadUrl = (string)a["browser_download_url"];
            u.FileName = name;
            u.Size = Convert.ToInt64(a["size"]);
            object digest;
            if (a.TryGetValue("digest", out digest) && digest is string && ((string)digest).StartsWith("sha256:"))
                u.Sha256 = ((string)digest).Substring(7).ToLowerInvariant();
            return u;
        }
        return null;
    }

    // Downloads the installer to %TEMP%, reporting progress (0-100). Returns the file path.
    public static async Task<string> Download(UpdateInfo u, Action<int> progress) {
        ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
        string path = Path.Combine(Path.GetTempPath(), u.FileName);
        using (var wc = new WebClient()) {
            wc.Headers[HttpRequestHeader.UserAgent] = "MakeMyWebScreenRecorder/" + Pretty(Current);
            wc.DownloadProgressChanged += delegate(object s, DownloadProgressChangedEventArgs e) { progress(e.ProgressPercentage); };
            await wc.DownloadFileTaskAsync(new Uri(u.DownloadUrl), path);
        }
        var info = new FileInfo(path);
        if (u.Size > 0 && info.Length != u.Size) throw new Exception("The download is incomplete. Please try again.");
        if (u.Sha256 != null) {
            string hash;
            using (var sha = SHA256.Create())
            using (var fs = File.OpenRead(path))
                hash = BitConverter.ToString(sha.ComputeHash(fs)).Replace("-", "").ToLowerInvariant();
            if (hash != u.Sha256) {
                try { File.Delete(path); } catch { }
                throw new Exception("The downloaded installer failed its integrity check, so it was not run.");
            }
        }
        return path;
    }

    // Removes installers left in %TEMP% by earlier updates.
    public static void CleanUp() {
        try {
            foreach (var f in Directory.GetFiles(Path.GetTempPath(), "MakeMyWebScreenRecorder-Setup-*.exe"))
                try { File.Delete(f); } catch { }
        } catch { }
    }

    // Runs the installer silently; it closes this app if needed and starts the new version when done.
    public static void Install(string installer) {
        Process.Start(new ProcessStartInfo(installer, "/SILENT /SUPPRESSMSGBOXES /NORESTART") { UseShellExecute = true });
    }
}

}
