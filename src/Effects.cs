// Camera effects: background blur / replacement (person segmentation with Google's MediaPipe
// selfie model running on Windows ML) and simple colour filters. Frames are 32-bit BGR squares.

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Windows.AI.MachineLearning;

namespace MakeMyWebRecorder {

// Current effect choice; read by the camera thread on every frame.
class CameraEffects {
    public volatile string Background = "none";   // none | blur | scene
    public volatile string Scene = "ocean";       // ocean | sunset | studio | office | brand | custom
    public volatile string CustomImage = "";
    public volatile string Filter = "none";       // none | warm | cool | mono | bright

    public static readonly string[] Scenes = { "ocean", "sunset", "studio", "office", "brand" };
    public static readonly string[] Filters = { "none", "warm", "cool", "mono", "bright" };
    public bool NeedsModel { get { return Background != "none"; } }
}

static class SegmentationModel {
    // Apache-2.0 model published by Qualcomm AI Hub (ONNX export of Google's MediaPipe selfie segmentation).
    const string Url = "https://qaihub-public-assets.s3.us-west-2.amazonaws.com/qai-hub-models/models/mediapipe_selfie/releases/v0.63.0/mediapipe_selfie-onnx-float.zip";

    public static string ModelsDir {
        get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MakeMyWebScreenRecorder", "models"); }
    }
    public static string FilePath { get { return Path.Combine(ModelsDir, "selfie-segmentation.onnx"); } }

    // Deletes speech-recognition models left by version 2.3 (subtitles were taken out in 2.4).
    public static void RemoveOldSpeechModels() {
        try {
            if (!Directory.Exists(ModelsDir)) return;
            foreach (var pattern in new[] { "ggml-*.bin", "ggml-*.part" })
                foreach (var f in Directory.GetFiles(ModelsDir, pattern)) try { File.Delete(f); } catch { }
        } catch { }
    }
    public static bool Available { get { return File.Exists(FilePath); } }

    public static async Task Download() {
        if (Available) return;
        ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
        Directory.CreateDirectory(ModelsDir);
        byte[] zip;
        using (var wc = new WebClient()) {
            wc.Headers[HttpRequestHeader.UserAgent] = "MakeMyWebScreenRecorder";
            zip = await wc.DownloadDataTaskAsync(new Uri(Url));
        }
        byte[] model = null, data = null;
        using (var archive = new ZipArchive(new MemoryStream(zip))) {
            foreach (var e in archive.Entries) {
                if (e.Name.EndsWith(".onnx")) model = ReadAll(e);
                else if (e.Name.EndsWith(".data")) data = ReadAll(e);
            }
        }
        if (model == null) throw new InvalidDataException("The downloaded background model is incomplete.");
        File.WriteAllBytes(FilePath, OnnxFix.Convert(model, data));
    }

    static byte[] ReadAll(ZipArchiveEntry e) {
        using (var s = e.Open()) using (var m = new MemoryStream()) { s.CopyTo(m); return m.ToArray(); }
    }
}

// Reads the raw buffer of a Windows ML tensor (much faster than enumerating its values one by one).
[System.Runtime.InteropServices.ComImport, System.Runtime.InteropServices.Guid("52f547ef-5b03-49b5-82d6-565f1ee0dd49"),
 System.Runtime.InteropServices.InterfaceType(System.Runtime.InteropServices.ComInterfaceType.InterfaceIsIUnknown)]
interface ITensorNative {
    void GetBuffer(out IntPtr value, out uint capacity);
}

// Applies effects to frames. Use from a single thread.
class EffectProcessor {
    const int N = 256;                     // model input size
    LearningModelSession session;
    bool modelFailed;
    readonly float[] input = new float[3 * N * N];
    readonly float[] raw = new float[N * N];
    readonly float[] mask = new float[N * N];   // smoothed person mask
    bool haveMask;
    byte[] background;                     // scene pixels at the current frame size
    string backgroundKey;
    byte[] small, smallTmp;                // for the blur
    // Lookup tables for the current frame size: source index and 8-bit weight for bilinear sampling.
    int tableSize;
    int[] sx, mx0, mx1, mwx, bx0, bx1, bwx;
    int blurSize;

    public string Error { get; private set; }

    public void Apply(byte[] px, int size, CameraEffects fx) {
        if (fx.Background != "none" && Segment(px, size)) {
            byte[] bg = fx.Background == "blur" ? Blurred(px, size) : Scene(fx, size);
            Composite(px, bg, size);
        }
        if (fx.Filter != "none") ColourFilter(px, fx.Filter);
    }

