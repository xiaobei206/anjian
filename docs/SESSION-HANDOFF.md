# 当前会话交接

## 当前状态
- 项目：按键精灵 2026（暂定工作名）
- 仓库：xiaobei206/anjian
- 主分支：main
- 已发布版本：**v0.1.0-preview.4**
- Release：https://github.com/xiaobei206/anjian/releases/tag/v0.1.0-preview.4
- Release commit：`08886d9e66c08ba24f6f1b7d9e91bbdaeba8a9e0`
- 发布 Actions run：`43253611158`，结论 SUCCESS。
- v0.1.0-preview.4 发布包对应代码 commit：`08886d9e66c08ba24f6f1b7d9e91bbdaeba8a9e0`。
- M3 当前 main 开发代码验收基线：`71fd493cad0fbed1451a4ffa46192752a18bd0a6`（Windows Actions run `43282795272` PASS）；接手时仍须重新核对 main HEAD。
- main 分支可能继续产生文档提交；新 GPT 接手时必须重新查询 main 最新 HEAD，不要把 handoff 中的历史提交 SHA 当成分支当前头部。
- 当前阶段：M0/M2/M3/M4/M5/M6 代码与 Windows CI 验收均已完成；M6 最终 commit 4f9b2b2648fb154353ca55522e9c77fe77645506 对应 run 43300620614 SUCCESS（61/61 tests、Release Build 0 warnings/0 errors、API handbook check、win-x64 publish、ZIP/SHA256、artifact upload 全 PASS）。M1 Hook 与 M3/M4/M5/M6 Windows 桌面手工验收仍需独立执行（NOT RUN）。

## 完整产品目标已冻结（2026-11-13）
- 产品范围基线：[docs/PRD-CN.md](PRD-CN.md)。
- 完整功能矩阵（当前状态、阶段、验收条件）：[docs/FEATURE-MATRIX-CN.md](FEATURE-MATRIX-CN.md)。
- 中文函数与参数参考手册：[docs/FUNCTION-REFERENCE-CN.md](FUNCTION-REFERENCE-CN.md)。
- API 当前实现契约：[docs/LUA-API-CN.md](LUA-API-CN.md)。
- 阶段路线图：[docs/ROADMAP-CN.md](ROADMAP-CN.md)。
- 完整 IDE、窗口/坐标、图色/OCR、参数 UI、小精灵、插件及函数手册均为正式目标，不得再从大纲中删除或只标为可选远期项目。
- 当前 main 的 M3–M7 已完成各自代码/CI 阶段；M8 核心图像 API 已实现并经 Windows CI 验证，但模板资源 UI、视觉调试层与 Windows 桌面实测仍未完成。API registry 现为 0.5，共 74 个公开函数。每轮任务仍以文末唯一任务为准。公开 API 增改仍必须遵守 M2 单一注册表。


## preview.4 窗口与托盘决策
- 悬浮窗和完整主窗口必须是两个独立顶层窗口，可以同时显示；打开主窗口不替换或隐藏悬浮窗。
- 程序启动默认显示悬浮窗；主窗口隐藏且没有任务栏按钮，系统托盘图标保持可用。
- 主窗口右上角 × 与“返回悬浮窗”按钮只隐藏主窗口、移除任务栏按钮，不退出进程；如果悬浮窗此前被用户隐藏，关闭主窗口不擅自重新显示它。
- 托盘菜单负责重新打开主窗口、显示/隐藏悬浮窗和明确退出程序。悬浮窗关闭按钮只隐藏悬浮窗。
- 悬浮窗直接设置循环次数和轮间隔；与主窗口使用同一份配置并双向同步，0 次表示无限循环。
- EXE、主窗口、悬浮窗和托盘使用同一套自定义多尺寸图标。
- 原有只录点击、录制暂停/续录、全局快捷键和播放循环能力继续保留。

## 已冻结的产品目标与当前版本语义

- 目标平台：Windows 10/11 x64；当前技术基础为 WinForms + .NET 10 + Lua 5.4。
- 当前 preview.4 的用户脚本 API 仍保留 MoveTo(x, y) = 虚拟桌面绝对物理像素；副屏负坐标合法。窗口/客户区/锚点/归一化坐标是完整产品必须实现的新能力，不能静默改变 MoveTo 既有语义。
- Click / KeyPress 默认按下 50ms；显式传入 0 表示不增加人为等待；当前规格统一最大 600000ms。长按使用 Down / Delay / Up。
- 播放次数默认 1；正整数按完整脚本轮数；0 = 无限循环。
- 每轮间隔默认 2000ms，只在两轮之间等待，最后一轮后不等待；与脚本内部 Delay 独立。
- 经典 PC 版功能的核心等价范围包括 IDE/调试、录制与动作列表、键鼠、完整坐标/窗口、图色/OCR、脚本参数、自定义用户界面、插件和独立小精灵打包。
- 原先“首版先做轻量小工具”“当前版本不提供窗口相对坐标”的决定只描述 preview.4/首阶段，不再限制完整产品目标。
- 旧版 Q 语言/旧插件二进制完全兼容、移动端、云账号/商城、远程群控、内核驱动与安全绕过不属于目标。

## 当前已交付功能
- WinForms 主窗口和脚本列表。
- 脚本本地存储、新建、打开、保存、重命名、复制、删除和右键菜单。
- M3 Lua 编辑器（仅在当前 main 未发布开发版）：语法高亮、行号、注册表驱动 API 补全/签名提示、查找替换、多标签、语法只编译诊断与错误行导航；标签切换/关闭自动保存。v0.1.0-preview.4 发布包仍只有基础文本编辑器。
- 全局鼠标/键盘录制，鼠标移动合并，录制过滤注入事件及本程序窗口事件，避免键盘自动重复污染；录制结束时补齐尚未释放的 Down/Up。
- M6 坐标系统与窗口 API（仅 main 开发版，尚未发布）：screen/window/client/anchor/ratio 双向转换；窗口查找/枚举（含子窗口）、属性、显示状态/置顶/移动/尺寸/锚点对齐/等待；不透明窗口引用与销毁失效保护。MoveTo 绝对屏幕物理像素语义不变。
- 录制数据生成 Lua 代码，点击按下时长与动作间隔分开保存。
- NLua 1.7.9 / Lua 5.4 worker 子进程；named pipe IPC；指令钩子 Pause 检查点；Stop 可终止 CPU 无限循环 worker。
- Windows SendInput / SetCursorPos 鼠标键盘输入、键状态追踪和停止清理。
- Unicode 文本输入、可取消长文本注入、常见标点键和数字小键盘键映射。
- 播放/暂停/停止、重复次数和轮间间隔；配置持久化于用户本地配置目录。
- 全局快捷键：空闲时 F10 启动；运行/暂停时 F11 暂停/继续、F12 中止。低级键盘钩子过滤模拟注入事件；只有对应动作可执行时才截获功能键。代码提交 `e8e96cb26df1a254d2db45dd2a72b2c7ef11cd9f`，Windows CI 已通过；实机验收待用户完成。
- self-contained win-x64 ZIP 和 SHA256 发布资产。

## 最新 Release 资产：v0.1.0-preview.4
- Release 页面：https://github.com/xiaobei206/anjian/releases/tag/v0.1.0-preview.4
- ZIP：`Anjian2026-0.1.0-preview.4-win-x64.zip`
- ZIP 下载：https://github.com/xiaobei206/anjian/releases/download/v0.1.0-preview.4/Anjian2026-0.1.0-preview.4-win-x64.zip
- ZIP 大小：51,910,925 bytes。
- SHA256 sidecar：`Anjian2026-0.1.0-preview.4-win-x64.zip.sha256`
- SHA256 下载：https://github.com/xiaobei206/anjian/releases/download/v0.1.0-preview.4/Anjian2026-0.1.0-preview.4-win-x64.zip.sha256
- ZIP SHA256：`6b59ab27f825aa48b2ec5343cec826ad8b0ded759e9989af02d51f8b949f09aa`
- GitHub Release asset digest 与 ZIP 内容 SHA256 一致；sidecar 已随 ZIP 一同发布。
- Release asset IDs：ZIP `740099276`；SHA256 `740099275`。
- Release ID：`443178727`，prerelease=true。

## 历史 Release：v0.1.0-preview.1
- Release 页面：https://github.com/xiaobei206/anjian/releases/tag/v0.1.0-preview.1
- ZIP 大小：51,879,710 bytes。
- ZIP SHA256：`80bfbf3ddbebafaf6e835bc8fb29148760662418b2c57de79f7ab7dbe88e6164`。
- Release ID：`442961207`，prerelease=true。

## Windows CI 验证
- v0.1.0-preview.4 Release workflow run：`43253611158`，SUCCESS；Restore/Build/7 项测试/Publish/ZIP/SHA256/Artifact upload/Release creation 全部成功。
- v0.1.0-preview.3 Release workflow run：`43232898372`，SUCCESS；所有 job/step 均成功。
- 修复空值边界后的 CI run：`43232594517`，SUCCESS。
- 最终 Release 构建、7 项自动化测试、自包含 win-x64 publish、ZIP/SHA256 package、artifact upload、GitHub Release creation：全部 SUCCESS。
- 自动化测试：7 passed / 0 failed / 0 skipped（测试项目现有 7 项）。
- 测试包括：播放次数/轮间隔默认值、100 轮 99 次间隔、0 无限循环、边界值校验；Lua 5.4 版本确认、Lua 调用 C# 全局函数、Lua instruction-count hook 触发。
- self-contained win-x64 publish：SUCCESS。
- ZIP/SHA256 package：SUCCESS。
- Release ZIP SHA256 已通过实际解包 Actions artifact 并运行 `sha256sum -c` 核验。
- 正式 prerelease v0.1.0-preview.3 已成功创建（历史版本）。
- 第一轮 Release 构建曾因 `MainForm.cs` 的 nullable warning-as-error 失败；加上 `_current is null` 守卫后，修复提交 `2eacde0c12f9616e4f37d209c929c1b473edab43` 的 CI 通过，随后以新 release commit 完成发布。

