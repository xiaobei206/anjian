# 按键精灵 2026：完整功能矩阵

- 基线日期：2026-11-13
- 对应完整产品目标：docs/PRD-CN.md
- 功能阶段：docs/ROADMAP-CN.md
- 原则：本矩阵中的完整目标能力属于正式交付范围；阶段号表示实施顺序，不代表可以删除或无限期延期。
- 状态：只描述当前已验证的事实。CI 通过不等于 Windows 桌面实机验收通过。

## 验收策略变更（用户决定，2026-11-13）

M1–M13 的 Windows 桌面实机验收统一延期至 M14 全部功能开发完成后集中执行。代码、单元测试和 Windows CI 可分别按证据记录，但不能将任何尚未实测的桌面项标记为 PASS。各阶段桌面验收仍须保留完整步骤，并以 NOT RUN / FAIL / PASS 如实更新。

## 状态定义

- **已实现（CI PASS，实机待验收）**：代码已进入当前仓库，Windows CI/自动测试通过，但仍需要桌面实测。
- **部分实现**：只有基础能力存在，离完整产品目标仍有明确缺口。
- **未实现**：目前没有可用实现。
- **目标定义**：已有范围和验收标准，具体代码尚未完成。
- **验收通过**：必须有自动化测试或记录测试环境的手工证据，不以文档或代码阅读代替。

## 完整功能矩阵

