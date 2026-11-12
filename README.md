# 按键精灵 2026（暂定项目名）

Windows 桌面自动化软件。独立实现经典按键精灵式工作流，采用 Lua 5.4 脚本引擎、自定义自动化命令、动作录制与代码编辑双模式。

> 当前阶段：M0 文档与协作框架已建立；尚未开始正式应用编码。最新状态以 docs/SESSION-HANDOFF.md 为准。

## 新会话从这里开始

1. [AGENTS.md](AGENTS.md)：所有 GPT / 开发会话必须遵守的协作规则。
2. [最短接手提示词](docs/SESSION-START-PROMPT-CN.md)：复制到新 GPT 对话即可接手。
3. [当前交接状态](docs/SESSION-HANDOFF.md)：当前唯一有效状态和下一会话唯一任务。
4. [会话协议](docs/SESSION-PROTOCOL-CN.md)：如何分配任务、使用上下文和完成交接。
5. [开发路线图](docs/ROADMAP-CN.md)：所有里程碑和任务 ID。

## 产品与技术规范

- [产品需求 PRD](docs/PRD-CN.md)
- [技术架构与模块边界](docs/ARCHITECTURE-CN.md)
- [Lua 5.4 命令 API](docs/LUA-API-CN.md)
- [录制器与双模式设计](docs/RECORDER-DESIGN-CN.md)
- [首版验收清单](docs/ACCEPTANCE-TEST-CN.md)
- [依赖选型与 PoC 验证](docs/TECHNICAL-VALIDATION-CN.md)
- [架构决策记录](docs/DECISIONS-CN.md)
- [累计开发日志](docs/DEVELOPMENT-LOG-CN.md)

## 核心约束

- 首版只做 Windows 10/11 x64 桌面自动化；不做 Android、云同步和旧版 Q 脚本兼容。
- “按键精灵 2026”为项目工作名；这是独立实现，不使用原产品闭源代码或未经授权的素材。
- 目标技术路线：C# / .NET 10 LTS、WinForms、Lua 5.4、自定义命令 API。具体 NuGet 包和编辑器组件必须先做验证，不允许仅凭名称或版本号假定兼容。
- 动作列表可以生成 Lua 代码；不承诺任意 Lua 代码无损反向转换为动作列表。
- 先做技术 PoC，再建立正式解决方案和功能代码。
- 每个 GPT 会话只执行 handoff 中指定任务；结束前更新 handoff、开发日志、路线图并提交 Git。
- main 是唯一长期集成分支；保持小步、可回滚提交。

## 当前仓库结构

    AGENTS.md
    README.md
    docs/
      PRD-CN.md
      ARCHITECTURE-CN.md
      LUA-API-CN.md
      RECORDER-DESIGN-CN.md
      ACCEPTANCE-TEST-CN.md
      TECHNICAL-VALIDATION-CN.md
      ROADMAP-CN.md
      DECISIONS-CN.md
      SESSION-PROTOCOL-CN.md
      SESSION-HANDOFF.md
      SESSION-START-PROMPT-CN.md
      DEVELOPMENT-LOG-CN.md
    src/README.md
    tests/README.md

src/ 和 tests/ 目前只有结构说明，没有正式项目文件、应用代码或可运行构建。

## 下一步

按 docs/SESSION-HANDOFF.md 执行 TV-01：Lua 5.4 / .NET 10 绑定与取消机制 PoC。
