// Lists the tabs of open Chromium browsers (Chrome, Edge, Brave, ...) through UI Automation,
// switches to a chosen tab and finds the on-screen area of its page.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Windows.Automation;

namespace MakeMyWebRecorder {

static class BrowserTabs {
    static readonly string[] Browsers = { "chrome", "msedge", "brave", "opera", "vivaldi" };

    public static List<SrcItem> List() {
        var list = new List<SrcItem>();
        foreach (var w in Native.Windows()) {
            if (Array.IndexOf(Browsers, w.Proc.ToLowerInvariant()) < 0) continue;
            if (Native.ClassName(w.Handle) != "Chrome_WidgetWin_1") continue;
            try {
                var tabs = new List<AutomationElement>();
                FindTabs(AutomationElement.FromHandle(w.Handle), tabs, 0);
                foreach (var t in tabs) {
                    string name = t.Current.Name;
                    if (string.IsNullOrEmpty(name)) continue;
                    var it = new SrcItem();
                    it.Kind = "tab"; it.Handle = w.Handle; it.Proc = w.Proc; it.Tab = t;
                    it.Label = name.Length > 90 ? name.Substring(0, 90) + "..." : name;
                    it.Detail = Native.Friendly(w.Proc);
                    list.Add(it);
                }
            } catch { }
        }
        return list;
    }

    // Walks the browser UI (not the web pages) looking for the tab strip's tabs.
    static void FindTabs(AutomationElement el, List<AutomationElement> found, int depth) {
        if (depth > 18) return;
        var walker = TreeWalker.ControlViewWalker;
        for (var c = walker.GetFirstChild(el); c != null; c = walker.GetNextSibling(c)) {
            var type = c.Current.ControlType;
            if (type == ControlType.Document) continue;
            if (type == ControlType.TabItem) { found.Add(c); continue; }
            FindTabs(c, found, depth + 1);
        }
    }

    // Brings the browser to the front and selects the tab. Returns false if the tab no longer exists.
    public static bool Activate(SrcItem tab) {
        if (!Native.IsWindow(tab.Handle)) return false;
        Native.BringToFront(tab.Handle);
        try {
            var el = (AutomationElement)tab.Tab;
            object p;
            if (el.TryGetCurrentPattern(SelectionItemPattern.Pattern, out p)) ((SelectionItemPattern)p).Select();
            else if (el.TryGetCurrentPattern(InvokePattern.Pattern, out p)) ((InvokePattern)p).Invoke();
            return true;
        } catch {
            return false;
        }
    }

    // Screen rectangle (x, y, w, h in physical pixels) of the page area of the browser's current tab.
    public static int[] PageRect(IntPtr browser) {
        try {
            var doc = AutomationElement.FromHandle(browser).FindFirst(TreeScope.Descendants,
                new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Document));
            if (doc != null) {
                var r = doc.Current.BoundingRectangle;
                if (!r.IsEmpty && r.Width > 50 && r.Height > 50)
                    return new int[] { (int)Math.Round(r.X), (int)Math.Round(r.Y), (int)Math.Round(r.Width), (int)Math.Round(r.Height) };
            }
        } catch { }
        return Native.ChromiumContentRect(browser);
    }
}

// Keeps checking, in the background, whether the chosen tab is still what is shown in the recorded area.
// It is not when another tab is selected, the browser is minimised or closed, or another program's
// window is in front of the page.
class TabWatch {
    readonly SrcItem tab;
    readonly int[] area;           // recorded area on screen (x, y, w, h)
    readonly uint self = (uint)Process.GetCurrentProcess().Id;
    volatile bool visible = true, stopped;

    public bool Visible { get { return visible; } }

    public TabWatch(SrcItem tab, int[] area) {
        this.tab = tab;
        this.area = area;
        var t = new Thread(Loop);
        t.IsBackground = true;
        t.Start();
    }

    public void Stop() { stopped = true; }

    void Loop() {
        while (!stopped) {
            bool v;
            try { v = Check(); } catch { v = false; }
            visible = v;
            Thread.Sleep(150);
        }
    }

    bool Check() {
        var h = tab.Handle;
        if (!Native.IsWindow(h) || Native.IsMinimized(h)) return false;
        if (!Selected()) return false;
        var fg = Native.GetForegroundWindow();
        if (fg == IntPtr.Zero || fg == h || Native.RootOwner(fg) == h) return true;
        if (Native.ProcessId(fg) == self) return true;                 // our control bar, camera bubble, tray menu
        string cls = Native.ClassName(fg);
        if (cls == "Progman" || cls == "WorkerW" || cls == "Shell_TrayWnd" || cls == "Shell_SecondaryTrayWnd") return true;
        var r = Native.WindowRect(fg);
        if (r == null) return true;
        bool overlaps = r[0] < area[0] + area[2] && area[0] < r[0] + r[2] && r[1] < area[1] + area[3] && area[1] < r[1] + r[3];
        return !overlaps;
    }

    bool Selected() {
        var el = (AutomationElement)tab.Tab;
        object p;
        if (el.TryGetCurrentPattern(SelectionItemPattern.Pattern, out p))
            return ((SelectionItemPattern)p).Current.IsSelected;
        // No selection info: the browser's title starts with the selected tab's title.
        string name = el.Current.Name, title = Native.Title(tab.Handle);
        int cut = title.LastIndexOf(" - ");
        string page = cut > 0 ? title.Substring(0, cut) : title;
        return page.Length > 0 && (name.StartsWith(page) || page.StartsWith(name));
    }
}

}
