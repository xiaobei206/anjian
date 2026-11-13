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

### 12.3 M12-12 插件 API registry extension view

- 内置 lua-api-registry.json 是只读基线；M12-12 在运行时读取本机插件 catalog 中已安装、启用且通过 API 兼容及文件 SHA-256 完整性检查的 contributions，并通过 LuaApiRegistry.WithPluginContributions 形成内存视图。内置 registry 的版本保持 1.2 / 103，插件元数据不写回 JSON，也不混入随应用编译的静态 FUNCTION-REFERENCE-CN.md。
- 合并视图被编辑器补全、签名提示、API 命令面板与本机动态参考文档生成器共用。停用/卸载/损坏插件从后续视图移除；启用前检查所有已启用插件间的名称冲突，失败不保存 catalog。
- 纯 Lua helper 在普通脚本专用 Worker 内惰性加载到每个插件独立环境，仅访问受限基础函数和 math/string/table/utf8 副本。模块 loader 每次重新核验 catalog、manifest、closed file/hash list；导出参数/返回类型由桥接包装逐次验证。helper 无宿主 automation global、IPC 或扩展 Grant 的入口。此设计约束公开能力但不等同 OS 沙箱。
- 插件 Worker 和小精灵 Worker 不注入普通用户启用插件的贡献计划；贡献 API 不改变当前权限 sidecar 三方交集模型。M14 仍需验证真实 WinForms 命令面板、插件管理、DPI/多显示器与交互。

### 12.4 边界与依赖

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
- `filePath` / `directoryPath` 的配置值保留用户输入的相对路径以便迁移；存在性校验和运行时解析都以脚本所在目录为基准，送入 Worker 的 Lua 参数为规范化绝对路径。
- `Args` 顶层和嵌套表赋值均由代理拒绝；`pairs(Args)` 迭代器也递归包装返回值，避免通过遍历拿到内部原始表。Lua `rawset` / `debug` 标准库仍可绕过代理，因此它不是恶意脚本安全沙箱。
- Sensitive 只做表单遮蔽、导出确认与应用常规日志不自动输出参数值；本地用户配置仍为明文 JSON。脚本本身若主动打印敏感值，应用无法替脚本隐藏。

### 15.4 持久化和恢复
- 配置写入采用同目录临时文件、flush 和原子替换；保留上一份有效备份。损坏时先尝试恢复备份，失败后可用默认值但须明确告知用户；未知未来 schema 不得写回/覆盖。
- JSON 导入只接收受限大小的 UTF-8 JSON，校验后才能替换配置；导出使用同 schema/validator。迁移必须保留原文件副本，失败则回滚。
- M11 小精灵参数 UI 直接复用 Core schema/validator，不能另造第二套参数定义或校验语义。


### 15.5 当前 main 实现映射（M10 首轮）
- `src/Anjian.Core/Parameters/ScriptParameterSchema.cs`：schema DTO 和单一类型验证器。
- `src/Anjian.Core/Parameters/ParameterConfigurationStore.cs`：1 MiB 上限、原子替换、上一份有效备份、损坏恢复和未来版本拒绝。
- `src/Anjian.Core/Parameters/LuaParameterInjector.cs`：递归构建 Lua 表；顶层/嵌套赋值和通过 `pairs` 迭代取得的嵌套表均由 proxy 拒绝写入；Lua `rawset`/debug 仍意味着这不是恶意脚本沙箱。
- `src/Anjian.App/Services/ScriptParameterService.cs`：sidecar `<script>.params.json` 与 LocalAppData 用户配置路径解析。
- `src/Anjian.App/Controls/ScriptParameterDesignerForm.cs`：参数定义表格编辑、类型选择、JSON 默认值、范围、选项、正则、敏感标记和排序。
- `src/Anjian.App/Controls/ScriptParameterDialog.cs`：由 schema 生成的运行表单、文件/目录选择、恢复默认、JSON 导入/导出和敏感值导出确认。
- `PlaybackRunner` 的 worker-ready 握手将已验证参数作为独立 JSON payload 传递；`WorkerProgram` 注入 `Args`。MainForm 在播放和调试前统一收集、验证参数，并把路径值按脚本目录规范化为绝对路径。
- 配置文件为本地 JSON 明文；Sensitive 只做 UI 遮蔽、导出提醒和日志不记录，不提供静态加密。


