# 按键精灵 2026：Lua 函数与参数参考手册

- 目标读者：脚本编写者与自动化工程维护者
- 对应运行时：Lua 5.4
- 当前产品版本：v0.1.0-preview.4
- 文档状态：持续维护中的第一版参考手册
- 当前坐标规则：已实现函数仍使用虚拟桌面绝对物理像素坐标
- 重要声明：本手册明确区分“当前 API”和“完整产品目标 API”。标为计划中的函数现在不能被当成已实现函数调用。

## 1. 如何阅读本手册

每个公开函数最终都必须记录以下字段：类别、稳定名称、签名、用途、参数类型、必填性、默认值、范围与单位、返回值、可能的错误、暂停/停止行为、副作用、坐标空间、最低 API 版本、示例和状态。

状态说明：

- **当前可用**：已进入当前代码基线；仍需遵守本文写明的限制。
- **CI 已测，实机待验收**：自动化/Windows CI 覆盖过，但真实 Windows 桌面行为仍待验证。
- **目标设计**：属于完整产品正式范围，但尚未实现或签名仍需经过设计评审。
- **示例/候选名称**：用于表达预期功能；实现前应进入 API 注册表并冻结正式签名。

### 当前 API 错误约定

当前运行器对无效参数和输入注入失败返回 Lua 错误，运行日志尽可能包含脚本文件和行号。脚本主动停止会被标记为 Cancelled，而不是普通错误。当前鼠标/键盘动作多数不返回值；未来 API 必须明确使用的返回值结构，不能有时返回 nil、有时返回未定义的字符串格式。

## 2. 当前已实现的公开 API

### 2.1 鼠标

| 函数签名 | 参数说明 | 返回值/行为 | 状态 |
|---|---|---|---|
| **MoveTo(x, y)** | x：整数，虚拟桌面物理像素 X；y：整数，物理像素 Y；副屏可为负坐标 | 无返回值；定位到屏幕绝对坐标 | 当前可用；多屏/DPI 实机待验收 |
| **LeftClick(holdMs?)** | holdMs：非负整数毫秒；缺省 50；0 表示不增加人为等待；范围 0–600000 | 左键按下并释放；成功无返回值；停止/错误应释放 | CI 基础验证；实机待验收 |
| **RightClick(holdMs?)** | 同 LeftClick | 右键按下并释放 | CI 基础验证；实机待验收 |
| **MiddleClick(holdMs?)** | 同 LeftClick | 中键按下并释放 | CI 基础验证；实机待验收 |
| **LeftDoubleClick(holdMs?, gapMs?)** | holdMs 缺省 50ms；gapMs 缺省 100ms；两者均为非负整数毫秒 | 连续执行两次左键单击 | 需要实机验证实际双击阈值 |
| **RightDoubleClick(holdMs?, gapMs?)** | 同左键双击参数 | 连续执行两次右键单击 | 需要实机验证 |
| **MiddleDoubleClick(holdMs?, gapMs?)** | 同左键双击参数 | 连续执行两次中键单击 | 需要实机验证 |
| **LeftDown() / LeftUp()** | 无参数 | 按下/释放左键；用于拖动或长按 | 当前 API |
| **RightDown() / RightUp()** | 无参数 | 按下/释放右键 | 当前 API |
| **MiddleDown() / MiddleUp()** | 无参数 | 按下/释放中键 | 当前 API |
| **MouseWheel(delta)** | delta：整数；正数向上、负数向下；120 为一个标准刻度 | 滚动鼠标滚轮 | 当前 API；不同应用的响应程度可能不同 |

点击函数是完整动作，停止时必须走清理路径并释放本程序持有的鼠标按钮。暂停请求在当前原子点击完成后生效。需要更长的持续按住过程时，优先用 Down、Delay、Up，并确保异常情况下也能释放。

示例：

    MoveTo(700, 450)
    LeftClick()
    RightClick(100)
    LeftDown()
    Delay(500)
    LeftUp()

### 2.2 键盘

| 函数签名 | 参数说明 | 返回值/行为 | 状态 |
|---|---|---|---|
| **KeyPress(key, holdMs?)** | key：规范键名字符串；holdMs：非负整数毫秒，缺省 50，范围 0–600000 | 按下指定键后释放；成功无返回值 | 当前 API；键盘布局/权限受 Windows 约束 |
| **KeyDown(key)** | key：规范键名字符串 | 按下并保持，直到 KeyUp 或统一清理 | 当前 API |
| **KeyUp(key)** | key：规范键名字符串 | 释放指定键 | 当前 API |
| **Hotkey(modifier1, ..., key)** | 多个按键名字符串；按传入顺序按下、逆序释放；常见例为 CTRL、SHIFT、S | 发送组合键 | 当前 API |
| **TypeText(text)** | text：UTF-8/Lua 字符串，支持 Unicode 文本内容 | 输入文本；不保证重建物理输入法原始组合过程 | 当前 API；IME 行为需理解限制 |

