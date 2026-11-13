# 累计开发日志

本文件按时间追加记录，不覆盖历史。当前状态以 SESSION-HANDOFF.md 为准。

## 2026-11-12 — S000 — 仓库初始化与文档框架

### 目标
在 xiaobei206/anjian 建立按键精灵 2026 的唯一长期项目资料库和 GPT 会话接力机制；不开始功能编码。

### 已完成
- 核实仓库为 public，默认分支 main，连接权限为 admin/push。
- 创建 README.md，作为文档导航和项目入口。
- 建立 AGENTS.md，约定 GPT 会话开始、Git、验证、范围控制和强制 handoff 流程。
- 建立 PRD：首版产品范围、P0/P1/P2、用户流程和完成定义。
- 建立 ARCHITECTURE：项目拆分、依赖方向、线程模型、Lua 取消和输入边界。
- 建立 LUA-API：Lua 5.4 与 Mouse / Keyboard / Delay / Log API 草案。
- 建立 RECORDER-DESIGN：Raw Input/Hook PoC、动作模型、双模式同步和数据保存规则。
- 建立 ACCEPTANCE-TEST：P0 功能、录制、停止、稳定性、DPI 和发布验收。
- 建立 TECHNICAL-VALIDATION：Lua 绑定、编辑器、输入采集和便携发布 PoC。
- 建立 ROADMAP、DECISIONS、SESSION-PROTOCOL、SESSION-HANDOFF、最短接手提示词及 src/tests 目录说明。

### 技术核对记录
- Microsoft 生命周期资料将 .NET 10 列为 LTS，生命周期截至 2028-11-14。
- Microsoft LowLevelKeyboardProc 文档提醒钩子回调必须迅速返回；Windows 可能静默移除超时的低级钩子，并建议考虑 Raw Input。
- 因此录制器不预先锁定低级钩子，先做 Raw Input 和 Hook 对比 PoC。
- Lua 绑定与编辑器组件均未锁定具体 NuGet 版本；必须先实测 .NET 10、Lua 5.4、原生依赖、许可证和 self-contained 发布。

### Git 与验证
- 初始 README 提交：c6b4cf436e3de87accb95650a5e7d52321336161
- 文档规格批次提交：a595e1dd9e937e2e63b697526a5944497b7c13a1
- S000 最终提交：306ed5dc85c36576c52452a499551e4a8f9abc70
- 目录/文件落库：通过 GitHub Contents API 复读确认。
- 应用构建/测试：NOT RUN（本轮没有应用代码，符合范围）。

## 2026-11-12 — S001 — 首版交互与 API 语义冻结

### 目标
根据用户对首版交互的确认，统一脚本语法、录制移动合并、坐标语义和点击按下持续时间，避免规格分叉。

### 已确认
- Lua 用户 API 采用全局函数形式：`MoveTo(122, 100)`，不是自创 DSL，也不强制命名空间。
- 录制鼠标移动合并/降噪，保留必要路径采样点和终点；点击、按下、释放、滚轮顺序完整。
- 坐标使用虚拟桌面绝对物理像素，允许负坐标；首版不做窗口相对坐标。
- Click / KeyPress 默认按下 50 ms 后释放；显式参数指定持续时间，0 表示不增加等待。
- 长按使用 Down / Delay / Up。Click / KeyPress Stop 时立即释放，Pause 在当前原子动作结束后生效。
- 已记录 ADR-014 至 ADR-017，统一 API、架构、录制器文档。

### 外部依赖初步核对（尚未等于采用）
- NuGet 页面显示 NLua 1.7.9（2026-05-01 更新）为当前可见版本候选；KeraLua 上游 releases 显示 v1.4.11，仍需核对与 NLua 的版本依赖关系、许可证和原生资产。
- 不据此直接锁定依赖；TV-01 必须构建和执行实际 PoC。
- 参考：https://www.nuget.org/packages/NLua
- 参考：https://www.nuget.org/packages/KeraLua
- 上游：https://github.com/NLua/NLua
- 上游：https://github.com/NLua/KeraLua

### Git / 测试
- 本次为规格文档更新；提交 SHA 以本条记录所在的 GitHub commit 为准。
- 应用构建和测试：NOT RUN（仍没有应用代码或 PoC）。

### 下一步
TV-01：Lua 5.4 / .NET 10 绑定与中断机制 PoC。


## 2026-11-12 — S002 — 首个可用 Windows 预览版

### 实现内容
- .NET 10 WinForms 主程序、Core 项目和自动化测试项目。
- 脚本本地存储、编辑器、新建/打开/保存/重命名/复制/删除及右键菜单。
- NLua 1.7.9 / Lua 5.4 worker 子进程；脚本通过 IPC 请求主进程执行键鼠输入、延时和日志。
- Windows SendInput / SetCursorPos 鼠标键盘输入，输入状态跟踪和 Stop 清理。
- 全局低级鼠标/键盘 Hook 录制；过滤注入事件和本程序窗口操作，移动合并，Down/Up 配对，键盘自动重复过滤。
- Lua 点击/按键默认 50ms，显式按下时长，双击快捷 API，常见标点键和数字小键盘键。
- 播放次数默认 1，0=无限；每轮间隔默认 2000ms，只在两轮之间等待，末轮后不等待；设置持久化。
- 长文本 Unicode 注入可在字符边界响应 Stop；代理对按 UTF-16 序列注入。
- GitHub Actions Windows build/test/self-contained ZIP/SHA256 与 release 流程。

### 最新 CI 结果（测试提交 fbf208f）
- Workflow run：43222878724，SUCCESS。
- Release build：0 warning / 0 error。
- Tests：7 passed / 0 failed / 0 skipped。
- Lua runtime 实测：Lua 5.4、Lua 调用 C# 注册函数、instruction count hook 触发。
- win-x64 self-contained publish 成功。
- ZIP 上传成功，Actions artifact ID 13947200664。

### 产物复核
- ZIP：Anjian2026-0.1.0-preview.1-win-x64.zip
- 大小：51,879,713 bytes。
- SHA256：baceea2b0c0b2a63783851ff942e049a6d59a8a0a5a23e957a952966a78f4135。
- 已下载 Actions artifact 并检查内部 ZIP，276 个文件，主程序 EXE 与 NLua.dll 存在。
- sidecar SHA256 与实际 ZIP 计算结果一致。

### 限制与验收
- 本次 CI 和包结构验证通过，但尚无用户 Windows 实机验收证据。
- 不将 2 小时稳定性、复杂拖拽、混合 DPI、多显示器和所有权限目标标记 PASS。
- 当前不是完整 IDE，也没有可视化动作列表；录制结果直接生成 Lua。

### 当前任务
发布 GitHub prerelease v0.1.0-preview.1，并核验正式 Release 的 ZIP 和 SHA256 资产。


### Release 工作流修复（13:05 UTC）
- 首次 Release 工作流的构建、7 项测试、自包含发布、打包与上传全部成功；仅 PowerShell 输出文件步骤因误用 Set-Content -Append 失败。
- 已改用 Add-Content 写入 GITHUB_OUTPUT。
- 修复提交：21441470ca6f8de57df5cc04ba46359d2cb2b828。
- 修复提交 Actions run：43223670738，结论 SUCCESS；Release 条件步骤按预期跳过（该提交不是 release: 前缀）。
- 本次 release: 提交将触发 v0.1.0-preview.1 tag 和 prerelease 创建。


## 2026-11-12 — S002 — v0.1.0-preview.1 GitHub prerelease 正式发布

### 最终发布信息
- Release：<https://github.com/xiaobei206/anjian/releases/tag/v0.1.0-preview.1>
- Tag：`v0.1.0-preview.1`
- 发布提交：`cf24ac9292ea21dd1063ea9e9d65f18e3637442a`
- Release workflow run：`43224009158`，结论 SUCCESS。
- Assets：Windows x64 ZIP + SHA256 sidecar。
- ZIP 大小：51,879,710 bytes。
- SHA256：`80bfbf3ddbebafaf6e835bc8fb29148760662418b2c57de79f7ab7dbe88e6164`。
- GitHub Release API 中 ZIP asset digest 与 Actions artifact 内实际 ZIP 文件 sha256、sidecar 三方一致。
- Release ID：442961207，prerelease=true。

### PowerShell 发布修复
- 首次发布尝试中 PowerShell Set-Content 没有 -Append 参数，导致 output 变量步骤失败；构建、7 项测试和 ZIP 上传本身已成功。
- 改为 Add-Content 写入 GITHUB_OUTPUT，修复提交 Actions SUCCESS。
- 后续 release: 提交重新执行全流程，Read release version 和 Create GitHub Release 均 SUCCESS。

### 验收边界
- CI 编译和 7 项自动测试 PASS。
- 用户 Windows 实机手工验收、2 小时稳定性、多屏/DPI 与具体目标程序兼容仍为 NOT RUN。


## 2026-11-12 — S003 — 经典全局快捷键

### 用户需求
用户明确要求沿用按键精灵的熟悉快捷键，尤其需要能够在脚本运行时直接中止，不必切回主窗口。

### 实现
- 默认 F10 启动当前脚本、F11 暂停/继续、F12 中止当前脚本。
- 新增全局低级键盘钩子，并将控制消息投递到 WinForms 窗口消息队列。
- 仅截获物理按键；过滤 SendInput 等模拟注入事件，避免脚本自行发送功能键时误触发。
- 对按住功能键产生的自动重复进行抑制。
- 空闲时只接管 F10；运行或暂停时才接管 F11/F12；无对应动作时功能键正常传递给前台程序。
- 主工具栏、README、PRD、验收清单和用户手册补充快捷键说明。

