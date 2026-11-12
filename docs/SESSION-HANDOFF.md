# 当前会话交接

## 当前状态
- 项目：按键精灵 2026（暂定工作名）
- 仓库：xiaobei206/anjian
- 主分支：main
- 已发布版本：**v0.1.0-preview.4**
- Release：https://github.com/xiaobei206/anjian/releases/tag/v0.1.0-preview.4
- Release commit：`08886d9e66c08ba24f6f1b7d9e91bbdaeba8a9e0`
- 发布 Actions run：`43253611158`，结论 SUCCESS。
- v0.1.0-preview.4 发布包对应代码 commit：`08886d9e66c08ba24f6f1b7d9e91bbdaeba8a9e0`。
- M3 当前 main 开发代码验收基线：`71fd493cad0fbed1451a4ffa46192752a18bd0a6`（Windows Actions run `43282795272` PASS）；接手时仍须重新核对 main HEAD。
- main 分支可能继续产生文档提交；新 GPT 接手时必须重新查询 main 最新 HEAD，不要把 handoff 中的历史提交 SHA 当成分支当前头部。
- 当前阶段：M0/M2/M3 已完成；M1 Windows 实机验收仍 IN_PROGRESS/NOT RUN；M3 Lua IDE 基础已通过 Windows CI，桌面手工验收待执行；下一开发阶段为 M4 Lua 调试器。

## 完整产品目标已冻结（2026-11-13）
- 产品范围基线：[docs/PRD-CN.md](PRD-CN.md)。
- 完整功能矩阵（当前状态、阶段、验收条件）：[docs/FEATURE-MATRIX-CN.md](FEATURE-MATRIX-CN.md)。
- 中文函数与参数参考手册：[docs/FUNCTION-REFERENCE-CN.md](FUNCTION-REFERENCE-CN.md)。
- API 当前实现契约：[docs/LUA-API-CN.md](LUA-API-CN.md)。
- 阶段路线图：[docs/ROADMAP-CN.md](ROADMAP-CN.md)。
- 完整 IDE、窗口/坐标、图色/OCR、参数 UI、小精灵、插件及函数手册均为正式目标，不得再从大纲中删除或只标为可选远期项目。
- 下一项开发任务是 M3：实现完整 Lua IDE 基础（语法高亮、行号、补全、参数提示、诊断、查找/替换、多标签和错误行导航）；补全与命令面板数据必须直接读取 M2 的唯一 API 注册表。


## preview.4 窗口与托盘决策
- 悬浮窗和完整主窗口必须是两个独立顶层窗口，可以同时显示；打开主窗口不替换或隐藏悬浮窗。
- 程序启动默认显示悬浮窗；主窗口隐藏且没有任务栏按钮，系统托盘图标保持可用。
- 主窗口右上角 × 与“返回悬浮窗”按钮只隐藏主窗口、移除任务栏按钮，不退出进程；如果悬浮窗此前被用户隐藏，关闭主窗口不擅自重新显示它。
- 托盘菜单负责重新打开主窗口、显示/隐藏悬浮窗和明确退出程序。悬浮窗关闭按钮只隐藏悬浮窗。
- 悬浮窗直接设置循环次数和轮间隔；与主窗口使用同一份配置并双向同步，0 次表示无限循环。
- EXE、主窗口、悬浮窗和托盘使用同一套自定义多尺寸图标。
- 原有只录点击、录制暂停/续录、全局快捷键和播放循环能力继续保留。

## 已冻结的产品目标与当前版本语义