首批规范键名包括 ENTER、ESC、TAB、SPACE、BACKSPACE、DELETE、方向键、CTRL、SHIFT、ALT、WIN、F1–F24、A–Z、0–9、常见标点键及数字小键盘键。正式注册表必须维护完整且可校验的键名清单；无效名称应抛出明确参数错误。

示例：

    KeyPress("ENTER")
    Hotkey("CTRL", "SHIFT", "S")
    TypeText("按键精灵 2026")

脚本异常、停止或执行器退出时必须释放本程序持有的键。物理录制得到的按键事件与输入法最终上屏的 Unicode 文本不是同一层数据。

### 2.3 时间与随机数

| 函数签名 | 参数说明 | 返回值/行为 | 状态 |
|---|---|---|---|
| **Delay(milliseconds)** | milliseconds：非负整数毫秒 | 可暂停、可取消的延时；执行期间 UI 不冻结 | 当前 API |
| **RandomDelay(minMs, maxMs)** | 两个非负整数毫秒；minMs 不得大于 maxMs | 在闭区间 [minMs, maxMs] 中选择整数毫秒并等待 | 当前 API |
| **Log(message)** | message：要记录的文本消息 | 输出普通级别运行日志；不作为持久化配置 | 当前 API |

示例：

    Delay(500)
    RandomDelay(100, 300)
    Log("本轮完成")

Delay 与运行设置里的“每轮间隔”独立。循环次数设置为 N 表示完整脚本执行 N 轮；0 表示无限循环。每轮间隔仅发生在两轮之间。

### 2.4 Lua 语言本身

脚本语言是标准 Lua 5.4。变量、局部变量、表、条件、循环、函数、闭包和错误处理使用 Lua 原生语法，不是本项目另外实现的一套 Q 语言语法。

示例：

    local count = 0
    for i = 1, 5 do
        if i % 2 == 0 then
            count = count + 1
            Log("偶数轮次")
        end
        Delay(200)
    end
    Log("偶数计数完成")

可用标准库以当前运行时实际暴露集合为准。当前执行环境不默认暴露任意 .NET 类型导入、io、os、package、debug、原生库加载或任意 Shell 执行能力。此受限执行环境是为了降低误操作和不必要的本机权限，并不构成对恶意脚本的安全沙箱承诺。

## 3. 当前参数约定

### 3.1 数值和时间单位

- 坐标单位为虚拟桌面物理像素。
- 时间参数单位为毫秒，不是秒。
- 需要整数的参数不接受小数。
- 不允许的负值、超范围值、无效键名或错误类型必须在执行前或 API 入口明确报错。
- 目前 Click/KeyPress 的规范范围统一为 0–600000ms；显式 0 表示不增加人为等待。超过范围时应报参数错误；需要长时间保持时使用 Down/Delay/Up。
- 长时间运行、网络、文件、图像识别等未来 API 必须提供明确超时和取消约定。

### 3.2 坐标空间

当前已实现的 MoveTo(x, y) 只使用 screen 绝对坐标。完整目标还包括：

- screen：虚拟桌面绝对物理像素。
- window：相对目标窗口外框基准。
- client：相对目标窗口客户区基准。
- anchor：相对窗口的指定锚点偏移。
- ratio：客户区宽高归一化的比例位置。

这些未来模式必须由显式参数或独立函数选择，不能悄悄改变 MoveTo(x, y) 的现有屏幕绝对坐标语义。具体函数签名在实现前统一进入 API 注册表。

## 4. 完整产品目标中的函数类别

下面列出完整目标的全部核心 API 类别。除第 2 节明确列出的函数外，均为目标设计，不应在 preview.4 中直接调用。正式签名、默认值和返回结构必须在对应实现阶段冻结。

