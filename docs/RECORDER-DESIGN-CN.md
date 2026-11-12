# 录制器与动作 / 代码双模式设计

## 1. 目标

用户录制物理键鼠操作后，得到可读、可修改、可重放的动作列表；同时能生成 Lua 脚本，让进阶用户继续编辑。录制器不是屏幕录像，也不是把每一个原始鼠标移动都直接写成一条代码。

## 2. 已确认的首版录制策略

- 鼠标移动采用合并/降噪策略，不逐帧、逐事件输出每个原始坐标。
- 保留移动终点和必要的路径采样点；点击、按下、释放、滚轮及其顺序不得因合并而丢失。
- 坐标采用全屏绝对坐标：虚拟桌面物理像素，允许副屏出现负坐标。
- 首版不做窗口相对坐标。
- Down / Up 配对后，点击动作保存实际按下时长；生成 Lua 时输出 `LeftClick(holdMs)` / `RightClick(holdMs)` 等函数。
- 生成代码中的点击时长不再重复生成相同的 Delay；动作间隔与按钮按下时长是两个独立时间量。

## 3. 数据流

    物理输入源
      -> 标准化 InputEvent
      -> 有界事件队列
      -> 配对 / 去噪 / 移动合并
      -> RecorderDocument（动作列表）
      -> Action List UI
      -> Lua Code Generator
      -> .lua + .actions.json

采集回调只复制必要字段、标记单调时钟时间戳并快速入队。不能在低级钩子回调内写文件、输出大量日志或执行图片匹配。

## 4. 输入源技术选型

待 PoC 对比：
- Raw Input（优先验证）：适合后台窗口接收物理设备输入消息，不需要抑制用户输入。
- WH_KEYBOARD_LL / WH_MOUSE_LL：可观察全局输入细节，但回调有严格时限；超时可能导致钩子被系统静默移除。
- 必要时采用混合输入源，但必须验证重复事件、注入事件识别和权限行为。

录制仅观察输入，不阻止/吞掉用户事件。默认忽略本程序模拟输入，避免回放被再次录制。

## 5. 标准化 InputEvent

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

## 6. 动作模型（Action Model）

首版动作类型：
- MouseMove(x, y)
- MouseClick(button, holdMs)
- MouseDown(button)
- MouseUp(button)
- MouseWheel(delta)
- KeyPress(key, holdMs)
- KeyDown(key)
- KeyUp(key)
- TypeText(text)（仅用户明确编辑或文本识别结果，不假设物理键盘事件能无损还原输入法文本）
- Delay(milliseconds)

每个动作包含稳定 actionId、动作类型、参数、与前一动作间隔、原始事件引用和来源信息。动作列表能展示并编辑所有 P0 字段。

## 7. 延时定义

- 保留原始单调时间戳。
- UI 默认显示“当前动作与上一动作之间的间隔”。
- 首个动作的 delay 为 0。
- 按钮按下持续时间（Down 到 Up）与动作之间的空闲间隔分别保存，不能混为同一个 Delay。
- 长时间空闲不自动丢弃；允许用户选择压缩或保留。
- 代码生成时显式插入 Delay，避免录制回放速度不可预测。

## 8. 动作合并规则

- 连续 Move：在满足时间/距离阈值时合并为路径采样点；保留终点，必要时保留关键转向点。
- Click：只有确认完整 Down/Up 配对后才合并成点击；holdMs = Down 与 Up 的单调时间差；不确定时保留原始事件并标记。
- DoubleClick：只在两次点击间隔符合系统双击阈值且按钮相同的情况下，可折叠为双击；仍允许显示两次单击。
- KeyDown/KeyUp：按键必须成对；停止或录制结束仍未释放时标记异常，不伪造静默成功。
- Wheel：保留 delta 和方向。
- 队列溢出：停止录制或进入显式错误状态，记录丢失数量；不能静默丢弃关键按键状态转换。

## 9. 双模式同步规则（重要）

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
    MoveTo(500, 300)
    LeftClick(50)
    Delay(200)
    -- </anjian:generated>

## 10. 持久化格式

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

## 11. 录制体验

- 全局录制状态浮层可隐藏，但必须有明显状态反馈。
- 支持暂停/恢复录制；暂停期间事件不记入动作。
- 录制开始/结束快捷键可配置。
- 默认不录制本软件自身的工具栏点击（需通过窗口/事件来源策略实现并验证）。
- 录制期间软件关闭或崩溃，尽力恢复最近一次保存快照。
- 大量鼠标移动时 UI 虚拟化/批处理更新，不逐事件刷新界面。

## 12. 测试重点

- Down/Up 配对与异常结束。
- Raw Input 与 Hook 重复事件。
- 注入输入过滤。
- 负坐标、多显示器、混合 DPI。
- 输入法、Unicode 和组合键差异。
- 高速移动和大量事件队列。
- 双击阈值与系统设置变化。
- 代码编辑后 dirty 状态、预览覆盖和恢复。


## 13. M5 实现状态与边界（main 开发版）

- sidecar 文件名固定为脚本名.actions.json，例如 main.lua 对应 main.actions.json；与 Lua 脚本放在同一目录。写入通过临时文件、flush-to-disk、旧文件 .bak 备份和覆盖移动完成。
- schemaVersion 1 为当前持久化格式。遇到未知 schema/坐标空间时拒绝读取和编辑，保留原文件，不进行自动降级覆盖。
- RecorderDocument 保存 ScriptId、RecordingId、timestamp frequency、coordinate space、clickOnly、队列丢失数、全部原始 events、结构化 actions、generated block ID/hash 和同步状态。原始事件保存 Hook Source 与 Flags；动作保存 actionId、来源、时间戳范围和 RawEventIds。
- 当前配对策略故意保守：只有时间排序后相邻、同名且时长不超过 Click/KeyPress 上限的 Down/Up 才折叠；Move 或其他事件插入时不跨事件配对，重叠按键也保留 Down/Up。该策略优先保证可回放顺序，不臆测复杂拖动/组合键。
- M5 的动作列表已采用 DataGridView VirtualMode，提供插入、删除、复制、上下移动、启停、字段/延时、分组、注释、批量延时，展示 actionId、来源和原始事件 ID。录制时仍由旧 Hook 合并鼠标移动；本轮没有重写 Hook。
- Lua 生成区块的外层标记含 generated block ID/schema，每个动作另有稳定 actionId 注释，可从列表定位到编辑器。动作表到 Lua 是单向生成；任意 Lua 文本不反解析成动作。
- 代码块之外的用户代码保持原样。手改区块时状态为 Detached，删除区块时状态为 Missing；覆盖/补回前展示当前区块与新生成区块并明确确认。取消操作不会更改脚本文本。区块 hash 将 CRLF/CR 统一为 LF 后再计算。
- 单步试运行由既有 PlaybackRunner 执行临时 Lua 片段；执行前确认真实输入风险，时长有界，Worker 退出仍走已有 ReleaseAllOwnedInputs 清理。它不是模拟预览。
- Windows CI 只验证构建与自动化测试；Hook 安装/漏录/重复、权限、DPI、多显示器、实际动作表手工编辑和键鼠试运行仍需 Windows 10/11 桌面验收。
