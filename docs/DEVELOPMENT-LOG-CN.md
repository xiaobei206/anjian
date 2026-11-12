# 累计开发日志

本文件按时间追加记录，不覆盖历史。当前状态以 SESSION-HANDOFF.md 为准。

## 2026-11-12 — S000 — 仓库初始化与文档框架

### 目标
在 xiaobei206/anjian 建立按键精灵 2026 的唯一长期项目资料库和 GPT 会话接力机制；不开始功能编码。

### 已完成
- 核实仓库为 public，默认分支 main，连接权限为 admin/push。
- 创建 README.md，作为文档导航和项目入口。
- 建立 AGENTS.md，约定 GPT 会话开始、Git、验证、范围控制和强制 handoff 流程。
- 建立 PRD：Windows 首版产品范围、P0/P1/P2、用户流程和完成定义。
- 建立 ARCHITECTURE：项目拆分、依赖方向、线程模型、Lua 取消和输入边界。
- 建立 LUA-API：Lua 5.4 与 Mouse / Keyboard / Delay / Log API 草案。
- 建立 RECORDER-DESIGN：Raw Input/Hook PoC、动作模型、双模式同步和数据保存规则。
- 建立 ACCEPTANCE-TEST：P0 功能、录制、停止、稳定性、DPI 和发布验收。
- 建立 TECHNICAL-VALIDATION：Lua 绑定、编辑器、输入采集和便携发布 PoC。
- 建立 ROADMAP、DECISIONS、SESSION-PROTOCOL、SESSION-HANDOFF 和最短接手提示词。

### 技术核对记录
- Microsoft 生命周期资料将 .NET 10 列为 LTS，支持到 2028 年 11 月（生命周期页面为准）。
- Microsoft LowLevelKeyboardProc 文档提醒钩子回调必须迅速返回；Windows 可能静默移除超时的低级钩子，并建议考虑 Raw Input。
- 因此录制器不预先锁定低级钩子，先做 Raw Input 和 Hook 对比 PoC。
- Lua 绑定与编辑器组件均未锁定具体 NuGet 版本；必须先实测 .NET 10、Lua 5.4、原生依赖、许可证和 self-contained 发布。

### Git
- 初始化 README 提交：c6b4cf436e3de87accb95650a5e7d52321336161
- 本轮文档批次 commit：完成后补充准确 SHA。

### 验证
- GitHub 仓库元数据、默认分支和写权限：已核实。
- README 初始提交：GitHub 返回成功。
- 文档内容：提交后需从 main 复读检查。
- 应用构建/测试：NOT RUN（本轮没有应用代码，符合范围）。

### 下一步
TV-01：Lua 5.4 / .NET 10 绑定与中断机制 PoC。
