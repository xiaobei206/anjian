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
