# 按键精灵 2026 首版使用说明

> 完整产品目标、各项功能状态和函数参考分别见 [完整 PRD](PRD-CN.md)、[完整功能矩阵](FEATURE-MATRIX-CN.md) 和 [函数/参数参考手册](FUNCTION-REFERENCE-CN.md)。本文只介绍当前 v0.1.0-preview.4 已实现的界面与用法，不代表完整产品已交付。


## 1. 脚本管理

脚本默认保存在当前 Windows 用户的 %LOCALAPPDATA%\Anjian2026\Scripts。

左侧列表支持新建、打开、重命名、复制和删除；右键脚本可打开菜单。编辑器支持普通文本编辑，按 Ctrl+S 保存。录制会自动创建一个新脚本，不会覆盖当前已有脚本。

## 2. 录制

录制按钮会在“开始录制 / 暂停录制 / 继续录制”之间切换；单独的“结束录制”按钮用于结束本次会话并生成脚本。暂停后之前采集的事件保留，继续时不会清空；暂停期间的时间不会变成脚本中的 Delay。录制中的编辑器只读，避免预览代码时误改采集结果。

默认勾选 **只记录点击坐标**：忽略鼠标移动轨迹，每次点击只保留按下时的准确坐标，生成 `MoveTo(x, y)`、需要的 `Delay(ms)`、再执行 `LeftClick/RightClick/MiddleClick`。这适合不需要鼠标轨迹、但要求点击位置稳定的自动化。取消勾选后才会录制鼠标移动轨迹。滚轮仍会记录发生位置。

录制只观察输入，不会拦截目标应用操作。按键精灵自身界面的操作不会写入脚本。键盘输入以物理按键为主，不能保证将输入法预编辑过程还原成 Unicode 文本。

## 2.1 独立悬浮窗、主窗口与托盘

应用启动后默认显示独立悬浮窗，主窗口隐藏，任务栏不显示主窗口按钮，系统托盘保留应用图标。悬浮窗和主窗口是两个独立窗口，可以同时显示；打开完整界面不会关闭或替换悬浮窗。

悬浮窗提供开始/暂停/继续录制、结束录制、播放/暂停/停止、当前脚本名称和实时鼠标坐标。它还可直接设置循环次数和轮间隔：循环次数默认 1，设为 0 表示无限循环；间隔设置与主窗口同步并保存在本地。默认勾选“只录点击”，置顶状态也会持久化。拖动悬浮窗顶部区域即可移动它。

点击悬浮窗的“完整界面”打开主窗口；主窗口中的“返回悬浮窗”按钮会隐藏主窗口，但不会关闭悬浮窗。点击主窗口右上角关闭按钮也只是隐藏主窗口，主窗口的任务栏按钮随之消失，脚本与悬浮窗继续运行，不会退出程序。

右键系统托盘图标可选择“打开主窗口”“显示悬浮窗”“隐藏悬浮窗”或“退出按键精灵 2026”；双击托盘图标打开主窗口。悬浮窗上的“×”只隐藏悬浮窗。只有通过托盘菜单选择“退出按键精灵 2026”才会真正退出程序。

## 3. 全局快捷键

应用运行期间，无论当前是否切回本软件，都可以使用：

- **F10**：启动当前选中的脚本。
- **F11**：暂停 / 继续当前脚本。
- **F12**：立即中止当前脚本，并释放本程序仍按住的键盘按键和鼠标按钮。

这组按键沿用经典按键精灵的 F10 启动、F12 中止习惯；F11 作为本软件默认的暂停/继续键。当前预览版使用固定默认值，暂不提供热键自定义。仅在对应操作可执行时截获：空闲时 F10 可启动脚本；脚本运行或暂停时 F11/F12 分别控制暂停/继续和中止；其他状态下这些键照常传递给前台应用，例如空闲时浏览器的 F12 仍可打开开发者工具。

## 4. 播放与重复

