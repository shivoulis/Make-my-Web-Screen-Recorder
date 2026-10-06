// Full-screen overlay for choosing the part of the screen to record: shows a dimmed snapshot of the
// screen, you drag a rectangle (kept within one monitor), Esc or right-click cancels.

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace MakeMyWebRecorder {

class AreaPicker : Form {
    const int MinSize = 32;

    readonly Bitmap shot, dimmed;
    readonly Rectangle virt;
    readonly List<Rectangle> monitors = new List<Rectangle>();   // in form coordinates
    readonly float scale;
    readonly Color accent = Color.FromArgb(255, 61, 94);
    readonly Font font;
    Point start;
    Rectangle sel, clampTo;
    bool dragging;

    // Selected area in screen coordinates (physical pixels), valid when ShowDialog returns OK.
    public Rectangle Selected { get; private set; }

    public AreaPicker(IEnumerable<SrcItem> outputs, double dpiScale) {
        scale = (float)dpiScale;
        virt = SystemInformation.VirtualScreen;
        foreach (var o in outputs) monitors.Add(new Rectangle(o.X - virt.X, o.Y - virt.Y, o.W, o.H));
        if (monitors.Count == 0) monitors.Add(new Rectangle(0, 0, virt.Width, virt.Height));

        shot = new Bitmap(virt.Width, virt.Height);
        using (var g = Graphics.FromImage(shot)) g.CopyFromScreen(virt.Location, Point.Empty, virt.Size);
        dimmed = new Bitmap(shot);
        using (var g = Graphics.FromImage(dimmed))
        using (var b = new SolidBrush(Color.FromArgb(150, 8, 10, 14)))
            g.FillRectangle(b, 0, 0, dimmed.Width, dimmed.Height);

        font = new Font("Segoe UI", 10f, FontStyle.Bold);
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        Bounds = virt;
        ShowInTaskbar = false;
        TopMost = true;
        DoubleBuffered = true;
        KeyPreview = true;
        Cursor = Cursors.Cross;
    }

    protected override void OnShown(EventArgs e) {
        base.OnShown(e);
        Bounds = virt;     // re-apply in case Windows adjusted it for the taskbar
        Activate();
    }

    protected override void OnKeyDown(KeyEventArgs e) {
        if (e.KeyCode == Keys.Escape) { DialogResult = DialogResult.Cancel; Close(); }
    }

    Rectangle MonitorAt(Point p) {
        foreach (var m in monitors) if (m.Contains(p)) return m;
        return monitors[0];
    }

    protected override void OnMouseDown(MouseEventArgs e) {
        if (e.Button == MouseButtons.Right) { DialogResult = DialogResult.Cancel; Close(); return; }
        if (e.Button != MouseButtons.Left) return;
        dragging = true;
        clampTo = MonitorAt(e.Location);
        start = e.Location;
        sel = Rectangle.Empty;
    }

    protected override void OnMouseMove(MouseEventArgs e) {
        if (!dragging) { Invalidate(HintRect(MonitorAt(e.Location))); return; }
        int x = Math.Max(clampTo.Left, Math.Min(clampTo.Right, e.X));
        int y = Math.Max(clampTo.Top, Math.Min(clampTo.Bottom, e.Y));
        var old = sel;
        sel = Rectangle.FromLTRB(Math.Min(start.X, x), Math.Min(start.Y, y), Math.Max(start.X, x), Math.Max(start.Y, y));
        // Repaint only the area that changed (plus room for the border and size label).
        var dirty = Rectangle.Union(old, sel);
        dirty.Inflate((int)(140 * scale), (int)(40 * scale));
        Invalidate(dirty);
    }

    protected override void OnMouseUp(MouseEventArgs e) {
        if (e.Button != MouseButtons.Left || !dragging) return;
        dragging = false;
        if (sel.Width >= MinSize && sel.Height >= MinSize) {
            int w = sel.Width - sel.Width % 2, h = sel.Height - sel.Height % 2;
            Selected = new Rectangle(sel.X + virt.X, sel.Y + virt.Y, w, h);
            DialogResult = DialogResult.OK;
            Close();
        } else {
            sel = Rectangle.Empty;
            Invalidate();
        }
    }

    Rectangle HintRect(Rectangle monitor) {
        int w = (int)(430 * scale), h = (int)(40 * scale);
        return new Rectangle(monitor.X + (monitor.Width - w) / 2, monitor.Y + (int)(24 * scale), w, h);
    }

    static GraphicsPath Pill(RectangleF r) {
        var p = new GraphicsPath();
        float d = r.Height;
        p.AddArc(r.X, r.Y, d, d, 90, 180);
        p.AddArc(r.Right - d, r.Y, d, d, 270, 180);
        p.CloseFigure();
        return p;
    }

    void Label(Graphics g, string text, PointF at, bool centred) {
        var size = g.MeasureString(text, font);
        float padX = 12 * scale, padY = 6 * scale;
        var r = new RectangleF(at.X - (centred ? (size.Width + 2 * padX) / 2 : 0), at.Y, size.Width + 2 * padX, size.Height + 2 * padY);
        using (var path = Pill(r))
        using (var b = new SolidBrush(Color.FromArgb(235, 22, 25, 32)))
            g.FillPath(b, path);
        g.DrawString(text, font, Brushes.White, r.X + padX, r.Y + padY);
    }

    protected override void OnPaint(PaintEventArgs e) {
        var g = e.Graphics;
        g.InterpolationMode = InterpolationMode.NearestNeighbor;
        g.DrawImage(dimmed, e.ClipRectangle, e.ClipRectangle, GraphicsUnit.Pixel);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;

        if (sel.Width > 0 && sel.Height > 0) {
            g.DrawImage(shot, sel, sel, GraphicsUnit.Pixel);
            using (var pen = new Pen(accent, Math.Max(2f, 2 * scale)))
                g.DrawRectangle(pen, sel.X, sel.Y, sel.Width - 1, sel.Height - 1);
            string size = (sel.Width - sel.Width % 2) + " × " + (sel.Height - sel.Height % 2);
            float ly = sel.Y - 36 * scale;
            if (ly < clampTo.Top + 4) ly = sel.Y + 8 * scale;
            Label(g, size, new PointF(sel.X, ly), false);
        } else {
            var m = MonitorAt(PointToClient(Cursor.Position));
            var hint = HintRect(m);
            Label(g, "Drag to select the area to record  ·  Esc to cancel", new PointF(hint.X + hint.Width / 2f, hint.Y), true);
        }
    }

    protected override void Dispose(bool disposing) {
        if (disposing) { shot.Dispose(); dimmed.Dispose(); font.Dispose(); }
        base.Dispose(disposing);
    }
}

}
