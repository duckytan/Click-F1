using System;
using System.Drawing;
using System.Drawing.Drawing2D;
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
    static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);
    [DllImport("user32")]
    static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);
    [DllImport("user32")]
    static extern bool GetClientRect(IntPtr hWnd, out RECT lpRect);
    [DllImport("user32")]
    static extern IntPtr FindWindowEx(IntPtr hWndParent, IntPtr hWndChildAfter, string lpszClass, string lpszWindow);
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
    const uint SWP_NOMOVE = 0x0002;
    const uint SWP_NOSIZE = 0x0001;
    const uint SWP_NOACTIVATE = 0x0010;
    const uint SWP_FRAMECHANGED = 0x0020;
    const uint SWP_NOZORDER = 0x0004;

    const int WS_CHILD = 0x40000000;
    const int WS_CLIPCHILDREN = 0x02000000;
    const int GWL_STYLE = -16;
    const int EM_GETLIMITTEXT = 0x00BA;
    const int EM_SETLIMITTEXT = 0x00C5;

    struct RECT { public int left, top, right, bottom; }

    [StructLayout(LayoutKind.Sequential)]
    struct POINT { public int x; public int y; }

    static void Alert(string text)
    {
        MessageBox.Show(text, "Clicker F1", MessageBoxButtons.OK, MessageBoxIcon.Error);
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
            Alert("Cannot find Click.exe. Keep this launcher in the same folder as Click.exe.");
            return;
        }

        try
        {
            foreach (var pr in Process.GetProcessesByName("Click")) { try { pr.Kill(); } catch { } }
        }
        catch { }
        Thread.Sleep(300);

        Process p;
        try { p = Process.Start(new ProcessStartInfo(exe) { WorkingDirectory = baseDir }); }
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

        Application.Run(new EmbeddedToggle(p, mainHwnd, statusHwnd));
    }

    class EmbeddedToggle : Form
    {
        Process clickProc;
        IntPtr clickHwnd;
        IntPtr statusHwnd = IntPtr.Zero;
        NotifyIcon tray;
        CheckBox cb;
        System.Windows.Forms.Timer aliveTimer;

        public EmbeddedToggle(Process proc, IntPtr hwnd, IntPtr statusHwnd)
        {
            clickProc = proc;
            clickHwnd = hwnd;
            this.statusHwnd = statusHwnd;

            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            BackColor = SystemColors.Control;
            TopMost = false;

            cb = new CheckBox();
            cb.Text = "窗口置顶";
            cb.AutoSize = true;
            cb.Font = new Font("Microsoft YaHei UI", 9f);
            cb.BackColor = SystemColors.Control;
            cb.ForeColor = SystemColors.ControlText;
            cb.Location = new Point(4, 2);
            cb.CheckedChanged += (s, e) => SetClickTopmost(cb.Checked);
            Controls.Add(cb);

            this.ClientSize = new Size(cb.Width + 10, cb.Height + 6);

            tray = new NotifyIcon();
            tray.Icon = SystemIcons.Application;
            tray.Text = "Clicker F1" + (hwnd == IntPtr.Zero ? " (独立)" : "");
            ContextMenu cm = new ContextMenu();
            cm.MenuItems.Add("退出", (s, e) => ExitAll());
            tray.ContextMenu = cm;
            tray.Visible = true;

            aliveTimer = new System.Windows.Forms.Timer();
            aliveTimer.Interval = 1000;
            aliveTimer.Tick += (s, e) =>
            {
                try { if (clickProc != null && clickProc.HasExited) Application.Exit(); }
                catch { Application.Exit(); }
            };
            aliveTimer.Start();
        }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.Style |= WS_CHILD;
                if (clickHwnd != IntPtr.Zero) cp.Parent = clickHwnd;
                return cp;
            }
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            if (clickHwnd != IntPtr.Zero)
            {
                RECT cr; GetClientRect(clickHwnd, out cr);
                int x = cr.right - this.Width - 10;
                int y = cr.bottom - this.Height - 8;
                if (statusHwnd != IntPtr.Zero)
                {
                    RECT sr; GetWindowRect(statusHwnd, out sr);
                    POINT sp = new POINT(); sp.x = sr.left; sp.y = sr.top;
                    ScreenToClient(clickHwnd, ref sp);
                    y = sp.y - this.Height - 4;
                }
                if (x < 4) x = 4;
                if (y < 4) y = 4;
                this.Location = new Point(x, y);
                this.Visible = true;
            }
            else
            {
                this.Location = new Point(Screen.PrimaryScreen.WorkingArea.Right - this.Width - 20, Screen.PrimaryScreen.WorkingArea.Bottom - this.Height - 20);
                this.Visible = true;
            }
        }

        void SetClickTopmost(bool top)
        {
            if (clickHwnd != IntPtr.Zero && IsWindow(clickHwnd))
                SetWindowPos(clickHwnd, top ? HWND_TOPMOST : HWND_NOTOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
        }

        void ExitAll()
        {
            try { if (clickProc != null && !clickProc.HasExited) clickProc.Kill(); } catch { }
            Application.Exit();
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            base.OnFormClosing(e);
            if (tray != null) tray.Dispose();
        }
    }
}
