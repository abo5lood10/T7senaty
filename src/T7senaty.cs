// T7senaty — مساعد مشاهدة البث على محاكي MuMu Player
//  - أولوية عالية للمحاكي وقت ما يكون شغّال فقط (ترجع طبيعية لما يتقفل البرنامج)
//  - منع ويندوز من إبطاء المحاكي (Efficiency mode) لو كان بالخلفية
//  - إخفاء الماوس بعد ثانية بدون حركة فوق نافذة المحاكي
//  - منع الشاشة من الانطفاء أثناء المشاهدة
//  - F11 داخل المحاكي = شاشة كاملة محسّنة بالذكاء الاصطناعي (Magpie + FSRCNNX)
//  - تطبيق إعدادات المحاكي (الدقة + إخفاء عداد الفريمات) أول ما يتقفل المحاكي
//  - فحص النت (البنق + الثبات + السرعة)
// يُبنى بـ build.ps1 بمترجم C# الموجود أصلاً في ويندوز (.NET Framework 4).

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

[assembly: AssemblyTitle("T7senaty")]
[assembly: AssemblyDescription("MuMu stream helper")]
[assembly: AssemblyProduct("T7senaty")]
[assembly: AssemblyVersion("1.0.0.0")]
[assembly: AssemblyFileVersion("1.0.0.0")]

namespace T7senaty
{
    static class Native
    {
        [StructLayout(LayoutKind.Sequential)]
        public struct POINT { public int X; public int Y; }

        [StructLayout(LayoutKind.Sequential)]
        public struct PROCESS_POWER_THROTTLING_STATE { public uint Version; public uint ControlMask; public uint StateMask; }

        public const uint GA_ROOT = 2;
        public const uint SPI_SETCURSORS = 0x57;
        public const uint ES_CONTINUOUS = 0x80000000;
        public const uint ES_SYSTEM_REQUIRED = 0x1;
        public const uint ES_DISPLAY_REQUIRED = 0x2;
        public const int ProcessPowerThrottling = 4;
        public const uint THROTTLE_EXECUTION_SPEED = 0x1;
        public const uint THROTTLE_IGNORE_TIMER_RESOLUTION = 0x4;
        public const int WM_HOTKEY = 0x0312;
        public const uint MOD_NOREPEAT = 0x4000;
        public const uint VK_F11 = 0x7A;

        [DllImport("user32.dll")] public static extern bool GetCursorPos(out POINT p);
        [DllImport("user32.dll")] public static extern IntPtr WindowFromPoint(POINT p);
        [DllImport("user32.dll")] public static extern IntPtr GetAncestor(IntPtr hwnd, uint flags);
        [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint pid);
        [DllImport("user32.dll")] public static extern short GetAsyncKeyState(int vk);
        [DllImport("user32.dll", SetLastError = true)] public static extern IntPtr CreateCursor(IntPtr hInst, int xHotSpot, int yHotSpot, int nWidth, int nHeight, byte[] andPlane, byte[] xorPlane);
        [DllImport("user32.dll")] public static extern IntPtr CopyIcon(IntPtr hIcon);
        [DllImport("user32.dll")] public static extern bool SetSystemCursor(IntPtr hcur, uint id);
        [DllImport("user32.dll")] public static extern IntPtr LoadCursor(IntPtr hInst, IntPtr id);
        [DllImport("user32.dll")] public static extern bool SystemParametersInfo(uint action, uint param, IntPtr pv, uint winIni);
        [DllImport("user32.dll")] public static extern bool RegisterHotKey(IntPtr hWnd, int id, uint mods, uint vk);
        [DllImport("user32.dll")] public static extern bool UnregisterHotKey(IntPtr hWnd, int id);
        [DllImport("kernel32.dll")] public static extern uint SetThreadExecutionState(uint flags);
        [DllImport("kernel32.dll", SetLastError = true)] public static extern bool SetProcessInformation(IntPtr hProcess, int infoClass, ref PROCESS_POWER_THROTTLING_STATE info, int size);

        [StructLayout(LayoutKind.Sequential)]
        public struct KEYBDINPUT { public ushort wVk; public ushort wScan; public uint dwFlags; public uint time; public IntPtr dwExtraInfo; }

        [StructLayout(LayoutKind.Sequential)]
        public struct MOUSEINPUT { public int dx; public int dy; public uint mouseData; public uint dwFlags; public uint time; public IntPtr dwExtraInfo; }

        [StructLayout(LayoutKind.Explicit)]
        public struct InputUnion { [FieldOffset(0)] public MOUSEINPUT mi; [FieldOffset(0)] public KEYBDINPUT ki; }

        [StructLayout(LayoutKind.Sequential)]
        public struct INPUT { public uint type; public InputUnion u; }

        public const uint INPUT_KEYBOARD = 1;
        public const uint KEYEVENTF_KEYUP = 0x2;

        [DllImport("user32.dll", SetLastError = true)] public static extern uint SendInput(uint count, INPUT[] inputs, int size);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern IntPtr FindWindow(string className, string title);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int GetClassName(IntPtr hwnd, StringBuilder name, int max);
        [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hwnd);
        [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr hwnd);
        [DllImport("user32.dll")] public static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool attach);
        [DllImport("user32.dll")] public static extern IntPtr SetCursor(IntPtr hCursor);
        [StructLayout(LayoutKind.Sequential)]
        public struct CURSORINFO { public int cbSize; public int flags; public IntPtr hCursor; public POINT ptScreenPos; }
        [DllImport("user32.dll")] public static extern bool GetCursorInfo(ref CURSORINFO ci);
        [DllImport("user32.dll", SetLastError = true)] public static extern IntPtr SendMessageTimeout(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam, uint flags, uint timeout, out UIntPtr result);
        [DllImport("kernel32.dll")] public static extern uint GetCurrentThreadId();
        [DllImport("user32.dll")] public static extern bool IsZoomed(IntPtr hwnd);
        [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr hwnd, int cmd);
        [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hwnd, out RECT r);
        [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr hwnd, IntPtr after, int x, int y, int cx, int cy, uint flags);

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT { public int Left; public int Top; public int Right; public int Bottom; }

