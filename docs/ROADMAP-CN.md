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
| M1 preview.4 实机基线与可靠性 | DONE（代码/CI；Windows 桌面验收延期至 M14 后集中执行） | preview.4 基础窗口/托盘/热键/Hook 代码已交付；实机长时运行、多显示器与 DPI 仍 NOT RUN，统一延后验收 |
| M2 单一 API 注册表与文档生成 | DONE（Windows CI PASS，2026-11-13） | 建立机器可读 API 注册描述；以同一来源生成手册和后续 IDE 提示 |
| M3 完整 Lua IDE 基础 | DONE（Windows CI PASS，2026-11-13；桌面手工验收待执行） | 高亮、行号、补全、参数提示、诊断、查找/替换、多标签、错误行导航 |
| M4 调试器 | DONE（基础能力 Windows CI PASS；桌面实机验收 NOT RUN） | 文件/行号断点、单步、运行到光标、变量快照、调用栈 |
| M5 可视化录制/动作列表 | DONE（Windows CI PASS；桌面手工验收 NOT RUN） | 结构化原始事件/动作、虚拟化动作编辑器、参数/延时/分组/注释/启用状态、Lua 区块同步保护、sidecar 和单步试运行 |
| M6 坐标系统与窗口自动化 | DONE（按本 handoff 的 API/Win32 范围；最终 Windows CI PASS；桌面验收 NOT RUN） | screen/window/client/anchor/ratio、双向坐标转换、安全窗口引用/枚举/属性/操作/等待 |
| M7 控件与前后台能力适配 | DONE（Windows CI PASS；桌面验收 NOT RUN） | UI Automation Control View/Pattern；worker 隔离与可停止性；按能力报告语义后台控件操作、受限前台输入和明确 unsupported 边界；run 43350412493：64/64 tests、0 warning/error、API check、win-x64 publish、ZIP/SHA256、artifact upload 均 PASS |
| M8 截图、图色与模板资源 | DONE（实现/Windows CI；桌面验收延期至 M14 后） | 图像资源 UI、manifest/assets、相对路径/依赖、RGB/hex/颜色块、透明模板、视觉调试 overlay 已进入 main 并通过 Windows CI；M8-01..M8-18 仍 NOT RUN，集中验收 |
| M9 OCR 与文本识别 | DONE（实现与 Windows CI PASS；桌面验收延期至 M14 后，NOT RUN） | PaddleOcrNet 2.4.0 CPU 适配器、显式 ch/en/japan 模型管理、严格离线推理、行级结果/置信度/边界框、OCR/FindText/WaitText、暂停/停止与资源释放 |
| M10 参数、配置与脚本自定义 UI | DONE（实现与 Windows 自动化验收完成；WinForms/桌面实测延期 M14） | 参数 sidecar schema v1、10 种类型、统一 validator、配置持久化/恢复、播放/调试前表单、参数设计器、JSON 导入导出、typed Worker IPC、递归只读 Lua Args、脚本目录相对路径解析 |
| M11 小精灵打包器 | DONE（Windows CI run 229 PASS；WinForms/DPI/真实输入桌面验收延期 M14，NOT RUN） | 独立便携目录 + EXE 启动 ZIP、依赖/资源收集、M10 参数 UI、运行控制、manifest/SHA256、构建日志与离线函数参考 Markdown |
| M12 内置扩展 API 与插件系统 | IN_PROGRESS（M12-01 权限基座和播放/调试审阅/Worker 注入已通过 Windows CI；系统 API 和插件待实现） | 默认拒绝权限契约、逐次运行审阅、Worker grant 注入；文件/剪贴板/对话框/进程/HTTP/JSON 的主机双重授权和插件 manifest/加载仍待实现 |
| M13 多实例与定时任务 | TODO | 独立运行实例、任务状态、定时、超时、失败策略与隔离 |
| M14 1.0 稳定化与发布 | TODO | 自动与手工验收、依赖许可、离线帮助、性能/长时测试和正式稳定版发布 |

## 统一 Windows 桌面实机验收策略（用户决定，2026-11-13）

用户明确决定先完成 M9–M14 全部功能开发，整体开发结束后再集中进行 Windows 10/11 实机验收。自本决策起：

