// Lists the tabs of open Chromium browsers (Chrome, Edge, Brave, ...) through UI Automation,
// switches to a chosen tab and finds the on-screen area of its page.

using System;
using System.Collections.Generic;
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

}
