# 按键精灵 2026：Lua 函数与参数参考手册

- 目标读者：脚本编写者与自动化工程维护者
- 对应运行时：Lua 5.4
- 当前 API 注册表版本：0.8（main 开发版，尚未进入已发布的 preview.4）
- 当前产品版本：v0.1.0-preview.4
- 文档状态：当前公开 API 第 2 节由机器可读注册表生成；其他目标设计章节与当前 API 明确分开。
- 当前坐标规则：已实现函数仍使用虚拟桌面绝对物理像素坐标
- 重要声明：本手册第 2 节由机器可读注册表自动生成；第 4 节的计划函数仍是目标设计，不能被当成已实现函数调用。
- 唯一来源：`src/Anjian.Core/Api/lua-api-registry.json`；生成/检查命令见 README。

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

<!-- BEGIN GENERATED API REFERENCE -->

### 2.1 鼠标

#### `MoveTo(x, y)` — 移动鼠标

- **稳定 ID：** `anjian.mouse.move-to`
- **状态：** 当前已实现；自动化 API 契约测试覆盖，Windows 桌面实机验收待完成。
- **说明：** 移动到虚拟桌面绝对物理像素坐标；副屏可使用负坐标。

| 参数 | Lua 类型 | 必填 | 默认值 | 范围/单位/选项 | 说明 |
|---|---|---|---|---|---|
| `x` | `integer` | 是 | — | -2147483648–2147483647 物理像素 | 虚拟桌面物理像素 X 坐标；允许负值。 |
| `y` | `integer` | 是 | — | -2147483648–2147483647 物理像素 | 虚拟桌面物理像素 Y 坐标；允许负值。 |

**返回值：** `nil` — 无显式返回值；成功后继续执行后续 Lua 语句。
**错误：** 参数类型、整数性或范围不合法时产生 Lua 错误。；输入调用失败、权限不足或目标程序拒绝输入时会报告执行错误。
**执行/取消语义：** 在执行器命令检查点遵循暂停状态。 停止会终止 worker；主进程清理本程序持有的键鼠状态。
**坐标/权限：** 坐标空间：screen（虚拟桌面绝对物理像素）。使用 SetCursorPos；坐标为物理像素，允许负值。权限和桌面会话受 Windows 约束。

**示例：**
```lua
MoveTo(-120, 450)
```

#### `LeftClick(holdMs?)` — 左键单击

- **稳定 ID：** `anjian.mouse.left-click`
- **状态：** 当前已实现；自动化 API 契约测试覆盖，Windows 桌面实机验收待完成。
- **说明：** 左键单击并在指定时间后释放；省略时长默认 50 毫秒。

| 参数 | Lua 类型 | 必填 | 默认值 | 范围/单位/选项 | 说明 |
|---|---|---|---|---|---|
| `holdMs` | `integer` | 否 | `50` | 0–600000 毫秒 | 按钮按下持续时间；0 表示不增加人为等待。 |

**返回值：** `nil` — 无显式返回值；成功后继续执行后续 Lua 语句。
**错误：** 参数类型、整数性或范围不合法时产生 Lua 错误。；输入调用失败、权限不足或目标程序拒绝输入时会报告执行错误。
**执行/取消语义：** 在执行器命令检查点遵循暂停状态。 点击等待可被停止取消；正常结束、错误或取消时尝试释放，worker 退出时主进程再清理。
**坐标/权限：** 坐标空间：none。使用 Windows SendInput；目标程序可能因权限、桌面或自身输入处理而拒绝。

**示例：**
```lua
LeftClick()
```

#### `RightClick(holdMs?)` — 右键单击

- **稳定 ID：** `anjian.mouse.right-click`
- **状态：** 当前已实现；自动化 API 契约测试覆盖，Windows 桌面实机验收待完成。
- **说明：** 右键单击并在指定时间后释放；省略时长默认 50 毫秒。

| 参数 | Lua 类型 | 必填 | 默认值 | 范围/单位/选项 | 说明 |
|---|---|---|---|---|---|
| `holdMs` | `integer` | 否 | `50` | 0–600000 毫秒 | 按钮按下持续时间；0 表示不增加人为等待。 |

**返回值：** `nil` — 无显式返回值；成功后继续执行后续 Lua 语句。
**错误：** 参数类型、整数性或范围不合法时产生 Lua 错误。；输入调用失败、权限不足或目标程序拒绝输入时会报告执行错误。
**执行/取消语义：** 在执行器命令检查点遵循暂停状态。 点击等待可被停止取消；正常结束、错误或取消时尝试释放，worker 退出时主进程再清理。
**坐标/权限：** 坐标空间：none。使用 Windows SendInput；目标程序可能因权限、桌面或自身输入处理而拒绝。

**示例：**
```lua
RightClick()
```

#### `MiddleClick(holdMs?)` — 中键单击

- **稳定 ID：** `anjian.mouse.middle-click`
- **状态：** 当前已实现；自动化 API 契约测试覆盖，Windows 桌面实机验收待完成。
- **说明：** 中键单击并在指定时间后释放；省略时长默认 50 毫秒。

| 参数 | Lua 类型 | 必填 | 默认值 | 范围/单位/选项 | 说明 |
|---|---|---|---|---|---|
| `holdMs` | `integer` | 否 | `50` | 0–600000 毫秒 | 按钮按下持续时间；0 表示不增加人为等待。 |

**返回值：** `nil` — 无显式返回值；成功后继续执行后续 Lua 语句。
**错误：** 参数类型、整数性或范围不合法时产生 Lua 错误。；输入调用失败、权限不足或目标程序拒绝输入时会报告执行错误。
**执行/取消语义：** 在执行器命令检查点遵循暂停状态。 点击等待可被停止取消；正常结束、错误或取消时尝试释放，worker 退出时主进程再清理。
**坐标/权限：** 坐标空间：none。使用 Windows SendInput；目标程序可能因权限、桌面或自身输入处理而拒绝。

**示例：**
```lua
MiddleClick()
```

#### `LeftDoubleClick(holdMs?, gapMs?)` — 左键双击

- **稳定 ID：** `anjian.mouse.left-double-click`
- **状态：** 当前已实现；自动化 API 契约测试覆盖，Windows 桌面实机验收待完成。
- **说明：** 左键双击；可指定单次按下持续时间和两次点击间隔。

| 参数 | Lua 类型 | 必填 | 默认值 | 范围/单位/选项 | 说明 |
|---|---|---|---|---|---|
| `holdMs` | `integer` | 否 | `50` | 0–600000 毫秒 | 每次单击的按下持续时间。 |
| `gapMs` | `integer` | 否 | `100` | 0–600000 毫秒 | 两次单击之间的延时。 |

**返回值：** `nil` — 无显式返回值；成功后继续执行后续 Lua 语句。
**错误：** 参数类型、整数性或范围不合法时产生 Lua 错误。；输入调用失败、权限不足或目标程序拒绝输入时会报告执行错误。
**执行/取消语义：** 在执行器命令检查点遵循暂停状态。 停止会终止 worker 并清理仍按下的鼠标按钮。
**坐标/权限：** 坐标空间：none。双击识别受 Windows 双击速度和目标程序行为影响。

**示例：**
```lua
LeftDoubleClick(50, 100)
```

#### `RightDoubleClick(holdMs?, gapMs?)` — 右键双击

- **稳定 ID：** `anjian.mouse.right-double-click`
- **状态：** 当前已实现；自动化 API 契约测试覆盖，Windows 桌面实机验收待完成。
- **说明：** 右键双击；可指定单次按下持续时间和两次点击间隔。

| 参数 | Lua 类型 | 必填 | 默认值 | 范围/单位/选项 | 说明 |
|---|---|---|---|---|---|
| `holdMs` | `integer` | 否 | `50` | 0–600000 毫秒 | 每次单击的按下持续时间。 |
| `gapMs` | `integer` | 否 | `100` | 0–600000 毫秒 | 两次单击之间的延时。 |

**返回值：** `nil` — 无显式返回值；成功后继续执行后续 Lua 语句。
**错误：** 参数类型、整数性或范围不合法时产生 Lua 错误。；输入调用失败、权限不足或目标程序拒绝输入时会报告执行错误。
**执行/取消语义：** 在执行器命令检查点遵循暂停状态。 停止会终止 worker 并清理仍按下的鼠标按钮。
**坐标/权限：** 坐标空间：none。双击识别受 Windows 双击速度和目标程序行为影响。

**示例：**
```lua
RightDoubleClick(50, 100)
```

#### `MiddleDoubleClick(holdMs?, gapMs?)` — 中键双击

- **稳定 ID：** `anjian.mouse.middle-double-click`
- **状态：** 当前已实现；自动化 API 契约测试覆盖，Windows 桌面实机验收待完成。
- **说明：** 中键双击；可指定单次按下持续时间和两次点击间隔。

| 参数 | Lua 类型 | 必填 | 默认值 | 范围/单位/选项 | 说明 |
|---|---|---|---|---|---|
| `holdMs` | `integer` | 否 | `50` | 0–600000 毫秒 | 每次单击的按下持续时间。 |
| `gapMs` | `integer` | 否 | `100` | 0–600000 毫秒 | 两次单击之间的延时。 |

**返回值：** `nil` — 无显式返回值；成功后继续执行后续 Lua 语句。
**错误：** 参数类型、整数性或范围不合法时产生 Lua 错误。；输入调用失败、权限不足或目标程序拒绝输入时会报告执行错误。
**执行/取消语义：** 在执行器命令检查点遵循暂停状态。 停止会终止 worker 并清理仍按下的鼠标按钮。
**坐标/权限：** 坐标空间：none。双击识别受 Windows 双击速度和目标程序行为影响。

**示例：**
```lua
MiddleDoubleClick(50, 100)
```

#### `LeftDown()` — 左键按下

- **稳定 ID：** `anjian.mouse.left-down`
- **状态：** 当前已实现；自动化 API 契约测试覆盖，Windows 桌面实机验收待完成。
- **说明：** 左键按下并保持到对应 Up 或统一清理。

**参数：** 无。

**返回值：** `nil` — 无显式返回值；成功后继续执行后续 Lua 语句。
**错误：** 参数类型、整数性或范围不合法时产生 Lua 错误。；输入调用失败、权限不足或目标程序拒绝输入时会报告执行错误。
**执行/取消语义：** 命令检查点遵循暂停状态。 停止会终止 worker；主进程清理本程序持有的键鼠状态。
**坐标/权限：** 坐标空间：none。受 Windows 权限级别和目标程序能力约束；不会绕过 UIPI 或受保护桌面。

**示例：**
```lua
LeftDown()
```

#### `LeftUp()` — 左键释放

- **稳定 ID：** `anjian.mouse.left-up`
- **状态：** 当前已实现；自动化 API 契约测试覆盖，Windows 桌面实机验收待完成。
- **说明：** 左键释放并释放程序跟踪的该按钮。

**参数：** 无。

**返回值：** `nil` — 无显式返回值；成功后继续执行后续 Lua 语句。
**错误：** 参数类型、整数性或范围不合法时产生 Lua 错误。；输入调用失败、权限不足或目标程序拒绝输入时会报告执行错误。
**执行/取消语义：** 允许暂停期间释放按钮，避免遗留按下状态。 停止会终止 worker；主进程清理本程序持有的键鼠状态。
**坐标/权限：** 坐标空间：none。受 Windows 权限级别和目标程序能力约束；不会绕过 UIPI 或受保护桌面。

**示例：**
```lua
LeftUp()
```

#### `RightDown()` — 右键按下

- **稳定 ID：** `anjian.mouse.right-down`
- **状态：** 当前已实现；自动化 API 契约测试覆盖，Windows 桌面实机验收待完成。
- **说明：** 右键按下并保持到对应 Up 或统一清理。

**参数：** 无。

**返回值：** `nil` — 无显式返回值；成功后继续执行后续 Lua 语句。
**错误：** 参数类型、整数性或范围不合法时产生 Lua 错误。；输入调用失败、权限不足或目标程序拒绝输入时会报告执行错误。
**执行/取消语义：** 命令检查点遵循暂停状态。 停止会终止 worker；主进程清理本程序持有的键鼠状态。
**坐标/权限：** 坐标空间：none。受 Windows 权限级别和目标程序能力约束；不会绕过 UIPI 或受保护桌面。

**示例：**
```lua
RightDown()
```

#### `RightUp()` — 右键释放

- **稳定 ID：** `anjian.mouse.right-up`
- **状态：** 当前已实现；自动化 API 契约测试覆盖，Windows 桌面实机验收待完成。
- **说明：** 右键释放并释放程序跟踪的该按钮。

**参数：** 无。

**返回值：** `nil` — 无显式返回值；成功后继续执行后续 Lua 语句。
**错误：** 参数类型、整数性或范围不合法时产生 Lua 错误。；输入调用失败、权限不足或目标程序拒绝输入时会报告执行错误。
**执行/取消语义：** 允许暂停期间释放按钮，避免遗留按下状态。 停止会终止 worker；主进程清理本程序持有的键鼠状态。
**坐标/权限：** 坐标空间：none。受 Windows 权限级别和目标程序能力约束；不会绕过 UIPI 或受保护桌面。

**示例：**
```lua
RightUp()
```

#### `MiddleDown()` — 中键按下

- **稳定 ID：** `anjian.mouse.middle-down`
- **状态：** 当前已实现；自动化 API 契约测试覆盖，Windows 桌面实机验收待完成。
- **说明：** 中键按下并保持到对应 Up 或统一清理。

**参数：** 无。

**返回值：** `nil` — 无显式返回值；成功后继续执行后续 Lua 语句。
**错误：** 参数类型、整数性或范围不合法时产生 Lua 错误。；输入调用失败、权限不足或目标程序拒绝输入时会报告执行错误。
**执行/取消语义：** 命令检查点遵循暂停状态。 停止会终止 worker；主进程清理本程序持有的键鼠状态。
**坐标/权限：** 坐标空间：none。受 Windows 权限级别和目标程序能力约束；不会绕过 UIPI 或受保护桌面。

**示例：**
```lua
MiddleDown()
```

#### `MiddleUp()` — 中键释放

- **稳定 ID：** `anjian.mouse.middle-up`
- **状态：** 当前已实现；自动化 API 契约测试覆盖，Windows 桌面实机验收待完成。
- **说明：** 中键释放并释放程序跟踪的该按钮。

**参数：** 无。

**返回值：** `nil` — 无显式返回值；成功后继续执行后续 Lua 语句。
**错误：** 参数类型、整数性或范围不合法时产生 Lua 错误。；输入调用失败、权限不足或目标程序拒绝输入时会报告执行错误。
**执行/取消语义：** 允许暂停期间释放按钮，避免遗留按下状态。 停止会终止 worker；主进程清理本程序持有的键鼠状态。
**坐标/权限：** 坐标空间：none。受 Windows 权限级别和目标程序能力约束；不会绕过 UIPI 或受保护桌面。

**示例：**
```lua
MiddleUp()
```

#### `MouseWheel(delta)` — 滚动鼠标滚轮

- **稳定 ID：** `anjian.mouse.mouse-wheel`
- **状态：** 当前已实现；自动化 API 契约测试覆盖，Windows 桌面实机验收待完成。
- **说明：** 发送整数滚轮增量；正数向上、负数向下，120 通常是一个标准刻度。

| 参数 | Lua 类型 | 必填 | 默认值 | 范围/单位/选项 | 说明 |
|---|---|---|---|---|---|
| `delta` | `integer` | 是 | — | -2147483648–2147483647 滚轮增量 | 滚轮增量；正数向上、负数向下。 |

**返回值：** `nil` — 无显式返回值；成功后继续执行后续 Lua 语句。
**错误：** 参数类型、整数性或范围不合法时产生 Lua 错误。；输入调用失败、权限不足或目标程序拒绝输入时会报告执行错误。
**执行/取消语义：** 在执行器命令检查点遵循暂停状态。 停止会终止 worker；主进程清理本程序持有的键鼠状态。
**坐标/权限：** 坐标空间：none。使用 Windows SendInput；应用响应距离由目标程序决定。

**示例：**
```lua
MouseWheel(-120)
```

### 2.2 键盘

#### `KeyPress(key, holdMs?)` — 单键按下并释放

- **稳定 ID：** `anjian.keyboard.key-press`
- **状态：** 当前已实现；自动化 API 契约测试覆盖，Windows 桌面实机验收待完成。
- **说明：** 按下规范键名对应的键后释放，缺省持续时间为 50 毫秒。

| 参数 | Lua 类型 | 必填 | 默认值 | 范围/单位/选项 | 说明 |
|---|---|---|---|---|---|
| `key` | `keyName` | 是 | — | 固定值：ENTER、ESC、TAB、SPACE、BACKSPACE、DELETE、INSERT、HOME、END、PAGEUP、PAGEDOWN、UP、DOWN、LEFT、RIGHT、CTRL、CONTROL、SHIFT、ALT、WIN、CAPSLOCK、PRINTSCREEN、PAUSE、NUMLOCK、SCROLLLOCK、NUMPAD0、NUMPAD1、NUMPAD2、NUMPAD3、NUMPAD4、NUMPAD5、NUMPAD6、NUMPAD7、NUMPAD8、NUMPAD9、NUMPADMULTIPLY、NUMPADADD、NUMPADSUBTRACT、NUMPADDECIMAL、NUMPADDIVIDE、SEMICOLON、EQUALS、COMMA、MINUS、PERIOD、SLASH、BACKTICK、LEFTBRACKET、BACKSLASH、RIGHTBRACKET、QUOTE；规则：A-Z、0-9、F1-F24 | 按键名（不区分大小写）；具体映射由 Windows 输入层解析。 |
| `holdMs` | `integer` | 否 | `50` | 0–600000 毫秒 | 按键持续时间；0 表示不增加人为等待。 |

