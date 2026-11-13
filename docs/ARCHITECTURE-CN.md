# 技术架构与模块边界

- 版本：Architecture 0.6（追加 M8 图像视觉模块与 M9 OCR Worker/模型管理）
- 目标框架：net10.0-windows
- 平台：Windows x64
- 状态：preview.4 实际实现已确认；后续模块化架构仍按路线图渐进演进。
- 注意：本文件同时记录已采用实现和未来目标，不能把“拟定”模块视为当前已有项目。

## 1. 总体架构原则

1. UI 不直接调用 Win32 键鼠 API。
2. Lua 运行时不运行在 UI 线程。
3. 一个 Lua state 在任一时刻只能由一个执行线程访问。
4. Windows API、Lua 绑定、编辑器控件必须封装在自己的适配层后面。
5. 业务层只依赖接口，不依赖具体 Win32 类型或第三方控件类型。
6. 录制动作模型是中立数据结构；列表 UI 和 Lua 代码生成都是它的消费者。
7. 所有跨模块事件和可保存数据都有版本/类型定义。
8. 每个模块可独立单元测试；平台依赖放在 Windows 集成测试中。

## 2. 当前仓库结构与目标拆分

### 2.1 preview.4 实际存在的项目

    Anjian2026/
    ├── src/
    │   ├── Anjian.App/                 # WinForms：MainForm、FloatingForm、托盘、Hook、脚本与播放协调
    │   └── Anjian.Core/                # 播放选项等共享模型
    ├── tests/
    │   └── Anjian.Core.Tests/          # 当前 7 项自动化测试
    ├── docs/                           # 产品、架构、API、录制、验收和交接资料
    └── .github/workflows/              # Windows CI / self-contained 发布流程

目前没有独立的 Anjian.Scripting、Anjian.Automation、Anjian.Recorder、Anjian.ProjectSystem 或 Imaging 项目。main 已在 Anjian.Core 中落地 Anjian.Core.Imaging（纯像素模型与匹配算法），在 Anjian.App 中落地 WindowsImageService（GDI+ 捕获与本轮资源池）；NLua Host、播放、录制、Win32 窗口和输入适配仍主要位于 Anjian.App。已发布 preview.4 的 Release 包不包含这些 M8 main 开发功能。不要假设规划中的独立项目已经存在。

### 2.2 后续目标模块（规划，不代表当前已落地）

    App
      ├── Core
      ├── Scripting
      ├── Automation
      ├── Windows.Input
      ├── Recorder
      ├── CodeGeneration
      ├── ProjectSystem
      └── Imaging（P1）

只有在一个模块需要独立测试、复用或明确隔离时才逐步拆分；避免为追求目录结构而一次性重构。

## 3. 依赖方向

    App
      ├── Core
      ├── Automation
      ├── Recorder
      ├── ProjectSystem
      └── CodeGeneration

    Automation ──> Core + Scripting
    Scripting  ──> Core
    Windows.Input ──> Core
    Recorder ──> Core + Windows.Input（只依赖采集抽象/DTO，不依赖 UI）
    CodeGeneration ──> Core
    ProjectSystem ──> Core
    Imaging（P1）──> Core + Windows 屏幕捕获适配

组合根（App）负责注入实现。Core 不得引用其他项目。避免 Automation 与 Scripting 相互引用；由 Core 定义所需接口，App 组装具体实现。

## 4. 核心接口草案

### IInputController
- MoveMouse(x, y)
- Click(button, holdMs)：按下指定按钮 holdMs 毫秒后释放；缺省时长由用户 API 层填入 50 ms。
- DoubleClick(button, holdMs, gapMs)
- MouseDown(button) / MouseUp(button)
- Scroll(delta)
- KeyPress(key, holdMs)：按下指定键 holdMs 毫秒后释放；缺省时长由用户 API 层填入 50 ms。
- KeyDown(key) / KeyUp(key)
- Hotkey(keys)
- TypeText(text)
- ReleaseAllOwnedInputs()

输入控制器内部追踪注入按下状态。Click / KeyPress 的释放必须放入 finally 清理路径；取消请求打断保持时长后应立即释放。暂停请求在当前原子 Click / KeyPress 动作完成后生效。

### IScriptRuntime
- RunAsync(ScriptRequest, CancellationToken)
- Pause() / Resume() / RequestStop()
- 报告脚本状态、输出、错误文件名和行号。
- 不向 UI 暴露 Lua 原生对象或 Lua state。