### 提交
- 实现：`5ce20ee50542bed34dad86aae89c5dd045767803`
- 状态按需截获修正：`e8e96cb26df1a254d2db45dd2a72b2c7ef11cd9f`
- 文档：`55afa24e54d3614b9e25088fd65b0dc864ac117b`
- 最终代码提交 Windows Actions：待完成。

### 验证状态
- 初版代码 Windows CI：`43226021330`，SUCCESS（编译、7 项测试、自包含发布及 ZIP 打包均通过）。
- 状态按需截获修正版 Windows CI：待完成。
- Windows 10/11 实机全局快捷键、最小化状态、键鼠释放与冲突测试：NOT RUN，必须由用户实测。

### 版本安排
- 作为 v0.1.0-preview.2 的功能增量；preview.1 既有资产保持不变。


## 2026-11-12 — S003 — 全局热键与 v0.1.0-preview.2 发布完成

### 用户需求
用户明确提出脚本运行时必须有按键精灵熟悉的快捷键，避免无法快速中止。

### 实现
- F10：应用空闲且未录制时启动当前脚本。
- F11：脚本运行/暂停时切换暂停与继续。
- F12：脚本运行/暂停时立即中止并清理本程序持有的按键和鼠标按钮。
- 使用全局低级键盘钩子；过滤模拟注入按键，避免脚本自身发送 F10/F11/F12 导致误触发。
- 只在有对应动作时截获按键：空闲时 F12 仍可交给浏览器打开开发者工具；运行期间 F12 被用作中止键。
- 主界面、README、PRD、用户手册、验收清单、handoff 和 roadmap 已同步说明。

### Git 提交
- 快捷键实现：`5ce20ee50542bed34dad86aae89c5dd045767803`
- 状态按需截获修正：`e8e96cb26df1a254d2db45dd2a72b2c7ef11cd9f`
- 文档更新：`0bcff7373319a47cc4253309238f3603c7eab81d`
- preview.2 release：`3e63ff38d8111181fa1a402547350c80e6ceb882`

### CI 与发布
- 最终代码提交 Actions run：`43226322656`，SUCCESS。
- Release Actions run：`43226666516`，SUCCESS；Build、Test、Publish、Package、Artifact Upload、Read Release Version、Create GitHub Release 全部成功。
- 自动化测试：7 passed / 0 failed / 0 skipped。
- Release：<https://github.com/xiaobei206/anjian/releases/tag/v0.1.0-preview.2>
- ZIP：`Anjian2026-0.1.0-preview.2-win-x64.zip`，51,881,918 bytes。
- SHA256：`41139ffeefd695bce3e1f00f232fc8d2fc1cb7b34b67f8bfb53d68019c0bd089`。
- Sidecar 与实际 ZIP SHA256 已通过本地校验；Release asset ZIP digest 与文件 SHA 一致。

### 验收边界与下一步
- CI 编译、自动化测试、自包含发布及 Release 资产核验：PASS。
- 用户 Windows 10/11 实机 F10/F11/F12 热键测试：NOT RUN。
- 2 小时稳定性、混合 DPI、多显示器、复杂拖拽和不同权限级别目标程序兼容：仍 NOT RUN。
- 下一步仅优先做用户实机验收与反馈修复，不要擅自扩大首版范围。


## 2026-11-12 — S004 — 点击坐标录制、续录和紧凑悬浮窗（待 CI）

### 用户需求
- 可选只保留点击位置，忽略手部抖动带来的鼠标移动轨迹。
- 录制过程中可以暂停并继续，之前录制内容不清空。
- 默认紧凑悬浮窗，显示脚本名和实时鼠标坐标，支持完整模式切换与始终置顶。

### 本轮代码变更
- GlobalRecorder 增加 click-only 采集模式，忽略移动事件；生成代码按 MoveTo -> 可选 Delay -> Click 的顺序执行。
- 增加 Pause/Resume 录制会话，暂停时卸载全局钩子、闭合已按住输入，恢复时排除暂停时长。
- MainForm 增加紧凑/完整两种模式、拖动标题区、实时坐标、置顶切换、状态化录制按钮。
- 新增 AppSettings 持久化录制模式、置顶和窗口模式；默认只记录点击坐标、紧凑模式启动。
- 用户手册和验收清单增加对应说明与 B16-B18 测试项。

### 验证状态
- 本地工具链未执行 Windows 编译；等待 GitHub Actions Windows runner 验证。
- Windows 实机交互和 UI 布局：NOT RUN。
- 若 CI 通过，再将版本递增至 v0.1.0-preview.3 并发布；不覆盖 preview.2。


## 2026-11-12 — S004.1 — Windows CI 第一轮通过

- 功能提交 `659db9836c1ab51c7e8812958eb1fee7166fa9ad` 对应 Actions run `43231551625`：SUCCESS。
- Build、Test、self-contained publish、ZIP/SHA256 package、artifact upload 全部通过。
- 随后在紧凑悬浮窗顶部增加“只录点击”勾选，commit `3d79665418869487405e74c12d0f4308f404bbf1`，其 CI run `43231710764` 已通过 Build/Test/Publish/Package。
- 正在递增版本至 v0.1.0-preview.3，并由 Release workflow 对最终代码再次完整验证。


## 2026-11-12 — S004.2 — v0.1.0-preview.3 正式发布

### CI 与发布证据
- 首轮 release run `43232299395` 因 `MainForm.cs(492,55)` nullable warning-as-error 失败，未发布资产。
- 修复：录制期间恢复脚本列表选择时，对 `_current` 做 null 检查。修复提交 `2eacde0c12f9616e4f37d209c929c1b473edab43` 的 run `43232594517` SUCCESS。
- 最终 release commit：`3938b1550f32cb98511b67cfb5642e6eaf78f8fe`。
- Release run `43232898372` SUCCESS；Build/Test/Publish/Package/Artifact Upload/Read Release Version/Create GitHub Release 全部成功。
- 自动化测试 7/7；构建 0 warnings / 0 errors。

### 发布资产
- Release：https://github.com/xiaobei206/anjian/releases/tag/v0.1.0-preview.3
- ZIP：`Anjian2026-0.1.0-preview.3-win-x64.zip`，51,887,704 bytes。
- SHA256：`b18071d5ecfb05435198e5ee900e59cc861ef7706328b413bee2412650acf188`。
- 已从 Actions artifact 提取 ZIP 和 sidecar；`sha256sum -c` 输出 OK，GitHub asset digest 与实际文件 SHA256 一致。

### 验收边界
- CI / Release 资产：PASS。
- 用户 Windows 实机点击坐标录制、暂停续录、紧凑 UI、置顶与坐标刷新：NOT RUN。


## 2026-11-12 — S005 — 独立悬浮窗、系统托盘与应用图标（preview.4 已发布）

### 用户反馈与产品决策
- preview.3 的“悬浮模式/完整模式”其实是同一个 MainForm 中切换面板，悬浮窗不独立；完整模式会让悬浮面板消失。
- 确认悬浮窗与完整主窗口必须是两个独立顶层窗口，可同时显示并共享同一状态。
- 主窗口关闭或点“返回悬浮窗”时应隐藏主窗口、移除任务栏按钮，应用继续由托盘图标维持。
- 托盘菜单可打开主窗口、显示/隐藏悬浮窗和退出程序；悬浮窗关闭只隐藏窗口。
- 悬浮窗直接提供循环次数和轮间隔输入，与主窗口实时同步。
- 用户要求更新应用图标，且图标必须应用于 EXE 和托盘。

### 已落地代码
- 新增独立 `FloatingForm`；主窗口保留脚本列表、Lua 编辑器、日志和完整设置。
- 启动时隐藏主窗口（ShowInTaskbar=false、Opacity=0），默认显示独立悬浮窗。
- 完整界面按钮仅显示主窗口，不关闭悬浮窗；主窗口“返回悬浮窗”和右上角关闭都只隐藏主窗口。
- 新增 NotifyIcon 和托盘菜单；显式“退出按键精灵 2026”才关闭两个窗口、释放 Hook/播放资源。
- 悬浮窗增加循环次数和轮间隔控件，并与主窗口 NumericUpDown 双向同步。
- 自定义多尺寸 ICO 已提交到 `src/Anjian.App/Resources/Anjian2026.ico`；`ApplicationIcon` 已嵌入 EXE，主窗体、悬浮窗和 NotifyIcon 使用同一图标。
- Windows CI run `43252971971` 和最新代码 run `43253075450` 均 SUCCESS；Restore/Build/7 项测试/Publish/ZIP/SHA256/Artifact upload 全部完成。
- Release：<https://github.com/xiaobei206/anjian/releases/tag/v0.1.0-preview.4>。
- Release workflow run `43253611158`：SUCCESS；Restore/Build/7 项测试/Publish/ZIP/SHA256/Upload/Release creation 全部成功。
- ZIP：`Anjian2026-0.1.0-preview.4-win-x64.zip`，51,910,925 bytes；SHA256 `6b59ab27f825aa48b2ec5343cec826ad8b0ded759e9989af02d51f8b949f09aa`。
- Windows 10/11 实机窗口、托盘和图标验收仍为 NOT RUN。

### 最终验证与发布结论
- 代码/构建/测试/便携发布/ZIP/SHA256/Release：PASS。
- Release workflow run：`43253611158`，SUCCESS。
- Release：<https://github.com/xiaobei206/anjian/releases/tag/v0.1.0-preview.4>
- ZIP：`Anjian2026-0.1.0-preview.4-win-x64.zip`，51,910,925 bytes。
- ZIP SHA256：`6b59ab27f825aa48b2ec5343cec826ad8b0ded759e9989af02d51f8b949f09aa`。
- Windows 10/11 实机窗口、托盘、图标、Hook、长时间稳定性验收：NOT RUN。


## 2026-11-13 — S006 — 完整产品目标冻结与函数手册建立

