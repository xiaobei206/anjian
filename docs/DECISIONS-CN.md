# 架构决策记录（ADR）

状态说明：ACCEPTED = 已确认；PROPOSED = 暂定方向；VERIFY = 需 PoC 后冻结；SUPERSEDED = 已被新决策替代。

## ADR-001：首版平台与产品定位
- 状态：ACCEPTED
- 决策：首版只做 Windows 10/11 x64 桌面自动化；产品独立实现，工作名“按键精灵 2026”暂定。
- 原因：控制范围，先交付稳定的桌面版本。
- 不做：首版 Android、云同步、旧版 Q 脚本兼容。

## ADR-002：Lua 作为脚本语言
- 状态：ACCEPTED
- 决策：使用 Lua 5.4，不自创完整脚本语言。
- 原因：成熟的语言语法和流程控制；项目只维护自动化 API。
- 影响：旧版 Q 脚本不能直接执行，之后如有需求再研究转换器。

## ADR-003：录制输出支持动作与代码两种模式
- 状态：ACCEPTED
- 决策：动作列表可编辑并生成 Lua；Lua 可自由编辑，但不承诺任意代码无损转回动作列表。
- 原因：避免复杂 Lua 结构与有限动作模型之间的伪双向同步。
- 影响：生成区块需 ID、schemaVersion、dirty/detached 状态和覆盖确认。

## ADR-004：桌面 UI 使用 WinForms
- 状态：ACCEPTED
- 决策：首版 UI 使用 WinForms。
- 原因：Windows 桌面工具、经典停靠面板和低额外依赖。
- 影响：编辑器/Dock 组件需独立验证，不得将第三方控件类型泄漏到业务层。

## ADR-005：目标框架使用 .NET 10 LTS
- 状态：ACCEPTED
- 决策：目标框架 net10.0-windows，发布先做 win-x64 self-contained。
- 原因：当前 LTS 生命周期适合长期维护。
- 参考：微软生命周期页面 https://learn.microsoft.com/zh-cn/lifecycle/products/microsoft-net-and-net-core

## ADR-006：Lua 绑定与编辑器依赖不提前锁版
- 状态：VERIFY
- 候选：NLua/KeraLua；Scintilla.NET 系列。
- 决策：先做 PoC，确认 Lua 5.4、指令钩子、行号、原生 DLL、许可证和 .NET 10 支持后再锁定。
- 禁止：仅凭网上版本号或旧聊天记录认定兼容。

## ADR-007：脚本执行不占用 UI 线程
- 状态：ACCEPTED
- 决策：Lua state 由独立执行线程串行访问；首版暂不引入独立 Worker 进程。
- 原因：满足 UI 响应和取消要求，同时避免首版 IPC 复杂度。
- 重新评估条件：PoC 或稳定性测试证明同进程线程模型无法满足隔离/恢复要求。

## ADR-008：录制采集源先验证 Raw Input
- 状态：VERIFY
- 决策：先比较 Raw Input 与 WH_KEYBOARD_LL / WH_MOUSE_LL，默认不预设低级钩子为最终实现。
- 原因：低级钩子回调有时限，超时可能被 Windows 静默移除；录制场景通常无需屏蔽物理输入。
- 参考：https://learn.microsoft.com/en-us/windows/win32/winmsg/lowlevelkeyboardproc

## ADR-009：输入注入采用标准 Windows API
- 状态：ACCEPTED
- 决策：首版前台输入基于 SendInput，不默认使用驱动或内核组件。
- 限制：遵守 UIPI/完整性级别与受保护桌面限制；不承诺所有窗口可操作。
- 参考：https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-sendinput

## ADR-010：main 为唯一长期集成分支
- 状态：ACCEPTED
- 决策：本项目默认小步提交 main，不建立长期分支/PR 流程。
- 原因：个人项目，优先可见、易回滚和低维护成本。
- 约束：写入前检查 HEAD；禁止 force push。

## ADR-011：录制数据以结构化动作作为来源
- 状态：ACCEPTED
- 决策：动作列表到 Lua 代码为明确的一向生成；修改生成区块后标记不同步。
- 原因：无法保证任意 Lua 代码反解析成动作步骤。
- 影响：重新生成前预览并确认。

## ADR-012：便携版优先
- 状态：ACCEPTED
- 决策：首版优先 self-contained win-x64 文件夹 ZIP；暂不强求单文件 EXE。
- 原因：原生 Lua/编辑器组件可能有独立 DLL，文件夹式部署更便于排错。

## ADR-013：产品版本与交付纪律
- 状态：ACCEPTED
- 决策：使用语义化版本，不使用 FIX 版号；发布文件名含产品名与版本，文档和 SHA 对齐。
