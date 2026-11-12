# 开发总路线图与会话任务板

- 当前版本：Roadmap 0.2
- 任务状态只允许：TODO / IN_PROGRESS / BLOCKED / DONE
- 更新规则：每个会话结束时更新本文件和 SESSION-HANDOFF.md。
- 任务分配原则：一个 GPT 会话优先负责一个清晰、可验证的任务。

## 0. 里程碑总览

| 里程碑 | 内容 | 状态 | 结果 |
|---|---|---|---|
| M0 | 仓库初始化与项目文档框架 | DONE | 已建立规范、需求、API、验收和交接文件 |
| M1 | 技术验证 PoC | TODO | 锁定 Lua 绑定、编辑器、输入采集和发布方式 |
| M2 | .NET 解决方案与空壳程序 | TODO | 可启动的 WinForms 主程序和测试结构 |
| M3 | Core 数据模型与工程文件 | TODO | 工程/脚本/动作模型及可靠保存 |
| M4 | Lua Host 与运行协调器 | TODO | 脚本可执行、日志、取消/暂停/停止 |
| M5 | 编辑器与命令面板 | TODO | Lua 编辑、补全、参数提示 |
| M6 | Windows 输入控制 | TODO | 鼠标键盘注入及输入状态清理 |
| M7 | 录制器 | TODO | 物理键鼠事件采集、动作整理 |
| M8 | 动作列表与 Lua 生成 | TODO | 可编辑动作和生成区块规则 |
| M9 | P0 功能集成与稳定性 | TODO | P0 全链路验收通过 |
| M10 | 首个可用版本 | TODO | self-contained ZIP、说明、测试报告、SHA256 |

## 1. M0 — 仓库初始化与产品交互规格（已完成）

| 任务 ID | 内容 | 状态 | 验收 |
|---|---|---|---|
| BOOT-01 | 初始化 README 与项目入口 | DONE | README 指向权威文档 |
| BOOT-02 | 建立 AGENTS 与会话协议 | DONE | 新会话有强制读取顺序 |
| BOOT-03 | 建立 PRD/架构/API/录制/验收文档 | DONE | 文档覆盖范围明确 |
| BOOT-04 | 建立路线图、决策、日志和 handoff | DONE | 文件已提交并复核 |
| BOOT-05 | 建立 src/tests 结构说明 | DONE | 无代码但保留规划入口 |

## 2. M1 — 技术验证 PoC

### TV-01：Lua 5.4 / .NET 绑定与取消机制
- 状态：TODO（当前唯一下一任务）
- 范围：只做最小 PoC，不做正式应用 UI。
- 输出：poc/LuaRuntime 或同等清晰目录；记录精确 NuGet 版本、许可证、Lua 版本、Win-x64 发布结果。
- 验证：C# 注册全局 API、Lua 错误行号、Lua 指令钩子、CPU 无限循环停止、可取消 Delay、暂停/恢复。
- 验收：TECHNICAL-VALIDATION-CN.md 第 3 节逐项有结果；关键取消目标有重复实测数据；所有包版本和来源写明。

### TV-02：WinForms Lua 编辑器组件
- 状态：TODO
- 输出：候选控件对比和最小 PoC；确认中文、Lua lexer、补全扩展、DPI、self-contained 原生依赖。

### TV-03：Windows 输入与录制采集源
- 状态：TODO
- 输出：SendInput 鼠标键盘 PoC；Raw Input 与低级钩子对比；坐标、DPI、Unicode、注入过滤、UIPI 和清理测试。

### TV-04：便携发布
- 状态：TODO
- 输出：net10.0-windows win-x64 self-contained ZIP 最小 PoC；确认依赖和干净环境启动。

## 3. M2 — 解决方案骨架

| 任务 ID | 内容 | 依赖 | 验收 |
|---|---|---|---|
| SCAFFOLD-01 | 建立 Anjian2026.sln 与项目结构 | TV-01 至少确定 Lua 集成路径 | 项目依赖方向符合架构 |
| SCAFFOLD-02 | 建立 WinForms 主窗体和空面板 | SCAFFOLD-01 | Release/Debug 均可启动 |
| SCAFFOLD-03 | 建立单元测试/集成测试项目 | SCAFFOLD-01 | CI 或本地测试可运行 |
| SCAFFOLD-04 | 建立版本/构建元数据与 .gitignore | SCAFFOLD-01 | 版本来源单一，构建产物不入库 |

## 4. M3 — Core 数据与工程系统

- CORE-01：工程、脚本、资源和动作 DTO。
- CORE-02：工程 schemaVersion 与迁移机制。
- CORE-03：原子保存、备份、路径解析。
- CORE-04：单元测试与损坏文件恢复测试。

## 5. M4 — Lua 引擎与运行控制

- LUA-01：Lua 5.4 Host、受限环境和 API 注册。
- LUA-02：日志输出与错误文件/行号定位。
- LUA-03：Run / Pause / Resume / Stop 状态机。
- LUA-04：指令钩子取消、可取消 Delay 和按键清理。
- LUA-05：无限循环、长等待、异常与长时运行测试。

## 6. M5 — 编辑器与命令面板

- EDITOR-01：编辑器标签、基础编辑、文件保存。
- EDITOR-02：Lua 高亮、自动缩进、括号匹配、查找替换。
- EDITOR-03：命令注册中心和中文命令面板。
- EDITOR-04：自动补全、参数提示、错误标记。

## 7. M6 — Windows 输入控制

- INPUT-01：鼠标移动、点击、Down/Up、滚轮。
- INPUT-02：键盘键值映射、组合键、Unicode 文本。
- INPUT-03：虚拟桌面物理坐标与 Per-Monitor V2 DPI。
- INPUT-04：输入状态追踪、ReleaseAll、异常清理。
- INPUT-05：真实 Windows 集成测试和权限限制提示。

## 8. M7/M8 — 录制与代码生成

- REC-01：采集源、标准化事件和有界队列。
- REC-02：Down/Up 配对、动作模型、移动合并。
- REC-03：动作列表编辑器。
- CODEGEN-01：Action Model 到 Lua 代码生成。
- CODEGEN-02：Generated Block ID、dirty/detached 状态和覆盖确认。
- REC-04：工程保存、恢复和异常录制测试。

## 9. M9/M10 — 集成和首发

- INTEG-01：P0 全链路打通。
- INTEG-02：P0 验收表逐项测试。
- INTEG-03：2 小时稳定性和停止延迟测试。
- INTEG-04：干净 Windows 10/11 便携部署测试。
- RELEASE-01：版本号、变更日志、用户手册、测试报告和 SHA256。
- RELEASE-02：首个可用版本发布。

## 10. 新会话任务认领规则

1. 新会话先读当前 handoff，只领取其中指定任务。
2. 如果下一任务被阻塞，先完成 handoff 指定的阻塞解除项，不擅自跳到后续大模块。
3. 同时进行的 PoC 分支如有必要，合入前必须基于最新 main 并做验证；默认仍直接提交 main。
4. 未达到验收条件不得把任务标 DONE。
5. 每个任务完成后写明 commit SHA、测试证据和下一任务。
