# Lua 5.4 自动化命令 API 规格

> 这是当前运行时 API 契约与完整目标 API 设计的技术规格。当前函数与目标函数的状态以 [中文函数/参数参考手册](FUNCTION-REFERENCE-CN.md) 和 [完整功能矩阵](FEATURE-MATRIX-CN.md) 为准。

- Lua 版本：5.4
- API 版本：0.9（M12 文件操作扩展；仅 main 开发版，尚未发布到 preview.4）
- 命令风格：全局英文函数名 + 标准 Lua 函数调用语法
- 时间单位：毫秒
- 坐标单位：虚拟桌面物理像素
- 参数错误：抛出带脚本文件和行号的 Lua 错误
- 用户停止：以 Cancelled 结束，不作为普通脚本异常

## 1. 语言与脚本形式

使用标准 Lua 5.4，不自创 Q 语言解析器。支持局部变量、表、循环、条件、函数、闭包和错误处理。

首版命令采用直观的全局函数形式，不要求用户记忆 Mouse / Keyboard 命名空间：

```lua
MoveTo(122, 100)
LeftClick(50)
Delay(300)

KeyPress("ENTER")
TypeText("Hello")
```

扩展名为 `.lua`；编码 UTF-8；换行兼容 CRLF/LF。

## 2. 鼠标命令

| 中文名称 | API | 说明 |
|---|---|---|
| 移动鼠标 | `MoveTo(x, y)` | 移动到虚拟桌面绝对坐标 |
| 左键单击 | `LeftClick(holdMs?)` | 左键按下指定时长后释放 |
| 右键单击 | `RightClick(holdMs?)` | 右键按下指定时长后释放 |
| 中键单击 | `MiddleClick(holdMs?)` | 中键按下指定时长后释放 |
| 左键双击 | `LeftDoubleClick(holdMs?, gapMs?)` | 左键连续单击两次 |
| 右键双击 | `RightDoubleClick(holdMs?, gapMs?)` | 右键连续单击两次 |
| 中键双击 | `MiddleDoubleClick(holdMs?, gapMs?)` | 中键连续单击两次 |
| 左键按下 | `LeftDown()` | 按下并保持，直到 LeftUp 或统一清理 |
| 左键释放 | `LeftUp()` | 释放左键 |
| 右键按下/释放 | `RightDown() / RightUp()` | 按下或释放右键 |
| 中键按下/释放 | `MiddleDown() / MiddleUp()` | 按下或释放中键 |
| 滚轮 | `MouseWheel(delta)` | 正数向上、负数向下；120 为一个标准刻度 |


### 点击持续时间（重要）

- `LeftClick()` 等省略参数时，默认按下 **50 ms**，然后释放。
- `LeftClick(50)` 表示按下 50 ms 后释放；`LeftClick(100)` 表示按下 100 ms 后释放。
- 双击函数默认每次按下 50ms、两次点击之间间隔 100ms；可分别传入 holdMs 与 gapMs。
- 参数是按下持续时间，不是两次点击之间的间隔。
- 显式传入 `0` 表示按下与释放连续发送，不增加人为等待。
- 时间参数必须是非负整数毫秒；超过 600,000 ms 时应报参数错误。需要更长的按住过程时，使用 Down / Delay / Up。
- 点击动作属于一个完整动作。停止请求必须能够中断动作并立即释放本程序持有的鼠标按钮；暂停请求在当前点击动作完成后生效。
- 点击的按下/等待/释放必须走同一套输入状态追踪和异常清理逻辑，不能只在正常路径释放。

示例：

```lua
MoveTo(700, 450)
LeftClick()       -- 默认 50 ms
RightClick(100)   -- 右键保持 100 ms
LeftDown()
Delay(500)
LeftUp()
```

## 3. 键盘命令

| 中文名称 | API | 说明 |
|---|---|---|
| 单键点击 | `KeyPress(key, holdMs?)` | 按下指定时长后释放；默认 50 ms |
| 按下按键 | `KeyDown(key)` | 按下并保持 |
| 释放按键 | `KeyUp(key)` | 释放指定按键 |
| 组合键 | `Hotkey(modifier1, ..., key)` | 按参数顺序按下，逆序释放 |
| 输入文本 | `TypeText(text)` | 输入 Unicode 文本 |

首批规范键名：`ENTER`、`ESC`、`TAB`、`SPACE`、`BACKSPACE`、`DELETE`、方向键、`CTRL`、`SHIFT`、`ALT`、`WIN`、`F1..F24`、`A..Z`、`0..9`、常见标点键及数字小键盘键。