**返回值：** `nil` — 无显式返回值；成功后继续执行后续 Lua 语句。
**错误：** 参数类型、整数性或范围不合法时产生 Lua 错误。；输入调用失败、权限不足或目标程序拒绝输入时会报告执行错误。
**执行/取消语义：** 在执行器命令检查点遵循暂停状态。 按键等待可停止取消；正常结束、错误或取消时尝试释放，worker 退出时主进程清理。
**坐标/权限：** 坐标空间：none。键名由 Windows 输入映射解析，权限及键盘布局可能影响结果。

**示例：**
```lua
KeyPress("ENTER")
```

#### `KeyDown(key)` — 按下按键

- **稳定 ID：** `anjian.keyboard.key-down`
- **状态：** 当前已实现；自动化 API 契约测试覆盖，Windows 桌面实机验收待完成。
- **说明：** 按下按键；按下保持到 KeyUp 或统一清理。

| 参数 | Lua 类型 | 必填 | 默认值 | 范围/单位/选项 | 说明 |
|---|---|---|---|---|---|
| `key` | `keyName` | 是 | — | 固定值：ENTER、ESC、TAB、SPACE、BACKSPACE、DELETE、INSERT、HOME、END、PAGEUP、PAGEDOWN、UP、DOWN、LEFT、RIGHT、CTRL、CONTROL、SHIFT、ALT、WIN、CAPSLOCK、PRINTSCREEN、PAUSE、NUMLOCK、SCROLLLOCK、NUMPAD0、NUMPAD1、NUMPAD2、NUMPAD3、NUMPAD4、NUMPAD5、NUMPAD6、NUMPAD7、NUMPAD8、NUMPAD9、NUMPADMULTIPLY、NUMPADADD、NUMPADSUBTRACT、NUMPADDECIMAL、NUMPADDIVIDE、SEMICOLON、EQUALS、COMMA、MINUS、PERIOD、SLASH、BACKTICK、LEFTBRACKET、BACKSLASH、RIGHTBRACKET、QUOTE；规则：A-Z、0-9、F1-F24 | 按键名（不区分大小写）；具体映射由 Windows 输入层解析。 |

**返回值：** `nil` — 无显式返回值；成功后继续执行后续 Lua 语句。
**错误：** 参数类型、整数性或范围不合法时产生 Lua 错误。；输入调用失败、权限不足或目标程序拒绝输入时会报告执行错误。
**执行/取消语义：** 命令检查点遵循暂停状态。 停止会终止 worker；主进程清理本程序持有的键鼠状态。
**坐标/权限：** 坐标空间：none。受 Windows 权限级别和目标程序能力约束；不会绕过 UIPI 或受保护桌面。

**示例：**
```lua
KeyDown("CTRL")
```

#### `KeyUp(key)` — 释放按键

- **稳定 ID：** `anjian.keyboard.key-up`
- **状态：** 当前已实现；自动化 API 契约测试覆盖，Windows 桌面实机验收待完成。
- **说明：** 释放按键；暂停时仍允许释放，避免遗留按键。

| 参数 | Lua 类型 | 必填 | 默认值 | 范围/单位/选项 | 说明 |
|---|---|---|---|---|---|
| `key` | `keyName` | 是 | — | 固定值：ENTER、ESC、TAB、SPACE、BACKSPACE、DELETE、INSERT、HOME、END、PAGEUP、PAGEDOWN、UP、DOWN、LEFT、RIGHT、CTRL、CONTROL、SHIFT、ALT、WIN、CAPSLOCK、PRINTSCREEN、PAUSE、NUMLOCK、SCROLLLOCK、NUMPAD0、NUMPAD1、NUMPAD2、NUMPAD3、NUMPAD4、NUMPAD5、NUMPAD6、NUMPAD7、NUMPAD8、NUMPAD9、NUMPADMULTIPLY、NUMPADADD、NUMPADSUBTRACT、NUMPADDECIMAL、NUMPADDIVIDE、SEMICOLON、EQUALS、COMMA、MINUS、PERIOD、SLASH、BACKTICK、LEFTBRACKET、BACKSLASH、RIGHTBRACKET、QUOTE；规则：A-Z、0-9、F1-F24 | 按键名（不区分大小写）；具体映射由 Windows 输入层解析。 |

**返回值：** `nil` — 无显式返回值；成功后继续执行后续 Lua 语句。
**错误：** 参数类型、整数性或范围不合法时产生 Lua 错误。；输入调用失败、权限不足或目标程序拒绝输入时会报告执行错误。
**执行/取消语义：** 允许暂停期间释放按键。 停止会终止 worker；主进程清理本程序持有的键鼠状态。
**坐标/权限：** 坐标空间：none。受 Windows 权限级别和目标程序能力约束；不会绕过 UIPI 或受保护桌面。

**示例：**
```lua
KeyUp("CTRL")
```

#### `TypeText(text)` — 输入 Unicode 文本

- **稳定 ID：** `anjian.keyboard.type-text`
- **状态：** 当前已实现；自动化 API 契约测试覆盖，Windows 桌面实机验收待完成。
- **说明：** 向当前输入目标注入 Unicode 文本，不保证重建真实物理键盘或输入法组合过程。

| 参数 | Lua 类型 | 必填 | 默认值 | 范围/单位/选项 | 说明 |
|---|---|---|---|---|---|
| `text` | `string` | 是 | — | — | 待输入的 Unicode 文本；允许空字符串，不允许 nil。 |

**返回值：** `nil` — 无显式返回值；成功后继续执行后续 Lua 语句。
**错误：** 参数类型、整数性或范围不合法时产生 Lua 错误。；输入调用失败、权限不足或目标程序拒绝输入时会报告执行错误。
**执行/取消语义：** 在执行器命令检查点遵循暂停状态。 文本按 rune 分段注入并检查停止令牌；worker 退出时主进程清理输入状态。
**坐标/权限：** 坐标空间：none。输入法、受保护控件、权限级别和目标程序行为可能影响结果。

**示例：**
```lua
TypeText("按键精灵 2026")
```

#### `Hotkey(key1, ..., keyN)` — 组合键

- **稳定 ID：** `anjian.keyboard.hotkey`
- **状态：** 当前已实现；自动化 API 契约测试覆盖，Windows 桌面实机验收待完成。
- **说明：** 按参数顺序按下一个或多个规范键名，再按逆序释放；出错时尝试释放已按下的键。

| 参数 | Lua 类型 | 必填 | 默认值 | 范围/单位/选项 | 说明 |
|---|---|---|---|---|---|
| `keys` | `keyName[]` | 是 | — | 固定值：ENTER、ESC、TAB、SPACE、BACKSPACE、DELETE、INSERT、HOME、END、PAGEUP、PAGEDOWN、UP、DOWN、LEFT、RIGHT、CTRL、CONTROL、SHIFT、ALT、WIN、CAPSLOCK、PRINTSCREEN、PAUSE、NUMLOCK、SCROLLLOCK、NUMPAD0、NUMPAD1、NUMPAD2、NUMPAD3、NUMPAD4、NUMPAD5、NUMPAD6、NUMPAD7、NUMPAD8、NUMPAD9、NUMPADMULTIPLY、NUMPADADD、NUMPADSUBTRACT、NUMPADDECIMAL、NUMPADDIVIDE、SEMICOLON、EQUALS、COMMA、MINUS、PERIOD、SLASH、BACKTICK、LEFTBRACKET、BACKSLASH、RIGHTBRACKET、QUOTE；规则：A-Z、0-9、F1-F24 | 一个或多个规范按键名，按输入顺序按下、逆序释放。 |

**返回值：** `nil` — 无显式返回值；成功后继续执行后续 Lua 语句。
**错误：** 参数类型、整数性或范围不合法时产生 Lua 错误。；输入调用失败、权限不足或目标程序拒绝输入时会报告执行错误。
**执行/取消语义：** 在执行器命令检查点遵循暂停状态。 执行错误时逆序释放已记录按键；Stop 时主进程最终清理。
**坐标/权限：** 坐标空间：none。受 Windows 权限级别和目标程序能力约束；不会绕过 UIPI 或受保护桌面。

**示例：**
```lua
Hotkey("CTRL", "SHIFT", "S")
```

### 2.3 运行时与日志

#### `Delay(milliseconds)` — 可取消延时

- **稳定 ID：** `anjian.runtime.delay`
- **状态：** 当前已实现；自动化 API 契约测试覆盖，Windows 桌面实机验收待完成。
- **说明：** 等待指定的非负整数毫秒，与脚本轮间隔相互独立。

| 参数 | Lua 类型 | 必填 | 默认值 | 范围/单位/选项 | 说明 |
|---|---|---|---|---|---|
| `milliseconds` | `integer` | 是 | — | 0–600000 毫秒 | 延时长度。 |

**返回值：** `nil` — 无显式返回值；成功后继续执行后续 Lua 语句。
**错误：** 参数类型、整数性或范围不合法时产生 Lua 错误。；输入调用失败、权限不足或目标程序拒绝输入时会报告执行错误。
**执行/取消语义：** 在执行器命令检查点遵循暂停状态。 等待可被 Stop 取消；worker 退出时主进程清理键鼠状态。
**坐标/权限：** 坐标空间：none。不需要提升权限；实际时间受系统调度和暂停时长影响。

**示例：**
```lua
Delay(500)
```

#### `RandomDelay(minMs, maxMs)` — 随机延时

- **稳定 ID：** `anjian.runtime.random-delay`
- **状态：** 当前已实现；自动化 API 契约测试覆盖，Windows 桌面实机验收待完成。
- **说明：** 在包含两端的整数毫秒区间内随机选择时长；最小值不得大于最大值。

| 参数 | Lua 类型 | 必填 | 默认值 | 范围/单位/选项 | 说明 |
|---|---|---|---|---|---|
| `minMs` | `integer` | 是 | — | 0–600000 毫秒 | 区间最小值。 |
| `maxMs` | `integer` | 是 | — | 0–600000 毫秒 | 区间最大值；不得小于最小值。 |

**返回值：** `nil` — 无显式返回值；成功后继续执行后续 Lua 语句。
**错误：** 参数类型、整数性或范围不合法时产生 Lua 错误。；输入调用失败、权限不足或目标程序拒绝输入时会报告执行错误。
**执行/取消语义：** 在执行器命令检查点遵循暂停状态。 等待阶段可被 Stop 取消。
**坐标/权限：** 坐标空间：none。结果为闭区间内整数毫秒。

**示例：**
```lua
RandomDelay(100, 300)
```

#### `Log(message)` — 写入运行日志

- **稳定 ID：** `anjian.runtime.log`
- **状态：** 当前已实现；自动化 API 契约测试覆盖，Windows 桌面实机验收待完成。
- **说明：** 使用 Lua tostring 将参数转为文本并发送到主程序运行日志。

| 参数 | Lua 类型 | 必填 | 默认值 | 范围/单位/选项 | 说明 |
|---|---|---|---|---|---|
| `message` | `any` | 是 | — | — | 任意 Lua 值；运行时使用 tostring 转换为文本。 |

**返回值：** `nil` — 无显式返回值；成功后继续执行后续 Lua 语句。
**错误：** 参数类型、整数性或范围不合法时产生 Lua 错误。；输入调用失败、权限不足或目标程序拒绝输入时会报告执行错误。
**执行/取消语义：** 在执行器命令检查点遵循暂停状态。 停止会终止 worker；主进程清理本程序持有的键鼠状态。
**坐标/权限：** 坐标空间：none。不需要额外 Windows 权限；消息不会自动写入持久化配置。

**示例：**
```lua
Log("本轮完成")
```

### 2.4 窗口与坐标

#### `WindowFind(titleContains?, className?, processName?, processId?, visibleOnly?)` — 查找窗口

- **稳定 ID：** `anjian.window.window-find`
- **状态：** 当前已实现；自动化 API 契约测试覆盖，Windows 桌面实机验收待完成。
- **说明：** 按标题包含、窗口类名精确值、进程名、PID 和可见状态筛选顶层窗口，返回第一个匹配的安全窗口引用；未命中返回 nil。

| 参数 | Lua 类型 | 必填 | 默认值 | 范围/单位/选项 | 说明 |
|---|---|---|---|---|---|
| `titleContains` | `string` | 否 | `` | — | 标题包含匹配；空字符串表示不筛选。 |
| `className` | `string` | 否 | `` | — | 窗口类名精确匹配；空字符串表示不筛选。 |
| `processName` | `string` | 否 | `` | — | 进程名匹配，可省略 .exe；空字符串表示不筛选。 |
| `processId` | `integer` | 否 | `0` | 0–2147483647 | Windows 进程 ID；0 表示不筛选。 |
| `visibleOnly` | `boolean` | 否 | `true` | — | true 仅返回可见窗口；false 同时包括隐藏窗口。 |

**返回值：** `table\|nil` — 窗口信息表；未在超时内找到时返回 nil。id 是本次脚本运行内有效的不透明字符串引用。
**错误：** 筛选参数、ID、超时或坐标不合法时产生 Lua 错误。；无法访问或窗口在调用期间销毁时返回可定位错误；不会重用失效引用。
**执行/取消语义：** 在执行器命令边界遵循暂停状态；等待操作期间响应暂停/停止。 等待操作响应统一停止令牌；停止会终止 worker，窗口等待取消后不保留后台任务。
**坐标/权限：** 坐标空间：none。仅枚举当前交互桌面可见的 Win32 顶层窗口；不绕过 UIPI/UAC/安全桌面。

**示例：**
```lua
local w = WindowFind("记事本", "", "", 0, true)\nif w then WindowActivate(w.id) end
```

#### `WindowEnumerate(titleContains?, className?, processName?, processId?, visibleOnly?)` — 枚举窗口

- **稳定 ID：** `anjian.window.window-enumerate`
- **状态：** 当前已实现；自动化 API 契约测试覆盖，Windows 桌面实机验收待完成。
- **说明：** 按筛选条件返回最多 512 个匹配的顶层窗口信息表，每项包含不透明 ID、标题、类名、进程、显示状态、前台/焦点标志和外框/客户区矩形。

| 参数 | Lua 类型 | 必填 | 默认值 | 范围/单位/选项 | 说明 |
|---|---|---|---|---|---|
| `titleContains` | `string` | 否 | `` | — | 标题包含匹配；空字符串表示不筛选。 |
| `className` | `string` | 否 | `` | — | 窗口类名精确匹配；空字符串表示不筛选。 |
| `processName` | `string` | 否 | `` | — | 进程名匹配，可省略 .exe；空字符串表示不筛选。 |
| `processId` | `integer` | 否 | `0` | 0–2147483647 | Windows 进程 ID；0 表示不筛选。 |
| `visibleOnly` | `boolean` | 否 | `true` | — | true 仅返回可见窗口；false 同时包括隐藏窗口。 |

**返回值：** `table` — 从 1 开始索引的窗口信息数组；无匹配时为空表。
**错误：** 筛选参数、ID、超时或坐标不合法时产生 Lua 错误。；无法访问或窗口在调用期间销毁时返回可定位错误；不会重用失效引用。
**执行/取消语义：** 在执行器命令边界遵循暂停状态；等待操作期间响应暂停/停止。 等待操作响应统一停止令牌；停止会终止 worker，窗口等待取消后不保留后台任务。
**坐标/权限：** 坐标空间：none。只查询顶层窗口元数据；不读取或操作控件内容。枚举上限 512。

**示例：**
```lua
local windows = WindowEnumerate("", "", "notepad", 0, true)\nfor i, w in ipairs(windows) do Log(w.title) end
```

#### `WindowEnumerateChildren(id)` — 枚举子窗口

- **稳定 ID：** `anjian.window.window-enumerate-children`
- **状态：** 当前已实现；自动化 API 契约测试覆盖，Windows 桌面实机验收待完成。
- **说明：** 枚举指定窗口下的全部子窗口后代（包括多层嵌套），最多 512 个；不提供控件读写或后台输入能力。

| 参数 | Lua 类型 | 必填 | 默认值 | 范围/单位/选项 | 说明 |
|---|---|---|---|---|---|
| `id` | `string` | 是 | — | — | 由 WindowFind/WindowEnumerate 返回的窗口引用 ID。 |

**返回值：** `table` — 子窗口信息数组；不包含父窗口本身。
**错误：** 窗口 ID 无效、过期或父窗口已关闭时产生 Lua 错误。；子窗口在枚举期间销毁时跳过该条目。
**执行/取消语义：** 在执行器命令边界遵循暂停状态；等待操作期间响应暂停/停止。 等待操作响应统一停止令牌；停止会终止 worker，窗口等待取消后不保留后台任务。
**坐标/权限：** 坐标空间：none。仅枚举 Win32 子窗口层级与基本属性；不操作控件值，不绕过 UIPI/UAC。

**示例：**
```lua
local w = WindowFind("记事本")\nif w then\n  local children = WindowEnumerateChildren(w.id)\nend
```

#### `WindowGetInfo(id)` — 读取窗口属性

- **稳定 ID：** `anjian.window.window-get-info`
- **状态：** 当前已实现；自动化 API 契约测试覆盖，Windows 桌面实机验收待完成。
- **说明：** 读取当前安全窗口引用的实时元数据、外框与客户区物理矩形、显示状态、前台和键盘焦点状态。

| 参数 | Lua 类型 | 必填 | 默认值 | 范围/单位/选项 | 说明 |
|---|---|---|---|---|---|
| `id` | `string` | 是 | — | — | 由窗口查找/枚举 API 返回的窗口引用 ID。 |

**返回值：** `table` — 包含 id/title/className/processId/processName/visible/minimized/maximized/foreground/focused/isChild/windowRect/clientRect/destroyTracking 的信息表。
**错误：** 窗口 ID 无效、过期或句柄已销毁时产生 Lua 错误。
**执行/取消语义：** 在执行器命令边界遵循暂停状态；等待操作期间响应暂停/停止。 等待操作响应统一停止令牌；停止会终止 worker，窗口等待取消后不保留后台任务。
**坐标/权限：** 坐标空间：screen（矩形均为虚拟桌面物理像素）。只读取窗口基本元数据；窗口矩形为右/下边界不包含的矩形。

