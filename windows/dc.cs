// dc — minimal desktop control for Windows via SendInput.
// Coordinates are in screenshot pixels: `shot` downscales the primary display so
// Claude sees it 1:1, and records the factor in %TEMP%\dc-scale.txt; dc multiplies by it.
//
// Usage:
//   dc move X Y
//   dc click X Y            (left click)
//   dc dclick X Y           (double click)
//   dc rclick X Y           (right click)
//   dc drag X1 Y1 X2 Y2
//   dc scroll DY [DX]       (lines; negative DY scrolls down)
//   dc type "text"
//   dc key KEY [mods...]    (e.g. dc key s ctrl; dc key return; dc key tab shift)
//   dc pos                  (print current mouse position)
//   dc focus TITLE          (bring the first window whose title contains TITLE to front)

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;

static class Dc
{
    [StructLayout(LayoutKind.Sequential)]
    struct MOUSEINPUT { public int dx, dy; public uint mouseData, dwFlags, time; public IntPtr extra; }
    [StructLayout(LayoutKind.Sequential)]
    struct KEYBDINPUT { public ushort wVk, wScan; public uint dwFlags, time; public IntPtr extra; }
    [StructLayout(LayoutKind.Explicit)]
    struct INPUT
    {
        [FieldOffset(0)] public uint type;
        [FieldOffset(8)] public MOUSEINPUT mi;
        [FieldOffset(8)] public KEYBDINPUT ki;
    }
    [StructLayout(LayoutKind.Sequential)]
    struct POINT { public int x, y; }

    [DllImport("user32.dll")] static extern bool SetProcessDPIAware();
    [DllImport("user32.dll")] static extern uint SendInput(uint n, INPUT[] inputs, int size);
    [DllImport("user32.dll")] static extern int GetSystemMetrics(int i);
    [DllImport("user32.dll")] static extern bool GetCursorPos(out POINT p);
    [DllImport("user32.dll")] static extern bool SetForegroundWindow(IntPtr h);
    [DllImport("user32.dll")] static extern bool ShowWindow(IntPtr h, int cmd);
    [DllImport("user32.dll")] static extern bool IsIconic(IntPtr h);

    const uint INPUT_MOUSE = 0, INPUT_KEYBOARD = 1;
    const uint MOVE = 0x1, LDOWN = 0x2, LUP = 0x4, RDOWN = 0x8, RUP = 0x10, WHEEL = 0x800, HWHEEL = 0x1000, ABSOLUTE = 0x8000;
    const uint KEYUP = 0x2, UNICODE = 0x4;

    static double scale = 1.0;

    static void Die(string msg) { Console.Error.WriteLine(msg); Environment.Exit(1); }

    static void Send(INPUT i)
    {
        SendInput(1, new[] { i }, Marshal.SizeOf(typeof(INPUT)));
        Thread.Sleep(15);
    }

    static void Mouse(uint flags, int dx = 0, int dy = 0, int data = 0)
    {
        var i = new INPUT { type = INPUT_MOUSE };
        i.mi.dx = dx; i.mi.dy = dy; i.mi.mouseData = unchecked((uint)data); i.mi.dwFlags = flags;
        Send(i);
    }

    // Move to a point given in screenshot pixels on the primary display.
    static void MoveTo(double x, double y)
    {
        int w = GetSystemMetrics(0), h = GetSystemMetrics(1);
        double px = x * scale, py = y * scale;
        Mouse(MOVE | ABSOLUTE, (int)Math.Round(px * 65535 / (w - 1)), (int)Math.Round(py * 65535 / (h - 1)));
    }

    static double[] Pt(string[] a, int i)
    {
        double x = 0, y = 0;
        if (a.Length <= i + 1 || !double.TryParse(a[i], NumberStyles.Float, CultureInfo.InvariantCulture, out x)
            || !double.TryParse(a[i + 1], NumberStyles.Float, CultureInfo.InvariantCulture, out y)) Die("expected X Y");
        return new[] { x, y };
    }

    static void Click(double[] p, bool right = false, int count = 1)
    {
        MoveTo(p[0], p[1]);
        for (int n = 0; n < count; n++)
        {
            Mouse(right ? RDOWN : LDOWN);
            Mouse(right ? RUP : LUP);
        }
    }

    static readonly Dictionary<string, ushort> Keys = new Dictionary<string, ushort>
    {
        {"return", 0x0D}, {"enter", 0x0D}, {"tab", 0x09}, {"space", 0x20}, {"backspace", 0x08},
        {"delete", 0x2E}, {"fdelete", 0x2E}, {"esc", 0x1B}, {"escape", 0x1B},
        {"home", 0x24}, {"end", 0x23}, {"pageup", 0x21}, {"pagedown", 0x22},
        {"left", 0x25}, {"up", 0x26}, {"right", 0x27}, {"down", 0x28}, {"insert", 0x2D},
        {"-", 0xBD}, {"=", 0xBB}, {"[", 0xDB}, {"]", 0xDD}, {"\\", 0xDC}, {";", 0xBA},
        {"'", 0xDE}, {",", 0xBC}, {".", 0xBE}, {"/", 0xBF}, {"`", 0xC0},
    };

