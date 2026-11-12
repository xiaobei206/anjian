# 当前会话交接

## 当前状态
- 项目：按键精灵 2026（暂定工作名）
- 仓库：xiaobei206/anjian
- 主分支：main
- 已发布版本：**v0.1.0-preview.2**
- Release：https://github.com/xiaobei206/anjian/releases/tag/v0.1.0-preview.2
- 发布提交：`3e63ff38d8111181fa1a402547350c80e6ceb882`
- 发布 Actions run：`43226666516`，结论 SUCCESS。
- 当前开发：新增点击坐标模式、录制暂停/继续、紧凑悬浮窗、实时坐标和可选置顶，代码已写入 main，尚待 Windows CI 编译验证；preview.2 仍是最新正式 Release。通过后发布 preview.3。

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

## 最新 Release 资产：v0.1.0-preview.2
- Release 页面：https://github.com/xiaobei206/anjian/releases/tag/v0.1.0-preview.2
- ZIP：`Anjian2026-0.1.0-preview.2-win-x64.zip`
- ZIP 下载：https://github.com/xiaobei206/anjian/releases/download/v0.1.0-preview.2/Anjian2026-0.1.0-preview.2-win-x64.zip
- ZIP 大小：51,881,918 bytes。
- SHA256 sidecar：`Anjian2026-0.1.0-preview.2-win-x64.zip.sha256`
- SHA256 下载：https://github.com/xiaobei206/anjian/releases/download/v0.1.0-preview.2/Anjian2026-0.1.0-preview.2-win-x64.zip.sha256
- ZIP SHA256：`41139ffeefd695bce3e1f00f232fc8d2fc1cb7b34b67f8bfb53d68019c0bd089`
- 已从 Actions artifact 下载内层 ZIP 和 SHA sidecar；文件大小及 SHA256 三方核对通过（sidecar 与实际 ZIP 计算值一致，GitHub Release asset digest 与 ZIP SHA 一致）。
- Release asset IDs：ZIP `739580318`；SHA256 `739580310`。
- Release ID：`442980321`，prerelease=true。

## 历史 Release：v0.1.0-preview.1
- Release 页面：https://github.com/xiaobei206/anjian/releases/tag/v0.1.0-preview.1
- ZIP 大小：51,879,710 bytes。
- ZIP SHA256：`80bfbf3ddbebafaf6e835bc8fb29148760662418b2c57de79f7ab7dbe88e6164`。
- Release ID：`442961207`，prerelease=true。

## Windows CI 验证
- v0.1.0-preview.2 Release workflow run：`43226666516`，SUCCESS；所有 job/step 均成功。
- 最终快捷键代码提交 `e8e96cb26df1a254d2db45dd2a72b2c7ef11cd9f` 对应 run：`43226322656`，SUCCESS。
- Release 构建、测试、自包含 win-x64 publish、ZIP/SHA256 package、artifact upload、GitHub Release creation：全部 SUCCESS。
- 自动化测试：7 passed / 0 failed / 0 skipped（测试项目现有 7 项）。
- 测试包括：播放次数/轮间隔默认值、100 轮 99 次间隔、0 无限循环、边界值校验；Lua 5.4 版本确认、Lua 调用 C# 全局函数、Lua instruction-count hook 触发。
- self-contained win-x64 publish：SUCCESS。
- ZIP/SHA256 package：SUCCESS。
- Release ZIP SHA256 已通过实际解包 Actions artifact 并运行 `sha256sum -c` 核验。
- 正式 prerelease v0.1.0-preview.2 已成功创建。

## 仍未完成（务必如实）
- 用户 Windows 实机手工验收：NOT RUN。
- 2 小时稳定性测试：NOT RUN。
- Windows 10/11 干净环境、DPI、多显示器负坐标、复杂拖拽及具体目标程序权限兼容：NOT RUN。
- 录制采用低级 Hook，需在用户电脑确认 Hook 采集工作正常、不会漏录或重复。
- 编辑器仍为基础文本框，没有语法高亮、补全、参数提示、断点。
- 没有可视化动作列表/动作参数编辑 UI，录制结束直接生成 Lua。
- 不支持图像识别/OCR、窗口相对坐标、后台窗口注入、插件或云同步。

## 下一步唯一任务
用户下载并在 Windows 10/11 电脑实测：
1. 启动、创建/保存/重命名/复制/删除脚本。
2. 全局录制鼠标移动、左右中键、滚轮、键盘和长按，结束录制后生成 Lua。
3. 手写 `MoveTo`、`LeftClick(50)`、`KeyPress`、`Hotkey`、`TypeText`、`Delay` 测试。
4. 重复次数设置 100，确认完整执行 100 轮；每轮间隔 0/1000/2000ms，确认只在两轮之间发生等待。
5. 设置 0 无限循环并执行 Stop；测试 CPU 无限循环 Stop、Delay 中 Stop、Pause/Resume 和输入释放。
6. 实机验证 F10/F11/F12 全局快捷键：应用最小化或焦点在目标应用时分别启动、暂停/继续、停止；验证 F12 停止后键鼠释放，空闲时 F12 仍可交给浏览器/前台程序。若钩子安装失败，收集日志和 Win32 错误码。
7. 把实际错误日志、具体复现步骤和目标程序环境交给下一会话，按小版本迭代修复。

## 下一会话接手指令
先读 AGENTS.md、README.md、本文件、docs/ROADMAP-CN.md、docs/LUA-API-CN.md 和 docs/USER-GUIDE-CN.md。先核对当前 main HEAD 与 Release，不要假设旧版本状态。优先处理用户实机反馈；除非反馈明确要求，不要擅自重写初版 UI 或扩大范围。