示例：

```lua
KeyPress("ENTER")
Hotkey("CTRL", "SHIFT", "S")
TypeText("按键精灵 2026")
```

- `KeyPress` 的可选时长规则与鼠标 Click 相同：默认 50 ms，显式 0 表示不增加等待，最大 600,000 ms。
- Down 与 Up 必须配对；脚本异常或停止时释放仍由本程序持有的键。
- `TypeText` 提供 Unicode 文本注入能力，不承诺重建物理输入法的原始组合过程。
- 物理按键录制与输入法最终生成的文字不是同一层数据。

## 4. 时间、日志与流程

| 中文名称 | API | 说明 |
|---|---|---|
| 延时 | `Delay(milliseconds)` | 可暂停、可取消的毫秒延时 |
| 随机延时 | `RandomDelay(minMs, maxMs)` | 两端均包含的整数毫秒随机延时 |
| 日志 | `Log(message)` | 输出普通信息 |

循环、条件、变量和函数全部使用 Lua 原生语法：

```lua
for i = 1, 5 do
    if i % 2 == 0 then
        Log("偶数轮次")
    end
    Delay(200)
end
```

## 5. 坐标策略

### 5.1 当前已实现语义

MoveTo(x, y) 的既有契约始终是虚拟桌面绝对物理像素。主屏左上角通常为 (0,0)；位于主屏左侧或上方的副屏允许出现负坐标。M6 新增的窗口与坐标函数不改变 MoveTo 的签名、参数或语义。

录制、生成和回放必须使用一致的坐标语义；不得混用逻辑 DPI 坐标、单屏客户区坐标和虚拟桌面物理像素。

### 5.2 当前实现的坐标空间

M6 已在 main 开发版提供以下显式坐标空间：

- `screen`：虚拟桌面绝对物理像素，允许副屏负坐标。
- `window`：相对目标窗口外框左上角；外框采用 Win32 GetWindowRect，包含标题栏与非客户区。
- `client`：相对客户区左上角；通过 ClientToScreen 转为屏幕物理像素，不含标题栏和边框。
- `anchor`：相对窗口外框九宫格锚点（top-left、top-center、top-right、center-left、center、center-right、bottom-left、bottom-center、bottom-right）的偏移。
- `ratio`：相对客户区宽高的归一化坐标，(0,0) 到 (1,1) 对应客户区左上至右下边界。

统一转换单位为 Windows Per-Monitor-V2 感知进程下的物理像素。矩形采用左/上包含、右/下不包含约定；比率 ToScreen 输入限制在闭区间 0–1，反向转换保留窗口外的负数或大于 1 的比例，便于判断越界。物理像素转换最终取整时使用四舍五入、半值远离 0。

通过 `CoordinateToScreen(id, space, x, y, anchor?)` 与 `CoordinateFromScreen(id, space, x, y, anchor?)` 执行双向转换；`id` 必须是当前运行内有效的安全窗口引用。`anchor` 只在 `space="anchor"` 时使用。

### 5.3 当前实现的窗口 API

窗口引用是随机不透明字符串，不向 Lua 暴露原始 HWND。`WindowFind` / `WindowEnumerate` 支持标题包含匹配、类名精确匹配、进程名、PID 和可见性筛选；`WindowEnumerateChildren(id)` 枚举目标窗口的所有子窗口后代（最多 512 项）。`WindowGetInfo(id)` 返回标题、类名、进程、可见/最小化/最大化、前台/焦点标志、`isChild`、外框矩形、客户区屏幕矩形和销毁跟踪状态。

窗口操作函数：

| API | 行为 |
|---|---|
| `WindowActivate(id)` | 还原并请求前台激活；Windows 前台锁定策略可能拒绝，返回 false |
| `WindowShow(id)` / `WindowHide(id)` | 显示/隐藏 |
| `WindowMinimize(id)` / `WindowMaximize(id)` / `WindowRestore(id)` | 最小化、最大化、还原 |
| `WindowSetTopmost(id, enabled?)` | 置顶或取消置顶 |
| `WindowMove(id, x, y)` | 按屏幕物理像素移动窗口外框左上角，允许负坐标；对子窗口会先换算为父客户区坐标再调用 SetWindowPos |
| `WindowResize(id, width, height)` | 调整窗口外框物理像素宽高（1–32768）；对子窗口保留原屏幕位置并换算父客户区坐标 |
| `WindowAlign(id, anchor?, offsetX?, offsetY?)` | 在目标窗口所在显示器的工作区按九宫格锚点对齐，可加偏移；对子窗口转换为父客户区坐标 |
| `WindowWait(..., timeoutMs?)` | 等待窗口出现；超时返回 nil，默认 30000ms，最大 600000ms |
| `WindowWaitClosed(id, timeoutMs?)` | 等待窗口关闭；关闭返回 true，超时返回 false |