| ID | 功能域 | 1.0 完整目标 | 当前主线状态（已发布的 preview.4 资产不变） | 目标阶段 | 完成验收的最低标准 |
|---|---|---|---|---|---|
| F01 | 应用启动与窗口生命周期 | 主窗、独立悬浮窗、托盘、图标、退出与状态恢复 | 已实现（CI PASS，实机待验收） | M1 | Windows 10/11 实测窗口、托盘、任务栏、图标与退出行为 |
| F02 | 工程/脚本管理 | 工程、脚本列表、多标签、资源目录、最近项目、恢复与迁移 | 部分实现：脚本文件管理与 M3 多标签已接入；工程资源目录、最近项目、项目恢复/迁移尚未实现 | M3 | 保存/重开/复制/迁移和原子写入测试完整通过 |
| F03 | Lua 运行时 | Lua 5.4、错误行号、状态机、停止/暂停、状态隔离 | 部分实现 | M2/M10 | 运行、取消、异常、崩溃和资源清理测试通过 |
| F04 | 代码编辑器 | 高亮、行号、补全、参数提示、诊断、查找替换、折叠、多标签 | 部分实现：M3 已实现语法高亮、行号、注册表驱动补全/签名提示、只编译语法诊断/错误行导航、查找替换与多标签；代码折叠和 Windows 桌面手工验收仍未完成 | M3 | 自动化测试覆盖注册表一致性/语法诊断；Windows 实机验证中文/日文、1MB 脚本、DPI、标签保存与错误导航 |
| F05 | 断点调试 | 断点、单步、运行到光标、变量窗、监视、调用栈 | 部分实现：M4 main 基础版已加入文件/行号断点、真实 Lua Line hook 暂停、单步进入/越过/跳出、运行到光标、局部/可读全局变量快照及调用栈；断点暂为当前打开标签内存态，未实现命名 watch / 表展开；Windows 桌面手工验收未执行 | M4 | 自动测试覆盖断点/单步/嵌套调用/运行到光标/快照/Hook 清理；另需 Windows 桌面手工验收 |
| F06 | API 命令面板 | 分类、搜索、插入、签名、返回值、中文帮助、示例 | 未实现 | M3 | 从注册表生成，展示内容与参考手册一致 |
| F07 | API 注册与手册 | 单一 API 描述源，生成补全、提示、命令面板和中文函数/参数手册 | 内置 registry 驱动运行时 binding、IDE 补全/签名和静态手册；M12-12 启用插件贡献通过内存 extension view 进入 IDE 与 API 命令面板，并可生成本机动态参考；Windows Actions PASS，真实桌面 UI M14 NOT RUN | M2/M3/M12 | 内置手册 check 可重复；插件签名/说明来自同一合并元数据，停用/损坏插件不在后续视图 |
| F08 | 键鼠前台控制 | 移动、点击、双击、Down/Up、滚轮、组合键、Unicode | 部分实现：基础输入已接入 | M1/M4 | 按键布局、取消、安全释放和权限错误测试通过 |
| F09 | 输入录制 | 全局录制、暂停/续录、点击模式、事件过滤、异常告警 | 结构化原始事件与动作转换已接入；保留 Hook 的暂停/续录、click-only、注入过滤、Move 合并和溢出计数；Hook 实机可靠性仍未验收 | M1/M5 | Hook 稳定性、重复/漏录、注入过滤、队列压力需 Windows 实机通过 |
| F10 | 可视化动作列表 | 动作编辑、分组、排序、参数/延时编辑、代码生成区块同步 | M5 实现已完成，Windows CI PASS（37/37 tests、Build 0 warning/0 error、API check、win-x64 publish/artifact upload 均 PASS）；Windows 桌面交互仍 NOT RUN | M5 | 修改动作后生成代码一致；块外代码保留；手改/删除区块必须预览并确认；sidecar round-trip 与不兼容 schema 不覆盖原文件 |
| F11 | 坐标基础 | 当前屏幕绝对物理像素、负坐标、多屏 | 已实现基础语义，DPI 实机待验收 | M1/M6 | 100–200% DPI、混合 DPI、多屏和负坐标测试通过 |
| F12 | 多坐标空间 | screen/window/client/anchor/ratio 及双向换算 | 坐标 API 已实现并通过 Windows CI；M5 录制仍固定 screen 物理像素，窗口相对录制/回放绑定未实现；桌面验收 NOT RUN | M6 | 各空间转换、窗口移动/缩放、既有 screen 录制/回放不回归；窗口相对录制绑定另需实现；100–200% DPI/多屏实机通过 |
| F13 | 窗口管理 | 查找/枚举/安全窗口引用、属性、激活/显示状态/置顶/移动/尺寸/对齐/等待 | 基础 Win32 API 已实现并通过 Windows CI；含子窗口屏幕坐标换算和 HWND 竞态保护；桌面验收 NOT RUN | M6 | 标题/类名/进程过滤、外框/客户区/焦点/isChild、销毁/复用保护、对齐和超时/取消桌面测试通过 |
| F14 | 控件自动化 | 可访问控件信息、文本/选项/勾选和控件操作 | M7 实现并通过 Windows CI（run 43350412493；64/64 tests）；Windows 桌面实机 NOT RUN | M7 | 对声明支持的控件类型有能力探测；不支持/失效/密码读取限制均明确报错；Stop 可终止卡住的 worker |
| F15 | 前后台适配 | 后端能力矩阵、绑定/解绑、输入结果与失败原因 | M7 实现与能力边界通过 Windows CI；UIA 语义操作可按 Pattern 工作；通用后台鼠标/键盘/文本明确 unsupported；桌面实机 NOT RUN | M7 | 能力状态/后端/限制精确；绝不伪报通用后台键鼠成功，不静默回退，不绕过 UIPI/UAC |
| F16 | 截图与图像资源 | 全屏/窗口/客户区/区域截图，资源预览、裁剪和缓存释放 | 部分实现：M8 资源 UI 已进入 main，支持 PNG 导入/列表/预览/命名、.images.json schema、路径边界、PNG 尺寸/像素数/文件大小/SHA-256 校验；脚本相对路径解析与静态 LoadImage 依赖扫描；重命名/复制/删除同步管理 manifest/assets；本轮图像池仍限 128 项/256 MiB；Windows Actions [43358188770](https://github.com/xiaobei206/anjian/actions/runs/43358188770) 85/85 Core tests PASS、Build 0 warnings/errors；Windows 桌面验收 NOT RUN | M8 | 资源清单/PNG 损坏、路径穿越、哈希不一致、脚本复制/重命名依赖更新和 128/256 MiB 生命周期实机验证；最终 Windows 10/11 验收 |
| F17 | 找色/颜色比较 | 单点颜色、多点找色、容差、颜色计数/色块 | 部分实现：RGB 比较/单点/多点找色/计数 + `RgbToHex` / `HexToRgb` / `ColorDifference` / `FindColorBlocks` 已接入 registry 0.6；颜色块采用八邻域连通分量、最多 400 万采样点/100 结果；命中事件可进入视觉调试层；Windows Actions 85/85 Core tests PASS，Windows 桌面 NOT RUN | M8 | RGB 编码/各通道容差、hex 格式、差值公式、颜色块连通/排序/step/取消和真实屏幕颜色显示实测 |
| F18 | 找图 | 区域/相似度/多结果/排序/超时/等待出现或消失 | 部分实现：透明模板、灰度/缩放、多结果与等待 API 保持；新增脚本模板资源 UI、预览/重命名/路径校验，视觉调试层显示截图区域、找图/找色命中框、相似度、颜色与耗时；实际 Windows 桌面验收 NOT RUN | M8 | 覆盖透明模板、灰度/缩放、阈值边界、误命中/性能/取消；验证覆盖层点击穿透/截图排除和资源管理 UI；最终 Windows 实机验收 |
| F19 | OCR | 本地识字、语言/字库配置、文本/边界框/置信度 | 已实现（Windows CI PASS，桌面验收延期至 M14 后）：PaddleOcrNet 2.4.0 App 适配层（CPU）、显式 ch/en/japan 模型管理、默认严格离线、OCR/FindText/WaitText（API 0.7/81 项）；run 43363150335 通过 90/90 Core tests、Build 0 warning/error、API check、self-contained publish 与 artifact 上传；尚未发布到 preview.4 | M9 | 语言模型下载与 SHA-256、断网识别、文本/置信度/矩形/多边形、FindText exact/contains、WaitText Pause/Stop/timeout、输入限制与多屏坐标在集中 Windows 10/11 实测通过 |
| F20 | 脚本参数 | 参数声明、类型/默认值/范围/选项/校验/持久化 | DONE（实现/自动化）：Core schema/validator/store、Worker typed JSON、递归只读 Args、运行时脚本相对路径解析和未来 schema 防覆盖已完成；Windows CI run 43368224862 的 102/102 tests PASS；WinForms/Windows 桌面验收 M14 后 | M10 | 启动前校验，参数 UI 与脚本取值一致，配置可迁移 |
| F21 | 变量与流程 | Lua 原生变量/表/循环/函数、模块与可复用函数库 | Lua 原生语法可用；模块/工程集成部分 | M3/M10 | 示例库、模块查找和错误传播测试通过 |
| F22 | 运行选项 | 次数、轮间隔、热键、超时、错误策略和运行实例状态 | 部分实现：次数/轮间隔、F10/F11/F12；M13-02 已提供选中实例 Pause/Resume/Stop 与 4+16 容量反馈；用户超时/错误重试策略待 M13-03，完整实例列表待 M13-04 | M1/M10/M13 | 主/悬浮窗同步、持久化、独立实例停止/暂停、超时和失败策略通过 |
| F23 | 日志与诊断 | 结构化日志、级别/脚本/行号、导出、轮转、可复制错误报告 | 部分实现 | M2/M10 | Lua 运行失败可关联行号，日志脱敏且可导出 |
| F24 | 小精灵打包器 | 独立运行包/EXE 入口、独立 UI、参数表单、图标、资源依赖 | DONE（M11 + M12-11 main；Windows Actions run 43436997492 的 self-contained 包/Worker smoke PASS；权限审阅 UI/DPI 实机验收仍留 M14） | M11/M12 | 干净 Windows 用户环境无需 IDE 即能运行生成包；每个入口权限请求不串用且逐次审阅 |
| F25 | 打包完整性 | manifest、依赖检查、SHA256、版本/API 最低要求、配置迁移 | DONE（逐文件 SHA256、清单闭合校验、路径/资源/schema/OCR 验证、稳定 PackageId/EntryId；M12-11 侧车按入口绑定并纳入 manifest/hash，启动时复核；Windows Actions run 43436997492 全 PASS） | M11/M12 | 损坏/缺失资源与权限侧车篡改能被检测；manifest、SHA256 和授权审阅依据一致 |
| F26 | 自定义用户界面 | 标签、文本/数字框、开关、选项、文件/目录、热键等参数控件 | DONE（M10 参数设计器/动态表单及 M11 小精灵复用同一 schema/validator；配置跨包版本稳定隔离；自动化 PASS；WinForms/DPI/交互验收 M14） | M10/M11 | UI 可配置、参数验证和用户运行状态完整 |
| F27 | 插件扩展 | 本地插件包、manifest、API 版本、权限提示、启停/卸载、第三方 Lua API/命令贡献 | M12-10/11 本地包生命周期、三方权限交集及入口 sidecar 已实现；M12-12 contributions schema v1、纯 Lua helper、内存 registry extension、IDE 补全/签名、命令面板和动态参考已实现；Windows Actions Run 43441156420 PASS；真实 UI/DPI/多屏 M14 NOT RUN | M12 | 安装/每次 Worker 启动校验闭合文件和 SHA-256；不加载 DLL、不增加 IPC/Grant；冲突启用拒绝且不写 catalog |
| F28 | 文件/目录/剪贴板 | 受控文件操作、剪贴板、对话框与常用数据 API | PARTIAL：受控文件/目录 13 项与剪贴板/对话框 5 项 API 已实现；均使用本次 Grant 双层校验；Windows Actions run 43408020908 的 147/147 tests、Build、API handbook、自包含发布与 Sprite/Worker smoke 全 PASS；真实桌面交互待 M14 | M12 | 文件权限/链接/上限/取消契约测试；剪贴板读写、三类对话框、默认拒绝、伪造 IPC 与主线程/停止路径验证 |
| F29 | 进程/应用控制 | 启动、等待、查询、明确权限的结束操作 | PARTIAL：StartProcess/WaitProcess/GetProcessInfo 已实现；Windows Actions run 43414530292 的 152/152 tests、0 warnings/errors、API handbook、win-x64 发布、Sprite/Worker smoke、ZIP/SHA 与 artifact 上传全 PASS；不提供任意进程终止/Shell，桌面验收 M14 | M12 | 精确路径默认拒绝；参数边界；超时/Stop/Pause；查询退出码；句柄不可跨会话或查询任意 PID |
| F30 | 网络/JSON | 显式 HTTP(S)、超时、状态码、JSON、上传/下载 | PARTIAL：HttpRequest 已实现；Windows Actions run 43427860126 的 161/161 Core tests、0 warnings/0 errors、API handbook 与自包含包 smoke 全 PASS；精确主机/方法逐次授权、HTTPS 默认、HTTP 单独批准、逐跳重定向授权、超时/Stop、1 MiB 请求体与 4 MiB 响应体上限；大文件流式上传/下载尚未实现 | M12 | 默认不自动联网；未批准主机/方法/HTTP/重定向拒绝；超时/Stop/响应上限可靠；无网络时其余功能正常 |
| F31 | 定时与多实例 | 定时运行、独立实例、任务状态、超时和失败策略 | 部分实现（M13-01/02）：Core/App 协调器已实现 4 active + 16 FIFO、同脚本独立 Start/请求幂等、每实例 Worker/Lua/参数/源码/Grant/进程句柄隔离、Worker IPC 身份+PID 绑定、全局 FIFO InputArbiter、选中实例 Pause/Stop 和发布包 Worker 并发/崩溃 smoke；定时/周期触发、用户运行超时/重试与完整实例列表待 M13-03/04 | M13 | 实例状态、参数、日志、Lua state、权限 Grant 与句柄隔离；全局输入串行并能逐实例 Stop/清理；定时/失败策略可验证 |
| F32 | 扩展兼容性 | 稳定 API 版本、弃用策略、迁移说明、插件 API 约束 | 插件 manifest min/max API 兼容已实现；贡献 schema v1；运行期 extension view 不改静态内置 registry（1.2 / 103）与静态手册；Windows Actions Run 43441156420 PASS | M12 | API 不兼容、未知/未来 schema、保留名/重复名拒绝；内置公开 API 语义不静默改变 |
| F33 | 离线文档 | 用户手册、函数手册、打包/插件开发说明和离线 HTML | 部分实现：现有 Markdown 文档分散 | M2/M14 | 发布包内文档与发行版本匹配，可断网查阅 |
| F34 | 发布/测试 | Windows 10/11、长时稳定性、DPI/多屏、依赖许可和 SHA256 | CI 流程已接入；实机验收未完成 | M1/M14 | 测试报告、版本、ZIP、SHA256 和 release 一致 |

## 与经典 PC 版功能的对应关系

参考经典 PC 按键精灵知识库中的主界面、设置、脚本编辑器、调试、抓点/抓色/抓图、按键小精灵、插件、自定义界面、录制以及键盘/鼠标/流程/图形命令类别。本项目目标提供 Lua 5.4 功能等价，不承诺旧版 Q 脚本或旧插件二进制兼容。

主要参考：
- 经典 PC 知识库：https://www.vrbrothers.com/cn/qmacro/qkbase/index.html
- 官方电脑版下载入口：https://www.anjian.com/download
- 电脑版窗口函数目录：https://zimaoxy.com/docs/qscript/category/窗口/
- 前台/后台相对坐标基础说明：https://bbs.anjian.com/thread-542625-1-1.html

## M8 当前实机验收执行边界（2026-11-13）

- M8 代码和自动化测试证据：Windows Actions run [43358188770](https://github.com/xiaobei206/anjian/actions/runs/43358188770)，85/85 Core tests、Release Build 0 warnings / 0 errors、API 手册检查、自包含发布与 artifact 上传均 PASS。
- 本轮执行环境为 Debian 13，无 .NET SDK、无 Windows 桌面会话，也无法交互访问用户 Windows 桌面；因此未执行 M8-01..M8-18。上述 18 项状态全部仍是 NOT RUN。Windows Server 2025 CI 不构成 Windows 10/11 桌面实测证据。
- F16/F17/F18 继续保持部分实现；M8 不得标 DONE，不能据 CI 结果推断 GDI、DPI、多屏、资源 UI、overlay 点击穿透或 Display Affinity 实测通过。

## 变更纪律

- 如需删减完整产品目标，必须明确向用户说明影响并获得用户确认；不得通过把项目号改成 P2 或重排路线图来悄悄删除功能。
- 增加新 API 时必须更新注册表、函数手册、编辑器提示、测试和开发日志。
- 当前阶段只允许按依赖排序，不允许把尚未实现写成已经交付。


## M13-01 状态注记（2026-11-13）

F31 由“未实现”调整为“部分实现”，只代表 Core 契约与状态机测试进入 main。没有多实例运行时、并发 Worker、UI 管理器或 InputArbiter；这些仍必须按 M13-02/M13-04 落地。最新代码 HEAD `46bf87d` 的 Windows Actions run [43449173287](https://github.com/xiaobei206/anjian/actions/runs/43449173287) 的 187/187 Core tests、Build、API handbook 与 self-contained 包 smoke PASS；Windows 10/11 手工验收仍保持 NOT RUN（M14）。


## M13-02 状态注记（2026-11-14）

F31 从 M13-01 的“契约/状态模型部分实现”更新为 M13-02“运行时多实例部分实现”。这不等于完整的定时/多任务功能已经完成：M13-03 仍负责延迟/定时/周期触发、实例运行超时与失败重试；M13-04 仍负责完整实例列表、筛选、日志浏览和恢复体验。最新 Windows Actions run 43460841956（代码 HEAD f1ad298e）在文档提交时仍在执行；桌面验收一律保持 NOT RUN（M14）。
