# 架构决策记录（ADR）

状态说明：ACCEPTED = 已确认；PROPOSED = 暂定方向；VERIFY = 需 PoC 后冻结；SUPERSEDED = 已被新决策替代。

## ADR-001：首版平台与产品定位
- 状态：ACCEPTED
- 决策：首版只做 Windows 10/11 x64 桌面自动化；产品独立实现，工作名“按键精灵 2026”暂定。
- 原因：控制范围，先交付稳定的桌面版本。
- 不做：首版 Android、云同步、旧版 Q 脚本兼容。

## ADR-002：Lua 作为脚本语言
- 状态：ACCEPTED
- 决策：使用 Lua 5.4，不自创完整脚本语言。
- 原因：成熟的语言语法和流程控制；项目只维护自动化 API。
- 影响：旧版 Q 脚本不能直接执行，之后如有需求再研究转换器。

## ADR-003：录制输出支持动作与代码两种模式
- 状态：ACCEPTED
- 决策：动作列表可编辑并生成 Lua；Lua 可自由编辑，但不承诺任意代码无损转回动作列表。
- 原因：避免复杂 Lua 结构与有限动作模型之间的伪双向同步。
- 影响：生成区块需 ID、schemaVersion、dirty/detached 状态和覆盖确认。

## ADR-004：桌面 UI 使用 WinForms
- 状态：ACCEPTED
- 决策：首版 UI 使用 WinForms。
- 原因：Windows 桌面工具、经典停靠面板和低额外依赖。
- 影响：编辑器/Dock 组件需独立验证，不得将第三方控件类型泄漏到业务层。

## ADR-005：目标框架使用 .NET 10 LTS
- 状态：ACCEPTED
- 决策：目标框架 net10.0-windows，发布先做 win-x64 self-contained。
- 原因：当前 LTS 生命周期适合长期维护。
- 参考：微软生命周期页面 https://learn.microsoft.com/zh-cn/lifecycle/products/microsoft-net-and-net-core

## ADR-006：Lua 绑定与编辑器依赖不提前锁版
- 状态：VERIFY
- 候选：NLua/KeraLua；Scintilla.NET 系列。
- 决策：先做 PoC，确认 Lua 5.4、指令钩子、行号、原生 DLL、许可证和 .NET 10 支持后再锁定。
- 禁止：仅凭网上版本号或旧聊天记录认定兼容。

## ADR-007：脚本执行不占用 UI 线程
- 状态：ACCEPTED
- 决策：Lua state 由独立执行线程串行访问；首版暂不引入独立 Worker 进程。
- 原因：满足 UI 响应和取消要求，同时避免首版 IPC 复杂度。
- 重新评估条件：PoC 或稳定性测试证明同进程线程模型无法满足隔离/恢复要求。

## ADR-008：录制采集源先验证 Raw Input
- 状态：VERIFY
- 决策：先比较 Raw Input 与 WH_KEYBOARD_LL / WH_MOUSE_LL，默认不预设低级钩子为最终实现。
- 原因：低级钩子回调有时限，超时可能被 Windows 静默移除；录制场景通常无需屏蔽物理输入。
- 参考：https://learn.microsoft.com/en-us/windows/win32/winmsg/lowlevelkeyboardproc

## ADR-009：输入注入采用标准 Windows API
- 状态：ACCEPTED
- 决策：首版前台输入基于 SendInput，不默认使用驱动或内核组件。
- 限制：遵守 UIPI/完整性级别与受保护桌面限制；不承诺所有窗口可操作。
- 参考：https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-sendinput

## ADR-010：main 为唯一长期集成分支
- 状态：ACCEPTED
- 决策：本项目默认小步提交 main，不建立长期分支/PR 流程。
- 原因：个人项目，优先可见、易回滚和低维护成本。
- 约束：写入前检查 HEAD；禁止 force push。

## ADR-011：录制数据以结构化动作作为来源
- 状态：ACCEPTED
- 决策：动作列表到 Lua 代码为明确的一向生成；修改生成区块后标记不同步。
- 原因：无法保证任意 Lua 代码反解析成动作步骤。

## ADR-012：便携版优先
- 状态：ACCEPTED
- 决策：首版优先 self-contained win-x64 文件夹 ZIP；暂不强求单文件 EXE。
- 原因：原生 Lua/编辑器组件可能有独立 DLL，文件夹式部署更便于排错。

## ADR-013：产品版本与交付纪律
- 状态：ACCEPTED
- 决策：使用语义化版本，不使用 FIX 版号；发布文件名含产品名与版本，文档和 SHA 对齐。