`WindowFind`/`WindowWait` 未命中返回 `nil`。窗口枚举最多 512 个结果。窗口引用只在当前脚本运行期间有效；每轮脚本结束均清空引用。目标窗口销毁事件由 `SetWinEventHook(EVENT_OBJECT_DESTROY)` 跟踪，并在每次操作前再校验 HWND、PID、线程 ID、类名和进程启动时间。若销毁事件跟踪无法安装则拒绝建立引用；引用失效时抛出错误，要求重新查找，避免旧 HWND 被复用后误操作。`WindowWait` 的取消由脚本停止令牌终止，不会把取消伪装成超时。

这些函数仅处理当前交互桌面的 Win32 窗口元数据与窗口布局；不读取/设置控件值、不提供后台输入、不绕过 UIPI/UAC 或安全桌面，也不承诺跨用户会话访问。Windows 实机 DPI、多显示器和权限验收仍需单独进行。

### 5.4 M7 控件自动化与输入后端

以下 API 只在 main 开发版可用，未包含在已发布的 `v0.1.0-preview.4` 包中。

```lua
local w = WindowFind("目标窗口", "", "notepad", 0, true)
if not w then error("窗口未找到") end

local binding = WindowBind(w.id)
local caps = WindowGetCapabilities(binding)
for _, cap in ipairs(caps.capabilities) do
    Log(cap.name .. ": " .. cap.status .. " / " .. cap.backend)
end

local controls = ControlEnumerate(binding, 256)
for _, c in ipairs(controls) do
    Log(c.name .. " / " .. c.controlType .. " / " .. table.concat(c.supportedPatterns, ","))
end
WindowUnbind(binding)
```

- `WindowBind(id)` 接收当前运行内的 M6 不透明窗口 ID，返回本轮随机 `bindingId`；脚本不会得到 HWND 或 .NET/UIA 对象。
- `ControlEnumerate(bindingId, maxResults?)` 最多返回 512 项、遍历深度最多 16 层。每项包括 `controlId`、名称、AutomationId、类名、类型、物理像素矩形、启用/屏外/焦点状态、密码标记和当前 provider 暴露的 Pattern。
- `WindowGetCapabilities(bindingId)` 逐项报告控件树、Value/Text、Selection/SelectionItem、Toggle、Invoke、前台鼠标/键盘/文本与通用后台注入能力。状态包含 `available`、`conditional` 或 `unsupported`，并附带原因与限制。能力报告不是对每个控件都支持所有 Pattern 的保证。
- `ControlGetInfo`、`ControlGetValue`、`ControlSetValue`、`ControlGetText`、`ControlGetSelection`、`ControlSelect`、`ControlGetToggleState`、`ControlToggle` 和 `ControlInvoke` 只调用该控件实际暴露的 UIA Pattern。不支持时明确报错；控件销毁/重建后必须重新枚举。为避免凭据泄露，密码字段的 Value/Text 读取会被拒绝。
- UIA 的 Value/Selection/Toggle/Invoke 是**控件语义操作**，在目标 provider 允许时可以对非前台窗口操作；它们不等于后台键盘/鼠标消息注入。
- `WindowInput(bindingId, backend, action, name?, x?, y?, value?, holdMs?)` 的 `backend` 只能是 `auto` 或 `foreground`；两者在 M7 均明确使用前台 SendInput。目标窗口必须可见、未最小化且在前台；键盘/文本还要求目标窗口中已有键盘焦点。move/click 的屏幕物理像素坐标必须位于该窗口客户区。不会自动激活/抢焦点，也不会静默回退。
- 通用后台鼠标、键盘和文本注入当前明确不支持；能力表会返回 `unsupported`。不使用伪造 PostMessage 冒充后台输入，不绕过 UIPI、UAC、安全桌面或更高完整性级别的限制。
- UIA 运行在每轮独立 Lua worker 进程内。若第三方 provider/COM 调用挂起，主进程仍能终止该 worker；主进程仍负责验证 M6 窗口引用和执行前台 SendInput。窗口身份在每次控件 API 调用前复核，控件/binding ID 不跨脚本轮次持久化。

