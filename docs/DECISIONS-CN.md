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
- 类型：text、integer、number、boolean、select、multiSelect、filePath、directoryPath、hotkey、color。JSON 保留真实类型；文件/目录路径只校验语法/可选存在性，不自动打开；相对路径以脚本所在目录为基准，用户配置保留原始相对值，Worker 接收规范化绝对路径；热键使用规范化字符串，颜色使用 #RRGGBB 或 #AARRGGBB。参数机器名必须是非 Lua 保留字的唯一 Lua 标识符。
- 启动事务：先读取 schema 和用户配置，验证 schema/version、字段 ID/名称唯一性、类型、选项、范围、必填和每个值；任何错误都在创建 Worker 之前返回字段级错误。未知未来 schemaVersion 必须只读拒绝，不能自动备份后覆盖原文件。
- IPC：启动握手以独立结构化 JSON 载荷传入已验证参数；Worker 将值转换为 Lua 原生 string/number/boolean/table。禁止将用户值拼接到 Lua 源码。Lua 全局只读入口 `Args` 通过代理拒绝顶层/嵌套写入，`pairs` 迭代也递归返回代理值；Lua `rawset` / `debug` 可绕过代理，因此它只防止常规误写，不构成恶意脚本沙箱。
- 持久化：配置写入同目录临时文件、flush、原子替换；替换前保留上一份有效备份。设计器保存现有 sidecar 前必须成功读取/验证其 schemaVersion，未知未来版本或损坏定义不得被静默覆盖。损坏配置可恢复有效备份或使用默认值并给出明确状态；导入 JSON 必须经过同一 validator。导出时默认对敏感参数进行提示/脱敏，显式导出才允许包含敏感值。
- UI：从 schema 生成参数对话框；开发者通过“参数设计器”新增/编辑/删除/排序定义。表单控件和 validator 共用类型定义；取消参数对话框不启动 Worker。打包器 M11 复用同一 schema/validator。
- 测试：Core 单元测试覆盖每种类型、边界/必填/选项、多选、路径/热键/颜色、序列化往返、未知 schema、损坏备份恢复、原子替换故障、注入类型与恶意字符串。Windows CI 不代替 WinForms 实机验收；M10 桌面项延后 M14。


## ADR-023：小精灵使用自包含目录 apphost、稳定身份和启动前完整性验证
- 状态：ACCEPTED（M11）。
- 交付形式：portable 文件夹 + 带 <ProductName>.exe 启动入口的 launcher ZIP。EXE 是 self-contained .NET apphost 的重命名副本，伴随文件不可删除；不宣传单文件。
- 包 ID 使用规范化 ProjectRoot + ProductName 派生稳定标识，入口 ID 使用包 ID + 工程相对路径派生；同一工程同一产品的版本升级不更换配置路径。用户配置独立存储 LocalAppData，绝不写进发布包。
- 依赖策略：静态 require 递归解析；动态 require 在构建时拒绝。LoadImage 路径相对入口脚本目录并限制在项目根目录；图像 catalog 以既有 manifest/hash 校验。OCR 只打包用户显式选择且本地缓存完整的模型，运行时离线。
- Anjian.Sprite.json 列明所有包 payload 文件、尺寸、kind、SHA-256、最低 API/.NET 版本与入口元数据；SHA256SUMS.txt 和 ZIP .sha256 供完整性检查。SHA-256 不提供签名/发行者认证。
- Launcher 启动前校验清单闭合、hash、入口、参数 schema、图像 manifest/assets、OCR 文件和 API/runtime 兼容性；验证失败时禁止执行 Worker。
- 参数沿用 ADR-022；Worker 只增加可选包模块目录和 OCR 模型目录，不改变既有 typed Args 语义。
- 包内包含 Markdown 函数参考；PRD 2.14 的离线 HTML 仍待帮助系统后续实现。Windows 自动化 smoke 不是桌面 UI/真实输入验收，后者统一留给 M14。


## ADR-024：脚本权限采用声明与逐次运行批准分离的默认拒绝模型
- 状态：ACCEPTED（M12 首阶段安全契约；OS API 与插件 Host 将按本 ADR 接入）。
- 声明文件：每个脚本可选同目录 <script>.permissions.json，UTF-8 JSON、schemaVersion=1，最大 256 KiB。它只表达请求，不代表授权；未声明的能力一律拒绝。
- 声明字段：fileReadRoots / fileWriteRoots 为脚本目录相对或绝对目录；clipboardRead / clipboardWrite 为布尔值；dialogs 仅允许 message、input、confirm；processExecutables 只允许明确 .exe 绝对/相对路径；httpHosts 为精确 DNS 主机名（不接受通配符），httpMethods 限 GET/POST/PUT/PATCH/DELETE/HEAD；明文 HTTP 需另有 allowInsecureHttp=true。
- 授权时机：主进程在每次脚本启动前展示本次请求与具体资源范围，由用户明确选择批准子集；取消或关闭对话框即全拒绝。M12 不将同意默认为永久，也不从脚本声明自动授权。
- 执行边界：Worker 与主进程共享同一份不可变 ScriptPermissionGrant。Lua bridge 在调用前检查一次；主进程 IPC dispatcher 对 OS 操作再次检查，Worker 伪造 IPC 也不能绕过主机权限。任何运行模式缺少审批对象即使用空权限集合。
- 文件系统：路径先规范化为完整路径，检查目录边界时使用 Path.GetRelativePath，不使用字符串前缀。Windows 文件适配器还必须逐级拒绝 reparse point/符号链接，并在最终打开时防止检查-使用竞态；Core 纯路径策略本身不宣称解决符号链接攻击。
- 进程：只允许启动用户批准的精确 .exe 路径；不提供任意 Shell、参数拼接执行或终止任意进程的初始能力。
- 网络：HTTPS 默认允许；HTTP 只有独立明确批准才允许。主机和方法分别做精确 allowlist；不支持通配符、重定向到未批准域名或继承外部代理凭据。请求必须有超时与 Stop cancellation token。
- 插件：M12 首版采用本地 ZIP/目录包和 manifest，不执行插件 DLL、不在 UI 进程执行第三方代码。插件 Lua entry/module 只能在独立 Worker 加载；manifest API 版本不兼容或文件清单/hash 不完整则拒绝启用。插件声明的权限与脚本声明取交集，用户批准取更小子集。
- 权限 UI 与 OS 桌面体验留 M14 实机验收；CI 只验证策略和 IPC/资源适配，不把 CI 结果写成实机 PASS。