- 目标平台：Windows 10/11 x64；当前技术基础为 WinForms + .NET 10 + Lua 5.4。
- 当前 preview.4 的用户脚本 API 仍保留 MoveTo(x, y) = 虚拟桌面绝对物理像素；副屏负坐标合法。窗口/客户区/锚点/归一化坐标是完整产品必须实现的新能力，不能静默改变 MoveTo 既有语义。
- Click / KeyPress 默认按下 50ms；显式传入 0 表示不增加人为等待；当前规格统一最大 600000ms。长按使用 Down / Delay / Up。
- 播放次数默认 1；正整数按完整脚本轮数；0 = 无限循环。
- 每轮间隔默认 2000ms，只在两轮之间等待，最后一轮后不等待；与脚本内部 Delay 独立。
- 经典 PC 版功能的核心等价范围包括 IDE/调试、录制与动作列表、键鼠、完整坐标/窗口、图色/OCR、脚本参数、自定义用户界面、插件和独立小精灵打包。
- 原先“首版先做轻量小工具”“当前版本不提供窗口相对坐标”的决定只描述 preview.4/首阶段，不再限制完整产品目标。
- 旧版 Q 语言/旧插件二进制完全兼容、移动端、云账号/商城、远程群控、内核驱动与安全绕过不属于目标。

## 当前已交付功能
- WinForms 主窗口和脚本列表。
- 脚本本地存储、新建、打开、保存、重命名、复制、删除和右键菜单。
- M3 Lua 编辑器（仅在当前 main 未发布开发版）：语法高亮、行号、注册表驱动 API 补全/签名提示、查找替换、多标签、语法只编译诊断与错误行导航；标签切换/关闭自动保存。v0.1.0-preview.4 发布包仍只有基础文本编辑器。
- 全局鼠标/键盘录制，鼠标移动合并，录制过滤注入事件及本程序窗口事件，避免键盘自动重复污染；录制结束时补齐尚未释放的 Down/Up。
- 录制数据生成 Lua 代码，点击按下时长与动作间隔分开保存。
- NLua 1.7.9 / Lua 5.4 worker 子进程；named pipe IPC；指令钩子 Pause 检查点；Stop 可终止 CPU 无限循环 worker。
- Windows SendInput / SetCursorPos 鼠标键盘输入、键状态追踪和停止清理。
- Unicode 文本输入、可取消长文本注入、常见标点键和数字小键盘键映射。
- 播放/暂停/停止、重复次数和轮间间隔；配置持久化于用户本地配置目录。
- 全局快捷键：空闲时 F10 启动；运行/暂停时 F11 暂停/继续、F12 中止。低级键盘钩子过滤模拟注入事件；只有对应动作可执行时才截获功能键。代码提交 `e8e96cb26df1a254d2db45dd2a72b2c7ef11cd9f`，Windows CI 已通过；实机验收待用户完成。
- self-contained win-x64 ZIP 和 SHA256 发布资产。

## 最新 Release 资产：v0.1.0-preview.4
- Release 页面：https://github.com/xiaobei206/anjian/releases/tag/v0.1.0-preview.4
- ZIP：`Anjian2026-0.1.0-preview.4-win-x64.zip`
- ZIP 下载：https://github.com/xiaobei206/anjian/releases/download/v0.1.0-preview.4/Anjian2026-0.1.0-preview.4-win-x64.zip
- ZIP 大小：51,910,925 bytes。
- SHA256 sidecar：`Anjian2026-0.1.0-preview.4-win-x64.zip.sha256`
- SHA256 下载：https://github.com/xiaobei206/anjian/releases/download/v0.1.0-preview.4/Anjian2026-0.1.0-preview.4-win-x64.zip.sha256
- ZIP SHA256：`6b59ab27f825aa48b2ec5343cec826ad8b0ded759e9989af02d51f8b949f09aa`
- GitHub Release asset digest 与 ZIP 内容 SHA256 一致；sidecar 已随 ZIP 一同发布。
- Release asset IDs：ZIP `740099276`；SHA256 `740099275`。
- Release ID：`443178727`，prerelease=true。

## 历史 Release：v0.1.0-preview.1
- Release 页面：https://github.com/xiaobei206/anjian/releases/tag/v0.1.0-preview.1
- ZIP 大小：51,879,710 bytes。
- ZIP SHA256：`80bfbf3ddbebafaf6e835bc8fb29148760662418b2c57de79f7ab7dbe88e6164`。
- Release ID：`442961207`，prerelease=true。

