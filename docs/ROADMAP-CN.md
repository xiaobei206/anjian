# 按键精灵 2026：完整产品路线图

- 当前已发布：v0.1.0-preview.4
- 产品完整目标：docs/PRD-CN.md
- 逐项状态：docs/FEATURE-MATRIX-CN.md
- 函数定义及参数参考：docs/FUNCTION-REFERENCE-CN.md
- 规则：路线图决定先后顺序，不代表可以删除完整产品目标中的任何必需能力。

## 当前状态

| 阶段 | 状态 | 交付目标 |
|---|---|---|
| M0 完整产品范围冻结 | DONE（文档已提交） | 完整 PRD、功能矩阵、函数手册初版、后续开发阶段与验收门槛统一 |
| M1 preview.4 实机基线与可靠性 | IN_PROGRESS | 实际 Windows 10/11 验收窗口/托盘/热键/Hook，进行长时运行、多显示器和 DPI 验证 |
| M2 单一 API 注册表与文档生成 | DONE（Windows CI PASS，2026-11-13） | 建立机器可读 API 注册描述；以同一来源生成手册和后续 IDE 提示 |
| M3 完整 Lua IDE 基础 | DONE（Windows CI PASS，2026-11-13；桌面手工验收待执行） | 高亮、行号、补全、参数提示、诊断、查找/替换、多标签、错误行导航 |
| M4 调试器 | DONE（基础能力 Windows CI PASS；桌面实机验收 NOT RUN） | 文件/行号断点、单步、运行到光标、变量快照、调用栈 |
| M5 可视化录制/动作列表 | DONE（Windows CI PASS；桌面手工验收 NOT RUN） | 结构化原始事件/动作、虚拟化动作编辑器、参数/延时/分组/注释/启用状态、Lua 区块同步保护、sidecar 和单步试运行 |
| M6 坐标系统与窗口自动化 | DONE（按本 handoff 的 API/Win32 范围；最终 Windows CI PASS；桌面验收 NOT RUN） | screen/window/client/anchor/ratio、双向坐标转换、安全窗口引用/枚举/属性/操作/等待 |
| M7 控件与前后台能力适配 | TODO | UI Automation/标准控件能力探测、后台输入适配、失败回退和边界说明 |
| M8 截图、图色与模板资源 | TODO | 截图区域、找色、多点找色、找图、阈值/多结果和资源管理 |
| M9 OCR 与文本识别 | TODO | 本地 OCR、语言/字库配置、文字区域/置信度与等待文本 |
| M10 参数、配置与脚本自定义 UI | TODO | 参数声明、类型/默认值/范围/选项、校验、持久化和通用表单 |
| M11 小精灵打包器 | TODO | 独立包/EXE 入口、独立参数界面、图标、依赖收集、运行控制、清单与校验 |
| M12 内置扩展 API 与插件系统 | TODO | 文件、剪贴板、对话框、进程、HTTP/JSON，插件 manifest/权限/API 版本 |
| M13 多实例与定时任务 | TODO | 独立运行实例、任务状态、定时、超时、失败策略与隔离 |
| M14 1.0 稳定化与发布 | TODO | 自动与手工验收、依赖许可、离线帮助、性能/长时测试和正式稳定版发布 |

## M1 — preview.4 实机基线与可靠性

当前 release 为 v0.1.0-preview.4，Windows CI 的 build、7 项自动测试、自包含发布、ZIP/SHA256 和 prerelease 创建通过，但桌面实机手工验收尚未完成。

- [ ] 默认启动仅显示独立悬浮窗与系统托盘，主窗口无任务栏按钮。
- [ ] 主窗口打开、关闭、返回悬浮窗以及托盘恢复/隐藏/退出行为正确。
- [ ] 悬浮窗与主窗口的循环次数/轮间隔双向同步且重启持久化。
- [ ] EXE、主窗、托盘图标一致，16/32px 仍清楚。
- [ ] F10/F11/F12 只在适用状态截获，运行/停止和输入释放可靠。
- [ ] Hook 录制不漏关键输入、不重复录入注入事件；异常可诊断。
- [ ] Windows 10/11、100/125/150/200% DPI、多显示器、负坐标测试。
- [ ] 2 小时稳定性与 100 次运行/停止测试；记录完整环境和日志。
- [ ] 若发现缺陷，先修复并提高版本号，不发布名为 FIX 的版本。