### 5.5 录制与回放要求

当前 M5 的 `RecorderDocument.CoordinateSpace` 固定为 `virtual-desktop-physical-pixels`；`RecorderDocumentBuilder` 保存全局 Hook 捕获到的虚拟桌面屏幕物理像素，`RecorderLuaGenerator` 生成 `MoveTo(x, y)`。M6 没有改变该 sidecar schemaVersion 1 或既有录制文件的含义，也没有把临时窗口 GUID 写入文件（该 GUID 只在当前脚本运行期间有效）。因此目前录制/回放的定义是明确的 screen 绝对坐标模式；按键/鼠标原有动作不回归，但窗口移动后不会自动跟随窗口。持久窗口选择器、窗口/客户区/ratio 坐标录制及回放重定位仍未实现，后续需要设计可持久化的窗口匹配条件和 schema 迁移。`CoordinateToScreen` / `CoordinateFromScreen` 可供脚本在运行时显式换算，不能视为 Recorder 已支持相对录制。窗口坐标转换成功也不代表后台输入成功；后台输入属于后续阶段独立能力和权限契约。

## 6. 返回值与错误契约

- 首版动作命令不承诺返回值；成功时继续执行下一条 Lua 语句。
- 输入注入失败、参数无效或 API 使用错误会抛出 Lua error，终止当前脚本并在运行日志中显示错误堆栈/可用行号。
- 用户停止由执行器识别为 Cancelled，不作为普通脚本异常。
- 用户停止：执行器产生 Cancelled 状态，不能吞成 nil 后继续运行。
- API 不返回 Win32 指针、Lua userdata 或 UI 控件对象。
- 所有按下状态由统一输入控制器追踪；脚本完成、停止或异常退出时执行 ReleaseAllOwnedInputs。

## 7. Lua 标准库暴露策略

允许：基础语法、string、table、受限 math、utf8。

默认不暴露：任意 .NET 类型导入、io、os、package、debug、原生库加载、任意 Shell 执行。后续文件/进程能力必须设计为用户可见、可授权的专用 API。

这是一种减少误操作的受限脚本环境，不应宣称为对本机恶意脚本的安全边界。

## 8. API 注册与兼容

- 唯一机器可读来源：`src/Anjian.Core/Api/lua-api-registry.json`，登记 94 个当前公开函数的稳定 ID、分类、签名、参数 schema、默认值/范围、返回值/错误、暂停/取消、坐标/权限、示例、实现状态和版本。
- `Anjian.Core.Api.LuaApiRuntime` 从该注册表生成 Lua 全局包装；内部 `__...` 桥接绑定在包装闭包建立后隐藏。
- `docs/FUNCTION-REFERENCE-CN.md` 第 2 节由 `tools/Anjian.ApiDocsGenerator` 生成；使用 `--write` 更新，`--check` 验证是否漂移。
- 注册表只包含真实当前实现。M6 窗口/坐标、M7 UIA、M8 图像资源/视觉调试与 M9 OCR 首轮 API 已进入 main；尚未完成的 M10+ 功能仍属于后续阶段，不会被伪装成可调用 API。
- 后续 IDE 补全/参数提示/命令面板应读取 `LuaApiRegistry.Load().Categories` 与 `.Apis`，禁止另建静态函数清单。

- API 注册中心维护命令标识、中文名、分类、参数 schema、默认值、返回值、示例、最低 API 版本和弃用状态。
- 编辑器补全、参数提示、命令面板和文档从同一注册描述生成。
- 已发布 API 不随意改名或改变参数语义。
- 有破坏性更改时增加 API 版本并提供迁移说明。
- 全局函数是用户脚本层 API；内部 C# 仍按 Mouse / Keyboard / Runtime 等模块分层，避免把全局 API 设计泄漏为内部架构。

## 9. M8 截图、图色与模板识别（API v0.5，main 开发版）

本节中的新 API 当前仅存在于 main 开发版，未包含于 v0.1.0-preview.4 Release。完整参数、默认值与结构以自动生成的函数/参数参考手册第 2 节为准。