## 16. M11 小精灵发布包与启动边界

### 16.1 包构建输入和依赖解析
- SpritePackageBuilder 只接受完整的 self-contained win-x64 发布目录，必须能找到 apphost、Anjian.App.dll、deps/runtimeconfig、hostfxr.dll 和 coreclr.dll；framework-dependent 开发输出拒绝打包。
- 入口脚本必须位于选定工程根目录内。每个入口单独建立内容根；脚本和 sidecar 保留工程相对路径。
- LuaStaticDependencyScanner 词法扫描注释、字符串和调用，识别字面量 require("module.name") 与 LoadImage("path")。模块解析为工程根下 module/name.lua 或 module/name/init.lua，递归扫描并放入该入口的 Modules。动态 require 无法静态证明依赖闭包，构建直接拒绝。
- 图片资源按入口脚本目录解析并限制在工程根内；既有 .images.json / .assets 用 ImageResourceCatalog 的校验结果作依据。动态 LoadImage 要求 manifest，且会发出“无法证明清单外动态路径”的 warning。
- OCR 不通过联网下载补齐。只有 UI 中明确勾选且缓存文件完整的语言模型会被复制到 Content/Ocr/Models；识别 Worker 以该目录覆盖模型缓存，并使用 offline 选项。

### 16.2 文件布局、清单与校验
- 文件夹名为 <ProductName>-<SemVer>-win-x64，启动 ZIP 名为文件夹名加 -launcher.zip。ZIP 内保留整个根目录。入口 EXE 是 .NET apphost 的重命名副本，不是真正单文件；不能删除伴随 DLL、hostfxr/coreclr 或 Content。
- Anjian.Sprite.json schema v1 包含稳定 PackageId、产品元数据、入口/参数/模块/图像 manifest 路径、OCR 语言/文件、运行选项、最低 API/.NET 版本、构建 UTC 时间和每个伴随文件的尺寸/SHA-256/kind。
- SHA256SUMS.txt 覆盖包内所有 payload 文件和 manifest；ZIP 旁另写 .sha256。哈希是完整性校验，不是数字签名或来源真实性证明。
- 启动前先验证 manifest JSON/schema、清单与真实目录闭合、逐文件大小/hash、参数 schema、图像 manifest/assets、OCR 文件、启动入口和最低兼容版本；路径必须为安全相对路径，符号链接/重解析点拒绝。
- 包同时包含 build.log、README-Sprite.txt 和 Content/Help/FUNCTION-REFERENCE-CN.md。离线函数手册目前为 Markdown；HTML 生成仍未实现。

### 16.3 Worker 与用户数据隔离
- 小精灵复用主程序和既有 Worker IPC；Worker-ready payload 增加可选模块根目录与 OCR 模型目录，不改变普通 IDE 播放的默认路径。
- 小精灵 require 由 SpriteLuaModuleLoader 注册的受限加载器替代：仅允许模块标识符字符集映射到包内 .lua / init.lua，拒绝目录穿越、符号链接和超过 2 MiB 的模块；缓存已加载模块并检测循环加载。
- 参数表单仍使用 M10 ScriptParameterSchema / ScriptParameterValidator / ScriptParameterDialog，不会在包端引入第二套参数语义。
- PackageId 基于规范化工程路径和产品名；EntryId 基于 PackageId 与入口工程相对路径。相同工程升级只变版本不换 ID；用户值存放到 %LOCALAPPDATA%\\Anjian2026\\Sprites\\<PackageIdHash>\\<EntryIdHash>.json，不写入发布目录。
- 启动器用 Mutex 实现按包单实例，用 RegisterHotKey 注册可选启动热键；支持自动运行、循环、间隔、暂停/继续/停止与失败重试。Stop 仍强制终止 Worker 并释放输入；退出清理是不可关闭的安全项。
- CI 增加真实 self-contained 发布包冒烟：运行重命名后的 apphost，经 Worker IPC 运行 package-local require 模块并注入 Args，检查完整执行结果；这不覆盖 WinForms UI/DPI/真实输入实机验收。


