# 首版验收与测试清单

- 当前状态：测试计划草案
- 适用对象：P0 首版（Windows 10/11 x64）
- 通过原则：有证据、有测试环境、有结果；不能仅凭代码阅读标记通过

## 1. 测试环境记录

每轮验收至少记录：
- Windows 版本与 build。
- CPU 架构、主屏与副屏分辨率。
- 每台显示器缩放比例。
- .NET SDK/Runtime 版本。
- 应用版本/commit SHA。
- 测试日期、测试人、结果与日志路径。

## 2. 基础功能（P0）

| ID | 场景 | 操作 | 通过标准 |
|---|---|---|---|
| A01 | 首次启动 | 解压 self-contained ZIP 并启动 | 不安装额外 Runtime、普通权限可启动 |
| A02 | 新建脚本 | 新建 Lua 文件并保存 | 文件可在外部编辑器打开 |
| A03 | 重开脚本 | 关闭应用后重新打开 | 内容无丢失 |
| A04 | 多标签 | 同时打开 3 个脚本 | 切换正确，未保存状态可见 |
| A05 | 编辑器 | 输入、撤销、重做、查找 | 操作符合预期，无 UI 冻结 |
| A06 | 补全 | 输入 API 前缀 | 展示命令名、参数和中文说明 |
| A07 | Lua 基础 | 变量、循环、条件、函数 | 输出和执行结果正确 |
| A08 | 行号定位 | 使用语法错误脚本 | 显示文件和正确行号 |
| A09 | 鼠标移动 | 在已知区域执行 MoveTo | 坐标误差符合系统输入能力，记录实测 |
| A10 | 鼠标按键 | 左/右/中点击、Down/Up | 目标应用接收事件，状态无残留 |
| A11 | 滚轮 | 正负 delta | 方向与滚动幅度符合定义 |
| A12 | 键盘 | 普通键、功能键、组合键 | 次序正确，不出现残留按键 |
| A13 | Unicode | 输入中英日文文本 | 文本内容正确；IME 组合限制有说明 |
| A14 | 延时 | Delay(500) | 实测偏差有记录且在定义容差内 |
| A15 | 随机延时 | RandomDelay(100, 300) | 多次结果在闭区间内 |
| A16 | 日志 | Info/Warn/Error | 时间、级别、脚本、行号可见 |
| A17 | 全局快捷键 | 应用不在前台时按 F10/F11/F12 | 空闲时 F10 启动；运行/暂停时 F11 暂停/继续、F12 中止并释放输入；无需切回窗口 |
| A18 | 工程保存 | 保存、关闭、重开 | 脚本、动作、资源引用完整 |
| A19 | 原子写入 | 模拟保存中断/异常 | 原文件可恢复，不出现静默截断 |
| A20 | 无网络 | 禁用网络启动运行 | P0 功能不依赖云服务 |
| A21 | 重复次数 | 设置为 100 并播放短脚本 | 完整脚本恰好运行 100 次，进度与日志一致 |
| A22 | 无限循环 | 设置重复次数为 0 | 脚本持续循环，手动停止可及时结束 |
| A23 | 轮间隔 | 设置 0/1000/2000ms，分别运行多轮 | 只在轮次之间等待；末轮后不等待；与脚本 Delay 独立 |
| A24 | 独立双窗口 | 启动悬浮窗后打开完整主窗口 | 主窗口与悬浮窗同时存在，互不替换；两边状态即时同步 |
| A25 | 主窗口关闭 | 在完整主窗口点右上角 × 或“返回悬浮窗” | 主窗口隐藏且任务栏按钮消失；悬浮窗和托盘图标仍在，脚本状态不中断 |
| A26 | 托盘生命周期 | 右键托盘图标，分别打开主窗口、显示/隐藏悬浮窗、退出 | 窗口可恢复；只有“退出”会结束进程并清理资源 |
| A27 | 悬浮窗播放设置 | 在悬浮窗更改次数和轮间隔，再打开主窗口修改 | 两处数值即时同步、本地持久化，下一次播放使用新设置 |
| A28 | 应用图标 | 检查 EXE、任务栏、主窗口和托盘 | EXE 使用嵌入图标；托盘/窗口图标一致，小尺寸清晰可辨 |

## 3. 录制系统（P0）

| ID | 场景 | 通过标准 |
|---|---|---|
| B01 | 鼠标移动 | 坐标和事件顺序合理，虚拟桌面坐标一致 |
| B02 | 左右中键 | Down/Up 均被记录或有明确错误 |
| B03 | 长按 | 按下到释放时长保留 |
| B04 | 键盘 | 物理按键按下/释放顺序正确 |
| B05 | 组合键 | 修饰键顺序、释放顺序正确 |
| B06 | 滚轮 | delta 与方向正确 |
| B07 | 事件整理 | 过滤冗余移动，不丢失关键事件 |
| B08 | 动作编辑 | 修改坐标/按键/延时后生成代码正确 |
| B09 | 生成区块 | ID、边界和 schema 信息可识别 |
| B10 | 手改代码 | 手工改动后提示不同步，不静默覆盖 |
| B11 | 重新生成 | 覆盖前预览并要求确认 |
| B12 | 注入过滤 | 回放不再次污染物理录制 |
| B13 | 队列溢出 | 有告警、停止或明确降级，不静默丢失 Down/Up |
| B14 | 负坐标 | 副屏在左/上方时坐标正确 |
| B15 | 异常结束 | 最近持久化录制可恢复，未配对动作可见 |
| B16 | 只记录点击坐标 | 勾选后移动鼠标多次再点击不同位置 | 不生成移动轨迹；每次点击坐标准确，代码顺序为 MoveTo、可选 Delay、点击命令 |
| B17 | 暂停后继续录制 | 录制几组操作，暂停、等待数秒、继续并再录几组，最后结束 | 前后事件都保留，暂停时间不生成额外 Delay，结束后生成的脚本完整 |
| B18 | 紧凑悬浮窗 | 启动后查看坐标、拖动、切完整模式/返回、切置顶并重启 | 默认显示紧凑控制窗，坐标持续刷新，置顶和模式设置持久化，完整模式编辑器功能正常 |

## 4. 运行控制与稳定性（硬门槛）

