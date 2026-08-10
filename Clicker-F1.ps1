$ErrorActionPreference = 'Stop'

Add-Type -TypeDefinition @'
using System; using System.Text; using System.Runtime.InteropServices;
public class W {
  [DllImport("kernel32", SetLastError=true)] public static extern IntPtr OpenProcess(uint a, bool i, int pid);
  [DllImport("kernel32", SetLastError=true)] public static extern bool CloseHandle(IntPtr h);
  [DllImport("kernel32", SetLastError=true)] public static extern bool ReadProcessMemory(IntPtr h, IntPtr addr, byte[] buf, int size, out int nr);
  [DllImport("kernel32", SetLastError=true)] public static extern bool WriteProcessMemory(IntPtr h, IntPtr addr, byte[] buf, int size, out int wr);
  [DllImport("kernel32")] public static extern bool TerminateProcess(IntPtr h, uint c);
  public delegate bool EW(IntPtr hwnd, IntPtr lp);
  [DllImport("user32")] public static extern bool EnumWindows(EW cb, IntPtr lp);
  [DllImport("user32")] public static extern bool EnumChildWindows(IntPtr p, EW cb, IntPtr lp);
  [DllImport("user32")] public static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint pid);
  [DllImport("user32", CharSet=CharSet.Unicode)] public static extern int GetClassNameW(IntPtr hwnd, StringBuilder s, int n);
  [DllImport("user32", CharSet=CharSet.Unicode)] public static extern IntPtr SendMessageW(IntPtr hwnd, uint msg, IntPtr w, StringBuilder l);
  [DllImport("user32", CharSet=CharSet.Unicode)] public static extern IntPtr SendMessageW(IntPtr hwnd, uint msg, IntPtr w, [MarshalAs(UnmanagedType.LPWStr)] string l);
  [DllImport("user32")] public static extern int MessageBoxW(IntPtr hwnd, string t, string c, uint type);
}
'@

$base = Split-Path -Parent $MyInvocation.MyCommand.Definition
$exe  = Join-Path $base 'Click.exe'
if (-not (Test-Path $exe)) { [W]::MessageBoxW([IntPtr]::Zero, "Cannot find Click.exe in the same folder.", "Clicker F1", 0x10); exit 1 }

$TARGET   = [IntPtr]0x401037
$IMG_BASE = [long]0x400000
$IMG_SIZE = [long]0x10d000
$HINT     = [byte[]](0xb0,0xd4,0x46,0x32)   # GBK bytes of "按F2"

try { Get-Process -Name Click -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue } catch {}
Start-Sleep -Milliseconds 300

$p = Start-Process -FilePath $exe -PassThru -WorkingDirectory $base
$pidv = $p.Id
$hp = [W]::OpenProcess(0x438, $false, $pidv)   # VM_READ|VM_WRITE|VM_OPERATION|QUERY_INFORMATION
if ($hp -eq [IntPtr]::Zero) { [W]::MessageBoxW([IntPtr]::Zero, "Failed to open Click.exe process.", "Clicker F1", 0x10); exit 1 }

# Wait until NSPack has unpacked and the original 'push 0x71' is live in memory.
$b = New-Object byte[] 1; $nr = 0; $ready = $false
for ($i = 0; $i -lt 50; $i++) {
  if ([W]::ReadProcessMemory($hp, $TARGET, $b, 1, [ref]$nr) -and $nr -eq 1 -and $b[0] -eq 0x71) { $ready = $true; break }
  Start-Sleep -Milliseconds 100
}
if (-not $ready) {
  [W]::MessageBoxW([IntPtr]::Zero, "Hotkey byte not found in memory. No changes made.", "Clicker F1", 0x10)
  [W]::TerminateProcess($hp, 0); [W]::CloseHandle($hp); exit 1
}

# 1) Hotkey VK_F2 (0x71) -> VK_F1 (0x70)
$wr = 0
[W]::WriteProcessMemory($hp, $TARGET, [byte[]](0x70), 1, [ref]$wr)

# 2) Best-effort: flip the '2' in the in-memory hint-string copies (按F2 -> 按F1)
$full = New-Object byte[] $IMG_SIZE
$pos = [long]0
while ($pos -lt $IMG_SIZE) {
  $tb = New-Object byte[] 0x1000; $tn = 0
  if ([W]::ReadProcessMemory($hp, [IntPtr]($IMG_BASE + $pos), $tb, 0x1000, [ref]$tn) -and $tn -gt 0) {
    [Array]::Copy($tb, 0, $full, $pos, $tn); $pos += $tn
  } else { $pos += 0x1000 }
}
$idx = -1
for ($i = 0; $i -le $full.Length - $HINT.Length; $i++) {
  $m = $true
  for ($j = 0; $j -lt $HINT.Length; $j++) { if ($full[$i + $j] -ne $HINT[$j]) { $m = $false; break } }
  if ($m) { $idx = $i; break }
}
if ($idx -ge 0) { [W]::WriteProcessMemory($hp, [IntPtr]($IMG_BASE + $idx + 2), [byte[]](0x31), 1, [ref]$wr) }

# 3) Rewrite the live, already-displayed status-bar text (F2 -> F1)
$status = [IntPtr]::Zero
$cb = {
  param($hwnd, $lp)
  $pp = 0
  [W]::GetWindowThreadProcessId($hwnd, [ref]$pp) | Out-Null
  if ($pp -eq $pidv) {
    $ccb = {
      param($ch, $lp2)
      $cs = New-Object Text.StringBuilder 256
      [W]::GetClassNameW($ch, $cs, 256) | Out-Null
      if ($cs.ToString() -eq 'msctls_statusbar32') { $script:status = $ch }
      return $true
    }
    [W]::EnumChildWindows($hwnd, [W+EW]$ccb, [IntPtr]::Zero)
  }
  return $true
}
for ($i = 0; $i -lt 60; $i++) {
  $status = [IntPtr]::Zero
  [W]::EnumWindows([W+EW]$cb, [IntPtr]::Zero)
  if ($status -ne [IntPtr]::Zero) { break }
  Start-Sleep -Milliseconds 100
}
if ($status -ne [IntPtr]::Zero) {
  $sb = New-Object Text.StringBuilder 512
  [W]::SendMessageW($status, 0x000D, [IntPtr]512, $sb)   # WM_GETTEXT
  $txt = $sb.ToString()
  if ($txt.Contains('F2') -and -not $txt.Contains('F1')) {
    [W]::SendMessageW($status, 0x000C, [IntPtr]::Zero, $txt.Replace('F2', 'F1'))   # WM_SETTEXT
  }
}
[W]::CloseHandle($hp)
# Launcher exits; the patched Click.exe keeps running on its own.