### IRunCoordinator
- 单实例任务状态机。
- 负责 Run / Pause / Resume / Stop 的并发与重复请求处理。
- 在脚本完成、取消、失败时统一调用输入状态清理。

### IRecorderInputSource
- StartAsync / StopAsync
- 输出标准化 InputEvent 流。
- 将物理输入和软件注入事件区分；默认忽略自身注入事件。
- 队列溢出必须产生显式错误/状态，不能静默丢事件。

### IProjectStore
- 创建、打开、保存工程和脚本。
- 相对路径解析、schema version 迁移、原子写入。
- 保存前校验目标路径和覆盖策略。

### IClock / IExecutionGate
- 支持可取消延时和暂停等待。
- 在 Lua 指令钩子、API 入口和延时结束前检查 Stop/Pause 状态。

## 5. 线程与任务模型

- WinForms UI：仅负责展示、编辑和用户交互。
- 录制采集线程：接收 Raw Input 或 Hook 消息；只做必要字段复制和快速入队。
- 录制处理线程：合并移动、识别按键配对、构建动作列表。
- 脚本执行线程：拥有唯一 Lua state，串行执行一个脚本。
- UI 通过线程安全消息/Event 接收状态和日志，使用 WinForms SynchronizationContext 更新界面。
- 不从 UI 线程同步等待脚本结束。
- 不在钩子回调内写磁盘、记录详细日志、匹配图片或做复杂分配。

首版先采用同进程独立执行线程；是否升级为独立 Worker 进程留待稳定性验证，当前不引入 IPC 复杂度。

## 6. 脚本执行与取消

- Lua 使用 Lua 5.4。
- Lua state 生命周期绑定一次运行或明确的脚本会话；禁止多线程并发访问。
- 通过 lua_sethook 指令计数钩子定期检查取消/暂停请求。
- 所有阻塞等待都由可取消的执行时钟实现，不直接用不可中断的大延时。
- 暂停在安全检查点生效；恢复从原状态继续。
- 停止时先设置取消标志，再等待执行线程退出；超时记录诊断并进入受控失败流程。
- finally/清理路径必须调用 ReleaseAllOwnedInputs()。
- 禁止将“从其他线程强行操作 Lua state”作为取消机制。

Lua 环境默认不开放任意 .NET 类型导入、程序集加载、文件系统全权限、Shell、未受控网络、os / io / package / debug 等不必要标准库。需要文件等能力时通过之后设计的显式 API 提供。该限制是防误操作的受限环境，不是抵御恶意本机用户脚本的安全沙箱。

### 6.1 M4 调试器执行边界

- 调试会话与 Lua state 均留在独立 worker 子进程；UI 只能通过主进程命名管道提交普通 JSON 命令和读取快照，不能持有 NLua/KeraLua 对象。
- worker 启动后先请求调试配置；调试会话同时启用 Lua 行 hook 与既有指令计数 hook。行 hook 在即将执行目标行时暂停，计数 hook 继续承担长循环检查点与普通暂停/取消行为。
- 继续/单步/运行到光标命令在暂停请求的 IPC 响应中返回；worker 的 Lua 执行线程同步等待命令，不会让 UI 线程运行 Lua。
- 单步进入在下一个 Lua 行停止；单步越过依据暂停时调用栈深度等待返回至相同或更浅深度；单步跳出等待更浅栈帧；运行到光标用规范化源码路径和行号精确匹配。
- 变量快照在 hook 所属线程内读取，然后只传输名称、类型和值文本。局部变量最多 96 项/栈帧，全局最多 100 项，调用栈最多 32 层；字符串最多显示 512 字符。表、函数、线程和 userdata 暂不展开，不执行任意 watch 表达式。
- 断点当前保存在打开脚本标签的内存模型中，关闭标签/重启应用不会保留；此项作为后续完善范围明确记录。
- DebugSession 退出时移除 Lua hook；主 runner 的停止路径仍终止 worker 并执行 ReleaseAllOwnedInputs。任何时刻均不从 UI 线程访问 Lua state。

## 7. Windows 输入与坐标