**示例：**
```lua
local w = WindowFind("记事本")\nif w then local info = WindowGetInfo(w.id); Log(info.title) end
```

#### `WindowActivate(id)` — 激活窗口

- **稳定 ID：** `anjian.window.window-activate`
- **状态：** 当前已实现；自动化 API 契约测试覆盖，Windows 桌面实机验收待完成。
- **说明：** 恢复并请求将目标窗口置于前台；Windows 可能基于前台锁定策略拒绝。

| 参数 | Lua 类型 | 必填 | 默认值 | 范围/单位/选项 | 说明 |
|---|---|---|---|---|---|
| `id` | `string` | 是 | — | — | 窗口引用 ID。 |

**返回值：** `boolean` — Windows 接受前台激活请求时返回 true；受前台锁定策略拒绝时返回 false；窗口引用失效或 Win32 查询失败时产生错误。
**错误：** 参数类型、整数性或范围不合法时产生 Lua 错误。；目标窗口关闭、权限不足或 Windows API 调用失败时报告执行错误。
**执行/取消语义：** 在执行器命令边界遵循暂停状态；等待操作期间响应暂停/停止。 等待操作响应统一停止令牌；停止会终止 worker，窗口等待取消后不保留后台任务。
**坐标/权限：** 坐标空间：none。通过标准 Win32 窗口 API 请求操作；不保证应用接受激活请求，不提升权限、不绕过安全桌面。

**示例：**
```lua
local w = WindowFind("目标窗口")\nif w then WindowActivate(w.id) end
```

#### `WindowShow(id)` — 显示窗口

- **稳定 ID：** `anjian.window.window-show`
- **状态：** 当前已实现；自动化 API 契约测试覆盖，Windows 桌面实机验收待完成。
- **说明：** 显示目标窗口。

| 参数 | Lua 类型 | 必填 | 默认值 | 范围/单位/选项 | 说明 |
|---|---|---|---|---|---|
| `id` | `string` | 是 | — | — | 窗口引用 ID。 |

**返回值：** `boolean` — 操作请求成功返回 true；Windows 拒绝或窗口已失效时产生错误。
**错误：** 参数类型、整数性或范围不合法时产生 Lua 错误。；目标窗口关闭、权限不足或 Windows API 调用失败时报告执行错误。
**执行/取消语义：** 在执行器命令边界遵循暂停状态；等待操作期间响应暂停/停止。 等待操作响应统一停止令牌；停止会终止 worker，窗口等待取消后不保留后台任务。
**坐标/权限：** 坐标空间：none。通过标准 Win32 窗口 API 请求操作；不保证应用接受激活请求，不提升权限、不绕过安全桌面。

**示例：**
```lua
local w = WindowFind("目标窗口")\nif w then WindowShow(w.id) end
```

#### `WindowHide(id)` — 隐藏窗口

- **稳定 ID：** `anjian.window.window-hide`
- **状态：** 当前已实现；自动化 API 契约测试覆盖，Windows 桌面实机验收待完成。
- **说明：** 隐藏目标窗口。

| 参数 | Lua 类型 | 必填 | 默认值 | 范围/单位/选项 | 说明 |
|---|---|---|---|---|---|
| `id` | `string` | 是 | — | — | 窗口引用 ID。 |

**返回值：** `boolean` — 操作请求成功返回 true；Windows 拒绝或窗口已失效时产生错误。
**错误：** 参数类型、整数性或范围不合法时产生 Lua 错误。；目标窗口关闭、权限不足或 Windows API 调用失败时报告执行错误。
**执行/取消语义：** 在执行器命令边界遵循暂停状态；等待操作期间响应暂停/停止。 等待操作响应统一停止令牌；停止会终止 worker，窗口等待取消后不保留后台任务。
**坐标/权限：** 坐标空间：none。通过标准 Win32 窗口 API 请求操作；不保证应用接受激活请求，不提升权限、不绕过安全桌面。

**示例：**
```lua
local w = WindowFind("目标窗口")\nif w then WindowHide(w.id) end
```

#### `WindowMinimize(id)` — 最小化窗口

- **稳定 ID：** `anjian.window.window-minimize`
- **状态：** 当前已实现；自动化 API 契约测试覆盖，Windows 桌面实机验收待完成。
- **说明：** 最小化目标窗口。

| 参数 | Lua 类型 | 必填 | 默认值 | 范围/单位/选项 | 说明 |
|---|---|---|---|---|---|
| `id` | `string` | 是 | — | — | 窗口引用 ID。 |

**返回值：** `boolean` — 操作请求成功返回 true；Windows 拒绝或窗口已失效时产生错误。
**错误：** 参数类型、整数性或范围不合法时产生 Lua 错误。；目标窗口关闭、权限不足或 Windows API 调用失败时报告执行错误。
**执行/取消语义：** 在执行器命令边界遵循暂停状态；等待操作期间响应暂停/停止。 等待操作响应统一停止令牌；停止会终止 worker，窗口等待取消后不保留后台任务。
**坐标/权限：** 坐标空间：none。通过标准 Win32 窗口 API 请求操作；不保证应用接受激活请求，不提升权限、不绕过安全桌面。

**示例：**
```lua
local w = WindowFind("目标窗口")\nif w then WindowMinimize(w.id) end
```

#### `WindowMaximize(id)` — 最大化窗口

- **稳定 ID：** `anjian.window.window-maximize`
- **状态：** 当前已实现；自动化 API 契约测试覆盖，Windows 桌面实机验收待完成。
- **说明：** 最大化目标窗口。

| 参数 | Lua 类型 | 必填 | 默认值 | 范围/单位/选项 | 说明 |
|---|---|---|---|---|---|
| `id` | `string` | 是 | — | — | 窗口引用 ID。 |

**返回值：** `boolean` — 操作请求成功返回 true；Windows 拒绝或窗口已失效时产生错误。
**错误：** 参数类型、整数性或范围不合法时产生 Lua 错误。；目标窗口关闭、权限不足或 Windows API 调用失败时报告执行错误。
**执行/取消语义：** 在执行器命令边界遵循暂停状态；等待操作期间响应暂停/停止。 等待操作响应统一停止令牌；停止会终止 worker，窗口等待取消后不保留后台任务。
**坐标/权限：** 坐标空间：none。通过标准 Win32 窗口 API 请求操作；不保证应用接受激活请求，不提升权限、不绕过安全桌面。

**示例：**
```lua
local w = WindowFind("目标窗口")\nif w then WindowMaximize(w.id) end
```

#### `WindowRestore(id)` — 还原窗口

- **稳定 ID：** `anjian.window.window-restore`
- **状态：** 当前已实现；自动化 API 契约测试覆盖，Windows 桌面实机验收待完成。
- **说明：** 将目标窗口还原为非最小化/最大化状态。

| 参数 | Lua 类型 | 必填 | 默认值 | 范围/单位/选项 | 说明 |
|---|---|---|---|---|---|
| `id` | `string` | 是 | — | — | 窗口引用 ID。 |

**返回值：** `boolean` — 操作请求成功返回 true；Windows 拒绝或窗口已失效时产生错误。
**错误：** 参数类型、整数性或范围不合法时产生 Lua 错误。；目标窗口关闭、权限不足或 Windows API 调用失败时报告执行错误。
**执行/取消语义：** 在执行器命令边界遵循暂停状态；等待操作期间响应暂停/停止。 等待操作响应统一停止令牌；停止会终止 worker，窗口等待取消后不保留后台任务。
**坐标/权限：** 坐标空间：none。通过标准 Win32 窗口 API 请求操作；不保证应用接受激活请求，不提升权限、不绕过安全桌面。

**示例：**
```lua
local w = WindowFind("目标窗口")\nif w then WindowRestore(w.id) end
```

#### `WindowSetTopmost(id, enabled?)` — 设置窗口置顶

- **稳定 ID：** `anjian.window.window-set-topmost`
- **状态：** 当前已实现；自动化 API 契约测试覆盖，Windows 桌面实机验收待完成。
- **说明：** 将目标窗口设为置顶或取消置顶。

| 参数 | Lua 类型 | 必填 | 默认值 | 范围/单位/选项 | 说明 |
|---|---|---|---|---|---|
| `id` | `string` | 是 | — | — | 窗口引用 ID。 |
| `enabled` | `boolean` | 否 | `true` | — | true 为置顶，false 为取消置顶。 |

**返回值：** `boolean` — 操作请求成功返回 true；Windows API 调用失败时产生错误。
**错误：** 参数类型、整数性或范围不合法时产生 Lua 错误。；目标窗口关闭、权限不足或 Windows API 调用失败时报告执行错误。
**执行/取消语义：** 在执行器命令边界遵循暂停状态；等待操作期间响应暂停/停止。 等待操作响应统一停止令牌；停止会终止 worker，窗口等待取消后不保留后台任务。
**坐标/权限：** 坐标空间：none。只调整窗口 Z 顺序，不改变权限或桌面隔离。

**示例：**
```lua
local w = WindowFind("目标窗口")\nif w then WindowSetTopmost(w.id, true) end
```

#### `WindowMove(id, x, y)` — 移动窗口

- **稳定 ID：** `anjian.window.window-move`
- **状态：** 当前已实现；自动化 API 契约测试覆盖，Windows 桌面实机验收待完成。
- **说明：** 将目标窗口外框左上角移动到虚拟桌面屏幕绝对物理像素坐标；允许负坐标。

| 参数 | Lua 类型 | 必填 | 默认值 | 范围/单位/选项 | 说明 |
|---|---|---|---|---|---|
| `id` | `string` | 是 | — | — | 窗口引用 ID。 |
| `x` | `integer` | 是 | — | -2147483648–2147483647 物理像素 | 屏幕物理像素 X 坐标；允许负值。 |
| `y` | `integer` | 是 | — | -2147483648–2147483647 物理像素 | 屏幕物理像素 Y 坐标；允许负值。 |

**返回值：** `boolean` — 操作请求成功返回 true。
**错误：** 参数类型、整数性或范围不合法时产生 Lua 错误。；目标窗口关闭、权限不足或 Windows API 调用失败时报告执行错误。
**执行/取消语义：** 在执行器命令边界遵循暂停状态；等待操作期间响应暂停/停止。 等待操作响应统一停止令牌；停止会终止 worker，窗口等待取消后不保留后台任务。
**坐标/权限：** 坐标空间：screen（窗口外框左上角的虚拟桌面绝对物理像素）。使用标准 SetWindowPos；目标窗口可拒绝/约束位置，系统权限和桌面会话受 Windows 限制。

**示例：**
```lua
local w = WindowFind("目标窗口")\nif w then WindowMove(w.id, -1200, 100) end
```

#### `WindowResize(id, width, height)` — 调整窗口大小

- **稳定 ID：** `anjian.window.window-resize`
- **状态：** 当前已实现；自动化 API 契约测试覆盖，Windows 桌面实机验收待完成。
- **说明：** 调整目标窗口外框宽度和高度，单位为物理像素。

| 参数 | Lua 类型 | 必填 | 默认值 | 范围/单位/选项 | 说明 |
|---|---|---|---|---|---|
| `id` | `string` | 是 | — | — | 窗口引用 ID。 |
| `width` | `integer` | 是 | — | 1–32768 物理像素 | 外框宽度，1 到 32768 物理像素。 |
| `height` | `integer` | 是 | — | 1–32768 物理像素 | 外框高度，1 到 32768 物理像素。 |

**返回值：** `boolean` — 操作请求成功返回 true。
**错误：** 参数类型、整数性或范围不合法时产生 Lua 错误。；目标窗口关闭、权限不足或 Windows API 调用失败时报告执行错误。
**执行/取消语义：** 在执行器命令边界遵循暂停状态；等待操作期间响应暂停/停止。 等待操作响应统一停止令牌；停止会终止 worker，窗口等待取消后不保留后台任务。
**坐标/权限：** 坐标空间：screen（物理像素尺寸）。使用标准 SetWindowPos；目标窗口可能具有最小/最大尺寸约束。

**示例：**
```lua
local w = WindowFind("目标窗口")\nif w then WindowResize(w.id, 1280, 800) end
```

#### `WindowAlign(id, anchor?, offsetX?, offsetY?)` — 窗口锚点对齐

- **稳定 ID：** `anjian.window.window-align`
- **状态：** 当前已实现；自动化 API 契约测试覆盖，Windows 桌面实机验收待完成。
- **说明：** 将窗口指定锚点对齐到窗口所在显示器的工作区同名锚点，可叠加物理像素偏移。

| 参数 | Lua 类型 | 必填 | 默认值 | 范围/单位/选项 | 说明 |
|---|---|---|---|---|---|
| `id` | `string` | 是 | — | — | 窗口引用 ID。 |
| `anchor` | `string` | 否 | `center` | — | top-left/top-center/top-right/center-left/center/center-right/bottom-left/bottom-center/bottom-right。 |
| `offsetX` | `integer` | 否 | `0` | -2147483648–2147483647 物理像素 | 水平物理像素偏移。 |
| `offsetY` | `integer` | 否 | `0` | -2147483648–2147483647 物理像素 | 垂直物理像素偏移。 |

**返回值：** `boolean` — 操作请求成功返回 true。
**错误：** 参数类型、整数性或范围不合法时产生 Lua 错误。；目标窗口关闭、权限不足或 Windows API 调用失败时报告执行错误。
**执行/取消语义：** 在执行器命令边界遵循暂停状态；等待操作期间响应暂停/停止。 等待操作响应统一停止令牌；停止会终止 worker，窗口等待取消后不保留后台任务。
**坐标/权限：** 坐标空间：screen（显示器工作区物理像素）。依据目标窗口所在显示器的工作区，不遮挡系统任务栏；多显示器使用目标窗口最近显示器。

**示例：**
```lua
local w = WindowFind("目标窗口")\nif w then WindowAlign(w.id, "center", 0, 0) end
```

#### `WindowWait(titleContains?, className?, processName?, processId?, visibleOnly?, timeoutMs?)` — 等待窗口出现

- **稳定 ID：** `anjian.window.window-wait`
- **状态：** 当前已实现；自动化 API 契约测试覆盖，Windows 桌面实机验收待完成。
- **说明：** 按筛选条件轮询等待窗口出现，使用单调时钟计算超时；返回匹配信息表，超时返回 nil，停止请求抛出取消结果。

| 参数 | Lua 类型 | 必填 | 默认值 | 范围/单位/选项 | 说明 |
|---|---|---|---|---|---|
| `titleContains` | `string` | 否 | `` | — | 标题包含匹配；空字符串表示不筛选。 |
| `className` | `string` | 否 | `` | — | 窗口类名精确匹配；空字符串表示不筛选。 |
| `processName` | `string` | 否 | `` | — | 进程名匹配，可省略 .exe；空字符串表示不筛选。 |
| `processId` | `integer` | 否 | `0` | 0–2147483647 | Windows 进程 ID；0 表示不筛选。 |
| `visibleOnly` | `boolean` | 否 | `true` | — | true 仅返回可见窗口；false 同时包括隐藏窗口。 |
| `timeoutMs` | `integer` | 否 | `30000` | 0–600000 毫秒 | 等待超时，0 立即检查一次，最大 600000 毫秒。 |

**返回值：** `table\|nil` — 出现时返回窗口信息表；超时返回 nil；取消会终止当前脚本。
**错误：** 超时超出 0 到 600000 毫秒或筛选参数非法时产生 Lua 错误。；停止操作返回 Cancelled，不吞掉为 nil。
**执行/取消语义：** 在执行器命令边界遵循暂停状态；等待操作期间响应暂停/停止。 等待操作响应统一停止令牌；停止会终止 worker，窗口等待取消后不保留后台任务。
**坐标/权限：** 坐标空间：none。轮询当前交互桌面顶层窗口；不等待安全桌面或其他用户会话。

**示例：**
```lua
local w = WindowWait("记事本", "", "", 0, true, 10000)\nif w then Log(w.title) end
```

#### `WindowWaitClosed(id, timeoutMs?)` — 等待窗口关闭

- **稳定 ID：** `anjian.window.window-wait-closed`
- **状态：** 当前已实现；自动化 API 契约测试覆盖，Windows 桌面实机验收待完成。
- **说明：** 等待指定窗口引用所指窗口关闭；关闭/销毁返回 true，超时返回 false，停止请求抛出取消结果。

| 参数 | Lua 类型 | 必填 | 默认值 | 范围/单位/选项 | 说明 |
|---|---|---|---|---|---|
| `id` | `string` | 是 | — | — | 窗口引用 ID。 |
| `timeoutMs` | `integer` | 否 | `30000` | 0–600000 毫秒 | 等待超时，最大 600000 毫秒。 |

**返回值：** `boolean` — 窗口关闭返回 true；超时仍存在返回 false。
**错误：** 窗口引用不存在时产生 Lua 错误。；停止操作返回 Cancelled，不吞掉为 false。
**执行/取消语义：** 在执行器命令边界遵循暂停状态；等待操作期间响应暂停/停止。 等待操作响应统一停止令牌；停止会终止 worker，窗口等待取消后不保留后台任务。
**坐标/权限：** 坐标空间：none。只检测本次运行所引用的窗口，不尝试关闭窗口。

**示例：**
```lua
local w = WindowFind("记事本")\nif w then WindowWaitClosed(w.id, 30000) end
```

#### `CoordinateToScreen(id, space, x, y, anchor?)` — 转换到屏幕坐标

- **稳定 ID：** `anjian.window.coordinate-to-screen`
- **状态：** 当前已实现；自动化 API 契约测试覆盖，Windows 桌面实机验收待完成。
- **说明：** 将指定窗口/客户区/锚点/归一化坐标转换为虚拟桌面物理像素坐标。