- M1–M13 的桌面手工验收不再作为进入下一开发阶段的前置条件；按阶段保留测试清单和状态，未执行一律 NOT RUN。
- M8-01..M8-18 仍全部 NOT RUN；代码/CI 完成不等于桌面通过，M8 不得因延期验收改写为实机 PASS。
- M9 及后续阶段照常实现 Core/API/Windows App、自动化测试、CI、文档和自包含构建；需要真实桌面才能判断的项目统一收集至 M14 后集中执行。
- M14 正式 1.0.0 Release 仍必须等待这轮集中验收。任何 FAIL 需修复并重新构建/测试；未测试项目继续公开标记，不得以 CI 结果替代。
- 最终集中测试需记录 Windows 10/11 OS build、DPI、屏幕拓扑、硬件、步骤、结果和日志；测试范围覆盖 M1–M13 的全部桌面验收行。

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


## M8 — 截图、图色与模板资源（实现/Windows CI DONE；桌面验收集中延期）

### 已交付的 main 核心能力（尚未包含在 preview.4 Release）
- Anjian.Core.Imaging 提供 PixelImage 与独立可测试的 ImageSearchEngine；Windows 端使用 GDI+ CopyFromScreen 捕获当前可见桌面合成画面。没有新增第三方图像识别依赖。
- API registry 从 0.4 经 M8 升至 0.6；公开函数共 78 项（M8 新增截图、像素/颜色、资源管理、模板匹配、RGB/hex、颜色块函数）。Lua worker IPC、运行时包装和中文函数手册同步更新。
- 支持虚拟桌面、可见窗口外框、客户区和 screen/window/client/anchor/ratio 坐标空间的矩形捕获；屏幕图像保留负坐标/屏幕原点元数据。
- 图像资源由每轮脚本运行持有，最多 128 个资源/轮、256 MiB 缓存/轮、单图最多 32,000,000 像素；支持 PNG 保存、加载、裁剪、读取像素、显式释放以及轮次结束清理。
- 支持 RGB 颜色比较/单点找色/多点找色/区域计数、每通道容差、区域与 step；模板找图支持透明通配像素、相似度阈值、灰度模式、0.25–4 倍最近邻缩放、多结果（最多 1000 个）与非重叠结果筛选。
- 支持等待图像/颜色出现或消失、超时、轮询间隔、连续确认次数（1–20）与停止取消。一次模板查询最多 50,000,000 次像素比较。
- 截图来自当前交互桌面可见合成画面。窗口被遮挡时不承诺获得后台真实像素；隐藏/最小化窗口拒绝截图，不绕过 UIPI/UAC/安全桌面，也不承诺硬件加速或受保护表面可读。

### Windows CI 代码验收
- 代码基线 commit 1b87af713ff17c907feac6984bb96aa5dd8e821f，Windows Actions run 43353801969 SUCCESS：Restore PASS；Release Build 0 warnings / 0 errors；73 passed / 0 failed / 0 skipped；API handbook --check PASS；self-contained win-x64 publish、ZIP/SHA256 package 与 artifact upload 全 PASS。Artifact ID 14004658368，76,430,108 bytes。该 artifact 是 CI 临时产物，不是正式 Release。
- 中间迭代曾出现 API 测试结构、生成手册漂移及灰度/缩放接线相关 CI 失败；已修复并由最新成功 run 覆盖验证，失败记录见 DEVELOPMENT-LOG-CN.md。
- 没有更新 VERSION/csproj 预览版本，没有创建 Release；v0.1.0-preview.4 正式 ZIP/SHA256 不变。

### 尚未完成，不能把 M8 标为 DONE
- 图像资源 UI：模板列表、预览、命名/路径与版本校验、资源依赖校验，以及项目资源目录联动尚未完成。
- 视觉调试层（截图区域框、命中位置、相似度/耗时/颜色值展示）尚未完成。
- PRD 提及的 RGB/十六进制辅助转换、颜色差值/颜色块检测尚未形成完整公开 API 闭环；需补充规格、实现与测试后再评估。
- docs/ACCEPTANCE-TEST-CN.md 中 M8 桌面项仍是 NOT RUN。实际 Windows 10/11、DPI/多显示器负坐标、部分遮挡、权限边界、GDI 失败路径、长时等待和资源清理仍须实机测试。CI 不替代实机验收。
- OCR 属于 M9，本阶段不实现 OCR、不引入 OCR 模型/云服务。
## M3–M7 — IDE、调试、录制、坐标与窗口

这些阶段必须形成可用闭环，而不是只有 API 名称：
- 编辑器提示要从 API 注册表读取，并可导航到函数手册。
- 调试器要真实控制执行点并观察变量，不以“显示日志”冒充单步调试。
- 动作列表要能编辑动作字段并生成正确 Lua，代码不同步时绝不静默覆盖。
- 坐标系统要支持窗口和客户区基准、锚点、归一化坐标以及多显示器/DPI。
- 窗口 API 要有等待、关闭后失效、错误处理、对齐和尺寸调整测试。
- 前台/后台适配需报告被验证支持的能力；不承诺任意第三方窗口都可后台操作。

