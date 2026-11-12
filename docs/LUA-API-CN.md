# Lua 5.4 自动化命令 API 规格

- Lua 版本：5.4
- API 版本：0.2（首版实现已接入；Lua 5.4 / 全局绑定 / 指令钩子已由 Windows CI 测试）
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

首版只采用全屏绝对坐标：虚拟桌面物理像素。主屏左上角通常为 (0,0)；位于主屏左侧或上方的副屏允许出现负坐标。首版不提供窗口相对坐标模式。

录制、代码生成和回放必须使用同一坐标语义；不得将逻辑 DPI 坐标、单屏客户区坐标与虚拟桌面物理像素混用。

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

- API 注册中心维护命令标识、中文名、分类、参数 schema、默认值、返回值、示例、最低 API 版本和弃用状态。
- 编辑器补全、参数提示、命令面板和文档从同一注册描述生成。
- 已发布 API 不随意改名或改变参数语义。
- 有破坏性更改时增加 API 版本并提供迁移说明。
- 全局函数是用户脚本层 API；内部 C# 仍按 Mouse / Keyboard / Runtime 等模块分层，避免把全局 API 设计泄漏为内部架构。