### 用户要求
- 将“按键精灵 2026：完整产品目标”正式写全，不能持续把完整 IDE、窗口相对坐标、图色/OCR、插件和小精灵打包留作未定义的远期想法。
- 正式建立可随软件交付的函数/参数说明书。
- 规格更新后继续接力开发，不重复讨论已经确认的产品范围。

### 已完成文档变更
- 重写 docs/PRD-CN.md：明确完整 Windows 桌面自动化开发环境的最终范围、用户流程、质量目标与 1.0.0 验收门槛。
- 新建 docs/FEATURE-MATRIX-CN.md：逐项记录功能目标、preview.4 现状、开发阶段和最小验收条件。
- 新建 docs/FUNCTION-REFERENCE-CN.md：建立中文函数/参数参考手册初版；区分当前 API 与规划 API，记录参数、默认值、范围、返回行为及限制。
- 更新 docs/LUA-API-CN.md：区分当前 screen 坐标与完整产品目标坐标空间。
- 重写 docs/ROADMAP-CN.md：M0–M14 按依赖阶段实施；小精灵、窗口/坐标、图色/OCR、插件和 API 文档都有明确里程碑。
- 更新 docs/ARCHITECTURE-CN.md：定义 API Catalog、文档生成、坐标/窗口、图像、参数、小精灵和插件等目标模块边界。
- 更新 AGENTS.md、README.md、USER-GUIDE-CN.md、ACCEPTANCE-TEST-CN.md、DECISIONS-CN.md 和 SESSION-START-PROMPT-CN.md。
- 统一 Click/KeyPress 单次按下持续时间上限为 600000 毫秒，修正旧决策记录与 API 规格不一致的问题。
- 冻结 API 注册表单一事实来源原则：运行时绑定、IDE 补全/参数提示、命令面板和函数手册必须使用同一注册描述。

### 核心产品决定
- 最终目标为 1.0.0 完整桌面自动化开发环境，不以 preview.4 现有功能作为完整产品边界。
- “小精灵”必须是真正可独立运行、含参数 UI、资源依赖和校验信息的打包交付物，不等同于保存 .lua 或压缩工程目录。
- 公开函数必须有稳定签名、参数类型/默认值/范围、返回值/错误、坐标及取消语义、示例、版本和状态；计划函数不能标为当前可用。
- 旧版 Q 语言/旧插件二进制完全兼容、移动端、云账号/商城、远程群控、内核驱动与安全绕过不作为目标。

### 验证状态
- 文档由 GitHub Contents API 写入 main；所有新增/更新文档已逐文件提交。
- 本轮只变更文档，没有修改应用代码；应用 build/test：NOT RUN。
- 当前 preview.4 的 7 项自动化测试和 Release workflow 既有结果未因本轮文档更新而重新执行。
- Windows 10/11 实机窗口、托盘、Hook、多显示器、DPI 与 2 小时稳定性：仍为 NOT RUN。
- 功能矩阵只把有代码/测试证据的能力标为已实现，且明确区分 CI 与桌面实机验收。

### 本轮主要文档提交
- PRD：2cf66f1cdf6427678ec77c95bbe5594c9e6047df
- 功能矩阵：4b55796663c6a3ca2bd5619a9ff6d44162cee873
- 函数/参数手册：05ce583928909f7e5af1a81155149e3889bd4264
- Lua API：7e60ba32ea1e921b1b44c70a7e321538a128de93
- 路线图：f88bcfbabdcf39f2f9cd45545750cea777988a6d
- 架构目标：e7fc51e65aa938e03fe51d394cff596721869280
- README：db84685c876b3c417ee30d36991134cf3546f383
- 协作规则：46d23a61f0df82c20dc4d057d5bf74a81fb85dab
- 决策记录：570777bd70d12aea8f03e02d104273404964e60f
- 会话启动提示：b0627a66b70fe44ad48605c41765de75e090fef7
- 用户手册：aa33c3a89ad348dd4cf5e60667da4e0b4c365c3e
- 验收清单：359abc424b385b1af72a5dc926e5b7153804f2d6

### 下一会话唯一任务
M2：实现单一机器可读 API 注册表，并使函数/参数手册可以自动生成/一致性校验。先盘点当前真实 Lua 绑定；不要因计划手册列出了候选函数就添加空壳或宣称功能可用。用自动化测试核对公开函数、注册表、参数默认值和生成文档。


## 2026-11-13 — M2 — 单一机器可读 API 注册表与手册生成（Windows CI 已通过）

### 真实实现盘点
- `WorkerProgram.cs` 当前向脚本公开 22 个全局函数；`__MoveTo` 等属于内部 IPC 桥接，不是用户 API。
- `MoveTo(x, y)` 保持虚拟桌面绝对物理像素语义，副屏负坐标合法。
- 发现 `MouseWheel(delta)` 旧实现会截断小数，且 `Hotkey()` 允许空参数无动作。本轮改为拒绝非整数滚轮增量，且组合键至少需要一个按键。

### 实现内容
- 新增嵌入式注册表 `src/Anjian.Core/Api/lua-api-registry.json`，记录 22 个真实 API 的稳定 ID、分类、签名、参数顺序/类型/必填/默认值/范围/单位/枚举、返回值、错误、执行/取消/坐标/权限语义、示例、状态和引入版本。
- Core 新增注册表模型、校验器和运行时 binder；worker 不再独立维护 API 绑定表。公开包装函数及内部桥接绑定由注册表驱动，内部桥接全局随后隐藏。
- 把 Lua bridge 与窄 IPC 接口移入 Core，以记录型管道替身测试真实 NLua 调用，不会发送真实键鼠输入。
- 新增 `tools/Anjian.ApiDocsGenerator` 的 `--write`/`--check` 命令；函数手册第 2 节生成式维护，规划函数仍位于目标设计章节。
- 新增公开函数、运行时全局、桥接隐藏、默认参数、600000ms 边界、MouseWheel 整数契约、Hotkey 空参数和手册漂移测试；Windows workflow 增加生成文档一致性检查。

### Windows CI 验证结果（已完成）
- M2 实现提交：`0f7a80c24f18789833d7fbb92e4979da65ed261d`；当前验证的最新主线代码基线：`fdff7c14398485fa30725e3ea283c03a545c97a6`。
- 首次 CI run `43271693389` 在 Build 阶段失败：新增测试的 tuple nullable 类型与 xUnit 期望类型不一致（CS8620）；后续已修正。
- run `43272945064` 的 Build 通过，但 `LuaCallsAndDefaultsMatchCatalog` 发现中文 TypeText 测试字符串被解码为问号。提交 `ac034aa10b1c8b1acae25a297d4e20928d9fe7f4` 明确设置 `lua.State.Encoding=Encoding.UTF8` 后，后续 Windows CI 通过。
- 最新主线 Actions run：`43273917056`，SUCCESS（<https://github.com/xiaobei206/anjian/actions/runs/43273917056>）。
- 最新成功运行：Restore PASS；Release Build PASS（0 errors）；自动化测试 **12 passed / 0 failed / 0 skipped**；`Anjian.ApiDocsGenerator --check` PASS；self-contained win-x64 publish PASS；ZIP/SHA256 package PASS；artifact upload PASS。
- 注册表当前包含 22 个真实已实现的公开 Lua 函数。测试覆盖公开函数清单、运行时 Lua 全局/内部桥接隐藏、函数调用与默认值/边界、Hotkey 和 MouseWheel 参数契约、Unicode 文本传输以及手册生成区一致性。
- 当前 push 不是 `release:` 提交，故发布专用的 Release version / Create Release 步骤按条件跳过；未创建新版本或覆盖 preview.4。
- 本地执行环境没有 .NET SDK（`dotnet --info`：command not found），本地 Restore/Build/Test 为 NOT RUN；构建和测试结果来自 Windows GitHub Actions runner，不冒称本地通过。
- Windows 10/11 实机窗口/托盘/Hook、DPI/多显示器、复杂拖拽与 2 小时稳定性验收仍为 NOT RUN。CI 不代替 Windows 桌面手工验收。

### 下一步
M2 已 DONE。下一开发任务为 M3：完整 Lua IDE 基础。编辑器补全、参数提示和命令面板必须消费 `LuaApiRegistry.Load()` 提供的数据；禁止在 UI 层维护第二份 API 清单。M4 调试器独立排期。


## 2026-11-13 — M3 — 完整 Lua IDE 基础（Windows CI 已通过）

### 实现内容
- 新增 `Anjian.Core.Editor.LuaEditorService`：Lua token 识别、注释/字符串上下文判断、脚本标识符前缀提取、注册表 API 候选、嵌套调用参数/签名提示和语法编译诊断。
- 语法诊断通过 NLua `LoadString` 加载/编译 Lua chunk，不执行脚本语句；因此检查语法不会触发 `Log`、键盘或鼠标动作。解析错误包含行号时返回诊断行。
- 新增 WinForms `LuaEditorControl`：语法颜色、行号 gutter、注册表驱动的自动补全列表、Ctrl+Space、Enter/Tab 选择、Escape 关闭和活动参数签名提示。
- 编辑器词法扫描移入后台任务；文本版本号用于丢弃过期结果，避免旧的高亮计算覆盖用户后续输入。长文件的签名帮助仅分析光标前有限上下文。
- 新增 `ScriptSearchReplaceForm`：查找下一个（循环）、替换当前、全部替换和区分大小写。
- `MainForm` 接入多标签。切换标签自动保存，关闭标签保存，已有脚本重新打开时恢复对应编辑器；录制期间禁止切换/关闭活动编辑器标签。
- 增加语法检查工具入口：显示诊断、将光标导航到首个错误行/列；语法通过与失败的日志都明确说明只检查语法，不运行脚本。
- 新增编辑器 Core 单元测试：token 分类和 API 识别、注册表补全边界、嵌套函数/表构造器签名提示、只编译不执行、错误行解析、注释/字符串上下文、标识符前缀和约 1MB 输入的词法扫描。
- API 补全、签名帮助、API 颜色只使用 `LuaApiRegistry.Load()` 的 `Categories` / `Apis`；未把计划中的 `FindWindow` 等目标设计 API 伪装成当前可调用函数。没有接入独立 API 命令面板；调试器保留在 M4。