M7 实现/CI 已完成不代表 M7-01..M7-10 Windows 桌面验收通过；这些实机项仍以 ACCEPTANCE-TEST-CN.md 标记的 NOT RUN 为准。

## M8–M10 — 图像、OCR、参数和用户界面

- 截图与视觉函数统一坐标空间和资源生命周期。
- 找色/找图必须有阈值、超时、区域、多结果和明确的未命中返回契约。
- OCR 以本地/离线能力为优先，记录模型/字库许可和包体积。
- 参数定义具有稳定 schema；默认值、范围、选项和验证规则在运行前一致执行。
- 自定义 UI 的输入必须安全地映射成 Lua 可读的参数表，并可在开发环境和小精灵中复用。

## M11 — 小精灵独立打包

- [x] 多入口脚本选择；静态 Lua require("module.name") 递归收集，动态 require(...) 因无法静态保证完整性而在打包前拒绝。
- [x] LoadImage("relative/path") 资源按入口脚本目录收集；图像 manifest / .assets 由既有 ImageResourceCatalog 校验；动态图像加载需要既有 .images.json 清单。
- [x] OCR 仅在用户勾选并且本机缓存模型完整时复制；不在打包期间自动下载；包内离线模型根目录在 Worker 中显式指定。
- [x] 复制实际 self-contained win-x64 发布目录，产出 <产品名>-<版本>-win-x64 文件夹和 -launcher.zip；重命名 apphost 作为启动入口，所有 .dll、hostfxr/coreclr、Content 文件必须保留。
- [x] 产品名称/版本/说明/作者/版权/About/窗口图标；循环次数、轮间隔、启动热键、自动启动、单实例、失败重试、详细日志策略；停止始终释放 Worker 和模拟输入。
- [x] M10 参数 sidecar/schema/validator/运行表单复用；小精灵配置保存到 LocalAppData，不写入包；包 ID/入口 ID 对相同工程路径、产品名和入口相对路径保持稳定，升级时沿用用户配置位置。
- [x] 输出 Anjian.Sprite.json、逐文件 SHA256SUMS.txt、ZIP .sha256、build.log、README-Sprite.txt 和 Content/Help/FUNCTION-REFERENCE-CN.md；启动前校验 hash、文件清单、参数 schema、图像清单、OCR 路径及最低 API/.NET 版本。
- [x] Windows CI 用真实 self-contained 发布目录构建小精灵，并启动改名后的 EXE，由包内 Worker 实际执行 Lua require 和 M10 Args 参数注入。
- [ ] Windows 10/11 WinForms 界面、DPI、热键交互、真实键鼠输入、暂停/继续/停止和长时间运行的人工验收：延期 M14，当前 NOT RUN。
- [ ] PRD 2.14 所述离线函数参考 HTML 生成链尚未建立；当前包先包含 Markdown，HTML 任务保留到后续帮助系统完善。

## M12–M13 — 扩展 API、插件和自动运行


### M12 当前进度（2026-11-13；M12-02 文件/目录、M12-07 剪贴板/对话框、M12-08 受控进程和 M12-09 HTTP(S)/JSON 已进入 main；M12-09 Windows Actions run 43427860126 全 PASS，桌面验收仍留 M14）