## ADR-025：首批文件 API 使用双层 Grant 校验与 Windows no-follow 句柄
- 状态：ACCEPTED（M12-02 首批 API；后续文件操作沿用此边界）。
- 授权：WorkerLuaApi 在构造时持有本次 ScriptPermissionGrant 与当前脚本目录；每个文件桥接函数先规范化路径并检查读取/写入根，再通过 Worker IPC 请求主机执行。PlaybackRunner 从当前运行载荷持有的 Grant 对每条文件 IPC 再检查，不能信任 Worker 传来的权限声明、路径类别或命令名。缺少 Grant 一律 ScriptPermissionGrant.Empty，未知命令拒绝。
- 路径语义：相对路径以入口脚本目录为基准；绝对路径只有处于对应批准根中才可使用；拒绝扩展设备路径、盘符相对路径、NTFS ADS 和超长路径。路径边界使用 Path.GetRelativePath，不做字符串前缀判断。
- Windows 句柄防护：祖先目录逐级以 OPEN_REPARSE_POINT 打开，确认目录属性且不共享删除；最终文件同样以 OPEN_REPARSE_POINT 打开并在句柄上检查 reparse point 与硬链接数。操作期间保留目录/文件句柄，缩小路径替换竞态。目录枚举仅一层，且逐项验证；符号链接/reparse point 与多硬链接普通文件不返回。
- API 范围：首批 ReadTextFile / WriteTextFile / AppendTextFile / FileExists / DirectoryExists / ListFiles / ListDirectories / CreateDirectory；M12-02B 再加入 CopyFile / MoveFile / RenameFile / CreateTempFile / CreateTempDirectory。严格 UTF-8，文本 4 MiB，目录枚举 1000 项；Copy/跨卷 Move 限 64 MiB。Copy 写目标同目录临时文件、flush + SHA-256/长度验证后原子提交；同卷 Move/Rename 用源文件句柄级 rename；跨卷 Move 验证目标后才删除源。Stop 在复制分块和提交前检查。
- 临时路径：只接受调用者显式给出的已批准写入目录，使用随机名称与 CREATE_NEW/单级目录创建，不自动授权或使用系统 TEMP。
- 限制：仍不是恶意同用户进程的完整 OS 沙箱；只支持普通文件，不支持目录复制/移动/重命名；小精灵缺失授权时仍为空 Grant。
- 验证：Windows Actions run 237 验证主机文件安全适配，run 241 验证首批 API 0.8/89 项；M12-02B 最新 Windows Actions [run 43385033936](https://github.com/xiaobei206/anjian/actions/runs/43385033936) 对 registry 0.9/94 项执行 0 warnings/0 errors Build、142/142 tests、手册一致性、自包含包和 Sprite/Worker smoke 全通过。Windows 桌面权限审阅仍由 M14 实机执行。


## ADR-026：普通文件传输采用验证后提交与受控临时路径

- 状态：ACCEPTED（M12-02B；Windows Actions run 43385033936 全 PASS，桌面实机留 M14）。
- 公开 API：CopyFile(sourcePath,destinationPath,overwrite?)、MoveFile(sourcePath,destinationPath,overwrite?)、RenameFile(path,newName,overwrite?)、CreateTempFile(directoryPath)、CreateTempDirectory(parentDirectoryPath)。overwrite 默认为 false。
- 对象边界：只接受普通文件；目录复制/移动/重命名不在本批范围。源文件路径先校验当前 Grant：Copy 要读取授权，Move/Rename 要同时读取和写入授权；目标文件/父目录必须写入授权。Worker 和 host dispatcher 分别重新校验，不信任 Worker 传入的路径类别或权限声明。
- CopyFile：最多 64 MiB；同目标目录创建安全随机临时文件（CREATE_NEW），分块复制并计算 SHA-256，flush 到磁盘后重新读取验证长度/hash，最后通过源临时文件句柄执行 FileRenameInfo 提交。Stop 在每个数据块、验证过程和提交前检查；提交边界后不再报告取消。默认不覆盖；覆盖时也必须先 no-follow 打开并拒绝 reparse point、符号链接和多硬链接目标。
- MoveFile：同卷直接对已打开源文件句柄执行 rename；跨卷先完成受限 Copy、SHA-256 验证和目标提交，随后对原源句柄标记删除。若 Stop 在目标提交后到达，则保留两个副本；若源删除失败则报告错误并保留源文件与已验证目标副本供恢复。
- RenameFile：只允许同一已批准父目录，使用 no-follow 源句柄；源路径要求读/写授权，覆盖目标先按 no-follow 验证。
- 临时 API：仅在调用者明确指定且已批准写入的目录中创建随机名称文件/目录，返回完整路径；通过 CREATE_NEW/单级目录创建处理竞态，名称碰撞只重试，不打开或覆盖既有对象。不会自动使用或授权系统 TEMP。
- 句柄边界：父目录链在操作期间以 OPEN_REPARSE_POINT 打开且不共享删除；源/已有目标以 no-follow 句柄验证 reparse 属性与硬链接数。FileRenameInfo 使用 RootDirectory=NULL 和 Windows verbatim 完整目标路径，补充 UTF-16 NUL 终止符，父目录句柄仍用于锁定路径链。
- 限制：此设计降低路径替换竞态，但不是对恶意同用户进程的完整 OS 沙箱；本批不提供递归目录操作，也不提供临时对象自动清理 API。


## ADR-027：剪贴板与对话框 API 使用 UI STA 调度和双层授权

- 状态：ACCEPTED（M12-07；Windows Actions run 43408020908 自动化验收 PASS，真实桌面验收留 M14）。
- 公开 API：`GetClipboardText()`、`SetClipboardText(text)`、`MessageBox(text,title?)`、`InputBox(prompt,title?,defaultValue?)`、`Confirm(text,title?)`。API registry 从 0.9 / 94 项递增为 1.0 / 99 项，函数手册继续由 registry 生成。
- 授权字段沿用 ADR-024：剪贴板读写分别要求 `clipboardRead` / `clipboardWrite`；对话框分别要求 `dialogs` 中精确的 `message`、`input`、`confirm`。声明不等于批准，用户每次运行可批准子集；Worker 和 PlaybackRunner host dispatcher 各自验证。
- Core 的 `ScriptClipboardDialogPolicy.ValidateCommand` 校验命令 allowlist、Grant 和请求载荷。剪贴板文本限制 1,048,576 个 UTF-16 字符；对话框正文/提示与 InputBox 默认值/输入限制 32,768 字符，标题 256 字符；未知命令及损坏/超限 payload fail-closed。
- App 使用 `ClipboardDialogHost`，通过主 Form 的 `BeginInvoke` 在 WinForms UI STA 线程调用 Clipboard，创建可取消的自定义模态窗体。InputBox 取消/关闭返回 nil；Confirm 点否/关闭返回 false；MessageBox 点确定返回 true，关闭返回 false。
- Stop 取消挂起 IPC，并关闭正在显示的自定义对话框；Pause 在命令边界生效，打开模态对话框时不强制关闭。Windows 已接受的剪贴板写入无法保证回滚。剪贴板/对话框内容不自动进入应用日志。
- 小精灵未接入 permissions sidecar 审阅，缺少 Grant 仍为空授权；禁止把这批 API 视为默认开放。M14 仍需在 Windows 10/11 实机验收权限审阅、剪贴板占用/错误、对话框停止、DPI 与多显示器布局。



## ADR-028：受控进程 API 仅操作本次批准的 EXE 与本次运行句柄

- 状态：ACCEPTED（M12-08；Windows Actions run 43414530292 自动化验收 PASS，152/152 tests、0 warnings/0 errors；桌面实机留 M14）。
- 公开 API：`StartProcess(executablePath, arguments?)`、`WaitProcess(processHandle, timeoutMs?)`、`GetProcessInfo(processHandle)`；API registry 从 1.0 / 99 项升为 1.1 / 102 项。
- 授权：复用 ADR-024 的 `processExecutables` 精确 .exe 路径声明；路径相对入口脚本目录解析。每次运行由现有权限审阅 UI 逐项默认拒绝，批准集合必须是请求集合子集。Worker bridge 和 PlaybackRunner host 分别重新验证精确完整路径；空 Grant/伪造 IPC 一律拒绝。
- 启动：仅启动批准路径中的现存 .exe；拒绝路径链中的 reparse point。使用 `ProcessStartInfo.UseShellExecute=false` 与 `ArgumentList` 逐项传递参数，不构造 shell 命令、不调用 cmd/PowerShell。参数最多 64 项，单项 8,192 字符、总长 32,768 字符；每轮 RunAsync 最多启动 32 个进程。
- 句柄：每次 Start 生成随机 GUID N 格式句柄；仅在当前 PlaybackRunner 会话内保留 Process 对象。Wait/Info 仅查找当前会话由本 API 创建且 EXE 仍获当前 Grant 的句柄，不接受任意 PID，不枚举/附加已有进程。
- 等待：`WaitProcess` 默认 30,000ms，范围 0–600,000ms；0 表示立即查询。每 100ms 或更短轮询一次，并经 ExecutionGate 处理 Pause/Stop；超时返回 false，进程已退出返回 true，GetProcessInfo 在退出后读取 exitCode。
- 生命周期：Stop 只取消 Worker/等待，不终止已启动外部应用。单轮运行结束后释放 Process 对象句柄；已启动的外部进程继续由 Windows 独立运行。当前子任务不提供任意进程终止、Shell、HTTP/JSON 或插件。
- 限制：路径/reparse 检查降低误启动风险，但进程 API 不是 OS 沙箱；用户批准的 EXE 本身仍可以执行其自身逻辑并访问该用户已有能力。

## ADR-029：受控 HTTP(S)/JSON 请求 API 使用逐跳授权与有界缓冲

- 状态：ACCEPTED（M12-09 首批网络 API；Windows 自动化验证不代替 M14 桌面验收）。
- 公开 API：`HttpRequest(url, options?)`，API registry 从 1.1 / 102 项递增为 1.2 / 103 项。options 可包含 method、headers、body、json、timeoutMs、followRedirects；body 与 json 互斥。json 值必须能无损映射为有限深度的 JSON object/array/scalar。
- 权限：复用 ADR-024 的 `httpHosts`、`httpMethods`、`allowInsecureHttp`。主机名必须是精确 DNS 名称，不接受通配符；方法仅 GET/POST/PUT/PATCH/DELETE/HEAD。HTTPS 默认；HTTP 必须同时被请求和本次用户批准。权限不包含任意 IP 字面量。由于 sidecar 的 host 字段不表达端口，本批只允许 HTTP 80 / HTTPS 443 默认端口，非默认端口拒绝。URI 不允许 userinfo 与 fragment。
- 双层检查：Worker bridge 在发 IPC 前使用当前 Grant 验证 URL、method、头和正文；PlaybackRunner host 每次接收 IPC 都重新解析并验证，不信任 Worker 的预检。Host 对首次请求以及每次 redirect 都重新验证目标 scheme、精确 host 和当时实际使用的 method；method 经 redirect 规则变化后也必须另行授权。
- 请求契约：默认 GET，timeout 默认 30,000ms、范围 1,000–120,000ms；URL 最多 2,048 字符；请求头最多 32 项，名称最多 128 字符，单值最多 8,192 字符、合计最多 32 KiB；禁止 Host、Cookie、Proxy-Authorization、Connection、Transfer-Encoding、Content-Length、Upgrade、TE 等 hop-by-hop/由传输栈管理的头。请求正文严格 UTF-8，body/json 序列化后最多 1 MiB。json 作为 Lua 值序列化并默认补 `Content-Type: application/json; charset=utf-8`，显式 Content-Type 优先。
- JSON 契约：响应始终返回 UTF-8 文本正文；只有 Content-Type 为 application/json 或 `+json` 时解析为 Lua table/原生 scalar，JSON 最大深度 32；声明为 JSON 但内容无效时产生明确错误。非 JSON 响应的 `json` 字段为 nil。状态码不代表传输异常：4xx/5xx 仍作为正常响应对象返回，调用方检查 `ok`。
- 返回值：`{ statusCode, reasonPhrase, ok, url, headers, body, json }`。url 为最终响应 URL；headers 为有界且过滤敏感响应头后的字典；body 最多 4 MiB（按解压后实际读取字节数计），响应头上限 32 KiB。正文按严格 UTF-8 解码；不支持其他 charset。超 Content-Length 或流式累计上限均拒绝，不截断、不返回部分正文。
- 重定向：禁用 HttpClient 自动跳转，由 Host 手动最多跟随 5 次；followRedirects 默认 true，false 时将 3xx 作为普通响应返回。301/302 的 POST 与 303 的非 HEAD 请求转 GET 并丢弃正文；307/308 保留 method/body。每一跳均重新授权；HTTPS 降级到 HTTP 仍需独立明文授权。跨 origin 重定向不转发 Authorization 或用户自定义凭据头；Cookie 容器关闭。
- 网络栈：不继承默认凭据、不使用 Cookie、不使用外部代理凭据；不把请求 URL、头、正文或响应正文自动写入日志。不得在应用 UI 线程上同步等待网络。
- 取消/暂停：请求在 PlaybackRunner 命令边界等待 Pause；启动后 Pause 不暂停 socket 或延长 timeout，响应完成后下一命令边界继续等待。Stop 令牌贯穿发送、重定向和响应流读取；Stop 立即取消本次操作。timeout 与 Stop 区分报告；两者都不得返回半截响应。
- 错误：参数/JSON/header/URL 无效、缺少 host/method/明文批准、重定向越权、超时、Stop 取消、响应体/头超限、非 UTF-8 正文、无效 JSON 和网络异常均有明确失败路径；未知 IPC 一律 fail-closed。
- 范围限制：本批提供有上限的缓冲请求，不是任意 TCP、WebSocket、代理配置、Cookie jar、下载器、上传器或浏览器会话。通过 body/json 可以向已授权服务发送文本/JSON；大文件/流式传输保留后续独立设计。网络请求只由脚本显式调用，不会自动联网。
- 验证：Core 契约测试使用可注入 HttpMessageHandler 覆盖授权、重定向、请求头、timeout、Stop、响应上限、JSON 与 Worker Lua table。Windows Actions run [43427860126](https://github.com/xiaobei206/anjian/actions/runs/43427860126) SUCCESS：161/161 Core tests、0 warnings/0 errors、103 项 API handbook、自包含包与 Sprite/Worker smoke、ZIP/SHA256/artifact upload 全 PASS；Artifact 14037418047，87,234,303 bytes，SHA-256 f8eafdbeb01727c96467a1882a95e9e55db40e4dd83d82e82c19ac3199b314bd。WinForms 权限审阅、真实网络/DPI/Stop 交互仍留 M14。


## ADR-030：本地 Lua 插件包、权限交集与 Worker 生命周期

- 状态：ACCEPTED（M12-10 实现落地；Windows CI 最终结果见路线图、开发日志与 handoff；真实桌面验收仍留 M14）。
- 插件采用本地目录或 ZIP 包，根 manifest 为 Anjian.Plugin.json，schemaVersion=1。格式详见本地插件包格式规格 PLUGIN-MANIFEST-CN.md。pluginId 稳定、版本语义化，按 pluginId/version 独立安装；初次安装默认禁用。
- manifest 声明 minApiVersion/maxApiVersion（major.minor），以唯一 Lua API registry 当前 apiVersion 做区间兼容检查；不修改当前 Lua API registry，也不将插件提供的函数伪装成已注册的内置 API。
- files 排除 manifest 自身、但必须精确列出包内其余文件和 SHA-256；安装和每次运行都校验完整闭合文件集、SHA、大小、路径边界和 reparse/symlink。拒绝 DLL/EXE/Shell/驱动/安装器，不执行包内原生代码；ZIP 解包有文件数与解压字节上限，拒绝穿越、重复路径和链接。
- 插件 entry 和模块仅由单独 Worker 执行。Worker 对 entry/module 的读取使用 manifest hash；require 只加载模块根下 manifest 已登记的 Lua 文件，未知模块、改动文件或加载异常会失败，不影响主 UI 进程。
- 权限取三层交集：插件 manifest permissions、entry sidecar entry.lua.permissions.json 中的脚本请求及用户本次逐项批准 Grant。文件根求交集时仅保留较窄重叠根；EXE 路径/HTTP host/method/dialog 为精确交集，clipboard/HTTP 明文 bool 使用 AND；缺少 sidecar/批准则为空 Grant。相对 processExecutables 路径拒绝越界，完全限定绝对路径只有在同样被脚本请求和用户本次批准后才有效。host 每条 IPC 仍复核有效 Grant，unknown IPC fail-closed。
- Catalog schemaVersion=1，原子写入并保留未来版本/损坏文件；安装/升级不覆盖现有不同内容，同 ID/版本目录已存在而 hash 不符时失败。禁用项不可运行，卸载仅允许已禁用项；用户数据与包文件分离。
- M12-10 不增加 Lua 公共函数，因此 API registry 保持 1.2 / 103，FUNCTION-REFERENCE 和 IDE 补全来源不变。插件动态贡献 API、命令面板集成与 M12-11 小精灵 sidecar 接入不在此子任务范围。
- 验证分为 Core 包/哈希/ZIP/权限交集/生命周期自动测试、Windows CI Worker smoke；真实 WinForms/DPI/权限交互继续属于 M14，不以 CI 代替。

- M12-10 自动化边界：不增加公开 Lua API，因此 registry 为 1.2 / 103、函数手册与 IDE 补全保持同一来源；管理器不提供第三方 API 动态注册或内置命令面板集成。SHA-256 非数字签名，Worker 非 OS 沙箱，不作为任意恶意代码隔离承诺。

- Windows Actions run [43433992383](https://github.com/xiaobei206/anjian/actions/runs/43433992383) 对代码 HEAD 582db78d9bddbed820727dd1bf01b19847beb5f6 全 PASS：Restore；Release Build 0 warnings / 0 errors；Core tests 171 passed / 0 failed / 0 skipped；API handbook check；self-contained win-x64 publish；published plugin Worker 与 Sprite Worker smoke；ZIP/SHA256 和 artifact upload 全 PASS。Artifact ID 14042985442，87,263,152 bytes，archive SHA-256 ebe8ca9bc7c4f82723e86628da5755bd783d655a9c1a170c7112336919e95af1，有效至 2026-12-13；这是 Actions 临时 artifact，不是 GitHub Release。


## ADR-031：小精灵按入口绑定权限请求 sidecar

- 状态：ACCEPTED（M12-11 规格冻结；实施与自动化结果见路线图和 handoff；真实桌面审批仍留 M14）。
- 入口 sidecar 使用现有脚本权限 schema v1，源文件名为 `<entry.lua>.permissions.json`。包内通过新增可空字段 `SpritePackageEntry.permissionManifestPath` 绑定到该入口，规范路径必须精确等于 `scriptPath + ".permissions.json"`，不可交叉引用其他入口。
- Sidecar 作为 payload 纳入 `Anjian.Sprite.json.files`，核对大小/SHA-256 与 `SHA256SUMS.txt`。构建时验证 UTF-8/schema/大小与 reparse point；包校验和每次入口启动都复核文件、闭合清单、哈希与 schema。
- 入口没有 sidecar 时合法但权限为空；有 sidecar 时其内容只建立 request。仅有用户本次逐项批准后，现有 `ScriptPermissionPolicy.CreateGrant` 才能产生非空 Grant。请求清单、manifest/hash 都不能代替用户同意；拒绝时不启动 Worker，审批不持久化/跨入口共享。
- Worker bridge 和 host IPC 维持双层检查，不修改 Lua API registry。manifest 已绑定的 sidecar 若损坏、丢失、hash 或绑定不一致均 fail-closed；manifest 未绑定 sidecar 的入口合法，但只能使用空 Grant。
- Core 测试覆盖多入口绑定、清单闭合/篡改；发布包 smoke 要验证 sidecar 请求不会在缺少显式 Grant 时自动生效。SHA-256 不是数字签名；CI smoke 不代替 M14 Windows 10/11 审批窗体实机验收。

## ADR-032：插件 Lua API 以受限纯函数贡献并合入 registry extension view

- 状态：ACCEPTED（M12-12 的代码实现与 Windows CI 状态以最新 handoff/路线图为准；真实桌面验收留 M14）。
- 插件可在 Anjian.Plugin.json 的可选 contributions 节点中声明稳定 ID、公开 Lua 名称、签名、参数/返回/错误/暂停/取消契约、示例，以及指向 hash-pinned modulesRoot Lua 模块的导出。贡献 schema 固定为 schemaVersion=1；未知字段、未来 schema、缺失字段、重复 ID 和非法类型均拒绝。
- 第一阶段仅支持纯 Lua helper，不允许新增宿主 IPC 或权限请求。模块调用时惰性加载到独立受限环境，仅开放安全基础函数和 math/string/table/utf8 标准库副本；不能访问 MoveTo、文件、剪贴板、对话框、进程、HTTP、窗口、图像或 OCR 宿主 API。helper 与调用脚本共用 Worker 的指令检查点，Stop 继续使用现有 Worker 终止边界。
- LuaApiRegistry.WithPluginContributions 形成运行期内存 registry extension view；静态内置 registry 不被插件改写，API 版本保持 1.2 / 103 个内置函数。IDE 补全/签名、命令面板、调用计划与可选本地动态参考文档均使用同一合并视图；静态 FUNCTION-REFERENCE-CN.md 仍代表内置 registry。
- 公开名称和内置 API/Lua 标准名/宿主全局名/其他启用插件函数名按大小写不敏感冲突检查。启用前验证完整启用集合，冲突时不保存 catalog。仅已安装、启用、兼容且文件闭合清单与 SHA-256 验证通过的插件可以贡献 API；每次普通脚本 Worker 启动时重验 catalog、manifest 与模块内容 hash。
- 参数只允许必填 string/number/integer/boolean；返回值只允许 string/number/integer/boolean/table/nil；参数数量与类型、返回类型在调用时检查。函数必须声明文档、示例、错误和执行语义；纯 Lua helper 的副作用声明必须为空。
- 安全边界：不加载任意 DLL/native code、不自动联网、不在主 UI 执行第三方 Lua、不通过贡献路径扩展脚本 Grant。SHA-256 不是数字签名，Worker 也不是 OS 级沙箱；插件 Lua 异常只中止当前脚本 Worker 执行。
- 验收覆盖 schema/未知与未来字段、内置和插件间冲突、启用失败原子性、停用/损坏/hash 篡改、实际 NLua 调用、类型/arity、隔离、IDE/API registry 一致性、Windows build/test/package smoke。真实 Windows 10/11 管理 UI、命令面板、DPI、多屏与完整用户交互留 M14。



## ADR-033：多实例运行实例身份、生命周期与资源隔离契约

- 状态：ACCEPTED（M13-01 规格及 M13-02 Core/App 运行时协调、Worker/IPC 身份和全局输入仲裁已实现并经 Windows CI；完整实例 UI 留 M13-04；真实桌面验收留 M14）。
- 身份分层：
  - `instanceId` 是一次用户明确启动意图的稳定身份，使用小写 32 位 GUID-N；Core 类型为 `RunInstanceId`。从创建到清理完成保持不变。
  - `startRequestId` 是 UI/调用方的幂等请求标识。同一 Start 手势的重复派发或网络/消息重试必须返回同一实例，不得二次启动；用户再次明确点击 Start 则创建新请求和新实例。不能仅按脚本路径去重。
  - `workerAttemptId` 标识一个具体 Worker 尝试。单个逻辑实例的每轮播放仍保持同一个 `instanceId`，每次新建 Worker 使用新的 attempt ID、随机命名管道和会话 nonce。
- 生命周期模型由 `Anjian.Core.Runtime.RunInstanceLifecycle` 机器可读维护。状态为 Created、Validating、Queued、Starting、Running、Paused、StopRequested、TimeoutRequested、Cancelling、ForceTerminating、CleaningUp、Cleaned；结果单独记录为 None/Succeeded/Failed/Cancelled/TimedOut；终止模式及清理结果也单独记录。Cleaned 是唯一终态。这样清理失败不会覆盖脚本本身的 Succeeded/Failed/Cancelled/TimedOut 结果。
- 合法转换图和不变量以 Core 的 `AllowedTransitions`、`CanTransition`、`Transition` 为唯一契约。StopRequested 首次锁定 Cancelled，TimeoutRequested 首次锁定 TimedOut；首个终止原因不可被后来的 Stop/timeout/Worker 退出覆盖。开始 CleaningUp 前必须确定结果；只有清理处于 InProgress 才能进入 Cleaned，并报告清理 Succeeded/Failed。非法边、跳过清理或覆盖结果均拒绝。`Transition` 是不可变快照上的纯转换校验，不是并发协调器；M13-02 必须按实例串行提交状态/终止原因（事件循环或锁/CAS），确保 Stop 与 timeout 的 winner 唯一。正常自然完成保持 TerminationMode=None；仅经 Cancelling 才是 Graceful，仅经 ForceTerminating 才是 Forced。
- 启动与并发边界：每个逻辑实例独立校验、独立审阅权限后才进入 FIFO 队列。最多 4 个占用 Worker 槽位的实例（Starting/Running/Paused/正在终止或清理的实例均占槽）；最多另排队 16 个；超限立即返回明确容量错误，不建立无界后台 Task。排队中可取消；槽位只有在实例进入 Cleaned、完成输入和句柄清理后才释放。契约常量冻结 Worker 启动连接 watchdog 为 30 秒、Stop 宽限期为 2 秒，届满仍未退出才强制终止；M13-02 已实际执行连接/握手 watchdog、宽限和强制杀进程树。启动 watchdog 记为 TimedOut；用户可配置运行超时属于 M13-03。数值由 `RunInstanceLimits` 提供。
- 同脚本多实例允许：不同 Start 意图各自创建实例，可同时排队/运行；一次 Start 的重试必须幂等。每个实例捕获独立且不可变的启动快照：规范化脚本路径/内容版本、类型化参数、播放选项、权限 Request 与本次 Grant、插件贡献计划、循环计数、日志上下文及创建时间。一个实例的参数修改不得改变已经启动的其他实例。
- 实例私有资源不得共享：Lua state/Worker/管道会话、Worker attempt、ExecutionGate/Pause 信号、CancellationTokenSource、运行/超时计时器、ScriptProcessSession 随机进程句柄字典、HTTP session、窗口引用、图像缓存、插件计划快照、调试命令等待源、键鼠按下资源与清理责任。Worker 退出、Stop、timeout 或异常只影响所属实例；兄弟实例继续运行。Worker/管道的每个请求必须由宿主绑定到创建它的实例，包含 instanceId + workerAttemptId + nonce 并与服务端 pipe/session 对照；不信任 Worker 自报身份或 Grant。任何未知/不匹配 IPC fail-closed。
- 权限不跨实例共享：每个实例启动时重新读取其脚本/小精灵入口的 sidecar，绑定规范化脚本路径、请求内容 hash、instanceId，并逐次展示本次审批；缺失 sidecar 得到 Empty Grant，取消/拒绝不启动。Grant 不持久化、不缓存给同路径其他实例，也不能由 Worker 在 IPC 中提交或升级。插件模式继续采用脚本请求 ∩ 插件 manifest ceiling ∩ 用户本次批准；host 每条 IPC 以实例端持有的权威不可变 Grant 再检查。随机进程句柄仅存在该实例的 ScriptProcessSession 中；其他实例即使猜中字符串也查不到。
- 前台键鼠输入是整个 Windows 桌面共享资源，不可能依靠每个 Worker 的 Lua state 隔离。M13-02 已实现主进程单一全局 InputArbiter：FIFO、单写者。Move/Wheel/Click/KeyPress/TypeText 等完整原子事件持有一次租约，禁止不同实例的 SendInput 序列交错；显式 KeyDown/MouseDown 的租约持续到对应 Up 全部释放。持有者暂停时仍保留租约，其他实例的输入请求等待且可取消；持有者的 Up、Stop/timeout 清理可绕过暂停门。所属实例的全部 held keys/buttons 发出 Up 并确认清理后才归还租约。若释放失败，标记全局输入仲裁器故障并阻止后续输入租约，直至恢复；不能让另一实例误释放别人的按键或在未知按键状态下继续注入。纯 UIA 语义操作不占 SendInput 租约，仍按各自 Worker 和 HWND 引用校验。
- 计时语义：启动/验证/用户权限审阅及 FIFO 排队时间不计入运行超时；Worker 从 Starting 到 Running 有独立 30 秒启动 watchdog。用户配置的实例运行超时从第一次进入 Running 起算，Running 与 Paused 均计入墙钟时长（避免长期暂停永久占用实例/输入资源），循环间隔计入；超时与 Stop 竞争时以先成功提交的状态转换为准。具体可配置值/范围与 M13-03 定时语义一并确定。
- 进程/应用生命周期：关闭主窗口但仅隐藏到托盘不停止实例；用户明确退出应用时先拒绝新 Start，再对所有实例并发请求停止，等待各 Worker 最多 2 秒后强制终止剩余 Worker，随后逐实例完成输入释放、会话句柄/窗口/图像/管道清理。当前 StartProcess 启动的外部应用不因脚本 Stop 默认被杀；仅清除该实例对进程的查询句柄。Worker 意外退出视为 Failed（除非更早的 Stop/timeout 已锁定结果），触发本实例清理，不取消兄弟实例。宿主崩溃/断电下无法保证用户态 SendInput 的 Up 一定发出，必须记录为 OS 进程异常退出边界，不宣称完整防护。
- 可观测性：每条日志/状态事件带 instanceId、workerAttemptId（若存在）、脚本路径、循环序号、UTC 时间、级别、事件码和消息；UI 聚合时仍可按 instanceId 筛选。日志不写敏感参数值、权限 sidecar 正文、HTTP header/body。清理结果、强制终止、Worker exit code、输入仲裁器故障必须单独记录。
- M13-01 只冻结设计和状态转换规则；M13-02 已将协调器/Worker/IPC 与输入仲裁实现。M13-03 实现定时与用户配置的超时/失败策略；M13-04 完成实例列表、日志筛选、恢复 UI 与回归。Windows 10/11 并发输入、停止释放、DPI、多屏和退出路径仍需 M14 实机验收。


### ADR-033 的 M13-02 实现补记（2026-11-14）

- Core RunInstanceCoordinator + App RunInstanceRuntimeCoordinator 实际执行 4 active / 16 FIFO queue；相同 Start request ID 幂等，重用到不同脚本路径拒绝；同脚本不同 Start 得到独立 instanceId。队列项可取消，Cleaned 后最早队列项晋升。
- 实例启动快照包含复制的 PlaybackOptions、JsonElement 参数、只读权限 Grant、plugin runtime options 与普通脚本源码；每实例独立 PlaybackRunner、ProcessSession/HTTP session/窗口/图像/调试状态/Worker。
- Worker pipe 经 current-user ACL、OS 客户端 PID、instanceId/attemptId/nonce 三重绑定；Worker 无权上传/覆盖 Grant，host 每条受保护 IPC 仍按实例端权威 Grant 检查。
- 全局 InputArbiter 已实现公平 FIFO 租约、owner re-entry 用于 Up/清理、等待取消、故障锁定与人工恢复；自动化测试覆盖租约交接、故障拒绝与恢复。
- MainForm 提供基本 selected instance 控制和 StopAll 退出；完整实例列表/筛选/日志视图留 M13-04。定时触发、运行超时用户配置和失败重试留 M13-03。

### ADR-033 M13-02 最终验收补记（2026-11-14）

- Windows Actions [43461688129](https://github.com/xiaobei206/anjian/actions/runs/43461688129) 对代码 HEAD d6854f872a36263f6e3658eb9cf2b7e9f252c947 SUCCESS：Release Build 0 warnings / 0 errors、202/202 Core tests、API handbook 103 项、自包含 win-x64 publish、Sprite/Plugin/MultiInstance smoke 3/3、ZIP/SHA256 和 artifact upload 全 PASS。
- Artifact ID 14056846918，87,318,097 bytes，SHA-256 393bd08c9da97368cb9ddb39c0de790000befa9ecad27ce161accaf8892987c8；这是临时 Actions artifact，不是正式 Release。
- Worker crash-in-flight-IPC 故障已修复：宿主并行观察 process exit 与当前 dispatch；Worker 提前退出会取消 host token，阻止向断开的 pipe 写响应，并将失败限制在所属实例。停止/崩溃清理仍受 OS 进程崩溃边界约束。

## ADR-034：定时任务的时间、重复投递与恢复语义（M13-03）

- 状态：ACCEPTED（M13-03 Core/App 实现；Windows Actions run 43470648199 对最终代码 HEAD 69febaab SUCCESS；Win10/11 桌面实测留 M14）。
- 任务触发统一以 UTC instant 持久化；每日/每周只保存本地 TimeZoneId、TimeOnly 和 weekday 集合。时区解析必须成功，禁止静默回退到系统本地时区。
- DST gap（本地时间不存在）向前移动到首个有效本地分钟；DST overlap（本地时间出现两次）固定选择较大 UTC offset，即较早的 UTC instant，同一日不在第二个重复小时再次触发。
- Scheduler 以 1 秒轮询；超过 2 分钟正常容差视为 misfire。Skip 丢弃；RunOnce 只在 MisfireGracePeriod 内补最近一次。一次 sleep/restart 跨过多个周期也只处理最近一次，不追赶执行全部历史周期。
- 任务游标在将 occurrence 交给执行层前原子持久化，重启后不重复处理已标记 occurrence。该策略明确是 at-most-once，不宣称 exactly-once；persist 与实际 coordinator admission 之间崩溃可能丢一次，不允许为了补偿而盲目重复桌面操作。
- Overlap：Skip 不排队；QueueOne 至多留一个，多个后续 occurrence 合并成一个 pending；Parallel 请求新实例但必须经过 M13-02 全局容量/队列 admission。全局容量竞争时最多暂存一个触发并等待下一次容量检查。
- 禁用/删除任务清除尚未 admission 的 pending 并阻止尚未开始的 retry；已经进入 RunInstanceCoordinator 的运行实例不被回溯取消。编辑任务只影响未来 occurrence；正在执行/已投递的 occurrence 保留 immutable task snapshot。
- occurrence key = taskId + scheduled UTC ticks；attempt 的 startRequestId 使用 taskId + occurrence key + attempt number 稳定哈希。重试一律创建新的逻辑实例和 Worker attempt，并再次审阅脚本权限。
- RuntimeTimeout 允许 1 秒至 7 天，从每个 RunInstance 第一次 WorkerReady/Running 起算；审阅、Starting 和 FIFO queue 不计入；Pause 不暂停计时。Stop 与 timeout 由 coordinator 同步锁内的第一个成功状态转换胜出，二者均复用相同 Worker/输入清理链。
- Failure Stop 在一次失败后禁用任务；Continue 保留下一计划；Retry 限次并使用 capped exponential backoff；连续失败上限触发禁用。用户取消参数/拒绝权限不当作脚本运行失败；清理失败不再继续自动任务。


### ADR-034 M13-03 验收记录（2026-11-14）

- Windows Actions [43470648199](https://github.com/xiaobei206/anjian/actions/runs/43470648199) 对最终代码 HEAD 69febaab37857fbe9186157a4725d52c193ce539 SUCCESS：218/218 Core tests、Release Build 0 warnings / 0 errors、API 文档一致 103/103、自包含 win-x64 发布、package smoke 3/3、ZIP/SHA256 与 artifact upload 全通过。
- 该结果验证 Core 自动化与发布包子进程 smoke，不代表 Windows 10/11 桌面上的 WinForms、权限弹窗、实际键鼠、DPI/多屏或托盘行为已验收；全部仍为 M14 NOT RUN。

## ADR-035：运行实例日志采用有界内存与显式安全导出（M13-04）

- 状态：ACCEPTED
- 决策：运行实例日志先经过敏感值/控制字符/长度处理，再写入最多 10,000 条的进程内缓存；UI 查询有界，导出由用户明确触发，当前只导出符合筛选条件的 JSONL/JSON。实例列表始终显示所有非终态实例并限量保留最近已结束历史。
- 原因：保持日志可观察性而不额外收集大体积网络正文、权限声明或敏感参数；限制长期运行的内存增长，并避免历史实例无限膨胀 UI。
- 故障恢复边界：UI 不覆写 coordinator 终态。人工确认只解除 InputArbiter 故障锁定；若实例 `CleanupStatus=Failed`，历史诊断仍为失败，不得以恢复输入锁定冒充资源清理成功。
- 影响：M13-04 增加实例列表/日志/恢复 UI 和查询/脱敏自动化。当前不提供磁盘日志持久化、轮转、Lua 行号关联；这些仍以功能矩阵和验收清单为准。Windows 10/11 桌面实测留 M14。