### Windows CI 验证
- M3 最终代码验收基线：`71fd493cad0fbed1451a4ffa46192752a18bd0a6`。
- Windows Actions run `43282795272`：**SUCCESS**（<https://github.com/xiaobei206/anjian/actions/runs/43282795272>）。
- Restore：PASS。
- Release Build：PASS，0 warnings / 0 errors。
- 自动化测试：**20 passed / 0 failed / 0 skipped**。
- `Anjian.ApiDocsGenerator --check`：PASS。
- self-contained win-x64 publish：PASS；ZIP/SHA256 package：PASS；Actions Artifact upload：PASS。
- 早期自动化验证曾出现 1 项签名提示断言失败。检查 Windows runner 日志后确认测试夹具中的字符串逗号位于光标之前，实际对应第三个外层参数；修正测试夹具后，又补强 `()` / `[]` / `{}` 嵌套深度，并增加表构造器逗号回归。最终 Actions run `43282795272` 的 20 项测试全部通过；Build 为 0 warnings / 0 errors。
- 当前执行容器没有 .NET SDK，且无法在本容器执行 Windows WinForms 桌面测试；本地 Restore/Build/Test：NOT RUN。以上成功结果仅来自 Windows GitHub Actions runner。
- 本轮没有创建 Release；已发布的 `v0.1.0-preview.4` 文件和 SHA256 没有更新。M3 功能目前只在 main 未发布代码中。

### 手工验收与未完成范围
- Windows 桌面手工验收：**NOT RUN**。尚未在用户 Windows 10/11 机器上实测多标签、中文/日文输入法、1MB 文件交互、查找替换、补全和签名提示、诊断错误跳转及 100–200% DPI。
- F04 仍为“部分实现”：完整目标中的代码折叠未在 M3 交付。
- F06 独立 API 命令面板仍未实现；编辑器自动补全不等于命令面板。
- M1 preview.4 的窗口/托盘/Hook、DPI/多屏和 2 小时稳定性实机验收仍为 NOT RUN。
- 断点/单步/运行到光标、变量窗和调用栈未实现，明确留给 M4。

### 关键实现提交
- Core 语言服务：`6000813f2a86c1d23e8997ef391c5e3af57289d8`。
- WinForms 编辑器控件：`fff758d58f8066ed206d483270026d74ff59c882`。
- 查找替换窗口：`14aef3017159d7d0823d0a82add4875fbce1a526`。
- 主窗口多标签/诊断接入：`0090450ffe275323e28cc46f8fcd79dbe21ecab7`。
- 原签名测试夹具修正：`71555d30815a60f96ef0708fe6c46f4cc0a16253`。
- 嵌套表构造器签名解析修复：`17ecde69672694354ccac2c9f3d7e096e8bb5b0b`；回归测试：`71fd493cad0fbed1451a4ffa46192752a18bd0a6`。

### 下一步
M3 已 DONE。下一会话唯一任务为 M4：Lua 调试器基础。必须实现真实断点、单步、运行到光标、变量观察和调用栈，禁止仅用日志显示冒充调试。M1 Windows 实机验收作为独立未完成事项继续保留，不阻塞 M4。


## 2026-11-13 — M4 — Lua 调试器基础（Windows CI PASS；桌面验收 NOT RUN）

### 实现内容
- 新增 Core 调试模型与会话：LuaDebugBreakpoint / LaunchOptions / Command / Variable / Frame / Snapshot；使用 NLua 1.7.9 的 Lua Line + Count hook，断点在对应行执行前暂停。
- 断点匹配规范化后的源码文件路径和 1-based 行号；支持 Continue、Step Into、Step Over、Step Out 和 Run To Cursor。Step Over / Out 基于当前 Lua 调用栈深度判断，不用固定延时或伪造行号。
- 暂停时通过 KeraLua GetStack / GetInfo / GetLocal 读取真实栈帧与局部值，并读取 _G 中可读全局值；只把名称/类型/格式化文本快照发出，不把 LuaTable/LuaFunction 等对象暴露给 UI。
- 快照限制：最多 32 个调用栈帧、每帧 96 个局部变量、100 个全局变量、字符串最多 512 个字符；table/function/thread/userdata 不展开并显示原因；不执行用户输入的 watch 表达式。
- Worker 在启动握手中接收调试配置，暂停时通过同一命名管道发送 JSON 快照并等待 JSON 调试命令；Lua state 始终留在 worker 子进程/执行线程。
- PlaybackRunner 增加调试挂起、命令提交、停止取消与结束事件；保留普通播放原有 Count hook、暂停检查点、Stop 杀 worker 以及 ReleaseAllOwnedInputs。
- WinForms 编辑器 gutter 提供断点圆点和当前执行行标记；主窗口新增调试按钮、调用栈和局部/全局变量面板。
- 新增 LuaDebugSessionTests：断点命中行、嵌套调用 Step Into/Over/Out 顺序、Run To Cursor、局部/全局变量快照、table 不展开和 DebugHook 清理。

### 关键实现提交（均直接提交 main）
- Core 调试会话：067f2d53f425bfd362b0581ff51feaff78c4befa。
- Core 调试自动测试：7655d136f1cc8c1a5402171074c7a4cfa1a5ebf9。
- Worker 调试握手：bfd5a2a2596e892abf2590db97b8060626c1127c。
- Runner 命令/暂停 IPC：92bf7c6e11505836feaeaf1f6ff3d8268800a690。
- IPC 消息 DTO：de0454980ab425c3edc893c833355bde420d86ad。
- 编辑器断点 gutter：3e1e9fcc017f844321996f7ad9870744967273c2。
- 主窗口调试面板：f34daf3c40374c6e72e71eeb0afbc54198cbecc9。
- 调试工具栏状态/变量显示修正：128d51d5472604b7aef5f6b7d49ce5f3b8c5c933、7e7b93a5abf42ac43d4e1882215e5b4d21a7b859。
- 单步嵌套测试和空脚本边界修正：46e1f21ef15644e54e6be3b0d3e4953755e3a0b7、7cdceee2884ad9c38aeb006dfdf99406dac2b86f。
- CI 命名冲突修复：66eebe01cd73ab921438a4bde507af045f514c5e。
- Lua 行号预期 / xUnit Assert.Single analyzer 修复：de3fcf78c2e7cd1d63c77fc24d26517164d052e0、edac117255cefbad94874b0c34329325e7c41678。
- M4 功能矩阵/架构/验收/路线图/用户指南文档提交：e3aacc49c5010093b42aefc5769a497ecb9a31da、bc7f39d2a961dcb2a25feaa02c7bb97d0f407039、0f8738c986afb8620eb26204ca16b26307663575、f58e54df72c12329a8a8a0002710b4b80fb71982、d73c6474682ad9e321bb14386fdc1663a1e9555d。

### Windows CI 反馈与当前状态
- Actions run 43289601917 首次编译反馈 Core 代码中 LuaFunction 同时来自 KeraLua 与 NLua；以 NLua.LuaFunction 限定后，Core 和 App 项目可继续编译。
- Actions run 43290120386 的 Restore PASS，Core/App 编译通过，但测试项目 Build 被 xUnit2031 analyzer 阻断：两处 Assert.Single(collection.Where(...)) 必须使用 Assert.Single(collection, predicate)；已在 commit edac117255cefbad94874b0c34329325e7c41678 修复。
- 修复后 Actions run 43290603954 / 43290697650 的 Restore/Build 均 PASS（0 warnings / 0 errors），但初版单步测试夹具未在 Continue 后设置后续断点，导致 24 passed / 1 failed。该失败是测试未提供下一步控制停点，并非运行时编译问题；随后在 commit ca56ef49feb531ab314b7ac446bddf1f99dc8fb5 将真实断点加在外层函数调用行，再按 Step Into / Step Over / Step Out 验证完整停点序列。
- 本轮本地执行容器没有 .NET SDK（dotnet command not found），本地 Restore/Build/Test：NOT RUN。真实构建和测试由 Windows GitHub Actions 执行。
- 已在 commit ca56ef49feb531ab314b7ac446bddf1f99dc8fb5 将嵌套步进测试修正为在 main 脚本第 9 行设置真实断点，之后依次执行 Step Into / Step Over / Step Out，并验证最终停点为外层函数后及主脚本后续行。Actions run 43291044845：SUCCESS（Restore / Build / Test / API check / win-x64 publish / ZIP+SHA256 / artifact upload 全部 PASS）；25 passed / 0 failed / 0 skipped。
- 最终主线验证：commit ab90b96f59deb173874c8aba819a9fc95288f8cf 对应 Windows Actions run 43291175992：SUCCESS。Restore PASS；Release Build 0 warnings / 0 errors；自动化测试 25 passed / 0 failed / 0 skipped；API handbook --check PASS；self-contained win-x64 publish PASS；ZIP/SHA256 PASS；artifact upload PASS。SHA256：1d37a3210e22646fd781a2803a17f40058d97f5c1e16845cef7d5d24059af8c4；Artifact ID 13976519155，51,794,098 bytes。Release version / Create Release 因本轮非 release 提交而跳过；preview.4 资产未改变。
- Windows 桌面手工验收尚未执行：M1 preview.4 窗口/托盘/Hook/DPI/多屏/长时稳定性、M3 编辑器交互、M4 断点/变量面板均为 NOT RUN。CI 不代表桌面实机验收。
- 本轮未创建新 Release；v0.1.0-preview.4 的 Release/ZIP/SHA256 不变。
- 当前 M4 只提供基础 scalar snapshot；断点不跨关闭标签/重启持久化，watch 表达式和表展开未实现。功能矩阵 F05 仍标记“部分实现”，不得描述为完整调试器交付。