- 默认使用 Windows SendInput 实现前台键鼠注入。
- 启动时明确设置 Per-Monitor V2 DPI awareness。
- 既有 MoveTo(x, y) 仍只使用虚拟桌面绝对物理像素，原点和范围以 Windows 虚拟桌面指标为准；负坐标合法。
- M6 Core 提供 screen/window/client/anchor/ratio 纯转换算法；window 基于外框，client/ratio 基于客户区，anchor 基于外框九宫格。
- M6 Win32 窗口管理器位于 Anjian.App.Services.WindowsWindowManager；Lua Worker 只传递不透明窗口 ID 与 JSON IPC，主进程执行 HWND 枚举/状态/布局/等待。窗口引用每轮运行清除，并结合 EVENT_OBJECT_DESTROY 与 PID/线程/类/进程启动时间验证。顶层/子窗口区分写入 `WindowInfo.isChild`；子窗口 Move/Resize/Align 的公开坐标仍是虚拟桌面屏幕物理像素，调用 SetWindowPos 前转换为父窗口客户区坐标。
- M7 UI Automation provider 位于 Lua worker 进程，而不在主 UI 进程中。主进程仅响应本轮私有命名管道的 `window-automation-target` 请求，把已通过 M6 引用校验的窗口身份描述发给 worker；该描述内部包含短期 HWND，但公开 Lua API 仍只暴露随机 binding/control ID。每次控件调用前 worker 再请求主进程复核同一个 M6 引用，并检查 PID、线程、类名和进程启动时间。UIA AutomationElement/COM 代理只保存在 worker，绑定随 worker 退出释放。这样 Stop 可以终止被第三方 provider 阻塞的 worker，而不会让主 UI 因同步 COM 调用卡死。
- UIA 的后台能力定义为标准控件语义：ValuePattern.SetValue、Selection/SelectionItem、Toggle 与 Invoke。M7 不把这类操作描述成后台键盘输入；通用后台鼠标/键盘/文本注入明确为 unsupported。前台 WindowInput 仍由主进程调用 SendInput，并检查可见/未最小化/前台/焦点/客户区范围，不自动抢焦点或静默回退。密码字段禁止读取 Value/Text。
- 不假设 SendInput 或 SetWindowPos 对任何权限等级、受保护应用、游戏或安全桌面都有效；不绕过 UIPI/UAC/安全桌面。
- 程序默认普通用户权限运行；发生 UIPI/权限限制时向用户说明。
- 追踪本程序模拟的按键/鼠标按下状态，结束时发出对应释放动作。
- 原始键盘扫描码、虚拟键码和 Unicode 文本输入需要分别定义，不混为一种操作。
- M5 `RecorderDocument` 当前固定记录 `virtual-desktop-physical-pixels`，Lua 生成区块调用 `MoveTo`；M6 不重解释旧 sidecar。窗口选择器持久化、相对坐标录制和回放重定位尚未实现。

## 8. 录制器数据流

物理输入源 -> 标准化 InputEvent -> 有界队列 -> 配对与动作合并 -> RecorderDocument -> 动作列表 UI / Lua Code Generator。

preview.4 当前采用低级鼠标/键盘 Hook，已纳入 Windows CI 编译与自动化发布；CI 不验证实际桌面钩子是否稳定安装。低级钩子回调可能因超时被系统移除，用户必须实测漏录/重复、Hook 安装与卸载、长时间录制和安全软件冲突。若实机暴露可靠性问题，再将 Raw Input 作为下一轮技术替代方案，不要在没有复现证据时重写采集器。

## 9. 工程与脚本存储

建议工程结构：

    Project/
    ├── project.kpproj
    ├── Scripts/
    │   ├── main.lua
    │   └── main.actions.json
    ├── Assets/
    │   ├── Images/
    │   └── Screenshots/
    ├── Config/
    └── Logs/

- .lua 可独立编辑/运行。
- .actions.json 保存录制事件、动作、时间戳、关联 ID 和 schemaVersion。
- 工程引用资源默认使用相对路径。
- 文件写入先写同目录临时文件，完成并 flush 后再替换；保留必要备份。
- 新版读取旧 schema 时做显式迁移；无法迁移时不覆盖原文件。
- 不把日志、缓存和运行状态混入用户源码。

## 10. 依赖策略