## ADR-014：用户脚本采用全局 Lua 函数 API
- 状态：ACCEPTED
- 决策：用户脚本使用 `MoveTo(122, 100)`、`LeftClick(50)`、`Delay(300)` 这类全局函数调用，不强制使用 Mouse / Keyboard 命名空间；语法仍是标准 Lua。
- 原因：直观、输入短，适合录制器生成和普通用户阅读。
- 影响：内部实现仍分层；Lua 注册中心向脚本暴露稳定全局 API。

## ADR-015：录制时合并鼠标移动
- 状态：ACCEPTED
- 决策：连续鼠标移动采用合并/降噪，只保留必要路径采样点和终点；所有点击、按下、释放、滚轮及顺序完整保留。
- 原因：避免录制代码被海量移动事件淹没，同时维持可回放轨迹。
- 影响：合并阈值需通过录制 PoC 和实机测试调优。

## ADR-016：首版只使用虚拟桌面绝对坐标
- 状态：ACCEPTED
- 决策：统一使用虚拟桌面物理像素坐标，允许副屏负坐标；首版不做窗口相对坐标。
- 原因：录制和回放坐标语义一致，先控制复杂度。
- 影响：必须验证多显示器和 DPI awareness，用户移动窗口后脚本仍按屏幕坐标执行。

## ADR-017：点击和单键按下持续时间有默认值
- 状态：ACCEPTED
- 决策：LeftClick / RightClick / MiddleClick 及 KeyPress 的 holdMs 默认 50 ms；可传入显式整数毫秒。传入 0 表示不增加等待；超过 600,000 ms 使用 Down / Delay / Up 组合而不是单次 Click/Press。
- 原因：按下和释放需要真实、可控的时间间隔，不能将普通点击实现成近乎同时的两个系统事件。
- 影响：Click / KeyPress 必须取消安全；停止时立即释放，暂停在当前原子动作结束后生效。


## ADR-018：冻结完整产品目标与经典 PC 功能等价范围
- 状态：ACCEPTED
- 决策：正式产品目标是完整 Windows 桌面自动化开发环境，不仅是录制和循环播放小工具。IDE/调试、可视化动作列表、多坐标空间、窗口/控件、前后台能力适配、图色/OCR、参数/自定义 UI、函数手册、插件与“小精灵”独立打包均进入正式范围。
- 原因：用户明确要求在大纲中一次性确定完整目标，并按路线图继续开发，不能持续把关键功能留到未定义的远期。
- 影响：PRD-CN.md 是完整需求基线，FEATURE-MATRIX-CN.md 是逐项状态/目标/验收表，ROADMAP-CN.md 只管理开发顺序。任何删减目标范围须先征得用户确认。
- 非目标：旧版 Q 语言和旧插件二进制完全兼容、移动端、云端脚本商城、远程群控、内核/驱动与安全绕过。

## ADR-019：公开 API 必须有单一注册表和中文函数手册
- 状态：ACCEPTED
- 决策：建立机器可读公开 API 注册表；注册表为运行时绑定验证、编辑器补全、参数签名提示、命令面板和中文函数/参数手册的单一来源。
- API 描述至少包含名称/签名、参数类型/必填/默认/范围/单位、返回值、错误、副作用、坐标语义、暂停/停止行为、后端/权限、最低 API 版本、示例、测试 ID 和实现状态。
- 影响：新增或修改任何公开函数时必须同步更新注册表、自动生成的手册/提示和契约测试；计划函数不能显示为可用函数。


## ADR-020：UI Automation 在可停止的 Lua worker 内运行

- 状态：ACCEPTED（M7）
- 决策：UI Automation/COM 调用必须运行在每轮独立 Lua worker 进程中，禁止把可能阻塞的第三方 provider 调用直接放在主 UI 进程。主进程仍是 M6 窗口引用与前台 SendInput 的唯一所有者。
- 窗口绑定时，主进程仅经本轮唯一命名管道把经过 M6 引用校验的短期目标身份描述送入 worker；脚本只接触随机 binding/control ID，不暴露 HWND 或 .NET/UIA 对象。每个 UIA 操作前 worker 重新请求主进程复核原窗口 ID，并验证句柄、PID、线程、类名与进程启动时间。
- Stop 通过终止 worker 来中断可能挂起的同步 UIA/COM provider 调用；主进程的 UI 不等待该 COM 调用结束，并继续执行统一输入释放。所有 binding 与 AutomationElement 缓存随 worker 生命周期结束。
- UIA 的 Value/Selection/Toggle/Invoke 被定义为标准控件语义操作，不能称作通用后台键鼠输入。通用后台鼠标、键盘和文本注入在未经过具体技术验证之前明确返回 unsupported；不伪造 PostMessage 输入、不绕过 UIPI/UAC/安全桌面。
- 影响：新增控件 API 时必须声明实际 Pattern、支持状态、错误/限制，并加入能力探测和 worker 停止测试。CI PASS 不视为 Windows 桌面验收完成。