## 仍未完成（务必如实）
- 用户 Windows 实机手工验收：NOT RUN。
- 2 小时稳定性测试：NOT RUN。
- Windows 10/11 干净环境、DPI、多显示器负坐标、复杂拖拽及具体目标程序权限兼容：NOT RUN。
- 录制采用低级 Hook，需在用户电脑确认 Hook 采集工作正常、不会漏录或重复。
- 代码折叠、独立 API 命令面板未实现；断点/单步/变量窗/调用栈属于下一阶段 M4。M3 Windows 桌面手工验收仍为 NOT RUN。
- 没有可视化动作列表/动作参数编辑 UI，录制结束直接生成 Lua。
- 尚未实现图像识别/OCR、标准控件读写、后台输入、插件或云同步。M6 的坐标转换 API 已实现，但 M5 Recorder 仍固定记录虚拟桌面屏幕物理像素，持久窗口选择器及相对窗口坐标录制/回放重定位尚未实现。

## preview.4 本轮变更与验证边界
- preview.3 的“紧凑/完整模式”是同一个 MainForm 切换面板，导致悬浮窗并非独立窗口；本轮已拆为独立 FloatingForm。
- 默认启动隐藏主窗口，仅显示悬浮窗和托盘图标；主窗口 ShowInTaskbar=false。打开完整界面时显示主窗口，不隐藏悬浮窗。
- 主窗口右上角 × 与“返回悬浮窗”都隐藏主窗口并移除任务栏按钮，不退出进程；托盘菜单负责恢复窗口或退出。
- 悬浮窗可直接设置循环次数与轮间隔，并与主窗口共用设置及持久化。
- 自定义多尺寸 ICO 已提交为 `src/Anjian.App/Resources/Anjian2026.ico`；csproj 设置 `ApplicationIcon`，主窗体、悬浮窗和 NotifyIcon 使用相同 EXE 图标资源。Windows CI 已成功编译，实机小尺寸可辨性仍待用户确认。
- Windows CI 编译、7 项自动化测试、self-contained 发布、ZIP/SHA256 和 Release 创建：PASS（Actions run `43253611158`）。
- Windows 10/11 实机手工验收：NOT RUN；CI 成功不代表桌面窗口、托盘、Hook 或图标实机通过。

## M1 实机验收待办（仍为 NOT RUN，不阻塞后续 M4 开发）
用户下载 v0.1.0-preview.4，在 Windows 10/11 实机验收：
1. 默认启动确认只显示悬浮窗与托盘图标，任务栏没有主窗口按钮。
2. 点击悬浮窗“完整界面”打开主窗口，确认悬浮窗一直留在桌面；关闭主窗口右上角 × 后，主窗口任务栏按钮消失，悬浮窗和托盘仍在。
3. 通过托盘右键菜单分别打开主窗口、显示/隐藏悬浮窗和退出；双击托盘图标应打开主窗口。若悬浮窗已隐藏，关闭主窗口后应只剩托盘图标，不应自动弹出悬浮窗。
4. 确认主窗口“返回悬浮窗”按钮存在且有效；点击后主窗口隐藏，任务栏按钮消失。
5. 在悬浮窗修改循环次数和轮间隔，在主窗口修改后反向确认同步；确认 0=无限、设置重启后持久化。
6. 检查 EXE 文件图标、任务栏主窗口图标、托盘图标和悬浮窗图标是否一致，16/32px 尺寸下是否清晰。
7. 回归只录点击、录制暂停/续录、F10/F11/F12、播放/停止和原有 Lua API；将失败步骤、截图和日志交给下一会话。

## M2 完成：单一机器可读 API 注册表与函数手册生成（Windows CI 已验收）

### 实现基线
- M2 实现提交：`0f7a80c24f18789833d7fbb92e4979da65ed261d`。
- 本次 CI 验收对应的最新主线代码基线：`fdff7c14398485fa30725e3ea283c03a545c97a6`。
- 唯一机器可读来源：`src/Anjian.Core/Api/lua-api-registry.json`，共 22 个**当前真实实现**的公开 Lua 函数。
- `LuaApiRuntime.Register` 根据注册表建立 Lua 全局包装；内部 IPC bridge 在闭包建立后隐藏，不作为公开 API。
- `tools/Anjian.ApiDocsGenerator` 支持 `--write` 和 `--check`；函数手册第 2 节由注册表生成。注册表另提供分类、签名、参数 schema、默认值、范围、返回契约、错误、暂停/取消、坐标/权限语义、示例、实现状态和 API 版本，供后续 IDE/命令面板读取。
- 当前注册表仅含 22 个真实函数；窗口、图色、OCR、后台控制等均保持“目标设计”，不作为当前可调用能力。

### Windows CI 验收证据
- 初始 M2 Actions run `43271693389` 在 Build 阶段暴露测试代码 nullable tuple 类型不一致（CS8620）；后续修正后 Build 通过。
- 随后的契约测试 run `43272945064` 发现 Lua 中文字符串在测试中变为问号；提交 `ac034aa10b1c8b1acae25a297d4e20928d9fe7f4` 将 Lua state encoding 明确设为 UTF-8。
- 最新主线代码提交 `fdff7c14398485fa30725e3ea283c03a545c97a6` 对应 Actions run `43273917056`：**SUCCESS**。
- 最新成功运行实际步骤：Restore PASS；Release Build PASS（0 errors）；自动化测试 **12 passed / 0 failed / 0 skipped**；API 手册 `--check` PASS；win-x64 self-contained publish PASS；ZIP/SHA256 打包 PASS；Actions artifact 上传 PASS。
- 该次 push 不是 `release:` 提交，因此 Read release version / Create GitHub Release 两个发布专用步骤按条件跳过；本轮没有创建新 Release，已发布的 preview.4 资产不变。
- Actions 记录：<https://github.com/xiaobei206/anjian/actions/runs/43273917056>。

### 验收边界与未完成事项
- 当前执行容器没有 .NET SDK（`dotnet --info` 返回 command not found），本地 Restore/Build/Test：NOT RUN；实际构建和 12 项自动化测试由 Windows GitHub Actions runner 执行并通过。
- M2 完成时，编辑器补全/签名提示和命令面板 UI 尚未实现；随后 M3 已交付注册表驱动的编辑器补全与签名提示，独立命令面板仍未实现。
- M1 Windows 10/11 窗口/托盘/Hook 实机验收、DPI/多显示器、复杂拖拽和 2 小时稳定性仍为 NOT RUN。CI 成功不等于桌面实机验收成功。

### M3 完成：完整 Lua IDE 基础（Windows CI 已验收）

- 最终 M3 代码验收基线：`71fd493cad0fbed1451a4ffa46192752a18bd0a6`。
- Windows Actions run `43282795272`：SUCCESS；Restore PASS；Release Build PASS（0 warnings / 0 errors）；自动化测试 `20 passed / 0 failed / 0 skipped`；API handbook `--check` PASS；self-contained win-x64 publish、ZIP/SHA256 和 artifact upload 均 PASS。
- 实现位于 `Anjian.Core.Editor.LuaEditorService` 和 WinForms `LuaEditorControl` / `ScriptSearchReplaceForm` / `MainForm`。功能包括语法高亮、行号、API 补全、活动参数/签名帮助、查找替换、多标签保存/重开、语法诊断和错误行导航。
- API 候选、签名和高亮只读取 `LuaApiRegistry.Load().Categories` / `.Apis`；不复制函数定义，只列出状态为 `implemented` 的 API。独立 API 命令面板未实现。
- 语法检查调用 NLua `LoadString` 编译 chunk，不执行脚本；高亮扫描移至后台线程并以文本版本号丢弃过期结果。签名提示统计嵌套 `()`、`[]`、`{}`，不会把表构造器或内层调用中的逗号误判成外层参数分隔符。
- Windows 自动测试 20 项全部通过；本地 Restore/Build/Test 为 NOT RUN（容器无 .NET SDK）。桌面实机验收与 1MB 文件/DPI/中文日文输入人工验证仍为 NOT RUN。
- 早期有一项签名提示测试因测试夹具逗号位置错误失败；修正夹具后最终 run `43282795272` 通过。前期错误不隐藏，最终状态以该次最新成功运行作为验收依据。
- M3 未创建 Release，v0.1.0-preview.4 Release ZIP/SHA256 未改变。代码折叠不在该 handoff 强制范围，功能矩阵 F04 仍为部分实现；M4 调试器未实现。

### M4 完成记录（基础调试器 CI DONE；桌面实机验收 NOT RUN）

M4 基础调试器已在 main 实现并通过 Windows CI。具体实现提交与早期失败/修复记录见 docs/DEVELOPMENT-LOG-CN.md 的 M4 小节。

#### 已交付
- Core LuaDebugSession 使用 NLua/KeraLua 真实 Line + Count hook，支持首个执行行暂停、文件/行号断点、Continue、Step Into / Step Over / Step Out 和 Run To Cursor。
- 暂停后获取当前执行行、调用栈、局部变量和可读全局变量；快照转成纯文本数据后通过命名管道发送给 UI。Lua state 始终留在 worker 执行线程。
- 编辑器 gutter 断点圆点、当前执行行标记；MainForm 调试控制、调用栈和变量窗。
- 标量变量安全格式化；32 栈帧、每帧 96 局部变量、100 全局变量、512 字符上限；table/function/thread/userdata 不展开，不执行任意 watch 表达式。
- 正常播放继续使用既有 worker IPC / Count hook / 暂停检查点；调试停止路径终止 worker，并走输入释放和 UI 状态清理。

#### Windows CI 验收
- 最终验证 main 代码基线：ab90b96f59deb173874c8aba819a9fc95288f8cf。
- Windows Actions run 43291175992：SUCCESS（https://github.com/xiaobei206/anjian/actions/runs/43291175992）。
- Restore PASS；Release Build PASS（0 warnings / 0 errors）；自动测试 25 passed / 0 failed / 0 skipped；API handbook --check PASS；self-contained win-x64 publish PASS；ZIP/SHA256 PASS；artifact upload PASS。
- 发布包 SHA256：1d37a3210e22646fd781a2803a17f40058d97f5c1e16845cef7d5d24059af8c4；Artifact ID 13976519155；ZIP 51,794,098 bytes。
- Read release version / Create GitHub Release 因不是 release 提交而跳过；未创建新 Release，preview.4 资产不变。
- 本地容器没有 .NET SDK，本地 Restore/Build/Test：NOT RUN。Windows CI 是唯一实际构建/测试证据。
- M1 preview.4、M3 编辑器与 M4 调试器的 Windows 桌面手工验收仍全部 NOT RUN；不把 CI 当作实机验收。

#### 已知限制（不阻塞进入 M5）
- 断点只保存在当前打开标签的内存模型中，关闭标签/退出程序不会保留。
- 暂无命名 Watch 表达式、table/object 展开和复杂对象属性求值。
- M4-01 至 M4-10 的桌面测试仍需在用户 Windows 10/11 环境执行。