- 目标框架 net10.0-windows / WinForms / win-x64。
- Lua 绑定当前采用 NLua 1.7.9，已验证运行时为 Lua 5.4、C# 全局函数注册调用和 instruction-count hook；Windows CI 测试通过。
- 当前 main 编辑器采用自定义 WinForms `LuaEditorControl`（RichTextBox + 行号 gutter），实现词法颜色、行号、注册表驱动补全/签名提示、查找替换、多标签与语法诊断；没有引入 Scintilla 等第三方编辑器依赖。
- 编辑语言服务位于 `Anjian.Core.Editor.LuaEditorService`；关键字/token 识别与 API 补全消费 `LuaApiRegistry.Load()` 的唯一注册表，不能在 UI 维护第二份 API 定义。
- 语法诊断使用 NLua `LoadString` 编译 chunk，不执行脚本语句；代码着色在后台线程计算，并通过文本版本号丢弃过期结果。WinForms 控件只在 UI 线程应用颜色。
- M3 Windows CI 已通过，但中文/日文输入、1MB 文件、DPI、多标签交互等 Windows 桌面手工验收仍为 NOT RUN；代码折叠和独立命令面板尚未实现。
- 后续引入编辑器/Dock/图像库时仍需先做 PoC 并记录许可证、原生依赖和发布兼容性。
- 不在业务层直接调用第三方 Lua/编辑器类型。
- 发布时优先 self-contained win-x64 文件夹 ZIP；单文件发布作为单独验证项。

## 11. 可观测性

- 每次运行有 RunId。
- 日志字段包含 UTC/本地时间、级别、RunId、脚本路径、行号、消息。
- 错误日志包含可读消息与内部异常类型，不记录敏感内容。
- 录制器记录事件数、丢弃数、队列峰值、是否有未配对的 Down/Up。
- UI 状态由运行状态机唯一驱动，避免控件各自维护互相矛盾的状态。


## 12. 完整产品目标对应的目标模块（规划，不代表当前仓库已有）

完整目标已由 docs/PRD-CN.md 冻结。模块按真实测试和依赖渐进拆分；不得一次性大规模重构，也不得把以下规划模块描述成当前已存在。

### 12.1 目标职责

- API Catalog：机器可读的公开 API 注册表，统一函数签名、参数 schema、返回值、错误、权限/取消语义和版本。
- Function Reference Generator：从同一注册表生成中文函数/参数手册；与编辑器补全、签名提示、命令面板和运行时绑定做一致性测试。
- Scripting / Debugging：Lua 运行器、调试状态、断点/单步、变量快照、调用栈和错误导航。
- Recorder / CodeGeneration：标准化输入事件、结构化动作、动作列表、生成代码区块与同步状态。
- Coordinate / Window Automation：screen/window/client/anchor/ratio 转换，窗口枚举、定位、移动、大小、对齐、等待和失效处理。
- Input Adapters：前台 SendInput、已验证的后台后端、控件/UI Automation 能力探测；不同后端明确声明功能矩阵。
- Imaging / OCR：截图、像素颜色、找色、模板匹配、OCR 与图像资源生命周期。
- ProjectSystem / Parameter UI：工程 manifest、脚本参数 schema、资源依赖、用户配置与版本迁移。
- PackageBuilder（小精灵）：收集入口脚本和资源，生成独立运行包、参数 UI、清单、校验值和离线说明。
- PluginHost：本地扩展包、manifest、权限提示、版本兼容、注册 API、错误隔离和卸载。
- TaskRunner：受控多实例、定时执行、超时、失败策略、独立日志和停止清理。

### 12.2 API 定义的数据模型

目标 API 元数据应至少包含：稳定 ID、公开名称、分类、中文标题、摘要、详细说明、参数数组、参数类型/必填/默认值/范围/选项/单位、返回值 schema、错误类型、副作用、坐标空间、暂停/停止行为、权限要求、适用后端、最低 API 版本、弃用信息、示例、测试 ID、实现状态和文档更新时间。

同一份机器可读定义必须服务于运行时注册、IDE、命令面板和函数手册。若某函数只存在于规划文档而没有运行时绑定，状态必须为“目标设计”，不得生成可用 API 提示。

### 12.3 边界与依赖

- Core 定义稳定 DTO 与接口；Windows、Lua、图像、打包与 UI 依赖通过适配层连接。
- 不将 Win32 HWND、原生图像对象或 Lua state 泄露给 UI 以外的通用业务模型。
- 窗口句柄失效、截图失败、后台输入不支持、OCR 语言缺失和打包资源缺失都应有明确的错误类型。
- API 清单与手册生成应先建立，然后再扩大公开函数数量，以免手册和代码分叉。
- 后台输入是否支持按目标能力报告；不因有窗口查找函数就承诺任意窗口都可后台点击。

