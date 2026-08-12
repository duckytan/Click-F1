using System;
using System.Drawing;
using System.Text;
using System.Runtime.InteropServices;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Windows.Forms;

class Program
{
    [STAThread]
    static void Main()
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        new Launcher().Run();
    }
}

class Launcher
{
    [DllImport("kernel32", SetLastError = true)]
    static extern IntPtr OpenProcess(uint access, bool inherit, int pid);
    [DllImport("kernel32", SetLastError = true)]
    static extern bool CloseHandle(IntPtr h);
    [DllImport("kernel32", SetLastError = true)]
    static extern bool ReadProcessMemory(IntPtr h, IntPtr addr, byte[] buf, int size, out int nr);
    [DllImport("kernel32", SetLastError = true)]
    static extern bool WriteProcessMemory(IntPtr h, IntPtr addr, byte[] buf, int size, out int wr);
    [DllImport("kernel32")]
    static extern bool TerminateProcess(IntPtr h, uint code);

    [DllImport("user32")]
    static extern bool EnumWindows(EnumWin cb, IntPtr lp);
    [DllImport("user32")]
    static extern bool EnumChildWindows(IntPtr parent, EnumWin cb, IntPtr lp);
    [DllImport("user32")]
    static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint pid);
    [DllImport("user32", CharSet = CharSet.Unicode)]
    static extern int GetClassNameW(IntPtr hwnd, StringBuilder s, int n);
    [DllImport("user32", CharSet = CharSet.Unicode)]
    static extern IntPtr SendMessageW(IntPtr hwnd, uint msg, IntPtr w, StringBuilder l);
    [DllImport("user32", CharSet = CharSet.Unicode)]
    static extern IntPtr SendMessageW(IntPtr hwnd, uint msg, IntPtr w, [MarshalAs(UnmanagedType.LPWStr)] string l);
    [DllImport("user32")]
    static extern IntPtr SendMessageW(IntPtr hwnd, uint msg, int w, int l);
    [DllImport("user32")]
    static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);
    [DllImport("user32")]
    static extern bool GetClientRect(IntPtr hWnd, out RECT lpRect);
    [DllImport("user32")]
    static extern bool ScreenToClient(IntPtr hWnd, ref POINT lpPoint);
    [DllImport("user32")]
    static extern IntPtr SetParent(IntPtr hWndChild, IntPtr hWndNewParent);
    [DllImport("user32")]
    static extern int GetWindowLong(IntPtr hWnd, int nIndex);
    [DllImport("user32")]
    static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);
    [DllImport("user32")]
    static extern bool IsWindow(IntPtr hWnd);
    [DllImport("user32")]
    static extern bool IsWindowVisible(IntPtr hWnd);
    [DllImport("user32", CharSet = CharSet.Unicode)]
    static extern IntPtr CreateWindowExW(uint dwExStyle, string lpClassName, string lpWindowName,
                                         uint dwStyle, int X, int Y, int nWidth, int nHeight,
                                         IntPtr hWndParent, IntPtr hMenu, IntPtr hInstance, IntPtr lpParam);
    [DllImport("kernel32")]
    static extern IntPtr GetModuleHandleW(string lpModuleName);
    [DllImport("user32")]
    static extern bool DestroyWindow(IntPtr hWnd);
    [DllImport("user32")]
    static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

    delegate bool EnumWin(IntPtr hwnd, IntPtr lp);

    const uint PROCESS_VM_READ = 0x10;
    const uint PROCESS_VM_WRITE = 0x20;
    const uint PROCESS_VM_OPERATION = 0x08;
    const uint PROCESS_QUERY_INFORMATION = 0x400;

    static readonly IntPtr TARGET = (IntPtr)0x401037;
    const long IMG_BASE = 0x400000;
    const long IMG_SIZE = 0x10d000;
    const string STATUS_CLASS = "msctls_statusbar32";
    static readonly byte[] HINT = { 0xb0, 0xd4, 0x46, 0x32 };

    static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
    static readonly IntPtr HWND_NOTOPMOST = new IntPtr(-2);
    static readonly IntPtr HWND_TOP = new IntPtr(0);
    const uint SWP_NOMOVE = 0x0002;
    const uint SWP_NOSIZE = 0x0001;
    const uint SWP_NOACTIVATE = 0x0010;
    const uint SWP_FRAMECHANGED = 0x0020;
    const uint SWP_NOZORDER = 0x0004;
    const uint WS_VISIBLE = 0x10000000;
    const uint WS_CHILD = 0x40000000;
    const uint WS_BORDER = 0x00800000;
    const uint WS_CLIPSIBLINGS = 0x04000000;
    const uint BS_AUTOCHECKBOX = 0x0003;
    const uint BM_GETCHECK = 0x00F0;
    const uint BM_SETCHECK = 0x00F1;
    const int GWL_STYLE = -16;
    const int WS_CLIPCHILDREN = 0x02000000;
    const int EM_GETLIMITTEXT = 0x00BA;
    const int EM_SETLIMITTEXT = 0x00C5;
    const uint WM_SETFONT = 0x0030;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    struct LOGFONT
    {
        public int lfHeight;
        public int lfWidth;
        public int lfEscapement;
        public int lfOrientation;
        public int lfWeight;
        public byte lfItalic;
        public byte lfUnderline;
        public byte lfStrikeOut;
        public byte lfCharSet;
        public byte lfOutPrecision;
        public byte lfClipPrecision;
        public byte lfQuality;
        public byte lfPitchAndFamily;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string lfFaceName;
    }

    [DllImport("gdi32", CharSet = CharSet.Auto)]
    static extern IntPtr CreateFontIndirect([In] ref LOGFONT lf);
    [DllImport("gdi32")]
    static extern bool DeleteObject(IntPtr hObject);
    [DllImport("user32")]
    static extern IntPtr SendMessageW(IntPtr hwnd, uint msg, IntPtr w, IntPtr l);

    struct RECT { public int left, top, right, bottom; }

    [StructLayout(LayoutKind.Sequential)]
    struct POINT { public int x; public int y; }

    static void Alert(string text)
    {
        MessageBox.Show(text, "Clicker F1", MessageBoxButtons.OK, MessageBoxIcon.Error);
    }

    static void Log(string text)
    {
        try
        {
            string path = Path.Combine(Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location), "Clicker-F1.log");
            File.AppendAllText(path, DateTime.Now.ToString("HH:mm:ss.fff ") + text + Environment.NewLine);
        }
        catch { }
    }

    // 从内嵌资源中取出打包好的 Click.exe（build.bat 用 /resource:Click.exe 嵌入）
    static byte[] LoadEmbeddedClick()
    {
        try
        {
            var asm = System.Reflection.Assembly.GetExecutingAssembly();
            foreach (var name in asm.GetManifestResourceNames())
            {
                if (name.EndsWith("Click.exe", StringComparison.OrdinalIgnoreCase))
                {
                    using (var s = asm.GetManifestResourceStream(name))
                    {
                        if (s == null) return null;
                        byte[] buf = new byte[s.Length];
                        int off = 0, n;
                        while (off < buf.Length && (n = s.Read(buf, off, buf.Length - off)) > 0) off += n;
                        return buf;
                    }
                }
            }
        }
        catch { }
        return null;
    }

    static bool FindClickWindow(int pid, out IntPtr mainHwnd, out IntPtr statusHwnd)
    {
        mainHwnd = IntPtr.Zero;
        statusHwnd = IntPtr.Zero;
        IntPtr foundMain = IntPtr.Zero;
        IntPtr foundStatus = IntPtr.Zero;
        EnumWindows((hwnd, lp) =>
        {
            uint p = 0;
            GetWindowThreadProcessId(hwnd, out p);
            if (p == (uint)pid && IsWindowVisible(hwnd))
            {
                IntPtr tmpStatus = IntPtr.Zero;
                EnumChildWindows(hwnd, (ch, lp2) =>
                {
                    StringBuilder cs = new StringBuilder(256);
                    GetClassNameW(ch, cs, 256);
                    if (cs.ToString() == STATUS_CLASS) { tmpStatus = ch; return false; }
                    return true;
                }, IntPtr.Zero);
                if (tmpStatus != IntPtr.Zero)
                {
                    foundMain = hwnd;
                    foundStatus = tmpStatus;
                    return false;
                }
            }
            return true;
        }, IntPtr.Zero);
        if (foundMain != IntPtr.Zero)
        {
            mainHwnd = foundMain;
            statusHwnd = foundStatus;
            return true;
        }
        return false;
    }

    public void Run()
    {
        string baseDir = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
        string exe = Path.Combine(baseDir, "Click.exe");
        if (!File.Exists(exe))
        {
            // 优先用同目录下的 Click.exe；否则从内嵌资源中释放
            byte[] data = LoadEmbeddedClick();
            if (data != null)
            {
                try { File.WriteAllBytes(exe, data); }
                catch
                {
                    // 同目录写入失败（只读/无权限），退而求其次放到临时目录
                    try
                    {
                        string tmp = Path.Combine(Path.GetTempPath(), "ClickerF1");
                        Directory.CreateDirectory(tmp);
                        exe = Path.Combine(tmp, "Click.exe");
                        if (!File.Exists(exe)) File.WriteAllBytes(exe, data);
                    }
                    catch { exe = null; }
                }
            }
            else exe = null;
        }
        if (string.IsNullOrEmpty(exe) || !File.Exists(exe))
        {
            Alert("找不到 Click.exe，且本程序内也未嵌入该文件。\n请把 Click.exe 放到本程序同目录，或用 build.bat 重新编译以将其打包进 exe。");
            return;
        }

        try
        {
            foreach (var pr in Process.GetProcessesByName("Click")) { try { pr.Kill(); } catch { } }
        }
        catch { }
        Thread.Sleep(300);

        Process p;
        try { p = Process.Start(new ProcessStartInfo(exe) { WorkingDirectory = Path.GetDirectoryName(exe) }); }
        catch (Exception ex) { Alert("Failed to start Click.exe: " + ex.Message); return; }

        int pid = p.Id;
        IntPtr hp = OpenProcess(PROCESS_VM_READ | PROCESS_VM_WRITE | PROCESS_VM_OPERATION | PROCESS_QUERY_INFORMATION, false, pid);
        if (hp == IntPtr.Zero)
        {
            Alert("Failed to open Click.exe process (error " + Marshal.GetLastWin32Error() + ").");
            return;
        }

        byte[] b = new byte[1];
        int nr = 0;
        int wr = 0;
        bool ready = false;
        for (int i = 0; i < 50; i++)
        {
            if (ReadProcessMemory(hp, TARGET, b, 1, out nr) && nr == 1 && b[0] == 0x71) { ready = true; break; }
            Thread.Sleep(100);
        }
        if (!ready)
        {
            Alert("Hotkey byte not found in memory (Click.exe may have been updated or is protected). No changes made.");
            TerminateProcess(hp, 0);
            CloseHandle(hp);
            return;
        }

        WriteProcessMemory(hp, TARGET, new byte[] { 0x70 }, 1, out wr);

        byte[] img = new byte[IMG_SIZE];
        long pos = 0;
        int tn = 0;
        while (pos < IMG_SIZE)
        {
            byte[] tb = new byte[0x1000];
            if (ReadProcessMemory(hp, (IntPtr)(IMG_BASE + pos), tb, 0x1000, out tn) && tn > 0)
            {
                Array.Copy(tb, 0, img, pos, tn);
                pos += tn;
            }
            else pos += 0x1000;
        }
        for (int i = 0; i <= img.Length - HINT.Length; i++)
        {
            bool match = true;
            for (int j = 0; j < HINT.Length; j++) if (img[i + j] != HINT[j]) { match = false; break; }
            if (match)
            {
                WriteProcessMemory(hp, (IntPtr)(IMG_BASE + i + 2), new byte[] { 0x31 }, 1, out wr);
                break;
            }
        }

        IntPtr mainHwnd = IntPtr.Zero;
        IntPtr statusHwnd = IntPtr.Zero;
        for (int i = 0; i < 60; i++)
        {
            if (FindClickWindow(pid, out mainHwnd, out statusHwnd)) break;
            Thread.Sleep(100);
        }

        if (statusHwnd != IntPtr.Zero)
        {
            StringBuilder sb = new StringBuilder(512);
            SendMessageW(statusHwnd, 0x000D, (IntPtr)512, sb);
            string txt = sb.ToString();
            if (txt.Contains("F2") && !txt.Contains("F1"))
                SendMessageW(statusHwnd, 0x000C, IntPtr.Zero, txt.Replace("F2", "F1"));
        }

        if (mainHwnd != IntPtr.Zero)
        {
            // prevent Click.exe from erasing our embedded control on repaint
            int st = GetWindowLong(mainHwnd, GWL_STYLE);
            SetWindowLong(mainHwnd, GWL_STYLE, st | WS_CLIPCHILDREN);
            SetWindowPos(mainHwnd, IntPtr.Zero, 0, 0, 0, 0, SWP_FRAMECHANGED | SWP_NOMOVE | SWP_NOSIZE | SWP_NOZORDER);

            // lift the bottom numeric input limit 6 -> 12
            EnumChildWindows(mainHwnd, (hwnd, lp) =>
            {
                StringBuilder cs = new StringBuilder(256);
                GetClassNameW(hwnd, cs, 256);
                if (cs.ToString() == "Edit")
                {
                    int lim = (int)SendMessageW(hwnd, EM_GETLIMITTEXT, 0, 0);
                    if (lim > 0 && lim < 12) SendMessageW(hwnd, EM_SETLIMITTEXT, 12, 0);
                }
                return true;
            }, IntPtr.Zero);
        }

        CloseHandle(hp);

        Application.Run(new AppContext(p, mainHwnd, statusHwnd));
    }

    class AppContext : ApplicationContext
    {
        Process clickProc;
        IntPtr clickHwnd;
        IntPtr statusHwnd;
        IntPtr checkHwnd = IntPtr.Zero;
        IntPtr checkFont = IntPtr.Zero;
        NotifyIcon tray;
        System.Windows.Forms.Timer timer;
        bool lastChecked = false;

        public AppContext(Process proc, IntPtr hwnd, IntPtr status)
        {
            clickProc = proc;
            clickHwnd = hwnd;
            statusHwnd = status;

            tray = new NotifyIcon();
            tray.Icon = SystemIcons.Application;
            tray.Text = "Clicker F1";
            ContextMenu cm = new ContextMenu();
            cm.MenuItems.Add("退出", (s, e) => ExitAll());
            tray.ContextMenu = cm;
            tray.Visible = true;

            timer = new System.Windows.Forms.Timer();
            timer.Interval = 200;
            timer.Tick += (s, e) => Tick();
            timer.Start();

            checkFont = CreateCheckFont();
        }

        IntPtr CreateCheckFont()
        {
            LOGFONT lf = new LOGFONT();
            lf.lfHeight = -12;            // ~9pt, 小号字体
            lf.lfWeight = 400;            // 常规字重
            lf.lfCharSet = 1;             // DEFAULT_CHARSET
            lf.lfQuality = 5;             // CLEARTYPE_QUALITY 抗锯齿
            lf.lfPitchAndFamily = 0;      // DEFAULT_PITCH | FF_DONTCARE
            lf.lfFaceName = "Microsoft YaHei";
            return CreateFontIndirect(ref lf);
        }

        void Tick()
        {
            try { if (clickProc != null && clickProc.HasExited) ExitApp(); }
            catch { ExitApp(); }

            if (!IsWindow(clickHwnd)) { ExitApp(); return; }

            if (!IsWindow(checkHwnd))
            {
                CreateCheckBox();
                lastChecked = false;
            }
            else
            {
                RepositionCheckBox();
                bool chk = (int)SendMessageW(checkHwnd, BM_GETCHECK, 0, 0) == 1;
                if (chk != lastChecked)
                {
                    lastChecked = chk;
                    SetWindowPos(clickHwnd, chk ? HWND_TOPMOST : HWND_NOTOPMOST,
                                 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
                }
            }
        }

        void GetCheckBoxPos(out int x, out int y, out int w, out int h)
        {
            w = 40; h = 16;
            RECT cr; GetClientRect(clickHwnd, out cr);
            x = cr.right - w - 6;
            y = 3;
            if (x < 4) x = 4;
        }

        void CreateCheckBox()
        {
            if (!IsWindow(clickHwnd)) return;
            int x, y, w, h;
            GetCheckBoxPos(out x, out y, out w, out h);

            Log(string.Format("create checkbox at ({0},{1}) size {2}x{3}", x, y, w, h));
            IntPtr hInstance = GetModuleHandleW(null);
            checkHwnd = CreateWindowExW(0, "BUTTON", "Top",
                                        WS_CHILD | WS_VISIBLE | WS_CLIPSIBLINGS | BS_AUTOCHECKBOX,
                                        x, y, w, h, clickHwnd, IntPtr.Zero, hInstance, IntPtr.Zero);
            int err = Marshal.GetLastWin32Error();
            Log(string.Format("checkbox hwnd={0:X} lastErr={1}", checkHwnd.ToInt64(), err));
            if (checkHwnd != IntPtr.Zero)
            {
                if (checkFont != IntPtr.Zero)
                    SendMessageW(checkHwnd, WM_SETFONT, checkFont, (IntPtr)1);
                SetWindowPos(checkHwnd, HWND_TOP, x, y, w, h, SWP_NOACTIVATE);
                SendMessageW(checkHwnd, BM_SETCHECK, 0, 0);
                lastChecked = false;
            }
            else
            {
                Alert("CreateWindowEx for checkbox failed (lastErr=" + err + ").");
            }
        }

        void RepositionCheckBox()
        {
            if (!IsWindow(clickHwnd) || !IsWindow(checkHwnd)) return;
            int x, y, w, h;
            GetCheckBoxPos(out x, out y, out w, out h);
            SetWindowPos(checkHwnd, HWND_TOP, x, y, w, h, SWP_NOACTIVATE);
        }

        void ExitAll()
        {
            try { if (clickProc != null && !clickProc.HasExited) clickProc.Kill(); } catch { }
            ExitApp();
        }

        void ExitApp()
        {
            try { timer.Stop(); } catch { }
            if (checkHwnd != IntPtr.Zero && IsWindow(checkHwnd)) DestroyWindow(checkHwnd);
            if (checkFont != IntPtr.Zero) { DeleteObject(checkFont); checkFont = IntPtr.Zero; }
            tray.Visible = false;
            tray.Dispose();
            Application.Exit();
        }

        protected override void Dispose(bool disposing)
        {
            try { if (checkHwnd != IntPtr.Zero && IsWindow(checkHwnd)) DestroyWindow(checkHwnd); }
            catch { }
            base.Dispose(disposing);
        }
    }
}
