# 当前会话交接

## 当前状态
- 项目：按键精灵 2026（暂定工作名）
- 仓库：xiaobei206/anjian
- 主分支：main
- 当前版本：0.1.0-preview.1
- 当前阶段：首个 Windows x64 预览版代码、CI 和便携 ZIP 已完成；本次提交会触发 GitHub prerelease 自动创建。
- 实机手工验收：NOT RUN，必须由用户在 Windows 电脑上验证后才能标记通过。

## 用户明确确认的产品决策
- Windows 10/11 x64。
- WinForms + .NET 10 + Lua 5.4。
- Lua 用户脚本采用全局函数调用，例如 MoveTo(122, 100)。
- 录制时合并/降噪鼠标移动，保留必要路径采样点、终点和关键输入事件。
- 首版坐标为虚拟桌面全屏绝对物理像素，允许副屏负坐标，不做窗口相对坐标。
- LeftClick() 默认按下 50ms 后释放；LeftClick(50) 为 50ms；0 表示不增加等待。KeyPress 同样默认 50ms。
- 重复次数默认 1，正整数按完整脚本轮数执行；0 表示无限循环。
- 每轮间隔默认 2000ms，只在两轮之间等待，末轮后不等待；独立于脚本内部 Delay。
- 初版界面以小工具为主，不先做完整 IDE。

## 当前首版实现
- WinForms 主窗口、脚本本地存储、新建/打开/保存/重命名/复制/删除、右键菜单。
- 基础 Lua 编辑器（RichTextBox），Ctrl+S 保存；目前没有语法高亮、补全或断点。
- 全局鼠标/键盘录制，低级 Hook 采集，合并鼠标移动，过滤注入事件和本软件窗口事件，避免键盘自动重复污染；录制结束时补齐尚未释放的按键。
- 录制结果生成 Lua 脚本，保留点击按下时长和动作之间的间隔。
- Lua 5.4 / NLua 1.7.9 执行 worker 子进程；named pipe 将键鼠操作、延时和日志交由主进程执行。
- CPU 无限循环可由主程序强制结束 worker；Stop 时清理本程序仍按下的鼠标/键盘输入。
- Windows SendInput / SetCursorPos 注入，Unicode 文本输入，长文本可在字符边界取消。
- 播放/暂停/停止、重复次数、每轮间隔；设置写入本地 settings.json。
- 0.1.0-preview.1 self-contained win-x64 文件夹 ZIP 和 SHA256 sidecar。

## Windows CI 验证证据
- 最后完整测试提交：fbf208fb42535d330286bb811a0911dd21f82afa
- GitHub Actions run：43222878724，结论 SUCCESS。
- Release build：0 warning / 0 error。
- 自动化测试：7 passed / 0 failed / 0 skipped。
  - 4 项重复次数/轮间隔规则测试。
  - 3 项 Lua 运行时测试：Lua 5.4 版本、C# 全局函数绑定、instruction-count hook。
- win-x64 self-contained publish 成功。
- ZIP 与 SHA256 sidecar 上传成功；Actions artifact id 13947200664。
- 内部应用 ZIP：Anjian2026-0.1.0-preview.1-win-x64.zip
- 内部 ZIP 大小：51,879,713 bytes。
- 内部 ZIP SHA256：baceea2b0c0b2a63783851ff942e049a6d59a8a0a5a23e957a952966a78f4135。
- 已从 Actions artifact 下载解包检查：包含 276 个文件，确认 Anjian.App.exe 和 NLua.dll 存在；内部 ZIP SHA256 与 sidecar 完全一致。
- Release 本次准备通过 commit message 前缀 release: 触发，由 GitHub Actions 创建 tag v0.1.0-preview.1 和 prerelease。

## 已知限制
- 当前是早期预览版，不等于完整 PRD 的全部 P0。
- 编辑器仅基础文本框；没有 Lua 高亮、自动补全、参数提示、断点。
- 没有可视化动作列表/动作编辑器；录制直接生成 Lua 代码。
- 录制基于低级 Windows Hook，需要实机验证长录制和 Hook 生命周期。
- 输入注入受 Windows UIPI/完整性级别限制，不绕过安全桌面或高权限边界。
- 鼠标移动经过采样/合并，不保证逐像素回放；复杂拖拽、多设备键盘行为需要手工验证。
- 尚未完成 2 小时稳定性、DPI 多屏和 Windows 10/11 干净环境手工验收。
- TypeText 支持 Unicode 注入，但不是物理 IME 组合过程的重建。

## 下一步
1. 检查本次 release workflow run。
2. 核验 GitHub Release v0.1.0-preview.1 和两个资产（ZIP、SHA256）。
3. 最后更新本 handoff 写入真实 Release URL 和 release workflow run。
4. 请用户在 Windows 电脑上实测录制、脚本编辑、LeftClick(50)、键盘输入、暂停/停止、100 次循环、无限循环和轮间隔。
5. 根据用户实测问题继续迭代，保持每次小版本提交、CI 和 handoff 完整。

## 新会话接手指令
先读取 AGENTS.md、README.md、docs/SESSION-HANDOFF.md、docs/ROADMAP-CN.md、docs/LUA-API-CN.md 和 docs/USER-GUIDE-CN.md。以 main 最新 HEAD 和当前 GitHub Release 为准；首先确认 Release 资产存在。下一任务是用户实机验收与反馈修复，不要回退到 M0 文档建设或 TV-01 任务。