## 13. M8 图像服务模块（main 开发版；Windows 桌面验收 NOT RUN）

- Anjian.Core.Imaging.PixelImage 持有不可变的 32-bit ARGB 像素副本；单图最多 32,000,000 像素，图像裁剪会保留截图的屏幕原点偏移。
- ImageSearchEngine 不依赖 Windows GDI 或第三方图像库；实现 RGB 各通道容差、单点找色、多点模式解析/匹配、区域计数、透明通配模板匹配、相似度、灰度亮度匹配、最近邻缩放和有上限的多结果搜索。模板匹配最多 50,000,000 次像素比较，并在循环内检查取消令牌。
- WindowsImageService 是 Windows 适配层，使用 CopyFromScreen 捕获当前桌面合成图像；截图资源由脚本运行轮次管理，限 128 个资源/轮和 256 MiB/轮。PNG 读写、裁剪、像素读取、资源 ID、释放和轮次清理统一在此服务中。
- PlaybackRunner 在主进程侧执行图像 IPC；Lua worker 只能拿随机资源 ID 和结构化结果，不接收原生 Bitmap/HWND。WaitImage/WaitColor 系列遵循暂停、取消令牌、超时和连续条件跟踪。
- CaptureWindow/CaptureClient 只对可见且未最小化窗口调用屏幕区域捕获；遮挡区域不保证目标窗口真实后台像素。
- 没有新增第三方图像识别/OCR 依赖。图像资源预览/管理 UI、模板命名/版本校验、视觉调试 overlay、颜色差值/颜色块检测还未完成，属于 M8 剩余工作。


## 11. M8 图像资源与视觉调试边界

### 11.1 图像资源清单
- 每个脚本可拥有同名的 <script>.images.json 与 <script>.assets/ 目录。清单 schemaVersion 当前为 1，包含 ScriptId、资源显示名称、相对路径、PNG 宽高、文件大小、SHA-256 与加入时间。
- 资源相对路径固定落在当前脚本对应的 .assets/ 根目录内；路径解析使用 FullPath 前缀边界检查并拒绝目录穿越/非 PNG 后缀。PNG 导入上限为 128 MiB、32,768 单边和 32,000,000 像素。
- 校验时验证 schema、路径、文件存在、PNG 签名/IHDR 尺寸、字节数与 SHA-256。未知 schema 不回写、不自动迁移，以免覆盖未来版本。
- UI 只持有不可变 PixelImage 预览数据；从 PNG 载入/绘制时使用局部临时 GDI Bitmap 并立即释放。脚本 Worker 的 WindowsImageService 仍通过每轮资源池限制内存，LoadImage 相对路径以脚本所在目录为基准。
- ScriptRepository 的重命名/复制/删除同步处理 images manifest/assets；复制时重新生成 ScriptId、重基 manifest 相对路径并改写脚本中静态资产目录前缀。删除脚本是显式破坏性操作，删除其图片 manifest 与资产目录。

### 11.2 依赖扫描
- ImageResourceDependencyScanner 只识别 LoadImage("literal") 形式的静态首参数，并跳过常见行注释/长注释；动态表达式计数为无法静态解析，不假装知道实际文件依赖。
- 移除资源前，UI 检查当前脚本文本的静态字面路径；仍被引用时拒绝移除。移除清单条目不删除 PNG 文件，避免不可恢复的数据丢失。

### 11.3 视觉调试覆盖层
- WindowsImageService 以事件发送截图矩形、像素颜色、FindColor/FindImage 命中矩形、FindColorBlocks 边界、相似度和检测耗时；事件只传数值/标签，不跨模块传递 GDI Bitmap 或 Lua 对象。
- MainForm 通过 UI 线程调度事件到可选的 VisualDebugOverlayForm。覆盖层为 TopMost、透明键、WS_EX_TRANSPARENT/WS_EX_NOACTIVATE 点击穿透窗口；隐藏/关闭时不参与命中测试。
- 使用 SetWindowDisplayAffinity(WDA_EXCLUDEFROMCAPTURE) 尝试避免截图包含自身标注。Windows 版本、DWM 与捕获路径影响该能力，失败需要在桌面验收时记录；不能宣称所有截图路径都绝对排除覆盖层。
- 覆盖层只对带屏幕原点的图像结果绘制。加载的磁盘模板没有屏幕坐标，只参与匹配，不绘制伪造屏幕位置。