### 16.4 M12-11 小精灵逐入口权限 sidecar

- 契约详见 [小精灵权限 sidecar 规格](SPRITE-PERMISSIONS-CN.md)。可选文件命名为 `<entry.lua>.permissions.json`，并通过 `SpritePackageEntry.permissionManifestPath` 与当前入口一对一绑定。
- Sidecar 是请求，不是 Grant。打包时校验 schema 并纳入包文件闭合清单、大小与 SHA-256；每次启动选定入口前复核绑定和哈希。绑定文件缺失、损坏或不匹配时拒绝运行。
- 启动器复用 `ScriptPermissionService` 逐次审阅；无 sidecar/无请求时只给空 Grant，用户拒绝不启动 Worker。主进程仍对每条 IPC 做最终授权检查。
- 自动化应以 self-contained apphost/Worker smoke 验证“声明不自动授权”；真实 WinForms 审批、DPI/多屏留 M14。

## 17. M12 权限基础层（M12-01；后续系统 API 尚未开放）

### 17.1 声明与授权数据流
- 脚本可选同目录权限 sidecar：脚本文件全名加 .permissions.json，schemaVersion=1、最大 256 KiB。缺少 sidecar 表示无扩展能力请求；未知版本、损坏 JSON、超限和 sidecar 本身为 reparse point 时启动失败，不得当作“没有权限”静默运行。
- Anjian.Core.Permissions.ScriptPermissionPolicy 将脚本相对路径按脚本目录规范化；权限字段包括文件读取/写入根、剪贴板读写布尔值、message/input/confirm 对话框类别、明确 EXE 路径、精确 DNS 主机和 HTTP 方法，以及独立的明文 HTTP 标志。
- 请求清单不是授权。每次普通播放/调试前由 WinForms ScriptPermissionReviewForm 展示请求并逐项默认拒绝；用户确认后的选择由 CreateGrant 验证必须是请求集合的子集。取消审阅不启动 Worker，不保存永久同意。
- PlaybackRunner 在 worker-ready 握手载荷中传入 Grant；WorkerProgram 将其注入 WorkerLuaApi。每轮完成后 Runner 清空引用，未提供授权的模式使用空 Grant。

### 17.2 权限与 IPC 运行边界
- 主程序普通播放/调试在启动前取得本次 Grant，并将其写入 worker-ready 握手载荷；WorkerProgram 注入 WorkerLuaApi。SpriteLauncherForm 复用 PlaybackRunner 的相同 IPC dispatcher；当前小精灵没有 sidecar 审阅，RunAsync 未提供 Grant 时使用 ScriptPermissionGrant.Empty，默认拒绝。
- 对文件类命令，Worker bridge 在发送前执行 Grant 检查；PlaybackRunner.DispatchAsync 对每个 file/directory IPC 再次从当前运行 _launchPermissions 检查。M12-07 的 clipboard/dialog 命令使用独立的 ScriptClipboardDialogPolicy.ValidateCommand 在 Worker 与 host 两侧分别校验 clipboardRead、clipboardWrite、message/input/confirm 权限与载荷限制；未知命令默认拒绝。Worker 伪造 IpcMessage 不会绕过 host 检查。
- 权限仍不是恶意 Lua 的 OS 级沙箱：Lua 只能调用受注册表约束的桥接函数，但同用户本地进程、内核驱动或其他进程能力不在这层文件 API 的完全隔离承诺内。不得宣传为容器或沙箱。
- M12-07 已接入剪贴板/对话框 API；进程、HTTP 与插件 API仍待后续子任务。权限 sidecar 在小精灵包内的收集/逐次审阅尚未实现。权限窗体、真实剪贴板/对话框与桌面交互继续留 M14。