### M5 唯一任务定义（本会话已执行，范围留档）

严格按 docs/PRD-CN.md 第 2.3 节、docs/RECORDER-DESIGN-CN.md、docs/FEATURE-MATRIX-CN.md 的 F09/F10 和 docs/ROADMAP-CN.md 实施 M5。不得把当前简单的 Lua 文本生成或原始事件列表冒充结构化可视化动作列表。

#### 开始前必须先读现状
1. 核对 main 最新 HEAD，再读取 AGENTS.md、README.md、docs/SESSION-HANDOFF.md、docs/ROADMAP-CN.md、docs/RECORDER-DESIGN-CN.md。
2. 检查当前 GlobalRecorder、RecordedInputEvent、MainForm 录制流程、现有 Lua GenerateLua() 以及全部 recorder / input tests。以现有 hook 录制能力为基线，保留已工作的暂停/续录、click-only、注入事件过滤、鼠标移动合并、Down/Up 配对、溢出状态和脚本播放功能。
3. M1 的 Hook / Raw Input / DPI 实机可靠性验收仍为 NOT RUN；不要借 M5 重写输入采集器或声称 Hook 实机已通过。优先完成动作模型、UI、持久化和生成区同步。

#### M5 唯一实施范围
1. 建立结构化 RecorderDocument / RecorderAction 模型，与原始 RecordedInputEvent 分层；actionId 稳定，明确类型、参数、上一动作后的间隔 delayMs、按键/鼠标 holdMs、原始事件引用和来源元数据。
2. 从原始事件转成可编辑动作，保留单调时间戳与原始事件；Down/Up 配对后形成 MouseClick / KeyPress，holdMs 与相邻动作间隔独立；Move 按现有合并策略形成 MouseMove，Wheel/组合键等顺序不能丢。
3. 提供可视化动作列表基础 UI：插入、删除、复制、排序、分组、注释、启用/禁用、参数编辑、延时编辑和批量编辑；布局应适合大动作量，避免每条事件都触发完整重绘。
4. 允许选中动作生成/更新可读 Lua 代码；采用 docs/RECORDER-DESIGN-CN.md 指定的 generated block ID / schemaVersion 标记，支持动作列表与代码视图定位/关联。
5. 必须实现同步保护：用户只编辑生成区块外代码时保留用户代码；手工改动 generated block 后标记 dirty/detached；从动作列表重生成前展示将被替换的区块预览并要求明确确认，禁止静默覆盖。首版不做任意 Lua 反向解析。
6. 保存 <script>.actions.json，至少含 schemaVersion、recorderVersion、scriptId、recordingId、createdAtUtc、coordinateSpace、events、actions、generatedBlockId、synchronizationState；使用安全写入和显式 schema migration，不兼容时不得覆盖原文件。
7. 步骤试运行至少能对选中/指定动作执行有边界的预览或单步执行，并明确提示输入风险；不能绕过既有执行/停止与 ReleaseAllOwnedInputs 清理。
8. 原始事件、动作列表、generated Lua block 三者必须有可测试的稳定关联；未知/无法配对事件不能被静默丢弃。

#### 自动化验收
- Raw event -> action 的顺序、稳定 ID、Down/Up 配对、holdMs、delayMs 独立计算、负坐标、Wheel/KeyDown/KeyUp 与移动合并规则。
- 动作插入/删除/复制/重排/参数和 delay 编辑后的模型状态。
- disabled action 不生成执行语句；注释/分组/元数据按设计保存。
- actions.json round-trip、schemaVersion 检查、兼容迁移失败时原文件不被覆盖。
- generated block ID/schema 稳定；块外手写 Lua 保持不变；块内手改进入 detached/dirty；重新生成必须 preview + confirm，取消不修改脚本。
- 生成代码能够被 NLua 编译；点击 holdMs 不重复生成同一段 Delay，动作间隔与 holdMs 不混淆。
- Windows CI 运行所有 tests、API handbook check、self-contained publish、ZIP/SHA256 和 artifact upload；CI 不代替 M1 实机采集 Hook 稳定性验收。

#### M5 完成条件与边界
- 编辑动作后，动作列表中的结构化数据和生成区代码一致。
- 手工修改 generated block 可检测脱离同步，重生成有差异预览和显式确认。
- 块外手写 Lua、原始事件和动作元数据不被覆盖或丢失。
- F09/F10、Recorder 设计文档、用户指南、路线图、development log 与 handoff 同步更新；main 小步提交，不建长期分支或 PR。
- M1、M3、M4、M5 Windows 桌面验收仍为 NOT RUN，不因 M5 CI 成功而标记实机通过。



## M5 本次交付结果（2026-11-13）