| ID | 场景 | 通过标准 |
|---|---|---|
| C01 | 无限 Lua 循环 | 30 次测试停止响应 P95 ≤ 500 ms |
| C02 | 长延时取消 | Delay(60000) 中停止响应 P95 ≤ 300 ms |
| C03 | 暂停恢复 | 30 次暂停/恢复不丢失脚本状态、不重复执行已完成动作 |
| C04 | 停止清理 | 程序按住的所有键/鼠标按钮均释放 |
| C05 | UI 响应 | 脚本长时间运行时窗口持续可交互 |
| C06 | 异常处理 | Lua 错误后状态为 Failed，资源释放 |
| C07 | 重复操作 | 连续运行/停止 100 次无资源泄漏迹象 |
| C08 | 长时运行 | 连续运行 2 小时无崩溃、死锁或状态失控 |
| C09 | 采集压力 | 模拟至少 50,000 条输入事件，关键 Down/Up 无静默丢失 |
| C10 | 队列监控 | 队列峰值、溢出数、处理耗时可诊断 |
| C11 | 轮间隔中暂停/停止 | 无限或多轮运行期间，在间隔阶段暂停、恢复或停止 | 暂停保持剩余间隔，恢复后继续；停止立即退出 |
| C12 | 长文本输入停止 | 输入长 Unicode 文本时发送停止 | 在字符边界响应取消，不继续输入剩余文本，释放键鼠状态 |

P95 测试应至少 30 次，记录每次耗时并计算 95 百分位。普通脚本 Lua 指令钩子间隔必须经过测量，不允许仅设置一个常数就宣称达标。

## 5. Windows 环境兼容

| ID | 场景 | 通过标准 |
|---|---|---|
| D01 | DPI 100% | 界面和坐标正确 |
| D02 | DPI 125/150/200% | 编辑器控件与输入坐标无系统性偏移 |
| D03 | 混合 DPI 多屏 | 窗口跨屏后坐标仍正确 |
| D04 | 负虚拟坐标 | 左侧/上方副屏可操作 |
| D05 | 普通权限 | 默认用户权限正常启动 |
| D06 | 高权限目标 | 明确提示权限限制，不误报成功 |
| D07 | 受保护桌面 | 不声称可操作或绕过 |
| D08 | 快捷键钩子 | 安装失败、系统/安全软件阻止钩子或快捷键空闲状态 | 日志明确报错且按钮仍可用；注入的功能键不误触发；无对应动作时 F10/F11/F12 正常传递给前台程序 |

## 6. 发布验收

- [ ] Release win-x64 自包含发布成功。
- [ ] 干净 Windows 10 x64 启动成功。
- [ ] 干净 Windows 11 x64 启动成功。
- [ ] ZIP 解压即可运行。
- [ ] 无强制联网、无默认管理员要求。
- [ ] 所有第三方依赖和许可证清单齐全。
- [ ] P0 自动化测试通过。
- [ ] 手工验收报告记录测试环境。
- [ ] 用户文档、开发日志、版本号、SHA256 对应一致。
- [ ] 没有未解释的 P0 阻断问题。

## 7. 测试结论模板

每项只能标记：PASS / FAIL / BLOCKED / NOT RUN。必须附证据路径或失败说明。未经实测的功能不得标为 PASS。


## 8. 完整产品目标的后续验收门槛（preview.4 尚不适用）

本节是完整产品目标的正式验收要求。所有项目在未实现、未执行前均为 TODO / NOT RUN；不得因为 preview.4 的旧测试通过而标记 PASS。具体状态与阶段见 FEATURE-MATRIX-CN.md。

| ID | 目标能力 | 最低验收条件 |
|---|---|---|
| E01 | API 注册表与函数手册 | 当前所有公开 Lua 函数均存在于机器可读注册表；参数类型/默认值/返回值与手册一致；生成结果可复现 |
| E02 | IDE 补全/参数提示 | 使用同一注册数据；签名、默认值、中文说明正确；缺失函数不会显示为当前可用 |
| E03 | 错误行导航 | 语法/运行错误可定位到脚本、行号和调用栈，路径可包含中文 |
| E04 | 断点调试 | 文件/行号断点在目标 Lua 行执行前暂停；进入/越过/跳出、运行到光标真实控制执行点；暂停时查看当前栈帧、调用栈、局部与可读全局变量；UI 不直接访问 Lua state |
| E05 | 动作列表 | 插入/删除/排序/修改动作后 Lua 生成正确；手动修改生成区块会标记脱离同步，重生成需预览确认 |
| E06 | 多坐标空间 | screen/window/client/anchor/ratio 的转换正确；窗口移动/缩放后相对坐标仍符合定义 |
| E07 | 窗口查找与布局 | 按标题/类名/进程查找，取外框/客户区、移动、大小调整、对齐和超时等待有可重现测试 |
| E08 | 控件与后台能力 | 对能力不支持的目标返回明确错误；不将未验证的后台动作记为成功，不绕过 Windows 权限边界 |
| E09 | 截图/找色/找图 | 区域、坐标空间、容差/相似度、未命中、超时、多个结果和资源释放均有自动或集成测试 |
| E10 | OCR | 离线环境可识别样本文字；语言/字库缺失、低置信度和超时可诊断 |
| E11 | 脚本参数系统 | 参数类型/默认值/范围/选项校验在运行前执行；开发 UI 和小精灵 UI 对同一参数产生相同结果 |
| E12 | 自定义用户界面 | 文本、数字、复选框、选项、路径和热键等控件能够映射到脚本参数，取消/异常不会误启动任务 |
| E13 | 小精灵打包器 | 收集入口、模块和图像资源；输出包在干净 Windows 10/11 x64 运行，不需要开发 IDE 或手动 Lua 配置 |
| E14 | 打包完整性/迁移 | manifest、版本、依赖、SHA256 与文件清单一致；缺失/损坏文件提前报错；配置升级备份或迁移 |
| E15 | 插件系统 | 插件 manifest、API 版本和权限可查；加载失败可定位；停用/卸载后缺失 API 有明确错误 |
| E16 | 文件/剪贴板/进程/网络 API | 错误路径、权限、超时和取消测试通过；长操作不冻结 UI；核心功能在断网时正常 |
| E17 | 多实例与定时 | 多个实例状态、参数、日志和 Lua state 相互隔离；分别停止均可清理输入状态 |
| E18 | 文档交付 | 每个发行包包含匹配该版本的中文函数手册和离线使用说明；API 注册表、补全与手册一致性测试通过 |


## 9. M3 Lua IDE 基础验收（当前 main 开发版）

本节适用于包含 M3 编辑器代码的 main 构建，不代表已发布的 v0.1.0-preview.4 包含这些功能。CI 自动测试已通过；以下 Windows 桌面交互用例仍需人工执行并记录环境。

