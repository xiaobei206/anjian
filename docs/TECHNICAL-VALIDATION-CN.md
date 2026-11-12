# 技术选型与 PoC 验证清单

- 当前可运行版本：v0.1.0-preview.4。
- 本文区分“已采用并验证”“已采用但待实机验收”“未来候选”，旧 PoC 计划不代表当前依赖仍未选型。

## 1. 当前技术路线

| 层 | 当前候选 | 状态 |
|---|---|---|
| Framework | .NET 10 LTS / net10.0-windows | 已确认目标大版本 |
| UI | WinForms | 已确认方向 |
| Lua | Lua 5.4 | 已确认语言版本 |
| Lua/.NET 绑定 | NLua 1.7.9 / Lua 5.4 | 已采用；Lua 版本、C# 注册函数、指令计数钩子在 Windows CI 测试通过 |
| 代码编辑器 | WinForms RichTextBox | 当前基础编辑器；无高亮/补全，后续可做 Scintilla PoC |
| 输入 | Windows SendInput | 已确定首版前台输入方向 |
| 录制 | WH_MOUSE_LL / WH_KEYBOARD_LL 低级钩子 | 当前已实现；实际 Windows Hook 稳定性待用户验收；Raw Input 是备选改进方向 |
| 图像 | 截屏 + 模板匹配 | P1，暂不选库 |

禁止只凭 NuGet 包页面或 README 宣称组件兼容。每个候选都要在真实 net10.0-windows x64 WinForms 小程序中构建、运行、发布并验证。

## 2. .NET 版本

采用 .NET 10 LTS。微软生命周期表列出的支持结束时间为 2028-11-14（日期以微软当前生命周期页面为准）。不以 .NET 9/8 作为新项目目标。

官方：
- https://learn.microsoft.com/zh-cn/lifecycle/products/microsoft-net-and-net-core
- https://learn.microsoft.com/dotnet/core/releases-and-support

## 3. Lua 运行时 PoC（任务 TV-01）

### 当前验证结论（preview.4）
- NLua 1.7.9 已接入项目，脚本运行在独立 worker 子进程中，使用 named pipe 与主进程通信。
- Windows CI 已验证 Lua 5.4 运行时、Lua 调用 C# 注册的全局函数，以及 instruction-count hook。
- 当前 7 项自动化测试覆盖播放选项/循环间隔和 Lua 运行时基础行为。
- 尚未由此证明 2 小时稳定性、所有异常路径或恶意脚本隔离；本机脚本环境不是安全沙箱。

候选：
- Lua 5.4 官方手册：https://www.lua.org/manual/5.4/
- NLua：https://github.com/NLua/NLua
- KeraLua：https://github.com/NLua/KeraLua

必须验证：
1. NuGet 包的当前版本、许可证、上游维护状态和支持的 TFM。
2. Windows x64 self-contained 发布包含所需 native assets。
3. Lua 5.4 的版本确认（不能误用 Lua 5.3/5.2）。
4. C# 注册 Mouse、Keyboard 等模块。
5. Lua 错误能返回文件名、行号和堆栈。
6. lua_sethook / instruction count hook 可否通过当前绑定稳定调用。
7. CPU 无限循环在指令钩子中可检测取消。
8. Delay(60000) 可在停止请求后立即退出。
9. Pause/Resume 在安全检查点生效。
10. 不同运行实例 Lua state 隔离；一个 state 不被多线程访问。
11. 限制标准库后，API 可正常工作。
12. native crash/异常行为及进程退出清理有记录。

若 NLua 高层 API 无法可靠提供需要的指令钩子能力，验证直接通过 KeraLua 包装必要 C API 的可行性；只有 PoC 证明确有必要才自建更薄的 Lua Host。不要一开始自行维护整个 Lua P/Invoke 层。

## 4. 编辑器组件 PoC（任务 TV-02）

