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
    static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);
    [DllImport("user32")]
    static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);
    [DllImport("user32")]
    static extern bool IsWindow(IntPtr hWnd);
    [DllImport("user32")]
    static extern bool IsWindowVisible(IntPtr hWnd);
    [DllImport("user32")]
    static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
    [DllImport("user32")]
    static extern bool SetForegroundWindow(IntPtr hWnd);

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

    struct RECT { public int left, top, right, bottom; }

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
        bool ok = EnumWindows((hwnd, lp) =>
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

        CloseHandle(hp);

        Application.Run(new ControlPanel(p, mainHwnd));
    }

    class ControlPanel : Form
    {
        Process clickProc;
        IntPtr clickHwnd;
        bool isTopmost = false;
        bool dragging = false;
        Point dragStart;
        ToggleSwitch toggle;
        NotifyIcon tray;

        public ControlPanel(Process proc, IntPtr hwnd)
        {
            clickProc = proc;
            clickHwnd = hwnd;
            InitializeComponent();
            PositionByClickWindow();
        }

        void InitializeComponent()
        {
            this.Text = "Clicker F1";
            this.FormBorderStyle = FormBorderStyle.None;
            this.StartPosition = FormStartPosition.Manual;
            this.Size = new Size(200, 120);
            this.BackColor = Color.FromArgb(30, 32, 40);
            this.Opacity = 0.98;
            this.ShowInTaskbar = false;
            this.DoubleBuffered = true;
            this.Font = new Font("Microsoft YaHei UI", 9f);

            RoundRegion(16);

            Label title = new Label
            {
                Text = "Clicker F1",
                ForeColor = Color.White,
                AutoSize = true,
                Location = new Point(14, 10),
                Font = new Font("Microsoft YaHei UI", 10f, FontStyle.Bold)
            };
            this.Controls.Add(title);

            Label btnMin = CreateTopButton("\u2212", () => HideToTray());
            btnMin.Location = new Point(this.Width - 52, 8);
            this.Controls.Add(btnMin);

            Label btnClose = CreateTopButton("\u00D7", () => ExitAll());
            btnClose.Location = new Point(this.Width - 28, 8);
            this.Controls.Add(btnClose);

            toggle = new ToggleSwitch();
            toggle.Location = new Point(16, 50);
            toggle.CheckedChanged += (s, e) =>
            {
                isTopmost = toggle.Checked;
                SetClickTopmost(isTopmost);
            };
            this.Controls.Add(toggle);

            Label lbl = new Label
            {
                Text = "窗口置顶",
                ForeColor = Color.FromArgb(220, 220, 230),
                AutoSize = true,
                Location = new Point(72, 53),
                Font = new Font("Microsoft YaHei UI", 9.5f)
            };
            this.Controls.Add(lbl);

            Label hint = new Label
            {
                Text = "全局热键 F1",
                ForeColor = Color.FromArgb(130, 130, 145),
                AutoSize = true,
                Location = new Point(16, 88),
                Font = new Font("Microsoft YaHei UI", 8f)
            };
            this.Controls.Add(hint);

            this.MouseDown += Panel_MouseDown;
            this.MouseMove += Panel_MouseMove;
            this.MouseUp += Panel_MouseUp;

            tray = new NotifyIcon();
            tray.Icon = SystemIcons.Application;
            tray.Text = "Clicker F1";
            tray.Click += (s, e) => ShowFromTray();
            ContextMenu cm = new ContextMenu();
            cm.MenuItems.Add("显示面板", (s, e) => ShowFromTray());
            cm.MenuItems.Add("退出", (s, e) => ExitAll());
            tray.ContextMenu = cm;
            tray.Visible = true;

            Timer aliveTimer = new Timer();
            aliveTimer.Interval = 1000;
            aliveTimer.Tick += (s, e) =>
            {
                try { if (clickProc != null && clickProc.HasExited) ExitAll(); }
                catch { }
            };
            aliveTimer.Start();
        }

        Label CreateTopButton(string text, Action act)
        {
            var lbl = new Label
            {
                Text = text,
                ForeColor = Color.FromArgb(180, 180, 190),
                AutoSize = false,
                Size = new Size(20, 20),
                TextAlign = ContentAlignment.MiddleCenter,
                Cursor = Cursors.Hand,
                Font = new Font("Microsoft YaHei UI", 9f)
            };
            lbl.MouseEnter += (s, e) => lbl.ForeColor = Color.White;
            lbl.MouseLeave += (s, e) => lbl.ForeColor = Color.FromArgb(180, 180, 190);
            lbl.MouseClick += (s, e) => act();
            return lbl;
        }

        void RoundRegion(int radius)
        {
            int w = this.Width;
            int h = this.Height;
            using (GraphicsPath path = new GraphicsPath())
            {
                path.AddArc(0, 0, radius * 2, radius * 2, 180, 90);
                path.AddArc(w - radius * 2, 0, radius * 2, radius * 2, 270, 90);
                path.AddArc(w - radius * 2, h - radius * 2, radius * 2, radius * 2, 0, 90);
                path.AddArc(0, h - radius * 2, radius * 2, radius * 2, 90, 90);
                path.CloseFigure();
                this.Region = new Region(path);
            }
        }

        void PositionByClickWindow()
        {
            if (clickHwnd == IntPtr.Zero || !IsWindow(clickHwnd)) return;
            RECT rc;
            if (GetWindowRect(clickHwnd, out rc))
            {
                int x = rc.right + 10;
                int y = rc.top + Math.Max(0, ((rc.bottom - rc.top) - this.Height) / 2);
                var screen = Screen.FromHandle(clickHwnd).WorkingArea;
                if (x + this.Width > screen.Right)
                    x = rc.left - this.Width - 10;
                if (x < screen.Left) x = screen.Left + 10;
                if (y + this.Height > screen.Bottom) y = screen.Bottom - this.Height - 10;
                this.Location = new Point(x, y);
            }
            else
            {
                this.StartPosition = FormStartPosition.CenterScreen;
            }
        }

        void SetClickTopmost(bool top)
        {
            if (clickHwnd != IntPtr.Zero && IsWindow(clickHwnd))
                SetWindowPos(clickHwnd, top ? HWND_TOPMOST : HWND_NOTOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
            SetWindowPos(this.Handle, top ? HWND_TOPMOST : HWND_NOTOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
        }

        void Panel_MouseDown(object s, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                dragging = true;
                dragStart = new Point(e.X, e.Y);
            }
        }
        void Panel_MouseMove(object s, MouseEventArgs e)
        {
            if (dragging)
                this.Location = new Point(this.Left + e.X - dragStart.X, this.Top + e.Y - dragStart.Y);
        }
        void Panel_MouseUp(object s, MouseEventArgs e) { dragging = false; }

        void HideToTray()
        {
            this.Hide();
            tray.ShowBalloonTip(1000, "Clicker F1", "已最小化到托盘，点击图标恢复", ToolTipIcon.Info);
        }

        void ShowFromTray()
        {
            this.Show();
            SetForegroundWindow(this.Handle);
        }

        void ExitAll()
        {
            tray.Visible = false;
            try { if (clickProc != null && !clickProc.HasExited) clickProc.Kill(); }
            catch { }
            this.Close();
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            base.OnFormClosing(e);
            if (tray != null) tray.Dispose();
        }
    }

    class ToggleSwitch : Control
    {
        bool _checked = false;
        public bool Checked
        {
            get { return _checked; }
            set { _checked = value; Invalidate(); if (CheckedChanged != null) CheckedChanged(this, EventArgs.Empty); }
        }
        public event EventHandler CheckedChanged;

        public ToggleSwitch()
        {
            this.Size = new Size(44, 24);
            this.Cursor = Cursors.Hand;
            this.DoubleBuffered = true;
            this.Click += (s, e) => { Checked = !Checked; };
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            int h = this.Height;
            int w = this.Width;
            int r = h - 4;

            Color trackColor = _checked ? Color.FromArgb(0, 210, 106) : Color.FromArgb(70, 72, 82);
            using (SolidBrush br = new SolidBrush(trackColor))
            using (GraphicsPath path = RoundedRect(0, 0, w - 1, h - 1, h / 2))
            {
                g.FillPath(br, path);
            }

            int margin = 2;
            int x = _checked ? w - r - margin : margin;
            using (SolidBrush br = new SolidBrush(Color.White))
            {
                g.FillEllipse(br, x, margin, r, r);
            }
        }

        GraphicsPath RoundedRect(int x, int y, int w, int h, int r)
        {
            GraphicsPath path = new GraphicsPath();
            int d = r * 2;
            path.AddArc(x, y, d, d, 180, 90);
            path.AddArc(x + w - d, y, d, d, 270, 90);
            path.AddArc(x + w - d, y + h - d, d, d, 0, 90);
            path.AddArc(x, y + h - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }
    }
}
