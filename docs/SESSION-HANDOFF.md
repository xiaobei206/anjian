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