- 重复次数默认为 1。
- 设置为 100：脚本完整执行 100 次。
- 设置为 0：无限循环，必须手动停止。
- 每轮间隔默认为 2000ms。
- 间隔只在一轮完成且下一轮即将开始时发生；最后一轮结束后不会额外等待。
- 每轮间隔是整个脚本循环之间的等待，与代码内部的 Delay() 完全独立。

例如脚本内部有 Delay(500)，重复次数设为 3、每轮间隔设为 2000ms，则脚本每轮内部等待 500ms，两轮之间额外等待 2000ms。

“暂停”在当前原子鼠标/键盘动作结束后生效；“停止”会终止独立脚本执行进程，并由主程序释放它仍持有的鼠标键盘按键。

## 5. Lua 命令

| 命令 | 示例 | 说明 |
|---|---|---|
| 移动鼠标 | MoveTo(122, 100) | 虚拟桌面物理坐标 |
| 左/右/中键点击 | LeftClick(50) | 默认按下 50ms 后释放 |
| 双击 | LeftDoubleClick() | 两次左键点击，默认间隔 100ms |
| 按下/释放 | LeftDown() / LeftUp() | 长按控制 |
| 滚轮 | MouseWheel(120) | 正数向上 |
| 单键点击 | KeyPress("ENTER") | 默认按下 50ms |
| 键按下/释放 | KeyDown("CTRL") | 显式控制 |
| 组合键 | Hotkey("CTRL", "S") | 按顺序按下、逆序释放 |
| 文本输入 | TypeText("Hello") | Unicode 输入 |
| 延时 | Delay(500) | 可暂停/停止 |
| 日志 | Log("完成") | 写入运行日志 |

标准 Lua 5.4 的循环、变量、条件和函数均可使用。

## 6. Lua 调试器（main 开发版，未包含在 preview.4）

当前 main 开发版已接入基础 Lua 调试器；已发布的 `v0.1.0-preview.4` 安装包仍不含这一功能。后续如需体验，请使用通过 Windows CI 构建的 main 开发版，不要从 preview.4 的界面推断功能缺失。

在打开的脚本编辑器行号左侧点击红色圆点可添加/移除断点。点击“启动调试”后，脚本会在第一个 Lua 执行行暂停；命中断点时也会在该行执行前暂停。暂停时可使用：

- **继续**：恢复脚本执行，直到下一个断点或结束。
- **单步进入**：执行到下一个 Lua 行；遇到 Lua 函数调用会进入函数。
- **单步越过**：执行当前行并越过嵌套 Lua 函数，在调用者下一行暂停。
- **单步跳出**：从当前函数继续到调用者下一行。
- **运行到光标**：继续执行到当前脚本中光标所在行。
- **停止**：终止当前 worker，执行输入释放并清理调试状态。

调试面板左侧展示调用栈；选择不同栈帧会切换对应的局部变量。变量表同时显示可读全局标量。字符串最多展示 512 个字符，局部变量/全局变量/栈帧数量都有保护上限。表、函数、线程和 userdata 目前只显示类型与不可展开说明，不会执行任意 Lua 表达式来求值。

目前断点只在当前打开的脚本标签内存中保存，关闭标签或退出程序后不会保留；尚无命名监视表达式、表展开和复杂对象属性求值。调试固定为单轮运行，不使用普通播放的重复次数/轮间隔设置。


## 7. 可视化录制动作列表（main 开发版，未包含在 preview.4）

本节介绍当前 main 开发版新增功能；已发布的 v0.1.0-preview.4 安装包不包含动作列表 UI。

结束录制后，原始 Hook 事件会保存在脚本旁边的“脚本名.actions.json”文件中，例如 task.lua 对应 task.actions.json。该文件保存事件来源、原始事件引用、时间戳、坐标空间、click-only 标志、动作列表和生成区同步状态。若事件队列达到上限，文件会保存丢失数量并在动作列表状态栏提示。

