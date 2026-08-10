using System;
using System.Text;
using System.Runtime.InteropServices;
using System.Diagnostics;
using System.IO;
using System.Threading;

class Program
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
    delegate bool EnumWin(IntPtr hwnd, IntPtr lp);

    [DllImport("user32")]
    static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint pid);
    [DllImport("user32", CharSet = CharSet.Unicode)]
    static extern int GetClassNameW(IntPtr hwnd, StringBuilder s, int n);
    [DllImport("user32", CharSet = CharSet.Unicode)]
    static extern IntPtr SendMessageW(IntPtr hwnd, uint msg, IntPtr w, StringBuilder l);
    [DllImport("user32", CharSet = CharSet.Unicode)]
    static extern IntPtr SendMessageW(IntPtr hwnd, uint msg, IntPtr w, [MarshalAs(UnmanagedType.LPWStr)] string l);
    [DllImport("user32")]
    static extern int MessageBoxW(IntPtr hwnd, string text, string caption, uint type);

    const uint PROCESS_VM_READ = 0x10;
    const uint PROCESS_VM_WRITE = 0x20;
    const uint PROCESS_VM_OPERATION = 0x08;
    const uint PROCESS_QUERY_INFORMATION = 0x400;

    // VA of the 'push 0x71' (VK_F2) immediate inside the packed Click.exe (ImageBase 0x400000, no ASLR)
    static readonly IntPtr TARGET = (IntPtr)0x401037;
    const long IMG_BASE = 0x400000;
    const long IMG_SIZE = 0x10d000;
    const string STATUS_CLASS = "msctls_statusbar32";
    // GBK bytes for the hint string "按F2" -> we flip the '2' (last byte) into '1'
    static readonly byte[] HINT = { 0xb0, 0xd4, 0x46, 0x32 };

    static void Alert(string text)
    {
        MessageBoxW(IntPtr.Zero, text, "Clicker F1", 0x10);
    }

    // Locate the status-bar control of the running Click.exe by PID + a msctls_statusbar32 child.
    // (We deliberately do NOT match the window title, to stay robust against source encoding.)
    static IntPtr FindStatusBar(int pid)
    {
        IntPtr found = IntPtr.Zero;
        EnumWin cb = (hwnd, lp) =>
        {
            uint p = 0;
            GetWindowThreadProcessId(hwnd, out p);
            if (p == (uint)pid)
            {
                EnumWin child = (ch, lp2) =>
                {
                    StringBuilder cs = new StringBuilder(256);
                    GetClassNameW(ch, cs, 256);
                    if (cs.ToString() == STATUS_CLASS) { found = ch; return false; }
                    return true;
                };
                EnumChildWindows(hwnd, child, IntPtr.Zero);
            }
            return true;
        };
        EnumWindows(cb, IntPtr.Zero);
        return found;
    }

    [STAThread]
    static void Main()
    {
        string baseDir = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
        string exe = Path.Combine(baseDir, "Click.exe");
        if (!File.Exists(exe))
        {
            Alert("Cannot find Click.exe. Keep this launcher in the same folder as Click.exe.");
            return;
        }

        // Single fresh instance so our patch always applies cleanly.
        try
        {
            foreach (var pr in Process.GetProcessesByName("Click")) { try { pr.Kill(); } catch { } }
        }
        catch { }
        Thread.Sleep(300);

        Process p = Process.Start(new ProcessStartInfo(exe)
        {
            WorkingDirectory = baseDir,
            CreateNoWindow = false
        });
        int pid = p.Id;
        IntPtr hp = OpenProcess(PROCESS_VM_READ | PROCESS_VM_WRITE | PROCESS_VM_OPERATION | PROCESS_QUERY_INFORMATION,
                                false, pid);
        if (hp == IntPtr.Zero)
        {
            Alert("Failed to open Click.exe process (error " + Marshal.GetLastWin32Error() + ").");
            return;
        }

        // Wait until NSPack has unpacked and the original 'push 0x71' is live in memory.
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

        // 1) Patch the hotkey VK_F2 (0x71) -> VK_F1 (0x70)
        WriteProcessMemory(hp, TARGET, new byte[] { 0x70 }, 1, out wr);

        // 2) Best-effort: flip the '2' in the in-memory hint-string copies (按F2 -> 按F1)
        byte[] img = new byte[IMG_SIZE];
        long pos = 0;
        while (pos < IMG_SIZE)
        {
            byte[] tb = new byte[0x1000];
            int tn = 0;
            if (ReadProcessMemory(hp, (IntPtr)(IMG_BASE + pos), tb, 0x1000, out tn) && tn > 0)
            {
                Array.Copy(tb, 0, img, pos, tn);
                pos += tn;
            }
            else pos += 0x1000;
        }
        int idx = -1;
        for (int i = 0; i <= img.Length - HINT.Length; i++)
        {
            bool match = true;
            for (int j = 0; j < HINT.Length; j++) if (img[i + j] != HINT[j]) { match = false; break; }
            if (match) { idx = i; break; }
        }
        if (idx >= 0)
            WriteProcessMemory(hp, (IntPtr)(IMG_BASE + idx + 2), new byte[] { 0x31 }, 1, out wr);

        // 3) Rewrite the live, already-displayed status-bar text (F2 -> F1)
        IntPtr status = IntPtr.Zero;
        for (int i = 0; i < 60; i++)
        {
            status = FindStatusBar(pid);
            if (status != IntPtr.Zero) break;
            Thread.Sleep(100);
        }
        if (status != IntPtr.Zero)
        {
            StringBuilder sb = new StringBuilder(512);
            SendMessageW(status, 0x000D, (IntPtr)512, sb); // WM_GETTEXT
            string txt = sb.ToString();
            if (txt.Contains("F2") && !txt.Contains("F1"))
                SendMessageW(status, 0x000C, IntPtr.Zero, txt.Replace("F2", "F1")); // WM_SETTEXT
        }

        CloseHandle(hp);
        // Launcher exits; the patched Click.exe keeps running on its own.
    }
}