| 参数 | Lua 类型 | 必填 | 默认值 | 范围/单位/选项 | 说明 |
|---|---|---|---|---|---|
| `id` | `string` | 是 | — | — | 窗口引用 ID；screen 模式亦要求有效引用以保证调用窗口状态明确。 |
| `space` | `string` | 是 | — | 固定值：screen、window、client、anchor、ratio | screen/window/client/anchor/ratio。 |
| `x` | `number` | 是 | — | — | X 坐标；物理空间为像素，ratio 空间为 0 到 1。 |
| `y` | `number` | 是 | — | — | Y 坐标；物理空间为像素，ratio 空间为 0 到 1。 |
| `anchor` | `string` | 否 | `top-left` | — | anchor 模式采用的窗口锚点。 |

**返回值：** `table` — 返回包含 x、y 的坐标表，坐标以物理屏幕像素或所选空间单位表示。
**错误：** 窗口引用失效、空间名称非法、坐标非有限数值或 ratio 输入超出 0 到 1 时产生 Lua 错误。
**执行/取消语义：** 在执行器命令边界遵循暂停状态；等待操作期间响应暂停/停止。 等待操作响应统一停止令牌；停止会终止 worker，窗口等待取消后不保留后台任务。
**坐标/权限：** 坐标空间：screen/window/client/anchor/ratio；window 使用外框左上；client/ratio 使用客户区；anchor 使用窗口外框九宫格。只执行物理坐标转换，不发送鼠标/键盘输入。窗口矩形来自当前 DPI-aware Win32 查询。

**示例：**
```lua
local w = WindowFind("目标窗口")\nif w then\n  local p = CoordinateToScreen(w.id, "client", 100, 80)\n  Log(p.x .. "," .. p.y)\nend
```

#### `CoordinateFromScreen(id, space, x, y, anchor?)` — 从屏幕坐标转换

- **稳定 ID：** `anjian.window.coordinate-from-screen`
- **状态：** 当前已实现；自动化 API 契约测试覆盖，Windows 桌面实机验收待完成。
- **说明：** 将虚拟桌面物理像素坐标反向转换为指定坐标空间。

| 参数 | Lua 类型 | 必填 | 默认值 | 范围/单位/选项 | 说明 |
|---|---|---|---|---|---|
| `id` | `string` | 是 | — | — | 窗口引用 ID；screen 模式亦要求有效引用以保证调用窗口状态明确。 |
| `space` | `string` | 是 | — | 固定值：screen、window、client、anchor、ratio | screen/window/client/anchor/ratio。 |
| `x` | `number` | 是 | — | — | X 坐标；物理空间为像素，ratio 空间为 0 到 1。 |
| `y` | `number` | 是 | — | — | Y 坐标；物理空间为像素，ratio 空间为 0 到 1。 |
| `anchor` | `string` | 否 | `top-left` | — | anchor 模式采用的窗口锚点。 |

**返回值：** `table` — 返回包含 x、y 的坐标表，坐标以物理屏幕像素或所选空间单位表示。
**错误：** 窗口引用失效、空间名称非法、坐标非有限数值或 ratio 输入超出 0 到 1 时产生 Lua 错误。
**执行/取消语义：** 在执行器命令边界遵循暂停状态；等待操作期间响应暂停/停止。 等待操作响应统一停止令牌；停止会终止 worker，窗口等待取消后不保留后台任务。
**坐标/权限：** 坐标空间：screen/window/client/anchor/ratio；window 使用外框左上；client/ratio 使用客户区；anchor 使用窗口外框九宫格。只执行物理坐标转换，不发送鼠标/键盘输入。窗口矩形来自当前 DPI-aware Win32 查询。

**示例：**
```lua
local w = WindowFind("目标窗口")\nif w then\n  local p = CoordinateFromScreen(w.id, "client", 100, 80)\n  Log(p.x .. "," .. p.y)\nend
```

### 2.5 控件与输入自动化

#### `WindowBind(id)` — 绑定自动化窗口

- **稳定 ID：** `anjian.automation.window-bind`
- **状态：** 当前已实现；自动化 API 契约测试覆盖，Windows 桌面实机验收待完成。
- **说明：** 将经过 M6 身份校验的不透明窗口引用绑定到本轮独立 Lua worker 的 UI Automation 会话。脚本只获得随机 bindingId，不获得 HWND。

| 参数 | Lua 类型 | 必填 | 默认值 | 范围/单位/选项 | 说明 |
|---|---|---|---|---|---|
| `id` | `string` | 是 | — | — | 由 WindowFind/WindowEnumerate 返回的窗口引用 ID。 |

**返回值：** `string` — 新的运行期窗口绑定 ID。
**错误：** 窗口引用不存在、已失效或目标进程身份无法验证时产生错误。
**执行/取消语义：** 在 Lua 命令边界遵循暂停状态；UI Automation 同步调用期间无法被中途抢占。 UI Automation 在独立 Lua worker 进程内运行；Stop 可直接终止卡在 provider/COM 调用中的 worker，主进程 UI 不会被该 COM 调用阻塞。
**坐标/权限：** 坐标空间：none。仅使用公开 Windows UI Automation API；受 UIPI、UAC、安全桌面、用户会话和目标 provider 能力约束，不提升权限或绕过访问控制。

**示例：**
```lua
local w = WindowFind("记事本")
if w then local binding = WindowBind(w.id) end
```

#### `WindowUnbind(bindingId)` — 解除自动化窗口绑定

- **稳定 ID：** `anjian.automation.window-unbind`
- **状态：** 当前已实现；自动化 API 契约测试覆盖，Windows 桌面实机验收待完成。
- **说明：** 解除 WindowBind 建立的绑定并释放该绑定缓存的控件引用。

| 参数 | Lua 类型 | 必填 | 默认值 | 范围/单位/选项 | 说明 |
|---|---|---|---|---|---|
| `bindingId` | `string` | 是 | — | — | WindowBind 返回的运行期绑定 ID。 |

**返回值：** `boolean` — 解绑成功返回 true；不存在或已经解绑返回 false。
**错误：** 绑定 ID 为空时产生参数错误。
**执行/取消语义：** 在 Lua 命令边界遵循暂停状态；UI Automation 同步调用期间无法被中途抢占。 UI Automation 在独立 Lua worker 进程内运行；Stop 可直接终止卡在 provider/COM 调用中的 worker，主进程 UI 不会被该 COM 调用阻塞。
**坐标/权限：** 坐标空间：none。仅使用公开 Windows UI Automation API；受 UIPI、UAC、安全桌面、用户会话和目标 provider 能力约束，不提升权限或绕过访问控制。

**示例：**
```lua
local binding = WindowBind(w.id)
WindowUnbind(binding)
```

#### `WindowGetCapabilities(bindingId)` — 查询窗口自动化能力

- **稳定 ID：** `anjian.automation.window-capabilities`
- **状态：** 当前已实现；自动化 API 契约测试覆盖，Windows 桌面实机验收待完成。
- **说明：** 逐项报告目标窗口的 UIA 控件树/标准 Pattern、可用前台 SendInput，以及尚不支持的通用后台鼠标/键盘/文本输入能力；包含后端、状态、原因和限制。

| 参数 | Lua 类型 | 必填 | 默认值 | 范围/单位/选项 | 说明 |
|---|---|---|---|---|---|
| `bindingId` | `string` | 是 | — | — | WindowBind 返回的运行期绑定 ID。 |

**返回值：** `table` — 返回 provider、目标 PID、前台状态以及 capabilities 数组；每项含 name/status/backend/reason/limitations。
**错误：** 绑定失效或窗口关闭时产生错误。
**执行/取消语义：** 在 Lua 命令边界遵循暂停状态；UI Automation 同步调用期间无法被中途抢占。 UI Automation 在独立 Lua worker 进程内运行；Stop 可直接终止卡在 provider/COM 调用中的 worker，主进程 UI 不会被该 COM 调用阻塞。
**坐标/权限：** 坐标空间：none。仅使用公开 Windows UI Automation API；受 UIPI、UAC、安全桌面、用户会话和目标 provider 能力约束，不提升权限或绕过访问控制。

**示例：**
```lua
local caps = WindowGetCapabilities(binding)
for i, item in ipairs(caps.capabilities) do Log(item.name .. ":" .. item.status) end
```

#### `ControlEnumerate(bindingId, maxResults?)` — 枚举标准控件

- **稳定 ID：** `anjian.automation.control-enumerate`
- **状态：** 当前已实现；自动化 API 契约测试覆盖，Windows 桌面实机验收待完成。
- **说明：** 按 UI Automation Control View 深度优先枚举目标窗口根元素及控件，返回运行期 controlId、名称、AutomationId、类名、控件类型、物理像素矩形、状态及受支持 Pattern。

| 参数 | Lua 类型 | 必填 | 默认值 | 范围/单位/选项 | 说明 |
|---|---|---|---|---|---|
| `bindingId` | `string` | 是 | — | — | WindowBind 返回的运行期绑定 ID。 |
| `maxResults` | `integer` | 否 | `256` | 1–512 | 最多返回控件数量，范围 1 到 512。 |

**返回值：** `table` — 返回控件信息数组；每项含 controlId/name/automationId/className/controlType/bounds/isEnabled/isOffscreen/isKeyboardFocusable/isPassword/supportedPatterns。
**错误：** 数量超出范围、绑定失效或 UI Automation 无法访问目标窗口时产生错误。
**执行/取消语义：** 在 Lua 命令边界遵循暂停状态；UI Automation 同步调用期间无法被中途抢占。 UI Automation 在独立 Lua worker 进程内运行；Stop 可直接终止卡在 provider/COM 调用中的 worker，主进程 UI 不会被该 COM 调用阻塞。
**坐标/权限：** 坐标空间：none。仅使用公开 Windows UI Automation API；受 UIPI、UAC、安全桌面、用户会话和目标 provider 能力约束，不提升权限或绕过访问控制。

**示例：**
```lua
local controls = ControlEnumerate(binding, 256)
for i, c in ipairs(controls) do Log(c.name .. " " .. c.controlType) end
```

#### `ControlGetInfo(bindingId, controlId)` — 读取控件信息

- **稳定 ID：** `anjian.automation.control-get-info`
- **状态：** 当前已实现；自动化 API 契约测试覆盖，Windows 桌面实机验收待完成。
- **说明：** 读取当前枚举所得控件的最新 UIA 元数据与可用 Pattern；控件已销毁时要求重新枚举。

| 参数 | Lua 类型 | 必填 | 默认值 | 范围/单位/选项 | 说明 |
|---|---|---|---|---|---|
| `bindingId` | `string` | 是 | — | — | WindowBind 返回的运行期绑定 ID。 |
| `controlId` | `string` | 是 | — | — | ControlEnumerate 返回的运行期控件 ID。 |

**返回值：** `table` — 返回与 ControlEnumerate 相同结构的单个控件信息表。
**错误：** 绑定/控件 ID 失效或控件已销毁时产生明确错误。
**执行/取消语义：** 在 Lua 命令边界遵循暂停状态；UI Automation 同步调用期间无法被中途抢占。 UI Automation 在独立 Lua worker 进程内运行；Stop 可直接终止卡在 provider/COM 调用中的 worker，主进程 UI 不会被该 COM 调用阻塞。
**坐标/权限：** 坐标空间：none。仅使用公开 Windows UI Automation API；受 UIPI、UAC、安全桌面、用户会话和目标 provider 能力约束，不提升权限或绕过访问控制。

**示例：**
```lua
local info = ControlGetInfo(binding, control.controlId)
```

#### `ControlGetValue(bindingId, controlId)` — 读取控件值

- **稳定 ID：** `anjian.automation.control-get-value`
- **状态：** 当前已实现；自动化 API 契约测试覆盖，Windows 桌面实机验收待完成。
- **说明：** 读取控件 UIA ValuePattern 当前值。

| 参数 | Lua 类型 | 必填 | 默认值 | 范围/单位/选项 | 说明 |
|---|---|---|---|---|---|
| `bindingId` | `string` | 是 | — | — | 窗口绑定 ID。 |
| `controlId` | `string` | 是 | — | — | 控件运行期 ID。 |

**返回值：** `string` — ValuePattern 当前字符串值。
**错误：** 控件未实现 ValuePattern、绑定失效或控件销毁时产生错误。
**执行/取消语义：** 在 Lua 命令边界遵循暂停状态；UI Automation 同步调用期间无法被中途抢占。 UI Automation 在独立 Lua worker 进程内运行；Stop 可直接终止卡在 provider/COM 调用中的 worker，主进程 UI 不会被该 COM 调用阻塞。
**坐标/权限：** 坐标空间：none。仅使用公开 Windows UI Automation API；受 UIPI、UAC、安全桌面、用户会话和目标 provider 能力约束，不提升权限或绕过访问控制。

**示例：**
```lua
local value = ControlGetValue(binding, edit.controlId)
```

#### `ControlSetValue(bindingId, controlId, value)` — 设置控件值

- **稳定 ID：** `anjian.automation.control-set-value`
- **状态：** 当前已实现；自动化 API 契约测试覆盖，Windows 桌面实机验收待完成。
- **说明：** 通过 UIA ValuePattern.SetValue 设置控件值；只读、不支持该 Pattern 或值超长时明确失败，不回退为键盘输入。

| 参数 | Lua 类型 | 必填 | 默认值 | 范围/单位/选项 | 说明 |
|---|---|---|---|---|---|
| `bindingId` | `string` | 是 | — | — | 窗口绑定 ID。 |
| `controlId` | `string` | 是 | — | — | 控件运行期 ID。 |
| `value` | `string` | 是 | — | — | 要设置的字符串，最多 32768 个字符。 |

**返回值：** `boolean` — UIA provider 的 SetValue 调用成功返回 true。
**错误：** 不支持 ValuePattern、只读、控件失效、权限拒绝或值超过 32768 字符时产生错误。
**执行/取消语义：** 在 Lua 命令边界遵循暂停状态；UI Automation 同步调用期间无法被中途抢占。 UI Automation 在独立 Lua worker 进程内运行；Stop 可直接终止卡在 provider/COM 调用中的 worker，主进程 UI 不会被该 COM 调用阻塞。
**坐标/权限：** 坐标空间：none。仅使用公开 Windows UI Automation API；受 UIPI、UAC、安全桌面、用户会话和目标 provider 能力约束，不提升权限或绕过访问控制。

**示例：**
```lua
ControlSetValue(binding, edit.controlId, "hello")
```

#### `ControlGetText(bindingId, controlId)` — 读取控件文本

- **稳定 ID：** `anjian.automation.control-get-text`
- **状态：** 当前已实现；自动化 API 契约测试覆盖，Windows 桌面实机验收待完成。
- **说明：** 优先读取 UIA TextPattern 文档范围文本；若无 TextPattern，则读取 ValuePattern 值。

| 参数 | Lua 类型 | 必填 | 默认值 | 范围/单位/选项 | 说明 |
|---|---|---|---|---|---|
| `bindingId` | `string` | 是 | — | — | 窗口绑定 ID。 |
| `controlId` | `string` | 是 | — | — | 控件运行期 ID。 |

**返回值：** `string` — 最多 32768 个字符的控件文本。
**错误：** 控件未实现 TextPattern/ValuePattern、绑定失效或控件销毁时产生错误。
**执行/取消语义：** 在 Lua 命令边界遵循暂停状态；UI Automation 同步调用期间无法被中途抢占。 UI Automation 在独立 Lua worker 进程内运行；Stop 可直接终止卡在 provider/COM 调用中的 worker，主进程 UI 不会被该 COM 调用阻塞。
**坐标/权限：** 坐标空间：none。仅使用公开 Windows UI Automation API；受 UIPI、UAC、安全桌面、用户会话和目标 provider 能力约束，不提升权限或绕过访问控制。

**示例：**
```lua
local text = ControlGetText(binding, edit.controlId)
```

#### `ControlGetSelection(bindingId, controlId)` — 读取控件选项

- **稳定 ID：** `anjian.automation.control-get-selection`
- **状态：** 当前已实现；自动化 API 契约测试覆盖，Windows 桌面实机验收待完成。
- **说明：** 读取容器 UIA SelectionPattern 当前已选元素名称数组。

| 参数 | Lua 类型 | 必填 | 默认值 | 范围/单位/选项 | 说明 |
|---|---|---|---|---|---|
| `bindingId` | `string` | 是 | — | — | 窗口绑定 ID。 |
| `controlId` | `string` | 是 | — | — | 支持 SelectionPattern 的容器控件 ID。 |

**返回值：** `table` — 返回 Lua 数组，数组元素是 provider 报告的已选控件名称。
**错误：** 控件未实现 SelectionPattern、绑定失效或控件销毁时产生错误。
**执行/取消语义：** 在 Lua 命令边界遵循暂停状态；UI Automation 同步调用期间无法被中途抢占。 UI Automation 在独立 Lua worker 进程内运行；Stop 可直接终止卡在 provider/COM 调用中的 worker，主进程 UI 不会被该 COM 调用阻塞。
**坐标/权限：** 坐标空间：none。仅使用公开 Windows UI Automation API；受 UIPI、UAC、安全桌面、用户会话和目标 provider 能力约束，不提升权限或绕过访问控制。

**示例：**
```lua
local selected = ControlGetSelection(binding, list.controlId)
```

#### `ControlSelect(bindingId, controlId)` — 选择控件项

- **稳定 ID：** `anjian.automation.control-select`
- **状态：** 当前已实现；自动化 API 契约测试覆盖，Windows 桌面实机验收待完成。
- **说明：** 调用控件 UIA SelectionItemPattern.Select 选择单个项目。

| 参数 | Lua 类型 | 必填 | 默认值 | 范围/单位/选项 | 说明 |
|---|---|---|---|---|---|
| `bindingId` | `string` | 是 | — | — | 窗口绑定 ID。 |
| `controlId` | `string` | 是 | — | — | 支持 SelectionItemPattern 的项目控件 ID。 |