## Windows CI 验证
- v0.1.0-preview.4 Release workflow run：`43253611158`，SUCCESS；Restore/Build/7 项测试/Publish/ZIP/SHA256/Artifact upload/Release creation 全部成功。
- v0.1.0-preview.3 Release workflow run：`43232898372`，SUCCESS；所有 job/step 均成功。
- 修复空值边界后的 CI run：`43232594517`，SUCCESS。
- 最终 Release 构建、7 项自动化测试、自包含 win-x64 publish、ZIP/SHA256 package、artifact upload、GitHub Release creation：全部 SUCCESS。
- 自动化测试：7 passed / 0 failed / 0 skipped（测试项目现有 7 项）。
- 测试包括：播放次数/轮间隔默认值、100 轮 99 次间隔、0 无限循环、边界值校验；Lua 5.4 版本确认、Lua 调用 C# 全局函数、Lua instruction-count hook 触发。
- self-contained win-x64 publish：SUCCESS。
- ZIP/SHA256 package：SUCCESS。
- Release ZIP SHA256 已通过实际解包 Actions artifact 并运行 `sha256sum -c` 核验。
- 正式 prerelease v0.1.0-preview.3 已成功创建（历史版本）。
- 第一轮 Release 构建曾因 `MainForm.cs` 的 nullable warning-as-error 失败；加上 `_current is null` 守卫后，修复提交 `2eacde0c12f9616e4f37d209c929c1b473edab43` 的 CI 通过，随后以新 release commit 完成发布。

## 仍未完成（务必如实）
- 用户 Windows 实机手工验收：NOT RUN。
- 2 小时稳定性测试：NOT RUN。
- Windows 10/11 干净环境、DPI、多显示器负坐标、复杂拖拽及具体目标程序权限兼容：NOT RUN。
- 录制采用低级 Hook，需在用户电脑确认 Hook 采集工作正常、不会漏录或重复。
- 代码折叠、独立 API 命令面板未实现；断点/单步/变量窗/调用栈属于下一阶段 M4。M3 Windows 桌面手工验收仍为 NOT RUN。
- 没有可视化动作列表/动作参数编辑 UI，录制结束直接生成 Lua。
- 不支持图像识别/OCR、窗口相对坐标、后台窗口注入、插件或云同步。

## preview.4 本轮变更与验证边界
- preview.3 的“紧凑/完整模式”是同一个 MainForm 切换面板，导致悬浮窗并非独立窗口；本轮已拆为独立 FloatingForm。
- 默认启动隐藏主窗口，仅显示悬浮窗和托盘图标；主窗口 ShowInTaskbar=false。打开完整界面时显示主窗口，不隐藏悬浮窗。
- 主窗口右上角 × 与“返回悬浮窗”都隐藏主窗口并移除任务栏按钮，不退出进程；托盘菜单负责恢复窗口或退出。
- 悬浮窗可直接设置循环次数与轮间隔，并与主窗口共用设置及持久化。
- 自定义多尺寸 ICO 已提交为 `src/Anjian.App/Resources/Anjian2026.ico`；csproj 设置 `ApplicationIcon`，主窗体、悬浮窗和 NotifyIcon 使用相同 EXE 图标资源。Windows CI 已成功编译，实机小尺寸可辨性仍待用户确认。
- Windows CI 编译、7 项自动化测试、self-contained 发布、ZIP/SHA256 和 Release 创建：PASS（Actions run `43253611158`）。
- Windows 10/11 实机手工验收：NOT RUN；CI 成功不代表桌面窗口、托盘、Hook 或图标实机通过。