    void Tables(int size) {
        if (tableSize == size) return;
        tableSize = size;
        sx = new int[N];
        for (int i = 0; i < N; i++) sx[i] = Math.Min(size - 1, i * size / N);
        Bilinear(size, N, out mx0, out mx1, out mwx);
        blurSize = Math.Max(8, size / 8);
        Bilinear(size, blurSize, out bx0, out bx1, out bwx);
    }

    // For each of 'size' output positions: the two nearest of 'n' source positions and the weight of the second (0..256).
    static void Bilinear(int size, int n, out int[] i0, out int[] i1, out int[] w) {
        i0 = new int[size]; i1 = new int[size]; w = new int[size];
        double scale = (double)n / size;
        for (int x = 0; x < size; x++) {
            double f = (x + 0.5) * scale - 0.5;
            int a = Math.Max(0, Math.Min(n - 1, (int)Math.Floor(f)));
            i0[x] = a;
            i1[x] = Math.Min(n - 1, a + 1);
            w[x] = (int)(Math.Max(0, Math.Min(1, f - a)) * 256);
        }
    }

    // Runs the model on the frame and updates the smoothed mask. Returns false if unavailable.
    bool Segment(byte[] px, int size) {
        if (modelFailed) return false;
        if (session == null) {
            if (!SegmentationModel.Available) return false;
            try {
                session = new LearningModelSession(LearningModel.LoadFromFilePath(SegmentationModel.FilePath),
                                                   new LearningModelDevice(LearningModelDeviceKind.Cpu));
            } catch (Exception e) { modelFailed = true; Error = e.Message; return false; }
        }
        Tables(size);
        // Downscale to 256x256 RGB in 0..1, planar (NCHW).
        const float k = 1f / 255f;
        for (int y = 0; y < N; y++) {
            int row = sx[y] * size;
            for (int x = 0; x < N; x++) {
                int s = (row + sx[x]) * 4, i = y * N + x;
                input[i] = px[s + 2] * k;
                input[N * N + i] = px[s + 1] * k;
                input[2 * N * N + i] = px[s] * k;
            }
        }
        try {
            var binding = new LearningModelBinding(session);
            binding.Bind("image", TensorFloat.CreateFromArray(new long[] { 1, 3, N, N }, input));
            var result = session.Evaluate(binding, "fx");
            var tensor = (TensorFloat)result.Outputs["mask"];
            IntPtr buffer; uint capacity;
            ((ITensorNative)(object)tensor).GetBuffer(out buffer, out capacity);
            System.Runtime.InteropServices.Marshal.Copy(buffer, raw, 0, N * N);
        } catch (Exception e) { modelFailed = true; Error = e.Message; return false; }
        // Smooth over time to avoid flicker at the edges.
        if (!haveMask) { Array.Copy(raw, mask, raw.Length); haveMask = true; }
        else for (int i = 0; i < raw.Length; i++) mask[i] = mask[i] * 0.45f + raw[i] * 0.55f;
        return true;
    }

    // Blends the original (person) over bg using the mask, upscaled bilinearly with soft edges.
    void Composite(byte[] px, byte[] bg, int size) {
        for (int y = 0; y < size; y++) {
            int r0 = mx0[y] * N, r1 = mx1[y] * N, wy = mwx[y], iy = 256 - wy;
            for (int x = 0; x < size; x++) {
                int x0 = mx0[x], x1 = mx1[x], wx = mwx[x], ix = 256 - wx;
                float a = ((mask[r0 + x0] * ix + mask[r0 + x1] * wx) * iy + (mask[r1 + x0] * ix + mask[r1 + x1] * wx) * wy) * (1f / 65536f);
                a = (a - 0.25f) * 2f;                       // sharpen the edge a little
                int ai = a <= 0 ? 0 : a >= 1 ? 256 : (int)(a * 256), bi = 256 - ai;
                int i = (y * size + x) * 4;
                px[i] = (byte)((px[i] * ai + bg[i] * bi) >> 8);
                px[i + 1] = (byte)((px[i + 1] * ai + bg[i + 1] * bi) >> 8);
                px[i + 2] = (byte)((px[i + 2] * ai + bg[i + 2] * bi) >> 8);
            }
        }
    }