在主窗口工具栏点击“动作列表”打开动作编辑器。可以插入、删除、复制动作、上移/下移，启用/禁用动作，编辑动作类型、坐标、按键/鼠标键、滚轮值、按住时长、动作间隔、文本、分组和注释；“批量延时”可一次修改所选动作。动作 ID、来源和原始事件 ID 作为只读列显示，方便追溯。

动作间隔与按住时长是两回事：DelayMs 是上一动作结束到当前动作开始之间的间隔；HoldMs 是鼠标/键盘按下到释放的时长。拖动/长按或重叠按键不会被强行折叠成点击；不能确定配对的 Down/Up 会保留原顺序。

“生成/同步 Lua”会创建或更新带 ID/schema 的 generated block；动作列表中的“定位代码”可导航到对应动作标记。生成区块之外的 Lua 用户代码不会被覆盖。手工编辑生成区块后，状态会变为“代码已手改/脱离同步”；删除区块会显示“生成区块缺失”。此时重生成会先显示旧区块与新代码差异预览，只有明确确认才替换/补回；取消不会修改脚本。当前采用动作到 Lua 的单向生成，不会把任意手写 Lua 反向解析成动作。

“步骤试运行”会通过现有脚本 Worker 真实执行所选动作，因此会向当前桌面发送键鼠输入。每次执行前都有风险确认，单步延时/按住时间合计限制为 60 秒；输入清理沿用现有播放停止路径。请先确认前台窗口和光标位置。

脚本重命名、复制、删除时会同步管理对应 sidecar；保存 sidecar 会保留 .bak 备份。遇到不支持的 schema 时，编辑入口会禁用且不会自动覆盖原文件。

## 8. 窗口控件自动化（main 开发版，未包含在 preview.4）

本节 API 仅在 main 开发版可用；已发布的 v0.1.0-preview.4 包未包含 M7。

基本用法是先通过 M6 查找窗口，再绑定控件自动化会话：

```lua
local w = WindowFind("目标窗口", "", "notepad", 0, true)
if not w then error("窗口未找到") end

local binding = WindowBind(w.id)
local caps = WindowGetCapabilities(binding)
local controls = ControlEnumerate(binding, 256)
for _, c in ipairs(controls) do
    Log(c.name .. " | " .. c.controlType .. " | " .. table.concat(c.supportedPatterns, ","))
end
WindowUnbind(binding)
```

`WindowBind`、`controlId` 与其缓存只在本轮脚本运行有效；窗口身份由主进程通过 M6 引用复核。不要把 binding/control ID 保存到脚本文件用于下次运行，也不要尝试传递 HWND。

只有控件真正暴露对应 UI Automation Pattern 时，才可读取/设置值、读取/选择项目、切换勾选状态或调用 Invoke。用 `supportedPatterns` 先检查声明支持的模式。不支持的控件会报错；密码字段的 Value/Text 读取会被拒绝。控件更新、关闭或重建后重新执行 ControlEnumerate，不复用旧 ID。

UI Automation 的后台 `ValuePattern.SetValue`、`SelectionItem.Select`、`Toggle`、`Invoke` 是控件语义操作，不等于向后台应用发送键盘或鼠标。M7 当前没有通用后台鼠标/键盘/文本注入。需要可见输入时使用 `WindowInput(binding, "foreground", ...)`；目标窗口必须可见、未最小化、在前台且键盘/文本操作有焦点，鼠标坐标必须位于客户区。它不会自动抢占焦点或静默回退。所有 API 都受 UIA provider、UIPI/UAC 和交互桌面权限限制。

## 9. 注意事项