## M1 实机验收待办（仍为 NOT RUN，不阻塞后续 M4 开发）
用户下载 v0.1.0-preview.4，在 Windows 10/11 实机验收：
1. 默认启动确认只显示悬浮窗与托盘图标，任务栏没有主窗口按钮。
2. 点击悬浮窗“完整界面”打开主窗口，确认悬浮窗一直留在桌面；关闭主窗口右上角 × 后，主窗口任务栏按钮消失，悬浮窗和托盘仍在。
3. 通过托盘右键菜单分别打开主窗口、显示/隐藏悬浮窗和退出；双击托盘图标应打开主窗口。若悬浮窗已隐藏，关闭主窗口后应只剩托盘图标，不应自动弹出悬浮窗。
4. 确认主窗口“返回悬浮窗”按钮存在且有效；点击后主窗口隐藏，任务栏按钮消失。
5. 在悬浮窗修改循环次数和轮间隔，在主窗口修改后反向确认同步；确认 0=无限、设置重启后持久化。
6. 检查 EXE 文件图标、任务栏主窗口图标、托盘图标和悬浮窗图标是否一致，16/32px 尺寸下是否清晰。
7. 回归只录点击、录制暂停/续录、F10/F11/F12、播放/停止和原有 Lua API；将失败步骤、截图和日志交给下一会话。

## M2 完成：单一机器可读 API 注册表与函数手册生成（Windows CI 已验收）

### 实现基线
- M2 实现提交：`0f7a80c24f18789833d7fbb92e4979da65ed261d`。
- 本次 CI 验收对应的最新主线代码基线：`fdff7c14398485fa30725e3ea283c03a545c97a6`。
- 唯一机器可读来源：`src/Anjian.Core/Api/lua-api-registry.json`，共 22 个**当前真实实现**的公开 Lua 函数。
- `LuaApiRuntime.Register` 根据注册表建立 Lua 全局包装；内部 IPC bridge 在闭包建立后隐藏，不作为公开 API。
- `tools/Anjian.ApiDocsGenerator` 支持 `--write` 和 `--check`；函数手册第 2 节由注册表生成。注册表另提供分类、签名、参数 schema、默认值、范围、返回契约、错误、暂停/取消、坐标/权限语义、示例、实现状态和 API 版本，供后续 IDE/命令面板读取。
- 当前注册表仅含 22 个真实函数；窗口、图色、OCR、后台控制等均保持“目标设计”，不作为当前可调用能力。

### Windows CI 验收证据
- 初始 M2 Actions run `43271693389` 在 Build 阶段暴露测试代码 nullable tuple 类型不一致（CS8620）；后续修正后 Build 通过。
- 随后的契约测试 run `43272945064` 发现 Lua 中文字符串在测试中变为问号；提交 `ac034aa10b1c8b1acae25a297d4e20928d9fe7f4` 将 Lua state encoding 明确设为 UTF-8。
- 最新主线代码提交 `fdff7c14398485fa30725e3ea283c03a545c97a6` 对应 Actions run `43273917056`：**SUCCESS**。
- 最新成功运行实际步骤：Restore PASS；Release Build PASS（0 errors）；自动化测试 **12 passed / 0 failed / 0 skipped**；API 手册 `--check` PASS；win-x64 self-contained publish PASS；ZIP/SHA256 打包 PASS；Actions artifact 上传 PASS。
- 该次 push 不是 `release:` 提交，因此 Read release version / Create GitHub Release 两个发布专用步骤按条件跳过；本轮没有创建新 Release，已发布的 preview.4 资产不变。
- Actions 记录：<https://github.com/xiaobei206/anjian/actions/runs/43273917056>。

### 验收边界与未完成事项
- 当前执行容器没有 .NET SDK（`dotnet --info` 返回 command not found），本地 Restore/Build/Test：NOT RUN；实际构建和 12 项自动化测试由 Windows GitHub Actions runner 执行并通过。
- M2 完成时，编辑器补全/签名提示和命令面板 UI 尚未实现；随后 M3 已交付注册表驱动的编辑器补全与签名提示，独立命令面板仍未实现。
- M1 Windows 10/11 窗口/托盘/Hook 实机验收、DPI/多显示器、复杂拖拽和 2 小时稳定性仍为 NOT RUN。CI 成功不等于桌面实机验收成功。

### M3 完成：完整 Lua IDE 基础（Windows CI 已验收）