### 下一步
M4 基础调试器与 M5 可视化录制/动作列表均已完成 Windows CI 验收。下一会话唯一任务切换为 M6：坐标系统与窗口自动化（详见 SESSION-HANDOFF）。M1、M3、M4、M5 的 Windows 桌面实机验收仍分别保留为 NOT RUN。

 
## 2026-11-13 — M5 — 可视化录制/动作列表（Windows CI 全部 PASS，桌面验收 NOT RUN）

### 实现内容
- 在 Anjian.Core/Recorder 新增 RecorderDocument、RecorderRawEvent、RecorderAction、RecorderDocumentBuilder、RecorderDocumentStore 和 RecorderLuaGenerator；原始事件与结构化动作分层，保留 timestamp/source/flags/坐标/RawEventIds/动作 ID、holdMs、delayMs、clickOnly 和 droppedEventCount。
- 转换只折叠时间排序后相邻、同名且有效的 Down/Up；在拖动移动或重叠按键中保留原始 Down/Up 顺序，不能配对的事件不静默丢弃。鼠标 Hook 的既有合并、暂停/续录、click-only、注入过滤保持不变。
- 主窗口增加“动作列表”入口；WinForms 动作表采用 DataGridView VirtualMode，支持插入/删除/复制/排序、启用状态、参数/延时、分组/注释和批量延时；显示动作 ID、事件来源和原始事件引用。
- Lua generated block 有 ID/schema 标记与每动作 actionId 注释；保留区块外用户代码；手工修改和删除区块分别标记 Detached/Missing，预览+确认后才替换/补回；支持从动作表定位到生成代码。哈希统一换行符，避免 CRLF/LF 误报。
- sidecar 为 脚本名.actions.json（main.lua -> main.actions.json），支持 schema 检查、临时文件+flush、备份、改名/复制/删除联动。不兼容 schema 不覆盖。
- 步骤试运行复用现有 PlaybackRunner/Worker、停止清理路径；单步风险确认、时长上限 60 秒、文本长度上限 1000 字符；超长动作间隔有 7 天上限并以有作用域循环生成，控制 Lua 区块体积。
- 新增 RecorderDocumentTests.cs 覆盖 builder、同步、持久化、Lua 编译执行、路径、安全边界；不新增、不改变公开 Lua API，因此函数手册与注册表无需更新。

### 关键提交（均直接提交 main）
- M5 功能主体：eedc1f89329e2ebfbf9ccadb54b529c954fe248d。
- sidecar 名称修正：2093aa24c4cf49b89c902c164171ae6e8835daec。
- CI 首轮编译反馈修正（代码导航字符字面量、测试临时目录生命周期）：ec06cf1d3a0e847267b9de251d5a6ff26c710c4f。

### Windows CI 与实机验收
- 首轮 Windows CI（commit eedc1f8, run 43295605802）：Restore PASS；Release Build FAIL，0 warning / 3 errors；错误为 MainForm 字符字面量 CS1012 两项、测试将 DirectoryInfo 作为 IDisposable 的 CS1674 一项。此失败已在 ec06cf1 中修正，不能隐藏这次失败记录。
- sidecar 文件名修正后的 run 43295786937 仍因前述两处 CS1012 与一处 CS1674 失败；修复后的最终提交 run 43295924472 SUCCESS。Restore PASS；Release Build 0 warnings/0 errors；37 passed / 0 failed / 0 skipped；API handbook --check 22 个公开函数 PASS；win-x64 publish、ZIP/SHA256 与 artifact upload PASS。Artifact ID 13979855824（51,827,155 bytes）。内部 ZIP SHA256：ddc08cfd78503d9e5e5c7995ff1202e3bc36abfcf1de6fa3ae9cc35a5baf531f。CI runner 为 Windows Server 2025，不是用户桌面实机。
- 本地执行容器没有 .NET SDK（dotnet --info 返回 command not found），本地 Restore/Build/Test：NOT RUN；Windows GitHub Actions 是实际构建/测试环境。
- M1 Hook 实机可靠性，以及 M3/M4/M5 WinForms 桌面手工验收均 NOT RUN。Windows CI 不代表实机验收通过。

## 2026-11-13 — M6 — 坐标系统与窗口自动化（代码已提交 main；完整 CI 待文档同步复测）

### 实现内容
- Core 新增 CoordinateSpace / WindowAnchor / PixelRect / WindowGeometry / CoordinateConverter：screen/window/client/anchor/ratio 五种空间、双向屏幕换算、九宫格锚点、客户区 ratio、物理像素取整及显示器工作区对齐算法。window 外框取 Win32 GetWindowRect（含标题栏/非客户区）；client 先经 ClientToScreen 取物理屏幕原点。
- 保持旧 MoveTo(x, y) 完全不变：仍为虚拟桌面绝对物理像素，副屏负坐标合法；不把窗口相对坐标隐式注入旧 API。
- 新增 WindowsWindowManager：顶层及全部子窗口后代枚举；标题包含、类名精确、进程名、PID、可见性筛选；元数据包括标题/类名/进程/外框/客户区/可见/最小化/最大化/前台/焦点。
- 窗口操作支持激活、显示/隐藏、最小化/最大化/还原、置顶、Move、Resize、工作区九宫格 Align、等待出现/等待关闭；Find/Wait 命中返回表，Wait 超时 nil，WaitClosed 超时 false，Stop 可取消等待。
- Worker IPC 对窗口命令使用 JSON；Lua API 返回 Lua 表而非裸 HWND。随机 GUID 引用只在本轮脚本运行有效；每轮 worker 结束清空引用。WinEvent destroy hook 标记句柄销毁，每次使用还校验 IsWindow、PID、线程 ID、类名与进程启动时间；hook 不可安装时拒绝引用，失效引用抛错。不绕过 UIPI/UAC/安全桌面。
- API 注册表新增 18 项；公开 Lua API 从 22 项增至 40 项，API version 从 0.2 升到 0.3；原有 API 定义不变。新增 CoordinateSystemTests 和窗口 Lua API contract test。
- 新增 M6-01 至 M6-10 桌面验收清单；当前全部 NOT RUN。