| ID | 场景 | 操作 | 通过标准 | 当前状态 |
|---|---|---|---|---|
| M3-01 | 多标签编辑 | 打开至少 3 个脚本，分别修改内容后切换标签，再逐个关闭并重新打开 | 标签切换自动保存；未保存标记正确；关闭/重开后内容不丢失；每个标签始终对应正确文件 | NOT RUN（桌面） |
| M3-02 | 行号与语法高亮 | 输入 Lua 关键字、数字、字符串、长字符串、行注释、长注释和已注册 API | 行号随滚动更新；token 颜色分类正确；编辑和滚动不造成明显卡顿 | NOT RUN（桌面） |
| M3-03 | API 自动补全 | 输入 `Left`、`Move` 等前缀，使用方向键、Enter/Tab、Escape；尝试 Ctrl+Space | 候选仅来自注册表中 `implemented` API；插入函数调用后光标位于括号内；计划函数不出现 | NOT RUN（桌面） |
| M3-04 | 签名/参数提示 | 输入 `LeftDoubleClick(`，填写第一个参数并输入逗号；在嵌套函数参数中继续输入 | 当前函数签名、活动参数名称和中文说明正确；嵌套逗号不错误推进外层参数 | NOT RUN（桌面） |
| M3-05 | 查找与替换 | 打开查找替换，查找下一项、循环查找、区分大小写、替换当前和全部替换 | 匹配范围和替换次数准确；无匹配给出明确反馈；替换后标签标记为已修改并能保存 | NOT RUN（桌面） |
| M3-06 | 语法诊断与错误导航 | 分别对合法脚本、缺少 end 的脚本运行“检查语法” | 合法脚本提示通过；非法脚本显示错误信息并跳转对应行/列；语法检查只编译 chunk，不执行 Log/键鼠等脚本动作 | NOT RUN（桌面） |
| M3-07 | 中文/日文输入 | 在编辑器输入、查找、替换中文与日文字符，保存并重开 | 输入法组合、光标移动、选区、编码和保存内容一致 | NOT RUN（桌面） |
| M3-08 | 大文件性能 | 打开并编辑约 1MB Lua 文件，连续输入、滚动、切换选区并触发高亮/补全 | UI 不出现长时间无响应；旧文本版本的后台高亮结果不会覆盖新文本；内存和 CPU 无持续失控 | NOT RUN（桌面） |
| M3-09 | DPI | 在 Windows 100%、125%、150%、200% 缩放下检查标签、补全列表、签名提示、行号和查找窗口 | 控件不裁切、不重叠，行号/错误定位准确 | NOT RUN（桌面） |



## 10. M4 Lua 调试器验收（main 开发版，尚未随 preview.4 发布）

CI 自动测试应覆盖 Lua hook 与 worker 数据协议；桌面交互需要额外在 Windows 实机执行。自动化测试成功不将下面桌面项自动标为 PASS。

| ID | 场景 | 操作 | 通过标准 | 当前状态 |
|---|---|---|---|---|
| M4-01 | 文件/行号断点 | 在脚本第 3 行设置断点并启动调试 | 行 hook 在第 3 行执行前暂停；显示文件名/行号，删除断点后继续不再次因该断点暂停 | NOT RUN（桌面） |
| M4-02 | 首行暂停与继续 | 不设置断点启动脚本，在首个 Lua 执行行暂停后点继续 | 首行停点真实发生；继续后脚本执行结束，普通日志与键鼠 API 正常 | NOT RUN（桌面） |
| M4-03 | 单步进入 | 在包含嵌套 Lua 函数调用的行选择单步进入 | 下一执行位置进入被调用的 Lua 函数，调用栈增加 | NOT RUN（桌面） |
| M4-04 | 单步越过 | 在嵌套函数调用行选择单步越过 | 子函数内部行不触发可见停点；在调用者下一行暂停 | NOT RUN（桌面） |
| M4-05 | 单步跳出 | 在嵌套函数内部选择单步跳出 | 返回调用者后暂停，调用栈深度减少 | NOT RUN（桌面） |
| M4-06 | 运行到光标 | 将编辑器光标定位到后续行并点击运行到光标 | 运行直至同文件同一行的实际 Lua 行 hook 再暂停 | NOT RUN（桌面） |
| M4-07 | 变量/栈帧观察 | 暂停后选择不同调用栈帧，检查局部变量、全局变量、表值与长字符串 | 局部范围随栈帧切换；可读标量值准确；不可展开值与截断有说明；无任意表达式求值 | NOT RUN（桌面） |
| M4-08 | 停止和清理 | 在断点暂停期间按停止，再次正常播放另一脚本 | worker 退出、调试控件复位、Lua hook 清理，键鼠释放；后续可再次运行 | NOT RUN（桌面） |
| M4-09 | 正常播放回归 | 不启动调试，分别播放普通脚本、长循环、Delay 和多轮脚本 | 原有 count hook、暂停/停止、输入释放、运行日志行为不回归 | NOT RUN（桌面） |
| M4-10 | 路径与 DPI | 使用包含中文目录的脚本，在 100/125/150/200% DPI 检查断点 gutter、当前行标记和调用栈 | 源码路径匹配、行号位置、按钮和视图无裁切/重叠 | NOT RUN（桌面） |

M4 Core 自动测试文件：tests/Anjian.Core.Tests/LuaDebugSessionTests.cs，覆盖真实断点行、嵌套调用 step-into/over/out 顺序、run-to-cursor、局部/全局快照、表不可展开和 hook dispose。CI 结果与 Windows 桌面手工结果分别记录在 DEVELOPMENT-LOG-CN.md / SESSION-HANDOFF.md。

 
## 11. M5 可视化录制/动作列表验收（main 开发版，未随 preview.4 发布）

CI 自动化测试只验证数据模型、转换、生成、持久化和代码保护；run 43295924472 为 Windows CI SUCCESS（37/37，0 failed/0 skipped），但以下 UI、Hook 和真实输入场景仍必须在 Windows 10/11 桌面执行并记录。当前全部桌面项均为 NOT RUN。

