# 按键精灵 2026：Lua 函数与参数参考手册

- 目标读者：脚本编写者与自动化工程维护者
- 对应运行时：Lua 5.4
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
| `keys` | `keyName[]` | 是 | — | 固定值：ENTER、ESC、TAB、SPACE、BACKSPACE、DELETE、INSERT、HOME、END、PAGEUP、PAGEDOWN、UP、DOWN、LEFT、RIGHT、CTRL、CONTROL、SHIFT、ALT、WIN、CAPSLOCK、PRINTSCREEN、PAUSE、NUMLOCK、SCROLLLOCK、NUMPAD0、NUMPAD1、NUMPAD2、NUMPAD3、NUMPAD4、NUMPAD5、NUMPAD6、NUMPAD7、NUMPAD8、NUMPAD9、NUMPADMULTIPLY、NUMPADADD、NUMPADSUBTRACT、NUMPADDECIMAL、NUMPADDIVIDE、SEMICOLON、EQUALS、COMMA、MINUS、PERIOD、SLASH、BACKTICK、LEFTBRACKET、BACKSLASH、RIGHTBRACKET、QUOTE；规则：A-Z、0-9、F1-F24；可变参数 | 一个或多个规范按键名，按输入顺序按下、逆序释放。 |

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