**返回值：** `boolean` — SelectionItemPattern.Select 调用成功返回 true。
**错误：** 控件未实现 SelectionItemPattern、选择被容器拒绝或控件失效时产生错误。
**执行/取消语义：** 在 Lua 命令边界遵循暂停状态；UI Automation 同步调用期间无法被中途抢占。 UI Automation 在独立 Lua worker 进程内运行；Stop 可直接终止卡在 provider/COM 调用中的 worker，主进程 UI 不会被该 COM 调用阻塞。
**坐标/权限：** 坐标空间：none。仅使用公开 Windows UI Automation API；受 UIPI、UAC、安全桌面、用户会话和目标 provider 能力约束，不提升权限或绕过访问控制。

**示例：**
```lua
ControlSelect(binding, item.controlId)
```

#### `ControlGetToggleState(bindingId, controlId)` — 读取勾选状态

- **稳定 ID：** `anjian.automation.control-toggle-state`
- **状态：** 当前已实现；自动化 API 契约测试覆盖，Windows 桌面实机验收待完成。
- **说明：** 读取 UIA TogglePattern 状态 Off、On 或 Indeterminate。

| 参数 | Lua 类型 | 必填 | 默认值 | 范围/单位/选项 | 说明 |
|---|---|---|---|---|---|
| `bindingId` | `string` | 是 | — | — | 窗口绑定 ID。 |
| `controlId` | `string` | 是 | — | — | 支持 TogglePattern 的控件 ID。 |

**返回值：** `string` — Off、On 或 Indeterminate。
**错误：** 控件未实现 TogglePattern、绑定失效或控件销毁时产生错误。
**执行/取消语义：** 在 Lua 命令边界遵循暂停状态；UI Automation 同步调用期间无法被中途抢占。 UI Automation 在独立 Lua worker 进程内运行；Stop 可直接终止卡在 provider/COM 调用中的 worker，主进程 UI 不会被该 COM 调用阻塞。
**坐标/权限：** 坐标空间：none。仅使用公开 Windows UI Automation API；受 UIPI、UAC、安全桌面、用户会话和目标 provider 能力约束，不提升权限或绕过访问控制。

**示例：**
```lua
local state = ControlGetToggleState(binding, checkbox.controlId)
```

#### `ControlToggle(bindingId, controlId)` — 切换勾选状态

- **稳定 ID：** `anjian.automation.control-toggle`
- **状态：** 当前已实现；自动化 API 契约测试覆盖，Windows 桌面实机验收待完成。
- **说明：** 调用控件 UIA TogglePattern.Toggle 切换一次状态；不会假设调用前状态。

| 参数 | Lua 类型 | 必填 | 默认值 | 范围/单位/选项 | 说明 |
|---|---|---|---|---|---|
| `bindingId` | `string` | 是 | — | — | 窗口绑定 ID。 |
| `controlId` | `string` | 是 | — | — | 支持 TogglePattern 的控件 ID。 |

**返回值：** `boolean` — TogglePattern.Toggle 调用成功返回 true。
**错误：** 控件未实现 TogglePattern、provider 拒绝或控件失效时产生错误。
**执行/取消语义：** 在 Lua 命令边界遵循暂停状态；UI Automation 同步调用期间无法被中途抢占。 UI Automation 在独立 Lua worker 进程内运行；Stop 可直接终止卡在 provider/COM 调用中的 worker，主进程 UI 不会被该 COM 调用阻塞。
**坐标/权限：** 坐标空间：none。仅使用公开 Windows UI Automation API；受 UIPI、UAC、安全桌面、用户会话和目标 provider 能力约束，不提升权限或绕过访问控制。

**示例：**
```lua
ControlToggle(binding, checkbox.controlId)
```

#### `ControlInvoke(bindingId, controlId)` — 触发控件操作

- **稳定 ID：** `anjian.automation.control-invoke`
- **状态：** 当前已实现；自动化 API 契约测试覆盖，Windows 桌面实机验收待完成。
- **说明：** 调用控件 UIA InvokePattern.Invoke，适用于 provider 声明可调用的按钮等元素。

| 参数 | Lua 类型 | 必填 | 默认值 | 范围/单位/选项 | 说明 |
|---|---|---|---|---|---|
| `bindingId` | `string` | 是 | — | — | 窗口绑定 ID。 |
| `controlId` | `string` | 是 | — | — | 支持 InvokePattern 的控件 ID。 |

**返回值：** `boolean` — InvokePattern.Invoke 调用成功返回 true。
**错误：** 控件未实现 InvokePattern、provider 拒绝或控件失效时产生错误。
**执行/取消语义：** 在 Lua 命令边界遵循暂停状态；UI Automation 同步调用期间无法被中途抢占。 UI Automation 在独立 Lua worker 进程内运行；Stop 可直接终止卡在 provider/COM 调用中的 worker，主进程 UI 不会被该 COM 调用阻塞。
**坐标/权限：** 坐标空间：none。仅使用公开 Windows UI Automation API；受 UIPI、UAC、安全桌面、用户会话和目标 provider 能力约束，不提升权限或绕过访问控制。

**示例：**
```lua
ControlInvoke(binding, button.controlId)
```

#### `WindowInput(bindingId, backend, action, name?, x?, y?, value?, holdMs?)` — 按能力执行窗口输入

- **稳定 ID：** `anjian.automation.window-input`
- **状态：** 当前已实现；自动化 API 契约测试覆盖，Windows 桌面实机验收待完成。
- **说明：** 执行经过显式能力边界约束的前台输入。目标窗口必须可见、未最小化且处于前台；键盘/文本还要求窗口内有键盘焦点，鼠标坐标必须位于客户区。通用后台鼠标/键盘/文本注入明确不支持，且不会静默回退。

| 参数 | Lua 类型 | 必填 | 默认值 | 范围/单位/选项 | 说明 |
|---|---|---|---|---|---|
| `bindingId` | `string` | 是 | — | — | 窗口绑定 ID。 |
| `backend` | `string` | 是 | — | 固定值：auto、foreground | auto 或 foreground。 |
| `action` | `string` | 是 | — | 固定值：move、click、key-press、text | move、click、key-press 或 text。 |
| `name` | `string` | 否 | `` | — | click/key-press 必填；click 为 left/right/middle，key-press 为现有规范键名。 |
| `x` | `integer` | 否 | `0` | -2147483648–2147483647 物理像素 | move/click 必填；绑定窗口客户区内的屏幕绝对物理像素 X 坐标。 |
| `y` | `integer` | 否 | `0` | -2147483648–2147483647 物理像素 | move/click 必填；绑定窗口客户区内的屏幕绝对物理像素 Y 坐标。 |
| `value` | `string` | 否 | `` | — | text 必填；Unicode 文本，最多 100000 个字符。 |
| `holdMs` | `integer` | 否 | `50` | 0–600000 毫秒 | click/key-press 按下时长，范围 0 到 600000 毫秒。 |

**返回值：** `boolean` — 输入 API 成功提交返回 true；实际目标控件是否响应仍取决于前台焦点、权限与应用状态。
**错误：** backend 不支持、目标不是前台窗口、参数非法、SendInput 被 Windows 拒绝或停止取消时产生错误。
**执行/取消语义：** 在 Lua 命令边界遵循暂停状态；UI Automation 同步调用期间无法被中途抢占。 前台输入在每个动作边界检查窗口状态；统一输入控制器跟踪按下状态，Stop/异常时由主进程执行 ReleaseAllOwnedInputs。
**坐标/权限：** 坐标空间：move/click 使用绑定窗口客户区内的虚拟桌面绝对物理像素；key-press/text 无坐标。。SendInput 前台输入；目标窗口必须可见、未最小化并保持前台。键盘/文本还要求目标窗口内有键盘焦点。move/click 的坐标必须位于绑定窗口客户区。遵守 UIPI、UAC 和交互桌面限制；不注入后台窗口，不自动抢占焦点。

**示例：**
```lua
WindowInput(binding, "auto", "click", "left", 400, 300, "", 50)
```

### 2.6 截图与图像识别

#### `CaptureScreen()` — 截取虚拟桌面

- **稳定 ID：** `anjian.imaging.captureScreen`
- **状态：** 当前已实现；自动化 API 契约测试覆盖，Windows 桌面实机验收待完成。
- **说明：** 捕获 Windows 虚拟桌面的可见合成画面，返回本轮有效的图像资源 ID。副屏负坐标保存在 origin 元数据中。

**参数：** 无。

**返回值：** `string` — 本轮图像资源 ID。
**错误：** 桌面不可用、截图失败或超过图像/资源池限制时产生错误。
**执行/取消语义：** 在主进程图像操作命令边界遵循暂停状态。 本轮 worker 结束时主进程清空本轮所有图像资源。
**坐标/权限：** 坐标空间：返回带屏幕原点的图像；像素使用图像局部零基坐标。。不新增第三方图像引擎依赖。截图依赖当前 Windows 交互桌面的可见合成画面；不保证被遮挡/最小化窗口、硬件加速表面或受保护内容的真实后台像素。不绕过 UIPI/UAC/安全桌面。每张图像最多 32,000,000 像素；单轮最多 128 个资源与 256 MiB 像素缓存。

**示例：**
```lua
local screen = CaptureScreen()
```

#### `CaptureWindow(windowId)` — 截取窗口外框

- **稳定 ID：** `anjian.imaging.captureWindow`
- **状态：** 当前已实现；自动化 API 契约测试覆盖，Windows 桌面实机验收待完成。
- **说明：** 截取指定可见且未最小化窗口的屏幕外框区域。截图来自桌面合成画面，窗口被其他内容遮挡时不保证得到其后台内容。

| 参数 | Lua 类型 | 必填 | 默认值 | 范围/单位/选项 | 说明 |
|---|---|---|---|---|---|
| `windowId` | `string` | 是 | — | — | 当前运行内有效的 M6 不透明窗口引用 ID。 |

**返回值：** `string` — 本轮图像资源 ID。
**错误：** 窗口引用失效、窗口隐藏/最小化、区域无效或截图失败时产生错误。
**执行/取消语义：** 在主进程图像操作命令边界遵循暂停状态。 本轮 worker 结束时主进程清空本轮所有图像资源。
**坐标/权限：** 坐标空间：屏幕物理像素；结果同时提供局部像素坐标与 screenX/screenY。。不新增第三方图像引擎依赖。截图依赖当前 Windows 交互桌面的可见合成画面；不保证被遮挡/最小化窗口、硬件加速表面或受保护内容的真实后台像素。不绕过 UIPI/UAC/安全桌面。每张图像最多 32,000,000 像素；单轮最多 128 个资源与 256 MiB 像素缓存。

**示例：**
```lua
local image = CaptureWindow(window.id)
```

#### `CaptureClient(windowId)` — 截取窗口客户区

- **稳定 ID：** `anjian.imaging.captureClient`
- **状态：** 当前已实现；自动化 API 契约测试覆盖，Windows 桌面实机验收待完成。
- **说明：** 截取指定可见且未最小化窗口的客户区屏幕区域；不含标题栏和边框。被遮挡内容不保证可获取。

| 参数 | Lua 类型 | 必填 | 默认值 | 范围/单位/选项 | 说明 |
|---|---|---|---|---|---|
| `windowId` | `string` | 是 | — | — | 当前运行内有效的 M6 不透明窗口引用 ID。 |

**返回值：** `string` — 本轮图像资源 ID。
**错误：** 窗口引用失效、窗口隐藏/最小化、客户区无效或截图失败时产生错误。
**执行/取消语义：** 在主进程图像操作命令边界遵循暂停状态。 本轮 worker 结束时主进程清空本轮所有图像资源。
**坐标/权限：** 坐标空间：屏幕物理像素；结果同时提供局部像素坐标与 screenX/screenY。。不新增第三方图像引擎依赖。截图依赖当前 Windows 交互桌面的可见合成画面；不保证被遮挡/最小化窗口、硬件加速表面或受保护内容的真实后台像素。不绕过 UIPI/UAC/安全桌面。每张图像最多 32,000,000 像素；单轮最多 128 个资源与 256 MiB 像素缓存。

**示例：**
```lua
local image = CaptureClient(window.id)
```

#### `CaptureRegion(windowId, space, x, y, width, height, anchor?)` — 按坐标空间截图

- **稳定 ID：** `anjian.imaging.captureRegion`
- **状态：** 当前已实现；自动化 API 契约测试覆盖，Windows 桌面实机验收待完成。
- **说明：** 按 screen/window/client/anchor/ratio 坐标空间捕获自定义矩形。windowId 为空字符串时必须使用 screen，坐标为虚拟桌面屏幕物理像素；非空 ID 复用 M6 双向坐标转换契约。

| 参数 | Lua 类型 | 必填 | 默认值 | 范围/单位/选项 | 说明 |
|---|---|---|---|---|---|
| `windowId` | `string` | 是 | — | — | 当前运行内的窗口引用 ID；空字符串表示不使用窗口引用，且只允许 screen 坐标。 |
| `space` | `string` | 是 | — | — | screen、window、client、anchor 或 ratio。 |
| `x` | `number` | 是 | — | — | 区域左上角 X；使用指定坐标空间的单位。 |
| `y` | `number` | 是 | — | — | 区域左上角 Y；使用指定坐标空间的单位。 |
| `width` | `number` | 是 | — | 0–32768 | 区域宽度；使用指定坐标空间的单位，必须大于 0。 |
| `height` | `number` | 是 | — | 0–32768 | 区域高度；使用指定坐标空间的单位，必须大于 0。 |
| `anchor` | `string` | 否 | `top-left` | — | anchor 空间所用的九宫格锚点。 |

**返回值：** `string` — 本轮图像资源 ID。
**错误：** 坐标空间无效、窗口引用失效、矩形越界、区域无效或截图失败时产生错误。
**执行/取消语义：** 在主进程图像操作命令边界遵循暂停状态。 本轮 worker 结束时主进程清空本轮所有图像资源。
**坐标/权限：** 坐标空间：截图区域复用 M6 screen/window/client/anchor/ratio 契约；像素输出单位为物理像素。。不新增第三方图像引擎依赖。截图依赖当前 Windows 交互桌面的可见合成画面；不保证被遮挡/最小化窗口、硬件加速表面或受保护内容的真实后台像素。不绕过 UIPI/UAC/安全桌面。每张图像最多 32,000,000 像素；单轮最多 128 个资源与 256 MiB 像素缓存。

**示例：**
```lua
local image = CaptureRegion("", "screen", 100, 80, 640, 360)
```

#### `LoadImage(path)` — 加载模板图像

- **稳定 ID：** `anjian.imaging.loadImage`
- **状态：** 当前已实现；自动化 API 契约测试覆盖，Windows 桌面实机验收待完成。
- **说明：** 从本地文件加载图像并复制到资源池；PNG 透明像素可在找图时作为通配区域。动图仅载入当前帧，不播放动画。

| 参数 | Lua 类型 | 必填 | 默认值 | 范围/单位/选项 | 说明 |
|---|---|---|---|---|---|
| `path` | `string` | 是 | — | — | 本地图像文件的绝对或相对路径。 |

**返回值：** `string` — 本轮图像资源 ID。
**错误：** 文件不存在、格式不支持、图像尺寸超过限制或资源池达到上限时产生错误。
**执行/取消语义：** 在主进程图像操作命令边界遵循暂停状态。 本轮 worker 结束时主进程清空本轮所有图像资源。
**坐标/权限：** 坐标空间：本地图像没有屏幕坐标原点；像素使用图像局部零基坐标。。不新增第三方图像引擎依赖。截图依赖当前 Windows 交互桌面的可见合成画面；不保证被遮挡/最小化窗口、硬件加速表面或受保护内容的真实后台像素。不绕过 UIPI/UAC/安全桌面。每张图像最多 32,000,000 像素；单轮最多 128 个资源与 256 MiB 像素缓存。

**示例：**
```lua
local template = LoadImage("C:\\scripts\\button.png")
```

#### `SaveScreenshot(imageId, path)` — 保存图像为 PNG

- **稳定 ID：** `anjian.imaging.saveScreenshot`
- **状态：** 当前已实现；自动化 API 契约测试覆盖，Windows 桌面实机验收待完成。
- **说明：** 将图像资源写为 PNG；目标扩展名必须为 .png。若目标文件已存在，会覆盖该文件。

| 参数 | Lua 类型 | 必填 | 默认值 | 范围/单位/选项 | 说明 |
|---|---|---|---|---|---|
| `imageId` | `string` | 是 | — | — | 当前运行内有效的图像资源 ID。 |
| `path` | `string` | 是 | — | — | 输出 PNG 文件路径；扩展名必须为 .png。 |

**返回值：** `boolean` — 成功保存返回 true。
**错误：** 资源失效、路径无效、文件写入失败或扩展名不是 .png 时产生错误。
**执行/取消语义：** 在主进程图像操作命令边界遵循暂停状态。 本轮 worker 结束时主进程清空本轮所有图像资源。
**坐标/权限：** 坐标空间：写入图像资源的局部像素；不会改变 MoveTo 的 screen 物理像素契约。。不新增第三方图像引擎依赖。截图依赖当前 Windows 交互桌面的可见合成画面；不保证被遮挡/最小化窗口、硬件加速表面或受保护内容的真实后台像素。不绕过 UIPI/UAC/安全桌面。每张图像最多 32,000,000 像素；单轮最多 128 个资源与 256 MiB 像素缓存。

**示例：**
```lua
SaveScreenshot(screen, "C:\\scripts\\screen.png")
```

#### `ReleaseImage(imageId)` — 释放图像资源

- **稳定 ID：** `anjian.imaging.releaseImage`
- **状态：** 当前已实现；自动化 API 契约测试覆盖，Windows 桌面实机验收待完成。
- **说明：** 释放指定的本轮图像资源并立即归还资源池额度；未知或已释放的 ID 返回 false。每轮脚本结束时本轮资源池也会清空。

| 参数 | Lua 类型 | 必填 | 默认值 | 范围/单位/选项 | 说明 |
|---|---|---|---|---|---|
| `imageId` | `string` | 是 | — | — | 要释放的图像资源 ID。 |

