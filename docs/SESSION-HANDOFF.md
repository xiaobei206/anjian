# 当前会话交接（唯一有效状态）

> 本文件每个会话结束时整体更新。历史过程写在 DEVELOPMENT-LOG-CN.md。

## 当前状态

- 项目：按键精灵 2026（暂定名）
- 仓库：xiaobei206/anjian
- 主分支：main
- 当前阶段：M0 文档与产品交互规格完成；尚无应用代码
- 当前版本：未发布（不要创建虚假版本/发布资产）
- 本次规格冻结提交：本文件所在提交（以 GitHub main HEAD 为准）

## 本会话 S001

- 主题：首版交互确认与 Lua API 语义冻结
- 状态：DONE（文档决策已提交；功能开发尚未开始）
- 用户明确确认：A/A/A
  1. Lua 函数调用语法，例如 `MoveTo(122, 100)`。
  2. 录制时合并鼠标移动，保留必要路径点和终点。
  3. 首版采用全屏绝对坐标，不做窗口相对坐标。
- 用户补充确认的 API 语义：
  - `LeftClick(50)` = 左键按下 50 ms 后释放。
  - `LeftClick()` 默认 50 ms。
  - 右键、中键点击和 `KeyPress` 同样默认 50 ms。
  - 显式 0 表示不增加等待；超过 60,000 ms 的长按使用 Down / Delay / Up。
  - Stop 必须中断点击保持并释放输入；Pause 在当前完整 Click / KeyPress 动作完成后生效。

## 已确认的首版用户层 API

- 鼠标：`MoveTo`、`LeftClick`、`RightClick`、`MiddleClick`、`Left/Right/MiddleDown`、`Left/Right/MiddleUp`、`MouseWheel`、`GetMousePosition`。
- 键盘：`KeyPress`、`KeyDown`、`KeyUp`、`Hotkey`、`TypeText`。
- 时间/日志：`Delay`、`RandomDelay`、`Log`。
- 标准 Lua 5.4 语法；API 全局函数形式，不要求 Mouse / Keyboard 命名空间。
- 时间统一为毫秒。坐标使用虚拟桌面物理像素，负坐标合法。

## 已更新的权威文档

- `docs/LUA-API-CN.md`：API 0.2，函数命名、点击按下时长、默认值和坐标语义。
- `docs/RECORDER-DESIGN-CN.md`：移动合并、Down/Up 配对、holdMs 和动作间隔分离、Lua 生成形式。
- `docs/ARCHITECTURE-CN.md`：内部 IInputController 时长参数、Stop/Pause 约定。
- `docs/DECISIONS-CN.md`：ADR-014 至 ADR-017，记录四项冻结决策。
- `docs/ROADMAP-CN.md`：M0 完成，TV-01 为唯一下一任务。
- `docs/DEVELOPMENT-LOG-CN.md`：追加 S001 记录。

## 当前技术事实

- 仓库最新已确认基线：S000 最终提交 `306ed5dc85c36576c52452a499551e4a8f9abc70`；写入前必须再次检查最新 main HEAD。
- 目前没有 .sln、应用项目、应用代码、可运行安装包或 Release。
- .NET 10、WinForms、Lua 5.4 是已确认技术方向；NLua/KeraLua 包版本和 lua_sethook 使用路径仍须 TV-01 实测。
- 当前会话只完成规格冻结，没有运行构建或测试，不得把 PoC 标为通过。

## 下一任务（唯一）

- ID：TV-01
- 名称：Lua 5.4 / .NET 10 绑定与中断机制 PoC
- 目标：在独立最小 PoC 中验证 Lua 5.4 运行、C# 全局函数注册、错误行号、CPU 无限循环 Stop、可取消 Delay、Pause/Resume、win-x64 self-contained。
- 先核对 NLua / KeraLua 当前包、依赖树、许可和 native assets；检查高层绑定是否能提供稳定的 instruction hook。
- 若高层 API 做不到，在证据支持下验证 KeraLua 原生 API 路径；不要直接编写正式桌面 UI。
- 需要提交 PoC 代码、Windows CI 构建/测试工作流、验证结果和依赖锁定结论。
- 必须更新 TECHNICAL-VALIDATION-CN.md、DECISIONS-CN.md、ROADMAP-CN.md、DEVELOPMENT-LOG-CN.md、SESSION-HANDOFF.md。
- 结束时实际检查 GitHub Actions 结果；未通过则记录日志和精确失败原因。

## 下一会话接手指令

先读取 AGENTS.md、README.md、docs/SESSION-HANDOFF.md、docs/ROADMAP-CN.md、docs/TECHNICAL-VALIDATION-CN.md 与 docs/LUA-API-CN.md。以 main 最新 HEAD 为准。只执行 TV-01；写入前检查 HEAD；完成独立 PoC、Windows CI 构建/测试和真实验证后，更新 handoff / log / roadmap 并提交 main。不得把未执行测试写成 PASS。