候选比较：
- Scintilla.NET / Scintilla5.NET 相关维护分支。
- FastColoredTextBox 等 WinForms 编辑器控件。
- AvalonEdit（WPF 控件，若使用需评估 ElementHost 与 Dock 交互成本）。
- 其他有清晰许可证、持续维护且支持 .NET 10 的组件。

验证项：
1. net10.0-windows WinForms 项目直接引用与启动。
2. UTF-8 中文、日文、emoji 的显示和输入。
3. Lua lexer、高亮、行号、括号匹配。
4. 自动补全和自定义参数提示可扩展。
5. 错误行标记、代码折叠、查找/替换。
6. 大文件（至少 1 MB）编辑性能。
7. 125/150/200% DPI 显示。
8. 原生 DLL 在 self-contained ZIP 中的加载与许可证。
9. 组件在主窗体关闭时释放，无 native handle 泄漏迹象。

输出：对比表、最小 POC 工程、构建日志、许可证链接、推荐结果。未经验证不锁定组件。

## 5. Windows 输入 PoC（任务 TV-03）

### 当前验证结论（preview.4）
- 输入注入已采用 SendInput / SetCursorPos，并维护按下状态，在停止/退出路径执行释放。
- 录制和全局快捷键已采用低级 Windows Hook；CI 只验证编译，不等于 Hook 实机稳定性通过。
- Raw Input 不属于当前实现；如实机测试出现漏录、重复或 Hook 被系统移除，再依据日志和复现条件开展替换评估。

官方文档：
- SendInput：https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-sendinput
- Raw Input：https://learn.microsoft.com/en-us/windows/win32/inputdev/raw-input
- LowLevelKeyboardProc：https://learn.microsoft.com/en-us/windows/win32/winmsg/lowlevelkeyboardproc
- SetWindowsHookEx：https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-setwindowshookexw

验证项：
1. 普通权限下鼠标移动/点击/按键事件的成功率。
2. SendInput 返回值与部分注入失败的处理。
3. UIPI 对更高完整性级别目标的限制和用户提示。
4. Unicode 文本输入、中英文、日文和组合字符。
5. 物理键、虚拟键、扫描码和扩展键映射。
6. 多显示器虚拟桌面范围、负坐标。
7. Per-Monitor V2 DPI awareness 后的坐标准确性。
8. 停止时 ReleaseAllOwnedInputs 的可靠性。
9. Raw Input 的后台接收、设备标识和事件数据完整度。
10. 低级钩子的回调时限、消息循环、hook 安装/卸载和注入标记。
11. 回放事件是否会被录制器重复捕获。
12. 录制本软件界面操作的排除方案。

微软指出低级钩子回调超时可能被静默移除，并建议在专用线程上快速转交处理；不少场景应优先评估 Raw Input。因此先测 Raw Input，再评估是否必须增加 Hook。

## 6. 便携发布 PoC（任务 TV-04）

验证：
- dotnet publish -c Release -r win-x64 --self-contained true。
- 是否依赖 VC++ Runtime 或其他 native runtime。
- 原生 Lua/编辑器 DLL 是否随包发布。
- ZIP 解压路径含中文、空格时可启动。
- 不联网也能启动。
- 无管理员权限启动。
- 用户配置与工程数据不写入应用安装目录以外的意外位置。
- 升级时用户工程和配置不丢失。

## 7. PoC 交付标准

每个 PoC 必须交付：
- 使用了哪些 SDK / NuGet 精确版本。
- 许可证和上游地址。
- 复现步骤。
- 构建和运行输出。
- 已通过/未通过项目。
- 已知限制。
- 最终建议：采用 / 淘汰 / 继续调查。
- 更新 DECISIONS-CN.md 和路线图任务状态。

## 8. 外部信息更新规则

SDK、NuGet、Windows API、许可证和支持周期可能变化。实施前再次查看上游与官方页面。不要把聊天中旧版本号当作事实，也不要仅依赖搜索摘要。