| ID | 场景 | 操作 | 通过标准 | 当前状态 |
|---|---|---|---|---|
| M5-01 | 原始事件持久化 | 录制鼠标、键盘和滚轮，结束并检查脚本同目录文件 | 生成脚本名.actions.json；保存 source/flags/eventId/timestamp/坐标空间/clickOnly/recordingId，文件可重开 | NOT RUN（桌面） |
| M5-02 | 点击/按键配对 | 录制普通点击、长按、键按下释放、拖动时插入 Move、重叠按键和滚轮 | 相邻匹配 Down/Up 才折叠；holdMs 与 delayMs 分开；拖动/重叠/无法配对事件顺序不丢，负坐标正确 | NOT RUN（桌面） |
| M5-03 | 点击模式与队列告警 | 分别启用/禁用只录点击，进行高速移动和长时间录制 | click-only 不产生移动轨迹；关闭后保留合并移动；队列溢出有明确丢失数，不伪称完整录制 | NOT RUN（桌面） |
| M5-04 | 动作列表编辑 | 插入、删除、复制、上下移动动作，切换类型/坐标/按键/时长/延时/启停、编辑分组注释并批量修改延时 | 修改即时反映在动作模型；只读 ID/source/raw event refs 正确；大量动作滚动与编辑无明显卡顿 | NOT RUN（桌面） |
| M5-05 | 代码生成与定位 | 生成 Lua 区块，选中动作执行“定位代码” | 每个动作有稳定 ID 标记；生成语句与动作参数一致；定位选中正确行；生成 Lua 可由运行时执行 | NOT RUN（桌面） |
| M5-06 | 块外代码保护 | 在 generated block 前后分别加入普通 Lua 代码，再修改动作并生成 | 区块外内容逐字保留；只替换指定 ID 的生成区块；不会覆盖用户自定义代码 | NOT RUN（桌面） |
| M5-07 | 脱离/缺失同步保护 | 手工改动生成区块或整个删除，打开动作列表并尝试同步；分别取消和确认预览 | Detached/Missing 状态正确；预览完整展示替换内容；取消不修改 Lua；确认后同步且更新 hash | NOT RUN（桌面） |
| M5-08 | Sidecar 生命周期 | 保存/重开、复制、重命名、删除脚本；手工把 schemaVersion 改为未知值 | main.lua 对应 main.actions.json；改名/复制/删除联动正确；未知 schema 不覆盖原数据；备份可识别 | NOT RUN（桌面） |
| M5-09 | 单步试运行与停止 | 试运行点击/按下/滚轮/文本输入，测试取消或 F12，随后再次运行普通脚本 | 风险确认清晰；限时/文本长度有效；输入停止/worker 退出后释放所有本程序持有输入，后续普通播放可用 | NOT RUN（桌面） |
| M5-10 | DPI/多屏/权限 | 在 100/125/150/200% 与主副屏（含负坐标）下录制、编辑、定位和试运行；普通权限/高权限目标分别尝试 | 坐标与代码一致且无 UI 裁切；权限受限给出真实错误，不绕过 UIPI/UAC 或安全边界 | NOT RUN（桌面） |

### M5 自动化测试范围

tests/Anjian.Core.Tests/RecorderDocumentTests.cs 覆盖：原始事件排序与稳定 ID、保守 Down/Up 配对、hold/delay 分离、Move 造成的配对中断、重叠按键、滚轮/负坐标/source/flags、溢出计数、sidecar 文件名与 round-trip、未知 schema 原文件保护、超长延时拒绝、Lua 区块生成/执行、CRLF/LF 哈希、块外代码保留、手工改动/删除区块需要预览确认、取消不改脚本和 actionId 代码映射。自动化测试结果见 DEVELOPMENT-LOG-CN.md 与 SESSION-HANDOFF.md。

## 12. M6 坐标系统与窗口自动化验收（main 开发版，尚未随 preview.4 发布）

M6 自动化测试可以验证坐标纯计算、API 注册/包装和 IPC 数据契约；Windows 桌面窗口枚举、DPI、多屏、权限、WinEvent 销毁事件以及真实窗口操作必须在 Windows 10/11 实机执行。初始状态全部 NOT RUN；CI PASS 不得自动改为桌面 PASS。

| ID | 场景 | 操作 | 通过标准 | 当前状态 |
|---|---|---|---|---|
| M6-01 | screen/window/client 双向转换 | 选定窗口，分别输入 screen/window/client 坐标，执行 CoordinateToScreen 与 CoordinateFromScreen | 结果互逆；window 原点为含标题栏/边框的外框左上；client 原点为客户区左上；屏幕负坐标保留 | NOT RUN（桌面） |
| M6-02 | 九宫格锚点 | 分别使用 9 个 anchor，叠加正/负 offset | 锚点位置与窗口外框几何一致；移动/缩放窗口后重新查询矩形再转换，结果与新几何一致 | NOT RUN（桌面） |
| M6-03 | ratio 归一化 | 用客户区 0/0、0.5/0.5、1/1 转换，再反向转换 | 左上、中心、右下边界一致；窗口外屏幕点反向转换可得到小于 0 或大于 1 的比例；ToScreen 越界输入报错 | NOT RUN（桌面） |
| M6-04 | 混合 DPI / 多显示器 | 在 100/125/150/200% 缩放和横纵排列多屏环境下，包含副屏位于左侧/上方，移动、缩放、再换算 | 以物理像素为统一单位；无 DPI 虚拟化重复换算；窗口/客户区/屏幕往返误差不超过 1 px | NOT RUN（桌面） |
| M6-05 | 窗口过滤与枚举 | 用标题片段、类名、进程名、PID、可见性分别筛选；枚举父窗口子窗口 | 过滤规则按文档执行；最多 512 项；标题/类名/进程/PID、显示状态、isChild、外框/客户区和前台/焦点字段准确 | NOT RUN（桌面） |
| M6-06 | 窗口生命周期与布局操作 | 对顶层窗口和子窗口分别执行激活、显示/隐藏、最小化/最大化/还原、置顶开关、Move、Resize、九宫格 Align | 公开 Move/Align 坐标始终是屏幕物理像素；子窗口 SetWindowPos 前转换为父客户区；目标窗口正确变化；前台策略拒绝时 WindowActivate 返回 false | NOT RUN（桌面） |
| M6-07 | 等待/超时/取消 | 等待一个即将出现和一个不会出现的窗口；分别等待一个关闭/不关闭窗口；在等待过程中按 F12 | WindowWait 命中返回信息表、超时返回 nil；WindowWaitClosed 关闭返回 true、超时返回 false；停止及时取消且无遗留 worker | NOT RUN（桌面） |
| M6-08 | HWND 销毁与复用保护 | 查找目标后关闭；再打开窗口触发系统句柄复用压力，尝试旧 ID 调用 | 旧 ID 一旦失效就报明确错误；不向新窗口发送操作；重新查找才可继续 | NOT RUN（桌面） |
| M6-09 | 权限边界 | 普通权限控制普通权限窗口；尝试操作高完整性级别窗口及 UAC 安全桌面 | 如系统拒绝，返回真实失败/受限状态；不提权、不绕过 UIPI/UAC 或安全桌面 | NOT RUN（桌面） |
| M6-10 | 旧 API / 录制回归 | 运行旧脚本 MoveTo(-120,450)，再移动窗口并运行 M5 已录制脚本 | MoveTo 始终按虚拟桌面绝对物理像素执行；M5 原始事件的 screen 坐标不被静默改写；旧函数名、参数、默认值不变 | NOT RUN（桌面） |