- 功能主体提交：eedc1f89329e2ebfbf9ccadb54b529c954fe248d；sidecar 名称修正：2093aa24c4cf49b89c902c164171ae6e8835daec；CI 编译错误修复：ec06cf1d3a0e847267b9de251d5a6ff26c710c4f（当前 main HEAD）。
- Windows CI run 43295924472：SUCCESS。Restore PASS；Release Build 0 warnings / 0 errors；Core Tests 37 passed / 0 failed / 0 skipped；API 文档一致性检查 22 个当前公开函数 PASS；self-contained win-x64 publish PASS；ZIP/SHA256 与 artifact upload PASS。Workflow：[run 43295924472](https://github.com/xiaobei206/anjian/actions/runs/43295924472)。Artifact ID 13979855824，51,827,155 bytes；内部 ZIP SHA256 `ddc08cfd78503d9e5e5c7995ff1202e3bc36abfcf1de6fa3ae9cc35a5baf531f`。CI runner 为 Windows Server 2025，不等于用户 Windows 桌面实机验收。
- CI artifact：Artifact ID 13979855824，51,827,155 bytes；内部 ZIP 为 Anjian2026-0.1.0-preview.4-win-x64.zip，SHA256 ddc08cfd78503d9e5e5c7995ff1202e3bc36abfcf1de6fa3ae9cc35a5baf531f（已从 artifact 中读取 .sha256 并重新计算校验一致）。该文件是 main CI 临时产物，不是正式 Release；官方 preview.4 Release ZIP/SHA 未更改。
- 首轮 run 43295605802 与 sidecar 修正后的 run 43295786937 均 Restore PASS、Build FAIL（0 warning / 3 errors）；CS1012 字符字面量错误两处、CS1674 DirectoryInfo 生命周期错误一处，已在 ec06cf1 修复。两次失败和根因均在 DEVELOPMENT-LOG-CN.md 留档。
- 本地执行容器没有 .NET SDK（dotnet --info 返回 command not found），本地 Restore/Build/Test：NOT RUN；实际构建和 37 项自动化测试由 Windows GitHub Actions 执行。
- Windows 桌面手工验收 M5-01 至 M5-10、M1 Hook、M3 编辑器、M4 调试器均保持 NOT RUN；CI 不等于桌面实机验收。
- 本次不创建新 Release；v0.1.0-preview.4 官方 Release/ZIP/SHA 不变。

## M6 交付结果（main 已合并；桌面验收仍待执行）

- 代码主体提交：`3479491d3e9f3c16dcab8f78399a5811f73f9fe1`。
- 函数手册/规格同步：`8c143982ab49bf87c3461981205fe6d00757562d`。
- 子窗口屏幕坐标与 isChild 修正：`c7cc5f7b2ee435ec13ece0846cf4ee5e64e03db5`。
- HWND 销毁/进程退出校验竞态修正：`4f9b2b2648fb154353ca55522e9c77fe77645506`。
- 最终 Windows CI run `43300620614` SUCCESS：Restore PASS；Build 0 warnings / 0 errors；61 passed / 0 failed / 0 skipped；API handbook --check、self-contained win-x64 publish、ZIP/SHA256、artifact upload 全 PASS。Artifact ID `13981497112`，51,857,145 bytes。无新 Release。
- API version 0.3；注册表和函数手册均有 40 个公开函数，其中新增 18 个窗口/坐标 API。
- M6-01..M6-10 Windows 桌面手工验收全部 NOT RUN。CI 不能替代多 DPI、多显示器、UIPI、窗口销毁/句柄复用和子窗口实际行为验证。
- M5 Recorder 当前 schemaVersion 1 仍固定 `virtual-desktop-physical-pixels`；Lua generated block 使用 `MoveTo`。窗口相对录制/回放绑定尚未实现，切勿将其描述为支持窗口移动后自动跟随。
- M6 不提供后台输入、控件值读写、UI Automation、图像/OCR，不绕过 UIPI/UAC/安全桌面。

## M7 交付结果与下一阶段接手指令（2026-11-13）

### M7 代码与 API 范围

- API registry version `0.4`，公开 Lua API 共 54 项，M7 新增 14 项：WindowBind、WindowUnbind、WindowGetCapabilities、ControlEnumerate、ControlGetInfo、ControlGetValue、ControlSetValue、ControlGetText、ControlGetSelection、ControlSelect、ControlGetToggleState、ControlToggle、ControlInvoke、WindowInput。
- UI Automation Control View 和 Value/Text/Selection/SelectionItem/Toggle/Invoke 标准 Pattern 已接入。能力按模式明确报告，控件操作只在目标 provider 实际暴露对应 Pattern 时调用。不支持的模式明确失败；密码字段 Value/Text 读取被拒绝。
- UIA/COM 与 AutomationElement 全部位于每轮独立 Lua worker。主进程通过本轮唯一 named pipe 把经过 M6 校验的短期窗口目标描述送给 worker；worker 每次控件操作前再次复核原 M6 window ID 和窗口身份。Lua 只获得随机 binding/control ID，不获得 HWND 或 UIA 对象。Stop 可以终止卡住的 worker。
- WindowInput 仍由主进程执行 SendInput，只接受 auto/foreground；验证窗口可见、未最小化、前台、键盘焦点和客户区坐标。通用后台鼠标/键盘/文本注入明确 unsupported，不静默回退、不伪造 PostMessage 输入，也不绕过 UIPI/UAC/安全桌面。
- 自建 WinForms `--automation-probe` 提供文本框、密码框、复选框、组合框、列表和 Invoke 按钮，供 Windows 桌面人工测试。

### 自动化验收

- **代码验收基线**：commit `7624763610ae83a74d73b71c048e179a98f0cd6f`，Windows Actions run [43350412493](https://github.com/xiaobei206/anjian/actions/runs/43350412493) SUCCESS。Restore PASS；Release Build 0 warnings / 0 errors；Core Tests 64 passed / 0 failed / 0 skipped；API handbook `--check` PASS；self-contained win-x64 publish、ZIP/SHA256 和 artifact upload PASS。Artifact ID `14003911442`，76,394,920 bytes；SHA256 `2054fcea5cde979d1edb59d884f932a2d5f7d8da4e3ac2cfe97c12cd713b047b`。
- 函数手册、架构、用户指南、Feature Matrix、Acceptance、README 与路线图的同步提交 `b9110f03e105a30f0d7609a66b7415a54474d9f3` 的完整 Windows Actions run [43350563985](https://github.com/xiaobei206/anjian/actions/runs/43350563985) 也 SUCCESS：64/64 tests；Build 0 warning/error；API handbook check、publish、package 和 upload 均 PASS。
- 当前状态更新提交（Roadmap M7 标记 DONE，用户指南调整了 M7 章节顺序）为 `52ef72957bb0202b116e8459261604bf2dc1da6d`，其重新验证的 Windows Actions run 为 `43350753487`；生成本 handoff 时该 run 正在运行。继续开发前核对该 run 最终状态和最新 main HEAD。
- **桌面验收仍未完成**：M7-01..M7-10 在 `docs/ACCEPTANCE-TEST-CN.md` 全部保持 NOT RUN。不要将 GitHub Actions CI 当成 Windows 10/11 UIA Pattern、焦点、UIPI、Stop 响应的实机通过证明。
- 本阶段未更新 VERSION/csproj 预览版本、未创建 Release；已发布的 v0.1.0-preview.4 资产未改变。

## M8 阶段历史交付快照（该小节记录 M8-CLOSE 前状态；已由下方 M8-CLOSE 更新）

### 实现范围
- 核心代码基线 1b87af713ff17c907feac6984bb96aa5dd8e821f。API registry 已从 0.4 升到 0.5，共 74 个真实公开函数；新增 20 个图像 API，函数手册从同一 registry 生成。
- 图像模型/纯算法：Anjian.Core.Imaging.PixelImage 与 ImageSearchEngine。覆盖像素/裁剪原点、RGB 容差、单点/多点找色、区域计数、透明模板、相似度、多结果、灰度、最近邻缩放、取消预算和连续确认跟踪。
- Windows 侧：WindowsImageService 使用 GDI+ CopyFromScreen，支持虚拟桌面、可见窗口外框/客户区、自定义区域、PNG 加载/保存、尺寸/原点、裁剪、像素读取与资源释放；每轮最多 128 资源/256 MiB，单张 32,000,000 像素。
- 模板匹配默认 similarity=0.95、grayscale=false、scale=1；scale 范围 0.25–4 最近邻；多结果上限 1000，单次模板匹配最多 50,000,000 次像素比较。Wait 系列支持 timeout/interval/stableCount 和 Stop 取消。
- 不新增第三方图像引擎或 OCR 依赖；不提供后台真实窗口像素保证，不绕过 UIPI/UAC/安全桌面。遮挡窗口获取的是当前桌面合成像素，不是可信后台截图。

### Windows CI 最终证据
- 代码提交 1b87af713ff17c907feac6984bb96aa5dd8e821f，run [43353801969](https://github.com/xiaobei206/anjian/actions/runs/43353801969) SUCCESS：Restore PASS、Release Build 0 warnings/0 errors、73/73 Core tests、API handbook check、self-contained win-x64 publish、ZIP/SHA256 package、artifact upload 均 PASS。
- CI artifact ID 14004658368，76,430,108 bytes。它是 workflow 临时产物；本会话没有创建 Release，也没有替换 v0.1.0-preview.4 官方 ZIP/SHA256。
- 中间迭代确实出现过测试类结构、手册生成区漂移和灰度/缩放接线相关失败；修正后最新 run 通过，详细 run/根因和修复记录见 docs/DEVELOPMENT-LOG-CN.md。
- Windows 桌面验收仍未执行：M8-01..M8-13 全部 NOT RUN；M8-14 的资源管理 UI/视觉调试层尚未实现。CI PASS 不代表截图/颜色/模板在真实显示器上已实测成功。

### M8 尚未完成（不可标 DONE）
- 图像资源管理 UI：模板列表、预览、命名、路径/版本校验和工程资源依赖联动。
- 视觉调试 overlay：截图区域、命中位置、similarity、颜色值和耗时展示。
- F17 剩余规格：RGB/十六进制辅助转换、颜色差值、颜色块识别及测试。
- M8-01..M8-14 实机验收与相关 Windows 边界测试。


## M8-CLOSE 本次实施结果（2026-11-13；Windows CI PASS，桌面验收 NOT RUN）

### 已实施
- Lua API registry 从 0.5/74 项升至 0.6/78 项，新增 RgbToHex、HexToRgb、ColorDifference、FindColorBlocks；编辑器补全/签名提示消费同一 registry，函数手册由 registry 生成。
- 新增 RGB/hex 解析与格式化、通道差/最大通道/Manhattan/Euclidean 距离、八邻域颜色块识别。颜色块最多扫描 4,000,000 个采样点、最多返回 100 项，支持取消。
- 新增脚本级 .images.json 清单和同名 .assets PNG 目录；提供 PNG 导入、列表、预览、命名、移除、相对路径复制、schema/路径/尺寸/文件大小/SHA-256 校验。PNG 文件上限 128 MiB；像素上限遵循 32,000,000。未知 schema 不覆盖。
- 图像资源 UI 只保留 PixelImage 不可变像素数据；Bitmap 仅在读取/绘制时临时创建并释放。静态 LoadImage 字面路径扫描与动态调用统计；资源仍被静态引用时禁止移除，移除条目不会删除 PNG 源文件。
- LoadImage 相对路径以脚本目录为基准。脚本重命名/复制/删除同步管理清单和资产目录，重命名/复制时更新静态资产目录引用并重基 manifest。
- 新增置顶透明点击穿透视觉调试覆盖层，事件显示截图区域、像素 RGB、FindColor/FindImage 命中框、颜色块、相似度和耗时。通过 SetWindowDisplayAffinity 尝试排除截图，并在 API 不支持/失败时向日志警告。
- 已更新 PRD、Feature Matrix、Roadmap、Architecture、Acceptance、Lua API、Function Reference、User Guide、README、Development Log 和本 handoff。没有进入 M9 OCR。

### 验证边界
- 本地容器未提供 dotnet SDK，不能进行本地 Restore/Build/Test。实现代码基线 `7dae8af881524fae08ddd5af281ef9d4e47a58c7` 的 run [43357876777](https://github.com/xiaobei206/anjian/actions/runs/43357876777) SUCCESS；文档同步 main HEAD `c2b30c1d9aabd876cd848d6203376060f294c34e` 的 run [43358188770](https://github.com/xiaobei206/anjian/actions/runs/43358188770) 也 SUCCESS：Restore PASS、Release Build 0 warnings / 0 errors、85/85 Core tests、API handbook `--check` PASS、self-contained win-x64 publish 与 ZIP/SHA256 上传 PASS。最新 artifact ID `14006903094`，76,460,671 bytes，30 天保留；[下载 artifact](https://github.com/xiaobei206/anjian/actions/runs/43358188770/artifacts/14006903094)。
- M8-01..M8-18 Windows 10/11 桌面验收全部 NOT RUN。CI 不等于用户桌面验收。尤其需要验证多显示器/负坐标、资源 rename/duplicate 路径、点击穿透和 SetWindowDisplayAffinity 实际截图隔离。
- 未修改 VERSION/csproj 预览版本；没有创建 Release；v0.1.0-preview.4 官方 Release ZIP/SHA256 不变。

## 下一会话唯一开发任务：M8-DESKTOP-ACCEPTANCE — 完成 Windows 10/11 实机验收

开始前重新核对 main 最新 HEAD，读取 AGENTS.md、README.md、本 handoff、docs/ROADMAP-CN.md、docs/ACCEPTANCE-TEST-CN.md 与 M8 源码；先核实本次 M8-CLOSE Windows Actions 最终结果。若 CI 失败，先根据真实失败日志修复并复验，不进入新功能，不建长期分支/PR。
1. 使用最新 main 的 Windows x64 CI artifact，在真实 Windows 10/11 桌面逐项执行 M8-01..M8-18；记录 OS build、DPI、显示器布局/负坐标、步骤、结果与日志。
2. 实测资源 UI 的 PNG 导入/预览/重命名/校验/依赖扫描；测试脚本重命名/复制/删除后资源路径和 manifest；覆盖未知 schema、路径穿越、文件大小/尺寸/hash 损坏。
3. 实测视觉调试覆盖层置顶、点击穿透、关闭后不拦截输入、截图区域/像素颜色/命中相似度/耗时、多屏负坐标；核实 SetWindowDisplayAffinity 返回值与实际截图内容，失败要记录。
4. 实测 RgbToHex、HexToRgb、ColorDifference、FindColorBlocks 八邻域/step/minPixels/maxResults/取消，并回归 Capture/FindColor/FindImage/Wait 相关 API。
5. 只有通过真实桌面验收的项目才可标 PASS；失败项记 FAIL，未测项保持 NOT RUN。全部关键范围通过后才可把 M8 标 DONE，再由后续 handoff 决定是否进入 M9。不要修改 VERSION，不创建 Release，preview.4 官方资产保持不变。
## 2026-11-13 会话执行记录：M8-DESKTOP-ACCEPTANCE 环境阻塞

- 本轮重新核对的 main 起始 HEAD：`b06910f93a40780c3ffa10a04d64d41d176b2767`。当前唯一任务仍是 M8-01..M8-18 的 Windows 10/11 桌面实机验收；没有擅自进入 M9 或增加新功能。
- 已核查 M8 代码基线 `c2b30c1d9aabd876cd848d6203376060f294c34e` 对应的 Windows Actions run [43358188770](https://github.com/xiaobei206/anjian/actions/runs/43358188770)：Windows Server 2025 runner，Restore、Release Build（0 warnings / 0 errors）、85/85 Core tests、API handbook check、win-x64 self-contained publish、ZIP/SHA256 与 artifact upload 全部 SUCCESS。Artifact ID `14006903094`，76,460,671 bytes；该 artifact 于 2026-12-13 UTC 到期。该 CI 仅验证构建与自动化测试，不是桌面实机证据。
- 本轮执行容器为 Debian 13 x86_64；`dotnet` 不存在，且没有 Windows / PowerShell 桌面会话，也没有对用户 Windows 桌面的交互访问。因此本轮没有运行程序或执行任何 M8 手工步骤。**M8-01..M8-18 全部保持 NOT RUN；没有项目可标 PASS，也没有因未执行而标 FAIL。**
- 未修改源码、API、VERSION 或 preview.4 Release 资产。M8 仍为 IN_PROGRESS；桌面实测门槛未满足。

### 下一会话唯一任务（保持不变）

继续 M8-DESKTOP-ACCEPTANCE。执行前先核对 main HEAD 和本文件，再读取 `docs/ACCEPTANCE-TEST-CN.md` 的 M8-01..M8-18 全部步骤。只有在获得可交互的 Windows 10/11 桌面（或收到可追溯的用户实测环境、步骤、日志/截图证据）后，才能逐项执行并改变状态。若仍只有远程 GitHub/CI 能力，必须继续保持全部 NOT RUN，不得用 Windows Server CI、静态代码审查或理论推断替代；不要进入 M9，不修改 VERSION，不创建 Release。


## 当前策略覆盖：先完成 M9–M14 全部开发，再集中实机验收（用户决定，2026-11-13）

本节优先于文档中早先的“M8-DESKTOP-ACCEPTANCE 是下一任务”记录。用户最新明确要求：直接调整文档进入 M9，先整体开发完成，之后由用户统一进行 Windows 实机验收。

- M1–M13 桌面实机验收不再阻塞下一阶段开发；所有尚未执行项目继续保持 NOT RUN。M8-01..M8-18 不改为 PASS。
- M14 所有功能开发完成后集中执行 Windows 10/11 实机测试；M14 正式 1.0.0 Release 仍必须等待这轮验收。
- M9 首轮 API registry 已升为 0.7 / 81 项，新增 OCR、FindText、WaitText；Core DTO/interface、PaddleOcrNet 2.4.0 CPU 适配器、离线模型管理器、PNG IPC 已进入 main。
- 模型下载由用户点击“OCR 模型”显式触发；Worker 识别使用 Offline=true，缓存位置 `%LOCALAPPDATA%\\Anjian2026\\Ocr\\Models`。当前真实识别质量和桌面运行表现未验收。

### 当前 M9 代码检查点
- API/Core 起始实现提交：`87d4bf8799e60ba65c16a8b0f88c61c38c7d6e9a`。
- App 引擎/模型 UI/IPC 接入提交：`3858fd62e441bdd351bbd67b72eba64e6cf13c7b`。
- MainForm 与 API 测试修复：`7da4316652e7d09e907b92c0f48b691d8ec8a288`。
- Core OCR 测试 using 修复：`c9f51d5c7c7917e284336acda9dac88cfe5211c5`。
- 产品/架构/API/路线图策略文档：`63a98d7de2ca0f81804a6a635c9829538ce1e9da`。
- 函数手册 API 0.7 生成区：`95558736a2e0ebf8af5228217450e0bdb51d73da`。
- M9 完整 README/使用说明/验收/交接日志正在本次会话继续完善；必须重新核对当前 main HEAD。

### 下一会话唯一任务
先核对本次 M9 首轮代码提交触发的 Windows Actions 结果。若失败，读取真实 job log 并修复；如果通过，按剩余 M9 目标继续补充功能/测试并把 M9 实现与 CI 状态更新为 DONE（桌面验收 NOT RUN、统一延期），然后进入 M10 参数、配置与脚本自定义 UI。不要回头要求先执行 M8 桌面验收，不修改 VERSION，不创建 Release。


# 最新交接状态（覆盖此前 M8 阻塞交接，更新于 2026-11-13）

用户最新决策：M1–M13 的 Windows 桌面实机验收统一延期至 M14 全部功能开发完成后集中执行。不得再要求先完成 M8 桌面验收才进入 M9/M10。未执行的 M8-01..18、M9-01..08 等验收行继续保持 NOT RUN；CI 不等于桌面 PASS。M14 正式 1.0.0 发布仍必须等待用户集中实机验收及缺陷修复。

## 当前代码和 CI 状态
- M9 最终代码基线：`41e6d56a6485114e44cc9453b97ce05dd04f4c5d`；本轮状态文档提交：`c4482bf315178daf1886b83af2299e7d5ddaa3fd`。接手仍须重新查询当前 main HEAD。
- M9 OCR 已完成实现与 Windows CI：run [43363150335](https://github.com/xiaobei206/anjian/actions/runs/43363150335) SUCCESS（Windows Server 2025）：Restore PASS；Release Build 0 warnings/0 errors；90/90 Core tests；API handbook `--check` PASS；win-x64 self-contained publish、ZIP/SHA256、artifact upload 全 PASS。
- 临时 artifact：ID `14008744436`，87,078,010 bytes，30 天保留；不是 GitHub Release。没有修改 VERSION，preview.4 正式 ZIP/SHA256 不变。
- M9 核心：API 0.7 / 81 项；新增 `OCR`、`FindText`、`WaitText`；PaddleOcrNet 2.4.0 CPU adapter；用户显式下载 ch/en/japan 模型；Worker 识别严格离线；16 MP/64 MiB 输入上限；文本置信度/矩形/polygon/屏幕绝对坐标；WaitText 使用 ExecutionGate 可暂停/取消 delay，F12 Stop 终止 Worker；模型管理窗下载取消后安全关闭。
- 本地 Debian 13 环境没有 .NET SDK；本地 Restore/Build/Test NOT RUN。Windows Actions 的 90/90 是自动化证据；M9-01..M9-08 桌面验收全部 NOT RUN，M1–M13 统一延期 M14。
- 实际 CI 失败与修复记录见 DEVELOPMENT-LOG-CN.md；最终以 run 43363150335 为准。

## 下一会话唯一开发任务：M10 — 参数、配置与脚本自定义 UI

### 开始前
1. 核对 main 最新 HEAD，读取 AGENTS.md、README.md、本 handoff、ROADMAP、PRD 第 2.9/2.13 节、FEATURE-MATRIX 的 F20/F26、ARCHITECTURE、DECISIONS、Lua API registry 与当前 Worker IPC。
2. 依据用户冻结的完整产品目标实施 M10；不回到 M8 实机验收，不缩减完整产品范围，不创建长期分支/PR。
3. 开始编码前先完成参数 schema / 持久化 / 启动前校验 / Worker 参数注入的 ADR 和规格，避免执行用户脚本来读取参数声明。

### M10 唯一实施范围
- 建立版本化参数 schema：稳定参数 ID、机器名、中文标签、类型、必填、默认值、最小/最大值、选项、描述、验证规则、排序。
- 支持文本、整数、小数、布尔、下拉/单选、多选、文件路径、目录路径、热键和可选颜色值。
- 参数定义和运行值分离；建议使用脚本旁版本化元数据文件或独立安全描述，不能为读取参数定义而提前执行用户 Lua 脚本。
- 启动前统一校验，失败时指出字段和规则且不得启动 Worker；成功后通过类型安全 IPC 注入独立 Lua Worker，提供只读参数表入口，用户输入绝不能通过 Lua 字符串拼接执行。
- UI 根据同一 schema 自动生成通用表单；支持参数新增/编辑/删除/排序、默认值、范围/选项、必填与帮助说明。
- 支持保存/载入、恢复默认、JSON 导入/导出、原子写入、备份、损坏恢复和 schema migration；未知未来 schema 不得覆盖原文件。
- 敏感参数默认不写普通日志；开发环境与未来小精灵打包器共用 schema/validator。
- 只有当新增 Lua 公开 API 确实必要时才更新 registry/API version；按 M2 纪律同步函数手册、编辑器提示、测试和日志。

### M10 自动化验收
- 每种类型的有效/无效值、默认值、必填、边界、选项、多选、文件/目录/热键/颜色校验。
- 参数 schema/配置 round-trip、原子写入、备份、损坏恢复、未知 schema 与迁移失败原文件保护。
- 参数经 Worker IPC 后保持实际类型，不发生代码注入；启动校验失败时 Worker 不启动。
- UI 与 Lua 读取完全相同值；敏感项不进入普通日志。
- Windows CI Restore/Build/Core Tests/API handbook/self-contained win-x64 publish/ZIP/SHA256 必须全 PASS。真实 WinForms 表单、DPI 和交互仍延后至 M14 集中实机验收。


# 最新交接状态：M10 参数、配置与脚本自定义 UI（更新于 2026-11-13）

## 当前 HEAD 与证据
- 开始 M10 时核对的 main HEAD：`a5aa0aa21ca16e95fc8e3a1ace5c7b8d7d0925d8`。
- M9 Windows CI 最终 run [43363150335](https://github.com/xiaobei206/anjian/actions/runs/43363150335) SUCCESS：Restore、Build 0 warnings/errors、90/90 tests、API handbook check、win-x64 self-contained publish、ZIP/SHA256、artifact upload 全 PASS。M9 真实桌面验收 NOT RUN。
- 用户决定 M1–M13 桌面验收统一延后到 M14 完成后；不阻塞开发，不得把 NOT RUN 标为 PASS。

## M10 唯一开发任务
先实现 ADR-022 规定的 Core 参数 schema / validator / 配置持久化与测试，再接 Worker typed IPC 和只读 `Args`，最后完成 WinForms 参数设计器/自动表单、导入导出和完整 CI。规格已经追加到 ARCHITECTURE、DECISIONS、ROADMAP、FEATURE-MATRIX、ACCEPTANCE 和 DEVELOPMENT-LOG；M10 仍 IN_PROGRESS。

- Sidecar：`<script>.params.json`，schemaVersion=1；不得执行 Lua 来读取参数定义。
- 类型：text、integer、number、boolean、select、multiSelect、filePath、directoryPath、hotkey、color。
- 用户运行值独立于 schema，存储在用户数据目录；启动前统一校验，错误不创建 Worker。
- Worker 通过独立 JSON 结构化载荷接收类型化值，脚本使用只读 `Args`；禁止将用户值拼接成 Lua 源码。
- 配置需原子写入、备份、损坏恢复、JSON 导入/导出、版本迁移与未知未来 schema 保护；敏感参数不进入普通日志。
- 自动化验收 M10-01..M10-10 已列入 ACCEPTANCE，均 NOT RUN，桌面验收 M14 后统一执行。

### 下一步
1. 实现 Core DTO/schema loader/validator/config store；补齐每种类型与迁移/恢复/安全测试。
2. 把 validated values 通过 PlaybackRunner 的 worker-ready handshake 传递，Worker 注入只读 Lua `Args`。
3. 增加运行参数对话框和 schema 设计器；保存、恢复默认、导入/导出与敏感值保护。
4. 运行 Windows CI；记录真实 Restore/Build/Test/API docs/self-contained package/SHA 结果；不发布新版本。


# 最新交接状态：M10 实现与自动化收尾完成（更新于 2026-11-13）

## 当前 main / 代码与 Windows CI 证据
- 本轮开始时已核对 `main` HEAD：`3fffccd12d0d046ba1c1cada037e832f27a4c23a`；本轮实现最终代码 HEAD：`a923f1e02dce73629514da39bb10925b1271fc15`。
- 本轮六个代码/测试小提交均直接进入 `main`；未创建长期分支或 PR。
- 最终 Windows Actions run [43368224862](https://github.com/xiaobei206/anjian/actions/runs/43368224862)（run #218）针对最终代码 HEAD SUCCESS：Restore PASS；Release Build 0 warnings / 0 errors；Core Tests 102 passed / 0 failed / 0 skipped；API handbook `--check` PASS；self-contained win-x64 publish、ZIP/SHA256 与 artifact upload 全 PASS。GitHub Release 步骤 SKIPPED。
- 临时 artifact：`Anjian2026-preview-win-x64`，ID `14010789337`，87,099,638 bytes，archive SHA-256 `324ed884d9938548d2d35b94cd372a4bc46bf081f7abd1c77e17b28d1acf1323`，有效至 2026-12-13。此 artifact 不是 Release。
- GitHub Actions 日志含 Node.js 20 弃用提示；项目 Release Build 为 0 warnings / 0 errors。运行环境本地 Debian/Linux 无 .NET SDK，本地 Restore/Build/Test 均 NOT RUN。
- M10-01..M10-10 的真实 WinForms/DPI/交互/运行时桌面验收全部仍为 NOT RUN；M1–M13 桌面验收统一延期至 M14 后。v0.1.0-preview.4 不变，没有创建 tag 或 Release。

## M10 已完成的实现与自动化边界
- sidecar `<script>.params.json` schema v1 与独立 LocalAppData 用户配置；10 种类型和 Core 统一 validator、原子保存/备份/损坏恢复、JSON 导入导出、默认值和敏感输入处理。
- 相对文件/目录路径保留在配置中；存在性验证和运行时解析以脚本所在目录为基准，进入 Worker 的 Lua 参数路径为绝对路径。
- 参数名拒绝 Lua 保留字；非法路径、null schema 项和非有限范围被拒绝；设计器不会静默覆盖损坏或未来版本 schema。
- 播放与调试共用同一启动前参数收集/验证流程。用户取消、schema 无效或值校验失败均在 Worker 创建前结束；Worker 通过独立 JSON 握手接收 typed values，注入 `Args` 后再执行脚本。
- `Args` 常规顶层/嵌套写入和 `pairs(Args)` 遍历拿到的嵌套值写入均被递归代理拒绝；Lua `rawset` / `debug` 能绕过，故不是恶意脚本安全沙箱。
- Sensitive 遮蔽和导出确认不是加密；LocalAppData 用户配置仍为明文 JSON。应用普通日志不自动输出参数 payload，但脚本主动打印/抛出敏感内容仍可能把内容写入日志。
- 未新增公开 Lua API；registry 仍为 0.7 / 81 项，函数参考手册不需更新。

## 本轮代码提交链
- `026aa726a6cd935a0c71a98b505a1eb6e5b0d376` — validator 路径/保留字/运行时路径解析。
- `8722822fa9d8aaede8abd0d57d32a95e68873434` — `pairs(Args)` 递归只读代理。
- `938548b43c41dc2f89601113deef5de4bcd2db27` — Worker 启动参数路径规范化。
- `3bf90eab244026652e41deb8ae43256e73da942c` — 未来/损坏 schema 防静默覆盖与写入 flush。
- `ac661c3ea19aa0a3c2228291518e964d0be6a711` — Core 相对路径和非法值回归测试。
- `a923f1e02dce73629514da39bb10925b1271fc15` — Lua pairs 深层只读回归测试（最终代码 HEAD）。

## 下一会话唯一任务：M11 — 小精灵独立打包器与发布包

### 开始前
1. 重新核对 `main` 最新 HEAD，读取 `AGENTS.md`、`README.md`、本 handoff、`docs/ROADMAP-CN.md`、PRD 2.13、FEATURE-MATRIX F24/F25/F26、ARCHITECTURE、DECISIONS、API registry、现有脚本/图像/OCR 资源布局和 Worker 启动入口。
2. 以已冻结的 PRD 2.13 为完整范围；不得把“压缩 Lua 文件”或“复制普通主程序”称为小精灵，也不得把无法安全单文件化的文件夹包伪装成单 EXE。
3. 继续直接在 `main` 小步提交；不创建长期分支/PR，不修改版本号、不创建 Release，除非后续明确要求发布。

### M11 唯一实施范围
- 支持选择一个或多个入口脚本，递归/显式收集工程依赖、模块、图片模板、OCR 字库/模型（遵循显式资源策略）、参数定义和必需运行资源。
- 生成可独立运行的发布包：至少提供便携文件夹包和带清晰启动入口的 EXE/启动器包；输出清单必须真实列出伴随文件，不宣传技术上并非真正单文件的包。
- 每个小精灵可配置产品名、版本、图标、说明、作者/版权和可选 About 页面；输出 package manifest、文件清单、逐文件 SHA-256、最低 API/运行时版本和构建日志。
- 运行时提供按参数 schema 自动生成的参数 UI，直接复用 M10 Core schema/validator，不得再造第二套参数语义；保留默认值、范围、必填、敏感项和相对路径规则。
- 提供启动、暂停/继续、停止、状态/错误提示和可配置日志模式；支持循环次数、轮间隔、启动热键、自动启动、单实例策略、失败重试和退出清理。
- 打包前验证入口、schema、API 版本、依赖、资源路径与 hash；缺失/损坏资源在执行脚本前报清晰错误。
- 用户配置与包文件分离；升级不得静默覆盖用户值，schema 变化需备份/迁移/失败回滚。更新检查可关闭，不把云服务作为运行依赖。
- 新增公开 Lua API 才更新 API registry、函数手册、编辑器提示和契约测试；M11 不得改变 M10 既有参数语义。

### M11 自动化验收
- 一个/多个入口脚本能生成 portable folder 与 EXE/launcher 包；在干净 Windows x64 环境不依赖 IDE/开发配置运行。
- 递归依赖/图片/OCR/模块/参数定义收集准确；缺少、路径穿越、损坏或 hash 不匹配的资源在启动前被拦截。
- manifest 中名称/版本/入口/API 最低版本/文件列表/逐文件 SHA-256 与实际包内容一致。
- M10 全部参数类型、验证、配置隔离和敏感字段规则在小精灵界面保持一致；用户数据升级不被覆盖。
- 暂停/继续/停止、循环、热键、单实例、失败策略和退出释放行为有 Core/集成测试；Windows CI 的 Restore/Build/Test/self-contained package/SHA256/artifact 全 PASS。
- 桌面 UI/DPI/显示和真实输入验收继续按策略延期 M14；未执行项目必须保持 NOT RUN。

### 本会话之后的状态
- M10 实现与自动化已收尾，**下一会话只做 M11**；不回头要求先跑 M10/M8 桌面实测。
- M11 完成前不提前启动 M12；M14 正式 1.0.0 仍等待 M1–M13 集中桌面验收和缺陷修复。


---

## 2026-11-13 会话收尾状态覆盖（优先于上方旧交接内容）

- 本会话已完成 M11 实施范围；代码提交基线 7e2ed0f52c08b5b76069d405688c8af2aa0ec097，文档收尾与本 handoff 一并进入 main。
- 小精灵生成 portable folder + launcher ZIP；启动 EXE 是 self-contained apphost，必须保留 companion DLL、hostfxr/coreclr、Content，不宣传单文件。
- M11 已覆盖多入口、静态 require 递归、图像资源 manifest/assets、显式 OCR 缓存模型、M10 参数 schema/validator/UI、稳定包/入口 ID、用户配置隔离、启动/暂停/继续/停止、循环/间隔、热键、自动启动、单实例、失败重试、退出资源释放、manifest/hash/版本校验、构建日志和离线函数参考 Markdown。
- Windows 真实包 smoke 使用发布目录生成小精灵并启动改名 EXE，由包内 Worker 执行 require 与 Args；M11 桌面 WinForms/DPI/真实输入手工项仍 NOT RUN，统一延期 M14。
- Run 227（代码 HEAD a006103...）、Run 228（代码 HEAD 86de4e6...）和最终 Run 229（代码 HEAD 7e2ed0f...）均 SUCCESS；Run 229 的 Build 0 warnings/0 errors、Core tests 110/110、API handbook check、自包含发布、含离线函数参考 Markdown 的真实包/Worker smoke 1/1（33 秒）、ZIP/SHA256、artifact 上传全 PASS。最终 artifact ID 14014490869，87,162,650 bytes，digest sha256:6a7bfaee6d0af15f3e80a6c48071a62c8459e9c0db2e4fc450568e317bd6654b，有效期至 2026-12-13。
- 本地没有 .NET SDK；本地 Build/Test NOT RUN。未改版本/未发 Release；v0.1.0-preview.4 保持不变。
- PRD 2.14 HTML 离线参考手册仍待实现；当前发布包包含 Markdown。

## 下一会话唯一任务：M12 — 内置扩展 API 与插件系统

### 开始前
1. 核对 main 最新 HEAD，读取 AGENTS.md、README.md、本 handoff、PRD 中扩展/插件章节、ROADMAP、FEATURE-MATRIX、ARCHITECTURE、DECISIONS、API registry、当前 Worker/权限边界。
2. 只做 M12，不回头重做已通过 CI 的 M11；M11 WinForms/DPI/真实输入人工验收保留 M14。
3. 继续直接在 main 小步提交，不创建长期分支/PR；除非明确提出发布要求，不改 VERSION、不创建 tag/Release。

### M12 唯一实施范围
- 按 PRD/FEATURE-MATRIX 实现内置扩展 API：文件、剪贴板、对话框、进程、HTTP/JSON；定义准确的输入/输出/错误/超时/取消契约。
- 插件 manifest、插件包结构、稳定 ID、版本/API 兼容字段、来源和权限声明；加载/卸载和禁用流程。
- 最小权限模型：默认拒绝；明确授予文件根目录、剪贴板、进程启动、HTTP 域名/方法等能力；Worker 端执行强制检查，UI 展示权限审阅。
- 依赖 Worker IPC，主 UI 不执行用户 Lua/第三方插件代码；停止/超时/卸载时可靠取消并清理资源。
- API registry 为唯一来源；新增公开 API 同步生成函数手册/IDE 提示/命令面板和契约测试。
- 不做云商城、强制联网授权和远程执行；HTTP 是脚本声明授权的显式能力。
- CI 覆盖权限默认拒绝、授权/撤销、路径越界、进程退出/取消、HTTP 超时/取消、插件兼容性与损坏包拒绝；真实 WinForms/OS 交互留 M14。

### M12 开始前的约束
- 先读最新 handoff 与现有 API registry；避免重复实现 M8/M9/M10/M11 的现有能力。
- 对敏感 OS API 所有操作落实在 Worker/能力代理，不允许仅 UI 隐藏按钮作为权限边界。
- 每次增加 API 要同步 registry、FUNCTION-REFERENCE、IDE 补全/提示和自动化检查。


---

# 最新交接状态覆盖：M12-01 权限基础层已进入 main（2026-11-13）

本节优先于本文件上方所有历史 M12 任务段落。当前开发阶段仍为 M12，**M12-01 只完成权限请求/批准/Worker 注入基础，不表示文件/剪贴板/进程/HTTP API 或插件系统已完成**。

## 当前代码与验证基线
- M12-01 代码 HEAD：2a69776374abb9b472aa4db3b6bc5dd32c0f6a92。
- 最近四个代码提交（均直接进入 main）：e1940930（Core 默认拒绝权限策略 + ADR-024）、41474018（xUnit using 修复）、f378a600（主程序每次播放/调试前审阅 + worker-ready grant 载荷）、2a697763（WorkerLuaApi 接收 grant）。
- Windows Actions run [234](https://github.com/xiaobei206/anjian/actions/runs/43377155430) SUCCESS：Restore、Build（0 warnings/0 errors）、Core tests 122/122、API handbook check、自包含 win-x64 publish、小精灵包内 Worker smoke、ZIP/SHA256、artifact upload 全通过。Artifact ID 14014463955，87,176,294 bytes，SHA-256 c17f159787d8129031e97e364e991bec9a6ea3a72b9b612567867acd1d93d770。run 231 的漏引用失败已在 run 232 修复后全绿；run 233 同样通过。
- 本地无 .NET SDK，本地 Restore/Build/Test NOT RUN。Windows CI 不等于权限窗体实机验收。
- API registry 当前仍是 0.7 / 81 项，未新增公开 Lua 函数；FUNCTION-REFERENCE/IDE API 数据无需添加占位项。
- VERSION 和发布资产不变：v0.1.0-preview.4；无 tag/Release。M1–M13 真实 Windows 10/11 桌面验收继续 NOT RUN，统一延期到 M14。

## M12-01 当前代码范围
- Core：src/Anjian.Core/Permissions/ScriptPermissionPolicy.cs，schemaVersion=1、最大 256 KiB；请求/审批/grant 分离；路径根规范化、精确 EXE 路径、精确 DNS 主机/HTTP 方法、IDN、HTTPS 默认及明文 HTTP 独立授权；审批必须是请求子集；无审批为空授权。
- App：脚本同目录 sidecar 命名为脚本全名加 .permissions.json；主程序播放/调试前展示逐项审阅；默认全不勾选；取消不启动；无 sidecar 为空授权，损坏/未来 schema/重解析点拒绝启动；不持久保存授权。
- Worker：worker-ready 载荷携带 grant；WorkerProgram 注入 WorkerLuaApi。
- 重要缺口：目前没有新增扩展 OS API；WorkerLuaApi 还未使用 Grant 执行具体操作；PlaybackRunner 的主进程 IPC 二次授权没有实现；Core 词法路径比较不是符号链接/TOCTOU 防护；小精灵 Launcher 未接入 sidecar/审阅；插件 manifest 和生命周期完全待实现。

## 下一会话唯一任务：M12-02 — 文件/目录 API 与主进程 IPC 二次授权

严格只做以下顺序，不要跳过主机安全边界，也不要重做 M12-01：
1. 阅读最新版 handoff、ScriptPermissionPolicy、MainForm 审阅、WorkerLaunchPayload、WorkerProgram、WorkerLuaApi 和 PlaybackRunner dispatcher。
2. 先设计并实现主进程可独立执行的文件操作权限校验：Worker 侧调用前检查 Grant；PlaybackRunner 在处理每个 file IPC 命令时再次检查 Grant，未知/伪造/无授权命令默认拒绝。
3. 完成第一批受控文件/目录 API（读取文本、写入/追加文本、文件/目录存在性、枚举、创建目录、复制/移动/重命名、临时目录/文件按 PRD 语义选定）；定义 UTF-8、大小上限、路径相对基准、异常映射、暂停/Stop 取消行为。先实现少量闭合 API，不要一次堆全部功能。
4. 对路径使用规范化目录边界；对每一级现存父目录/文件检查 Windows reparse point/symlink。评估并避免最终打开时的检查-使用竞态；若无法可靠保证，缩小允许路径/文件打开方式并写明限制，不能把词法前缀检查冒充安全沙箱。
5. 加入 Core 策略测试和 IPC/Worker 契约测试：默认拒绝、批准根内读写、同名兄弟目录越界、.. 穿越、符号链接/reparse point 拒绝、拒绝超限文件、Stop 取消，以及伪造 IPC 不可绕过。
6. 只有当主进程二次检查已真正接入并有测试后，才新增文件类公开 Lua API；随后同一提交链更新 lua-api-registry.json / API version（预期按项目版本策略由 0.7 递增到 0.8）、自动生成 FUNCTION-REFERENCE、编辑器补全/签名提示和测试。
7. 普通主程序和小精灵都必须采用同一 Grant/Dispatcher 校验策略。此子任务先把小精灵缺失 sidecar 时固定为空授权；权限 sidecar 打包/逐次审阅可作为 M12 后续任务，但不可默认放宽权限。
8. Windows Actions Build/Test/API handbook/self-contained package smoke 必须全绿；桌面文件对话框/真实权限体验仍 M14 实机验收。

M12-02 验收前不做剪贴板、对话框、进程、HTTP 或插件加载。完成后 handoff 再指定下一个唯一子任务。不得改 VERSION、创建 tag/Release，不做 FIX 版本名。


---

# 2026-11-13 会话交接：M12-02 首批文件 API 已进 main，后续补齐仍属同一任务

## 本轮完成
- 执行前核对 main HEAD：初始 75d50b8514539795c0cf9082be7a3a714b2f8890；读取 AGENTS.md、README.md、本 handoff 和 ROADMAP-CN.md，唯一任务按原要求为 M12-02。
- 代码提交：ffd09bc（host 文件 IPC dispatcher）、186695d（目录/链接测试修正与硬链接拒绝）、41c64e8（首批 Worker Lua API / registry）、fcaa675（桥接 JSON 序列化修复）、e020f2a（生成手册一致性和 API version 测试）、fdfa70d（修正 API count 测试为 89）。
- 当前首批 API 版本：registry 0.8，共 89 项；新增 8 个文件 API：ReadTextFile、WriteTextFile、AppendTextFile、FileExists、DirectoryExists、ListFiles、ListDirectories、CreateDirectory。函数手册生成区已同步，IDE 自动补全/签名由同一 registry 提供。
- Worker API 对路径先解析并检查本次 Grant；PlaybackRunner 对每条 file/directory IPC 再检查本轮 Grant；unknown command fail closed。小精灵复用 PlaybackRunner，缺少授权时 Grant 为空。首批 API 不包含 copy/move/rename/temp。
- Windows 适配器采用逐级 OPEN_REPARSE_POINT 目录句柄、目录操作期间不共享删除、最终文件 no-follow handle、reparse/symlink 和多硬链接文件拒绝；文本 4 MiB、目录枚举 1000 项、严格 UTF-8、同目录 temp+flush+replace。
- CI 历史完整记录见 docs/DEVELOPMENT-LOG-CN.md。最终 Windows run [241](https://github.com/xiaobei206/anjian/actions/runs/43381018127) SUCCESS：0 warnings/0 errors、134/134 tests、API handbook、self-contained publish、package Sprite/Worker smoke、ZIP/SHA256、artifact upload 全 PASS；artifact ID 14016840736，SHA-256 db2b6ac754543d9ca821434bb3f3edc98b4735e58359ebe843a1c25d9a692534。
- M12-02 整体仍为 PARTIAL：复制、移动、重命名、受控临时文件/目录尚未实现；剪贴板/对话框、进程、HTTP、插件均未开放。Windows 桌面权限 UI / 真实交互 NOT RUN，留 M14。未修改 VERSION/AssemblyVersion/FileVersion；无 tag/Release，preview.4 不变。
- 本会话未本地执行 dotnet Restore/Build/Test；测试来源为 Windows Actions。不要把 run 241 CI 通过写为真实 WinForms/桌面验收通过。

## 下一会话唯一任务：M12-02B — 文件复制/移动/重命名与受控临时路径

严格按以下唯一范围推进，不要进入 M12-07（剪贴板/对话框）：
1. 开始先核对 main 最新 HEAD，并读取 AGENTS.md、README.md、本节 handoff、ROADMAP-CN.md；再复查 ScriptFileSystem.cs、ScriptFileSystemHostDispatcher、WorkerLuaApi.Files.cs、PlaybackRunner.DispatchAsync 和当前 8 个 API 的测试。
2. 补齐文件操作：优先 CopyFile(source, destination, overwrite?)、MoveFile(source, destination, overwrite?)、RenameFile(path, newName, overwrite?)；再实现受控临时文件/目录创建 API。函数名/参数应先按现有注册表命名规则确定，避免模糊 system temp 暴露。
3. 源文件路径必须同时通过本次 Grant 的读取根校验；目标目录/文件必须通过写入根校验。Rename/Move 也要同时核对源和目标授权，不能因源可写就允许移出批准根。覆盖目标前按 no-follow 方式检查并拒绝 reparse point、符号链接、多硬链接目标。
4. 临时文件/目录必须由调用者提供一个已获批的写入根/父目录，使用安全随机名称与 CreateNew/单级创建；返回完整路径。不得因为调用临时 API 而自动批准系统 TEMP 或扩大权限根。
5. Copy 使用目标目录同目录临时文件，设定并测试最大字节数、flush/提交前取消、目标覆盖语义；移动在同卷优先使用安全 rename，跨卷必须先完成受限复制并验证目标后再删除源，删除失败时返回清晰错误并保留可恢复状态；重命名只允许同一已批准父目录，不跟随链接。必须明确目录复制是否本轮不支持，首轮可以只支持普通文件。
6. 对每个新增操作都要在 Worker bridge 和主进程 dispatcher 双重检查；unknown/forged IPC fail-closed；暂停通过 PlaybackRunner checkpoint，Stop cancellation token 贯穿复制循环和提交边界。
7. 增加测试：source-read/destination-write 各自授权、目标越界、同名兄弟目录、覆盖开关、同卷/跨卷失败路径、已存在链接/硬链接拒绝、临时名碰撞与目录穿越、大小上限、复制中取消、目标原子提交和未知 IPC 拒绝。Windows Actions 的 Build/Test/API handbook/self-contained package smoke 全绿后再更新矩阵与 handoff。
8. 完成这批 API 后把 registry 从 0.8 递增为 0.9，自动重生成 FUNCTION-REFERENCE 并验证 IDE 仍读取同一注册表。只改 main，小步提交；不创建分支/PR、不改产品 VERSION、不建 tag/Release。

验收完成后，本任务再交给下一 M12 子任务；在此之前禁止启动剪贴板、对话框、进程、HTTP 或插件功能。


---

# 2026-11-13 最新交接覆盖：M12-02B 已实现并通过 Windows CI

本节优先于上述历史 M12 handoff。当前唯一任务 M12-02B 已按 handoff 范围完成并通过最终 Windows Actions。下一会话唯一任务转为 M12-07。

## 当前 main
- 初始 HEAD：`484cbe941258de8b12fd924f00d89d1a54e3bdc3`。
- 功能提交：`3a2f23ab66a06491fe5cbf3ca16696751fc8b860`，修复提交：`32d28073ca1bc1030f49f1d7d86d344d7791a788`、`057f400657c8ebaf673eeefae17030476d71789b`。
- API registry 为 0.9 / 94 项，新增 CopyFile、MoveFile、RenameFile、CreateTempFile、CreateTempDirectory；函数手册生成区与 API registry 同步；IDE 补全/签名继续读同一 registry。
- Copy/Move/Rename 仅支持普通文件，不支持目录。Copy/跨卷 Move 限 64 MiB；复制临时文件位于目标目录，分块写入、flush、SHA-256/长度复核后提交。默认不覆盖，overwrite? 默认 false。
- Move/Rename 源文件必须同时获读取和写入授权；目标获写入授权。Copy 源获读取、目标获写入。临时对象只允许调用者指定的已批准写入目录，不自动开放系统 TEMP。
- 新增操作 Worker bridge 与 PlaybackRunner dispatcher 双层 Grant 检查；目录父链持有 no-delete-share 句柄；源/已有目标文件 no-follow 并拒绝 reparse/symlink/多硬链接。
- Windows FileRenameInfo 经过两轮 CI 发现的问题后，当前使用 RootDirectory=NULL + Windows verbatim 完整目标路径 + UTF-16 NUL 终止符；父目录锁仍保持。最终 run [43385033936](https://github.com/xiaobei206/anjian/actions/runs/43385033936) 对 HEAD 057f400657c8ebaf673eeefae17030476d71789b 全 PASS：Release Build 0 warnings/0 errors、142/142 Core tests（0 skipped）、94 个公开函数的 API handbook check、自包含 win-x64 publish、Sprite/Worker 包内 smoke、ZIP/SHA256 和 artifact upload。Artifact ID 14018690914，87,196,057 bytes，上传 artifact archive SHA-256 `5ebc2dae013634a0018f1ffed5c0e74e49d7cef253f9e33bc0f4e630452fe431`。CI 日志显示 runner 具备 D: 测试卷，跨卷移动成功与超限路径已实际执行并通过。
- 本地 .NET SDK 不存在，本地 Restore/Build/Test NOT RUN；Windows Actions 是本轮自动化构建/测试的来源。
- Windows 10/11 桌面实机权限 UI、文件操作、DPI/多显示器和小精灵逐次 sidecar 审阅仍 NOT RUN，统一留 M14。
- 未修改 VERSION/AssemblyVersion/FileVersion；没有 tag/Release/PR；v0.1.0-preview.4 官方资产不变。

## M12-02B 范围完成情况
- [x] CopyFile(sourcePath, destinationPath, overwrite?)。
- [x] MoveFile(sourcePath, destinationPath, overwrite?)。
- [x] RenameFile(path, newName, overwrite?)。
- [x] CreateTempFile(directoryPath)。
- [x] CreateTempDirectory(parentDirectoryPath)。
- [x] Worker/host 双层授权、暂停 checkpoint 复用、Stop token 分块取消和提交边界检查。
- [x] 注册表 0.8 → 0.9，FUNCTION-REFERENCE 由注册表生成；契约测试检查公开 API 名称/签名和 Lua bridge。
- [x] 覆盖/链接/硬链接/临时名称碰撞/路径越界/64 MiB 上限/取消/未知 IPC 测试已加入。
- [x] Windows Actions Build/Test/API handbook/self-contained package smoke 全绿：run 43385033936，142/142 tests，0 warnings/errors，artifact 14018690914。
- [ ] M14 Windows 10/11 桌面验收（不属于本任务 CI 阻塞项，继续 NOT RUN）。

## 下一会话唯一任务：M12-07 — 剪贴板与对话框 API

严格只做剪贴板读写与消息/输入/确认对话框，沿用 default-deny + Worker/host 双层 Grant。先读取 PRD、权限 schema、MainForm 审阅、PlaybackRunner dispatcher 和当前 API registry；定义 clipboardRead/clipboardWrite、dialogs 资源权限语义，STA 与 WinForms 主 UI thread 调度，输入/返回值长度限制，取消/暂停/Stop 语义和错误处理。未授权/伪造 IPC 必须 fail closed。不得顺带启动进程 API、HTTP/JSON、插件 manifest 或小精灵 sidecar 审阅。

---

# 最新交接状态覆盖：M12-07 已完成实现与 Windows CI（2026-11-13）

本节覆盖本文之前所有将 M12-07 标为待实现/待验证的描述。当前唯一任务 M12-07 已完成代码、API 契约、函数手册、验收记录和 Windows CI；真实 Windows 10/11 桌面交互仍保留 NOT RUN，按统一策略留 M14。

## 当前 main 与交付提交
- 最终核对的开发代码/文档 HEAD：`81a221e4d6a7b7754b084ff9209dc46fe176878d`（本条状态记录提交前；后续提交仅同步最终 handoff/status）。
- 实现提交：`7c561f9d342d7d6f64a9a14535898b8790bb08f6`。
- API 契约与 Lua API 规格提交：`bd316e26c34f6ddf3198036875baba06cb568d9a`。
- 架构/路线图范围提交：`21886b64d31623e74ac96bfee7d988b6780ff9ee`。
- Catalog 规则重生成函数手册提交：`6d1ae9e64764dd55bec9c55b439b3f8b6fcff4f0`。
- 验收清单/开发日志/用户指南提交：`81a221e4d6a7b7754b084ff9209dc46fe176878d`。

## M12-07 已交付
- 新增 GetClipboardText、SetClipboardText、MessageBox、InputBox、Confirm；API registry 1.0 / 99 项。FUNCTION-REFERENCE 由同一 registry/Catalog 规则生成，IDE 提示仍读取同一 registry。
- sidecar 沿用 clipboardRead、clipboardWrite 与 dialogs: message/input/confirm。Worker bridge 与 PlaybackRunner host 对每条 IPC 分别校验 Grant、命令类型与 payload；未知/伪造 IPC fail-closed。
- WinForms Clipboard 和自定义模态窗体在所属 UI STA 线程执行；剪贴板文本最多 1,048,576 个 UTF-16 字符，Dialog 正文/提示/default/input 最多 32,768，标题最多 256；InputBox 取消返回 nil。Stop 关闭自定义模态窗体；暂停在命令边界生效。
- 小精灵暂未接入 permissions sidecar 审阅，未显式提供批准时依旧为空 Grant；未默认放开剪贴板/对话框权限。

## 最终 Windows Actions 结果
- Run [43408020908](https://github.com/xiaobei206/anjian/actions/runs/43408020908) SUCCESS（HEAD `81a221e4d6a7b7754b084ff9209dc46fe176878d`）：Restore PASS；Release Build 0 warnings/0 errors；Core tests 147/147；API handbook `--check` PASS；self-contained win-x64 publish PASS；Sprite/Worker smoke 1/1 PASS；ZIP/SHA256 与 artifact upload PASS。
- 临时 artifact ID `14027962784`，87,207,518 bytes，archive digest `sha256:0887a163767200037a81109f6be5e0695cdd7d66aeda0341fdeccf429fca0cce`。它不是 GitHub Release。
- Run 248/249 的真实失败已保留在 DEVELOPMENT-LOG-CN.md：先是手册未同步，再是数字默认值缺反引号。按 Catalog 规则修复后由 run 43407935185 和 43408020908 全绿覆盖验证。
- 本地环境没有 .NET SDK，因此本地 Restore/Build/Test 仍 NOT RUN；Windows Actions 为本轮构建测试证据。
- Windows 10/11 真实剪贴板、权限审阅窗体、对话框布局/DPI、剪贴板占用和 Stop 实机交互仍 NOT RUN（M14）。CI PASS 不等于桌面实机验收。
- 未修改 VERSION / AssemblyVersion / FileVersion；未建 tag/Release；v0.1.0-preview.4 官方资产不变。

## 下一会话唯一任务：M12-08 — 受控进程 API

先核对 main HEAD 并读取本 handoff、ROADMAP、PRD、FEATURE-MATRIX、ADR-024、API registry 和当前 Worker/PlaybackRunner dispatcher。只实现严格授权的进程/应用启动、等待与查询能力：精确批准的 EXE 路径、参数安全传递（不得拼 Shell 命令）、超时/Stop 取消、退出状态和错误契约；未声明/未批准路径默认拒绝。不得开放任意 shell、任意进程终止、HTTP/JSON 或插件。新增公开 API 前先定好参数/返回/权限/取消规格，再同步 registry、函数手册、IDE 提示、测试、开发日志和验收清单。直接在 main 小步提交，不建长期分支/PR；不改产品版本、不创建 Release。真实桌面验收统一留 M14。
