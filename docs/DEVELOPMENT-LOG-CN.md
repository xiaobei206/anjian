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
