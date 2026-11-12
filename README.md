# 按键精灵 2026

Windows 10/11 x64 桌面自动化工具，首版使用 WinForms + .NET 10 + Lua 5.4。

## 当前进度

Windows x64 预览版 v0.1.0-preview.2 已完成 CI 构建与自动化测试。该版本新增全局快捷键：F10 启动、F11 暂停/继续、F12 中止。它不是完整 IDE，优先提供录制、Lua 编辑、播放控制和重复运行设置。

下载入口：[v0.1.0-preview.2 正式预览版](https://github.com/xiaobei206/anjian/releases/tag/v0.1.0-preview.2)，或直接下载 [Windows x64 ZIP](https://github.com/xiaobei206/anjian/releases/download/v0.1.0-preview.2/Anjian2026-0.1.0-preview.2-win-x64.zip) 和 [SHA256](https://github.com/xiaobei206/anjian/releases/download/v0.1.0-preview.2/Anjian2026-0.1.0-preview.2-win-x64.zip.sha256)。
使用说明：[首版使用说明](docs/USER-GUIDE-CN.md)。

## 首版目标

- 本地 Lua 脚本列表：新建、打开、保存、重命名、复制、删除；支持右键菜单。
- 全局鼠标/键盘录制，生成可编辑 Lua 脚本；录制会过滤本程序自身界面和注入输入。
- 鼠标移动合并/降噪；点击和按键的按下时长会被保留。
- 鼠标移动、点击、按下/释放、滚轮；键盘单键、按下/释放、组合键和 Unicode 文本输入。
- 播放、暂停、停止；脚本在独立子进程执行，停止通过终止执行进程处理 Lua 无限循环，并清理仍按下的输入。
- 全局快捷键：F10 启动、F11 暂停/继续、F12 立即中止；无需切回主窗口。
- 重复次数：默认 1；输入 100 表示执行 100 轮；0 表示无限循环。
- 每轮间隔：默认 2000ms，可设 0 至 600000ms。只在两轮之间等待，最后一轮后不等待。
- 设置保存在本机用户配置目录。

## 快速使用

1. 从 GitHub Releases 下载 Anjian2026 对应版本的 win-x64 ZIP，解压后运行 Anjian.App.exe。
2. 点击“新建脚本”或“开始录制”。录制开始后切换到目标程序操作，完成后切回本软件点击“结束录制”。
3. 双击脚本列表中的脚本打开编辑；Ctrl+S 保存。
4. 设置重复次数和每轮间隔后点击“播放”；也可在任意窗口按 F10 启动、F11 暂停/继续、F12 中止并释放键鼠输入。

脚本和设置目录：
- 脚本：%LOCALAPPDATA%\Anjian2026\Scripts
- 设置：%LOCALAPPDATA%\Anjian2026\settings.json

## Lua API 示例

MoveTo(122, 100)
LeftClick()        -- 默认按下 50ms 后释放
LeftClick(100)     -- 按下 100ms 后释放
RightClick(50)
KeyPress("ENTER")  -- 默认按下 50ms
Hotkey("CTRL", "SHIFT", "S")
TypeText("按键精灵 2026")
Delay(500)
Log("本轮完成")

长按使用 LeftDown() / Delay(500) / LeftUp()。Delay() 可暂停和停止；点击和按键动作收到停止请求时会在清理路径中释放。

## 当前限制

- 这是早期预览版，编辑器暂为基础文本编辑框，尚无语法高亮/补全。
- 录制使用低级 Windows 输入钩子；如果系统策略、权限或安全软件拦截，录制可能不可用。
- 前台输入受 Windows UIPI 和目标程序权限级别限制；本软件不会尝试绕过受保护桌面或更高权限边界。
- 鼠标移动经过合并/采样，不保证逐像素还原高速轨迹。
- 复杂拖拽和多设备键盘录制需要实机验证。
- 不要在未经验证的生产环境或敏感操作中直接设置无限循环；运行时确认目标窗口正确。

## 开发

PowerShell commands:
dotnet restore Anjian2026.sln
dotnet build Anjian2026.sln -c Release --no-restore
dotnet test tests/Anjian.Core.Tests/Anjian.Core.Tests.csproj -c Release
dotnet publish src/Anjian.App/Anjian.App.csproj -c Release -r win-x64 --self-contained true

版本号见 VERSION。Windows 构建与测试由 .github/workflows/windows-build.yml 执行。
