# 按键精灵 2026（暂定项目名）

Windows 桌面自动化软件。独立实现经典按键精灵式工作流，采用 Lua 脚本引擎、自定义自动化命令、动作录制与代码编辑双模式。

> 当前阶段：需求与架构冻结准备。尚未开始功能编码。所有新会话必须先阅读 `AGENTS.md`、`docs/SESSION-HANDOFF.md` 和 `docs/ROADMAP-CN.md`。

## 从哪里开始

1. [`AGENTS.md`](AGENTS.md)：每个 GPT / 开发会话必须遵守的协作规则。
2. [`docs/SESSION-START-PROMPT-CN.md`](docs/SESSION-START-PROMPT-CN.md)：新会话最短接手提示词。
3. [`docs/SESSION-HANDOFF.md`](docs/SESSION-HANDOFF.md)：当前唯一有效的交接状态。
4. [`docs/PRD-CN.md`](docs/PRD-CN.md)：产品需求和首版范围。
5. [`docs/ARCHITECTURE-CN.md`](docs/ARCHITECTURE-CN.md)：技术架构和模块边界。
6. [`docs/LUA-API-CN.md`](docs/LUA-API-CN.md)：脚本语法及命令 API 草案。
7. [`docs/RECORDER-DESIGN-CN.md`](docs/RECORDER-DESIGN-CN.md)：录制器和动作/代码双模式设计。
8. [`docs/ACCEPTANCE-TEST-CN.md`](docs/ACCEPTANCE-TEST-CN.md)：验收标准。
9. [`docs/ROADMAP-CN.md`](docs/ROADMAP-CN.md)：可分配给单个会话的开发任务。
10. [`docs/DECISIONS-CN.md`](docs/DECISIONS-CN.md)：已确认决策及待验证事项。
11. [`docs/DEVELOPMENT-LOG-CN.md`](docs/DEVELOPMENT-LOG-CN.md)：累计开发日志。

## 核心约束

- 首版只做 Windows 10/11 x64 桌面自动化；不做 Android、云同步和旧版 Q 脚本兼容。
- 产品工作名为“按键精灵 2026”；这是独立实现，不使用原产品闭源代码或未经授权的素材。
- 目标技术路线：C# / .NET 10 LTS、WinForms、Lua 5.4、自定义命令 API。具体 NuGet 包和编辑器组件必须先做验证，不允许仅凭名称或版本号假定兼容。
- 先做文档与技术验证，再开始功能代码；首版 P0 以可测试的验收标准为准。
- 每个会话只承担一个明确里程碑，结束前必须更新 handoff、开发日志和路线图，并提交 Git。
- `main` 是当前唯一长期集成分支；保持小步、可回滚提交。未经用户明确要求，不创建长期开发分支或 PR 流程。

## 当前状态

- 仓库已初始化。
- 文档框架正在建立。
- 没有应用代码、解决方案文件或可运行构建。
- 当前下一步：完成文档首版，之后进行 Lua 绑定、编辑器和 Windows 输入/录制方案的技术验证。
