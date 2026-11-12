# 新 GPT 会话最短接手提示词

继续开发 GitHub 仓库 xiaobei206/anjian（按键精灵 2026）。当前已发布 v0.1.0-preview.4；完整产品目标已经冻结，见 docs/PRD-CN.md、docs/FEATURE-MATRIX-CN.md、docs/ROADMAP-CN.md 和 docs/FUNCTION-REFERENCE-CN.md。

开始前核对 main 最新 HEAD，并读取 AGENTS.md、README.md、docs/SESSION-HANDOFF.md、docs/ROADMAP-CN.md。严格按 handoff 指定的唯一任务继续；不要重复询问仓库里已有答案。默认直接在 main 小步提交，不创建长期分支或 PR。实际构建/测试并如实记录；CI 通过不等于 Windows 桌面实机验收通过。

当前主线的下一开发任务是 M2：实现单一机器可读 API 注册表与函数/参数手册生成机制。必须先盘点当前实际 Lua 绑定，不能把计划函数宣传为已实现；用自动测试保证运行时绑定、API 注册表、编辑器/命令面板数据和手册之间的一致性。完成后更新 handoff、development log、roadmap、功能矩阵和相关规格，再提交 main。

若本会话已读到本提示词，直接读取 SESSION-HANDOFF 的“下一会话唯一任务”，以其中 ID、范围和验收条件为准。