| 能力 | 当前公开函数 | 约定 |
|---|---|---|
| 截图/资源 | CaptureScreen、CaptureWindow、CaptureClient、CaptureRegion、LoadImage、SaveScreenshot、ReleaseImage、GetImageSize、CropImage | 资源 ID 仅在当前运行轮次有效；轮次结束自动清理；PNG 文件读写；截图的屏幕原点/负坐标会保留 |
| 像素/找色 | GetPixelColor、CompareColor、FindColor、FindMultiColor、CountColors | RGB 颜色为 0xRRGGBB；tolerance 是每个通道的最大绝对差；区域以图像局部零基像素表示 |
| 模板找图 | FindImage、FindImages | similarity 为 0–1；支持透明模板像素、可选灰度模式与 0.25–4 倍最近邻缩放；多结果上限 1000 |
| 等待 | WaitImage、WaitImageGone、WaitColor、WaitColorGone | 可配置 timeout/interval 和 1–20 次连续确认；Stop 取消搜索循环；超时返回 nil 或 false，不伪装成命中 |

### 9.1 资源和性能上限
- 单张图像最多 32,000,000 像素；单轮最多 128 个资源和 256 MiB 像素缓存。使用 ReleaseImage(id) 及时释放无用图像。
- 单次模板搜索最多 50,000,000 次像素比较；超出上限会明确报错，建议缩小区域/模板或增大 step。
- FindColor、FindMultiColor、CountColors 的 width/height 为 0 表示延伸至图像右/下边界；step 影响候选像素采样间隔。
- FindImage/FindImages 默认 similarity=0.95、grayscale=false、scale=1；灰度比较使用亮度加权值，缩放使用最近邻，不做平滑插值。
- Wait 系列 stableCount 默认 1。增加该值需要连续多次检测均满足条件；反向条件会重置连续计数。等待期间尊重暂停和 Stop。

### 9.2 截图权限与可见性边界
- CaptureScreen 捕获 Windows 虚拟桌面的可见合成画面；GetImageSize 返回尺寸与 hasScreenCoordinates、screenOriginX、screenOriginY。
- CaptureWindow 与 CaptureClient 只接受可见、未最小化的窗口。底层是屏幕区域捕获；窗口被覆盖的部分不能保证得到目标窗口真实后台内容，硬件加速/受保护表面可能不可读。
- 不绕过 UIPI、UAC、安全桌面或其他访问控制。截图 API 返回结果不代表通用后台输入受到支持。
- 模板预览/命名/版本校验 UI、视觉调试 overlay、颜色差值/颜色块工具仍为 M8 剩余工作；OCR 已在 M9 以本地离线 API 接入 main，详见第 10 节；仍未发布到 preview.4。


## 8. M8 RGB、颜色块与图像资源扩展（API 0.6）

| API | 签名 | 返回 |
|---|---|---|
| RGB 转十六进制 | RgbToHex(red, green, blue) | #RRGGBB 大写字符串 |
| 十六进制转 RGB | HexToRgb(hex) | red、green、blue、hex、color 字段的表 |
| 颜色差值 | ColorDifference(first, second) | 各通道绝对差、maxChannel、manhattan、euclidean |
| 颜色块 | FindColorBlocks(imageId, color, tolerance?, minPixels?, left?, top?, width?, height?, step?, maxResults?) | 按采样点数排序的八邻域连通块数组 |

ColorDifference 的三类差值分别为：最大单通道绝对差、三个通道差绝对值之和、三个通道差的欧氏距离。颜色通道均为 0–255；十六进制输入只接受 6 位 RRGGBB 以及可选 # / 0x 前缀。

FindColorBlocks 的 step 是采样网格步长，pixelCount 指命中采样点数量，并非原始连续像素面积；块以八邻域（含对角）相连。单次最多扫描 4,000,000 个采样点，最多返回 100 个块；minPixels 和 maxResults 均有范围校验，停止可取消扫描。返回项含局部 x/y/width/height/centerX/centerY、pixelCount，截图还含 screen 绝对坐标。

脚本图像模板 UI 使用与 Lua Worker 相同的相对路径基准：相对 LoadImage 路径以脚本所在目录解析；资源管理器复制的 .assets/... 路径可以直接传入 LoadImage。资源 ID 仍只在当前脚本运行轮次内有效。


## 10. M9 OCR 文字识别 API（API 0.7；main 开发版）

本节 API 已登记在唯一注册表 `src/Anjian.Core/Api/lua-api-registry.json`，当前公开 API 总数 81。函数手册中的完整参数表由注册表自动生成。本功能仅存在于 main 开发版，不在 v0.1.0-preview.4 中。

