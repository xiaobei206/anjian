# Lua 5.4 与自动化命令 API 规格（草案）

- Lua 版本：5.4
- 命令风格：英文稳定 API 名称 + 中文命令面板说明
- API 版本：0.1（尚未实现）
- 约束：API 的签名、单位、返回值和异常行为必须在代码与帮助文档中保持一致

## 1. 语言层

使用标准 Lua 5.4 语法：局部变量、表、循环、条件、函数、闭包、错误处理。不要另外创造一套 Q 语言式解析器。

示例：

    local count = 10
    for i = 1, count do
        Mouse.MoveTo(500, 300)
        Mouse.Click("left")
        Delay(500)
    end

脚本扩展名为 .lua。文件编码 UTF-8；换行兼容 CRLF/LF。

## 2. API 命名与约束

- 命令面板显示中文名称，插入英文 API。
- Namespace 使用 Mouse、Keyboard、Log 等 PascalCase 模块名。
- 函数名采用 PascalCase。
- 坐标单位：虚拟桌面物理像素。
- 时间单位：毫秒。
- API 参数错误：抛出带脚本位置的 Lua 错误。
- Windows 操作失败：返回 nil, errorCode, message；成功返回 true 或明确值。
- 用户停止：作为取消状态结束，不当作普通脚本异常。

## 3. Mouse 鼠标命令

| 命令 | 签名 | 含义 |
|---|---|---|
| 移动鼠标 | Mouse.MoveTo(x, y) | 移动到虚拟桌面坐标 |
| 单击 | Mouse.Click(button) | 单击 left / right / middle |
| 双击 | Mouse.DoubleClick(button) | 双击指定按钮 |
| 按下 | Mouse.Down(button) | 按下并保持 |
| 释放 | Mouse.Up(button) | 释放指定按钮 |
| 滚轮 | Mouse.Scroll(delta) | 正数向上、负数向下；120 为一个标准滚轮刻度 |
| 获取坐标 | Mouse.GetPosition() | 返回 x, y |

按钮值：left、right、middle。后续扩展侧键时增加明确枚举，不接受任意字符串静默映射。

示例：

    Mouse.MoveTo(700, 450)
    Mouse.Click("left")
    local x, y = Mouse.GetPosition()
    Log.Info(string.format("当前位置：%d,%d", x, y))

## 4. Keyboard 键盘命令

| 命令 | 签名 | 含义 |
|---|---|---|
| 单键点击 | Keyboard.Press(key) | 按下后释放 |
| 按下 | Keyboard.Down(key) | 按下并保持 |
| 释放 | Keyboard.Up(key) | 释放指定键 |
| 组合键 | Keyboard.Hotkey(modifier1, ..., key) | 按顺序按下，逆序释放 |
| 文本输入 | Keyboard.TypeText(text) | 输入 Unicode 文本 |

首批规范键名包括：ENTER、ESC、TAB、SPACE、BACKSPACE、DELETE、UP、DOWN、LEFT、RIGHT、CTRL、SHIFT、ALT、WIN、F1..F24、A..Z、0..9。

示例：

    Keyboard.Hotkey("CTRL", "SHIFT", "S")
    Keyboard.TypeText("按键精灵 2026")

注意：
- Press 是完整按下/释放；Down 与 Up 必须配对。
- 组合键会按参数顺序按下，逆序释放；实现必须在异常/停止时清理仍由本程序持有的按键。
- TypeText 的中文/日文输入法组合行为需要 PoC；首版保证 Unicode 文本注入能力，不承诺重建原始 IME 组合过程。
- 物理按键录制与输入法最终生成的文字不是同一层数据。

## 5. 时间与流程

| 命令 | 签名 | 含义 |
|---|---|---|
| 延时 | Delay(milliseconds) | 可暂停、可取消延时 |
| 随机延时 | RandomDelay(minMs, maxMs) | min/max 均包含，整数毫秒 |
| 日志信息 | Log.Info(message) | 普通信息 |
| 日志警告 | Log.Warn(message) | 警告 |
| 日志错误 | Log.Error(message) | 错误级别输出 |

循环、条件、变量和函数使用 Lua 原生语法：

    for i = 1, 5 do
        if i % 2 == 0 then
            Log.Info("偶数轮次")
        end
        Delay(200)
    end

## 6. 返回值与错误契约

- 操作成功：返回 true，读取类命令返回定义好的数据。
- 可恢复的系统操作失败：返回 nil, code, message。
- 参数无效、API 使用错误：抛出 Lua error，包含文件、行号和函数名。
- 用户停止：由执行器产生 Cancelled 状态，不能吞成 nil 后继续运行。
- API 不应返回含有 Win32 指针、Lua userdata 或 UI 控件的对象。

## 7. Lua 标准库暴露策略

允许：基础语法、string、table、受限 math、utf8。

默认不暴露：任意 .NET 类型导入、io、os、package、debug、原生库加载、任意 Shell 执行。后续文件/进程能力必须设计为用户可见、可授权的专用 API。

这是一种减少误操作的受限脚本环境，不应宣称为对本机恶意脚本的安全边界。

## 8. API 版本与兼容

- API 注册中心维护命令标识、中文名、分类、参数 schema、返回值、示例、最低 API 版本和弃用状态。
- 编辑器补全、参数提示、命令面板和文档从同一注册描述生成。
- 已发布 API 不随意改名或改变参数语义。
- 有破坏性更改时增加 API 版本并提供迁移说明。
- API 完整字典在技术 PoC 后再冻结；当前文档是 v0.1 草案。