### 11.4 RGB 与颜色块算法
- RGB 颜色统一为 0xRRGGBB；十六进制接受 RRGGBB、#RRGGBB、0xRRGGBB。通道差为绝对值；ColorDifference 返回 R/G/B 差、MaxChannel、Manhattan 与 Euclidean。
- FindColorBlocks 使用八邻域连通分量；step 是采样网格步长，pixelCount 是命中采样点数，矩形和中心按采样坐标计算。单次最多 4,000,000 个采样点、返回最多 100 块，支持取消。阈值 tolerance 按 RGB 每通道最大差判断。


## 14. M9 OCR 文本识别模块（main 开发版；桌面验收延期）

### 14.1 分层与依赖
- `Anjian.Core.Ocr` 定义引擎无关 DTO（全文、行文本、置信度、矩形、多边形、识别语言、源图像尺寸、耗时）和 `IOcrRecognitionService` 接口；Core 不引用第三方 OCR SDK、ONNX Runtime 或模型文件。
- `Anjian.App.Services.PaddleOcrRecognitionService` 是 PaddleOcrNet 适配层，只在 WinForms App/Worker 侧引用第三方包。精确依赖固定为 PaddleOcrNet 2.4.0；默认 CPU 推理。
- 主 UI 的 OCR 模型管理窗只负责展示缓存状态与用户显式下载/补齐模型；模型缓存放在 `%LOCALAPPDATA%\\Anjian2026\\Ocr\\Models`。下载使用上游模型清单和 SHA-256 校验。
- 每轮 Lua Worker 创建识别适配器，采用 `Download.Offline=true` 严格离线策略。识别路径先检查语言模型文件是否存在；缺失、损坏或不支持的模型明确报错并提示打开模型管理器，不在执行脚本时自动联网下载。

### 14.2 图像 IPC 与坐标
- Lua Worker 只持有 M8 图像资源随机 ID；主进程根据 ID 从本轮 `WindowsImageService` 读取尺寸/坐标元数据，并把受限 PNG 编码以 Base64 响应 `image-export-png` IPC。Worker 不接触 GDI Bitmap、HWND 或窗口 UI 对象。
- 单次 OCR 输入最多 16,000,000 像素，PNG IPC 编码最多 64 MiB；超限时要求调用方先 CropImage 缩小区域。引擎还通过 `MaxImagePixels` 限制解码像素，防止异常大图输入。
- OCR 框坐标以图像局部零基像素表示；当图像来自带 screen origin 的截图，结果额外返回虚拟桌面物理像素 `screenX/screenY`，保留负坐标。磁盘加载图片只返回局部坐标，不伪造 screen 坐标。

### 14.3 API 与生命周期
- API registry 0.7 新增 `OCR`、`FindText`、`WaitText`；自动生成函数参考手册并作为编辑器补全/签名提示的数据源。
- `OCR` 返回全文与 1 起始行数组，每行含 confidence、矩形和 polygon；`FindText` 支持 contains/exact、不区分大小写；`WaitText` 通过主进程截图 IPC 循环检测，每次截图前请求 checkpoint，间隔使用受 `ExecutionGate` 暂停/取消门控的 `delay` IPC；F12 Stop 终止独立 Worker。
- WaitText 每轮释放临时截图 ID；即使脚本异常/Stop，PlaybackRunner 仍会清理当前轮全部图像资源。OCR 进程结束时释放模型 session。
- 当前版本仅公开简体中文 `ch`、英语 `en`、日语 `japan`；缺少对应模型时显式报错。OCR 单次识别的推理阶段不承诺线程内中断，统一 Stop 边界是终止 Worker 进程。