- 首版输入坐标为全屏虚拟桌面绝对坐标；副屏位于主屏左侧或上方时可能出现负坐标。
- SendInput 受 UIPI 影响，普通权限程序不能保证向管理员权限程序注入输入。
- 无限循环可能持续重复点击、输入或提交操作；使用前确认脚本和目标窗口。
- 停止时执行输入释放，但如果进程崩溃、系统拒绝注入或目标应用丢失输入，仍应自行确认目标状态。
- 当前 preview.4 尚不含图像识别、OCR、完整窗口相对坐标、插件或经过验证的后台输入。它们已进入完整产品正式目标；当前缺失项以 FEATURE-MATRIX-CN.md 为准。

## 9. 截图、图色与模板找图（main 开发版，未包含在 preview.4）

M8 的核心 Lua API、图像资源管理 UI 与视觉调试覆盖层已接入 main，但尚未进入已发布的 v0.1.0-preview.4 Release。使用前需从 main 的 Windows CI 临时构建包体验；资源 UI/覆盖层尚未经过真实 Windows 10/11 桌面验收。

```lua
-- 捕获当前虚拟桌面的可见合成画面
local screen = CaptureScreen()
local size = GetImageSize(screen)
Log("screen=" .. size.width .. "x" .. size.height)

-- 获取一个像素 RGB（颜色为 0xRRGGBB）
local rgb = GetPixelColor(screen, 10, 20)
if CompareColor(screen, 10, 20, 0xFF0000, 8) then
    Log("这个像素接近红色")
end

-- 单点找色：未命中返回 nil；截图结果同时带局部 x/y 与 screenX/screenY
local red = FindColor(screen, 0xFF0000, 8)
if red then
    Log("red at " .. red.screenX .. "," .. red.screenY)
end

-- 多点颜色模式使用 dx,dy,RRGGBB 分号列表
local marker = FindMultiColor(screen, 0x112233, "1,0,FF0000;0,1,00FF00", 4)

-- 加载模板；PNG 透明像素可用作模板通配区
local template = LoadImage("C:\\scripts\\button.png")
local hit = FindImage(screen, template, 0.95, 4, 0, 0, 0, 0, 1, false, 1)
if hit then
    Log("template at " .. hit.screenX .. "," .. hit.screenY ..
        " similarity=" .. hit.similarity)
end

-- 等待目标稳定出现：默认超时 5000ms、间隔 100ms；stableCount=2 表示连续两次命中
local ready = WaitImage(template, 0.95, 4, 5000, 100, false, 1, 2)
if ready then
    Log("目标连续两次检测命中")
end

-- RGB/hex 转换与颜色距离
local hex = RgbToHex(255, 128, 0) -- #FF8000
local rgb = HexToRgb("0xFF8000")
local difference = ColorDifference(0xFF0000, 0xF00010)
Log(hex .. " R差=" .. difference.red .. " 最大通道差=" .. difference.maxChannel)

-- 八邻域颜色块识别；结果按采样点数从大到小
local blocks = FindColorBlocks(screen, 0xFF0000, 8, 4, 0, 0, 800, 600, 2, 20)
for _, block in ipairs(blocks) do
    Log("block=" .. block.x .. "," .. block.y .. " count=" .. block.pixelCount)
end

ReleaseImage(template)
ReleaseImage(screen)
```

