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
| M11 小精灵打包器 | TODO | 独立包/EXE 入口、独立参数界面、图标、依赖收集、运行控制、清单与校验 |
| M12 内置扩展 API 与插件系统 | TODO | 文件、剪贴板、对话框、进程、HTTP/JSON，插件 manifest/权限/API 版本 |
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


## M10 — 参数、配置与脚本自定义 UI（下一开发阶段）

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


## M10 — 参数、配置与脚本自定义 UI（IN_PROGRESS）

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
- 上一次完整 Windows CI run [43366381240](https://github.com/xiaobei206/anjian/actions/runs/43366381240) 对代码基线通过：Restore、Build 0 warnings/errors、98/98 tests、API handbook check、win-x64 自包含发布和 ZIP/SHA256；artifact ID 14011010562，87,098,604 bytes，digest `7d5f988b7cdaabd3f0c3db2fe3f9b8c1ecb2fb205ee9963ca3614c745dc12b17`。之后补充数值/路径/正则和可选空值测试，最新 run [43366600434](https://github.com/xiaobei206/anjian/actions/runs/43366600434) 已 SUCCESS：100/100 Core tests、Build 0 warnings/errors、API handbook check、自包含发布、ZIP/SHA256 与 artifact upload 全 PASS；artifact ID 14009953905，digest `da291400de9917568941c927d441e1df7cec3e6daf6a2ca877b9ec83cce7cd88`。
- M10-01..M10-10 的桌面实测保持 NOT RUN；按统一策略延期至 M14 后。
