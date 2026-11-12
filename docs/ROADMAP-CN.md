# 首版路线图

| 里程碑 | 状态 | 交付目标 |
|---|---|---|
| M0 产品规格 | DONE | 已确认首版交互、Lua API 和录制策略 |
| M1 首个 Windows 预览版 | IN_PROGRESS | 实现与 CI 已完成，正在创建 v0.1.0-preview.1 Release |
| M2 稳定性完善 | TODO | 用户实机验证、录制/输入边界、停止响应和长期运行 |
| M3 编辑器增强 | TODO | Lua 语法高亮、补全、参数提示和错误标记 |
| M4 可视化录制增强 | TODO | 动作列表、动作编辑和代码同步机制的完整 UI |
| M5 P1 能力 | TODO | 找图/找色、窗口能力、断点调试 |

## 首个预览版功能清单

- [x] .NET 10 WinForms 应用和 Windows x64 自包含发布
- [x] 脚本新建、打开、保存、重命名、复制、删除
- [x] 基础 Lua 文本编辑
- [x] Lua 5.4 / NLua 执行 worker 和停止隔离
- [x] Lua 全局 API：鼠标、键盘、延时、随机延时和日志
- [x] Click / KeyPress 默认 50ms，支持显式按下时长
- [x] 全局键鼠录制、鼠标移动合并和 Lua 代码生成
- [x] 播放/暂停/停止
- [x] 重复次数：默认 1，正整数按完整轮数，0=无限
- [x] 每轮间隔：默认 2000ms；只在轮次之间等待，不在最后一轮后等待
- [x] 播放设置本地持久化
- [x] GitHub Actions Release build、7 项测试、win-x64 publish 全部成功
- [x] Actions ZIP 和 SHA256 sidecar 内容已下载核验
- [ ] GitHub prerelease v0.1.0-preview.1 资产复核
- [ ] 用户 Windows 实机手工验收

## 首版明确暂缓
完整 Lua IDE、动作列表/可视化动作编辑、找图找色、OCR、窗口相对坐标、插件、云同步、Android、旧版 Q 语言兼容。先让最基本录制、编辑、按次数循环和间隔运行可用。
