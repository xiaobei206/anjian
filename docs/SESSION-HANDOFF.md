# 当前会话交接

## 当前状态
- 项目：按键精灵 2026（暂定工作名）
- 仓库：xiaobei206/anjian
- 主分支：main
- 已发布版本：**v0.1.0-preview.4**
- Release：https://github.com/xiaobei206/anjian/releases/tag/v0.1.0-preview.4
- Release commit：`08886d9e66c08ba24f6f1b7d9e91bbdaeba8a9e0`
- 发布 Actions run：`43253611158`，结论 SUCCESS。
- 最新产品代码/release commit：`08886d9e66c08ba24f6f1b7d9e91bbdaeba8a9e0`。
- main 分支可能继续产生文档提交；新 GPT 接手时必须重新查询 main 最新 HEAD，不要把 handoff 中的历史提交 SHA 当成分支当前头部。
- 当前阶段：preview.4 的 CI/发布已完成；下一步优先 Windows 实机验收、2 小时稳定性、DPI/多显示器及输入 Hook 验证，不要默认用户已经完成验收。

## 完整产品目标已冻结（2026-11-13）
- 产品范围基线：[docs/PRD-CN.md](PRD-CN.md)。
- 完整功能矩阵（当前状态、阶段、验收条件）：[docs/FEATURE-MATRIX-CN.md](FEATURE-MATRIX-CN.md)。
- 中文函数与参数参考手册：[docs/FUNCTION-REFERENCE-CN.md](FUNCTION-REFERENCE-CN.md)。
- API 当前实现契约：[docs/LUA-API-CN.md](LUA-API-CN.md)。
- 阶段路线图：[docs/ROADMAP-CN.md](ROADMAP-CN.md)。
- 完整 IDE、窗口/坐标、图色/OCR、参数 UI、小精灵、插件及函数手册均为正式目标，不得再从大纲中删除或只标为可选远期项目。
- 下一项开发任务是 M2：先建立机器可读 API 注册表和文档生成/一致性测试，再以它驱动编辑器提示、命令面板和校验。


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
- 基础 Lua 文本编辑器，Ctrl+S 保存。
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
- 编辑器仍为基础文本框，没有语法高亮、补全、参数提示、断点。
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

## M1 实机验收待办（仍为 NOT RUN，不阻塞 M2 文档/API 工程）
用户下载 v0.1.0-preview.4，在 Windows 10/11 实机验收：
1. 默认启动确认只显示悬浮窗与托盘图标，任务栏没有主窗口按钮。
2. 点击悬浮窗“完整界面”打开主窗口，确认悬浮窗一直留在桌面；关闭主窗口右上角 × 后，主窗口任务栏按钮消失，悬浮窗和托盘仍在。
3. 通过托盘右键菜单分别打开主窗口、显示/隐藏悬浮窗和退出；双击托盘图标应打开主窗口。若悬浮窗已隐藏，关闭主窗口后应只剩托盘图标，不应自动弹出悬浮窗。
4. 确认主窗口“返回悬浮窗”按钮存在且有效；点击后主窗口隐藏，任务栏按钮消失。
5. 在悬浮窗修改循环次数和轮间隔，在主窗口修改后反向确认同步；确认 0=无限、设置重启后持久化。
6. 检查 EXE 文件图标、任务栏主窗口图标、托盘图标和悬浮窗图标是否一致，16/32px 尺寸下是否清晰。
7. 回归只录点击、录制暂停/续录、F10/F11/F12、播放/停止和原有 Lua API；将失败步骤、截图和日志交给下一会话。

## 当前会话 M2 实施状态（CI 待确认）

- 代码提交：`0f7a80c24f18789833d7fbb92e4979da65ed261d`。唯一 API 来源：`src/Anjian.Core/Api/lua-api-registry.json`，当前含 22 个实际实现的公开函数。
- 运行时注册、手册生成器和 API 契约测试已接入；候选窗口/找图/OCR/后台能力仍仅属于目标设计。
- 生成/检查：`dotnet run --project tools/Anjian.ApiDocsGenerator -c Release -- --write` / `--check`。
- 本环境没有 .NET SDK；本地 Restore/Build/Test 为 NOT RUN。请以 Windows Actions 的实际结果为准，不得伪称本地通过。
- Windows 10/11 实机、DPI/多显示器、Hook 与 2 小时稳定性验收仍未执行；CI 不代替实机验收。

### 下一会话唯一任务（M2 的 CI 确认）
查询本次提交的 Windows Actions。若 Restore/Build/Test/API 文档检查/发布打包任一步失败，只修复失败项并重跑；全部成功后更新日志与路线图为 DONE，并将唯一开发任务推进到 M3。

## 下一会话接手指令

先核对最新 main HEAD。当前 M2 代码已提交，先检查本提交的 Windows Actions；失败则仅修复失败项，全部成功后更新 handoff 将唯一开发任务推进至 M3。产品大纲已冻结，不要重做。preview.4 Windows 实机验收仍未完成，须保留 M1 清单并如实记录。所有计划中的函数均保持“目标设计”，直到代码和测试确实存在。