## ADR-021：OCR 使用可替换的本地引擎适配层
- 状态：ACCEPTED（M9 首轮实现；真实 Windows 质量与性能验收延期）
- 决策：Core 只定义 `IOcrRecognitionService` 和引擎无关结果 DTO；Windows App 使用锁定的 PaddleOcrNet 2.4.0 适配器，默认 CPU 推理。公开 Lua API 只消费统一的 OCR 结果模型，不暴露第三方类型。
- 离线策略：Worker 的 `PaddleOcrServiceOptions.Download.Offline` 固定为 true；执行脚本时不得隐式下载模型。用户只能通过主 UI 的“OCR 模型”管理器明确选择语言并下载模型；缓存位于 `%LOCALAPPDATA%\\Anjian2026\\Ocr\\Models`。下载过程由上游模型清单校验 SHA-256。
- 语言首轮范围：简体中文 `ch`、英语 `en`、日语 `japan`。识别输出使用统一 DTO，包含全文、逐行置信度、局部矩形、多边形、语言、源图像尺寸和耗时；截图结果另附 screen 绝对坐标。
- 停止/隔离：OCR 推理只能发生在独立 Lua Worker；Stop 通过终止 Worker 打断阻塞的原生推理，主 UI 不等待 OCR 引擎。WaitText 在每轮截图前经 checkpoint 支持暂停，临时截图在 finally 路径释放。
- 影响与保留意见：PaddleOcrNet 2.4.0 的上游规模较小，因此只能通过适配层依赖；不得将 Paddle 类型泄漏至 Core。若未来替换引擎，只需重写 App adapter 和模型管理 UI，不改变 Lua/DTO 契约。发布前仍需核对包许可证、所有传递依赖的许可和 win-x64 native assets，并在真实 Windows 10/11 测试精度、速度、内存和断网行为。


## ADR-022：脚本参数使用独立版本化 schema 与类型安全 Worker 注入
- 状态：ACCEPTED（M10 实施基线）
- 参数定义：每个脚本使用同目录 sidecar `<script>.params.json`，schemaVersion 固定为 1。元数据包含稳定参数 ID、机器名、中文标签、类型、必填、默认值、数值边界、选项、说明、验证规则、排序与敏感标记。读取参数定义绝不执行用户 Lua。
- 运行配置：用户值单独存放在用户数据目录的配置文件中，使用脚本规范化路径的 SHA-256 派生 ID，不写回脚本目录。参数定义与运行值分离，默认值和用户值均经同一 validator 验证。
- 类型：text、integer、number、boolean、select、multiSelect、filePath、directoryPath、hotkey、color。JSON 保留真实类型；路径只验证语法/存在性规则，不自动打开文件；热键使用规范化字符串，颜色使用 #RRGGBB 或 #AARRGGBB。
- 启动事务：先读取 schema 和用户配置，验证 schema/version、字段 ID/名称唯一性、类型、选项、范围、必填和每个值；任何错误都在创建 Worker 之前返回字段级错误。未知未来 schemaVersion 必须只读拒绝，不能自动备份后覆盖原文件。
- IPC：启动握手以独立结构化 JSON 载荷传入已验证参数；Worker 将值转换为 Lua 原生 string/number/boolean/table。禁止将用户值拼接到 Lua 源码。Lua 全局只读入口 `Args` 通过代理拒绝顶层写入；值表在 Worker 内构建，用户输入不被当作代码执行。
- 持久化：配置写入同目录临时文件、flush、原子替换；替换前保留上一份有效备份。损坏配置可恢复有效备份或使用默认值并给出明确状态；导入 JSON 必须经过同一 validator。导出时默认对敏感参数进行提示/脱敏，显式导出才允许包含敏感值。
- UI：从 schema 生成参数对话框；开发者通过“参数设计器”新增/编辑/删除/排序定义。表单控件和 validator 共用类型定义；取消参数对话框不启动 Worker。打包器 M11 复用同一 schema/validator。
- 测试：Core 单元测试覆盖每种类型、边界/必填/选项、多选、路径/热键/颜色、序列化往返、未知 schema、损坏备份恢复、原子替换故障、注入类型与恶意字符串。Windows CI 不代替 WinForms 实机验收；M10 桌面项延后 M14。
