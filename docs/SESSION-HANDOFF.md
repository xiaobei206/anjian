# 当前会话交接

## 当前状态
- 项目：按键精灵 2026（暂定工作名）
- 仓库：xiaobei206/anjian
- 主分支：main
- 已发布版本：**v0.1.0-preview.3**
- Release：https://github.com/xiaobei206/anjian/releases/tag/v0.1.0-preview.3
- Release commit：`3938b1550f32cb98511b67cfb5642e6eaf78f8fe`
- 发布 Actions run：`43232898372`，结论 SUCCESS。
- 最新任务：preview.4 独立悬浮窗、托盘生命周期和应用图标已进入代码；Windows CI 与实机验收待完成。
- 最新代码提交：`7c10cd9f3f396afbca1aceccd990c7128c90280c`。
- Windows CI run：`43253075450`，SUCCESS；Restore/Build/Test/Publish/Package/Artifact upload 全部成功。

## 本轮新增需求与实现目标
- 默认勾选“只记录点击坐标”：鼠标移动不生成 MoveTo 轨迹；点击使用真实按下坐标，生成 MoveTo -> 可选 Delay -> Click。
- 录制按钮状态循环：开始录制 -> 暂停录制 -> 继续录制；单独结束录制，暂停不清空前段事件，暂停时长不进入脚本时间线。
- 默认启动为紧凑悬浮窗：小型录制/播放控制、当前脚本名、实时 X/Y；顶部可拖动，双击/按钮进入完整模式。
- 可选始终置顶，设置本地持久化。
- 验收时检查切换模式、录制续录、点击坐标和置顶持久化；尚未发布/实机验证前不得声称通过。

## 用户确认的产品决策
- Windows 10/11 x64。
- WinForms + .NET 10 + Lua 5.4。
- Lua 全局函数：`MoveTo(122, 100)`。
- 录制合并/降噪鼠标移动，保留必要路径点、终点和关键输入事件。
- 坐标为虚拟桌面全屏绝对物理像素，副屏负坐标合法；不提供窗口相对坐标。
- `LeftClick()` 默认按下 50ms；`LeftClick(50)` 按住 50ms 后释放；`LeftClick(0)` 不增加等待。右键、中键、KeyPress 同样默认 50ms。
- 重复次数默认 1；正整数按完整脚本轮数；0=无限循环。
- 每轮间隔默认 2000ms，只在两轮之间等待，最后一轮后不等待；与脚本内部 Delay 独立。
- 初版先做轻量小工具，而非完整 IDE。

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

## 最新 Release 资产：v0.1.0-preview.3
- Release 页面：https://github.com/xiaobei206/anjian/releases/tag/v0.1.0-preview.3
- ZIP：`Anjian2026-0.1.0-preview.3-win-x64.zip`
- ZIP 下载：https://github.com/xiaobei206/anjian/releases/download/v0.1.0-preview.3/Anjian2026-0.1.0-preview.3-win-x64.zip
- ZIP 大小：51,887,704 bytes。
- SHA256 sidecar：`Anjian2026-0.1.0-preview.3-win-x64.zip.sha256`
- SHA256 下载：https://github.com/xiaobei206/anjian/releases/download/v0.1.0-preview.3/Anjian2026-0.1.0-preview.3-win-x64.zip.sha256
- ZIP SHA256：`b18071d5ecfb05435198e5ee900e59cc861ef7706328b413bee2412650acf188`
- Actions artifact 已下载并解包；运行 `sha256sum -c` 成功，sidecar、实际 ZIP 和 GitHub Release ZIP digest 一致。
- Release asset IDs：ZIP `739709384`；SHA256 `739709386`。
- Release ID：`443028031`，prerelease=true。

## 历史 Release：v0.1.0-preview.1
- Release 页面：https://github.com/xiaobei206/anjian/releases/tag/v0.1.0-preview.1
- ZIP 大小：51,879,710 bytes。
- ZIP SHA256：`80bfbf3ddbebafaf6e835bc8fb29148760662418b2c57de79f7ab7dbe88e6164`。
- Release ID：`442961207`，prerelease=true。

## Windows CI 验证
- v0.1.0-preview.3 Release workflow run：`43232898372`，SUCCESS；所有 job/step 均成功。
- 修复空值边界后的 CI run：`43232594517`，SUCCESS。
- 最终 Release 构建、7 项自动化测试、自包含 win-x64 publish、ZIP/SHA256 package、artifact upload、GitHub Release creation：全部 SUCCESS。
- 自动化测试：7 passed / 0 failed / 0 skipped（测试项目现有 7 项）。
- 测试包括：播放次数/轮间隔默认值、100 轮 99 次间隔、0 无限循环、边界值校验；Lua 5.4 版本确认、Lua 调用 C# 全局函数、Lua instruction-count hook 触发。
- self-contained win-x64 publish：SUCCESS。
- ZIP/SHA256 package：SUCCESS。
- Release ZIP SHA256 已通过实际解包 Actions artifact 并运行 `sha256sum -c` 核验。
- 正式 prerelease v0.1.0-preview.3 已成功创建。
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
- 当前代码尚未经过 Windows CI 编译、自动化测试和 Windows 实机验收，不能标记为通过。

## 下一步唯一任务
1. 确认 preview.4 Release workflow 成功创建 tag 和 prerelease。
2. 核实版本号、ZIP 与 SHA256 对应同一个 preview.4，并更新本交接文件。
3. 交由用户进行 Windows 实机验收；未经手工验证的界面/托盘行为不得标记 PASS。
4. Windows 实机检查：默认启动只显示悬浮窗和托盘；主窗口打开时悬浮窗仍在；主窗口关闭后任务栏按钮消失，托盘仍可重新打开主窗口。
5. 测试托盘右键菜单、双击托盘、隐藏/显示悬浮窗、明确退出；检查进程与 Hook、NotifyIcon 是否正确释放。
6. 检查悬浮窗次数/间隔与主窗口双向同步、0 无限、设置持久化；验证录制和播放时状态同步。
7. 完成 preview.3 原有点击录制、暂停续录、F10/F11/F12 与播放/停止手工回归。

## 下一会话接手指令
先读 AGENTS.md、README.md、本文件、docs/ROADMAP-CN.md、docs/LUA-API-CN.md 和 docs/USER-GUIDE-CN.md。先核对当前 main HEAD 与 Release，不要假设旧版本状态。优先处理用户实机反馈；除非反馈明确要求，不要擅自重写初版 UI 或扩大范围。
