# Click-F1

一个极小的启动器：把你机器上的 **Click.exe**（一款加壳的鼠标点击器，全局热键绑定在 **F2**）在**运行时**把热键改成 **F1**，同时把底部状态栏里“按 F2 启动/停止”的文字也改成 F1。

> 用途：把 F2 让出来给资源管理器做「重命名」，点击器改用 F1 触发。

---

## 原理

`Click.exe` 是用 NSPack 加壳的原生 PE（文件熵 ≈ 7.99，真实代码在 `.nsp1` 里压缩），而且**没有开 ASLR**（固定基址 `0x400000`）。因此：

- 没法直接在文件里改字节（热键常量藏在压缩数据里）；
- 也没法干净地脱壳重建（系统 DLL 基址 ASLR 随机化会让重建的导入表失效）。

最稳的做法是 **运行时内存补丁**，原 exe 自己负责加载/解包/导入表，每次运行都正确、跨重启稳定：

1. 启动 `Click.exe`，等 NSPack 解包完成；
2. 在内存里把热键立即数 `VK_F2`(`0x71`) → `VK_F1`(`0x70`)，位置 **VA `0x401037`**；
3. 把内存中两份“按F2”字符串常量里的 `2` 改成 `1`（双保险）；
4. 用 `WM_SETTEXT` 把状态栏控件（`msctls_statusbar32`）**正在显示**的文本 F2→F1。

启动器退出后，被打过补丁的 `Click.exe` 继续独立运行。

---

## 文件说明

| 文件 | 作用 |
|------|------|
| `Clicker-F1.cs` | 启动器源码（C#，原生 winexe） |
| `build.bat` | 一键调用系统自带 `csc` 编译出 `Clicker-F1.exe` |
| `Clicker-F1.exe` | 编译好的启动器（约 9KB，已包含在仓库） |
| `Clicker-F1.ps1` | PowerShell 版兜底（逻辑同上，免编译） |
| `Clicker-F1.vbs` | 双击即用，调用上面的 ps1 |

---

## 使用方法

**方式一（推荐，得到真·小 exe）：**
1. 把本仓库的 `Clicker-F1.exe`（或源码）和你的 **`Click.exe`** 放在**同一目录**；
2. 以后**双击 `Clicker-F1.exe`** 启动即可。

**想自己编译：**
1. 双击 `build.bat`（调用 Windows 自带的 `csc.exe`，无需安装环境）；
2. 生成的 `Clicker-F1.exe` 会覆盖旧的。

**方式二（免编译，立刻能用）：**
- 直接双击 `Clicker-F1.vbs`，用系统 PowerShell 跑同款逻辑。

---

## 注意事项

- **`Click.exe` 需自备**：本仓库不含第三方打包程序，请把它放到启动器同目录。
- **未签名**：`Clicker-F1.exe` 没有代码签名，首次运行 Windows SmartScreen 可能拦截，点“仍要运行”即可（这是本项目生成的工具，不是病毒）。
- 设置界面里的「可选热键列表」（Ctrl+Shift+F2 / Shift+F2 等）**刻意未动**，避免下拉框错乱。
- 依赖 `.NET Framework 4.x`（Windows 自带）。

---

## 技术细节

- 目标：`Click.exe`，PE32，ImageBase `0x400000`，无 ASLR / 无 DEP。
- 热键补丁：`WriteProcessMemory` 改 `0x401037` 处 `0x71`→`0x70`。
- 状态栏：`EnumWindows` 按 PID 定位主窗口 → `EnumChildWindows` 找 `msctls_statusbar32` → `WM_SETTEXT`（`0x000C`）改写。
- 之所以不用状态栏专用的 `SB_SETTEXT`：跨进程传本地指针会出错/崩溃，只有 `WM_SETTEXT` 会被系统自动封送。