- M12-08 已实现三项公开 API：StartProcess、WaitProcess、GetProcessInfo；复用 processExecutables 精确路径请求与每次运行逐项批准，Worker/host 双层 Grant 校验；UseShellExecute=false + ArgumentList，参数数组不经 Shell；当前运行随机句柄隔离，Pause-aware 等待（暂停时间不消耗 timeout），超时/Stop 只取消等待而不终止外部进程；退出后可查 exitCode。API registry 为 1.1 / 102 项。Windows Actions run 43414530292 全 PASS：152/152 tests、Build 0 warnings/0 errors、API handbook、self-contained publish、Sprite/Worker smoke、ZIP/SHA256 与 artifact upload；桌面验收仍留 M14。
- M12-01 已完成并进入 main：Core 权限请求/批准/Grant 数据模型、schema v1 解析和范围校验；每脚本可选 sidecar 为脚本文件全名后缀 .permissions.json，最大 256 KiB，声明本身不代表授权。
- 主程序普通播放/调试启动前逐项审阅本次请求，默认全不勾选、取消不启动 Worker；缺少 sidecar 使用空 Grant，损坏/未来 schema/重解析点 sidecar 拒绝启动。
- M12-02 已完成文件 API 首批 8 项与补齐 5 项：ReadTextFile、WriteTextFile、AppendTextFile、FileExists、DirectoryExists、ListFiles、ListDirectories、CreateDirectory、CopyFile、MoveFile、RenameFile、CreateTempFile、CreateTempDirectory。Worker bridge 发 IPC 前校验 Grant；PlaybackRunner 对每条文件 IPC 再次检查；未知命令 fail-closed。SpriteLauncherForm 复用同一 PlaybackRunner，未提供授权时仍使用 Empty Grant。
- API registry 从 0.8/89 项升至 main 开发版 0.9/94 项；FUNCTION-REFERENCE 由同一注册表生成，IDE 补全/签名继续读取 LuaApiRegistry，不另建函数表。CopyFile/MoveFile/RenameFile 的 overwrite 参数默认 false。
- Windows 文件适配按相对路径以脚本入口目录解析；拒绝设备/扩展设备路径、盘符相对路径、NTFS ADS、reparse point/符号链接和多硬链接文件。文本严格 UTF-8、4 MiB；目录枚举非递归、最多 1000 项。普通文件复制/跨卷移动最大 64 MiB；复制使用目标同目录随机临时文件、64 KiB 分块、磁盘 flush、SHA-256/长度校验后句柄级原子提交；Stop 在循环与提交前检查，取消/校验失败不会替换已有目标。同卷移动/重命名使用源文件句柄级 rename；跨卷移动只在目标校验提交后删除源，删除失败会报告并保留源/目标可恢复状态。
- CreateTempFile / CreateTempDirectory 必须显式提供已批准写入目录/父目录，随机名称配合 CREATE_NEW/单级目录创建，返回完整路径；不自动访问或扩大系统 TEMP 授权。首轮只支持普通文件，目录复制/移动/重命名不支持。
- Windows Actions run 43384232623 和 43384744116 均在 Core tests 发现 Windows FileRenameInfo 行为问题（先是非空 RootDirectory，后是目标路径格式）；分别修复为 RootDirectory=NULL、完全限定 verbatim 路径并加入 UTF-16 终止符。最终 run [43385033936](https://github.com/xiaobei206/anjian/actions/runs/43385033936) 对 HEAD 057f400657c8ebaf673eeefae17030476d71789b 全 PASS：Restore；Release Build 0 warnings/0 errors；142/142 Core tests、0 skipped；94 个公开函数的 API handbook check；自包含 win-x64 publish；包内 Sprite/Worker smoke；ZIP/SHA256；artifact upload。Artifact ID 14018690914，87,196,057 bytes，上传 artifact SHA-256 5ebc2dae013634a0018f1ffed5c0e74e49d7cef253f9e33bc0f4e630452fe431。本地容器没有 .NET SDK，local Restore/Build/Test 为 NOT RUN。
- Windows 桌面权限窗口、小精灵逐次 sidecar 审阅和 Windows 10/11 真实桌面文件操作均 NOT RUN，统一留到 M14；CI PASS 不代表实机验收。
- 未修改 VERSION / AssemblyVersion / FileVersion；不创建 tag/Release；已发布的 v0.1.0-preview.4 资产保持不变。


### M12-07 剪贴板与对话框（DONE：实现与 Windows CI PASS；桌面验收 M14）

- 新增 `GetClipboardText()`、`SetClipboardText(text)`、`MessageBox(text,title?)`、`InputBox(prompt,title?,defaultValue?)`、`Confirm(text,title?)`，registry 从 0.9/94 升为 1.0/99。
- 权限使用既有 sidecar：`clipboardRead`、`clipboardWrite` 与 `dialogs: ["message","input","confirm"]`；每次运行由用户明确批准。Worker 侧和 PlaybackRunner 主进程分别按本次 Grant 验证，未知命令 fail-closed。
- Clipboard 与自定义模态对话框都调度到 WinForms 主 UI/STA 线程；输入/提示/标题/剪贴板文本有长度上限；InputBox 取消返回 nil，Confirm 否/关闭返回 false；Stop 会关闭挂起的自定义对话框。暂停在命令边界应用；剪贴板已提交的外部副作用不承诺回滚。
- 当前提交：实现 `7c561f9d342d7d6f64a9a14535898b8790bb08f6`，API 契约 `bd316e26c34f6ddf3198036875baba06cb568d9a`，函数手册由 Catalog 重生成 `6d1ae9e64764dd55bec9c55b439b3f8b6fcff4f0`。Windows Actions run [43408020908](https://github.com/xiaobei206/anjian/actions/runs/43408020908) 全 PASS：147/147 tests、0 warnings/errors、API handbook、self-contained publish、Sprite/Worker smoke、ZIP/SHA256 与 artifact upload。Artifact ID `14027962784`，87,207,518 bytes，archive digest `sha256:0887a163767200037a81109f6be5e0695cdd7d66aeda0341fdeccf429fca0cce`。所有真实 Windows 10/11 权限、剪贴板和窗口交互继续 NOT RUN，统一留 M14。

### M12 后续实施顺序（必须按安全依赖推进）

1. M12-07：已完成；Windows Actions run 43408020908 全 PASS，真实剪贴板/对话框交互待 M14。
2. M12-08：DONE（Windows CI run 43414530292 全 PASS）；精确 EXE 启动/等待/查询及退出状态，不开放任意 Shell；真实桌面验收留 M14。
3. M12-09：DONE（Windows Actions run 43427860126 全 PASS：161/161 Core tests、0 warnings/0 errors、API handbook 103 项、自包含 win-x64/Sprite Worker smoke/ZIP/SHA256 上传全通过）；HttpRequest、精确主机/方法、HTTPS 默认、HTTP 单独审批、重定向逐跳授权、超时/Stop、请求/响应上限；真实网络桌面验收留 M14。
4. M12-10：DONE（本地 manifest/目录 ZIP、安全校验、API 兼容、三方权限交集、版本隔离、管理窗口与独立 Worker；自动化结果见下方 M12-10 实施与验证记录；真实桌面验收 M14）。不加载任意 DLL，不在主 UI 执行插件 Lua。
5. M12-11：DONE（Windows Actions run 43436997492 全 PASS：173/173 Core tests、0 warnings/0 errors、API handbook、自包含 win-x64 发布、Sprite/Plugin Worker smoke、ZIP/SHA256 与 artifact upload 全通过）。小精灵按入口收集并绑定 permissions sidecar，Launcher 按 manifest SHA-256 对正在审阅的字节做校验并复用逐次权限审阅；无 sidecar 时为空 Grant，声明本身不授权。真实 WinForms/权限/DPI/多屏实机验收仍留 M14。
6. M12-12（待办）：按 PRD §2.12 完成第三方插件 Lua API/命令贡献接入。先冻结贡献描述 schema、兼容性、命名冲突/权限边界与调用生命周期 ADR，再做最小端到端垂直切片，统一接入 API registry、IDE 补全/签名、命令面板和函数参考。不得让插件在主 UI 执行 Lua、加载任意 DLL 或扩大脚本 Grant。
7. 每批功能完成后同步 registry/API version、中文函数手册、IDE 提示和契约测试；Windows CI 不替代 M14 桌面验收。

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


## M9 — OCR 与文本识别（实现/Windows CI DONE；桌面验收统一延期至 M14 后）

### 首轮已进入 main 的实现
- API registry 从 0.6 升至 0.7，当前公开 API 81 项；新增 `OCR`、`FindText`、`WaitText`，由单一注册表驱动 Lua runtime、编辑器补全/签名和函数手册。
- Core 新增引擎中立 `IOcrRecognitionService` 与文本/行/多边形 DTO；Core 不引用第三方 OCR 或 ONNX 包。
- Windows App 新增 PaddleOcrNet 2.4.0 适配层，CPU 模式；OCR 模型管理窗显式下载 ch/en/japan 模型，并把缓存写入用户 LocalAppData。
- Worker 识别严格 `Offline=true`，模型缺失只报错，不在脚本执行期间自动联网。模型管理下载使用上游 SHA-256 校验报告。
- OCR 输入最多 16 MP / PNG IPC 64 MiB；WaitText 通过主进程截图 IPC，在每次截图前走 checkpoint、支持 Pause；Stop 终止独立 Worker，截图资源在 finally 中释放。
- 返回逐行全文、confidence、图像局部矩形/多边形；截图另返回 screenX/screenY 并保留虚拟桌面负坐标。

### M9 后续开发/自动化验收
- Core API 自动测试覆盖 Lua 表结构、文本匹配模式、置信度过滤、屏幕坐标映射、WaitText checkpoint、Pause-aware delay 与截图释放。
- Windows Actions 最终 run [43363150335](https://github.com/xiaobei206/anjian/actions/runs/43363150335) SUCCESS：Restore PASS；Release Build 0 warnings / 0 errors；90/90 Core tests；API handbook --check；self-contained win-x64 publish；ZIP/SHA256 与 artifact upload 全 PASS。Artifact ID `14008744436`，87,078,010 bytes；artifact SHA-256 digest `70a0d63982f035b3169c4319867d6a893ed891e8ba5046976a3a328d455f6ccc`。
- M9 桌面验收行 M9-01..M9-08 仍全部 NOT RUN，随 M1–M8 一并推迟至 M14 后的集中验收；重点为模型下载/哈希、断网识别、三种语言、文本边界框、超大/损坏图片、WaitText Pause/Stop 和多显示器负坐标。Windows CI 仅证明编译与自动化契约测试，不代表真实模型识别质量通过。
- 上游包规模较小，保留 Core/App 适配边界；若 CI、许可或实际质量不达标，替换 App adapter，不改变稳定 Lua DTO/API 契约。


## M10 — 参数、配置与脚本自定义 UI（范围冻结与实施记录；当前已完成）

以 PRD 2.9、2.13、F20/F26 为完整范围，先设计稳定 schema，再实现闭环。M10 不得把脚本运行时变量误当作用户启动参数。

### 目标与基本数据契约
- 每个脚本/入口可声明具稳定 ID 的参数：机器名、中文标签、类型、必填、默认值、最小/最大值、选项、帮助说明、验证规则和排序。
- 首轮类型覆盖文本、整数、小数、布尔、下拉/单选、多选、文件路径、目录路径、热键和可选颜色值；所有类型必须定义序列化格式、范围/选项验证、空值和默认值语义。
- 参数 schema 作为受版本管理的数据模型保存，具有 schemaVersion、scriptId、createdAt/updatedAt 和稳定参数 ID；未知未来 schema 不覆盖原文件。
- 用户输入在启动前统一校验；错误指出字段、值和具体规则，未通过时不得启动 Worker。成功后将类型明确的参数安全注入 Lua 独立 Worker，区分用户参数与运行中的 Lua 变量。
- 自定义 UI 根据同一 schema 自动生成通用表单；开发环境和未来小精灵打包器共用 schema/validator/rendering model，不复制第二套参数定义。
- 配置支持保存、重新载入、恢复默认、导入/导出；使用原子写入/备份和迁移策略；敏感输入默认不写普通日志。

### M10 实施顺序与验收
1. 读取 PRD 2.9/2.13、Architecture、Decision 与当前 Worker IPC；先更新 ADR/API/架构和验收标准，冻结参数 schema。
2. 建立 Core 参数定义/值/校验 DTO 与纯函数 validator，覆盖缺少必填、类型不匹配、整数/小数边界、选项、多选重复项、文件/目录路径、热键格式和颜色值。
3. 确定脚本参数定义的声明方式（建议由一个版本化脚本元数据/参数描述文件承载；不要在 Lua 文件执行前为读取定义而执行用户脚本），并给出未知 schema 保护/迁移设计。
4. 实现参数编辑器与运行前表单；用户可新增/编辑/删除/排序参数，设置默认值与约束，预览最终运行表单。
5. 把已校验的强类型值通过独立 IPC 传入 Worker，并提供只读 Lua 参数表入口；不能通过字符串拼接生成 Lua 代码或把用户输入当代码执行。
6. 实现配置原子持久化、备份、恢复默认、JSON 导入/导出和损坏配置恢复。
7. 同步 registry（仅在确实新增用户可调用 API 时）、中文函数手册、IDE schema 提示、User Guide、Feature Matrix、Acceptance 与 Development Log；编写 Core/IPC/UI 自动测试。

### M10 自动化验收
- 每种类型的有效/无效值和默认值、必填、边界、选项、多选、文件/目录/热键/颜色校验。
- 参数 schema/配置 round-trip、原子写入、备份、损坏恢复、未知 schema 与迁移失败原文件保护。
- 参数经 Worker IPC 后保持实际类型，不发生代码注入；启动校验失败时 Worker 不启动。
- UI 与 Lua 读取完全相同值；敏感项不进入普通日志。
- Windows CI Restore/Build/Core Tests/API handbook/self-contained win-x64 publish/ZIP/SHA256 必须全 PASS。真实 WinForms 表单、DPI 和交互仍延后至 M14 集中验收。


## M10 — 参数、配置与脚本自定义 UI（DONE：实现/自动化；桌面验收待 M14）

### 首轮规格冻结（2026-11-13）
- ADR-022 决定脚本 sidecar `<script>.params.json` schemaVersion=1；不执行 Lua 读取声明。用户值与参数定义分离，配置存放用户数据目录并按脚本规范化路径派生稳定键。
- 支持 text、integer、number、boolean、select、multiSelect、filePath、directoryPath、hotkey、color 十种类型；每项含 stable ID、Lua 名称、中文标签、required/default、范围、选项、说明、规则、排序和敏感标记。
- Core 统一验证器用于默认值、启动值、JSON 导入、UI 表单和未来 M11 小精灵；启动失败时禁止创建 Worker。
- IPC 使用独立 JSON 参数载荷，Worker 将真实类型映射到 Lua 值，禁止 Lua 字符串拼接。脚本只读入口计划为 `Args`。
- 配置采用原子替换、上一份有效备份、损坏恢复；未知未来 schema 只读拒绝，绝不覆盖。
- M10 桌面验收行将保持 NOT RUN，随 M1–M13 于 M14 后集中执行。

### 开发顺序
1. Core schema DTO / validator / JSON 配置存储与单元测试。
2. PlaybackRunner 启动前加载/校验；Worker 握手注入 typed JSON；Lua 只读 `Args`。
3. WinForms 参数设计器与自动表单；保存/载入/默认/导入/导出；敏感字段脱敏。
4. Windows CI 验证 Restore/Build/Test/API handbook/self-contained win-x64/ZIP/SHA256；桌面验收延后。


### M10 已实现的首轮代码（2026-11-13）
- Core：`ScriptParameterSchema`、`ScriptParameterValidator`、`ParameterConfigurationStore` 和 `LuaParameterInjector`；类型校验保留 JSON 原生类型，覆盖必填、范围、选项、多选重复、路径存在性、热键、颜色、正则和未来 schema 保护。
- App：`ScriptParameterService`、`ScriptParameterDialog`、`ScriptParameterDesignerForm`；`<script>.params.json` 与 LocalAppData 运行配置分离，播放/调试启动前统一读取/校验，用户取消或校验失败不启动 Worker。
- Worker：通过 `worker-ready` 握手接收结构化 JSON 参数，构建 Lua 原生值；`Args` 使用递归代理拦截常规写入，不把用户输入拼接为代码。该只读代理不是恶意脚本沙箱。
- M10 最终代码 HEAD `a923f1e02dce73629514da39bb10925b1271fc15` 的 Windows CI run [43368224862](https://github.com/xiaobei206/anjian/actions/runs/43368224862) SUCCESS：Restore PASS、Release Build 0 warnings/0 errors、102/102 Core tests、API handbook check、自包含 win-x64 publish、ZIP/SHA256 与 artifact upload 全 PASS。artifact ID `14010789337`，87,099,638 bytes，SHA-256 `324ed884d9938548d2d35b94cd372a4bc46bf081f7abd1c77e17b28d1acf1323`（有效至 2026-12-13；非 Release）。
- M10-01..M10-10 的桌面实测保持 NOT RUN；按统一策略延期至 M14 后。


### M12-09 实施范围（2026-11-13）

- 公开 API：`HttpRequest(url, options?)`；registry 1.2 / 103 项，函数手册由 Catalog 生成，IDE 补全继续读取同一 registry。
- 复用 ADR-024 的 `httpHosts`、`httpMethods`、`allowInsecureHttp`；逐次批准、主机/方法精确匹配、不接受通配符/IP/非默认端口。HTTPS 默认，HTTP 单独审批。
- 手动最多 5 次重定向，每一跳重新授权；301/302 POST 和 303 非 HEAD 改 GET，307/308 保持；跨 origin 不转发 Authorization/自定义凭据头。
- 请求头 32 项/32 KiB、正文 1 MiB、响应头 32 KiB/64 项、解压后响应正文 4 MiB；timeout 1–120 秒；Stop 取消发送和响应流读取。
- 仅有界缓冲请求；不实现 Cookie、代理配置、WebSocket、任意 TCP 或大文件流式传输。真实桌面验收留 M14。


### M12-10 本地插件包、manifest 与生命周期（2026-11-13）

- 状态：代码与 Windows 自动化验证完成。本地手动 Restore/Build/Test 因执行容器未安装 .NET SDK 而 NOT RUN；Windows 10/11 WinForms 权限审阅、DPI、多屏及真实用户交互仍 NOT RUN（统一留 M14）。
- Windows Actions run [43433992383](https://github.com/xiaobei206/anjian/actions/runs/43433992383) 对代码 HEAD 582db78d9bddbed820727dd1bf01b19847beb5f6 全 PASS：Restore；Release Build 0 warnings / 0 errors；Core tests 171 passed / 0 failed / 0 skipped；API handbook check；self-contained win-x64 publish；published plugin Worker 与 Sprite Worker smoke；ZIP/SHA256 和 artifact upload 全 PASS。Artifact ID 14042985442，87,263,152 bytes，archive SHA-256 ebe8ca9bc7c4f82723e86628da5755bd783d655a9c1a170c7112336919e95af1，有效至 2026-12-13；这是 Actions 临时 artifact，不是 GitHub Release。
- 目录/ZIP 导入只接受包根 Anjian.Plugin.json；要求顶层及 permissions 子对象都显式为 schemaVersion=1、稳定全小写 pluginId、语义版本、API 兼容区间、根级 Lua entry、可选 modulesRoot，以及不含 manifest 自身的闭合 files 清单。每个内容文件用 SHA-256 校验；启动时再次校验，require 仅加载 manifest 登记且 hash 匹配的 Lua 模块。
- 限制包内容类型、路径、重解析点/链接、文件数量及单文件/总大小；拒绝 DLL/EXE/Shell/驱动/安装器和 ZIP 路径穿越/重复路径/链接。ZIP 解压后经同卷 staging 完整校验再移动到 pluginId/version 版本目录。
- 安装默认停用；catalog schema v1 原子写入并保留损坏/未来版本；同 ID/版本不覆盖不同内容；启用一个版本会停用同 ID 其他版本，卸载需先停用。已损坏安装包在列表中隔离显示并不能运行。
- 权限为脚本入口 sidecar 请求 ∩ 插件 manifest 声明 ∩ 用户本次明确批准；文件根只保留重叠的较窄根，EXE/HTTP/dialog 集合精确相交，布尔权限 AND。没有 sidecar/批准时能力为空；host 对每条 IPC 再校验。插件在单独 Worker 的 Lua state 执行，受限 require 禁止普通 io/debug/dofile/loadfile/package 原生模块入口。
- 管理 UI 可导入目录/ZIP、查看声明权限、显式启用/停用/卸载、逐次审阅权限并运行/暂停/停止插件。此任务没有新增公共 Lua API；registry 仍为 1.2 / 103，函数手册与 IDE 补全仍以 registry 为唯一来源，因此无需改写 FUNCTION-REFERENCE。


### M12-11 小精灵权限 sidecar（2026-11-13；自动化 DONE，桌面审阅留 M14）

- `<entry.lua>.permissions.json` 按入口独立收集，精确绑定到 `SpritePackageEntry.PermissionManifestPath`；文件进入 `Anjian.Sprite.json` 的 files 闭合清单，并纳入 SHA-256 / `SHA256SUMS.txt`。
- 构建和包校验验证 schema v1、256 KiB 上限、reparse point 与入口绑定；每次启动所选入口再次校验文件、大小和 hash。Launcher 将 manifest 中的期望 SHA-256 传入权限审阅服务，服务对实际解析请求的同一份 UTF-8 字节验证哈希，避免仅先验证后重新读取而产生的内容替换窗口。
- 复用既有逐次 WinForms 审阅；明确拒绝/取消不会启动 Worker。没有 sidecar 的入口合法但 Grant 为空；声明不代表批准。Host 对每条 IPC 仍独立检查 Grant，未修改公开 Lua API registry（1.2 / 103）。
- 最新 Windows Actions run [43436997492](https://github.com/xiaobei206/anjian/actions/runs/43436997492) 对 HEAD `ff5dc1510f4f68fe23e4abf54e2c291f6df6229b` 全 PASS：Restore、Release Build 0 warnings / 0 errors、173/173 Core tests、API handbook check、自包含 win-x64 publish、发布包 Sprite 与 Plugin Worker smoke、ZIP/SHA256 及 artifact upload。Artifact ID `14044230189`，87,267,435 bytes，archive SHA-256 `8c4312eb072012db084917bd025d5640e105d623a4d7df819632bf7cf1e58fc0`；这是临时 Actions artifact，不是 GitHub Release。
- 本地容器无 .NET SDK，local Restore/Build/Test 为 NOT RUN。Windows 10/11 WinForms 审阅 UI、批准/拒绝路径、DPI/多屏实机验收 NOT RUN，统一留 M14；不能用 CI smoke 代替。