| 类别 | 目标函数 / 能力（候选名称不等于已实现） | 参数/返回约定必须定义的内容 |
|---|---|---|
| 坐标转换 | MoveToWindow、MoveToClient、MoveToAnchor、MoveToRatio、ScreenToClient、ClientToScreen、WindowToScreen | 窗口引用、坐标、锚点/比例、坐标空间，返回换算坐标或错误状态 |
| 窗口查找 | FindWindow、FindWindows、FindWindowByProcess、GetForegroundWindow、GetMousePointWindow | 标题/类名/进程过滤、匹配规则、超时；返回安全窗口引用和未找到状态 |
| 窗口布局 | GetWindowRect、GetClientRect、MoveWindow、ResizeWindow、AlignWindow、SetWindowState、WaitWindow | 窗口引用、位置/尺寸/锚点/状态、超时；区分外框与客户区 |
| 控件自动化 | FindControl、GetControlText、SetControlText、ClickControl、SelectControl | 控件路径、选择条件、后端能力、成功/失败原因；只对声明支持的控件保证工作 |
| 输入后端 | BindWindow、UnbindWindow、GetInputCapabilities、SendBackgroundText | 目标窗口、后端类型、动作；返回能力描述或明确错误，不伪报成功 |
| 截图 | CaptureScreen、CaptureWindow、CaptureRegion、SaveScreenshot | 区域/窗口引用、坐标空间、输出文件/图像数据、超时与资源释放 |
| 颜色识别 | GetPixelColor、FindColor、FindMultiColor、CompareColor、CountColors | 点/区域、颜色格式、容差、步长、阈值；返回结构化命中结果 |
| 图像识别 | FindImage、FindImages、WaitImage、WaitImageGone | 模板、区域、相似度、超时、结果数；返回命中坐标/置信信息/失败类型 |
| OCR | OCR、FindText、WaitText | 区域、语言、字库、阈值、超时；返回文本、矩形和置信度 |
| 参数/配置 | DefineParam、GetParam、GetConfig、SetConfig、ResetConfig | 参数名、类型/默认值/范围/选项；校验错误和持久化策略 |
| 文件与目录 | FileExists、ReadFile、WriteFile、CopyFile、MoveFile、CreateDirectory、EnumerateFiles | 路径、编码、覆盖策略、超时；返回值和权限/路径错误 |
| 剪贴板/对话框 | GetClipboardText、SetClipboardText、MessageBox、InputBox、Confirm | 文本、标题、默认值、按钮/选项；返回用户选择并处理取消 |
| 进程与应用 | RunApp、FindProcess、WaitProcess、GetProcessInfo、CloseProcess | 程序路径/参数、PID、超时和结束策略；返回启动结果或权限错误 |
| 网络/数据 | HttpRequest、HttpGet、HttpPost、DownloadFile、UploadFile、JsonDecode、JsonEncode | URL、方法、头、正文、超时、状态码、编码；不阻塞 UI |
| 图像资源 | LoadImage、ReleaseImage、GetImageSize、CropImage | 资源路径/区域、资源句柄/ID；释放和资源缺失必须可检测 |
| 任务运行 | GetRunState、GetElapsedTime、SetTimeout、RequestStop | 运行实例标识、超时/策略；返回定义明确的状态 |
| 通知与音频 | Notify、PlaySound、StopSound | 文本/声音文件/时长；错误必须不影响脚本停止 |
| 插件 | Register/manifest 级扩展函数、插件生命周期回调 | 版本、权限、入口、返回和异常约定；由插件 API 规范另行冻结 |

上表中名称是候选设计，不是稳定公开签名。具体实现前，必须将函数定义写入机器可读 API 注册表，进行代码评审后才可对用户发布。

## 5. API 注册表与文档生成规则

每条 API 描述至少包含：

- 稳定 ID、公开函数名、所属类别、中文标题、详细说明。
- 参数数组：参数名、Lua 类型、是否必填、默认值、最小/最大值、枚举项、单位、描述、敏感性和验证规则。
- 返回结构：类型、字段、成功条件、未命中和失败状态。
- 错误：参数错误、权限错误、资源缺失、超时、取消、后端不支持等。
- 运行语义：可否暂停、可否取消、是否等待、是否修改输入状态、是否涉及文件/网络。
- 坐标空间、适用输入后端、平台限制和目标窗口要求。
- 最低 API 版本、实现状态、弃用版本、替代函数和迁移说明。
- 最小示例、常见错误示例、自动测试 ID 和文档更新时间。

必须用同一注册描述生成/驱动编辑器自动补全、参数签名提示、命令面板、校验提示和本手册。不能出现代码函数已经变更而手册还写旧参数的情况。

## 6. 版本与兼容策略

- 当前实现 API 的参数含义属于兼容契约；新增功能优先新增 API 或可选参数，避免静默改变旧脚本行为。
- 对旧签名进行破坏性修改时，必须增加 API 版本并提供迁移说明。
- 计划中的函数只有完成注册、实现、测试、文档和版本记录后，才可标记为当前可用。
- 第三方插件 API 需声明兼容版本和来源；卸载插件后脚本必须给出缺失函数的明确错误。
- 每个发行包包含对应版本的函数手册；GitHub 上的文档必须能对应发行版本与源码提交。

## 7. 变更记录

- 2026-11-13：建立独立中文函数/参数参考手册；明确当前函数与完整目标函数边界；统一 Click/KeyPress 参数上限为 600000 毫秒，后续由 API 注册表作为唯一来源。