**返回值：** `boolean` — 首次释放成功返回 true；不存在的资源返回 false。
**错误：** 图像 ID 为空时产生错误。
**执行/取消语义：** 在主进程图像操作命令边界遵循暂停状态。 本轮 worker 结束时主进程清空本轮所有图像资源。
**坐标/权限：** 坐标空间：资源生命周期为单次运行轮次；资源 ID 不可跨轮次保存复用。。不新增第三方图像引擎依赖。截图依赖当前 Windows 交互桌面的可见合成画面；不保证被遮挡/最小化窗口、硬件加速表面或受保护内容的真实后台像素。不绕过 UIPI/UAC/安全桌面。每张图像最多 32,000,000 像素；单轮最多 128 个资源与 256 MiB 像素缓存。

**示例：**
```lua
ReleaseImage(template)
```

#### `GetImageSize(imageId)` — 读取图像尺寸

- **稳定 ID：** `anjian.imaging.getImageSize`
- **状态：** 当前已实现；自动化 API 契约测试覆盖，Windows 桌面实机验收待完成。
- **说明：** 返回 width、height、hasScreenCoordinates、screenOriginX 与 screenOriginY；从文件加载的图像没有有效屏幕原点。

| 参数 | Lua 类型 | 必填 | 默认值 | 范围/单位/选项 | 说明 |
|---|---|---|---|---|---|
| `imageId` | `string` | 是 | — | — | 当前运行内有效的图像资源 ID。 |

**返回值：** `table` — 包含尺寸与屏幕原点元数据的表。
**错误：** 资源失效或已释放时产生错误。
**执行/取消语义：** 在主进程图像操作命令边界遵循暂停状态。 本轮 worker 结束时主进程清空本轮所有图像资源。
**坐标/权限：** 坐标空间：宽高为像素；文件图像的屏幕原点标记为 false。。不新增第三方图像引擎依赖。截图依赖当前 Windows 交互桌面的可见合成画面；不保证被遮挡/最小化窗口、硬件加速表面或受保护内容的真实后台像素。不绕过 UIPI/UAC/安全桌面。每张图像最多 32,000,000 像素；单轮最多 128 个资源与 256 MiB 像素缓存。

**示例：**
```lua
local size = GetImageSize(screen)
```

#### `CropImage(imageId, x, y, width, height)` — 裁剪图像

- **稳定 ID：** `anjian.imaging.cropImage`
- **状态：** 当前已实现；自动化 API 契约测试覆盖，Windows 桌面实机验收待完成。
- **说明：** 按图像局部零基像素裁剪并创建新的独立资源；原资源保持不变，截图的屏幕原点会按裁剪偏移。

| 参数 | Lua 类型 | 必填 | 默认值 | 范围/单位/选项 | 说明 |
|---|---|---|---|---|---|
| `imageId` | `string` | 是 | — | — | 源图像资源 ID。 |
| `x` | `integer` | 是 | — | 0–32767 像素 | 裁剪起点 X。 |
| `y` | `integer` | 是 | — | 0–32767 像素 | 裁剪起点 Y。 |
| `width` | `integer` | 是 | — | 1–32768 像素 | 裁剪宽度。 |
| `height` | `integer` | 是 | — | 1–32768 像素 | 裁剪高度。 |

**返回值：** `string` — 新裁剪图像的资源 ID。
**错误：** 资源失效、裁剪区域越界或资源池容量不足时产生错误。
**执行/取消语义：** 在主进程图像操作命令边界遵循暂停状态。 本轮 worker 结束时主进程清空本轮所有图像资源。
**坐标/权限：** 坐标空间：图像局部零基像素坐标；截图裁剪结果会保留正确屏幕原点。。不新增第三方图像引擎依赖。截图依赖当前 Windows 交互桌面的可见合成画面；不保证被遮挡/最小化窗口、硬件加速表面或受保护内容的真实后台像素。不绕过 UIPI/UAC/安全桌面。每张图像最多 32,000,000 像素；单轮最多 128 个资源与 256 MiB 像素缓存。

**示例：**
```lua
local crop = CropImage(screen, 10, 10, 200, 120)
```

#### `GetPixelColor(imageId, x, y)` — 读取 RGB 像素颜色

- **稳定 ID：** `anjian.imaging.getPixelColor`
- **状态：** 当前已实现；自动化 API 契约测试覆盖，Windows 桌面实机验收待完成。
- **说明：** 返回图像局部零基坐标处的 0xRRGGBB 整数，不包含 alpha 通道。

| 参数 | Lua 类型 | 必填 | 默认值 | 范围/单位/选项 | 说明 |
|---|---|---|---|---|---|
| `imageId` | `string` | 是 | — | — | 图像资源 ID。 |
| `x` | `integer` | 是 | — | 0–32767 像素 | 零基像素 X。 |
| `y` | `integer` | 是 | — | 0–32767 像素 | 零基像素 Y。 |

**返回值：** `integer` — 0 到 16777215 的 RGB 数值。
**错误：** 资源失效、坐标越界或参数不合法时产生错误。
**执行/取消语义：** 在主进程图像操作命令边界遵循暂停状态。 本轮 worker 结束时主进程清空本轮所有图像资源。
**坐标/权限：** 坐标空间：图像局部零基像素坐标。。不新增第三方图像引擎依赖。截图依赖当前 Windows 交互桌面的可见合成画面；不保证被遮挡/最小化窗口、硬件加速表面或受保护内容的真实后台像素。不绕过 UIPI/UAC/安全桌面。每张图像最多 32,000,000 像素；单轮最多 128 个资源与 256 MiB 像素缓存。

**示例：**
```lua
local color = GetPixelColor(screen, 10, 20)
```

#### `CompareColor(imageId, x, y, color, tolerance?)` — 比较像素颜色

- **稳定 ID：** `anjian.imaging.compareColor`
- **状态：** 当前已实现；自动化 API 契约测试覆盖，Windows 桌面实机验收待完成。
- **说明：** 将指定像素与 0xRRGGBB 颜色比较；tolerance 是每个 RGB 通道允许的最大绝对差，默认 0。

| 参数 | Lua 类型 | 必填 | 默认值 | 范围/单位/选项 | 说明 |
|---|---|---|---|---|---|
| `imageId` | `string` | 是 | — | — | 图像资源 ID。 |
| `x` | `integer` | 是 | — | 0–32767 像素 | 零基像素 X。 |
| `y` | `integer` | 是 | — | 0–32767 像素 | 零基像素 Y。 |
| `color` | `integer` | 是 | — | 0–16777215 | 目标 RGB 颜色。 |
| `tolerance` | `integer` | 否 | `0` | 0–255 | 每个 RGB 通道最大绝对差。 |

**返回值：** `boolean` — 所有 RGB 通道差值均在容差内返回 true。
**错误：** 资源失效、坐标越界或颜色/容差不合法时产生错误。
**执行/取消语义：** 在主进程图像操作命令边界遵循暂停状态。 本轮 worker 结束时主进程清空本轮所有图像资源。
**坐标/权限：** 坐标空间：图像局部零基像素坐标；颜色参数为 0xRRGGBB。。不新增第三方图像引擎依赖。截图依赖当前 Windows 交互桌面的可见合成画面；不保证被遮挡/最小化窗口、硬件加速表面或受保护内容的真实后台像素。不绕过 UIPI/UAC/安全桌面。每张图像最多 32,000,000 像素；单轮最多 128 个资源与 256 MiB 像素缓存。

**示例：**
```lua
CompareColor(screen, 10, 20, 0xFF0000, 4)
```

#### `FindColor(imageId, color, tolerance?, left?, top?, width?, height?, step?)` — 查找单点颜色

- **稳定 ID：** `anjian.imaging.findColor`
- **状态：** 当前已实现；自动化 API 契约测试覆盖，Windows 桌面实机验收待完成。
- **说明：** 按从上到下、从左到右顺序查找首个符合颜色/容差的像素。无命中返回 nil；区域宽或高为 0 表示延伸至图像边缘。结果含局部 x/y；源图像是截图时另含 screenX/screenY。

| 参数 | Lua 类型 | 必填 | 默认值 | 范围/单位/选项 | 说明 |
|---|---|---|---|---|---|
| `imageId` | `string` | 是 | — | — | 图像资源 ID。 |
| `color` | `integer` | 是 | — | 0–16777215 | 目标 RGB 颜色。 |
| `tolerance` | `integer` | 否 | `0` | 0–255 | RGB 各通道容差。 |
| `left` | `integer` | 否 | `0` | 0–32767 像素 | 局部区域左边；0 为图像左边。 |
| `top` | `integer` | 否 | `0` | 0–32767 像素 | 局部区域上边；0 为图像上边。 |
| `width` | `integer` | 否 | `0` | 0–32768 像素 | 区域宽度；0 表示到图像右边界。 |
| `height` | `integer` | 否 | `0` | 0–32768 像素 | 区域高度；0 表示到图像下边界。 |
| `step` | `integer` | 否 | `1` | 1–64 像素 | 候选采样步长。 |

**返回值：** `table\|nil` — 命中位置表或未命中 nil。
**错误：** 资源失效、区域越界或参数不合法时产生错误。
**执行/取消语义：** 在主进程图像操作命令边界遵循暂停状态。 本轮 worker 结束时主进程清空本轮所有图像资源。
**坐标/权限：** 坐标空间：搜索区域为图像局部零基像素；截图命中另含 screenX/screenY。。不新增第三方图像引擎依赖。截图依赖当前 Windows 交互桌面的可见合成画面；不保证被遮挡/最小化窗口、硬件加速表面或受保护内容的真实后台像素。不绕过 UIPI/UAC/安全桌面。每张图像最多 32,000,000 像素；单轮最多 128 个资源与 256 MiB 像素缓存。

**示例：**
```lua
local hit = FindColor(screen, 0xFF0000, 5)
```

#### `FindMultiColor(imageId, color, pattern, tolerance?, left?, top?, width?, height?, step?)` — 多点颜色查找

- **稳定 ID：** `anjian.imaging.findMultiColor`
- **状态：** 当前已实现；自动化 API 契约测试覆盖，Windows 桌面实机验收待完成。
- **说明：** 以 base color 为基准验证多个相对偏移点。pattern 格式为 dx,dy,RRGGBB;dx,dy,RRGGBB，颜色可带 0x 或 # 前缀，最多 256 个点。无命中返回 nil。

| 参数 | Lua 类型 | 必填 | 默认值 | 范围/单位/选项 | 说明 |
|---|---|---|---|---|---|
| `imageId` | `string` | 是 | — | — | 图像资源 ID。 |
| `color` | `integer` | 是 | — | 0–16777215 | 基准 RGB 颜色。 |
| `pattern` | `string` | 是 | — | — | 分号分隔的 dx,dy,RRGGBB 列表。 |
| `tolerance` | `integer` | 否 | `0` | 0–255 | RGB 各通道容差。 |
| `left` | `integer` | 否 | `0` | 0–32767 像素 | 局部区域左边；0 为图像左边。 |
| `top` | `integer` | 否 | `0` | 0–32767 像素 | 局部区域上边；0 为图像上边。 |
| `width` | `integer` | 否 | `0` | 0–32768 像素 | 区域宽度；0 表示到图像右边界。 |
| `height` | `integer` | 否 | `0` | 0–32768 像素 | 区域高度；0 表示到图像下边界。 |
| `step` | `integer` | 否 | `1` | 1–64 像素 | 候选采样步长。 |

**返回值：** `table\|nil` — 首个命中位置表或未命中 nil。
**错误：** 模式格式错误、点数超限、资源失效或区域越界时产生错误。
**执行/取消语义：** 在主进程图像操作命令边界遵循暂停状态。 本轮 worker 结束时主进程清空本轮所有图像资源。
**坐标/权限：** 坐标空间：偏移和区域使用图像局部零基像素；命中截图另含 screenX/screenY。。不新增第三方图像引擎依赖。截图依赖当前 Windows 交互桌面的可见合成画面；不保证被遮挡/最小化窗口、硬件加速表面或受保护内容的真实后台像素。不绕过 UIPI/UAC/安全桌面。每张图像最多 32,000,000 像素；单轮最多 128 个资源与 256 MiB 像素缓存。

**示例：**
```lua
local hit = FindMultiColor(screen, 0x112233, "1,0,FF0000;0,1,00FF00", 2)
```

#### `CountColors(imageId, color, tolerance?, left?, top?, width?, height?, step?)` — 统计区域颜色

- **稳定 ID：** `anjian.imaging.countColors`
- **状态：** 当前已实现；自动化 API 契约测试覆盖，Windows 桌面实机验收待完成。
- **说明：** 统计指定区域命中颜色的采样像素数；step 默认 1，增大 step 会稀疏采样。

| 参数 | Lua 类型 | 必填 | 默认值 | 范围/单位/选项 | 说明 |
|---|---|---|---|---|---|
| `imageId` | `string` | 是 | — | — | 图像资源 ID。 |
| `color` | `integer` | 是 | — | 0–16777215 | 目标 RGB 颜色。 |
| `tolerance` | `integer` | 否 | `0` | 0–255 | RGB 各通道容差。 |
| `left` | `integer` | 否 | `0` | 0–32767 像素 | 局部区域左边；0 为图像左边。 |
| `top` | `integer` | 否 | `0` | 0–32767 像素 | 局部区域上边；0 为图像上边。 |
| `width` | `integer` | 否 | `0` | 0–32768 像素 | 区域宽度；0 表示到图像右边界。 |
| `height` | `integer` | 否 | `0` | 0–32768 像素 | 区域高度；0 表示到图像下边界。 |
| `step` | `integer` | 否 | `1` | 1–64 像素 | 候选采样步长。 |

**返回值：** `integer` — 匹配的采样像素数量。
**错误：** 资源失效、区域越界或参数不合法时产生错误。
**执行/取消语义：** 在主进程图像操作命令边界遵循暂停状态。 本轮 worker 结束时主进程清空本轮所有图像资源。
**坐标/权限：** 坐标空间：搜索区域为图像局部零基像素。。不新增第三方图像引擎依赖。截图依赖当前 Windows 交互桌面的可见合成画面；不保证被遮挡/最小化窗口、硬件加速表面或受保护内容的真实后台像素。不绕过 UIPI/UAC/安全桌面。每张图像最多 32,000,000 像素；单轮最多 128 个资源与 256 MiB 像素缓存。

**示例：**
```lua
local count = CountColors(screen, 0x00FF00, 0, 0, 0, 640, 480)
```

#### `FindImage(imageId, templateId, similarity?, tolerance?, left?, top?, width?, height?, step?, grayscale?, scale?)` — 模板找图

- **稳定 ID：** `anjian.imaging.findImage`
- **状态：** 当前已实现；自动化 API 契约测试覆盖，Windows 桌面实机验收待完成。
- **说明：** 在源图像区域查找模板首个命中；透明模板像素作为通配点。similarity 为 0–1 的匹配比例，未命中返回 nil。单次调用最多执行 5000 万次模板像素比较，防止长时间无界循环。 支持灰度比较和 0.25–4 倍最近邻缩放，默认仍是原尺寸 RGB。

| 参数 | Lua 类型 | 必填 | 默认值 | 范围/单位/选项 | 说明 |
|---|---|---|---|---|---|
| `imageId` | `string` | 是 | — | — | 被搜索图像资源 ID。 |
| `templateId` | `string` | 是 | — | — | 模板图像资源 ID。 |
| `similarity` | `number` | 否 | `0.95` | 0–1 | 所需相似度比例。 |
| `tolerance` | `integer` | 否 | `0` | 0–255 | RGB 每个通道允许的最大绝对差。 |
| `left` | `integer` | 否 | `0` | 0–32767 像素 | 局部区域左边；0 为图像左边。 |
| `top` | `integer` | 否 | `0` | 0–32767 像素 | 局部区域上边；0 为图像上边。 |
| `width` | `integer` | 否 | `0` | 0–32768 像素 | 区域宽度；0 表示到图像右边界。 |
| `height` | `integer` | 否 | `0` | 0–32768 像素 | 区域高度；0 表示到图像下边界。 |
| `step` | `integer` | 否 | `1` | 1–64 像素 | 候选采样步长。 |
| `grayscale` | `boolean` | 否 | `false` | — | 是否按亮度灰度进行模板匹配。 |
| `scale` | `number` | 否 | `1` | 0.25–4 | 模板缩放比例；1 为原尺寸，采用最近邻采样。 |

**返回值：** `table\|nil` — 命中位置/相似度与可用的 screenX/screenY；未命中返回 nil。
**错误：** 模板全透明、资源失效、区域越界或达到像素比较预算时产生错误。
**执行/取消语义：** 在主进程图像操作命令边界遵循暂停状态。 本轮 worker 结束时主进程清空本轮所有图像资源。
**坐标/权限：** 坐标空间：搜索区域与结果为图像局部零基像素；截图命中另含 screenX/screenY。。不新增第三方图像引擎依赖。截图依赖当前 Windows 交互桌面的可见合成画面；不保证被遮挡/最小化窗口、硬件加速表面或受保护内容的真实后台像素。不绕过 UIPI/UAC/安全桌面。每张图像最多 32,000,000 像素；单轮最多 128 个资源与 256 MiB 像素缓存。

**示例：**
```lua
local hit = FindImage(screen, template, 0.95, 4, 0, 0, 0, 0, 1, true, 1.5)
```

#### `FindImages(imageId, templateId, similarity?, tolerance?, maxResults?, left?, top?, width?, height?, step?, grayscale?, scale?)` — 查找多个模板位置

- **稳定 ID：** `anjian.imaging.findImages`
- **状态：** 当前已实现；自动化 API 契约测试覆盖，Windows 桌面实机验收待完成。
- **说明：** 返回按从上到下、从左到右排序且互不重叠的命中数组。maxResults 为 1–1000；未命中返回空数组。 支持灰度比较和 0.25–4 倍最近邻缩放，默认仍是原尺寸 RGB。