### M6 自动化测试范围

最终 Windows Actions run `43300620614` 已通过 Restore、Release Build（0 warnings / 0 errors）、61 项测试、API handbook check、win-x64 publish、ZIP/SHA256 和 artifact upload；这只证明 CI 自动化范围，不将下表任一 Windows 桌面测试改为 PASS。

- CoordinateSystemTests：负坐标、window/client 不同原点、9 锚点、ratio 正反向与越界、浮点转物理像素、显示器工作区对齐、无效矩形。
- LuaApiRegistryTests：18 个新函数注册、内部桥接隐藏、旧 MoveTo 绝对坐标语义、窗口数据表转 Lua 表、40 个函数生成手册一致性。
- WindowsWindowManager 的实际 Win32 行为仍要求本节桌面测试；不能用跨平台 Core 单测或 GitHub Actions 构建替代。



## 13. M7 控件自动化与前后台能力适配（main 开发版，未包含在 preview.4）

M7 使用 Windows 自带 UI Automation Control View / Pattern，不宣称任意控件或任意应用都有可操作接口。UIA 运行在可被停止的独立 Lua worker 内；主进程仍掌握 M6 窗口引用并执行前台 SendInput。以下 Windows 桌面项目初始均为 NOT RUN，CI 通过不能替代手工实测。

| ID | 场景 | 操作 | 通过标准 | 当前状态 |
|---|---|---|---|---|
| M7-01 | 能力探测矩阵 | 启动 `--automation-probe` 测试窗；对根窗口执行 WindowBind/WindowGetCapabilities | 每项返回 name/status/backend/reason/limitations；不将通用后台鼠标/键盘/文本标成支持；前台/后台语义操作区分明确 | NOT RUN（桌面） |
| M7-02 | 控件树及字段 | ControlEnumerate 测试窗，检查文本框、密码框、复选框、组合框、列表、按钮的 name/automationId/className/controlType/bounds/state/patterns | 只返回 UIA Control View 中实际暴露的元素；深度和数量有限制；窗口/控件失效后重新枚举，不复用旧 controlId | NOT RUN（桌面） |
| M7-03 | Value/Text | 对普通文本框读取 Value/Text，使用 ControlSetValue 改为新文本再读取；对密码框分别调用 ControlGetValue/ControlGetText | 普通文本框返回准确值；密码字段读值明确拒绝；只读/无 Pattern 控件不伪造成功 | NOT RUN（桌面） |
| M7-04 | Selection / SelectionItem | 读取组合框/列表当前选择，并调用 ControlSelect 选择另一项 | 返回值对应真实选择；只对实际暴露 Selection/SelectionItem 的节点工作；不支持时返回明确错误 | NOT RUN（桌面） |
| M7-05 | Toggle / Invoke | 读取并切换复选框；调用按钮 Invoke，检查测试窗状态计数 | Toggle 状态变化可再次读取；Invoke 触发可见计数变化；API 返回只代表 provider 接受调用，不保证远端业务最终完成 | NOT RUN（桌面） |
| M7-06 | 后台语义操作边界 | 把测试窗置于非前台，使用 UIA SetValue/Toggle/Select/Invoke；再尝试 WindowInput 的 move/click/key-press/text | 支持的 UIA Pattern 可按 provider 状态执行；通用后台鼠标/键盘/文本明确 unsupported；无静默回退或谎报成功 | NOT RUN（桌面） |
| M7-07 | 前台输入安全边界 | 令测试窗在前台/后台、最小化、无焦点，并分别把坐标置于客户区内外调用 WindowInput | 非前台/最小化/不可见目标拒绝；键盘/文本要求已有焦点；move/click 越过客户区边界拒绝；不自动激活/抢焦点 | NOT RUN（桌面） |
| M7-08 | 生命周期与窗口身份 | 绑定后关闭目标或重新打开同类窗口；调用旧 binding/control ID | 旧 M6 引用或身份变化被拒绝；旧控件 ID 失效；重新查找、绑定、枚举才可继续 | NOT RUN（桌面） |
| M7-09 | Stop/失去响应 provider | 运行循环探测脚本并按 F12；如自建可控 provider 能制造阻塞，再验证 worker 终止 | 主窗口仍可响应 Stop；被阻塞的 UIA COM 调用随 worker 终止；主进程释放自己持有的输入；下一轮运行可新建干净 binding | NOT RUN（桌面） |
| M7-10 | 权限边界 | 普通权限应用操作普通权限与管理员级窗口，尝试 UAC/安全桌面 | UIPI/UAC/安全桌面拒绝如实报告；不提权、不绕过、不将权限限制变成“后台成功” | NOT RUN（桌面） |

### M7 自动化测试范围

- LuaApiRegistryTests 覆盖：14 个新增公开函数绑定、worker-local UIA service 接口、内部 HWND 目标描述的 IPC 传递不向 Lua 返回 HWND、字符串结果、WindowInput 必填动作参数。
- Windows Actions 负责 .NET Restore/Release Build/Core Tests、API 函数手册 `--check`、self-contained win-x64 publish 与 artifact/package；最终 run 结果将在 DEVELOPMENT-LOG-CN.md 记录。
- 当前未在用户的 Windows 10/11 桌面执行 M7-01..M7-10，不能把这些项目标记为 PASS。

## 14. M8 截图、图色与模板资源（main 核心 API/CI 已通过；桌面实测 NOT RUN）

此处桌面项目必须在用户实际 Windows 10/11 桌面执行。CI 的 73/73 Core tests 和自包含发布成功不代表截图在真实 DPI、多显示器、遮挡与硬件加速场景已验收。

