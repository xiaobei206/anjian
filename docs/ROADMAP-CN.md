# 首版路线图

| 里程碑 | 状态 | 交付目标 |
|---|---|---|
| M0 产品规格 | DONE | 首版交互、Lua API 和录制策略已确认 |
| M1 Windows 预览版 v0.1.0-preview.2 | DONE | WinForms/Lua/键鼠录制和输入/重复播放；Release 已公开发布 |
| M1.1 点击录制与悬浮窗 v0.1.0-preview.3 | RELEASED | 点击录制、暂停续录和旧紧凑窗已发布；用户指出窗口生命周期问题 |
| M1.2 独立悬浮窗、系统托盘与应用图标 v0.1.0-preview.4 | RELEASED | 双窗口、托盘生命周期、循环设置和 EXE/托盘图标已发布；等待实机验收 |
| M2 用户实机验收与稳定性 | IN_PROGRESS | Windows 10/11 手工测试、2 小时稳定性、DPI 和多屏 |
| M3 编辑器增强 | TODO | Lua 语法高亮、补全、参数提示、错误标记 |
| M4 可视化录制增强 | TODO | 动作列表、动作编辑、代码同步 |
| M5 P1 能力 | TODO | 找图/找色、窗口能力、断点调试 |

## v0.1.0-preview.3 已交付

- [x] Windows 10/11 x64 WinForms 应用，.NET 10 self-contained
- [x] 脚本新建、打开、保存、重命名、复制、删除和右键菜单
- [x] 基础 Lua 文本编辑器
- [x] Lua 5.4 / NLua worker 子进程和 Stop 终止策略
- [x] 鼠标/键盘全局录制、鼠标移动合并和 Lua 生成
- [x] Windows SendInput / SetCursorPos 输入及输入状态清理
- [x] Click / KeyPress 默认 50ms
- [x] 播放/暂停/停止
- [x] 全局快捷键 F10 启动、F11 暂停/继续、F12 中止（Windows CI SUCCESS，待用户实机验收）
- [x] 重复次数：默认 1，正整数按轮数执行，0=无限循环
- [x] 每轮间隔：默认 2000ms，只在轮次之间等待，末轮后不等待
- [x] 播放设置本地持久化
- [x] GitHub Actions Release build 0 warning / 0 error
- [x] 7 项自动化测试通过（7/7）
- [x] win-x64 self-contained ZIP 发布成功
- [x] SHA256 sidecar 与实际发布 ZIP 复核一致
- [x] GitHub prerelease v0.1.0-preview.3 已发布
- [x] 只记录点击坐标，生成 MoveTo -> 可选 Delay -> Click
- [x] 录制暂停/继续，暂停期间时间不写入脚本
- [x] preview.3 曾提供同窗紧凑/完整切换（已由 preview.4 独立双窗口设计取代）
- [ ] 用户 Windows 实机验收点击坐标、续录和悬浮窗
- [ ] 用户 Windows 实机手工验收
- [ ] 2 小时稳定性测试和 DPI/多显示器实测

## 暂缓
完整 Lua IDE、动作列表可视化编辑、找图找色、OCR、窗口相对坐标、后台输入、插件、云同步、Android 和旧版 Q 语言兼容。先根据用户实测结果完善录制、编辑和循环运行。


## v0.1.0-preview.4 已发布

- [x] 拆分独立 FloatingForm 与完整 MainForm，不再切换同一个窗口的面板
- [x] 主窗口与悬浮窗可同时显示；悬浮窗保留播放/录制状态
- [x] 主窗口关闭或点“返回悬浮窗”时隐藏主窗口并移除任务栏按钮
- [x] 系统托盘菜单提供重新打开主窗口、显示/隐藏悬浮窗和退出程序
- [x] 悬浮窗直接编辑循环次数与轮间隔，设置与主窗口双向同步
- [x] 自定义多尺寸 ICO 已嵌入 EXE，并用于主窗体和托盘
- [x] Windows CI 编译、7 项测试、self-contained 发布和 ZIP/SHA256 打包通过（run 43253075450）
- [ ] Windows 实机验收：窗口切换、托盘退出、DPI/多屏位置和图标可见性