M1 的实机验证不要求用户提供未经要求的敏感数据。若实机无法同步完成后续开发也应继续推进文档/API 注册表等不依赖该环境的工作；不得因此把其他功能无限期搁置。

## M2 — API 注册表与函数手册生成（DONE，Windows CI 已通过）

目标是让“函数说明书”长期准确，而不是手工维护多份互相冲突的列表。

- [x] 选择适合当前 .NET 10 / WinForms 项目的机器可读定义格式。
- [x] 为每个当前 API 注册稳定 ID、签名、参数类型/默认值/范围、返回契约、错误、示例、坐标/取消语义、状态和最低 API 版本。
- [x] 通过注册表校验实际 Lua 绑定是否缺函数、重名、参数不一致或说明遗漏。
- [x] 从注册表生成 docs/FUNCTION-REFERENCE-CN.md 或建立可验证的生成区块，禁止手工生成文件悄悄偏离注册表。
- [x] 同一份定义为未来 IDE 补全、签名帮助、命令面板和运行前参数校验提供数据。
- [x] 为当前实际实现的 API 增加契约测试；计划函数不得被生成成“当前可用”。
- [x] 补上自动化测试，验证生成文档包含所有已公开 API、参数顺序和默认值。
- [x] 不在本阶段一次性重构整个运行器，也不改动已发布 API 的含义。

M2 验收：当前实现中的 22 个公开函数均可在注册表和手册中找到；生成文档可复现；错误参数/默认值、运行时绑定与 Unicode 契约测试通过；Windows CI run `43273917056` 的 Restore/Build/12 项测试/API 文档检查/win-x64 发布与 ZIP/SHA256/Artifact 上传全部成功。编辑器补全、参数提示和命令面板 UI 尚未实现，作为 M3 的实际工作继续推进。

## M3 — 完整 Lua IDE 基础（DONE，Windows CI 已通过）

### 已交付
- [x] WinForms Lua 编辑器具备语法高亮、行号和多标签；脚本标签切换/关闭会保存，重开从脚本文件恢复。
- [x] 查找下一个（含循环查找）、替换当前项、全部替换和大小写选项。
- [x] API 自动补全和参数/签名提示直接读取 `LuaApiRegistry.Load()` 的 `Categories` / `Apis`；只提示注册表中标记为 `implemented` 的真实 API，不在 UI 维护第二份函数定义。
- [x] Lua 语法诊断使用 NLua 编译 chunk 的 `LoadString` 路径，不调用脚本；错误写入运行日志并导航到首个错误行/列。
- [x] 自动化测试覆盖 token 分类、真实 API 补全边界、函数/表构造器嵌套参数提示、只编译不执行、语法错误定位、注释/字符串上下文以及大文本词法处理。
- [x] 语法着色词法扫描移至后台任务并使用文本版本号丢弃过期结果，避免大文本扫描阻塞 UI；录制期间防止切换或关闭活动脚本标签。

### Windows CI 验证
- 最终代码验收基线：`71fd493cad0fbed1451a4ffa46192752a18bd0a6`；Actions run `43282795272`：SUCCESS。
- Restore PASS；Release Build PASS（0 warnings / 0 errors）；自动化测试 **20 passed / 0 failed / 0 skipped**；API handbook `--check` PASS；self-contained win-x64 publish PASS；ZIP/SHA256 package PASS；artifact upload PASS。
- 前期测试曾因签名提示测试夹具把光标前的字符串逗号误算为外层参数，出现 1 项断言失败；修正夹具后又补上表构造器 `{}`、`[]` / `()` 嵌套逗号处理与回归测试，最终主线运行全部通过。UI 集成相关 Build 均通过。
- 本执行环境未安装 .NET SDK，故本地 Restore/Build/Test 为 NOT RUN；上述结果仅来自 Windows GitHub Actions runner。
- 本轮没有创建 release，已发布的 v0.1.0-preview.4 资产没有更新；新 IDE 功能目前只在 main 未发布代码中。