| 参数 | Lua 类型 | 必填 | 默认值 | 范围/单位/选项 | 说明 |
|---|---|---|---|---|---|
| `imageId` | `string` | 是 | — | — | 被搜索图像资源 ID。 |
| `templateId` | `string` | 是 | — | — | 模板图像资源 ID。 |
| `similarity` | `number` | 否 | `0.95` | 0–1 | 所需相似度比例。 |
| `tolerance` | `integer` | 否 | `0` | 0–255 | RGB 每个通道允许的最大绝对差。 |
| `maxResults` | `integer` | 否 | `100` | 1–1000 | 最大返回数。 |
| `left` | `integer` | 否 | `0` | 0–32767 像素 | 局部区域左边；0 为图像左边。 |
| `top` | `integer` | 否 | `0` | 0–32767 像素 | 局部区域上边；0 为图像上边。 |
| `width` | `integer` | 否 | `0` | 0–32768 像素 | 区域宽度；0 表示到图像右边界。 |
| `height` | `integer` | 否 | `0` | 0–32768 像素 | 区域高度；0 表示到图像下边界。 |
| `step` | `integer` | 否 | `1` | 1–64 像素 | 候选采样步长。 |
| `grayscale` | `boolean` | 否 | `false` | — | 是否按亮度灰度进行模板匹配。 |
| `scale` | `number` | 否 | `1` | 0.25–4 | 模板缩放比例；1 为原尺寸，采用最近邻采样。 |

**返回值：** `table` — Lua 数组形式的命中列表；没有匹配时为空表。
**错误：** 模板全透明、资源失效、区域越界或达到像素比较预算时产生错误。
**执行/取消语义：** 在主进程图像操作命令边界遵循暂停状态。 本轮 worker 结束时主进程清空本轮所有图像资源。
**坐标/权限：** 坐标空间：搜索区域与结果为图像局部零基像素；截图命中另含 screenX/screenY。。不新增第三方图像引擎依赖。截图依赖当前 Windows 交互桌面的可见合成画面；不保证被遮挡/最小化窗口、硬件加速表面或受保护内容的真实后台像素。不绕过 UIPI/UAC/安全桌面。每张图像最多 32,000,000 像素；单轮最多 128 个资源与 256 MiB 像素缓存。

**示例：**
```lua
local hits = FindImages(screen, template, 0.95, 0, 20, 0, 0, 0, 0, 1, true, 1)
```

#### `WaitImage(templateId, similarity?, tolerance?, timeoutMs?, intervalMs?, grayscale?, scale?, stableCount?)` — 等待模板出现

- **稳定 ID：** `anjian.imaging.waitImage`
- **状态：** 当前已实现；自动化 API 契约测试覆盖，Windows 桌面实机验收待完成。
- **说明：** 循环截取当前虚拟桌面并搜索模板；首次命中返回位置表，超时返回 nil。Stop 取消轮询，Pause 会暂停后续轮询。 可指定灰度/缩放，并要求连续多帧命中以降低单帧误判。

| 参数 | Lua 类型 | 必填 | 默认值 | 范围/单位/选项 | 说明 |
|---|---|---|---|---|---|
| `templateId` | `string` | 是 | — | — | 模板图像资源 ID。 |
| `similarity` | `number` | 否 | `0.95` | 0–1 | 所需相似度比例。 |
| `tolerance` | `integer` | 否 | `0` | 0–255 | RGB 每个通道允许的最大绝对差。 |
| `timeoutMs` | `integer` | 否 | `5000` | 0–600000 毫秒 | 最大等待时长。 |
| `intervalMs` | `integer` | 否 | `100` | 10–5000 毫秒 | 两次检测间隔。 |
| `grayscale` | `boolean` | 否 | `false` | — | 是否按亮度灰度进行模板匹配。 |
| `scale` | `number` | 否 | `1` | 0.25–4 | 模板缩放比例；1 为原尺寸，采用最近邻采样。 |
| `stableCount` | `integer` | 否 | `1` | 1–20 次 | 需连续满足条件的截图次数，用于抑制单帧误判。 |

**返回值：** `table\|nil` — 首次命中位置或超时 nil。
**错误：** 模板失效、截图失败、图像过大或搜索达到像素比较预算时产生错误。
**执行/取消语义：** 每轮轮询前检查暂停状态；暂停时等待恢复。 Stop 取消轮询并终止当前调用；不把取消伪装成超时。
**坐标/权限：** 坐标空间：搜索范围为当前虚拟桌面；返回图像局部与 screen 绝对坐标。。不新增第三方图像引擎依赖。截图依赖当前 Windows 交互桌面的可见合成画面；不保证被遮挡/最小化窗口、硬件加速表面或受保护内容的真实后台像素。不绕过 UIPI/UAC/安全桌面。每张图像最多 32,000,000 像素；单轮最多 128 个资源与 256 MiB 像素缓存。

**示例：**
```lua
local hit = WaitImage(template, 0.95, 0, 5000, 100, false, 1, 2)
```

#### `WaitImageGone(templateId, similarity?, tolerance?, timeoutMs?, intervalMs?, grayscale?, scale?, stableCount?)` — 等待模板消失

- **稳定 ID：** `anjian.imaging.waitImageGone`
- **状态：** 当前已实现；自动化 API 契约测试覆盖，Windows 桌面实机验收待完成。
- **说明：** 持续截取虚拟桌面；模板消失返回 true，超时时模板仍存在返回 false。Stop 取消轮询。 可指定灰度/缩放，并要求连续多帧未命中以降低单帧误判。

| 参数 | Lua 类型 | 必填 | 默认值 | 范围/单位/选项 | 说明 |
|---|---|---|---|---|---|
| `templateId` | `string` | 是 | — | — | 模板图像资源 ID。 |
| `similarity` | `number` | 否 | `0.95` | 0–1 | 所需相似度比例。 |
| `tolerance` | `integer` | 否 | `0` | 0–255 | RGB 每个通道允许的最大绝对差。 |
| `timeoutMs` | `integer` | 否 | `5000` | 0–600000 毫秒 | 最大等待时长。 |
| `intervalMs` | `integer` | 否 | `100` | 10–5000 毫秒 | 两次检测间隔。 |
| `grayscale` | `boolean` | 否 | `false` | — | 是否按亮度灰度进行模板匹配。 |
| `scale` | `number` | 否 | `1` | 0.25–4 | 模板缩放比例；1 为原尺寸，采用最近邻采样。 |
| `stableCount` | `integer` | 否 | `1` | 1–20 次 | 需连续满足条件的截图次数，用于抑制单帧误判。 |

**返回值：** `boolean` — 模板消失返回 true；超时仍存在返回 false。
**错误：** 模板失效、截图失败、图像过大或搜索达到像素比较预算时产生错误。
**执行/取消语义：** 每轮轮询前检查暂停状态；暂停时等待恢复。 Stop 取消轮询并终止当前调用；不把取消伪装成超时。
**坐标/权限：** 坐标空间：搜索范围为当前虚拟桌面。。不新增第三方图像引擎依赖。截图依赖当前 Windows 交互桌面的可见合成画面；不保证被遮挡/最小化窗口、硬件加速表面或受保护内容的真实后台像素。不绕过 UIPI/UAC/安全桌面。每张图像最多 32,000,000 像素；单轮最多 128 个资源与 256 MiB 像素缓存。

**示例：**
```lua
if WaitImageGone(template, 0.95, 0, 5000, 100, false, 1, 2) then Log("目标已稳定消失") end
```

#### `WaitColor(color, tolerance?, timeoutMs?, intervalMs?, stableCount?)` — 等待屏幕颜色出现

- **稳定 ID：** `anjian.imaging.waitColor`
- **状态：** 当前已实现；自动化 API 契约测试覆盖，Windows 桌面实机验收待完成。
- **说明：** 持续截取虚拟桌面；首次找到指定 RGB 颜色返回命中位置，超时返回 nil。 可指定连续命中次数以降低单帧误判。

| 参数 | Lua 类型 | 必填 | 默认值 | 范围/单位/选项 | 说明 |
|---|---|---|---|---|---|
| `color` | `integer` | 是 | — | 0–16777215 | 目标 RGB 颜色。 |
| `tolerance` | `integer` | 否 | `0` | 0–255 | RGB 每个通道允许的最大绝对差。 |
| `timeoutMs` | `integer` | 否 | `5000` | 0–600000 毫秒 | 最大等待时长。 |
| `intervalMs` | `integer` | 否 | `100` | 10–5000 毫秒 | 两次检测之间的间隔。 |
| `stableCount` | `integer` | 否 | `1` | 1–20 次 | 需连续满足条件的截图次数，用于抑制单帧误判。 |

**返回值：** `table\|nil` — 首次命中位置或超时 nil。
**错误：** 截图失败、图像过大或参数无效时产生错误。
**执行/取消语义：** 每轮轮询前检查暂停状态；暂停时等待恢复。 Stop 取消轮询并终止当前调用；不把取消伪装成超时。
**坐标/权限：** 坐标空间：搜索范围为当前虚拟桌面；结果包含 screenX/screenY。。不新增第三方图像引擎依赖。截图依赖当前 Windows 交互桌面的可见合成画面；不保证被遮挡/最小化窗口、硬件加速表面或受保护内容的真实后台像素。不绕过 UIPI/UAC/安全桌面。每张图像最多 32,000,000 像素；单轮最多 128 个资源与 256 MiB 像素缓存。

**示例：**
```lua
local hit = WaitColor(0x00FF00, 8, 3000, 100, 2)
```

#### `WaitColorGone(color, tolerance?, timeoutMs?, intervalMs?, stableCount?)` — 等待屏幕颜色消失

- **稳定 ID：** `anjian.imaging.waitColorGone`
- **状态：** 当前已实现；自动化 API 契约测试覆盖，Windows 桌面实机验收待完成。
- **说明：** 持续截取虚拟桌面；指定颜色不再出现返回 true，超时仍存在返回 false。 可指定连续未命中次数以降低单帧误判。

| 参数 | Lua 类型 | 必填 | 默认值 | 范围/单位/选项 | 说明 |
|---|---|---|---|---|---|
| `color` | `integer` | 是 | — | 0–16777215 | 目标 RGB 颜色。 |
| `tolerance` | `integer` | 否 | `0` | 0–255 | RGB 每个通道允许的最大绝对差。 |
| `timeoutMs` | `integer` | 否 | `5000` | 0–600000 毫秒 | 最大等待时长。 |
| `intervalMs` | `integer` | 否 | `100` | 10–5000 毫秒 | 两次检测之间的间隔。 |
| `stableCount` | `integer` | 否 | `1` | 1–20 次 | 需连续满足条件的截图次数，用于抑制单帧误判。 |

**返回值：** `boolean` — 颜色消失返回 true；超时仍存在返回 false。
**错误：** 截图失败、图像过大或参数无效时产生错误。
**执行/取消语义：** 每轮轮询前检查暂停状态；暂停时等待恢复。 Stop 取消轮询并终止当前调用；不把取消伪装成超时。
**坐标/权限：** 坐标空间：搜索范围为当前虚拟桌面。。不新增第三方图像引擎依赖。截图依赖当前 Windows 交互桌面的可见合成画面；不保证被遮挡/最小化窗口、硬件加速表面或受保护内容的真实后台像素。不绕过 UIPI/UAC/安全桌面。每张图像最多 32,000,000 像素；单轮最多 128 个资源与 256 MiB 像素缓存。

**示例：**
```lua
if WaitColorGone(0x00FF00, 8, 3000, 100, 2) then Log("颜色已稳定消失") end
```

#### `RgbToHex(red, green, blue)` — RGB 转十六进制

- **稳定 ID：** `anjian.imaging.rgbToHex`
- **状态：** 当前已实现；自动化 API 契约测试覆盖，Windows 桌面实机验收待完成。
- **说明：** 将三个 0–255 RGB 通道转为 #RRGGBB。

| 参数 | Lua 类型 | 必填 | 默认值 | 范围/单位/选项 | 说明 |
|---|---|---|---|---|---|
| `red` | `integer` | 是 | — | 0–255 | 红色通道。 |
| `green` | `integer` | 是 | — | 0–255 | 绿色通道。 |
| `blue` | `integer` | 是 | — | 0–255 | 蓝色通道。 |

**返回值：** `string` — #RRGGBB 大写十六进制颜色。
**错误：** 参数、颜色或图像资源不合法时产生 Lua 错误。
**执行/取消语义：** 在主进程图像操作命令边界遵循暂停状态。 Stop 可取消颜色块扫描；图像资源在本轮结束时统一释放。
**坐标/权限：** 坐标空间：RGB 使用 0xRRGGBB；FindColorBlocks 返回图像局部零基坐标，截图还提供 screenX/screenY。。不新增第三方图像引擎依赖；截图依赖当前 Windows 交互桌面的可见合成画面。

**示例：**
```lua
local hex = RgbToHex(255, 128, 0) -- #FF8000
```

#### `HexToRgb(hex)` — 十六进制转 RGB

- **稳定 ID：** `anjian.imaging.hexToRgb`
- **状态：** 当前已实现；自动化 API 契约测试覆盖，Windows 桌面实机验收待完成。
- **说明：** 将六位十六进制颜色解析为 RGB 通道。

| 参数 | Lua 类型 | 必填 | 默认值 | 范围/单位/选项 | 说明 |
|---|---|---|---|---|---|
| `hex` | `string` | 是 | — | — | RRGGBB、#RRGGBB 或 0xRRGGBB。 |

**返回值：** `table` — 包含 red、green、blue、hex 和 color 字段的表。
**错误：** 参数、颜色或图像资源不合法时产生 Lua 错误。
**执行/取消语义：** 在主进程图像操作命令边界遵循暂停状态。 Stop 可取消颜色块扫描；图像资源在本轮结束时统一释放。
**坐标/权限：** 坐标空间：RGB 使用 0xRRGGBB；FindColorBlocks 返回图像局部零基坐标，截图还提供 screenX/screenY。。不新增第三方图像引擎依赖；截图依赖当前 Windows 交互桌面的可见合成画面。

**示例：**
```lua
local rgb = HexToRgb('#FF8000') -- rgb.red=255
```

#### `ColorDifference(first, second)` — 计算颜色差值

- **稳定 ID：** `anjian.imaging.colorDifference`
- **状态：** 当前已实现；自动化 API 契约测试覆盖，Windows 桌面实机验收待完成。
- **说明：** 计算两种颜色的各通道绝对差及 Max/Manhattan/Euclidean 距离。

| 参数 | Lua 类型 | 必填 | 默认值 | 范围/单位/选项 | 说明 |
|---|---|---|---|---|---|
| `first` | `integer` | 是 | — | 0–16777215 | 第一个 0xRRGGBB 颜色。 |
| `second` | `integer` | 是 | — | 0–16777215 | 第二个 0xRRGGBB 颜色。 |

**返回值：** `table` — 包含 RGB 各通道绝对差、maxChannel、manhattan 与 euclidean。
**错误：** 参数、颜色或图像资源不合法时产生 Lua 错误。
**执行/取消语义：** 在主进程图像操作命令边界遵循暂停状态。 Stop 可取消颜色块扫描；图像资源在本轮结束时统一释放。
**坐标/权限：** 坐标空间：RGB 使用 0xRRGGBB；FindColorBlocks 返回图像局部零基坐标，截图还提供 screenX/screenY。。不新增第三方图像引擎依赖；截图依赖当前 Windows 交互桌面的可见合成画面。

**示例：**
```lua
local d = ColorDifference(0x102030, 0x132638)
```

#### `FindColorBlocks(imageId, color, tolerance?, minPixels?, left?, top?, width?, height?, step?, maxResults?)` — 识别颜色块

- **稳定 ID：** `anjian.imaging.findColorBlocks`
- **状态：** 当前已实现；自动化 API 契约测试覆盖，Windows 桌面实机验收待完成。
- **说明：** 用八邻域连通分量识别颜色区域，按采样点数从大到小返回；最大 100 个结果和 400 万采样点。

| 参数 | Lua 类型 | 必填 | 默认值 | 范围/单位/选项 | 说明 |
|---|---|---|---|---|---|
| `imageId` | `string` | 是 | — | — | 图像资源 ID。 |
| `color` | `integer` | 是 | — | 0–16777215 | 目标 0xRRGGBB。 |
| `tolerance` | `integer` | 否 | `0` | 0–255 | RGB 各通道最大差。 |
| `minPixels` | `integer` | 否 | `2` | 1–1000000 | 最小连通采样点数。 |
| `left` | `integer` | 否 | `0` | 0–32767 | 局部区域左边。 |
| `top` | `integer` | 否 | `0` | 0–32767 | 局部区域上边。 |
| `width` | `integer` | 否 | `0` | 0–32768 | 区域宽；0 到图像右边界。 |
| `height` | `integer` | 否 | `0` | 0–32768 | 区域高；0 到图像下边界。 |
| `step` | `integer` | 否 | `1` | 1–64 | 采样步长。 |
| `maxResults` | `integer` | 否 | `100` | 1–100 | 最大返回块数，按面积降序。 |

**返回值：** `table` — 颜色块数组；每项包含 x、y、width、height、pixelCount、centerX/centerY 以及可用 screenX/screenY。
**错误：** 参数、颜色或图像资源不合法时产生 Lua 错误。
**执行/取消语义：** 在主进程图像操作命令边界遵循暂停状态。 Stop 可取消颜色块扫描；图像资源在本轮结束时统一释放。
**坐标/权限：** 坐标空间：RGB 使用 0xRRGGBB；FindColorBlocks 返回图像局部零基坐标，截图还提供 screenX/screenY。。不新增第三方图像引擎依赖；截图依赖当前 Windows 交互桌面的可见合成画面。

**示例：**
```lua
local blocks = FindColorBlocks(screen, 0xFF0000, 8, 4, 0, 0, 800, 600, 2, 20)
```

### 2.7 文字识别

#### `OCR(imageId, language?, minConfidence?)` — 识别图像文字

