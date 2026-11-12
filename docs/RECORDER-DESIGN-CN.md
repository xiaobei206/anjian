# 录制器与动作 / 代码双模式设计

## 1. 目标

用户录制物理键鼠操作后，得到可读、可修改、可重放的动作列表；同时能生成 Lua 脚本，让进阶用户继续编辑。录制器不是屏幕录像，也不是把每一个原始鼠标移动都直接写成一条代码。

## 2. 数据流

    物理输入源
      -> 标准化 InputEvent
      -> 有界事件队列
      -> 配对 / 去噪 / 移动合并
      -> RecorderDocument（动作列表）
      -> Action List UI
      -> Lua Code Generator
      -> .lua + .actions.json

采集回调只复制必要字段、标记单调时钟时间戳并快速入队。不能在低级钩子回调内写文件、输出大量日志或执行图片匹配。

## 3. 输入源技术选型

待 PoC 对比：
- Raw Input（优先验证）：适合后台窗口接收物理设备输入消息，不需要抑制用户输入。
- WH_KEYBOARD_LL / WH_MOUSE_LL：可观察全局输入细节，但回调有严格时限；超时可能导致钩子被系统静默移除。
- 必要时采用混合输入源，但必须验证重复事件、注入事件识别和权限行为。

录制仅观察输入，不阻止/吞掉用户事件。默认忽略本程序模拟输入，避免回放被再次录制。

## 4. 标准化 InputEvent

建议字段：
- eventId：本次录制内唯一 ID。
- timestampTicks：单调时钟时间，不使用系统墙上时间计算延时。
- device：Mouse / Keyboard。
- kind：Move / ButtonDown / ButtonUp / Wheel / KeyDown / KeyUp。
- x, y：虚拟桌面物理像素；非鼠标事件可为空。
- keyOrButton：规范化按键或鼠标键。
- flags：Injected / Extended / Absolute 等。
- displaySnapshot：事件发生时的显示器/DPI 快照（必要时）。
- source：RawInput / LowLevelHook / Synthetic 等。

坐标允许负值。绝对坐标、相对移动和光标屏幕坐标不得混淆。

## 5. 动作模型（Action Model）

首版动作类型：
- MouseMove(x, y)
- MouseClick(button, count)
- MouseDown(button)
- MouseUp(button)
- MouseWheel(delta)
- KeyPress(key)
- KeyDown(key)
- KeyUp(key)
- TypeText(text)（仅用户明确编辑或文本识别结果，不假设物理键盘事件能无损还原输入法文本）
- Delay(milliseconds)

每个动作包含稳定 actionId、动作类型、参数、与前一动作间隔、原始事件引用和来源信息。动作列表能展示并编辑所有 P0 字段。

## 6. 延时定义

- 保留原始单调时间戳。
- UI 默认显示“当前动作与上一动作之间的间隔”。
- 首个动作的 delay 为 0。
- 自动整理可在可配置阈值内合并过密鼠标移动，但不得改变点击、按下、释放的相对顺序。
- 长时间空闲不自动丢弃；允许用户选择压缩或保留。
- 代码生成时显式插入 Delay，避免录制回放速度不可预测。

## 7. 动作合并规则

- 连续 Move：在满足时间/距离阈值时合并为路径采样点；保留终点，必要时保留关键转向点。
- Click：只有确认完整 Down/Up 配对后才合并成点击；不确定时保留原始事件并标记。
- DoubleClick：只在两次点击间隔符合系统双击阈值且按钮相同的情况下，可折叠为双击；仍允许显示两次单击。
- KeyDown/KeyUp：按键必须成对；停止或录制结束仍未释放时标记异常，不伪造静默成功。
- Wheel：保留 delta 和方向。
- 队列溢出：停止录制或进入显式错误状态，记录丢失数量；不能静默丢弃关键按键状态转换。

## 8. 双模式同步规则（重要）

动作列表和 Lua 代码不是任意情况下都能无损双向转换。

采用“结构化录制数据作为初始来源 + 代码可自由扩展”的规则：
1. 动作列表生成 Lua 代码时，创建有 ID 的 generated block。
2. 生成区块以注释标记边界和 recorderId/schemaVersion。
3. 用户在生成区块外添加的普通 Lua 代码不会被覆盖。
4. 用户编辑生成区块后，该区块标记为 detached/dirty；动作列表显示“与代码不同步”。
5. 从动作列表重新生成会覆盖对应生成区块，必须先预览并确认。
6. 首版不承诺把任意 Lua 语句解析回动作列表；只允许重新生成器管理的结构化区块。
7. 未来如实现 Lua 子集反向解析，必须明确可解析语法和失败提示。

示意：

    -- <anjian:generated id="recording-001" schema="1">
    Mouse.MoveTo(500, 300)
    Mouse.Click("left")
    Delay(200)
    -- </anjian:generated>

## 9. 持久化格式

文件：<script>.actions.json。

顶层至少包含：
- schemaVersion
- recorderVersion
- scriptId
- recordingId
- createdAtUtc
- coordinateSpace = virtual-desktop-physical-pixels
- events[]
- actions[]
- generatedBlockId
- synchronizationState

所有字段均应可迁移。schemaVersion 不兼容时不得覆盖原文件。

## 10. 录制体验

- 全局录制状态浮层可隐藏，但必须有明显状态反馈。
- 支持暂停/恢复录制；暂停期间事件不记入动作。
- 录制开始/结束快捷键可配置。
- 默认不录制本软件自身的工具栏点击（需通过窗口/事件来源策略实现并验证）。
- 录制期间软件关闭或崩溃，尽力恢复最近一次保存快照。
- 大量鼠标移动时 UI 虚拟化/批处理更新，不逐事件刷新界面。

## 11. 测试重点

- Down/Up 配对与异常结束。
- Raw Input 与 Hook 重复事件。
- 注入输入过滤。
- 负坐标、多显示器、混合 DPI。
- 输入法、Unicode 和组合键差异。
- 高速移动和大量事件队列。
- 双击阈值与系统设置变化。
- 代码编辑后 dirty 状态、预览覆盖和恢复。
