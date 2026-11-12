# 当前会话交接（唯一有效状态）

> 本文件每个会话结束时整体更新。历史过程写在 DEVELOPMENT-LOG-CN.md。

## 当前状态

- 项目：按键精灵 2026（暂定名）
- 仓库：xiaobei206/anjian
- 主分支：main
- 当前阶段：M0 文档框架建立；尚无应用代码
- 当前版本：未发布（不要创建虚假版本/发布资产）
- 最新已知提交：首个 README 初始化提交 c6b4cf436e3de87accb95650a5e7d52321336161；本轮文档批次提交后应将本字段更新为最新 SHA。

## 当前会话

- 会话编号：S000
- 任务：BOOT-01 至 BOOT-05，初始化仓库规范、需求文档和 GPT 会话交接机制。
- 已完成：首版 README；PRD、架构、Lua API、录制设计、验收和技术 PoC 清单；AGENTS 协作规则。
- 仍需完成：路线图、决策记录、会话协议、短提示词、开发日志、目录说明；完成后提交并复核。
- 没有编写功能代码，没有创建 .sln，也没有声称应用可运行。

## 已确认产品方向

- 首版 Windows 10/11 x64。
- WinForms + .NET 10 LTS。
- Lua 5.4 + 自定义自动化命令 API。
- 动作列表和 Lua 代码两种模式；不承诺任意代码无损双向转换。
- 便携版优先，本地文件存储。
- P0：前台键鼠输入、录制、动作列表、Lua 编辑/执行、暂停/恢复/停止、日志与可靠保存。
- 旧版 Q 语言兼容、Android、OCR、后台消息注入、云服务不在首版范围。

## 当前文档事实

- PRD：首版功能分级和用户流程已列出。
- Architecture：模块边界、依赖方向、线程模型、存储约定已列出。
- Lua API：Mouse / Keyboard / Delay / Log 的 v0.1 草案已列出。
- Recorder：事件模型、动作模型、代码生成区块和 dirty/detached 规则已列出。
- Acceptance：A/B/C/D 测试表和 P0 门槛已列出。
- Technical Validation：Lua、编辑器、输入采集、便携发布 PoC 清单已列出。
- 具体 Lua NuGet 绑定和编辑器组件尚未冻结。
- 录制采集源优先验证 Raw Input，再决定是否需要低级钩子。
- 首版先采用同进程独立执行线程，不先引入独立 Worker 进程。

## 已知注意事项

1. Windows SendInput 受 UIPI/完整性级别限制，不承诺能操作所有进程或受保护桌面。
2. 中文/日文物理输入法过程不能仅凭物理键盘事件无损重建；TypeText 单独定义 Unicode 注入能力。
3. 低级键盘/鼠标 Hook 有回调时限，超时可能被 Windows 静默移除。
4. 动作列表生成 Lua 后，用户手改 generated block 必须标记不同步，重新生成前预览并确认。
5. 所有技术依赖的当前版本、TFM、许可证和原生运行时都要在 PoC 中实测。

## 下一会话唯一任务

- 任务 ID：TV-01
- 任务名称：Lua 5.4 / .NET 10 绑定与中断机制 PoC
- 目标：验证 NLua/KeraLua 候选能否稳定运行 Lua 5.4、暴露自动化 API、返回行号，并实现 CPU 无限循环取消与可取消 Delay。
- 范围内：建立独立最小 PoC；检查 NuGet 当前版本、TFM、许可证、Lua 版本和 win-x64 self-contained 发布；验证 lua_sethook/instruction hook；形成结论。
- 范围外：正式桌面 UI、最终脚本引擎、录制器、鼠标键盘正式实现、编辑器控件选择。
- 建议输出目录：poc/LuaRuntime/（如 PoC 已达最小必要，按需创建，不要先创建整个正式解决方案）。
- 必须更新：TECHNICAL-VALIDATION-CN.md、DECISIONS-CN.md、ROADMAP-CN.md、DEVELOPMENT-LOG-CN.md、SESSION-HANDOFF.md。
- 验收：技术验证清单第 3 节有逐项结果；关键取消目标有重复实测数据；所有包版本和来源写明。
- 完成后下一任务：TV-02 编辑器组件 PoC（除非 TV-01 暴露架构阻断）。

## 给下一会话的执行指令

先读取 AGENTS.md、README.md、本文件、ROADMAP-CN.md 和 TECHNICAL-VALIDATION-CN.md。只执行 TV-01。基于最新 main 工作；完成实际 PoC、构建和测试；如需调整方案，记录证据并更新 ADR；最后更新 handoff / log / roadmap 并提交 main。不要重做产品方向讨论，也不要开始正式应用 UI。