### 14.4 验证边界
- Core 的 API 契约测试使用假 OCR 服务，不加载真实模型；它验证 Lua 表结构、屏幕原点换算、置信度过滤、匹配模式和 WaitText 资源释放。
- Windows Actions run [43363150335](https://github.com/xiaobei206/anjian/actions/runs/43363150335) SUCCESS：NuGet Restore PASS、Release Build 0 warnings / 0 errors、90/90 Core tests、API handbook check、win-x64 self-contained publish、ZIP/SHA256 与 artifact upload 全 PASS；artifact ID `14008744436`。真实语言识别质量、模型下载/断网运行、DPI/多屏与 OCR 性能仍需 M14 后 Windows 桌面集中验收；CI 不等同于用户桌面验收。


## 15. M10 参数、配置与自定义 UI（实现基线）

### 15.1 文件边界
- 参数定义是脚本同目录 sidecar：`<script>.params.json`；schemaVersion=1。定义可被编辑器读取/编辑，不需要执行脚本。
- 运行配置独立保存在用户数据目录，按脚本规范化路径的 SHA-256 派生稳定键，不污染项目源码。定义和用户运行值分离。
- Core 提供版本化 schema DTO、字段级 validator、配置迁移/持久化接口和参数注入 JSON 合约。WinForms 只负责参数设计器、按 schema 自动生成表单、导入导出交互。

### 15.2 类型和值语义
- text:string；integer:int64；number:finite double；boolean:boolean；select:string（必须属于选项）；multiSelect:string[]（每项必须属于选项且不重复）；filePath/directoryPath:string（支持 required、可选 exists）；hotkey:string（规范化组合键）；color:string（#RRGGBB 或 #AARRGGBB）。
- DefaultValue 与运行值使用 JSON 元素/节点保留真实类型；未提供默认值时不伪造类型默认值。必填字段可由用户显式提供，也可由有效默认值满足。
- 参数名使用 Lua 标识符规则且唯一；ID 使用稳定 GUID/slug，禁止同 schema 内重复 ID、name 或 order 不受控。

### 15.3 启动、Worker 与只读入口
- 主进程在启动前解析 schema/config、迁移兼容版本、验证所有值；错误按参数 ID/标签返回，绝不创建 Worker。
- Worker 启动握手后接收独立 JSON 参数对象；将 JSON 原生类型转换为 Lua string/number/boolean/table，不把用户字符串拼接成 Lua 源码。脚本通过只读 `Args` 表访问参数。
- `Args` 顶层写入应抛出明确错误；嵌套表也必须递归只读或提供深复制只读视图，避免脚本修改值后污染同轮其他消费者。
- 敏感参数只在必要 UI 显示，普通运行日志不得记录参数值；导入/导出、异常消息与测试快照同样执行脱敏。

### 15.4 持久化和恢复
- 配置写入采用同目录临时文件、flush 和原子替换；保留上一份有效备份。损坏时先尝试恢复备份，失败后可用默认值但须明确告知用户；未知未来 schema 不得写回/覆盖。
- JSON 导入只接收受限大小的 UTF-8 JSON，校验后才能替换配置；导出使用同 schema/validator。迁移必须保留原文件副本，失败则回滚。
- M11 小精灵参数 UI 直接复用 Core schema/validator，不能另造第二套参数定义或校验语义。


### 15.5 当前 main 实现映射（M10 首轮）
- `src/Anjian.Core/Parameters/ScriptParameterSchema.cs`：schema DTO 和单一类型验证器。
- `src/Anjian.Core/Parameters/ParameterConfigurationStore.cs`：1 MiB 上限、原子替换、上一份有效备份、损坏恢复和未来版本拒绝。
- `src/Anjian.Core/Parameters/LuaParameterInjector.cs`：递归构建 Lua 表，常规赋值经 proxy 抛错；Lua 标准库中的 `rawset`/debug 能力意味着这不是恶意脚本沙箱。
- `src/Anjian.App/Services/ScriptParameterService.cs`：sidecar `<script>.params.json` 与 LocalAppData 用户配置路径解析。
- `src/Anjian.App/Controls/ScriptParameterDesignerForm.cs`：参数定义表格编辑、类型选择、JSON 默认值、范围、选项、正则、敏感标记和排序。
- `src/Anjian.App/Controls/ScriptParameterDialog.cs`：由 schema 生成的运行表单、文件/目录选择、恢复默认、JSON 导入/导出和敏感值导出确认。
- `PlaybackRunner` 的 worker-ready 握手将已验证参数作为独立 JSON payload 传递；`WorkerProgram` 注入 `Args`。MainForm 在播放和调试前先收集参数。
- 配置文件为本地 JSON 明文；Sensitive 只做 UI 遮蔽、导出提醒和日志不记录，不提供静态加密。