    // Strong blur: shrink 8x, box-blur twice, scale back up.
    byte[] Blurred(byte[] px, int size) {
        int s = blurSize;
        if (small == null || small.Length != s * s * 4) { small = new byte[s * s * 4]; smallTmp = new byte[s * s * 4]; }
        for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++) {
                int si = ((y * size / s) * size + x * size / s) * 4, di = (y * s + x) * 4;
                small[di] = px[si]; small[di + 1] = px[si + 1]; small[di + 2] = px[si + 2];
            }
        for (int pass = 0; pass < 2; pass++) { BoxBlur(small, smallTmp, s, true); BoxBlur(smallTmp, small, s, false); }
        if (background == null || background.Length != px.Length || backgroundKey != "blur") { background = new byte[px.Length]; backgroundKey = "blur"; }
        var outp = background;
        for (int y = 0; y < size; y++) {
            int r0 = bx0[y] * s, r1 = bx1[y] * s, wy = bwx[y], iy = 256 - wy;
            for (int x = 0; x < size; x++) {
                int a = (r0 + bx0[x]) * 4, b = (r0 + bx1[x]) * 4, c = (r1 + bx0[x]) * 4, d = (r1 + bx1[x]) * 4;
                int wx = bwx[x], ix = 256 - wx, o = (y * size + x) * 4;
                outp[o] = (byte)(((small[a] * ix + small[b] * wx) * iy + (small[c] * ix + small[d] * wx) * wy) >> 16);
                outp[o + 1] = (byte)(((small[a + 1] * ix + small[b + 1] * wx) * iy + (small[c + 1] * ix + small[d + 1] * wx) * wy) >> 16);
                outp[o + 2] = (byte)(((small[a + 2] * ix + small[b + 2] * wx) * iy + (small[c + 2] * ix + small[d + 2] * wx) * wy) >> 16);
            }
        }
        return outp;
    }

    static void BoxBlur(byte[] src, byte[] dst, int s, bool horizontal) {
        const int r = 2;
        for (int a = 0; a < s; a++)
            for (int b = 0; b < s; b++) {
                int sumB = 0, sumG = 0, sumR = 0;
                for (int k = -r; k <= r; k++) {
                    int c = b + k < 0 ? 0 : b + k >= s ? s - 1 : b + k;
                    int i = horizontal ? (a * s + c) * 4 : (c * s + a) * 4;
                    sumB += src[i]; sumG += src[i + 1]; sumR += src[i + 2];
                }
                int o = horizontal ? (a * s + b) * 4 : (b * s + a) * 4;
                dst[o] = (byte)(sumB / 5); dst[o + 1] = (byte)(sumG / 5); dst[o + 2] = (byte)(sumR / 5);
            }
    }

    byte[] Scene(CameraEffects fx, int size) {
        string key = fx.Scene + "|" + fx.CustomImage + "|" + size;
        if (background == null || backgroundKey != key) {
            using (var bmp = SceneImages.Render(fx.Scene, fx.CustomImage, size)) background = Pixels(bmp);
            backgroundKey = key;
        }
        return background;
    }

    static byte[] Pixels(Bitmap bmp) {
        var r = new Rectangle(0, 0, bmp.Width, bmp.Height);
        var data = bmp.LockBits(r, ImageLockMode.ReadOnly, PixelFormat.Format32bppRgb);
        var bytes = new byte[bmp.Width * bmp.Height * 4];
        for (int y = 0; y < bmp.Height; y++)
            System.Runtime.InteropServices.Marshal.Copy(data.Scan0 + y * data.Stride, bytes, y * bmp.Width * 4, bmp.Width * 4);
        bmp.UnlockBits(data);
        return bytes;
    }

    static void ColourFilter(byte[] px, string filter) {
        for (int i = 0; i < px.Length; i += 4) {
            int b = px[i], g = px[i + 1], r = px[i + 2];
            switch (filter) {
                case "warm": r = r * 108 / 100 + 6; b = b * 88 / 100; break;
                case "cool": r = r * 90 / 100; b = b * 110 / 100 + 6; break;
                case "mono": { int l = (r * 299 + g * 587 + b * 114) / 1000; r = g = b = l; break; }
                case "bright": r = r * 112 / 100 + 14; g = g * 112 / 100 + 14; b = b * 112 / 100 + 14; break;
            }
            px[i] = (byte)(b > 255 ? 255 : b); px[i + 1] = (byte)(g > 255 ? 255 : g); px[i + 2] = (byte)(r > 255 ? 255 : r);
        }
    }
}
// Built-in background scenes, drawn in code (no image files or licences needed).
static class SceneImages {
    public static string Title(string scene) {
        switch (scene) {
            case "ocean": return "Ocean";
            case "sunset": return "Sunset";
            case "studio": return "Studio";
            case "office": return "Soft office";
            case "brand": return "Make my Web";
            default: return "Your image";
        }
    }