### 17.3 文件系统首批适配器与 Lua API（M12-02 部分完成）
- ScriptFileSystem.ResolvePath 把相对路径以脚本所在目录为基准解析为完整路径；拒绝 Windows 设备/扩展设备路径、盘符相对路径、NTFS 备用数据流和超长路径。最终授权由 ScriptPermissionGrant 的文件读取/写入根执行 Path.GetRelativePath 边界判断。
- Windows 适配器对路径链中每一级已存在目录使用 CreateFileW + FILE_FLAG_BACKUP_SEMANTICS | FILE_FLAG_OPEN_REPARSE_POINT，以句柄属性拒绝 reparse point。目录句柄不共享删除，在操作期间锁住祖先路径，避免目录链被重命名替换；最终文件使用 FILE_FLAG_OPEN_REPARSE_POINT 打开后检查文件属性和 NumberOfLinks，拒绝符号链接/reparse point 与多硬链接文件，并保留最终句柄执行读写。
- 已注册 8 个 API：ReadTextFile、WriteTextFile、AppendTextFile、FileExists、DirectoryExists、ListFiles、ListDirectories、CreateDirectory。文本严格 UTF-8（无 BOM 写入），读写/追加总上限 4 MiB；枚举直接子级、不递归、最多检查 1000 个项目；创建目录只建一级且要求父目录存在；写入使用同目录唯一临时文件、flush-to-disk 后 File.Move(overwrite:true) 替换，取消在提交前再次检查。
- WorkerLuaApi.Files.cs 负责 Worker Grant 检查与 IPC 参数/返回值转换；ScriptFileSystemHostDispatcher 在主进程以本轮 Grant 再检查一次，独立反序列化 IPC 请求；PlaybackRunner 只将上述明确白名单命令送入 dispatcher。文件/目录 API 无审批即拒绝。
- 本批未覆盖复制、移动、重命名、临时文件/目录 API；追加不是事务性原子替换；路径适配器只在 Windows 10/11 工作。大小/项目上限是安全边界，不是可配置项。M12-02 仍为 PARTIAL。
- Windows Actions run [241](https://github.com/xiaobei206/anjian/actions/runs/43381018127)：Release Build 0 warnings / 0 errors、134/134 Core tests、API handbook check、自包含 win-x64 publish 与包内 Sprite/Worker smoke 全 PASS；权限 UI 实机体验 NOT RUN（M14）。


### 17.4 剪贴板与对话框的 UI/取消边界（M12-07）

- Core 的 `ScriptClipboardDialogPolicy` 是 IPC 命令和载荷的共享契约；它在 Worker bridge 发请求前检查本次 Grant，PlaybackRunner 对接收的命令重新检查，不能只依赖 UI 是否显示某个按钮。
- Clipboard 读取/写入和模态窗体由 App 层 `ClipboardDialogHost` 通过所属 WinForms Control 的 `BeginInvoke` 调度到 UI STA 线程。对话框采用自定义窗体而非不可控的原生 MessageBox，以便 Stop 时关闭当前窗体。
- 剪贴板文本上限为 1,048,576 个 UTF-16 字符；消息/提示文本和 InputBox 默认值/输入上限为 32,768 个 UTF-16 字符；标题上限为 256 个字符。内容不会自动进入应用日志。
- Pause 在调用前的 Runner 命令边界生效；打开对话框期间进入 Pause 时，当前对话框继续显示，脚本在下一命令边界等待恢复。Stop 取消等待任务并尝试关闭挂起窗体；如果剪贴板写入已由 Windows 接受，不承诺回滚副作用。
- 小精灵尚未接入逐次 permissions sidecar 审阅，故启动其 Worker 时未传入 Grant 仍然使用 Empty，所有剪贴板/对话框 API 默认拒绝。CI 自动化不能代替 M14 实际 Windows 10/11 权限/DPI/交互验收。
- M12-07 Windows Actions run [43408020908](https://github.com/xiaobei206/anjian/actions/runs/43408020908)：147/147 Core tests、0 warnings/0 errors、API handbook check、自包含 win-x64 publish、Sprite/Worker smoke、ZIP/SHA256 和 artifact 上传均 PASS；此结果不代表真实 Windows 桌面验收通过。


### 18. M12-08 受控进程 Host 与会话句柄

- Core `ScriptProcessPolicy` 统一规范化相对/绝对 EXE 路径、精确 Grant、参数数组、GUID 句柄和等待时间；Worker bridge 在发 IPC 前检查，host 的 `ScriptProcessSession` 对 start/wait/info 每次重新检查 Grant。
- `ScriptProcessSession` 只保存本次 RunAsync 由 StartProcess 创建的 Process 对象，句柄随机且只在本次运行内有效。WaitProcess 使用 ExecutionGate 注入的 pause-aware delay，超时返回 false、Stop 仅取消等待；查询返回 processId/hasExited/exitCode。
- 启动必须 `.exe`、文件存在、路径链不经过 reparse point；WorkingDirectory 固定为 EXE 所在目录。UseShellExecute=false，ArgumentList 保留参数边界，不经 Shell。
- 每轮最多 32 个进程；参数上限 64 项/单项 8,192 字符/总长 32,768 字符；Wait 默认 30 秒、上限 10 分钟。运行结束释放 Process 句柄但不结束外部进程。
- 只允许当前运行会话创建的句柄；不允许传入 PID、任意进程枚举、附加、终止或 Shell。此能力不是对已批准 EXE 的 OS 级沙箱；真实应用启动、窗口交互、UIPI/DPI 和 Stop 边界仍需 M14 实机验收。


### 18.1 M12-08 Windows 自动化验证结果

- Windows Actions run [43414530292](https://github.com/xiaobei206/anjian/actions/runs/43414530292) 对代码 HEAD `bd848281b1125e4c8441d941119de54fdd252744` 全 PASS：Restore；Release Build 0 warnings/0 errors；152/152 Core tests、0 skipped；API handbook check（102 个公开函数）；self-contained win-x64 publish；packaged Sprite/Worker smoke；ZIP/SHA256；artifact upload。
- Artifact ID `14030797666`，87,218,351 bytes，archive digest `sha256:0985b5bb96d639a51bc2a947a87869cb82b655a316651204b0b6d7a1b4b4dc25`。该 artifact 是 CI 临时构建资产，不是 GitHub Release。
- 真实 Windows 10/11 权限审阅 UI、实际 GUI EXE 启动/生命周期、DPI/UIPI、多屏和用户 Stop 交互仍 NOT RUN，统一留 M14；CI 结果不代表桌面实机 PASS。


### 18.2 M12-09 受控 HTTP(S)/JSON

- 新增 Core `ScriptHttpPolicy` 与 `ScriptHttpSession`；Worker `HttpRequest` 将 Lua options 转为受限的 JSON IPC；PlaybackRunner host 每次重新解析当前 Grant 并调用 HTTP 会话，未知 IPC 继续 fail-closed。
- `SocketsHttpHandler` 关闭自动重定向、Cookie、代理和凭据继承；Host 最多手动处理 5 跳，每跳验证精确主机、方法和 HTTPS/获批 HTTP。301/302 POST、303 非 HEAD 改 GET；307/308 保留方法/正文；跨 origin 清除 Authorization 和用户自定义凭据头。
- URL 2,048 字符；默认端口 only；DNS 精确主机、无通配符/IP/userinfo/fragment；仅 GET/POST/PUT/PATCH/DELETE/HEAD。请求头最多 32 项/32 KiB，body/json 最多 1 MiB；响应头最多 64 项/32 KiB，解压后正文最多 4 MiB；JSON 深度 32。
- timeout 默认 30 秒、1–120 秒；Stop token 贯穿 SendAsync、redirect 与 response stream。HTTP 状态码直接作为正常响应对象返回；无效 UTF-8、错误 JSON、超限、未批准跳转与网络失败报告错误。
- 请求头/正文和响应正文不自动写应用日志。只实现显式有界缓冲请求，不支持 Cookie jar、代理、自定义 DNS、任意 TCP/WebSocket 或大文件流式传输。
- 真实 WinForms 权限审阅、HTTPS/HTTP、重定向、DPI 与 Stop 桌面验收继续留 M14。


### 18.3 M12-09 Windows 自动化验证结果

- Windows Actions run [43427860126](https://github.com/xiaobei206/anjian/actions/runs/43427860126) 对功能/测试 HEAD `e0301a3641a123792a9aa584fd4a8b09b4c7409b` 全 PASS：Restore；Release Build 0 warnings / 0 errors；161/161 Core tests、0 skipped；API handbook check（103 个公开函数）；self-contained win-x64 publish；packaged Sprite/Worker smoke；ZIP/SHA256 与 artifact upload。
- Artifact ID `14037418047`，87,234,303 bytes，archive SHA-256 `f8eafdbeb01727c96467a1882a95e9e55db40e4dd83d82e82c19ac3199b314bd`。没有创建 GitHub Release；该临时 artifact 有效至 2026-12-13。
- M12-09 的真实 Windows 10/11 权限审阅窗口、TLS/HTTP/重定向、网络错误、Pause/Stop、DPI 与实际桌面交互仍 NOT RUN，统一留 M14；CI PASS 不等于桌面实机验收。


### 18.4 M12-10 本地插件包与扩展 Worker

- PluginPackageValidator 负责 manifest schema、身份/semver/API 兼容、路径/扩展名和资源限制、精确 files 清单、SHA-256、sidecar 与源树 reparse point 检查；PluginPackageStore 负责 directory/ZIP staging 安装、版本目录、catalog、启停与卸载。
- Catalog 位于用户本机插件根目录；包根目录为 %LOCALAPPDATA%/Anjian2026/Plugins/<pluginId>/<version>。初次安装关闭；同 ID 只允许一个启用版本；禁用版本不可解析为执行计划，卸载仅限 disabled；损坏或未知未来 schema 不自动覆盖。
- 插件入口和模块 hash 清单经 Worker launch payload 传给独立 Worker。Worker 在执行前验证入口相对路径和 SHA-256；自定义 require 只允许已登记的模块，模块读取再次验证 SHA-256。插件代码不在 WinForms UI 线程执行，也不通过 plugin manifest 动态修改公开 API registry。
- 有效 grant 是脚本 sidecar request、插件 manifest ceiling 与用户本次 approved Grant 的交集。文件路径集合保留较窄的重叠路径，进程可执行文件/HTTP host/method/dialog 集合精确相交，clipboard/allowInsecureHttp 用 AND；host 对每条 IPC 仍执行既有 grant 校验。
- SHA-256 只检测与 manifest 不一致的修改，不是发布者签名或可信来源证明；能够同时改写 manifest 和文件的人仍可构造自洽包。独立 Worker 是执行隔离边界，不是完整 OS 沙箱。只导入可信插件，不加载 DLL/EXE，不承诺隔离任意恶意同用户代码。

- Windows Actions run [43433992383](https://github.com/xiaobei206/anjian/actions/runs/43433992383) 对代码 HEAD 582db78d9bddbed820727dd1bf01b19847beb5f6 全 PASS：Restore；Release Build 0 warnings / 0 errors；Core tests 171 passed / 0 failed / 0 skipped；API handbook check；self-contained win-x64 publish；published plugin Worker 与 Sprite Worker smoke；ZIP/SHA256 和 artifact upload 全 PASS。Artifact ID 14042985442，87,263,152 bytes，archive SHA-256 ebe8ca9bc7c4f82723e86628da5755bd783d655a9c1a170c7112336919e95af1，有效至 2026-12-13；这是 Actions 临时 artifact，不是 GitHub Release。


## 19. M13 多实例运行实例与资源隔离（ADR-033；M13-01/02）

M13-01 先冻结契约；新增 Core 类型只负责身份、状态转换和并发界限，不代表主程序已经可以同时运行多个脚本。当前 main 仍以单个 `PlaybackRunner` 和单活动运行作为现状；多实例协调器属于 M13-02。

### 19.1 身份与一次启动的边界

- `RunInstanceId` 为小写 32 位 GUID-N；一次明确 Start 只分配一次。重复派发同一个 `startRequestId` 必须幂等，同脚本的另一明确 Start 则是新实例，不做按路径去重。
- 逻辑实例贯穿全部 RepeatCount。每一轮可以启动新的 Worker，但必须有独立 `workerAttemptId`、命名管道和 nonce；IPC 服务端把该 pipe/session 固定映射到一个 instanceId/attempt，不按消息正文自报身份选择资源。
- 实例快照封存规范化脚本路径/内容版本、参数、播放选项、权限、插件计划和创建时刻。普通脚本在 Runner 启动时读取源码快照并发送给本 attempt Worker；编辑器后续更改不影响已封存内容。相同 startRequestId 重用到不同脚本路径会拒绝。

### 19.2 私有资源和生命周期

- 每实例拥有 Worker/Lua state、IPC 会话、ExecutionGate、CancellationTokenSource、运行计时、随机 process handles、HTTP session、Window 引用、Image cache、Plugin contribution snapshot、debug wait source、日志/异常上下文和输入释放登记。不能让 runner 复用另一实例的可变字段。
- Core 状态类型：`RunInstanceState` + 独立的 `RunInstanceOutcome`、`RunInstanceTerminationMode`、`RunInstanceCleanupStatus`。状态转换表由 `RunInstanceLifecycle.AllowedTransitions` 导出，并由测试枚举所有状态对核对；首个终止原因不可覆盖，进入 Cleaned 前必须完成清理并写入成功/失败结果。
- 容量契约：同时最多 4 个 Worker 槽位实例，另有最多 16 个 FIFO 排队项。正在启动、运行、暂停、终止和清理仍占用槽位；只有 Cleaned 后才释放。队列满立即明确拒绝。Worker 启动连接 watchdog 30 秒，Stop 后给 Worker 2 秒正常退出宽限，再强制杀进程树。M13-01 仅冻结 `RunInstanceLimits` 常量，尚无 queue/coordinator/watchdog 运行时执行。
- 实例运行超时从首个 Running 开始；验证、权限提示和排队不计时；Running 与 Paused 都计入墙钟时间，循环间隔也计入。具体用户可配置的上限/默认值由 M13-03 冻结。
- 首个状态提交决定 Stop 与 timeout 的胜者；用户 Stop 锁定 Cancelled，timeout 锁定 TimedOut，其他后续异常只能附加诊断，不能改写结果。清理异常另由 CleanupStatus 反映。正常自然结束的 TerminationMode 为 None；Graceful 仅对应经过 Cancelling 的停止/超时流程，Forced 仅对应 ForceTerminating。Core 转换器本身不提供跨线程互斥；App coordinator 用单一 admission/lifecycle lock 串行化状态提交、Stop/Pause 与 Complete/队列晋升。

### 19.3 输入、权限和 IPC 隔离

- SendInput/SetCursorPos 是整个桌面共享的全局副作用。所有实例实际共用主进程唯一 FIFO InputArbiter；单个 Click/KeyPress/TypeText 等原子事件不可交错，显式 Down 持有 owner lease 直到 Up。暂停时继续持有输入租约；Stop/Worker exit 必须发出属于自己的 Up 并清空 owned-held 集合后才释放。清理失败时全局输入仲裁进入故障锁定，主窗体显示故障原因并要求用户人工确认物理键鼠状态后才允许恢复。
- Worker 可以拥有各自 Lua state 和缓存，却不能通过独立 controller 绕过全局输入仲裁。纯 UIA 操作可并行，但窗口引用和 provider 缓存仍实例私有。
- 每实例重新解析权限请求并执行一次用户审阅。Grant 不按脚本路径共享、不持久化；App coordinator 为每实例复制只读 Grant 和参数快照，Worker 启动载荷只由其 pipe 的宿主会话发送，host 保留权威 Grant 并逐 IPC 双重验证。Worker IPC 同时绑定 instanceId、workerAttemptId 和 256-bit session nonce；host 另用 GetNamedPipeClientProcessId 核对 pipe 客户端 PID 必须等于该实例启动的 Worker PID。ProcessSession 的随机句柄字典属于单实例，跨实例查询被拒绝。
- 每条日志/状态事件必须带 instanceId；Worker attempt 及循环序号可用于诊断 Worker 崩溃、重复执行和清理失败。不得记录敏感参数或请求/响应正文。

### 19.4 当前可机读契约与下一步

- Core：`Anjian.Core.Runtime.RunInstanceLifecycle`、`RunInstanceId`、`RunInstanceSnapshot`、`RunInstanceLimits`。契约常量：4 active、16 queued、30,000 ms Worker startup watchdog、2,000 ms cancellation grace；这些常量尚未由运行时协调器强制执行。
- 自动化测试覆盖 GUID-N 与 IPC identity/nonce 伪造拒绝、同脚本多实例/Start 幂等、4+16 容量/FIFO 晋升/排队取消、独立状态、跨实例进程句柄拒绝、输入仲裁 FIFO/取消/故障锁定/人工恢复，以及启动超时结果不被 Worker 后续失败覆盖。发布包 smoke 验证多个真实 Worker 同时存活、单独 Stop 和 Worker crash 不影响兄弟实例。
- M13-02 已落地 coordinator、Worker payload/IPC identity、全局 InputArbiter 与 owner-only cleanup。M13-03 负责用户运行超时/定时触发/失败策略；M13-04 负责完整多实例 UI、日志筛选与恢复；M14 负责真实 Windows 10/11 桌面验收。


### 19.5 M13-02 当前实现状态

- RunInstanceCoordinator 管理稳定实例、请求幂等、4 active + 16 FIFO、排队取消、Pause/Resume/Stop/Timeout 状态和 Cleaned 后晋升；App coordinator 将状态提交放在同一锁内，避免自然完成与用户 Pause/Stop 的状态竞争。
- 每个 PlaybackRunner 的 Worker/Lua、pipe、attempt、nonce、CTS、pause gate、ScriptProcessSession、HTTP session、Window/Image/debug 资源均独立；普通脚本源码、参数、播放选项、Grant 和 plugin runtime options 在实例启动时复制。每次 Start 继续执行独立权限审阅，不按路径共享审批。
- Worker pipe 使用 current-user ACL、OS client PID 绑定、identity policy 逐消息检查和响应检查；无效、跨实例、跨 attempt 或 nonce 不匹配均拒绝。
- 全局 InputArbiter 在故障时拒绝新 lease，Input release failure 需人工恢复；这不解决宿主进程崩溃/断电下操作系统无法收到 KeyUp 的边界。
- MainForm 目前仅提供选中/最新实例的基础 Pause/Resume/Stop、容量状态与输入故障恢复；没有完整运行实例列表、筛选和实例日志独立视图。定时/用户配置运行超时/重试尚未实现。