### 尚未完成与验收边界
- Windows 桌面 UI 手工验收：NOT RUN；尤其是 WinForms 窗口中的多标签交互、中文/日文输入、1MB 文件输入/滚动、100–200% DPI 和错误导航仍需实机确认。
- 代码折叠不在本轮 handoff 的 M3 强制范围，F04 仍为“部分实现”，后续需按产品完整目标补齐。
- 独立 API 命令面板未实现；当前有编辑器自动补全与签名提示，不能把它们描述成命令面板。
- 断点、单步、变量窗和调用栈属于 M4，未实现。

### 下一步
M3/M4/M5 自动化代码与 Windows CI 均已完成；M1/M3/M4/M5 的 Windows 桌面实机验收仍各自独立保留为 NOT RUN。

## M4 — Lua 调试器基础（DONE：Windows CI 已通过；桌面手工验收 NOT RUN）

### 已接入的基础能力
- [x] 基于 NLua/KeraLua 真实 Lua line hook，而不是模拟日志或静态行号展示；脚本首个执行位置和断点行会在执行前暂停。
- [x] 文件/行号断点，编辑器 gutter 圆点标记和当前执行行标记。
- [x] 继续、单步进入、单步越过、单步跳出和运行到光标；越过/跳出按真实调用栈深度判断。
- [x] worker 内读取当前执行位置、调用栈、局部变量及可读全局变量；JSON 快照复制到主进程/UI。
- [x] 调试状态与 Lua state 留在 worker 子进程，UI 不访问 NLua/KeraLua 对象；普通播放仍使用原有 count hook / IPC 路径。
- [x] 变量按类型展示；字符串上限、局部变量/全局变量/栈帧数量设上限；表/函数/线程/userdata 当前不展开，不执行任意 watch 表达式。
- [x] Core 自动测试覆盖断点行、嵌套调用单步顺序、运行到光标、变量快照、表不可展开和 hook dispose。

### 当前限制
- 断点只存在当前打开标签的内存模型中，关闭标签/退出后不保存。
- 当前尚未提供命名 watch 表达式、表/对象展开、复杂对象属性求值或跨文件持久断点。
- 调试固定执行一轮；不沿用普通播放的循环次数/轮间隔。
- 基础调试器代码、Core 自动测试和 Windows CI 已通过；这代表 M4 基础代码交付 DONE，不代表调试器完整产品功能全部完成。Windows 桌面手工验收仍须独立执行，不能用 CI 代替。

### Windows CI 验证（基础调试器交付 DONE）
- 最终代码/文档验收基线：main commit ab90b96f59deb173874c8aba819a9fc95288f8cf；Windows Actions run 43291175992：SUCCESS（<https://github.com/xiaobei206/anjian/actions/runs/43291175992>）。
- Restore：PASS。
- Release Build：PASS，0 warnings / 0 errors。
- Core 自动化测试：25 passed / 0 failed / 0 skipped。
- Anjian.ApiDocsGenerator --check：PASS。
- self-contained win-x64 publish：PASS。
- ZIP/SHA256 package：PASS；SHA256 为 1d37a3210e22646fd781a2803a17f40058d97f5c1e16845cef7d5d24059af8c4。
- Actions Artifact upload：PASS；Artifact ID 13976519155，最终 ZIP 51,794,098 bytes。该次 commit 非 release 提交，Release version / Create GitHub Release 按条件跳过；未创建新版本，v0.1.0-preview.4 发布资产不变。
- 更早的测试失败已如实修正：LuaFunction 类型冲突、xUnit2031 analyzer，以及缺少后续断点的单步测试夹具均已通过后续 Windows run 验证。
- 本地容器无 .NET SDK，Restore/Build/Test：NOT RUN；上述结果来自 Windows GitHub Actions。
- Windows M1/M3/M4 桌面实机手工验收：NOT RUN。CI 不等同于 Windows 10/11 桌面验收。

### 验收门槛
- 自动：Windows Restore/Build、所有 Core 自动测试、API 手册一致性检查、self-contained win-x64 publish、ZIP/SHA256 package、artifact upload 全部通过。
- 手工：参照 docs/ACCEPTANCE-TEST-CN.md 的 M4-01 至 M4-10 执行并记录环境、操作、结果和日志。
- 断点/单步/运行到光标必须确实控制 Lua 执行点；状态和变量视图不得访问 UI 线程外的 Lua state。