    public static Bitmap Render(string scene, string customImage, int size) {
        var bmp = new Bitmap(size, size, PixelFormat.Format32bppRgb);
        using (var g = Graphics.FromImage(bmp)) {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            var all = new Rectangle(0, 0, size, size);
            if (scene == "custom" && File.Exists(customImage)) {
                try {
                    using (var img = Image.FromFile(customImage)) {
                        int side = Math.Min(img.Width, img.Height);
                        g.DrawImage(img, all, new Rectangle((img.Width - side) / 2, (img.Height - side) / 2, side, side), GraphicsUnit.Pixel);
                    }
                    return bmp;
                } catch { }
                scene = "studio";
            }
            switch (scene) {
                case "sunset":
                    using (var br = new LinearGradientBrush(all, Color.FromArgb(255, 183, 94), Color.FromArgb(108, 52, 131), 90f)) g.FillRectangle(br, all);
                    using (var sun = new SolidBrush(Color.FromArgb(90, 255, 236, 179))) g.FillEllipse(sun, size * 0.55f, size * 0.12f, size * 0.32f, size * 0.32f);
                    break;
                case "studio": {
                    g.Clear(Color.FromArgb(28, 30, 36));
                    using (var path = new GraphicsPath()) {
                        path.AddEllipse(-size * 0.3f, -size * 0.2f, size * 1.6f, size * 1.4f);
                        using (var br = new PathGradientBrush(path) { CenterColor = Color.FromArgb(92, 98, 110), SurroundColors = new[] { Color.FromArgb(22, 24, 28) } })
                            g.FillRectangle(br, all);
                    }
                    break;
                }
                case "office": {
                    using (var br = new LinearGradientBrush(all, Color.FromArgb(214, 200, 182), Color.FromArgb(150, 134, 118), 90f)) g.FillRectangle(br, all);
                    var rnd = new Random(7);
                    for (int i = 0; i < 26; i++) {
                        float d = size * (0.08f + (float)rnd.NextDouble() * 0.18f);
                        var c = i % 3 == 0 ? Color.FromArgb(70, 255, 236, 200) : i % 3 == 1 ? Color.FromArgb(55, 255, 255, 255) : Color.FromArgb(45, 120, 160, 140);
                        using (var b = new SolidBrush(c)) g.FillEllipse(b, (float)rnd.NextDouble() * size - d / 2, (float)rnd.NextDouble() * size * 0.7f - d / 2, d, d);
                    }
                    using (var shelf = new SolidBrush(Color.FromArgb(60, 70, 52, 40))) g.FillRectangle(shelf, 0, size * 0.62f, size, size * 0.05f);
                    break;
                }
                case "brand": {
                    g.Clear(Color.FromArgb(16, 18, 23));
                    using (var path = new GraphicsPath()) {
                        path.AddEllipse(size * 0.35f, -size * 0.25f, size * 0.9f, size * 0.9f);
                        using (var br = new PathGradientBrush(path) { CenterColor = Color.FromArgb(150, 255, 61, 94), SurroundColors = new[] { Color.FromArgb(0, 255, 61, 94) } })
                            g.FillPath(br, path);
                    }
                    using (var pen = new Pen(Color.FromArgb(40, 255, 255, 255), size / 90f))
                        for (int i = 0; i < 6; i++) g.DrawEllipse(pen, size * (0.55f - i * 0.08f), size * (0.6f - i * 0.05f), size * (0.3f + i * 0.16f), size * (0.3f + i * 0.16f));
                    break;
                }
                default:   // ocean
                    using (var br = new LinearGradientBrush(all, Color.FromArgb(52, 180, 200), Color.FromArgb(18, 44, 92), 90f)) g.FillRectangle(br, all);
                    using (var glow = new SolidBrush(Color.FromArgb(40, 255, 255, 255))) g.FillEllipse(glow, -size * 0.2f, -size * 0.45f, size * 1.4f, size * 0.8f);
                    break;
            }
            // Soften the bokeh scene so it looks like an out-of-focus room.
            if (scene == "office") using (var small = new Bitmap(bmp, Math.Max(8, size / 4), Math.Max(8, size / 4))) g.DrawImage(small, all);
        }
        return bmp;
    }
}

}