### 提交与 Windows CI
- M6 代码主体提交 main：3479491d3e9f3c16dcab8f78399a5811f73f9fe1（feat: add coordinate and window automation APIs）。
- Windows Actions run 43299445160：[run 43299445160](https://github.com/xiaobei206/anjian/actions/runs/43299445160)。Restore PASS；Release Build PASS（0 warnings / 0 errors）；Core Tests 60 passed / 1 failed / 0 skipped。失败项唯一为 ManualGeneratedRegionMatchesCatalog：代码 registry 已登记 40 个函数，而代码提交时文档的生成区域还保留 22 个函数；这是在提交代码后单独同步文档之前发生的预期文档漂移。API handbook --check、win-x64 publish、ZIP/SHA256 和 artifact upload 由于 Test 失败被 workflow 跳过；手册同步后必须再跑完整 CI。
- 上述 run 未报告坐标纯算法或 Lua API contract test 失败；60 项测试通过。但这不能证明 Win32 窗口管理的桌面行为正确。
- 本地容器没有 .NET SDK，Restore/Build/Test：NOT RUN。Windows 桌面 M6-01..M6-10：NOT RUN；M1 Hook 与 M3/M4/M5 的桌面验收状态不变。
- 未更新 VERSION/csproj 预览版本，未创建 Release；v0.1.0-preview.4 官方资产保持不变。


### M6 代码审查修正与最终验收（追加记录）
- 子窗口枚举会返回 WS_CHILD 后代；Windows 的 SetWindowPos 对子窗口解释的是父客户区坐标，而本项目公开 `WindowMove` / `WindowAlign` 契约必须始终是虚拟桌面屏幕物理像素。commit `c7cc5f7b2ee435ec13ece0846cf4ee5e64e03db5` 增加 `WindowInfo.isChild`，在 Win32 调用边界把子窗口的屏幕目标位置用 `ScreenToClient(parent)` 转换为父客户区坐标；`WindowResize` 保留既有屏幕位置。Lua contract 测试也覆盖 isChild 字段。
- 对 `IsCurrent` 增加 Win32 句柄在校验期间销毁、进程退出竞态的异常收敛：`Win32Exception` / `ArgumentException` / `InvalidOperationException` 被作为引用失效返回；WaitClosed 不会因为查询与销毁并发而将关闭误报成普通脚本异常。commit `4f9b2b2648fb154353ca55522e9c77fe77645506`。
- 修正注册表 `WindowActivate` 返回契约：Windows 前台锁定策略拒绝时实际返回 false，不描述为必然抛错；`WindowGetInfo` 手册补入 isChild；函数参考手册重新从 registry 生成。
- run `43299954437` 对代码+首次手册同步 commit 通过：61/61 tests；run `43300453577` 对子窗口坐标修正通过：61/61 tests、0 warning / 0 error、API handbook / publish / package / artifact 全 PASS。
- **最终验收基线 commit `4f9b2b2648fb154353ca55522e9c77fe77645506`，Windows Actions run `43300620614` SUCCESS**：Restore PASS；Release Build 0 warnings / 0 errors；61 passed / 0 failed / 0 skipped；API handbook --check PASS；self-contained win-x64 publish PASS；ZIP/SHA256 package PASS；artifact upload PASS。Artifact ID `13981497112`，51,857,145 bytes；GitHub Actions artifact digest `f46eaaf66ee204c82129902e280eed7a80c7c9a4a86c825888729af5280143f3`。
- `Read release version` / `Create GitHub Release` 按非 release commit 预期跳过；无新 Release，VERSION、preview.4 Release ZIP 和 SHA256 未变化。
- M6-01..M6-10 Windows 桌面验收仍全部 NOT RUN；M1 Hook 与 M3/M4/M5 桌面验收仍 NOT RUN。CI 不等于桌面实机通过。
- M5 sidecar schemaVersion 1 的坐标字段仍固定为 `virtual-desktop-physical-pixels`，动作生成继续用 `MoveTo`。持久窗口选择器和相对窗口坐标录制/回放重定位尚未实现；这是明确保留的产品缺口，不纳入本次 M6 API/Win32 交付完成声明。


## 2026-11-13 — M7 — 控件自动化与前后台能力适配（代码与 CI 完成；桌面实机待验）

### 实现内容
- API registry 从 0.3 升至 0.4；新增 14 项公开 API，合计 54 项。函数参考手册从同一 registry 生成，Lua API/架构/用户指南/Feature Matrix/Acceptance/Roadmap/README/handoff 同步。
- 新增 UI Automation Control View 遍历；能力探测当前控件的 Value、Text、Selection、SelectionItem、Toggle、Invoke Pattern；实现对应读取/设置/选择/切换/调用 API。限制控件遍历深度 16 层、单次结果 512 项、每绑定缓存最多 4096 个控件引用；控件失效后需重新枚举。
- 密码字段的 Value/Text 读取被拒绝。只读/不支持的 Pattern 明确报错，不假报成功。
- UIA/COM AutomationElement 缓存放在每轮独立 Lua worker。主进程通过随机命名管道发送短期窗口目标描述，Lua 脚本只拿随机 binding/control ID；worker 每次控件操作前再次通过 M6 不透明引用向主进程确认窗口 identity。Stop 通过终止 worker 处理卡在 UIA/COM provider 中的情况。
- WindowInput 留在主进程使用 SendInput，仅 auto/foreground 两个后端；校验可见/非最小化/前台、键盘焦点及客户区。通用后台鼠标/键盘/文本注入明确 unsupported；不自动抢焦点，不伪造 PostMessage 输入，不绕过 UIPI/UAC/安全桌面。
- 增加 `--automation-probe` WinForms 测试目标，包括文本框、密码框、复选框、组合框、列表和按钮，用于 M7-01..M7-10 的实机验证。

### Windows CI
- 初次 worker-provider Build run `43350032217` 失败，原因是缺少 JSON 导入、能力结果变量名错误和窗口类名读取函数缺失；修复已提交，未隐藏失败记录。
- 最终代码验收 commit `7624763610ae83a74d73b71c048e179a98f0cd6f`，run `43350412493` SUCCESS：Restore PASS；Build 0 warnings / 0 errors；64 passed / 0 failed / 0 skipped；API handbook check PASS；self-contained win-x64 publish PASS；ZIP/SHA256 与 artifact upload PASS。Artifact ID `14003911442`，76,394,920 bytes，SHA256 `2054fcea5cde979d1edb59d884f932a2d5f7d8da4e3ac2cfe97c12cd713b047b`。
- 同步函数手册/架构/用户指南等的 commit `b9110f03e105a30f0d7609a66b7415a54474d9f3`，run `43350563985` SUCCESS：64 tests、无 warning/error、API check、publish、package/upload 全 PASS。
- 状态更新 commit `52ef72957bb0202b116e8459261604bf2dc1da6d` 已把 M7 标记为代码/CI 范围 DONE；run `43350753487` 生成本日志更新时仍在运行，必须在下一次接手时核对最终状态和最新 HEAD。
- M7-01..M7-10 Windows 桌面人工验收仍全部 NOT RUN；CI 不代表 UIA Pattern、焦点权限、后台语义操作或 Stop 响应已经实机通过。
- 未升级 VERSION/csproj 预览版本、未创建 Release；v0.1.0-preview.4 正式 Release/ZIP/SHA256 未变化。

## 2026-11-13 — M8 — 截图、图色与模板识别核心 API（CI PASS；阶段仍 IN_PROGRESS）

### 实现与 API
- 新增 Anjian.Core.Imaging.PixelImage、ImageSearchEngine、图像请求 DTO 和纯算法测试；新增 Windows 侧 WindowsImageService，使用 GDI+ CopyFromScreen 获取当前桌面可见合成画面，资源由每轮脚本执行拥有，停止/异常/轮次结束统一清理。
- 新增 20 个公开 Lua 函数：CaptureScreen、CaptureWindow、CaptureClient、CaptureRegion、LoadImage、SaveScreenshot、ReleaseImage、GetImageSize、CropImage、GetPixelColor、CompareColor、FindColor、FindMultiColor、CountColors、FindImage、FindImages、WaitImage、WaitImageGone、WaitColor、WaitColorGone。registry 从 0.4 升至 0.5，共 74 项；函数手册从同一 registry 生成。
- 图片尺寸/资源界限：每张最多 32,000,000 像素；每轮最多 128 个资源、256 MiB 缓存；模板搜索最多 50,000,000 次像素比较。RGB 通道容差 0–255；多点模式最多 256 个偏移点；模板相似度 0–1、灰度开关、0.25–4 倍最近邻缩放；多结果最多 1000；等待 timeout 0–600000ms、interval 10–5000ms、stableCount 1–20。
- 支持截图 screen/window/client/anchor/ratio 区域换算、屏幕原点/负坐标保留、PNG 加载/保存、裁剪、显式 ReleaseImage、透明像素模板通配、结果局部与 screen 坐标、颜色/图像稳定等待、暂停/停止与循环内部取消令牌。
- 无新增第三方图像引擎/OCR 依赖；未改写 MoveTo 的屏幕绝对坐标语义。截图来自可见桌面合成结果，不保证被遮挡窗口真实后台像素，也不绕过 UIPI/UAC/安全桌面。

### 关键提交与 Windows CI
- 核心图像 API：e6433f4dca5d1dc0dfaad900481ef83351cc408a。
- 取消、灰度/缩放与稳定确认的后续接线/测试提交包括 cfffbca97cfdf951fb4f0789ea0fbd3574f90482、1c31de462e4b232e7b6d191851782dbffd0fa46b、09556c15f2652f53870c65f17c7c6631c29a39bb、d57f5f4ff2421c9b0105a96fdb57943363de2a41、482f4303d0867e8998f870eb1b6100206ff57cd6、d7546b491a280b6aaa182f52bc67b714c2c3d698、1b87af713ff17c907feac6984bb96aa5dd8e821f。
- 中间 CI 有失败，未隐藏：run 43352282268/43352416611 发现 API 契约测试结构与生成函数手册范围不一致；后续灰度/缩放迭代的 run 43353086988、43353344151、43353379802 失败于功能接线尚未完成的中间提交。修正后 run 43353694932 SUCCESS，后续加入连续确认跟踪器测试的最终代码基线 commit 1b87af713ff17c907feac6984bb96aa5dd8e821f 对应 run 43353801969 也 SUCCESS。
- 最终 Windows Actions run 43353801969：Restore PASS；Release Build 0 warnings/0 errors；73 passed / 0 failed / 0 skipped；API handbook --check PASS；self-contained win-x64 publish、ZIP/SHA256 package、artifact upload PASS。Artifact ID 14004658368，76,430,108 bytes。该 CI artifact 不是正式 Release，且上传 artifact digest 不应与内部应用 ZIP 的 SHA256 混为一谈。
- 本地执行容器没有 .NET SDK，因此本地 Restore/Build/Test 未运行；实际构建/测试由 Windows GitHub Actions 执行。

### M8 未完成与验收边界
- 尚未实现图像资源管理/模板预览 UI、模板命名及路径/版本校验、工程资源依赖联动、视觉调试 overlay、RGB/十六进制辅助转换、颜色差值和颜色块识别；因此 M8 保持 IN_PROGRESS，不能进入 DONE 状态。
- docs/ACCEPTANCE-TEST-CN.md M8-01..M8-13 的 Windows 桌面场景尚未执行（NOT RUN）；M8-14 对应 UI 尚未实现。CI PASS 不替代 Windows 10/11 的 DPI/多显示器/负坐标、遮挡、受保护窗口、Stop 与资源清理实测。
- 未修改 VERSION/csproj 预览版本，没有创建新 Release；v0.1.0-preview.4 官方 Release ZIP/SHA256 未变化。
 
## 2026-11-13 — M8-CLOSE — 图像资源 UI、视觉调试与 RGB/颜色块闭环（Windows CI PASS；桌面验收 NOT RUN）

### 实现
- API registry 从 0.5 升至 0.6，公开函数总数从 74 升至 78；新增 RgbToHex、HexToRgb、ColorDifference、FindColorBlocks。编辑器补全/签名提示继续使用唯一 registry，函数参考手册同步生成。
- 新增脚本级 .images.json 与 .assets PNG 资产目录，支持 PNG 导入/列表/预览/命名/移除/路径复制；清单保存 schema、ScriptId、相对路径、尺寸、文件大小、SHA-256 和导入时间。导入限制 128 MiB、32,000,000 像素；未知 schema 不覆盖，路径限制在当前脚本 assets 目录内。
- 图像资源 UI 保留 PixelImage 像素模型，不在列表/预览状态持有 GDI Bitmap；绘制时只临时创建 Bitmap 并释放。LoadImage 相对路径以脚本目录为基准。静态 LoadImage 字面资源引用可扫描，仍被引用时阻止移除；动态路径报告为无法静态解析。移除清单不删除 PNG 文件。
- ScriptRepository 的 rename/duplicate/delete 同步管理 manifest 和 assets；rename/duplicate 同步改写脚本中的静态资产目录前缀，duplicate 为清单重新生成 ScriptId 并重基资源相对路径。
- 增加视觉调试覆盖层，显示截图区域、颜色像素、FindColor/FindImage 命中矩形、颜色块、similarity、RGB 和执行耗时。覆盖层置顶、透明、点击穿透；使用 SetWindowDisplayAffinity 尝试排除自身截图，并在系统返回失败时向用户日志警告。
- Core 新增 RGB/hex、颜色差值、八邻域色块算法；增加 PNG manifest/path/hash 和 LoadImage 依赖扫描测试；颜色块扫描上限 4,000,000 个采样点、最多 100 个返回项并支持取消。

### 验证
- 本地执行容器无 .NET SDK，因此本地 Restore/Build/Test 未运行。最终实现代码基线 `7dae8af881524fae08ddd5af281ef9d4e47a58c7` 的 run [43357876777](https://github.com/xiaobei206/anjian/actions/runs/43357876777) 已通过；随后文档同步 main HEAD `c2b30c1d9aabd876cd848d6203376060f294c34e` 的 run [43358188770](https://github.com/xiaobei206/anjian/actions/runs/43358188770) 再次 SUCCESS。Windows Server 2025（10.0.26100，SDK 10.0.404）：Restore PASS；Release Build 0 warnings / 0 errors；Core Tests 85 passed / 0 failed / 0 skipped；API handbook `--check` PASS（78 个公开函数）；self-contained win-x64 publish PASS；ZIP/SHA256 package 与 artifact upload PASS。
- 最新临时 CI artifact：`Anjian2026-preview-win-x64`，Artifact ID `14006903094`，大小 76,460,671 bytes，保留期 30 天；[下载 artifact](https://github.com/xiaobei206/anjian/actions/runs/43358188770/artifacts/14006903094)。它是 Actions 临时 artifact，不是 GitHub Release；“Create GitHub Release”步骤按分支 push 预期 SKIPPED。没有修改 VERSION/csproj 版本，也没有替换 v0.1.0-preview.4 官方 ZIP/SHA256。
- 中间失败均保留并已修复：初次 Build 的 `RgbColor` 类型/方法同名遮蔽；随后 Build 的 WinForms `WFO1000` 和 xUnit 断言 analyzer；之后测试暴露 registry 0.6 仍残留 0.5/74 项旧预期。当前最终 run 已全绿。
- M8-01..M8-18 Windows 10/11 桌面人工验收全部 NOT RUN。特别需要真实桌面验证多显示器负坐标、透明 PNG/GDI 预览、脚本资源迁移、点击穿透、SetWindowDisplayAffinity 返回及截图是否含 overlay。Windows Server CI 不代表用户桌面通过。
## 2026-11-13 — M8-DESKTOP-ACCEPTANCE 执行受阻（未进行桌面测试）

### 核查结果
- 会话开始时 main HEAD：`b06910f93a40780c3ffa10a04d64d41d176b2767`，仅为文档同步提交。
- M8 实现代码基线：`c2b30c1d9aabd876cd848d6203376060f294c34e`。关联 Windows Actions run [43358188770](https://github.com/xiaobei206/anjian/actions/runs/43358188770) SUCCESS：Restore PASS；Release Build 0 warnings / 0 errors；85/85 Core tests；API handbook `--check`；self-contained win-x64 publish；ZIP/SHA256 与 artifact upload 全 PASS。Artifact ID `14006903094`，76,460,671 bytes。
- 该 run 使用 Windows Server 2025 runner。它证明代码构建和自动化测试通过，不证明 Windows 10/11 用户桌面的 GDI 截图、DPI、多显示器、WinForms 资源 UI、overlay 点击穿透或 Display Affinity 行为通过。

### 本轮实际执行与限制
- 当前执行环境：Debian GNU/Linux 13 x86_64；未安装 .NET SDK（`dotnet --info` 不可用）；无 Windows/PowerShell 桌面会话，也无交互访问用户 Windows 桌面的通道。
- 因 M8 唯一任务要求真实 Windows 10/11 桌面，本轮未运行应用，未执行 M8-01..M8-18。18 项全部继续为 **NOT RUN**，没有将未执行项目记为 PASS 或 FAIL。
- 未改动源码、公开 API、VERSION 或 v0.1.0-preview.4 Release 资产；未创建新 Release。

### 精确后续
下一会话仍只做 M8-DESKTOP-ACCEPTANCE：在可交互的 Windows 10/11 桌面使用最新 main 对应的 win-x64 artifact，逐项记录 OS build、DPI、显示器布局/负坐标、步骤、结果与日志/截图；优先覆盖资源管理 UI、manifest/路径/hash 防护、GDI 截图/透明像素、颜色/模板算法、overlay 点击穿透和 Display Affinity 截图隔离。没有实际桌面和证据时，保持 M8 IN_PROGRESS、M8-01..M8-18 NOT RUN，不进入 M9。


## 2026-11-13 — M9 OCR 首轮实现（Windows CI 待验证，桌面验收延期）

### 已提交代码
- API registry 0.6 → 0.7；公开 Lua API 78 → 81，新增 `OCR`、`FindText`、`WaitText`。函数/参数手册由注册表重新生成，编辑器提示仍直接读取相同注册表。
- 新增 Core 引擎中立 OCR DTO 与 `IOcrRecognitionService`；Core 不引用 PaddleOcrNet、ONNX Runtime 或第三方模型类型。
- App 引入 PaddleOcrNet 2.4.0 适配层，CPU 模式；主窗口新增“OCR 模型”管理器，支持 ch/en/japan 语言模型显式下载、进度/取消和 SHA-256 校验。缓存目录为 `%LOCALAPPDATA%\\Anjian2026\\Ocr\\Models`。
- Worker 端采用严格离线模式。模型缺失明确报错并提示用户下载；脚本执行时不会自动联网。识别仅在独立 Lua Worker 中运行。
- 新增图像 PNG 导出 IPC，受限为 16 MP 和 64 MiB；OCR 行结果包含全文、confidence、局部矩形与 polygon，截图还保留 screenX/screenY。
- WaitText 使用主进程截图，在每次轮询前走 checkpoint，支持 Pause；Stop 终止 Worker，并通过 finally 释放每轮截图 ID。
- 新增 Core Lua API 自动化测试：OCR table/置信度/坐标原点、FindText contains/exact、WaitText checkpoint/资源释放、非法语言/置信度/模式/interval 校验。

### 技术选择
- 精确 NuGet 依赖：PaddleOcrNet 2.4.0（MIT；.NET 10），固定在 Anjian.App，不进入 Core。源代码参考 <https://github.com/FarhanLodi/PaddleOcrNet/tree/v2.4.0>。
- 模型只有通过用户点击“OCR 模型”后下载；识别服务的 `Offline=true`，CPU provider，不要求用户安装 GPU Runtime。模型/语言的完整许可和分发清单仍需在 M14 发布审查时最终确认。
- 上游项目规模较小，使用适配层隔离并锁定版本；如 Windows CI/许可/质量不达标，可替换引擎但不改变 Core DTO 与 Lua API。

### 验证状态（最终结果已确认）
- 当前会话本地环境为 Debian 13，无 .NET SDK；本地 Restore/Build/Test：NOT RUN。
- Windows Actions 最终 run [43363150335](https://github.com/xiaobei206/anjian/actions/runs/43363150335) SUCCESS：Restore PASS；Release Build 0 warnings / 0 errors；90/90 Core tests；API handbook `--check`；self-contained win-x64 publish；ZIP/SHA256 与 artifact upload 全 PASS。Artifact ID 14008744436。
- M9-01..M9-08 真实 Windows 桌面验收均 NOT RUN，延后至 M14 功能开发结束后的统一验收；M8-01..M8-18 状态也不改变。
- 未修改 VERSION、AssemblyVersion、FileVersion；未创建 Release，preview.4 正式 ZIP/SHA256 不变。


## 2026-11-13 — M9 OCR 完成 Windows CI（实现/自动化 DONE；实机验收延期）

### 最终代码
- 最终 M9 实现与修复基线：`41e6d56a6485114e44cc9453b97ce05dd04f4c5d`。
- API registry 0.7 / 81 个公开函数；Core OCR DTO/interface、PaddleOcrNet 2.4.0 CPU adapter、显式模型管理窗、严格离线 Worker、截图 PNG IPC、OCR/FindText/WaitText、Pause-aware WaitText delay 均已进入 main。
- M9 模型管理窗下载中关闭时先取消下载，待异步任务 finally 收尾后再关闭；避免窗体释放后异步回调访问控件。

### Windows Actions 最终证据
- 最终 run [43363150335](https://github.com/xiaobei206/anjian/actions/runs/43363150335)：**SUCCESS**，runner 为 Windows Server 2025。
- Restore PASS；Release Build PASS（0 warnings / 0 errors）；Core Tests **90 passed / 0 failed / 0 skipped**；API handbook `--check` PASS；self-contained win-x64 publish PASS；ZIP/SHA256 package PASS；artifact upload PASS。
- 最终临时 artifact：`Anjian2026-preview-win-x64`，Artifact ID `14008744436`，87,078,010 bytes；Actions artifact archive SHA-256 digest `70a0d63982f035b3169c4319867d6a893ed891e8ba5046976a3a328d455f6ccc`；[下载 artifact](https://github.com/xiaobei206/anjian/actions/runs/43363150335/artifacts/14008744436)。该临时构建包不是 GitHub Release。
- 前一轮完整验证 run [43362673890](https://github.com/xiaobei206/anjian/actions/runs/43362673890) SUCCESS（89/89 tests），但随后针对 Pause-aware delay 和下载窗体关闭做了额外可靠性修正，因此以 43363150335 的 90/90 作为最终结论。

### 失败与修复记录
- 首批提交的 run 43361831218 等出现 Core OCR IPC 使用 App 层 `WindowApiJson` 的编译边界错误，以及 `IReadOnlyList.IndexOf` 不可用。IndexOf 改为显式索引遍历；Core 改用自己的 `JsonSerializerDefaults.Web` 选项，兼容主进程 camelCase IPC。修复后 run 43362673890 全绿。
- 可靠性补丁首轮 run [43362993922](https://github.com/xiaobei206/anjian/actions/runs/43362993922) Build FAIL：C# CS0157，不允许从 finally 子句返回。将窗体清理逻辑改为 if/else，不在 finally 中 return；最终 run 43363150335 全部通过。
- 这些失败均已留下明确根因和修复，不以中间失败作为最终验收，也不隐去最终通过证据。

### 尚未完成与验收边界
- 本地容器为 Debian 13，无 .NET SDK；本地 Restore/Build/Test：NOT RUN。最终 90 项测试和 Windows x64 发布全部来自 Windows Server 2025 GitHub Actions。
- M9-01..M9-08 真实 Windows 10/11 桌面验收全部 NOT RUN：包括真实模型下载/哈希、断网识别、中文/英语/日语精度、真实边界框、WaitText Pause/Stop、DPI/多屏与长时稳定性。它们与 M1–M8 桌面项一并延期至 M14 完成后的集中验收。
- 未修改 VERSION、AssemblyVersion、FileVersion；未创建 Release；v0.1.0-preview.4 官方 ZIP/SHA256 未更改。


## 2026-11-13 — 开始 M10 参数/配置/自定义 UI（规格优先）
- 已核对 main HEAD `a5aa0aa21ca16e95fc8e3a1ace5c7b8d7d0925d8`，并读取 AGENTS、README、handoff、PRD 2.9/2.13、F20/F26、架构、决策、API registry 与 Worker IPC。
- M9 Windows Actions run 43363150335：Restore、Release Build（0 warnings/0 errors）、90/90 Core tests、API handbook check、self-contained win-x64、ZIP/SHA256 和 artifact upload 全 PASS；真实桌面仍 NOT RUN。
- M10 先冻结 ADR-022：脚本旁版本化 params schema、独立用户运行配置、10 种参数类型、统一 Core validator、启动前拒绝、typed Worker IPC、Lua 只读 Args、原子写入/备份/损坏恢复/未知 schema 保护和敏感字段脱敏。
- 当前阶段仅完成规格与验收定义；代码和 Windows CI 尚未验证，不得标记 M10 完成。接下来实现 Core schema/validator/store 和测试，再接入 Worker/UI。


## 2026-11-13 — M10 首轮参数 schema / UI / Worker 集成

### 已进入 main
- ADR-022 冻结 sidecar schema v1：`<script>.params.json`，独立于 Lua 源码；读取定义不执行脚本。
- 新增 Core `ScriptParameterSchema`、`ScriptParameterValidator`、`ParameterConfigurationStore`、`LuaParameterInjector`。支持 text/integer/number/boolean/select/multiSelect/filePath/directoryPath/hotkey/color；范围、必填、选项、重复多选、路径存在性、热键/颜色格式、文本正则均有统一验证入口。
- 配置按脚本路径 SHA-256 派生键保存于 LocalAppData；原子写入、有效备份、损坏恢复；未来 schema 版本拒绝读取/覆盖，保护原文件。导入配置先完整验证。
- WinForms 新增“参数定义”按钮、DataGridView 参数设计器和 schema 驱动运行表单；播放与调试均在启动 Worker 前收集并验证参数，取消/错误不启动 Worker。表单支持恢复默认、JSON 导入/导出、文件/目录选择与敏感字段遮蔽/导出确认。
- Worker 启动握手通过结构化 JSON 传递已验证值，Lua 获得原生类型 `Args`；不将用户字符串拼接为 Lua 源码。常规顶层/嵌套赋值会被只读代理拒绝；这不是安全沙箱。敏感配置目前本地明文保存，不进入普通运行日志。
- 没有新增公开 Lua API，因此 API registry 仍为 0.7 / 81 项，函数手册不需要变更。

### CI 失败与修复
- 初次 Build 因 JSON 反序列化可产生 null ID 而触发 CS8604；所有字段级错误构造改为安全 ID 回退。
- 后续 Build 发现参数设计器的辅助方法 `Text(DataGridViewRow, string)` 隐藏了 Form.Text 并导致编译错误；改名为 `CellText`。
- 测试编译发现 JSON 字符串字面量转义错误，修复测试输入；测试又揭示配置读取未使用 Web JSON options（camelCase 配置丢失 Values）以及未来 schema 测试未预置有效备份，已修正序列化读取和测试夹具。
- run [43366381240](https://github.com/xiaobei206/anjian/actions/runs/43366381240) 对 M10 代码基线通过：Restore PASS、Release Build 0 warnings/0 errors、98/98 Core tests、API handbook check、自包含 win-x64 发布、ZIP/SHA256 和 artifact upload 全 PASS；artifact ID 14011010562，87,098,604 bytes，digest `7d5f988b7cdaabd3f0c3db2fe3f9b8c1ecb2fb205ee9963ca3614c745dc12b17`。
- 随后增加数值/相对路径/正则与可选空值测试，并修复默认/已保存/导入的相对路径必须以脚本目录为基准的问题；最新 run [43367065279](https://github.com/xiaobei206/anjian/actions/runs/43367065279) SUCCESS：100/100 Core tests、Build 0 warnings/0 errors、API handbook check、自包含发布、ZIP/SHA256 与 artifact upload 全 PASS。artifact ID 14011171617，87,098,770 bytes，SHA-256 `e13317464a78e9c8c01559faf9a9bd1e7d1b08d94097d5ef11dcb510dbb406b9`。本地环境没有 .NET SDK，本地 Build/Test NOT RUN。
- M10-01..M10-10 的 Windows 桌面验收仍 NOT RUN，延期至 M14 后；未修改 VERSION、未创建 Release。


## 2026-11-13 — M10 参数系统边界加固与最终 Windows CI

### 代码审查发现与修复
- 发现 Lua 只读代理的 `pairs(Args)` 原先直接返回内部原始值，脚本可通过遍历拿到嵌套 table 后修改内容。迭代器现在对每个返回值递归包装只读代理；新增 Lua 回归测试验证遍历后尝试修改失败且原值仍为 42。
- 明确文件/目录路径的双重语义：用户配置继续保存用户填写的相对路径，保证工程可移动；校验和运行时解析统一以脚本目录为基准，播放/调试送入 Worker 的参数值在启动前规范化为绝对路径。增加 Core 测试验证持久值与运行值不同但各自正确。
- 路径语法错误现在在 validator 阶段返回字段问题；参数机器名拒绝 Lua 保留字，避免 `Args.end` 等不可解析访问；schema validator 对 null 参数项和非有限最小/最大值增加保护。
- 参数设计器保存既有 `.params.json` 前先重新加载并验证；无法解析的旧定义或未来 schemaVersion 不会被静默备份/覆盖。原子写入补充 WriteThrough 与 flush。
- 复核播放和调试入口：两者共用 MainForm 的参数收集/验证；取消、缺 schema 或校验错误均在调用 `PlaybackRunner.RunAsync` 前返回。Worker 在 worker-ready 握手接收独立 JSON typed values，先注入 Lua `Args` 再执行脚本。应用常规启动日志不自动记录参数值；脚本主动打印敏感内容仍可能进入日志。Sensitive UI 遮蔽不是加密，配置文件仍为明文 JSON。
- 未新增公开 Lua API，registry 维持 0.7 / 81 项，函数参考手册无须变更。

### 本轮代码提交（全部直接提交 main）
- `026aa726a6cd935a0c71a98b505a1eb6e5b0d376` — validator 路径/保留字/运行时路径解析。
- `8722822fa9d8aaede8abd0d57d32a95e68873434` — `pairs(Args)` 递归只读代理。
- `938548b43c41dc2f89601113deef5de4bcd2db27` — 播放/调试参数传入 Worker 前路径规范化。
- `3bf90eab244026652e41deb8ae43256e73da942c` — 未来/损坏 schema 防静默覆盖与写入 flush。
- `ac661c3ea19aa0a3c2228291518e964d0be6a711` — 相对路径与非法值 Core 回归测试。
- `a923f1e02dce73629514da39bb10925b1271fc15` — Lua pairs 深层只读回归测试；最终代码 HEAD。

### 最终 Windows Actions 证据
- run [43368224862](https://github.com/xiaobei206/anjian/actions/runs/43368224862)（run #218，Windows build and test）针对最终代码 HEAD `a923f1e02dce73629514da39bb10925b1271fc15`：Restore PASS；Release Build PASS，0 warnings / 0 errors；Core Tests **102 passed / 0 failed / 0 skipped**；API handbook `--check` PASS；self-contained win-x64 publish PASS；ZIP/SHA256 与 artifact upload PASS。GitHub Release 相关步骤 SKIPPED。
- 临时 artifact `Anjian2026-preview-win-x64`，ID `14010789337`，87,099,638 bytes，archive digest `sha256:324ed884d9938548d2d35b94cd372a4bc46bf081f7abd1c77e17b28d1acf1323`，有效至 2026-12-13；这不是正式 Release。日志另有 GitHub Actions Node.js 20 弃用提示，非项目编译 warning。
- 本地执行环境为 Debian/Linux 且无 .NET SDK；本地 Restore/Build/Test 均 NOT RUN。最终自动化结果来自 Windows Actions。
- M10-01..M10-10 真实 WinForms/UI/运行时桌面验收仍 NOT RUN，按用户决定延期至 M14 集中执行。没有修改 VERSION、AssemblyVersion、FileVersion；没有创建 tag 或 Release；v0.1.0-preview.4 官方资产不变。