## M5 — 可视化录制/动作列表（实现与 Windows CI 已完成；桌面手工验收 NOT RUN）

### 本轮实现范围
- [x] 在 Anjian.Core 增加 RecorderDocument / RecorderRawEvent / RecorderAction / 同步状态模型；保留原始 Hook 事件、单调时间戳、来源、标志位、负坐标、click-only 和队列丢失计数。
- [x] Raw Event -> Action 转换使用 recording 内稳定 ID；仅相邻、同名且时长在范围内的 Down/Up 才折叠成 MouseClick / KeyPress；拖动中插入 Move、重叠按键和不能配对的事件保留 Down/Up 原顺序。
- [x] 按住时长 holdMs 与相邻动作的 delayMs 分开计算；MouseWheel、鼠标 Down/Up、KeyDown/KeyUp 均保留语义；动作保存来源与原始 event ID 引用。
- [x] WinForms 动作表使用 DataGridView VirtualMode；支持插入、删除、复制、上移/下移、启用/禁用、动作类型/参数/延时、分组/注释和批量延时编辑；显示 actionId、来源和原始事件引用。
- [x] generated Lua block 有稳定 ID/schema 标记，每个动作另有可导航的 action ID 注释；采用单向生成，不做任意 Lua 反向解析。
- [x] generated block 外用户代码保留；编辑区块后识别 Detached；区块被删除后识别 Missing；更新时显示旧/新代码预览并要求确认，取消不改脚本。哈希统一 LF/CRLF，避免 Windows 换行变化产生误报。
- [x] 以“脚本名.actions.json”保存 sidecar（例如 main.lua 对应 main.actions.json）；使用临时文件、磁盘 flush、.bak 备份和 schemaVersion 检查。不兼容 schema 不覆盖；脚本重命名/复制/删除会同步管理 sidecar。
- [x] 单步试运行复用现有 PlaybackRunner/Worker 和输入清理路径；执行前有真实输入风险确认，单次合计时间不超过 60 秒，文本最多 1000 字符。
- [x] 巨大动作延时以有作用域 Lua 循环生成，且文档限制单个动作间隔不超过 7 天，避免异常持久化数据放大代码体积。

### 自动化验收覆盖
- [x] Down/Up 配对、Hold 与 Delay 分离、Move 插入打断配对、重叠按键、负坐标、滚轮、稳定 ID、来源/Flags、溢出计数。
- [x] 动作 JSON 保存/加载、指定 sidecar 文件名、未知 schema 原文件保护、超长 delay 拒绝。
- [x] generated block 外代码保留、手改/删除区块确认、取消不改脚本、动作 ID 映射、NLua 语法执行和 CRLF/LF 哈希一致。
- [ ] Windows UI 手工验收：全部 M5 桌面场景仍 NOT RUN，不能由 CI 代替。Windows CI run 43295924472 已通过 37 项自动化测试，但不得据此改变本行状态。

### 完成边界
M5 不代表 Hook 在 Windows 桌面已完成可靠性验收，不代表多坐标空间、窗口绑定或后台输入已实现（属于后续 M6/M7）。M5 也不实现任意 Lua 回读动作表；generated block 由动作模型单向生成。

Windows CI 最终结果：run 43295924472 SUCCESS；37/37 tests、Release Build 0 warnings/0 errors、API handbook check、self-contained win-x64 publish、ZIP/SHA256 和 artifact upload 全部 PASS。Artifact ID 13979855824；内部 ZIP SHA256 `ddc08cfd78503d9e5e5c7995ff1202e3bc36abfcf1de6fa3ae9cc35a5baf531f`。所有 M5 桌面场景仍为 NOT RUN。

## M6 — 坐标系统与窗口自动化（代码范围 DONE；Windows CI PASS；桌面验收 NOT RUN）