        public const int SW_RESTORE = 9;
        public const int SW_MAXIMIZE = 3;
        public const uint SWP_NOZORDER = 0x4;
        public const uint SWP_NOACTIVATE = 0x10;
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)] public static extern bool QueryFullProcessImageName(IntPtr hProcess, int flags, StringBuilder name, ref int size);

        public static string ImagePath(Process p)
        {
            try
            {
                var sb = new StringBuilder(1024);
                int n = sb.Capacity;
                return QueryFullProcessImageName(p.Handle, 0, sb, ref n) ? sb.ToString() : null;
            }
            catch { return null; }
        }
    }

    static class Log
    {
        static readonly object gate = new object();

        public static void Write(string msg)
        {
            try
            {
                lock (gate)
                {
                    var fi = new FileInfo(Program.LogPath);
                    if (fi.Exists && fi.Length > 256 * 1024) fi.Delete();
                    File.AppendAllText(Program.LogPath, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "  " + msg + Environment.NewLine, Encoding.UTF8);
                }
            }
            catch { }
        }
    }

    // يخفي الماوس باستبدال مؤشرات النظام بمؤشر شفاف، ويرجعها من إعدادات ويندوز نفسها (ما يغيّر أي شي دائم)
    static class CursorHider
    {
        static readonly uint[] Ids = { 32512, 32513, 32514, 32515, 32516, 32642, 32643, 32644, 32645, 32646, 32648, 32649, 32650, 32651, 32671, 32672 };
        static IntPtr blank = IntPtr.Zero;

        public static bool Hidden { get; private set; }

        // مؤشر المحاكي (Qt) مو مؤشر ويندوز العادي، فنفرّغه داخل نافذته نفسها: نربط إدخال خيطنا بخيطها
        // ثم SetCursor(NULL). يرجع المؤشر تلقائياً لما يتحرك الماوس (النافذة تعيّن مؤشرها من جديد).
        public static void BlankIn(IntPtr hwnd)
        {
            if (hwnd == IntPtr.Zero) return;
            uint pid;
            uint theirs = Native.GetWindowThreadProcessId(hwnd, out pid);
            uint ours = Native.GetCurrentThreadId();
            if (theirs == 0 || theirs == ours) return;
            if (!Native.AttachThreadInput(ours, theirs, true)) return;
            try
            {
                if (blanked == IntPtr.Zero)
                {
                    var ci = new Native.CURSORINFO { cbSize = Marshal.SizeOf(typeof(Native.CURSORINFO)) };
                    if (Native.GetCursorInfo(ref ci) && ci.hCursor != IntPtr.Zero) saved = ci.hCursor;
                }
                Native.SetCursor(IntPtr.Zero);
                blanked = hwnd;
            }
            finally { Native.AttachThreadInput(ours, theirs, false); }
        }

        static IntPtr blanked = IntPtr.Zero;
        static IntPtr saved = IntPtr.Zero;

        // النافذة ما تعيد مؤشرها من نفسها بعد التفريغ: نرجّع المؤشر اللي حفظناه أول ما يتحرك الماوس
        static void Unblank()
        {
            IntPtr h = blanked;
            if (h == IntPtr.Zero) return;
            blanked = IntPtr.Zero;
            uint pid;
            uint theirs = Native.GetWindowThreadProcessId(h, out pid);
            uint ours = Native.GetCurrentThreadId();
            if (theirs != 0 && theirs != ours && Native.AttachThreadInput(ours, theirs, true))
            {
                try { Native.SetCursor(saved != IntPtr.Zero ? saved : Native.LoadCursor(IntPtr.Zero, new IntPtr(32512))); }
                finally { Native.AttachThreadInput(ours, theirs, false); }
            }
            UIntPtr res;
            Native.SendMessageTimeout(h, 0x20, h, (IntPtr)((0x200 << 16) | 1), 2, 100, out res);
        }

        public static void Hide()
        {
            if (Hidden) return;
            if (blank == IntPtr.Zero)
            {
                byte[] andPlane = Enumerable.Repeat((byte)0xFF, 32 * 4).ToArray();
                byte[] xorPlane = new byte[32 * 4];
                blank = Native.CreateCursor(Marshal.GetHINSTANCE(typeof(CursorHider).Module), 0, 0, 32, 32, andPlane, xorPlane);
                if (blank == IntPtr.Zero) return;
            }
            Hidden = true;
            foreach (uint id in Ids) Native.SetSystemCursor(Native.CopyIcon(blank), id);
        }

        public static void Show()
        {
            if (Hidden) Restore();
            Unblank();
        }

        public static void Restore()
        {
            Native.SystemParametersInfo(Native.SPI_SETCURSORS, 0, IntPtr.Zero, 0);
            Hidden = false;
            Unblank();
        }
    }

    static class Power
    {
        // systemDefault=false: لا تخلي ويندوز يبطّئ العملية (EcoQoS) ولا يتجاهل دقة المؤقت لما تكون بالخلفية
        public static bool SetThrottling(IntPtr process, bool systemDefault)
        {
            bool ok = Set(process, Native.THROTTLE_EXECUTION_SPEED, systemDefault);
            Set(process, Native.THROTTLE_IGNORE_TIMER_RESOLUTION, systemDefault); // ويندوز 11 فقط
            return ok;
        }

        static bool Set(IntPtr process, uint mask, bool systemDefault)
        {
            var s = new Native.PROCESS_POWER_THROTTLING_STATE { Version = 1, ControlMask = systemDefault ? 0u : mask, StateMask = 0 };
            try { return Native.SetProcessInformation(process, Native.ProcessPowerThrottling, ref s, Marshal.SizeOf(s)); }
            catch { return false; }
        }
    }

    static class AutoStart
    {
        const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";

        public static bool IsOn
        {
            get
            {
                using (var k = Registry.CurrentUser.OpenSubKey(RunKey))
                    return k != null && k.GetValue("T7senaty") != null;
            }
        }

        public static void Set(bool on)
        {
            using (var k = Registry.CurrentUser.CreateSubKey(RunKey))
            {
                if (on) k.SetValue("T7senaty", "\"" + Application.ExecutablePath + "\"");
                else if (k.GetValue("T7senaty") != null) k.DeleteValue("T7senaty", false);
            }
        }
    }

    class Settings
    {
        readonly string path;
        readonly Dictionary<string, string> values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        readonly List<string> order = new List<string>();

        public Settings(string path)
        {
            this.path = path;
            if (!File.Exists(path)) return;
            foreach (string line in File.ReadAllLines(path, Encoding.UTF8))
            {
                int i = line.IndexOf('=');
                if (i <= 0 || line.TrimStart().StartsWith("#")) continue;
                Set(line.Substring(0, i).Trim(), line.Substring(i + 1).Trim());
            }
        }

        public string Get(string key, string def)
        {
            string s;
            return values.TryGetValue(key, out s) ? s : def;
        }

        public bool Flag(string key, bool def) { return Get(key, def ? "1" : "0") == "1"; }

        public int Int(string key, int def)
        {
            int n;
            return int.TryParse(Get(key, ""), out n) ? n : def;
        }

        public void Set(string key, string value)
        {
            if (!values.ContainsKey(key)) order.Add(key);
            values[key] = value;
        }

        public void SetFlag(string key, bool on) { Set(key, on ? "1" : "0"); }

        public void Save()
        {
            var sb = new StringBuilder();
            sb.AppendLine("# T7senaty settings  (1 = on, 0 = off)");
            foreach (string k in order) sb.AppendLine(k + "=" + values[k]);
            try { File.WriteAllText(path, sb.ToString(), new UTF8Encoding(false)); }
            catch (Exception ex) { Log.Write("ما قدرت أحفظ الإعدادات: " + ex.Message); }
        }
    }

    static class MuMu
    {
        // العمليات اللي فيها المحاكي نفسه (تشغيل أندرويد + الرسم)
        public static readonly string[] DeviceProcs = { "MuMuNxDevice", "MuMuNxHeadless", "MuMuPlayer", "MuMuVMMHeadless" };
        // العمليات اللي تملك نافذة الشاشة
        public static readonly string[] WindowProcs = { "MuMuNxDevice", "MuMuPlayer" };

        public static string Dir = @"C:\Program Files\Netease\MuMuPlayer";
        public static string VmIndex = "0";

        public static string ManagerExe
        {
            get
            {
                foreach (string rel in new[] { @"nx_main\MuMuManager.exe", @"shell\MuMuManager.exe" })
                {
                    string p = Path.Combine(Dir, rel);
                    if (File.Exists(p)) return p;
                }
                return null;
            }
        }

        public static List<string> ShellConfigs()
        {
            var list = new List<string>();
            string vms = Path.Combine(Dir, "vms");
            if (!Directory.Exists(vms)) return list;
            foreach (string vm in Directory.GetDirectories(vms))
            {
                string f = Path.Combine(vm, @"configs\shell_config.json");
                if (File.Exists(f)) list.Add(f);
            }
            return list;
        }

        static readonly Regex ShowFps = new Regex("(\"show_fps\"\\s*:\\s*\\{\\s*\"enabled\"\\s*:\\s*\")(true|false)(\")");
        static readonly Regex FpsLimit = new Regex("(\"fps_limit\"\\s*:\\s*\")([^\"]*)(\")");

        public static string SetShowFps(string json, bool on)
        {
            return ShowFps.Replace(json, m => m.Groups[1].Value + (on ? "true" : "false") + m.Groups[3].Value);
        }

        public static string GetFpsLimit(string json)
        {
            Match m = FpsLimit.Match(json);
            return m.Success ? m.Groups[2].Value : null;
        }

        public static string SetFpsLimit(string json, string value)
        {
            return FpsLimit.Replace(json, m => m.Groups[1].Value + value + m.Groups[3].Value, 1);
        }

        static string ReadText(string path, out bool bom)
        {
            byte[] raw = File.ReadAllBytes(path);
            bom = raw.Length >= 3 && raw[0] == 0xEF && raw[1] == 0xBB && raw[2] == 0xBF;
            return new UTF8Encoding(false).GetString(raw, bom ? 3 : 0, raw.Length - (bom ? 3 : 0));
        }

        public static string Run(string args, int timeoutMs, out int exitCode)
        {
            exitCode = -1;
            string exe = ManagerExe;
            if (exe == null) return "MuMuManager.exe not found";
            var psi = new ProcessStartInfo(exe, args)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8,
                WorkingDirectory = Path.GetDirectoryName(exe)
            };
            using (var p = Process.Start(psi))
            {
                var stdout = p.StandardOutput.ReadToEndAsync();
                var stderr = p.StandardError.ReadToEndAsync();
                if (!p.WaitForExit(timeoutMs))
                {
                    try { p.Kill(); } catch { }
                    return "timeout";
                }
                exitCode = p.ExitCode;
                return stdout.Result + stderr.Result;
            }
        }

        // يطبّق إعدادات الجودة. يُستدعى والمحاكي مقفل عشان ما يقطع البث.
        // MuMuManager يعيد كتابة shell_config من عنده (ويرجّع fps_limit لـ 30)، فنعدّل الملف بعده ونحافظ على حد الفريمات.
        public static bool ApplyQuality(string resolutionMode, out string log)
        {
            var sb = new StringBuilder();
            bool ok = true;
            bool bom;

            var fpsBefore = new Dictionary<string, string>();
            foreach (string f in ShellConfigs())
            {
                try { fpsBefore[f] = GetFpsLimit(ReadText(f, out bom)); }
                catch { }
            }

            // نغيّر بس اللي يختلف (تعيين الدقة حتى لو نفسها يطفّي اختيار الدقة التلقائي في المحاكي)
            int code;
            string current = Run("setting -v " + VmIndex + " -k resolution_mode -k show_frame_rate", 20000, out code);
            var changes = new StringBuilder();
            if (!Regex.IsMatch(current, "\"resolution_mode\"\\s*:\\s*\"" + Regex.Escape(resolutionMode) + "\""))
                changes.Append(" -k resolution_mode -val " + resolutionMode);
            if (!Regex.IsMatch(current, "\"show_frame_rate\"\\s*:\\s*\"false\""))
                changes.Append(" -k show_frame_rate -val false");
            if (changes.Length > 0)
            {
                string set = Run("setting -v all" + changes, 30000, out code);
                sb.AppendLine("MuMuManager set (exit " + code + "): " + set.Trim());
            }
            else sb.AppendLine("MuMuManager: already correct");
            string check = Run("setting -v " + VmIndex + " -k resolution_mode", 20000, out code);
            sb.AppendLine("verify: " + check.Trim());
            if (code != 0 || !check.Contains("\"" + resolutionMode + "\"")) ok = false;

            foreach (string f in ShellConfigs())
            {
                try
                {
                    string text = ReadText(f, out bom);
                    string updated = SetShowFps(text, false);
                    string fps;
                    if (fpsBefore.TryGetValue(f, out fps) && fps != null) updated = SetFpsLimit(updated, fps);
                    if (updated != text)
                    {
                        File.WriteAllText(f, updated, new UTF8Encoding(bom));
                        sb.AppendLine("shell_config fixed (show_fps=false, fps_limit=" + fps + "): " + f);
                    }
                    else sb.AppendLine("shell_config already correct: " + f);
                }
                catch (Exception ex)
                {
                    ok = false;
                    sb.AppendLine("shell_config error: " + ex.Message);
                }
            }

            log = sb.ToString();
            return ok;
        }
    }

    static class NetCheck
    {
        public static string Run()
        {
            var times = new List<long>();
            int sent = 0, lost = 0;
            using (var ping = new Ping())
            {
                for (int i = 0; i < 25; i++)
                {
                    sent++;
                    try
                    {
                        PingReply r = ping.Send("1.1.1.1", 1000);
                        if (r.Status == IPStatus.Success) times.Add(r.RoundtripTime); else lost++;
                    }
                    catch { lost++; }
                    Thread.Sleep(120);
                }
            }

            double mbps = -1;
            try { mbps = DownloadMbps(); } catch { }

            double loss = 100.0 * lost / sent;
            double avg = times.Count > 0 ? times.Average() : -1;
            double jitter = 0;
            for (int i = 1; i < times.Count; i++) jitter += Math.Abs(times[i] - times[i - 1]);
            if (times.Count > 1) jitter /= times.Count - 1;

            var sb = new StringBuilder();
            sb.AppendLine("نتيجة فحص النت:");
            sb.AppendLine();
            if (times.Count > 0)
                sb.AppendLine(string.Format("• البنق: {0:0} ms   (أقل {1} / أعلى {2})", avg, times.Min(), times.Max()));
            else
                sb.AppendLine("• البنق: ما في رد من السيرفر");
            sb.AppendLine(string.Format("• التذبذب (Jitter): {0:0} ms", jitter));
            sb.AppendLine(string.Format("• فقدان البيانات: {0:0}%", loss));
            sb.AppendLine(mbps > 0 ? string.Format("• سرعة التحميل: {0:0} ميجابت/ث", mbps) : "• سرعة التحميل: ما قدرت أقيسها");
            sb.AppendLine();

            bool bad = times.Count == 0 || loss >= 3 || jitter > 30 || avg > 150 || (mbps > 0 && mbps < 8);
            bool meh = !bad && (loss > 0 || jitter > 15 || avg > 80 || (mbps > 0 && mbps < 15));
            if (bad)
            {
                sb.AppendLine("✗ فيه مشكلة بالنت وهي غالباً سبب التقطيع:");
                sb.AppendLine("  - وقّف التحميلات والتحديثات على الأجهزة الثانية بالبيت");
                sb.AppendLine("  - طفّ الراوتر 30 ثانية وشغّله");
                sb.AppendLine("  - أو اختر جودة أقل من داخل البث");
            }
            else if (meh)
            {
                sb.AppendLine("⚠ النت مقبول لكن فيه تذبذب بسيط.");
                sb.AppendLine("لو صار تقطيع جرّب سيرفر ثاني من داخل التطبيق، أو وقّف التحميلات على الأجهزة الثانية.");
            }
            else
            {
                sb.AppendLine("✓ النت ممتاز ويكفي لبث 1080p بسهولة.");
                sb.AppendLine("لو صار تقطيع، فالسبب من سيرفر البث نفسه (زحمة عليه) مو منك —");
                sb.AppendLine("غيّر السيرفر من داخل التطبيق (فيه أكثر من سيرفر لكل مباراة).");
            }
            return sb.ToString();
        }

        static double DownloadMbps()
        {
            ServicePointManager.SecurityProtocol = (SecurityProtocolType)3072; // TLS 1.2
            var req = (HttpWebRequest)WebRequest.Create("https://speed.cloudflare.com/__down?bytes=50000000");
            req.Timeout = 10000;
            req.ReadWriteTimeout = 10000;
            req.UserAgent = "T7senaty";
            using (var resp = req.GetResponse())
            using (var s = resp.GetResponseStream())
            {
                var sw = Stopwatch.StartNew();
                byte[] buf = new byte[81920];
                long total = 0;
                int n;
                while ((n = s.Read(buf, 0, buf.Length)) > 0)
                {
                    total += n;
                    if (sw.ElapsedMilliseconds > 7000) break;
                }
                return total * 8.0 / 1e6 / Math.Max(0.001, sw.Elapsed.TotalSeconds);
            }
        }
    }

    // Magpie (مجاني ومفتوح المصدر، داخل T7senaty\Magpie بوضع محمول): يكبّر نافذة المحاكي لكامل الشاشة
    // بفلتر ذكاء اصطناعي (FSRCNNX). T7senaty يشغّله مع المحاكي ويضغط اختصاره (Alt+Shift+A) عند F11.
    static class MagpieCtl
    {
        public const string ScalingClass = "Window_Magpie_967EB565-6F73-4E94-AE53-00CC42592A22";
        public const string ProfileName = "MuMu (T7senaty)";

        public static string Dir { get { return Path.Combine(Program.AppDir, "Magpie"); } }
        public static string Exe { get { return Path.Combine(Dir, "Magpie.exe"); } }
        public static string ConfigPath { get { return Path.Combine(Dir, @"config\config.json"); } }
        public static bool Installed { get { return File.Exists(Exe) && File.Exists(ConfigPath); } }

        // فقط نسخة Magpie اللي داخل مجلدنا (ما نلمس Magpie ثاني يكون مثبّت عند المستخدم)
        public static List<Process> Procs()
        {
            var list = new List<Process>();
            foreach (Process p in Process.GetProcessesByName("Magpie"))
            {
                string path = Native.ImagePath(p);
                if (path != null && path.StartsWith(Dir, StringComparison.OrdinalIgnoreCase)) list.Add(p);
                else p.Dispose();
            }
            return list;
        }

        public static bool IsRunning
        {
            get
            {
                List<Process> ps = Procs();
                foreach (Process p in ps) p.Dispose();
                return ps.Count > 0;
            }
        }

        public static bool IsScaling { get { return Native.FindWindow(ScalingClass, null) != IntPtr.Zero; } }

        public static void Start()
        {
            if (!Installed || IsRunning) return;
            Process.Start(new ProcessStartInfo(Exe, "-t") { WorkingDirectory = Dir, UseShellExecute = false }).Dispose();
            Log.Write("Magpie اشتغل");
        }

        public static void Stop()
        {
            foreach (Process p in Procs())
            {
                try { p.Kill(); p.WaitForExit(3000); } catch { }
                p.Dispose();
            }
        }

        // Alt+Shift+A: يبدأ/يوقف التكبير للنافذة اللي بالواجهة
        public static bool SendScaleHotkey()
        {
            ushort[] keys = { 0x12, 0x10, 0x41 }; // Alt, Shift, A
            var inputs = new List<Native.INPUT>();
            foreach (ushort k in keys) inputs.Add(Key(k, false));
            for (int i = keys.Length - 1; i >= 0; i--) inputs.Add(Key(keys[i], true));
            Native.INPUT[] arr = inputs.ToArray();
            return Native.SendInput((uint)arr.Length, arr, Marshal.SizeOf(typeof(Native.INPUT))) == arr.Length;
        }

        static Native.INPUT Key(ushort vk, bool up)
        {
            var input = new Native.INPUT { type = Native.INPUT_KEYBOARD };
            input.u.ki.wVk = vk;
            input.u.ki.dwFlags = up ? Native.KEYEVENTF_KEYUP : 0;
            return input;
        }

        static readonly Regex ModeInProfile = new Regex("(\"name\"\\s*:\\s*\"" + Regex.Escape(ProfileName) + "\"[\\s\\S]*?\"scalingMode\"\\s*:\\s*)(-?\\d+)");
        static readonly Regex PathInProfile = new Regex("(\"name\"\\s*:\\s*\"" + Regex.Escape(ProfileName) + "\"[\\s\\S]*?\"pathRule\"\\s*:\\s*\")((?:[^\"\\\\]|\\\\.)*)(\")");

        public static string SetPresetInJson(string json, int mode)
        {
            return ModeInProfile.Replace(json, m => m.Groups[1].Value + mode, 1);
        }

        public static string SetPathInJson(string json, string exePath)
        {
            string escaped = exePath.Replace("\\", "\\\\").Replace("\"", "\\\"");
            return PathInProfile.Replace(json, m => m.Groups[1].Value + escaped + m.Groups[3].Value, 1);
        }

        static readonly Regex ClassInProfile = new Regex("(\"name\"\\s*:\\s*\"" + Regex.Escape(ProfileName) + "\"[\\s\\S]*?\"classNameRule\"\\s*:\\s*\")((?:[^\"\\\\]|\\\\.)*)(\")");

        public static string GetClassInJson(string json)
        {
            Match m = ClassInProfile.Match(json);
            return m.Success ? m.Groups[2].Value : null;
        }

        public static string SetClassInJson(string json, string cls)
        {
            return ClassInProfile.Replace(json, m => m.Groups[1].Value + cls + m.Groups[3].Value, 1);
        }

        public static string GetPathInJson(string json)
        {
            Match m = PathInProfile.Match(json);
            return m.Success ? m.Groups[2].Value.Replace("\\\\", "\\") : null;
        }

        // يعدّل ملف إعدادات Magpie (لازم يكون Magpie مقفل لأنه يحفظ إعداداته من الذاكرة)
        public static bool EditConfig(Func<string, string> edit)
        {
            try
            {
                string json = File.ReadAllText(ConfigPath, Encoding.UTF8);
                string updated = edit(json);
                if (updated == json) return true;
                bool wasRunning = IsRunning;
                if (wasRunning) Stop();
                File.WriteAllText(ConfigPath, updated, new UTF8Encoding(false));
                if (wasRunning) Start();
                return true;
            }
            catch (Exception ex)
            {
                Log.Write("ما قدرت أعدّل إعدادات Magpie: " + ex.Message);
                return false;
            }
        }
    }

    static class Icons
    {
        public static Bitmap Draw(Color bg, int s)
        {
            var bmp = new Bitmap(s, s, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.Clear(Color.Transparent);
                float pad = Math.Max(1f, s * 0.04f), r = s * 0.24f, w = s - 2 * pad;
                using (var path = new GraphicsPath())
                {
                    path.AddArc(pad, pad, 2 * r, 2 * r, 180, 90);
                    path.AddArc(pad + w - 2 * r, pad, 2 * r, 2 * r, 270, 90);
                    path.AddArc(pad + w - 2 * r, pad + w - 2 * r, 2 * r, 2 * r, 0, 90);
                    path.AddArc(pad, pad + w - 2 * r, 2 * r, 2 * r, 90, 90);
                    path.CloseFigure();
                    using (var b = new SolidBrush(bg)) g.FillPath(b, path);
                }
                float cx = s * 0.53f, cy = s * 0.5f, h = s * 0.48f;
                var tri = new[] { new PointF(cx - h * 0.42f, cy - h / 2), new PointF(cx - h * 0.42f, cy + h / 2), new PointF(cx + h * 0.5f, cy) };
                using (var white = new SolidBrush(Color.White)) g.FillPolygon(white, tri);
            }
            return bmp;
        }

        public static Icon Make(Color bg, int s)
        {
            using (Bitmap b = Draw(bg, s)) return Icon.FromHandle(b.GetHicon());
        }

        // ملف .ico (صور PNG بعدة مقاسات) يُستخدم كأيقونة للبرنامج وقت البناء
        public static void WriteIco(string path, Color bg)
        {
            int[] sizes = { 256, 64, 48, 32, 24, 16 };
            var pngs = new List<byte[]>();
            foreach (int s in sizes)
                using (Bitmap b = Draw(bg, s))
                using (var ms = new MemoryStream())
                {
                    b.Save(ms, ImageFormat.Png);
                    pngs.Add(ms.ToArray());
                }
            using (var fs = File.Create(path))
            using (var w = new BinaryWriter(fs))
            {
                w.Write((short)0); w.Write((short)1); w.Write((short)sizes.Length);
                int offset = 6 + 16 * sizes.Length;
                for (int i = 0; i < sizes.Length; i++)
                {
                    byte dim = (byte)(sizes[i] >= 256 ? 0 : sizes[i]);
                    w.Write(dim); w.Write(dim); w.Write((byte)0); w.Write((byte)0);
                    w.Write((short)1); w.Write((short)32);
                    w.Write(pngs[i].Length); w.Write(offset);
                    offset += pngs[i].Length;
                }
                foreach (byte[] p in pngs) w.Write(p);
            }
        }
    }

    class HotkeyWindow : NativeWindow, IDisposable
    {
        public event Action Pressed;

        public HotkeyWindow()
        {
            CreateHandle(new CreateParams { Parent = new IntPtr(-3) }); // HWND_MESSAGE
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == Native.WM_HOTKEY && Pressed != null) Pressed();
            base.WndProc(ref m);
        }

        public void Dispose() { DestroyHandle(); }
    }

    class TrayApp : ApplicationContext
    {
        const int HotkeyId = 1;

        readonly Settings cfg;
        readonly NotifyIcon tray;
        readonly ContextMenuStrip menu;
        readonly ToolStripMenuItem miStatus;
        int sessionStart, goneScans;
        bool sawMuMu;
        volatile bool applyRunning;
        readonly System.Windows.Forms.Timer fast, slow;
        readonly HotkeyWindow hotkeys;
        readonly SynchronizationContext ui;
        readonly Icon iconOn, iconOff;
        readonly int hideDelay;

        static readonly string[] PresetNames =
        {
            "أقصى جودة — CuNNy 8x32 (الأثقل على الكرت)",
            "ذكاء اصطناعي + ألوان أقوى",
            "ذكاء اصطناعي بألوان طبيعية",
            "FSR (أخف وأسرع)"
        };

        HashSet<uint> windowPids = new HashSet<uint>();
        readonly Dictionary<int, ProcessPriorityClass> boosted = new Dictionary<int, ProcessPriorityClass>();
        readonly List<ToolStripMenuItem> presetItems = new List<ToolStripMenuItem>();
        bool running, statusShown, awake, f11Registered, applyTried, cleaned, magpieOurs, magpiePathChecked, magpieClassChecked;
        int fullscreenBusy, netBusy, lastToggleTick;
        IntPtr restoreMaximize;
        Native.POINT lastPos;
        int lastMoveTick;

        public TrayApp()
        {
            cfg = new Settings(Path.Combine(Program.AppDir, "settings.ini"));
            MuMu.Dir = cfg.Get("MuMuDir", MuMu.Dir);
            MuMu.VmIndex = cfg.Get("VmIndex", "0");
            hideDelay = Math.Max(300, cfg.Int("HideDelayMs", 1000));

            if (SynchronizationContext.Current == null)
                SynchronizationContext.SetSynchronizationContext(new WindowsFormsSynchronizationContext());
            ui = SynchronizationContext.Current;

            int iconSize = SystemInformation.SmallIconSize.Width;
            try { using (Icon exeIcon = Icon.ExtractAssociatedIcon(Application.ExecutablePath)) iconOn = iconOff = new Icon(exeIcon, iconSize, iconSize); }
            catch { iconOn = Icons.Make(Color.FromArgb(118, 185, 0), iconSize); iconOff = iconOn; }

            menu = new ContextMenuStrip { RightToLeft = RightToLeft.Yes, ShowCheckMargin = true, ShowImageMargin = false };
            miStatus = new ToolStripMenuItem("…") { Enabled = false };

            var miPresets = new ToolStripMenuItem("نمط تحسين الصورة") { RightToLeft = RightToLeft.Yes };
            int preset = Math.Max(0, Math.Min(PresetNames.Length - 1, cfg.Int("AIPreset", 0)));
            for (int i = 0; i < PresetNames.Length; i++)
            {
                int idx = i;
                var mi = new ToolStripMenuItem(PresetNames[i]) { Checked = i == preset };
                mi.Click += (s, e) => SetPreset(idx);
                presetItems.Add(mi);
            }
            miPresets.DropDownItems.AddRange(presetItems.ToArray());
            miPresets.DropDown.RightToLeft = RightToLeft.Yes;

            menu.Items.AddRange(new ToolStripItem[]
            {
                miStatus,
                new ToolStripSeparator(),
                Toggle("أولوية عالية للمحاكي (وقت تشغيله فقط)", "Priority", on => { if (on) ScanTick(); else UnboostAll(); }),
                Toggle("إخفاء الماوس بعد ثانية بدون حركة", "HideMouse", on => { if (!on) CursorHider.Show(); }),
                Toggle("منع الشاشة من الانطفاء وقت المشاهدة", "KeepAwake", on => ScanTick()),
                Toggle("زر F11 داخل المحاكي = شاشة كاملة", "F11", on => UpdateHotkey()),
                new ToolStripSeparator(),
                Toggle("تحسين الصورة بالذكاء الاصطناعي (Magpie)", "AI", on => ScanTick()),
                miPresets,
                new ToolStripMenuItem("شاشة كاملة الآن (F11)", null, (s, e) => FullscreenAction(true)),
                new ToolStripMenuItem("فحص النت (البنق + السرعة)", null, (s, e) => NetworkTest()),
                new ToolStripSeparator(),
                new ToolStripMenuItem("فتح مجلد البرنامج", null, (s, e) => Process.Start("explorer.exe", "\"" + Program.AppDir + "\"")),
                new ToolStripMenuItem("خروج", null, (s, e) => Quit())
            });

            tray = new NotifyIcon { Icon = iconOff, Text = "T7senaty", ContextMenuStrip = menu, Visible = true };
            tray.MouseClick += (s, e) =>
            {
                if (e.Button != MouseButtons.Left) return;
                MethodInfo show = typeof(NotifyIcon).GetMethod("ShowContextMenu", BindingFlags.Instance | BindingFlags.NonPublic);
                if (show != null) show.Invoke(tray, null);
            };

            hotkeys = new HotkeyWindow();
            hotkeys.Pressed += () => FullscreenAction(false);

            fast = new System.Windows.Forms.Timer { Interval = 80 };
            fast.Tick += (s, e) => MouseTick();
            slow = new System.Windows.Forms.Timer { Interval = 2000 };
            slow.Tick += (s, e) => ScanTick();

            SystemEvents.SessionEnded += (s, e) => Cleanup();
            Application.ApplicationExit += (s, e) => Cleanup();

            // ما يشتغل مع ويندوز أبداً: نشيل أي تشغيل تلقائي قديم
            if (AutoStart.IsOn) AutoStart.Set(false);

            if (cfg.Flag("Priority", true))
                try { Process.GetCurrentProcess().PriorityClass = ProcessPriorityClass.High; } catch { }

            sessionStart = Environment.TickCount;
            Log.Write("T7senaty بدأ (من السكربت)");

            if (Program.LaunchMuMu)
            {
                ThreadPool.QueueUserWorkItem(delegate
                {
                    int code;
                    string o = MuMu.Run("control -v " + MuMu.VmIndex + " launch", 60000, out code);
                    Log.Write("تشغيل المحاكي (exit " + code + "): " + o.Trim());
                    if (code != 0)
                        ui.Post(delegate { Balloon("ما قدرت أشغّل المحاكي", "تأكد من مسار المحاكي في settings.ini (MuMuDir).", ToolTipIcon.Warning); }, null);
                });
            }
            ScanTick();
            slow.Start();
        }

        ToolStripMenuItem Toggle(string text, string key, Action<bool> changed)
        {
            var mi = new ToolStripMenuItem(text) { CheckOnClick = true, Checked = cfg.Flag(key, true) };
            mi.CheckedChanged += (s, e) =>
            {
                cfg.SetFlag(key, mi.Checked);
                cfg.Save();
                changed(mi.Checked);
            };
            return mi;
        }

        // كل ثانيتين: هل المحاكي شغّال؟ فعّل/وقّف حسب حالته
        void ScanTick()
        {
            if (cleaned) return;
            var devices = new List<Process>();
            try
            {
                foreach (string n in MuMu.DeviceProcs) devices.AddRange(Process.GetProcessesByName(n));
                var pids = new HashSet<uint>();
                foreach (string n in MuMu.WindowProcs)
                    foreach (Process p in Process.GetProcessesByName(n)) { pids.Add((uint)p.Id); p.Dispose(); }
                windowPids = pids;

                bool now = devices.Count > 0;
                if (now && cfg.Flag("Priority", true))
                    foreach (Process p in devices) Boost(p);

                var alive = new HashSet<int>(devices.Select(p => p.Id));
                foreach (int pid in boosted.Keys.Where(k => !alive.Contains(k)).ToList()) boosted.Remove(pid);

                SetAwake(now && cfg.Flag("KeepAwake", true));
                ManageMagpie(now, devices);

                if (now != running || !statusShown)
                {
                    running = now;
                    statusShown = true;
                    fast.Enabled = now;
                    if (!now) { CursorHider.Show(); UpdateHotkey(); }
                    else applyTried = false;
                    miStatus.Text = now ? "✓ المحاكي شغّال — T7senaty مفعّل" : "بانتظار تشغيل المحاكي…";
                    tray.Icon = now ? iconOn : iconOff;
                    tray.Text = now ? "T7senaty — مفعّل على المحاكي" : "T7senaty — بانتظار المحاكي";
                    Log.Write(now ? "المحاكي شغّال — تم التفعيل" : "المحاكي مقفل");
                }

                if (!now && !applyTried && cfg.Flag("PendingMuMu", false))
                {
                    applyTried = true;
                    ApplyPendingAsync();
                }

                // الجلسة تنتهي مع المحاكي: لو انقفل (6 ثواني متواصلة) أو ما اشتغل خلال 3 دقايق، يقفل T7senaty ويرجّع كل شي
                if (now) { sawMuMu = true; goneScans = 0; }
                else if (sawMuMu) goneScans++;
                bool noShow = !sawMuMu && unchecked(Environment.TickCount - sessionStart) > 180000;
                if ((sawMuMu && goneScans >= 3 && !applyRunning) || noShow)
                {
                    Log.Write(noShow ? "المحاكي ما اشتغل — إنهاء الجلسة" : "المحاكي انقفل — إنهاء الجلسة");
                    Quit();
                }
            }
            finally
            {
                foreach (Process p in devices) p.Dispose();
            }
        }

        // Magpie يشتغل مع المحاكي فقط، ويتقفل معه
        void ManageMagpie(bool mumuRunning, List<Process> devices)
        {
            if (!(mumuRunning && cfg.Flag("AI", true) && MagpieCtl.Installed))
            {
                magpiePathChecked = false;
                magpieClassChecked = false;
                if (magpieOurs)
                {
                    MagpieCtl.Stop();
                    magpieOurs = false;
                    Log.Write("Magpie وقف");
                }
                return;
            }

            if (!magpiePathChecked)
            {
                // لو تحدّث المحاكي وتغيّر مساره، نحدّث بروفايله في Magpie عشان يتعرّف عليه
                magpiePathChecked = true;
                foreach (Process p in devices.Where(d => MuMu.WindowProcs.Contains(d.ProcessName)))
                {
                    string path = Native.ImagePath(p);
                    if (path == null) continue;
                    string current = null;
                    try { current = MagpieCtl.GetPathInJson(File.ReadAllText(MagpieCtl.ConfigPath, Encoding.UTF8)); } catch { }
                    if (current != null && current != path && MagpieCtl.EditConfig(j => MagpieCtl.SetPathInJson(j, path)))
                        Log.Write("مسار المحاكي في Magpie تحدّث: " + path);
                    break;
                }
            }

            // نفس الشي لاسم صنف نافذة المحاكي (يختلف بين إصدارات MuMu): ننتظر لين تظهر النافذة
            if (!magpieClassChecked)
            {
                IntPtr win = FindMuMuWindow();
                if (win != IntPtr.Zero)
                {
                    var cn = new StringBuilder(128);
                    Native.GetClassName(win, cn, cn.Capacity);
                    string cls = cn.ToString();
                    string curCls = null;
                    try { curCls = MagpieCtl.GetClassInJson(File.ReadAllText(MagpieCtl.ConfigPath, Encoding.UTF8)); } catch { }
                    if (curCls != null && curCls != cls && cls.Length > 0 && MagpieCtl.EditConfig(j => MagpieCtl.SetClassInJson(j, cls)))
                        Log.Write("صنف نافذة المحاكي في Magpie تحدّث: " + cls);
                    magpieClassChecked = true;
                }
            }

            if (!MagpieCtl.IsRunning)
            {
                MagpieCtl.Start();
                magpieOurs = true;
            }

            // Magpie هو اللي يكبّر الصورة على الكرت، فنرفع أولويته (تنتهي بانتهاء الجلسة لأنه يتقفل معها)
            if (cfg.Flag("Priority", true))
            {
                foreach (Process mp in MagpieCtl.Procs())
                {
                    try { if (mp.PriorityClass != ProcessPriorityClass.High) mp.PriorityClass = ProcessPriorityClass.High; }
                    catch { }
                    mp.Dispose();
                }
            }

            // لو انتهى التحسين بطريقة ثانية (مثلاً Alt+Shift+A)، نرجّع تكبير النافذة مثل ما كانت
            if (restoreMaximize != IntPtr.Zero && fullscreenBusy == 0 && unchecked(Environment.TickCount - lastToggleTick) > 3000 && !MagpieCtl.IsScaling)
                ThreadPool.QueueUserWorkItem(delegate { RestoreMaximized(); });
        }

        IntPtr FindMuMuWindow()
        {
            foreach (uint pid in windowPids)
            {
                try
                {
                    using (Process p = Process.GetProcessById((int)pid))
                        if (p.MainWindowHandle != IntPtr.Zero && Native.IsWindowVisible(p.MainWindowHandle)) return p.MainWindowHandle;
                }
                catch { }
            }
            return IntPtr.Zero;
        }

        // F11: شاشة كاملة محسّنة بـ Magpie (أو شاشة MuMu العادية لو التحسين مطفي)
        void FullscreenAction(bool fromMenu)
        {
            if (!(cfg.Flag("AI", true) && MagpieCtl.Installed))
            {
                ToggleFullscreen();
                return;
            }
            IntPtr window = fromMenu ? FindMuMuWindow() : Native.GetAncestor(Native.GetForegroundWindow(), Native.GA_ROOT);
            if (window == IntPtr.Zero || !IsMuMuWindow(window))
            {
                if (fromMenu) Balloon("المحاكي مو شغّال", "شغّل المحاكي أول، وبعدها اضغط F11 داخله.", ToolTipIcon.Info);
                return;
            }
            if (Interlocked.Exchange(ref fullscreenBusy, 1) == 1) return;
            bool starting = !MagpieCtl.IsScaling;
            bool needStart = !MagpieCtl.IsRunning;
            if (needStart)
            {
                MagpieCtl.Start();
                magpieOurs = true;
            }
            ThreadPool.QueueUserWorkItem(delegate
            {
                try
                {
                    if (needStart) Thread.Sleep(2500);
                    if (starting) PrepareNativeSize(window);
                    if (fromMenu || starting)
                    {
                        Native.SetForegroundWindow(window);
                        Thread.Sleep(300);
                    }
                    if (!MagpieCtl.SendScaleHotkey()) Log.Write("SendInput فشل");
                    lastToggleTick = Environment.TickCount;
                    if (!starting) RestoreMaximized();
                }
                finally { Interlocked.Exchange(ref fullscreenBusy, 0); }
            });
        }

        // التحسين يحتاج صورة المحاكي بحجمها الطبيعي (1:1). لو النافذة مكبّرة، المحاكي يمطّ الصورة
        // بنفسه (تغبيش) وما يبقى لـ Magpie شي يكبّره. فنرجّع النافذة لحجمها الطبيعي قبل التحسين.
        void PrepareNativeSize(IntPtr window)
        {
            int w = 1600, h = 900;
            string[] size = cfg.Get("AndroidSize", "1600x900").Split('x');
            if (size.Length == 2) { int.TryParse(size[0], out w); int.TryParse(size[1], out h); }
            int winW = w + 8, winH = h + 44; // حواف نافذة MuMu + شريط التبويبات (مقاسة)

            Screen screen = Screen.FromHandle(window);
            Native.RECT r;
            Native.GetWindowRect(window, out r);
            bool zoomed = Native.IsZoomed(window);
            if (!zoomed && r.Left <= screen.Bounds.Left && r.Top <= screen.Bounds.Top && r.Right >= screen.Bounds.Right && r.Bottom >= screen.Bounds.Bottom)
            {
                // الشاشة الكاملة حقت MuMu نفسه: نطلع منها أول
                int code;
                MuMu.Run("control -v " + MuMu.VmIndex + " tool func -n fullscreen", 15000, out code);
                Thread.Sleep(800);
                zoomed = Native.IsZoomed(window);
            }
            if (zoomed)
            {
                restoreMaximize = window;
                Native.ShowWindow(window, Native.SW_RESTORE);
                Thread.Sleep(250);
            }
            Native.GetWindowRect(window, out r);
            if (r.Right - r.Left == winW && r.Bottom - r.Top == winH) return;
            Rectangle area = screen.WorkingArea;
            int x = area.Left + Math.Max(0, (area.Width - winW) / 2);
            int y = area.Top + Math.Max(0, (area.Height - winH) / 2);
            Native.SetWindowPos(window, IntPtr.Zero, x, y, winW, winH, Native.SWP_NOZORDER);
            Thread.Sleep(400);
            Log.Write("نافذة المحاكي رجعت لحجمها الطبيعي " + w + "x" + h + (zoomed ? " (كانت مكبّرة)" : ""));
        }

        void RestoreMaximized()
        {
            IntPtr w = restoreMaximize;
            if (w == IntPtr.Zero) return;
            restoreMaximize = IntPtr.Zero;
            Thread.Sleep(300);
            Native.ShowWindow(w, Native.SW_MAXIMIZE);
        }

        void SetPreset(int idx)
        {
            for (int i = 0; i < presetItems.Count; i++) presetItems[i].Checked = i == idx;
            cfg.Set("AIPreset", idx.ToString());
            cfg.Save();
            if (!MagpieCtl.Installed) return;
            ThreadPool.QueueUserWorkItem(delegate
            {
                bool ok = MagpieCtl.EditConfig(j => MagpieCtl.SetPresetInJson(j, idx));
                Log.Write("نمط التحسين: " + PresetNames[idx] + (ok ? "" : " (فشل)"));
                ui.Post(delegate
                {
                    if (ok) Balloon("تم تغيير نمط التحسين ✓", PresetNames[idx] + " — اضغط F11 داخل المحاكي.", ToolTipIcon.Info);
                    else Balloon("ما قدرت أغيّر النمط", "التفاصيل في T7senaty.log", ToolTipIcon.Warning);
                }, null);
            });
        }

        void Boost(Process p)
        {
            try
            {
                if (!boosted.ContainsKey(p.Id))
                {
                    boosted[p.Id] = p.PriorityClass;
                    bool eco = Power.SetThrottling(p.Handle, false);
                    Log.Write("أولوية عالية: " + p.ProcessName + " (" + p.Id + ")" + (eco ? " + بدون Efficiency mode" : ""));
                }
                ProcessPriorityClass cur = p.PriorityClass;
                if (cur != ProcessPriorityClass.High && cur != ProcessPriorityClass.RealTime)
                    p.PriorityClass = ProcessPriorityClass.High;
            }
            catch (Exception ex)
            {
                if (!boosted.ContainsKey(p.Id)) boosted[p.Id] = ProcessPriorityClass.Normal;
                Log.Write("ما قدرت أرفع أولوية " + p.ProcessName + ": " + ex.Message);
            }
        }

        void UnboostAll()
        {
            foreach (var kv in boosted)
            {
                try
                {
                    using (Process p = Process.GetProcessById(kv.Key))
                    {
                        p.PriorityClass = kv.Value;
                        Power.SetThrottling(p.Handle, true);
                    }
                }
                catch { }
            }
            if (boosted.Count > 0) Log.Write("الأولوية رجعت طبيعية");
            boosted.Clear();
        }

        void SetAwake(bool on)
        {
            if (on == awake) return;
            awake = on;
            Native.SetThreadExecutionState(on
                ? Native.ES_CONTINUOUS | Native.ES_DISPLAY_REQUIRED | Native.ES_SYSTEM_REQUIRED
                : Native.ES_CONTINUOUS);
        }

        bool IsMuMuWindow(IntPtr hwnd)
        {
            if (hwnd == IntPtr.Zero) return false;
            IntPtr root = Native.GetAncestor(hwnd, Native.GA_ROOT);
            uint pid;
            Native.GetWindowThreadProcessId(root != IntPtr.Zero ? root : hwnd, out pid);
            return windowPids.Contains(pid);
        }

        static bool IsMagpieScaling(IntPtr hwnd)
        {
            if (hwnd == IntPtr.Zero) return false;
            IntPtr root = Native.GetAncestor(hwnd, Native.GA_ROOT);
            var sb = new StringBuilder(128);
            Native.GetClassName(root != IntPtr.Zero ? root : hwnd, sb, sb.Capacity);
            return sb.ToString() == MagpieCtl.ScalingClass;
        }

        void UpdateHotkey()
        {
            bool want = running && !cleaned && cfg.Flag("F11", true) && IsMuMuWindow(Native.GetForegroundWindow());
            if (want == f11Registered) return;
            if (want) f11Registered = Native.RegisterHotKey(hotkeys.Handle, HotkeyId, Native.MOD_NOREPEAT, Native.VK_F11);
            else
            {
                Native.UnregisterHotKey(hotkeys.Handle, HotkeyId);
                f11Registered = false;
            }
        }

        // كل 80ms والمحاكي شغّال: يخفي الماوس إذا وقف فوق المحاكي أكثر من ثانية
        void MouseTick()
        {
            UpdateHotkey();
            if (!running || !cfg.Flag("HideMouse", true))
            {
                CursorHider.Show();
                return;
            }

            Native.POINT p;
            Native.GetCursorPos(out p);
            int now = Environment.TickCount;
            bool moved = p.X != lastPos.X || p.Y != lastPos.Y;
            bool pressed = (Native.GetAsyncKeyState(0x01) & 0x8000) != 0
                        || (Native.GetAsyncKeyState(0x02) & 0x8000) != 0
                        || (Native.GetAsyncKeyState(0x04) & 0x8000) != 0;
            if (moved || pressed)
            {
                lastPos = p;
                lastMoveTick = now;
                CursorHider.Show();
                return;
            }

            IntPtr under = Native.WindowFromPoint(p);
            bool over = IsMuMuWindow(Native.GetForegroundWindow()) && (IsMuMuWindow(under) || IsMagpieScaling(under));
            if (!CursorHider.Hidden)
            {
                if (over && unchecked(now - lastMoveTick) >= hideDelay) { CursorHider.Hide(); CursorHider.BlankIn(under); }
            }
            else if (!over) CursorHider.Show();
            else CursorHider.BlankIn(under); // المحاكي يرسم مؤشره الخاص، فنفرّغه كل دورة لو رجّعه
        }

        void ToggleFullscreen()
        {
            if (Interlocked.Exchange(ref fullscreenBusy, 1) == 1) return;
            ThreadPool.QueueUserWorkItem(delegate
            {
                try
                {
                    int code;
                    string o = MuMu.Run("control -v " + MuMu.VmIndex + " tool func -n fullscreen", 15000, out code);
                    if (code != 0) Log.Write("شاشة كاملة فشلت (" + code + "): " + o.Trim());
                }
                catch (Exception ex) { Log.Write("شاشة كاملة: " + ex.Message); }
                finally { Interlocked.Exchange(ref fullscreenBusy, 0); }
            });
        }

        void ApplyPendingAsync()
        {
            string mode = cfg.Get("Resolution", "tablet.0");
            applyRunning = true;
            ThreadPool.QueueUserWorkItem(delegate
            {
                string log;
                bool ok;
                try { ok = MuMu.ApplyQuality(mode, out log); }
                catch (Exception ex) { ok = false; log = ex.ToString(); }
                finally { applyRunning = false; }
                Log.Write("تطبيق إعدادات الجودة: " + (ok ? "نجح" : "فشل") + Environment.NewLine + log);
                ui.Post(delegate
                {
                    if (ok)
                    {
                        cfg.SetFlag("PendingMuMu", false);
                        cfg.Save();
                        Balloon("تم تطبيق إعدادات الجودة ✓", "دقة 1920×1080 + إخفاء عداد الفريمات. تبان أول ما تشغّل المحاكي.", ToolTipIcon.Info);
                    }
                    else Balloon("ما قدرت أطبّق إعدادات الجودة", "بحاول مرة ثانية المرة الجاية اللي ينقفل فيها المحاكي.", ToolTipIcon.Warning);
                }, null);
            });
        }

        void NetworkTest()
        {
            if (Interlocked.Exchange(ref netBusy, 1) == 1) return;
            Balloon("فحص النت", "جاري الفحص… تقريباً 10 ثواني", ToolTipIcon.Info);
            ThreadPool.QueueUserWorkItem(delegate
            {
                string msg;
                try { msg = NetCheck.Run(); }
                catch (Exception ex) { msg = "تعذّر الفحص: " + ex.Message; }
                Log.Write(msg.Replace(Environment.NewLine, " | "));
                ui.Post(delegate
                {
                    Interlocked.Exchange(ref netBusy, 0);
                    ShowMessage(msg);
                }, null);
            });
        }

        static void ShowMessage(string text)
        {
            using (var owner = new Form { TopMost = true, ShowInTaskbar = false, FormBorderStyle = FormBorderStyle.None, Opacity = 0, StartPosition = FormStartPosition.CenterScreen, Size = new Size(1, 1) })
            {
                owner.Show();
                owner.Activate();
                MessageBox.Show(owner, text, "T7senaty", MessageBoxButtons.OK, MessageBoxIcon.Information,
                    MessageBoxDefaultButton.Button1, MessageBoxOptions.RtlReading | MessageBoxOptions.RightAlign);
            }
        }

        void Balloon(string title, string text, ToolTipIcon icon)
        {
            tray.BalloonTipTitle = title;
            tray.BalloonTipText = text;
            tray.BalloonTipIcon = icon;
            tray.ShowBalloonTip(5000);
        }

        // يرجّع كل شي طبيعي: الماوس، الأولوية، الشاشة، F11
        void Cleanup()
        {
            if (cleaned) return;
            cleaned = true;
            try { fast.Stop(); slow.Stop(); } catch { }
            CursorHider.Restore();
            UnboostAll();
            SetAwake(false);
            if (magpieOurs)
            {
                MagpieCtl.Stop();
                magpieOurs = false;
            }
            try { if (f11Registered) Native.UnregisterHotKey(hotkeys.Handle, HotkeyId); } catch { }
            f11Registered = false;
            Log.Write("T7senaty وقف — كل شي رجع طبيعي");
        }

        void Quit()
        {
            Cleanup();
            tray.Visible = false;
            tray.Dispose();
            hotkeys.Dispose();
            ExitThread();
        }
    }

    // فحص ذاتي للمطوّر: T7senaty.exe --selftest <ملف النتيجة>
    static class SelfTest
    {
        public static int Run(string outPath)
        {
            var sb = new StringBuilder();
            bool ok = true;
            Action<string, bool> check = (name, pass) =>
            {
                sb.AppendLine((pass ? "PASS  " : "FAIL  ") + name);
                if (!pass) ok = false;
            };
            try
            {
                int before = ArrowPixels();
                CursorHider.Hide();
                int hidden = ArrowPixels();
                CursorHider.Restore();
                int after = ArrowPixels();
                check("cursor visible before hide (" + before + " px)", before > 0);
                check("cursor blank while hidden (" + hidden + " px)", hidden == 0);
                check("cursor restored (" + after + " px)", after == before);

                foreach (string f in MuMu.ShellConfigs())
                {
                    string text = File.ReadAllText(f);
                    string off = MuMu.SetShowFps(text, false);
                    string[] a = text.Split('\n'), b = off.Split('\n');
                    int diff = a.Length == b.Length ? a.Where((line, i) => line != b[i]).Count() : -1;
                    check("show_fps off transform (changed lines: " + diff + ")", off.Contains("\"enabled\": \"false\"") && diff <= 1 && diff >= 0);
                    check("show_fps on transform restores original", MuMu.SetShowFps(off, true) == MuMu.SetShowFps(text, true));
                    string fps = MuMu.GetFpsLimit(text);
                    string fps30 = MuMu.SetFpsLimit(text, "30");
                    check("fps_limit read (" + fps + ") / write keeps other fps keys",
                        fps != null && MuMu.GetFpsLimit(fps30) == "30" && fps30.Contains("\"fps_limit_real\"") && MuMu.SetFpsLimit(fps30, fps) == text);
                }

                int code;
                string o = MuMu.Run("setting -v 0 -k resolution_mode", 20000, out code);
                check("MuMuManager read (exit " + code + "): " + Regex.Replace(o, "\\s+", " ").Trim(), code == 0 && o.Contains("resolution_mode"));

                string sample = "{\"profiles\": [{\"scalingMode\": 2}, {\"name\": \"" + MagpieCtl.ProfileName + "\", \"pathRule\": \"C:\\\\Old\\\\a.exe\", \"scalingMode\": 0, \"cropping\": {}}]}";
                string s1 = MagpieCtl.SetPresetInJson(sample, 1);
                check("Magpie preset edit touches only the MuMu profile", s1.Contains("{\"scalingMode\": 2}") && s1.Contains("\"scalingMode\": 1, \"cropping\""));
                string s2 = MagpieCtl.SetPathInJson(sample, @"C:\New Dir\b.exe");
                check("Magpie path edit round-trip (" + MagpieCtl.GetPathInJson(s2) + ")", MagpieCtl.GetPathInJson(s2) == @"C:\New Dir\b.exe" && MagpieCtl.GetPathInJson(sample) == @"C:\Old\a.exe");
                check("SendInput struct size (" + Marshal.SizeOf(typeof(Native.INPUT)) + ")", Marshal.SizeOf(typeof(Native.INPUT)) == (IntPtr.Size == 8 ? 40 : 28));
                if (MagpieCtl.Installed)
                {
                    string cfgText = File.ReadAllText(MagpieCtl.ConfigPath, Encoding.UTF8);
                    check("Magpie config has MuMu profile (path: " + MagpieCtl.GetPathInJson(cfgText) + ")", MagpieCtl.GetPathInJson(cfgText) != null);
                }
                else sb.AppendLine("INFO  Magpie config not created yet");

                int devices = MuMu.DeviceProcs.Sum(n => Process.GetProcessesByName(n).Length);
                sb.AppendLine("INFO  MuMu device processes running: " + devices);
                check("power throttling API", Power.SetThrottling(Process.GetCurrentProcess().Handle, true));

                string ico = Path.Combine(Path.GetTempPath(), "go3070-selftest.ico");
                Icons.WriteIco(ico, Color.FromArgb(118, 185, 0));
                using (var ic = new Icon(ico, 32, 32)) check("icon file loads (" + ic.Width + "px)", ic.Width == 32);
                File.Delete(ico);
            }
            catch (Exception ex)
            {
                sb.AppendLine("EXCEPTION " + ex);
                ok = false;
            }
            finally { CursorHider.Restore(); }
            sb.AppendLine(ok ? "RESULT: ALL PASS" : "RESULT: FAILED");
            File.WriteAllText(outPath, sb.ToString(), new UTF8Encoding(true));
            return ok ? 0 : 1;
        }

        static int ArrowPixels()
        {
            IntPtr h = Native.LoadCursor(IntPtr.Zero, new IntPtr(32512));
            using (Icon ic = Icon.FromHandle(h))
            using (Bitmap bmp = ic.ToBitmap())
            {
                int n = 0;
                for (int y = 0; y < bmp.Height; y++)
                    for (int x = 0; x < bmp.Width; x++)
                        if (bmp.GetPixel(x, y).A > 0) n++;
                return n;
            }
        }
    }

    static class Program
    {
        public static string AppDir;
        public static string LogPath;
        public static bool LaunchMuMu;

        [STAThread]
        static int Main(string[] args)
        {
            AppDir = Path.GetDirectoryName(Application.ExecutablePath);
            LogPath = Path.Combine(AppDir, "T7senaty.log");

            if (args.Length >= 1 && args[0] == "--selftest")
                return SelfTest.Run(args.Length >= 2 ? args[1] : Path.Combine(AppDir, "selftest.txt"));
            if (args.Length >= 2 && args[0] == "--make-icon")
            {
                Icons.WriteIco(args[1], Color.FromArgb(118, 185, 0));
                return 0;
            }
            if (args.Length >= 1 && args[0] == "--restore-cursor")
            {
                CursorHider.Restore();
                return 0;
            }

            // تطبيق إعدادات المحاكي (الدقة + إخفاء عداد الفريمات). يستدعيه المثبّت والمحاكي مقفل.
            if (args.Length >= 1 && args[0] == "--apply")
            {
                var cfg = new Settings(Path.Combine(AppDir, "settings.ini"));
                MuMu.Dir = cfg.Get("MuMuDir", MuMu.Dir);
                MuMu.VmIndex = cfg.Get("VmIndex", "0");
                string log;
                bool ok;
                try { ok = MuMu.ApplyQuality(cfg.Get("Resolution", "tablet.1"), out log); }
                catch (Exception ex) { ok = false; log = ex.ToString(); }
                Log.Write("--apply: " + (ok ? "نجح" : "فشل") + Environment.NewLine + log);
                return ok ? 0 : 1;
            }

            LaunchMuMu = Array.IndexOf(args, "--launch") >= 0;

            // يشتغل فقط من الاختصار / السكربت (--session). فتحه بدبل كليك ما يسوي شي.
            if (!(args.Length >= 1 && args[0] == "--session"))
            {
                MessageBox.Show("T7senaty ما يشتغل لحاله.\nشغّل الاختصار  T7senaty  من سطح المكتب\n(أو الملف  تشغيل.bat  داخل المجلد).", "T7senaty", MessageBoxButtons.OK,
                    MessageBoxIcon.Information, MessageBoxDefaultButton.Button1, MessageBoxOptions.RtlReading | MessageBoxOptions.RightAlign);
                return 0;
            }

            bool created;
            using (var mutex = new Mutex(true, @"Local\T7senaty_SingleInstance", out created))
            {
                if (!created) return 0; // جلسة شغّالة من قبل
                AppDomain.CurrentDomain.UnhandledException += delegate { CursorHider.Restore(); };
                Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
                Application.ThreadException += (s, e) => Log.Write("خطأ: " + e.Exception);
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                try { Application.Run(new TrayApp()); }
                finally { CursorHider.Restore(); }
            }
            return 0;
        }
    }
}