- **稳定 ID：** `anjian.ocr.recognize`
- **状态：** 当前已实现；自动化 API 契约测试覆盖，Windows 桌面实机验收待完成。
- **说明：** 对已加载/已捕获的图像进行离线文字检测与识别，按阅读顺序返回全文和逐行结果。每行包含 0–1 置信度、图像局部矩形及多边形；对桌面截图另提供 screenX/screenY。默认简体中文。模型缺失时明确报错，不在脚本执行期间自动联网下载。

| 参数 | Lua 类型 | 必填 | 默认值 | 范围/单位/选项 | 说明 |
|---|---|---|---|---|---|
| `imageId` | `string` | 是 | — | — | 图像资源 ID。 |
| `language` | `string` | 否 | `ch` | 固定值：ch、en、japan | 识别语言代码。 |
| `minConfidence` | `number` | 否 | `0` | 0–1 | 低于该值的行不返回。 |

**返回值：** `table` — 包含 text、lineCount、lines、languages、detectedLanguages、width、height、durationMs、hasScreenCoordinates；lines 为 1 起始的逐行数组，每项含 text/confidence/x/y/width/height/polygon，截图还含 screenX/screenY。
**错误：** 图像 ID 无效/已释放、语言代码不支持、模型缺失或损坏、图像解码失败及引擎识别失败时产生明确错误。
**执行/取消语义：** 识别计算在独立 Lua Worker 中执行；WaitText 在每次截图前经过主进程暂停检查点。 单次识别运行于独立 Worker；F12 停止会终止 Worker。图像资源由主进程在轮次结束时统一清理。
**坐标/权限：** 坐标空间：图像局部零基像素；截图结果同时提供虚拟桌面绝对物理像素坐标。。使用当前轮次图像资源；不提升权限。仅处理已捕获/已加载的图像字节；默认仅读取本地模型缓存，不自动联网。

**示例：**
```lua
local result = OCR(screen, "ch", 0.55)
Log(result.text)
for i, line in ipairs(result.lines) do Log(line.text .. " " .. line.confidence) end
```

#### `FindText(imageId, text, language?, minConfidence?, matchMode?)` — 查找文字

- **稳定 ID：** `anjian.ocr.find-text`
- **状态：** 当前已实现；自动化 API 契约测试覆盖，Windows 桌面实机验收待完成。
- **说明：** 在图像 OCR 结果中按阅读顺序查找第一条匹配文本。matchMode 为 contains（默认，不区分大小写子串）或 exact（不区分大小写完全匹配）；只检查达到 minConfidence 的文本行，未命中返回 nil。

| 参数 | Lua 类型 | 必填 | 默认值 | 范围/单位/选项 | 说明 |
|---|---|---|---|---|---|
| `imageId` | `string` | 是 | — | — | 图像资源 ID。 |
| `text` | `string` | 是 | — | — | 要查找的非空文本。 |
| `language` | `string` | 否 | `ch` | 固定值：ch、en、japan | 识别语言代码。 |
| `minConfidence` | `number` | 否 | `0` | 0–1 | 最低识别置信度。 |
| `matchMode` | `string` | 否 | `contains` | 固定值：contains、exact | contains 或 exact。 |

**返回值：** `table\|nil` — 命中行的 text、confidence、x/y/width/height、polygon；桌面截图还含 screenX/screenY。未命中返回 nil。
**错误：** 图像 ID 失效、文本为空、语言不支持、模型缺失/损坏或引擎识别失败时产生错误。
**执行/取消语义：** 识别计算在独立 Lua Worker 中执行；WaitText 在每次截图前经过主进程暂停检查点。 执行于独立 Worker；F12 停止终止 Worker，并由主进程清理轮次资源。
**坐标/权限：** 坐标空间：图像局部零基像素；截图结果同时提供虚拟桌面绝对物理像素坐标。。只读取调用者传入的图像资源和已安装的本地模型；不自动联网。

**示例：**
```lua
local hit = FindText(screen, "登录", "ch", 0.7, "contains")
if hit then MoveTo(hit.screenX, hit.screenY) end
```

#### `WaitText(text, timeoutMs?, intervalMs?, language?, minConfidence?, matchMode?)` — 等待文字出现

- **稳定 ID：** `anjian.ocr.wait-text`
- **状态：** 当前已实现；自动化 API 契约测试覆盖，Windows 桌面实机验收待完成。
- **说明：** 反复截取当前可见虚拟桌面并执行 OCR，直到出现匹配文本或超时。每次轮询前检查暂停状态；F12 停止终止 Worker。timeoutMs=0 仍会立即执行一次检测。默认简体中文、contains、不自动下载模型。

| 参数 | Lua 类型 | 必填 | 默认值 | 范围/单位/选项 | 说明 |
|---|---|---|---|---|---|
| `text` | `string` | 是 | — | — | 要等待的非空文本。 |
| `timeoutMs` | `integer` | 否 | `10000` | 0–600000 毫秒 | 总等待超时。 |
| `intervalMs` | `integer` | 否 | `300` | 10–5000 毫秒 | 两次截图识别间隔。 |
| `language` | `string` | 否 | `ch` | 固定值：ch、en、japan | 识别语言代码。 |
| `minConfidence` | `number` | 否 | `0` | 0–1 | 最低识别置信度。 |
| `matchMode` | `string` | 否 | `contains` | 固定值：contains、exact | contains 或 exact。 |

**返回值：** `table\|nil` — 命中行的文字、置信度、图像局部矩形、多边形与虚拟桌面绝对物理像素 screenX/screenY；超时返回 nil。
**错误：** 文本/参数无效、桌面截图失败、模型缺失/损坏或引擎识别失败时产生错误；Stop 不作为超时返回。
**执行/取消语义：** 识别计算在独立 Lua Worker 中执行；WaitText 在每次截图前经过主进程暂停检查点。 每次截图前通过主进程 checkpoint 遵循暂停；F12 终止 Worker，正在执行的原生推理随进程结束。
**坐标/权限：** 坐标空间：虚拟桌面截图坐标；返回 screenX/screenY 为绝对物理像素，允许负坐标。。截图依赖当前交互桌面可见合成画面；不承诺被遮挡/最小化窗口或受保护表面的真实后台像素。不绕过 UIPI/UAC/安全桌面。模型只从本地缓存读取。

**示例：**
```lua
local hit = WaitText("订单提交成功", 15000, 250, "ch", 0.65)
if hit then Log("识别到：" .. hit.text) end
```

### 2.8 文件与目录

#### `ReadTextFile(path)` — 读取文本文件

- **稳定 ID：** `anjian.files.read-text`
- **状态：** 当前已实现；自动化 API 契约测试覆盖，Windows 桌面实机验收待完成。
- **说明：** 读取批准根目录内的 UTF-8 文本文件；相对路径以当前脚本所在目录为基准。拒绝无效 UTF-8 和超过 4 MiB 的文件。

| 参数 | Lua 类型 | 必填 | 默认值 | 范围/单位/选项 | 说明 |
|---|---|---|---|---|---|
| `path` | `string` | 是 | — | — | 绝对路径或相对于当前脚本目录的路径。 |

**返回值：** `string` — 文件的完整 UTF-8 文本内容；不自动剥离 BOM。
**错误：** 路径未获本次运行读取授权、文件不存在、路径包含 reparse point/符号链接/多硬链接、文件超过 4 MiB 或文本不是有效 UTF-8 时产生错误。
**执行/取消语义：** 每条文件 IPC 在主进程执行前经过暂停检查点；暂停期间等待恢复。 Stop 取消当前运行 token；枚举循环会检查取消，写入在原子提交前再次检查；单次同步 I/O 受 4 MiB 上限约束。
**坐标/权限：** 坐标空间：none。Worker 在 IPC 前检查本次 Grant；主进程对每条文件 IPC 再次检查。只允许批准的读取/写入根；拒绝 reparse point、符号链接和多硬链接普通文件。这不是恶意脚本的 OS 级沙箱。

**示例：**
```lua
local text = ReadTextFile("data/config.txt")
```

#### `WriteTextFile(path, text)` — 写入文本文件

- **稳定 ID：** `anjian.files.write-text`
- **状态：** 当前已实现；自动化 API 契约测试覆盖，Windows 桌面实机验收待完成。
- **说明：** 以 UTF-8（无 BOM）写入文件；先写同目录临时文件并 flush，再原子替换目标。内容最大 4 MiB。

| 参数 | Lua 类型 | 必填 | 默认值 | 范围/单位/选项 | 说明 |
|---|---|---|---|---|---|
| `path` | `string` | 是 | — | — | 绝对路径或相对于当前脚本目录的路径。 |
| `text` | `string` | 是 | — | — | 要写入的文本内容。 |

**返回值：** `boolean` — 写入并替换成功时返回 true。
**错误：** 路径未获本次运行写入授权、父目录不存在、目标是目录/链接、内容超过 4 MiB 或文件系统拒绝替换时产生错误。
**执行/取消语义：** 每条文件 IPC 在主进程执行前经过暂停检查点；暂停期间等待恢复。 Stop 取消当前运行 token；枚举循环会检查取消，写入在原子提交前再次检查；单次同步 I/O 受 4 MiB 上限约束。
**坐标/权限：** 坐标空间：none。Worker 在 IPC 前检查本次 Grant；主进程对每条文件 IPC 再次检查。只允许批准的读取/写入根；拒绝 reparse point、符号链接和多硬链接普通文件。这不是恶意脚本的 OS 级沙箱。

**示例：**
```lua
WriteTextFile("data/output.txt", "hello\n")
```

#### `AppendTextFile(path, text)` — 追加文本文件

- **稳定 ID：** `anjian.files.append-text`
- **状态：** 当前已实现；自动化 API 契约测试覆盖，Windows 桌面实机验收待完成。
- **说明：** 以 UTF-8（无 BOM）追加到批准根目录内的文件；文件与追加后内容总大小不得超过 4 MiB。

| 参数 | Lua 类型 | 必填 | 默认值 | 范围/单位/选项 | 说明 |
|---|---|---|---|---|---|
| `path` | `string` | 是 | — | — | 绝对路径或相对于当前脚本目录的路径。 |
| `text` | `string` | 是 | — | — | 要追加的文本内容。 |

**返回值：** `boolean` — 追加成功时返回 true。
**错误：** 路径未获本次运行写入授权、父目录不存在、目标是目录/链接/多硬链接文件或追加后超过 4 MiB 时产生错误。
**执行/取消语义：** 每条文件 IPC 在主进程执行前经过暂停检查点；暂停期间等待恢复。 Stop 取消当前运行 token；枚举循环会检查取消，写入在原子提交前再次检查；单次同步 I/O 受 4 MiB 上限约束。
**坐标/权限：** 坐标空间：none。Worker 在 IPC 前检查本次 Grant；主进程对每条文件 IPC 再次检查。只允许批准的读取/写入根；拒绝 reparse point、符号链接和多硬链接普通文件。这不是恶意脚本的 OS 级沙箱。

**示例：**
```lua
AppendTextFile("data/run.log", "done\n")
```

#### `FileExists(path)` — 检查文件是否存在

- **稳定 ID：** `anjian.files.file-exists`
- **状态：** 当前已实现；自动化 API 契约测试覆盖，Windows 桌面实机验收待完成。
- **说明：** 检查批准读取根目录中的普通文件是否存在；链接或其他特殊文件会拒绝而不是跟随。

| 参数 | Lua 类型 | 必填 | 默认值 | 范围/单位/选项 | 说明 |
|---|---|---|---|---|---|
| `path` | `string` | 是 | — | — | 绝对路径或相对于当前脚本目录的路径。 |

**返回值：** `boolean` — 普通文件存在时 true，否则 false。
**错误：** 路径未获本次运行读取授权，或路径链包含 reparse point/符号链接/多硬链接时产生错误。
**执行/取消语义：** 每条文件 IPC 在主进程执行前经过暂停检查点；暂停期间等待恢复。 Stop 取消当前运行 token；枚举循环会检查取消，写入在原子提交前再次检查；单次同步 I/O 受 4 MiB 上限约束。
**坐标/权限：** 坐标空间：none。Worker 在 IPC 前检查本次 Grant；主进程对每条文件 IPC 再次检查。只允许批准的读取/写入根；拒绝 reparse point、符号链接和多硬链接普通文件。这不是恶意脚本的 OS 级沙箱。

**示例：**
```lua
if FileExists("data/config.txt") then Log("配置存在") end
```

#### `DirectoryExists(path)` — 检查目录是否存在

- **稳定 ID：** `anjian.files.directory-exists`
- **状态：** 当前已实现；自动化 API 契约测试覆盖，Windows 桌面实机验收待完成。
- **说明：** 检查批准读取根目录中的目录是否存在；拒绝任何 reparse point/符号链接目录。

| 参数 | Lua 类型 | 必填 | 默认值 | 范围/单位/选项 | 说明 |
|---|---|---|---|---|---|
| `path` | `string` | 是 | — | — | 绝对路径或相对于当前脚本目录的路径。 |

**返回值：** `boolean` — 目录存在时 true，否则 false。
**错误：** 路径未获本次运行读取授权，或路径链包含 reparse point/符号链接时产生错误。
**执行/取消语义：** 每条文件 IPC 在主进程执行前经过暂停检查点；暂停期间等待恢复。 Stop 取消当前运行 token；枚举循环会检查取消，写入在原子提交前再次检查；单次同步 I/O 受 4 MiB 上限约束。
**坐标/权限：** 坐标空间：none。Worker 在 IPC 前检查本次 Grant；主进程对每条文件 IPC 再次检查。只允许批准的读取/写入根；拒绝 reparse point、符号链接和多硬链接普通文件。这不是恶意脚本的 OS 级沙箱。

**示例：**
```lua
if DirectoryExists("data") then Log("目录存在") end
```

#### `ListFiles(path)` — 枚举目录文件

- **稳定 ID：** `anjian.files.list-files`
- **状态：** 当前已实现；自动化 API 契约测试覆盖，Windows 桌面实机验收待完成。
- **说明：** 只返回批准目录的直接子级普通文件名；不递归，隐藏链接/重解析点和多硬链接文件，最多检查 1000 项。

| 参数 | Lua 类型 | 必填 | 默认值 | 范围/单位/选项 | 说明 |
|---|---|---|---|---|---|
| `path` | `string` | 是 | — | — | 绝对路径或相对于当前脚本目录的目录路径。 |

**返回值：** `table` — 按名称排序的 Lua 数组，元素为文件名字符串；不返回完整路径。
**错误：** 目录未获本次运行读取授权、目录不存在、目录含无法读取的项目，或直接子项超过 1000 个时产生错误。
**执行/取消语义：** 每条文件 IPC 在主进程执行前经过暂停检查点；暂停期间等待恢复。 Stop 取消当前运行 token；枚举循环会检查取消，写入在原子提交前再次检查；单次同步 I/O 受 4 MiB 上限约束。
**坐标/权限：** 坐标空间：none。Worker 在 IPC 前检查本次 Grant；主进程对每条文件 IPC 再次检查。只允许批准的读取/写入根；拒绝 reparse point、符号链接和多硬链接普通文件。这不是恶意脚本的 OS 级沙箱。

**示例：**
```lua
for _, name in ipairs(ListFiles("data")) do Log(name) end
```

#### `ListDirectories(path)` — 枚举子目录

- **稳定 ID：** `anjian.files.list-directories`
- **状态：** 当前已实现；自动化 API 契约测试覆盖，Windows 桌面实机验收待完成。
- **说明：** 只返回批准目录的直接子级普通目录名；不递归，隐藏链接/重解析点，最多检查 1000 项。

| 参数 | Lua 类型 | 必填 | 默认值 | 范围/单位/选项 | 说明 |
|---|---|---|---|---|---|
| `path` | `string` | 是 | — | — | 绝对路径或相对于当前脚本目录的目录路径。 |

**返回值：** `table` — 按名称排序的 Lua 数组，元素为目录名字符串；不返回完整路径。
**错误：** 目录未获本次运行读取授权、目录不存在、目录含无法读取的项目，或直接子项超过 1000 个时产生错误。
**执行/取消语义：** 每条文件 IPC 在主进程执行前经过暂停检查点；暂停期间等待恢复。 Stop 取消当前运行 token；枚举循环会检查取消，写入在原子提交前再次检查；单次同步 I/O 受 4 MiB 上限约束。
**坐标/权限：** 坐标空间：none。Worker 在 IPC 前检查本次 Grant；主进程对每条文件 IPC 再次检查。只允许批准的读取/写入根；拒绝 reparse point、符号链接和多硬链接普通文件。这不是恶意脚本的 OS 级沙箱。

**示例：**
```lua
for _, name in ipairs(ListDirectories("data")) do Log(name) end
```

#### `CreateDirectory(path)` — 创建一级目录

- **稳定 ID：** `anjian.files.create-directory`
- **状态：** 当前已实现；自动化 API 契约测试覆盖，Windows 桌面实机验收待完成。
- **说明：** 在已存在且获批准写入的父目录下创建单级目录；若目标目录已存在且不是链接则返回 true，不递归创建父目录。

| 参数 | Lua 类型 | 必填 | 默认值 | 范围/单位/选项 | 说明 |
|---|---|---|---|---|---|
| `path` | `string` | 是 | — | — | 绝对路径或相对于当前脚本目录的路径。 |

**返回值：** `boolean` — 目录已存在或创建成功时 true。
**错误：** 目标路径未获本次运行写入授权、父目录不存在、目标为文件/链接或文件系统拒绝创建时产生错误。
**执行/取消语义：** 每条文件 IPC 在主进程执行前经过暂停检查点；暂停期间等待恢复。 Stop 取消当前运行 token；枚举循环会检查取消，写入在原子提交前再次检查；单次同步 I/O 受 4 MiB 上限约束。
**坐标/权限：** 坐标空间：none。Worker 在 IPC 前检查本次 Grant；主进程对每条文件 IPC 再次检查。只允许批准的读取/写入根；拒绝 reparse point、符号链接和多硬链接普通文件。这不是恶意脚本的 OS 级沙箱。

**示例：**
```lua
CreateDirectory("data/cache")
```

<!-- END GENERATED API REFERENCE -->

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