### 已交付
- Core 坐标系统提供 screen/window/client/anchor/ratio 五种空间、双向屏幕转换、外框九宫格锚点、客户区归一化坐标、物理像素取整和显示器工作区锚点对齐。外框取 Win32 GetWindowRect，客户区通过 ClientToScreen 转为屏幕物理像素；矩形右/下边界不包含。
- `MoveTo(x, y)` 的既有虚拟桌面绝对物理像素契约完全保留，副屏负坐标合法；M5 RecorderDocument 仍显式使用 `virtual-desktop-physical-pixels`，生成 Lua 仍调用 `MoveTo`，既有 sidecar / 录制内容没有被静默重解释。
- 公开 API 新增 18 项，注册表 API version 0.3，总数 40；包括窗口查找/枚举/子窗口枚举/属性、激活/显示/隐藏/最小化/最大化/还原/置顶、移动/尺寸/工作区锚点对齐、等待出现/关闭和双向坐标转换。原有 22 项 API 的签名和行为不变。
- 窗口引用为本轮运行内随机不透明 GUID，不向 Lua 暴露 HWND。枚举支持标题包含、类名精确、进程名、PID、可见性；顶层与子窗口后代各自最多 512 个结果。WindowGetInfo 返回 `isChild`、前台和键盘焦点标志，以及窗口外框/客户区矩形。
- WindowMove/WindowResize/WindowAlign 对外统一使用虚拟桌面屏幕物理坐标；遇到子窗口时在 Win32 调用边界转换为父窗口客户区坐标，避免 SetWindowPos 对 WS_CHILD 坐标解释不同导致错位。
- WindowWait 命中返回表、超时返回 nil；WindowWaitClosed 关闭返回 true、超时返回 false；Stop 可取消。窗口引用每轮 worker 结束清理。
- EVENT_OBJECT_DESTROY 通过 SetWinEventHook 跟踪；每次使用校验 HWND、PID、线程 ID、类名和进程启动时间。校验中途遇到窗口销毁/进程退出按失效引用处理；销毁 hook 无法安装则拒绝创建引用。不会绕过 UIPI/UAC/安全桌面。
- M6 不包含后台输入、控件读写、UI Automation、图像识别或 OCR；M5 当前仍没有将录制动作绑定到持久窗口选择器并自动转成 window/client/ratio 动作，录制坐标模式仍固定为 screen 物理像素。该剩余产品目标不得误写成已实现。

### 自动化测试与 Windows CI
- `CoordinateSystemTests` 覆盖负屏幕坐标、window/client 不同原点、9 个锚点、ratio 正反向和越界、物理像素取整、工作区对齐及无效矩形；Lua API 契约测试检查新 API 绑定、Lua 表返回和 `isChild` 字段；API 手册由注册表生成并通过 `--check`。
- 首个代码提交 `3479491d3e9f3c16dcab8f78399a5811f73f9fe1` 对应 run `43299445160`：Restore / Release Build PASS（0 warnings / 0 errors），60 passed / 1 failed；唯一失败是测试手册生成区尚未与 40 项 registry 同步。该早期失败保留记录，不作为最终结论。
- 手册同步提交 `8c143982ab49bf87c3461981205fe6d00757562d` 对应 run `43299954437`：61/61 tests PASS；Build 0 warnings / 0 errors；API handbook、win-x64 publish、ZIP/SHA256、artifact upload 全 PASS。
- 子窗口屏幕坐标兼容修复提交 `c7cc5f7b2ee435ec13ece0846cf4ee5e64e03db5` 对应 run `43300453577`：61/61 tests PASS；Build 0 warnings / 0 errors；API handbook、win-x64 publish、ZIP/SHA256、artifact upload 全 PASS。
- HWND 销毁/进程退出竞态修复提交 `4f9b2b2648fb154353ca55522e9c77fe77645506` 对应最终 Windows Actions run `43300620614`：**SUCCESS**。Restore PASS；Release Build PASS（0 warnings / 0 errors）；Core Tests 61 passed / 0 failed / 0 skipped；API handbook --check PASS；self-contained win-x64 publish PASS；ZIP/SHA256 package PASS；artifact upload PASS。Artifact ID `13981497112`，51,857,145 bytes；GitHub Actions 上传 artifact digest `f46eaaf66ee204c82129902e280eed7a80c7c9a4a86c825888729af5280143f3`。
- Read release version / Create GitHub Release 因本次不是 release 提交而跳过；没有创建新 Release。v0.1.0-preview.4 资产未更改。
- 当前容器没有 .NET SDK，本地 Restore/Build/Test：NOT RUN；以上实际构建与 61 项测试由 Windows GitHub Actions runner 执行。