- 最终 M3 代码验收基线：`71fd493cad0fbed1451a4ffa46192752a18bd0a6`。
- Windows Actions run `43282795272`：SUCCESS；Restore PASS；Release Build PASS（0 warnings / 0 errors）；自动化测试 `20 passed / 0 failed / 0 skipped`；API handbook `--check` PASS；self-contained win-x64 publish、ZIP/SHA256 和 artifact upload 均 PASS。
- 实现位于 `Anjian.Core.Editor.LuaEditorService` 和 WinForms `LuaEditorControl` / `ScriptSearchReplaceForm` / `MainForm`。功能包括语法高亮、行号、API 补全、活动参数/签名帮助、查找替换、多标签保存/重开、语法诊断和错误行导航。
- API 候选、签名和高亮只读取 `LuaApiRegistry.Load().Categories` / `.Apis`；不复制函数定义，只列出状态为 `implemented` 的 API。独立 API 命令面板未实现。
- 语法检查调用 NLua `LoadString` 编译 chunk，不执行脚本；高亮扫描移至后台线程并以文本版本号丢弃过期结果。签名提示统计嵌套 `()`、`[]`、`{}`，不会把表构造器或内层调用中的逗号误判成外层参数分隔符。
- Windows 自动测试 20 项全部通过；本地 Restore/Build/Test 为 NOT RUN（容器无 .NET SDK）。桌面实机验收与 1MB 文件/DPI/中文日文输入人工验证仍为 NOT RUN。
- 早期有一项签名提示测试因测试夹具逗号位置错误失败；修正夹具后最终 run `43282795272` 通过。前期错误不隐藏，最终状态以该次最新成功运行作为验收依据。
- M3 未创建 Release，v0.1.0-preview.4 Release ZIP/SHA256 未改变。代码折叠不在该 handoff 强制范围，功能矩阵 F04 仍为部分实现；M4 调试器未实现。

### 下一会话唯一任务（M4：Lua 调试器基础）

严格按冻结的 `docs/PRD-CN.md` / `docs/FEATURE-MATRIX-CN.md` 和 `docs/ROADMAP-CN.md` 实施 M4，不得用日志展示或仅显示行号冒充调试能力：
1. 先检查 NLua/KeraLua 可用的真实调试 hook/stack/local APIs 与现有 worker 子进程边界，确定可控的调试协议。
2. 实现真实断点（文件/行号）、单步进入/越过/跳出、继续运行、运行到光标；暂停后确实停在目标 Lua 执行点。
3. 暴露当前栈帧、调用栈、局部变量/可读全局变量观察；不得将 Lua state 从 worker 线程并发交给 UI。
4. UI 提供断点标记、当前执行行、调试控制和变量/调用栈视图；普通播放/停止行为继续工作。
5. 为断点命中、单步次序、嵌套调用栈、变量快照、继续/停止和调试结束清理建立自动化测试，并运行 Windows CI。
6. 任何无法可靠读取或显示的值都要明确标记类型/不可展开/截断原因；不执行任意表达式作为“观察变量”的捷径。
7. M1 preview.4 实机验收及 M3 Windows 编辑器手工验收继续保留为单独 NOT RUN 项，不因 M4 开发而宣称完成。

M3 Windows 桌面手工验收仍为 NOT RUN；代码折叠和独立 API 命令面板仍待后续完成。M1 实机验收继续保留，但不阻塞 M4 独立开发。


## 下一会话接手指令

先核对 `main` 最新 HEAD，再读取本文件、`AGENTS.md`、`README.md`、`docs/ROADMAP-CN.md`。M3 已由 Windows Actions run `43282795272` 验收通过；下一会话唯一开发任务为 M4 Lua 调试器基础，严格按本节 ID/范围/验收条件执行，不重复询问仓库已有答案。直接在 main 小步提交，不创建长期分支或 PR。v0.1.0-preview.4 发布资产不变；M1 和 M3 Windows 桌面手工验收都必须保留为 NOT RUN，CI 不可代替实机验收。