| ID | 场景 | 操作 | 通过标准 | 当前状态 |
|---|---|---|---|---|
| M8-01 | 虚拟桌面截图 | 在单屏与副屏位于左侧/上方的多屏环境运行 CaptureScreen、GetImageSize | 宽高正确；屏幕原点可为负数；hasScreenCoordinates 与原点字段准确；不发生坐标重复换算 | NOT RUN（桌面） |
| M8-02 | 窗口外框/客户区 | 对可见、未最小化窗口调用 CaptureWindow/CaptureClient，并改变窗口大小/位置 | 外框与客户区区域区分准确；窗口 ID 失效或窗口隐藏/最小化时明确失败；不宣称可读后台像素 | NOT RUN（桌面） |
| M8-03 | 区域截图与坐标空间 | 分别用 screen、window、client、anchor、ratio 调用 CaptureRegion，包含负屏幕坐标和边界 | 截图区域与 M6 CoordinateToScreen/FromScreen 几何契约一致；无效/越界矩形有明确错误；MoveTo 原语义不变 | NOT RUN（桌面） |
| M8-04 | PNG 加载/保存/裁剪 | LoadImage PNG，SaveScreenshot 到 PNG，CropImage 后再读取边角像素 | 文件释放及时；像素顺序/透明 alpha 正确；裁剪尺寸和屏幕原点偏移准确；非 PNG 输出扩展名被拒绝 | NOT RUN（桌面） |
| M8-05 | 图像资源生命周期 | 连续创建资源到 128 个、累计到 256 MiB，测试 ReleaseImage、重复释放、轮次结束/Stop 清理 | 上限失败可诊断；释放后可继续创建；重复释放返回 false；脚本下一轮没有旧 ID/资源泄漏 | NOT RUN（桌面） |
| M8-06 | RGB 像素与颜色容差 | 用测试图像检查 GetPixelColor/CompareColor，逐个调整 R/G/B 差值及 tolerance 边界 | 颜色为 0xRRGGBB；三个通道分别应用绝对差容差；越界坐标/非法参数明确报错 | NOT RUN（桌面） |
| M8-07 | 单点找色 | 对已知色块调用 FindColor，设置区域、step、容差，并测试无命中 | 结果排序为自上而下/从左到右；无命中返回 nil；截图结果带正确 screenX/screenY | NOT RUN（桌面） |
| M8-08 | 多点找色 | 使用 dx,dy,RRGGBB 分号模式；测试负偏移、越界点、格式错误与超过 256 点 | 所有相对点都满足才命中；格式错误/点数超限明确报错；不会读到区域外像素 | NOT RUN（桌面） |
| M8-09 | 区域颜色计数 | 在已知测试区域调用 CountColors，改变宽高、容差与 step | 统计范围正确；step 稀疏采样结果与约定一致；区域非法时明确报错 | NOT RUN（桌面） |
| M8-10 | 透明模板/阈值/灰度缩放 | 加载含透明区模板，验证 RGB、grayscale、scale=0.25/1/2/4 与非法范围 | 透明像素为通配；相似度分数/阈值稳定；灰度与最近邻缩放符合规格；全透明模板拒绝 | NOT RUN（桌面） |
| M8-11 | 多结果与性能上限 | 使用多个相隔/相邻图标的模板，设置 maxResults、region、step；构造较大搜索 | 结果按扫描顺序、避免重叠重复；maxResults 上限 1000；超过 50,000,000 次像素比较时明确报错而非无限卡死 | NOT RUN（桌面） |
| M8-12 | 等待/超时/暂停/停止 | 测试 WaitImage/WaitImageGone/WaitColor/WaitColorGone 的命中、未命中、stableCount、timeout，并在循环中按暂停/Stop | 连续条件重置准确；超时返回 nil/false；Pause 暂停轮询，Stop 能取消并清理资源；不把取消伪装为超时 | NOT RUN（桌面） |
| M8-13 | 遮挡与保护内容边界 | 部分覆盖目标窗口，尝试对窗口与客户区截图；验证受保护/硬件加速应用 | 结果只承诺桌面合成的可见像素；不可读内容如实失败或显示为桌面实际像素，不尝试绕过安全限制 | NOT RUN（桌面） |
| M8-14 | 模板资源 UI 与视觉调试层 | 在图像资源管理 UI 中导入/预览/命名模板；开启视觉调试层观察区域框、命中位置、相似度、颜色值与耗时 | 资源路径/版本可校验；可视化命中叠加准确且不会拦截输入；失败路径可诊断 | NOT RUN（桌面） |
| M8-15 | 模板清单完整性 | 导入有效/损坏/超大 PNG；修改资源文件字节、文件大小、IHDR 尺寸和 schemaVersion；尝试目录穿越路径 | 仅接受 PNG；最大 128 MiB/32,000,000 像素；manifest 校验 schema、路径根、尺寸、大小、SHA-256；未来 schema 不覆盖原文件 | NOT RUN（桌面 UI） |
| M8-16 | 资源依赖与脚本生命周期 | 让脚本使用 LoadImage("sample.assets/id.png")；分别重命名、复制、删除脚本；测试静态/动态 LoadImage 参数 | 静态资源路径随脚本重命名/复制更新；manifest 资源路径重基；动态参数报告无法静态解析；被引用资源不能从列表移除；脚本相对路径以脚本目录解析 | NOT RUN（桌面） |
| M8-17 | RGB/hex/颜色差值/颜色块 | 调用 RgbToHex、HexToRgb、ColorDifference、FindColorBlocks；覆盖 #RRGGBB/0xRRGGBB、非法字符、通道边界、相邻/对角/分离色块、step/minPixels/maxResults | RGB 通道 0–255；差值包含各通道绝对差/最大通道/Manhattan/Euclidean；颜色块按八邻域分组、按采样点数排序，区域/取消/采样上限明确 | NOT RUN（桌面） |
| M8-18 | 视觉调试覆盖层输入与截图隔离 | 开启覆盖层，移动/点击多个屏幕区域；运行 CaptureScreen 与 FindColor/FindImage；关闭覆盖层后重复输入 | 覆盖层置顶且点击穿透；关闭后不保留窗口、不拦截输入；Windows Display Affinity 排除成功时自身标注不进入截图；多屏负坐标正确 | NOT RUN（桌面） |

