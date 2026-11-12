# 当前会话交接

## 当前状态
- 项目：按键精灵 2026（暂定工作名）
- 仓库：xiaobei206/anjian
- 主分支：main
- 已发布版本：**v0.1.0-preview.1**
- Release：https://github.com/xiaobei206/anjian/releases/tag/v0.1.0-preview.1
- 发布提交：cf24ac9292ea21dd1063ea9e9d65f18e3637442a
- 发布 Actions run：43224009158，结论 SUCCESS。
- 当前任务：用户 Windows 实机试用与反馈；不要重建文档框架，不要把项目说成尚未编码。

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
- self-contained win-x64 ZIP 和 SHA256 发布资产。

## 正式 Release 资产
- Release 页面：https://github.com/xiaobei206/anjian/releases/tag/v0.1.0-preview.1
- ZIP：`Anjian2026-0.1.0-preview.1-win-x64.zip`
- ZIP 下载：https://github.com/xiaobei206/anjian/releases/download/v0.1.0-preview.1/Anjian2026-0.1.0-preview.1-win-x64.zip
- ZIP 大小：51,879,710 bytes。
- SHA256 sidecar：`Anjian2026-0.1.0-preview.1-win-x64.zip.sha256`
- SHA256 下载：https://github.com/xiaobei206/anjian/releases/download/v0.1.0-preview.1/Anjian2026-0.1.0-preview.1-win-x64.zip.sha256
- ZIP SHA256：`80bfbf3ddbebafaf6e835bc8fb29148760662418b2c57de79f7ab7dbe88e6164`
- 已下载 Actions artifact 并解包：其中 ZIP 为 51,879,710 bytes；实际 sha256 与 sidecar 及 GitHub Release asset digest 三方一致。
- Release asset IDs：ZIP `739527441`；SHA256 `739527433`。
- Release ID：`442961207`，prerelease=true。

## Windows CI 验证
- Release workflow run：43224009158，SUCCESS。
- Release build：0 warnings / 0 errors。
- 自动化测试：7 passed / 0 failed / 0 skipped。
- 测试包括：播放次数/轮间隔默认值、100 轮 99 次间隔、0 无限循环、边界值校验；Lua 5.4 版本确认、Lua 调用 C# 全局函数、Lua instruction-count hook 触发。
- self-contained win-x64 publish：SUCCESS。
- ZIP/SHA256 package：SUCCESS。
- Add-Content 写入 GitHub Actions output 变量修复已通过，正式 prerelease 已成功创建。

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
6. 把实际错误日志、具体复现步骤和目标程序环境交给下一会话，按小版本迭代修复。

## 下一会话接手指令
先读 AGENTS.md、README.md、本文件、docs/ROADMAP-CN.md、docs/LUA-API-CN.md 和 docs/USER-GUIDE-CN.md。先核对当前 main HEAD 与 Release，不要假设旧版本状态。优先处理用户实机反馈；除非反馈明确要求，不要擅自重写初版 UI 或扩大范围。