### 使用限制
- 所有图像 ID 仅在当前脚本运行轮次有效，不要写入文件或跨轮次重用。脚本结束/异常/停止后资源池会清空；长脚本应主动调用 ReleaseImage。
- 单图最多 32,000,000 像素；每轮最多 128 个图像资源、256 MiB 缓存；模板单次搜索最多 50,000,000 次像素比较。
- 找色颜色采用 0xRRGGBB；容差是 R/G/B 各通道独立允许的最大绝对差，并非 RGB 总距离。搜索区域使用图像局部零基像素，截图命中额外返回屏幕绝对坐标。
- 模板找图默认 similarity 0.95、grayscale=false、scale=1；支持 0.25–4 倍最近邻缩放。Wait 系列支持 timeout、interval 和 stableCount 连续确认。
- 图像资源 UI 从主窗口工具栏“图像资源”打开：导入 PNG 后可列表、预览、重命名、校验 schema/路径/尺寸/文件大小/SHA-256，并复制相对路径到剪贴板。清单写入脚本旁的 .images.json，图片放在 .assets/；在脚本中使用复制的路径，例如 LoadImage("task.assets/<id>.png")，相对路径以脚本目录为基准。静态字面 LoadImage 依赖仍在时不能移除资源；动态路径仅提示无法静态分析。脚本重命名/复制会同步改写资源目录前缀和清单；删除脚本会删除对应清单与资产目录。移除清单条目不会删除原 PNG。
- 主窗口工具栏“视觉调试：开/关”控制屏幕覆盖层。它显示截图区域、颜色点/颜色块和找图命中框；标签可含相似度、RGB 与耗时。覆盖层是点击穿透窗口；Windows Display Affinity 排除截图为系统能力，必须在目标 Windows 环境实测。已关闭覆盖层不处理输入。
- CaptureWindow/CaptureClient 读取当前桌面可见合成像素，不保证覆盖窗口后台内容，也不承诺硬件加速或受保护表面可截图。未实现 OCR；所有 M8 Windows 桌面检查仍需按验收表实测。

## 10. OCR 文字识别（main 开发版，未包含在 preview.4）

首次使用 OCR 时，先打开主窗口工具栏的 **“OCR 模型”**。选择简体中文、英语或日语，点击“下载/补齐模型”。下载器会检查上游模型文件的 SHA-256，并把模型缓存到：

`%LOCALAPPDATA%\\Anjian2026\\Ocr\\Models`

只有你明确点击下载时程序才联网；脚本运行时默认严格离线。缺少模型会报告错误并提示打开模型管理器，不会自动下载。

### 识别一张已有图像

```lua
local screen = CaptureScreen()
local result = OCR(screen, "ch", 0.55)

Log(result.text)
Log("识别行数：" .. result.lineCount)

for _, line in ipairs(result.lines) do
    Log(line.text .. " confidence=" .. line.confidence)
    Log("局部区域：" .. line.x .. "," .. line.y .. " " ..
        line.width .. "x" .. line.height)
    if line.screenX then
        Log("屏幕坐标：" .. line.screenX .. "," .. line.screenY)
    end
end

ReleaseImage(screen)
```

`OCR` 返回全文、逐行文本、置信度、矩形和多边形边界。局部 `x/y` 是图像左上角起点的零基像素。截图额外包含 `screenX/screenY`，表示虚拟桌面绝对物理像素；副屏位于左侧或上方时允许负坐标。磁盘图片只有局部坐标。

### 在图像中查找文字

```lua
local screen = CaptureScreen()
local hit = FindText(screen, "登录", "ch", 0.70, "contains")

if hit then
    Log("命中：" .. hit.text)
    MoveTo(hit.screenX, hit.screenY)
else
    Log("没有找到登录文字")
end

ReleaseImage(screen)
```

`matchMode` 支持 `contains`（不区分大小写包含匹配）和 `exact`（不区分大小写整行完全匹配）。未命中返回 nil，低于 `minConfidence` 的行会被忽略。

### 等待屏幕文字出现

```lua
local hit = WaitText("订单提交成功", 15000, 250, "ch", 0.65, "contains")
if hit then
    Log("识别到：" .. hit.text)
else
    Log("等待超时")
end
```

`WaitText` 默认最多等待 10 秒、每 300 毫秒重新截图识别；timeout 范围 0–600000 毫秒，interval 范围 10–5000 毫秒。timeout=0 仍会立即检测一次。等待期间按 F11 暂停后会在检查点停住，F12 会终止独立 OCR Worker 并清理该轮图片资源。

