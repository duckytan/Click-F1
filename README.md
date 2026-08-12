# Click-F1

一个极小的启动器：把你机器上的 **Click.exe**（一款加壳的鼠标点击器，全局热键绑定在 **F2**）在**运行时**把热键改成 **F1**，同时把底部状态栏里“按 F2 启动/停止”的文字也改成 F1；并把 **「Top」置顶勾选框直接注入到 Click.exe 原窗口顶部右侧**（小号字体、无外框、精致融入窗口），再把底部那个输入框的数字上限从 **6 位放宽到 12 位**。**Click.exe 已内嵌进 `Clicker-F1.exe`，整个工具就是「一个能单独运行的 exe」**——双击 `Clicker-F1.exe` 即可，无需再随身带 `Click.exe`。

> 用途：把 F2 让出来给资源管理器做「重命名」，点击器改用 F1 触发；在原窗口里直接加一个置顶开关，并把输入框限制放开。

---

## 原理

`Click.exe` 是用 NSPack 加壳的原生 PE（文件熵 ≈ 7.99，真实代码在 `.nsp1` 里压缩），而且**没有开 ASLR**（固定基址 `0x400000`）。因此：

- 没法直接在文件里改字节（热键常量藏在压缩数据里）；
- 也没法干净地脱壳重建（系统 DLL 基址 ASLR 随机化会让重建的导入表失效）。

最稳的做法是 **运行时内存补丁**，原 exe 自己负责加载/解包/导入表，每次运行都正确、跨重启稳定：

1. 启动 `Click.exe`，等 NSPack 解包完成；
2. 在内存里把热键立即数 `VK_F2`(`0x71`) → `VK_F1`(`0x70`)，位置 **VA `0x401037`**；
3. 把内存中两份“按F2”字符串常量里的 `2` 改成 `1`（双保险）；
4. 用 `WM_SETTEXT` 把状态栏控件（`msctls_statusbar32`）**正在显示**的文本 F2→F1；
5. 用 `CreateWindowEx` 创建一个原生 `BS_AUTOCHECKBOX` 勾选框（文字 `Top`、小号 Microsoft YaHei 字体、无外框、尺寸约 40×16），通过 `SetParent` 直接挂到 `Click.exe` 主窗口**顶部右侧**成为子控件；并对原窗口设 `WS_CLIPCHILDREN`，防止它重绘时盖掉这个勾选框；
6. 遍历原窗口里的 `Edit` 控件，把数字上限（`EM_GETLIMITTEXT`）为 6 的输入框通过 `EM_SETLIMITTEXT` 放宽到 12。

启动器只在后台驻留（托盘图标可退出），**不再有额外的浮动面板**；勾选框就长在原窗口里，点一下即可切换置顶。

---

## 文件说明

| 文件 | 作用 |
|------|------|
| `Clicker-F1.cs` | 启动器源码（C#，原生 winexe） |
| `build.bat` | 一键调用系统自带 `csc` 编译出 `Clicker-F1.exe` |
| `Clicker-F1.exe` | 编译好的**单文件**启动器（已内嵌 Click.exe，体积随 Click.exe 而定；由 `build.bat` 生成本地副本，不再提交到仓库） |
| `Clicker-F1.ps1` | PowerShell 版兜底（仅热键+状态栏，免编译；不含置顶勾选框注入/输入框放宽） |
| `Clicker-F1.vbs` | 双击即用，调用上面的 ps1 |

---

## 使用方法

**方式一（推荐，单文件分发）：**
1. 把源码和你的 **`Click.exe`** 放在**同一目录**；
2. **双击 `build.bat`**（调用 Windows 自带的 `csc.exe`，无需安装环境），生成 **已内嵌 Click.exe 的 `Clicker-F1.exe`**；
3. **双击 `Clicker-F1.exe`** 即可单独运行——首次启动它会把 `Click.exe` 自动释放到自身同目录（若同目录已存在则直接用），稍后窗口顶部右侧出现 `Top` 勾选框；
4. 勾上它，`Click.exe` 窗口即置顶；取消勾选则取消置顶（勾选框就嵌在原窗口里，不是独立浮窗）。

> 之后你只要带着这一个 `Clicker-F1.exe` 走，无需再带 `Click.exe`。若想把释放出来的 `Click.exe` 删掉也行——下次启动会重新从内嵌资源里解出来。

**方式二（免编译，立刻能用）：**
- 直接双击 `Clicker-F1.vbs`，用系统 PowerShell 跑同款逻辑。

---

## 注意事项

- **`Click.exe` 已内嵌**：`build.bat` 编译时会用 `csc /resource:Click.exe` 把同目录的 `Click.exe` 打进 `Clicker-F1.exe`；运行时若自身同目录没有 `Click.exe`，就自动从内嵌资源释放。仓库不提交 `Click.exe`（已 gitignore），但本机编译时会就地嵌入。
- **未签名**：`Clicker-F1.exe` 没有代码签名，首次运行 Windows SmartScreen 可能拦截，点“仍要运行”即可（这是本项目生成的工具，不是病毒）。
- 设置界面里的「可选热键列表」（Ctrl+Shift+F2 / Shift+F2 等）**刻意未动**，避免下拉框错乱。
- 依赖 `.NET Framework 4.x`（Windows 自带）。

---

## 技术细节

- 目标：`Click.exe`，PE32，ImageBase `0x400000`，无 ASLR / 无 DEP。
- 热键补丁：`WriteProcessMemory` 改 `0x401037` 处 `0x71`→`0x70`。
- 状态栏：`EnumWindows` 按 PID 定位主窗口 → `EnumChildWindows` 找 `msctls_statusbar32` → `WM_SETTEXT`（`0x000C`）改写。
- 之所以不用状态栏专用的 `SB_SETTEXT`：跨进程传本地指针会出错/崩溃，只有 `WM_SETTEXT` 会被系统自动封送。