### 尚待实机验收与后续边界
- M6-01 至 M6-10 的 Windows 10/11 桌面实机验收全部 NOT RUN，特别是 100–200% / 混合 DPI、多显示器负坐标、不同完整性级别、真实窗口销毁/句柄复用与子窗口定位。CI PASS 不代表桌面窗口行为已验收。
- M5 录制仍固定保存 `virtual-desktop-physical-pixels`，生成 `MoveTo`；窗口相对录制/回放绑定（持久窗口选择器 + 每次执行重定位）仍是未实现的 PRD 目标。不得把 M6 坐标转换 API 描述为已经完成该录制器能力。
- 不支持后台输入与控件读写；按 ROADMAP 进入 M7 前仍需按权限边界设计能力探测，不绕过 UIPI/UAC/安全桌面。
- 本阶段未更新 VERSION / preview 版本，未创建 Release。


## M3–M7 — IDE、调试、录制、坐标与窗口

这些阶段必须形成可用闭环，而不是只有 API 名称：
- 编辑器提示要从 API 注册表读取，并可导航到函数手册。
- 调试器要真实控制执行点并观察变量，不以“显示日志”冒充单步调试。
- 动作列表要能编辑动作字段并生成正确 Lua，代码不同步时绝不静默覆盖。
- 坐标系统要支持窗口和客户区基准、锚点、归一化坐标以及多显示器/DPI。
- 窗口 API 要有等待、关闭后失效、错误处理、对齐和尺寸调整测试。
- 前台/后台适配需报告被验证支持的能力；不承诺任意第三方窗口都可后台操作。

## M8–M10 — 图像、OCR、参数和用户界面

- 截图与视觉函数统一坐标空间和资源生命周期。
- 找色/找图必须有阈值、超时、区域、多结果和明确的未命中返回契约。
- OCR 以本地/离线能力为优先，记录模型/字库许可和包体积。
- 参数定义具有稳定 schema；默认值、范围、选项和验证规则在运行前一致执行。
- 自定义 UI 的输入必须安全地映射成 Lua 可读的参数表，并可在开发环境和小精灵中复用。

## M11 — 小精灵独立打包

- 收集入口脚本、依赖模块、资源、模板、配置与运行时要求。
- 可配置应用名、版本、图标、关于信息、参数表单、运行选项和单实例行为。
- 输出独立运行入口/包、依赖清单、SHA256、构建报告和对应版本的离线说明。
- 在干净 Windows 10/11 x64 环境验证，不要求用户安装 IDE 或手工配置 Lua。
- 配置和包升级有 schema 迁移/备份策略。

## M12–M13 — 扩展 API、插件和自动运行

- 增加有边界的文件/目录、剪贴板、对话框、进程、HTTP(S)/JSON 等常用 API。
- 插件 manifest、权限提示、版本兼容、启停/卸载、错误隔离和文档注册。
- 多实例拥有独立 Lua state、参数、日志和输入清理资源。
- 定时任务有时区/触发时刻定义、超时、失败策略和可取消行为。
- 所有涉及网络和进程的功能不冻结 UI，且默认不自动联网。

## M14 — 1.0.0 稳定版定义

只有以下全部达标才可称为完整产品 1.0.0：
1. FEATURE-MATRIX-CN.md 中所有标记为 1.0 必须的项目逐项通过验收。
2. 完整 IDE、动作列表、坐标/窗口、图色/OCR、参数/用户 UI、小精灵打包和插件能力均形成端到端用户流程。
3. 函数手册、编辑器提示、命令面板和运行时绑定来自同一 API 注册表并通过一致性测试。
4. Windows 10/11 干净环境、2 小时稳定性、多实例、停止释放、DPI/多屏验证通过。
5. 发行包包含用户手册、函数手册、打包/插件说明、依赖许可证、测试报告和 SHA256。
6. 未通过事项必须公开标记，不得把 CI 通过当成所有桌面能力实测成功。

## 发布与版本规则

- 采用递增版本，不发布 FIX 版本名。
- 每次发布文件名带产品名与版本；release、变更日志、构建报告和 SHA256 必须对应同一版本。
- PRD / 功能矩阵定义完整范围，ROADMAP 定义实施顺序，handoff 定义下一会话唯一任务。
- 优先直接在 main 小步提交并保持易回滚；写入前核对最新 HEAD。
