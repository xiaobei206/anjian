# 技术架构与模块边界

- 版本：Architecture 0.3
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

目前没有独立的 Anjian.Scripting、Anjian.Automation、Anjian.Recorder、Anjian.ProjectSystem 或 Imaging 项目；NLua Host、播放、录制和输入适配目前主要位于 Anjian.App 中。不要在接手时假设这些拆分项目已经存在。

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

## 7. Windows 输入与坐标

- 默认使用 Windows SendInput 实现前台键鼠注入。
- 启动时明确设置 Per-Monitor V2 DPI awareness。
- 坐标统一为虚拟桌面物理像素，原点和范围以 Windows 虚拟桌面指标为准；负坐标合法。
- 首版脚本 API 不提供窗口相对坐标。
- 不假设 SendInput 对任何权限等级、受保护应用、游戏或安全桌面都有效。
- 程序默认普通用户权限运行；发生 UIPI/权限限制时向用户说明。
- 追踪本程序模拟的按键/鼠标按下状态，结束时发出对应释放动作。
- 原始键盘扫描码、虚拟键码和 Unicode 文本输入需要分别定义，不混为一种操作。

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
