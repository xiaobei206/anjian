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