    static readonly Dictionary<string, ushort> Mods = new Dictionary<string, ushort>
    {
        {"ctrl", 0x11}, {"control", 0x11}, {"cmd", 0x11}, {"command", 0x11},  // cmd maps to Ctrl on Windows
        {"shift", 0x10}, {"alt", 0x12}, {"opt", 0x12}, {"option", 0x12}, {"win", 0x5B},
    };

    static ushort Vk(string name)
    {
        name = name.ToLowerInvariant();
        ushort vk;
        if (Keys.TryGetValue(name, out vk)) return vk;
        if (name.Length == 1 && char.IsLetterOrDigit(name[0])) return (ushort)char.ToUpperInvariant(name[0]);
        int f;
        if (name.StartsWith("f") && int.TryParse(name.Substring(1), out f) && f >= 1 && f <= 24) return (ushort)(0x6F + f);
        Die("unknown key: " + name);
        return 0;
    }

    static void KeyEvent(ushort vk, bool up)
    {
        var i = new INPUT { type = INPUT_KEYBOARD };
        i.ki.wVk = vk; i.ki.dwFlags = up ? KEYUP : 0;
        Send(i);
    }

    static void Key(string name, IEnumerable<string> mods)
    {
        var held = new List<ushort>();
        foreach (var m in mods)
        {
            ushort vk;
            if (!Mods.TryGetValue(m.ToLowerInvariant(), out vk)) Die("unknown modifier: " + m);
            held.Add(vk);
        }
        ushort code = Vk(name);
        foreach (var m in held) KeyEvent(m, false);
        KeyEvent(code, false);
        KeyEvent(code, true);
        for (int n = held.Count - 1; n >= 0; n--) KeyEvent(held[n], true);
    }

    static void Type(string text)
    {
        foreach (char ch in text)
        {
            foreach (bool up in new[] { false, true })
            {
                var i = new INPUT { type = INPUT_KEYBOARD };
                i.ki.wScan = ch; i.ki.dwFlags = UNICODE | (up ? KEYUP : 0);
                Send(i);
            }
        }
    }

    static void Focus(string title)
    {
        foreach (var p in Process.GetProcesses())
        {
            if (p.MainWindowHandle == IntPtr.Zero) continue;
            if (p.MainWindowTitle.IndexOf(title, StringComparison.OrdinalIgnoreCase) < 0) continue;
            if (IsIconic(p.MainWindowHandle)) ShowWindow(p.MainWindowHandle, 9);  // SW_RESTORE
            // Windows only lets the foreground process steal focus; a no-op Alt tap satisfies that.
            KeyEvent(0x12, false); KeyEvent(0x12, true);
            SetForegroundWindow(p.MainWindowHandle);
            Console.WriteLine(p.MainWindowTitle);
            return;
        }
        Die("no window title contains: " + title);
    }

    static void Main(string[] a)
    {
        SetProcessDPIAware();
        try
        {
            var f = Path.Combine(Path.GetTempPath(), "dc-scale.txt");
            if (File.Exists(f)) scale = double.Parse(File.ReadAllText(f).Trim(), CultureInfo.InvariantCulture);
        }
        catch { scale = 1.0; }

        if (a.Length == 0) Die("usage: dc move|click|dclick|rclick|drag|scroll|type|key|pos|focus ...");
        switch (a[0])
        {
            case "move": { var p = Pt(a, 1); MoveTo(p[0], p[1]); break; }
            case "click": Click(Pt(a, 1)); break;
            case "dclick": Click(Pt(a, 1), count: 2); break;
            case "rclick": Click(Pt(a, 1), right: true); break;
            case "drag":
            {
                var p1 = Pt(a, 1); var p2 = Pt(a, 3);
                MoveTo(p1[0], p1[1]);
                Mouse(LDOWN);
                const int steps = 20;
                for (int n = 1; n <= steps; n++)
                {
                    double t = (double)n / steps;
                    MoveTo(p1[0] + (p2[0] - p1[0]) * t, p1[1] + (p2[1] - p1[1]) * t);
                }
                Mouse(LUP);
                break;
            }
            case "scroll":
            {
                int dy, dx = 0;
                if (a.Length < 2 || !int.TryParse(a[1], out dy)) { Die("expected DY"); return; }
                if (a.Length > 2) int.TryParse(a[2], out dx);
                if (dy != 0) Mouse(WHEEL, data: dy * 120);
                if (dx != 0) Mouse(HWHEEL, data: dx * 120);
                break;
            }
            case "type":
                if (a.Length < 2) Die("expected text");
                Type(string.Join(" ", a, 1, a.Length - 1));
                break;
            case "key":
                if (a.Length < 2) Die("expected key");
                Key(a[1], new ArraySegment<string>(a, 2, a.Length - 2));
                break;
            case "pos":
            {
                POINT p; GetCursorPos(out p);
                Console.WriteLine("{0} {1}", (int)Math.Round(p.x / scale), (int)Math.Round(p.y / scale));
                break;
            }
            case "focus":
                if (a.Length < 2) Die("expected window title");
                Focus(string.Join(" ", a, 1, a.Length - 1));
                break;
            default:
                Die("unknown command: " + a[0]);
                break;
        }
    }
}
