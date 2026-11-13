# 按键精灵 2026

Windows 10/11 x64 桌面自动化工具，首版使用 WinForms + .NET 10 + Lua 5.4。

## 当前进度

v0.1.0-preview.4 已正式发布。Windows CI 的 Restore、Build、7 项测试、自包含发布、ZIP/SHA256 和 GitHub Release 创建全部成功。preview.4 加入独立悬浮窗、系统托盘生命周期、悬浮窗循环设置和自定义 EXE/托盘图标。注意：窗口/托盘功能仍需 Windows 10/11 实机手工验收。

下载入口：[v0.1.0-preview.4 Release](https://github.com/xiaobei206/anjian/releases/tag/v0.1.0-preview.4)，[Windows x64 ZIP](https://github.com/xiaobei206/anjian/releases/download/v0.1.0-preview.4/Anjian2026-0.1.0-preview.4-win-x64.zip)，[SHA256 sidecar](https://github.com/xiaobei206/anjian/releases/download/v0.1.0-preview.4/Anjian2026-0.1.0-preview.4-win-x64.zip.sha256)。
ZIP SHA256：`6b59ab27f825aa48b2ec5343cec826ad8b0ded759e9989af02d51f8b949f09aa`。

**main 开发状态（未发布）：** M3–M7 已进入 main；M8 图像资源 UI、manifest/assets、视觉调试、RGB/hex/颜色差值/颜色块已进入 main，Windows Actions run [43358188770](https://github.com/xiaobei206/anjian/actions/runs/43358188770) 的 85/85 tests、Build 0 warnings/errors、API 手册检查、自包含发布与 artifact 上传通过。M9 OCR 已进入 main：API registry 0.7 / 81 个函数，新增 OCR、FindText、WaitText；PaddleOcrNet 2.4.0 CPU 适配器，用户显式下载 ch/en/japan 模型，Worker 识别严格离线。M9 Windows CI run [43363150335](https://github.com/xiaobei206/anjian/actions/runs/43363150335) 全 PASS：90/90 tests、0 warnings/errors、API handbook check、win-x64 自包含发布与 ZIP/SHA256；临时 artifact 14008744436。M10 参数/配置与脚本自定义 UI 已完成实现和自动化收尾：版本化 `.params.json` sidecar、10 种类型、Core 统一校验、独立用户配置/原子写入/备份恢复、播放/调试前动态参数表单、参数设计器、JSON 导入导出、typed Worker IPC 与 Lua `Args` 只读代理；本轮加固了 `pairs(Args)` 深层只读、脚本相对路径运行时解析、Lua 保留字/非法路径校验及未来 schema 防覆盖。最终 Windows Actions run [43368224862](https://github.com/xiaobei206/anjian/actions/runs/43368224862) SUCCESS：Restore、Build（0 warnings/0 errors）、102/102 Core tests、API handbook check、自包含 win-x64 发布、ZIP/SHA256 和 artifact 上传全 PASS。临时 artifact ID 14010789337，87,099,638 bytes，SHA-256 `324ed884d9938548d2d35b94cd372a4bc46bf081f7abd1c77e17b28d1acf1323`，有效期至 2026-12-13；它不是 GitHub Release。M10 桌面验收仍 NOT RUN，M1–M13 统一延期至 M14 后。没有创建 Release，v0.1.0-preview.4 不变。M1–M13 Windows 桌面验收统一延期至 M14 后，未测项继续 NOT RUN；CI 不等于实机验收。v0.1.0-preview.4 不变，不修改 VERSION、不创建 Release。M5 窗口相对录制/回放重定位仍未实现。
使用说明：[首版使用说明](docs/USER-GUIDE-CN.md)。

## 项目完整大纲与接手资料

- [产品需求与完整功能范围（PRD）](docs/PRD-CN.md)
- [路线图与阶段状态](docs/ROADMAP-CN.md)
- [技术架构与模块边界](docs/ARCHITECTURE-CN.md)
- [Lua 5.4 命令 API 规格](docs/LUA-API-CN.md)
- [完整功能矩阵（与经典 PC 功能对应）](docs/FEATURE-MATRIX-CN.md)
- [中文函数与参数参考手册](docs/FUNCTION-REFERENCE-CN.md)
- [录制器与动作/代码生成设计](docs/RECORDER-DESIGN-CN.md)
- [架构决策记录（ADR）](docs/DECISIONS-CN.md)
- [技术选型与 PoC 验证清单](docs/TECHNICAL-VALIDATION-CN.md)
- [验收测试清单](docs/ACCEPTANCE-TEST-CN.md)
- [当前会话交接（新 GPT 必读）](docs/SESSION-HANDOFF.md)
- [GPT 会话协作协议](docs/SESSION-PROTOCOL-CN.md)
- [新 GPT 最短接手提示词](docs/SESSION-START-PROMPT-CN.md)
- [累计开发日志](docs/DEVELOPMENT-LOG-CN.md)

## 完整产品目标

完整的 Windows 桌面自动化开发环境是本项目的正式目标：完整 Lua IDE/调试器、录制与可视化动作列表、多坐标空间、窗口/控件自动化、前后台能力适配、截图/找图/找色/OCR、脚本参数与自定义 UI、插件和独立“小精灵”打包，以及有统一来源的函数/参数手册。阶段状态与验收标准请以 [产品 PRD](docs/PRD-CN.md)、[功能矩阵](docs/FEATURE-MATRIX-CN.md) 和 [路线图](docs/ROADMAP-CN.md) 为准。以上不是 preview.4 已全部实现的声明。

## 首版已实现功能（preview.4）

- 本地 Lua 脚本列表：新建、打开、保存、重命名、复制、删除；支持右键菜单。
- 全局鼠标/键盘录制，生成可编辑 Lua 脚本；录制会过滤本程序自身界面和注入输入。
- 鼠标移动合并/降噪；可勾选只记录点击坐标，生成 MoveTo -> 可选 Delay -> Click，避免录入手抖轨迹；点击和按键按下时长会被保留。
- 录制按钮可开始/暂停/继续，单独结束录制；暂停时保留前段事件并排除暂停时长。
- 鼠标移动、点击、按下/释放、滚轮；键盘单键、按下/释放、组合键和 Unicode 文本输入。
- 播放、暂停、停止；脚本在独立子进程执行，停止通过终止执行进程处理 Lua 无限循环，并清理仍按下的输入。
- 全局快捷键：F10 启动、F11 暂停/继续、F12 立即中止；无需切回主窗口。
- 重复次数：默认 1；输入 100 表示执行 100 轮；0 表示无限循环。
- 每轮间隔：默认 2000ms，可设 0 至 600000ms。只在两轮之间等待，最后一轮后不等待。
- 独立悬浮窗与完整主窗口可同时显示；悬浮窗有循环次数、轮间隔、只录点击、录制/播放控制和实时坐标。
- 系统托盘负责重新打开主窗口、显示/隐藏悬浮窗和退出；关闭主窗口只隐藏窗口并移除任务栏按钮，不退出程序。
- 自定义应用图标同时用于 EXE 文件、主窗口和系统托盘。
- 设置保存在本机用户配置目录。

## 快速使用

1. 从 GitHub Releases 下载 Anjian2026 对应版本的 win-x64 ZIP，解压后运行 Anjian.App.exe。
2. 默认显示独立悬浮窗；点击“完整界面”打开主窗口，悬浮窗会继续留在桌面。悬浮窗可直接修改循环次数和轮间隔；次数 0 表示无限循环。需要编辑脚本时使用主窗口。
3. 双击脚本列表中的脚本打开编辑；Ctrl+S 保存。
4. 主窗口右上角关闭或点击“返回悬浮窗”，只会隐藏主窗口并移除任务栏按钮。右键系统托盘图标可重新打开主窗口、显示/隐藏悬浮窗或退出程序。悬浮窗关闭按钮只隐藏悬浮窗。也可在任意窗口按 F10 启动、F11 暂停/继续、F12 中止并释放键鼠输入。

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

## API 注册表与函数手册生成

- 唯一机器可读来源：`src/Anjian.Core/Api/lua-api-registry.json`，嵌入到 Anjian.Core 供运行时直接读取。
- Lua 全局 API 由注册表生成；C# 内部桥接名不会作为公开 API 暴露。
- main 分支 M3 编辑器自动补全与签名提示已直接读取 `LuaApiRegistry.Load()` 的 `Categories` / `Apis`；不维护第二份函数、参数或说明清单。独立 API 命令面板仍未实现。此变化尚未发布到 preview.4。
- 重新生成手册：`dotnet run --project tools/Anjian.ApiDocsGenerator -c Release -- --write`。
- 检查手册漂移：`dotnet run --project tools/Anjian.ApiDocsGenerator -c Release -- --check`。

## 当前限制

- v0.1.0-preview.4 Release 包中的编辑器仍为基础文本编辑框；M3 增强编辑器只在尚未发布的 main 开发版中。
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
dotnet run --project tools/Anjian.ApiDocsGenerator -c Release -- --write
dotnet run --project tools/Anjian.ApiDocsGenerator -c Release -- --check
dotnet publish src/Anjian.App/Anjian.App.csproj -c Release -r win-x64 --self-contained true

版本号见 VERSION。Windows 构建与测试由 .github/workflows/windows-build.yml 执行。


### M11 小精灵发布（main 开发版，尚未创建正式 Release）

M11 已在 main 加入独立小精灵打包流程：多入口 Lua 依赖收集、可验证的便携目录与 EXE 启动 ZIP、产品元数据、参数界面、独立用户配置、循环/热键/单实例/重试、显式选择的离线 OCR 模型、图像资源校验、manifest/SHA-256、构建日志和随包离线函数参考 Markdown。EXE 是自包含目录中的启动入口，不是单文件程序；必须保留所有运行时伴随文件。Windows CI 自动化通过不等于 WinForms/DPI/真实键鼠输入的桌面验收；M11 桌面项目仍按计划延期至 M14。详见 [用户指南中的小精灵章节](docs/USER-GUIDE-CN.md)。

本轮不修改 VERSION、AssemblyVersion、FileVersion，不创建 tag 或 Release；v0.1.0-preview.4 官方下载资产不变。


### M12 内置扩展 API 与插件系统（main 开发中；受控文件 API 已实现）
M12-01 权限基座已实现：脚本 sidecar schema v1、请求/逐次批准/Grant 分离、默认拒绝、主程序播放/调试前权限审阅，以及 worker-ready 授权传递。M12-02 文件 API 已完成 13 项：ReadTextFile、WriteTextFile、AppendTextFile、FileExists、DirectoryExists、ListFiles、ListDirectories、CreateDirectory、CopyFile、MoveFile、RenameFile、CreateTempFile、CreateTempDirectory。Worker bridge 在发 IPC 前校验本次 Grant，PlaybackRunner 对每条文件 IPC 再独立检查；小精灵复用同一 dispatcher，未获授权时仍为空 Grant。
Windows 适配器逐级使用 OPEN_REPARSE_POINT 目录句柄，拒绝 reparse point/符号链接及多硬链接文件；文本严格 UTF-8、4 MiB 上限，目录枚举非递归、最多 1000 项；Copy/跨卷 Move 限制 64 MiB，复制经同目录临时文件、flush 和 SHA-256 校验后原子提交；同卷 Move/Rename 使用句柄级重命名。临时文件/目录必须明确指定已授权写入目录，不会自动开放系统 TEMP。仅支持普通文件，目录复制/移动/重命名不在本批范围。
当前 main 的 API registry 为 **0.9 / 94 项**；FUNCTION-REFERENCE 由同一注册表生成，IDE 补全/签名继续直接读取该注册表。Windows Actions [run 43385033936](https://github.com/xiaobei206/anjian/actions/runs/43385033936) 对 HEAD 057f400657c8ebaf673eeefae17030476d71789b 全 PASS：Release Build 0 warnings/0 errors、142/142 Core tests、API handbook check、自包含 win-x64 publish、包内 Sprite/Worker smoke、ZIP/SHA256 和 artifact upload。Artifact ID 14018690914，87,196,057 bytes，uploaded artifact SHA-256 5ebc2dae013634a0018f1ffed5c0e74e49d7cef253f9e33bc0f4e630452fe431。
M12-02B 已完成。M12-07 剪贴板/对话框 API 实现已进入 main：GetClipboardText、SetClipboardText、MessageBox、InputBox、Confirm；权限默认拒绝，Worker 与主进程分别验证 Grant，剪贴板/UI 对话框经主 UI 线程执行，Stop 可关闭自定义模态对话框。当前 API registry 1.0 / 99 项。代码提交 7c561f9，文档同步提交 bd316e2；最新 Windows Actions run 43407639817 正在验证，最终状态以 DEVELOPMENT-LOG-CN.md 为准。WinForms 权限审阅窗体、真实 Windows 10/11 文件操作与桌面交互仍 **NOT RUN**，统一留到 M14；CI 不等于实机验收。