### 10.1 OCR(imageId, language?, minConfidence?)
- `language`：`ch`（简体中文，默认）、`en`、`japan`；当前首轮只开放这三种语言。
- `minConfidence`：0–1，低于阈值的文本行会被过滤。
- 返回 table：`text`、`lineCount`、`lines`、`languages`、`detectedLanguages`、`width`、`height`、`durationMs`、`hasScreenCoordinates`、`screenOriginX/Y`。每个 line 含 1 起始 index、text、confidence、x/y/width/height、polygon；截图来源另含 screenX/screenY。
- 坐标：x/y/width/height 与 polygon 是图像局部零基像素；screenX/screenY 是虚拟桌面绝对物理像素。加载磁盘图片没有 screen 坐标，不返回 screenX/screenY。

### 10.2 FindText(imageId, text, language?, minConfidence?, matchMode?)
- `matchMode`：`contains`（默认）或 `exact`；不区分大小写，按 OCR 阅读顺序返回第一个满足置信度阈值的行。
- 未命中返回 nil；命中结构与 OCR 单行相同。空文本、无效模式、语言或置信度会抛出明确 Lua 错误。

### 10.3 WaitText(text, timeoutMs?, intervalMs?, language?, minConfidence?, matchMode?)
- 默认 timeout 10000ms、interval 300ms、语言 ch、置信度 0、模式 contains；timeout 范围 0–600000ms，interval 10–5000ms。
- 反复截取当前可见虚拟桌面并 OCR；命中返回单行结果，超时返回 nil。timeout=0 仍执行一次检测。
- 每轮截图前检查暂停；Stop 终止独立 Worker。临时截图在 finally 路径释放。
- 桌面截图使用可见合成像素；不保证获得遮挡窗口后台内容，不绕过 UIPI/UAC/安全桌面。

### 10.4 模型与离线行为
- 在主窗口工具栏点击“OCR 模型”，选择简体中文/英语/日语并明确下载所需模型包。
- 下载目录：`%LOCALAPPDATA%\\Anjian2026\\Ocr\\Models`。下载由上游模型清单进行 SHA-256 校验。
- 脚本识别默认严格离线。未安装所选语言模型时明确报错并提示下载；不会自动联网或静默下载。
- 每张 OCR 图像最多 16,000,000 像素，PNG IPC 编码最多 64 MiB；过大的图像先调用 CropImage。
- 当前接口的真实识别质量、模型耗时、断网行为和 Windows 多屏坐标均需在 M14 结束后的 Windows 10/11 集中桌面验收中完成。

## 11. M12 文件/目录 API（API 0.9；main 开发版）

本节 API 仅存在于 main 开发版，不包含在 v0.1.0-preview.4 中。当前 API registry 为 0.9 / 94 项；完整参数表与错误契约以自动生成的函数参考手册第 2 节为准。

| API | 行为与授权 |
|---|---|
| CopyFile(sourcePath, destinationPath, overwrite?) | 只复制普通文件，不复制目录；源路径要求读取授权，目标要求写入授权。最大 64 MiB，默认不覆盖。先写目标目录内的随机临时文件，flush、SHA-256 校验后再原子提交。 |
| MoveFile(sourcePath, destinationPath, overwrite?) | 只移动普通文件；源路径同时要求读取和写入授权，目标要求写入授权。同卷使用句柄级 rename；跨卷先复制、flush、SHA-256 校验并提交目标，再删除源。跨卷上限 64 MiB。源删除失败时保留源文件并报告已提交目标，便于恢复。 |
| RenameFile(path, newName, overwrite?) | 只在同一父目录内重命名普通文件；源路径要求读取和写入授权，目标名必须是单一文件名。默认不覆盖。 |
| CreateTempFile(directoryPath) | 在调用者明确指定且已获批准写入的目录内创建空临时文件，返回完整路径。 |
| CreateTempDirectory(parentDirectoryPath) | 在调用者明确指定且已获批准写入的目录下创建一级临时目录，返回完整路径。 |

- CopyFile / MoveFile / RenameFile 均拒绝符号链接、reparse point 和多硬链接文件；不跟随源文件或覆盖目标中的链接。
- 临时文件/目录的随机名称和 CreateNew 仅用于避免覆盖现有条目；API 不会自动访问系统 TEMP，也不会扩大批准根。调用者必须明确提供已授权写入目录。
- 所有新增文件 IPC 同时经过 Worker bridge 与 PlaybackRunner 主进程 dispatcher 的 Grant 检查。Stop 在复制分块与提交前检查；同卷 rename 是单个句柄操作。
- 本批 API 仍需 Windows 10/11 实机文件权限/真实桌面验收；CI 自动化测试不替代 M14 集中验收。