### 当前限制
- 首轮支持语言代码 `ch`、`en`、`japan`；`zh-cn`/ `zh` 和 `ja` 也会在桥接层归一化。
- 单张 OCR 图像最多 16,000,000 像素；PNG IPC 最多 64 MiB。超大截图先 CropImage 缩小区域。
- OCR 读取当前桌面可见合成画面；被遮挡窗口的后台像素不保证可见，不绕过 UIPI/UAC/安全桌面。
- PaddleOcrNet 适配器只在独立 Lua Worker 中运行，主 UI 不执行 OCR 推理。桌面识别精度、速度、语言模型体积和长时间内存表现需要后续 Windows 10/11 集中实机验收。


## 11. 脚本参数与自定义运行表单（M10，main 开发版）

在主窗口选择脚本后，点击 **“参数定义”**。参数定义保存在 Lua 文件旁边，例如 `hello.lua` 对应 `hello.params.json`。读取参数定义不需要、也不会执行脚本。保存定义后点击“播放”或“启动调试”，程序会先显示运行参数表单；用户确认并通过验证后才会启动 Worker。

### 参数定义示例

以下是 `hello.params.json` 的内容示例（保存时参数设计器会自动生成相同 schema）：

```json
{
  "SchemaVersion": 1,
  "ScriptId": "hello",
  "Parameters": [
    {
      "Id": "count",
      "Name": "count",
      "Label": "运行次数",
      "Type": "integer",
      "Required": true,
      "DefaultValue": 3,
      "Minimum": 1,
      "Maximum": 20,
      "Options": [],
      "Description": "本次任务的循环次数",
      "Order": 0
    },
    {
      "Id": "enabled",
      "Name": "enabled",
      "Label": "启用操作",
      "Type": "boolean",
      "Required": false,
      "DefaultValue": true,
      "Options": [],
      "Description": "关闭后只执行脚本中的非操作逻辑",
      "Order": 1
    },
    {
      "Id": "title",
      "Name": "title",
      "Label": "窗口标题",
      "Type": "text",
      "Required": true,
      "DefaultValue": "记事本",
      "Pattern": ".+",
      "Options": [],
      "Description": "示例文本参数",
      "Order": 2
    }
  ]
}
```

字段说明：
- `Id`：定义内稳定 ID；`Name`：Lua 参数名，必须是唯一 Lua 标识符；`Label`：表单中文标签。
- `Type`：`text`、`integer`、`number`、`boolean`、`select`、`multiSelect`、`filePath`、`directoryPath`、`hotkey` 或 `color`。
- `Required`、`DefaultValue`、`Minimum`、`Maximum`、`Options`、`Pattern`、`MustExist`、`Sensitive`、`Description` 和 `Order` 控制校验、选项、路径存在性、敏感值遮蔽、帮助和排序。默认值必须是 JSON 原生类型，不要把数字/布尔值写成字符串。

### 在 Lua 脚本中读取

```lua
Log("运行次数：" .. tostring(Args.count))
Log("窗口标题：" .. Args.title)

if Args.enabled then
    Log("启用状态为 true")
end
```

`Args` 由 Worker 启动握手注入，字符串不会被拼接成 Lua 源码；常规的顶层或嵌套赋值会报只读错误。它用于防止意外修改，不是对抗恶意脚本的安全沙箱。参数校验失败或用户取消表单时，不会启动 Worker。

### 配置保存与安全
- 用户填写值与 `.params.json` 定义分离，保存到 `%LOCALAPPDATA%\\Anjian2026\\ScriptParameters\\` 下按脚本路径散列命名的 JSON 配置。
- 配置支持原子写入、上一份有效备份、损坏恢复、恢复默认、JSON 导入/导出。未知的未来配置版本会被拒绝，不会自动回退并覆盖原文件。
- 标记为 `Sensitive` 的输入在表单中遮蔽，普通日志不记录参数值；导出时会询问是否包含敏感字段。当前本地配置文件**不加密**，不要把高敏感凭据保存在其中。
- 当前功能已通过 Windows CI 自动化验证；参数表单的 DPI、文件/目录选择、真实播放/调试交互仍待 M14 后统一 Windows 10/11 桌面验收。