### M8 自动化测试范围
M8 核心阶段 Windows Actions run [43353801969](https://github.com/xiaobei206/anjian/actions/runs/43353801969) 通过 73/73 tests。M8-CLOSE 实现代码基线的 Windows Actions run [43357876777](https://github.com/xiaobei206/anjian/actions/runs/43357876777) SUCCESS；随后文档同步 main HEAD `c2b30c1d9aabd876cd848d6203376060f294c34e` 的 run [43358188770](https://github.com/xiaobei206/anjian/actions/runs/43358188770) 也 SUCCESS：Windows Server 2025 runner，Release Build 0 warnings / 0 errors，85 passed / 0 failed / 0 skipped；API handbook generation --check 确认 78 个公开函数；self-contained win-x64 publish、ZIP/SHA256 package、artifact upload 全 PASS（最新 artifact ID 14006903094，76,460,671 bytes；[下载](https://github.com/xiaobei206/anjian/actions/runs/43358188770/artifacts/14006903094)）。新增自动化覆盖 RGB/hex 差值、颜色块连通分量、PNG manifest/path/hash 和静态 LoadImage 依赖扫描。**M8-01..M8-18 的 GDI、WinForms UI、点击穿透、显示亲和、多显示器和真实桌面行为仍全部 NOT RUN**，必须由 Windows 10/11 用户桌面实测。

**本会话执行记录（2026-11-13）：** 本轮在 Debian 13 x86_64 环境中无 Windows 桌面访问、无 .NET SDK，未执行任何 M8 桌面场景。M8-01..M8-18 全部继续为 NOT RUN。Windows Actions run 43358188770 的 85/85 Core tests、Build 与 win-x64 publish PASS 仅是自动化/构建证据，不替代本表要求的 Windows 10/11 桌面验收。


## Windows 桌面实机验收统一策略（用户决定，2026-11-13）

M1–M13 的桌面实测不再作为进入下一开发阶段的前置条件，统一在 M14 所有功能开发完成后集中执行。各场景在开发阶段仍保持 NOT RUN；CI、代码审查和 Core 单元测试不能将这些场景标成 PASS。M14 正式发布前必须完成本文件所有桌面验收行，记录 Windows 版本/build、DPI、显示器拓扑、步骤、结果与日志。

## 15. M9 OCR 与文本识别（main 开发版，实机验收延期至 M14 后）

下列场景目前均为 **NOT RUN**。自动化 API 测试使用假 OCR 服务，只验证契约，不代表真实识别模型的精度、运行时和 Windows 桌面通过。

| ID | 场景 | 操作 | 通过标准 | 当前状态 |
|---|---|---|---|---|
| M9-01 | 模型管理与完整性 | 在“OCR 模型”管理器分别选择 ch/en/japan，下载或补齐模型；检查 cache 文件及上游校验结果 | 下载仅由用户明确触发；所有需要的模型文件存在且 SHA-256 校验通过；缓存路径位于 LocalAppData；取消/失败状态准确 | NOT RUN（桌面） |
| M9-02 | 严格离线推理 | 安装所需模型后断开网络运行 OCR/FindText/WaitText；再移走其中一个模型文件重复运行 | 已安装模型时识别不请求网络；缺失模型明确报错并提示打开模型管理器；运行时不静默下载 | NOT RUN（桌面） |
| M9-03 | 语言与文字方向 | 对简体中文、英语、日语样本分别 OCR，覆盖横排、多行、混合数字/标点及空白图 | 语言选择与模型一致；文本阅读顺序合理；空白图返回空结果；不支持语言明确报错 | NOT RUN（桌面） |
| M9-04 | 行文本与置信度 | 对不同字体、大小、缩放、抗锯齿文本运行 OCR，改变 minConfidence | 返回全文与逐行 text/confidence；阈值只过滤低置信度行；置信度在 0–1；空结果不伪造命中 | NOT RUN（桌面） |
| M9-05 | 矩形/多边形与屏幕坐标 | 对窗口内截图、区域截图和副屏左/上方的多显示器截图识别；核对 polygon/矩形和 screenX/Y | 局部坐标以图像零基像素为基准；screen 坐标只在截图有 screen origin 时返回；负坐标和边界映射正确 | NOT RUN（桌面） |
| M9-06 | FindText 匹配 | 对大小写、中文子串、整行、重复匹配和置信度阈值调用 contains/exact | contains/exact 语义符合说明；按阅读顺序返回第一个匹配行；未命中返回 nil | NOT RUN（桌面） |
| M9-07 | WaitText 超时/暂停/停止 | 分别在文字立即出现、延迟出现、始终不出现时调用；中途 F11 暂停、F12 停止 | 命中返回行；timeout 返回 nil；暂停在检查点生效；Stop 能终止独立 Worker；临时截图/整轮资源最终释放；UI 持续响应 | NOT RUN（桌面） |
| M9-08 | 大图/损坏输入与稳定性 | 输入超过 16 MP 图片、超过 64 MiB PNG 编码、损坏 PNG、空图；连续识别 100 次 | 超限在分配/推理前明确拒绝；损坏图片报错可诊断；不会使主 UI 崩溃；模型 session 与资源可正常释放 | NOT RUN（桌面） |



**M9 自动化/构建证据（非桌面 PASS）：** Windows Actions run [43363150335](https://github.com/xiaobei206/anjian/actions/runs/43363150335) SUCCESS：Restore、Release Build（0 warnings / 0 errors）、90/90 Core tests、API handbook check、self-contained win-x64 publish、ZIP/SHA256 和 artifact upload 全 PASS（artifact ID 14008744436）。因此 M9 代码/自动化状态可记 DONE；上表 M9-01..M9-08 仍全部 NOT RUN，必须按用户决定延后至 M14 完成后的 Windows 10/11 集中桌面验收。


## 16. M10 参数、配置与脚本自定义 UI（当前均 NOT RUN）

| ID | 场景 | 操作 | 通过标准 | 当前状态 |
|---|---|---|---|---|
| M10-01 | schema 静态读取 | 创建含全部参数类型的 sidecar；确认不执行 Lua 也可显示参数设计器 | schemaVersion/字段唯一性/默认值/范围/选项验证准确；未知未来 schema 只读拒绝且原文件不变 | NOT RUN |
| M10-02 | 类型验证 | 分别提交 text/integer/number/boolean/select/multiSelect/filePath/directoryPath/hotkey/color 有效和无效值 | 保持 JSON/Lua 类型；非有限数字、越界、无效选项、重复多选、错误路径/热键/颜色被拒绝 | NOT RUN |
| M10-03 | 默认与必填 | 空配置启动，测试有效默认值和无默认必填字段 | 有效默认值被采用；缺少必填值给出字段级错误；失败时 Worker 绝不启动 | NOT RUN |
| M10-04 | 参数 UI | 编辑/新增/删除/排序参数，打开运行表单并修改值 | 设计器和运行表单都由同 schema 驱动；保存重开值一致；取消表单不运行脚本 | NOT RUN |
| M10-05 | 安全 Worker 注入 | 输入包含引号、换行、Lua 语句和恶意字符串；脚本读取 Args | 字符串仅作为数据；不会被执行；Args 顶层和嵌套值不可变；数字/布尔/数组/对象类型一致 | NOT RUN |
| M10-06 | 配置持久化与备份 | 保存、重新打开、故意破坏配置、再破坏备份、模拟写入失败 | 原子写入；优先恢复上一份有效备份；均损坏时退回默认值并告警；失败不丢失旧文件 | NOT RUN |
| M10-07 | JSON 导入/导出 | 导入合法、非法、超大 JSON；导出含敏感字段的配置 | 导入前完整验证；非法导入不覆盖现值；敏感值默认脱敏并明确告知 | NOT RUN |
| M10-08 | schema migration | 用旧 schema、未来 schema、未知字段和迁移失败样本启动 | 受支持版本可迁移并备份；未来版本拒绝写回；迁移失败恢复原文件 | NOT RUN |
| M10-09 | 敏感数据与日志 | 使用敏感参数并触发验证错误、Worker 异常、导出 | 普通日志/异常不包含敏感值；仅用户明确选择时才导出明文敏感值 | NOT RUN |
| M10-10 | 运行生命周期 | 校验失败、用户取消、Worker 启动、暂停/停止和多轮运行 | 校验/取消不启动 Worker；每轮参数隔离；Stop 后清理运行值与资源；UI 不冻结 | NOT RUN |



### M10 自动化测试最终结果（2026-11-13）
- 对最终 M10 代码 HEAD `a923f1e02dce73629514da39bb10925b1271fc15` 的 Windows Actions run [43368224862](https://github.com/xiaobei206/anjian/actions/runs/43368224862) SUCCESS：Restore PASS；Release Build 0 warnings / 0 errors；Core Tests **102 passed / 0 failed / 0 skipped**；API handbook `--check` PASS；self-contained win-x64 publish、ZIP/SHA256 和 artifact upload 全 PASS。
- 新增自动化覆盖脚本目录相对路径在配置中保持可移植、运行时解析绝对路径、非法路径和 Lua 保留字拒绝、`pairs(Args)` 遍历返回值递归只读；schema 未来版本防覆盖与既有路径/正则/数值/空值测试也已保留。
- 临时 artifact ID `14010789337`，87,099,638 bytes；artifact archive SHA-256 `324ed884d9938548d2d35b94cd372a4bc46bf081f7abd1c77e17b28d1acf1323`，保留至 2026-12-13。该 artifact 不是 Release。
- 自动化测试不等于桌面验收；M10-01..M10-10 以及 M1–M13 所有未执行的 Windows 10/11 手工项目仍保持 NOT RUN，统一延期至 M14 后。


## 17. M11 小精灵独立打包与发布包

| ID | 场景 | 自动化覆盖/结果 | Windows 桌面验收 |
|---|---|---|---|
| M11-01 | 多入口与 self-contained 运行时 | builder/Core tests；拒绝不完整 framework-dependent 目录 | NOT RUN（M14） |
| M11-02 | 静态 require 递归 | 扫描器忽略注释/字符串、递归包含模块；动态 require 拒绝 | NOT RUN（M14） |
| M11-03 | 图像资源 | LoadImage 相对路径、工程根限制、manifest/assets/hash 验证、动态资源清单警告 | NOT RUN（M14） |
| M11-04 | OCR 资源 | 显式已下载模型拷贝到包内离线模型目录；模型清单验证 | NOT RUN（M14） |
| M11-05 | M10 参数 UI 与持久化 | schema/validator 复用；参数 sidecar；PackageId/EntryId 稳定升级；Worker smoke 验证 Args | NOT RUN（M14） |
| M11-06 | manifest 与完整性 | 文件列表闭合、逐文件 SHA-256、大小、SHA256SUMS、ZIP sidecar、损坏/未登记文件拒绝 | NOT RUN（M14） |
| M11-07 | 打包后真实 Worker | Windows CI 从实际 self-contained 发布目录生成包，启动改名 EXE 并通过 Worker 执行 require + Args | 自动化 PASS（Run 229；真实 self-contained 包/Worker smoke 1/1 PASS） |
| M11-08 | 运行控制 | Start/Pause/Resume/Stop、循环/间隔、热键、自动启动、单实例、重试与退出清理代码接线 | NOT RUN（M14，真实输入/交互） |
| M11-09 | 清洁用户环境、DPI 与视觉 | 非开发机双击、热键/参数窗体布局、缩放、多显示器 | NOT RUN（M14） |

### M11 自动化结果记录
- Run 227（head a006103910885263b3b57603fd2cb077e1544dab）SUCCESS：Restore PASS；Release Build 0 warnings / 0 errors；Core tests 110/110 PASS；API handbook check PASS；self-contained win-x64 publish PASS；实际包/Worker smoke 1/1 PASS（12 秒）；ZIP/SHA256 和 artifact upload PASS。artifact ID 14012924919，87,142,009 bytes，digest e3765922f3719d6b8d0b4994d31ee8fed4c5dea3189ee52b60c4bb0575ad0cd。
- Run 228（head 86de4e6f5e7abbfeba7d8219480e24a07e7329b6）SUCCESS：Build 0 warnings / 0 errors；Core tests 110/110 PASS；API handbook check、自包含发布、实际包/Worker smoke 1/1 PASS（10 秒）、ZIP/SHA256 和 artifact upload PASS。artifact ID 14014515367，87,142,240 bytes，digest f5bbc0e2c233cf71a52ffda2793b7e516a86ffbd4ef97d059fc475062b500e2a。
- Run 229（head 7e2ed0f52c08b5b76069d405688c8af2aa0ec097）SUCCESS：Build 0 warnings / 0 errors；Core tests 110/110 PASS；API handbook check PASS；self-contained win-x64 publish PASS；实际小精灵 EXE/Worker smoke 1/1 PASS（33 秒，包含包内 Markdown 函数参考）；ZIP/SHA256 和 artifact upload PASS。artifact ID 14014490869，87,162,650 bytes，archive digest sha256:6a7bfaee6d0af15f3e80a6c48071a62c8459e9c0db2e4fc450568e317bd6654b，有效期至 2026-12-13。Run 229 是包含离线 Markdown 手册后的最终代码 HEAD 验证。
- 旧的中间 CI 失败均已修复：Run 221 nullable compile、Run 222 图像资源基准路径测试、Run 224/225 manifest/namespace 编译、Run 226 xUnit2020 分析器规则。最终状态以 Run 229 为准。
- 本地执行环境没有 .NET SDK；本地 Build/Test NOT RUN。GitHub Actions Windows runner 是构建和自动化测试来源。
