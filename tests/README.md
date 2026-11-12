# tests 目录说明

正式测试项目将在 M2 解决方案骨架阶段建立。

测试原则：
- Core/Scripting/Automation/Recorder 优先单元测试。
- Win32 输入和真实录制必须有 Windows x64 集成测试。
- 录制器的配对、队列、时间和代码生成使用确定性模拟事件测试。
- 无法在 CI 模拟的输入行为必须记录手工测试证据。
- 未执行的测试标记 NOT RUN，不得根据代码阅读标记 PASS。

验收入口：docs/ACCEPTANCE-TEST-CN.md。
