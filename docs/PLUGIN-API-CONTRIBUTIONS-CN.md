# 插件 Lua API 贡献契约（M12-12）

本文冻结本地插件贡献 Lua helper 的 schema、冲突、生命周期和安全边界。这是 main 开发版能力；不代表已发布 v0.1.0-preview.4 包含该功能。完整 WinForms/DPI/多显示器实机验收留 M14。

## Manifest 扩展

在 Anjian.Plugin.json 根对象中可选添加 contributions：

    {
      "contributions": {
        "schemaVersion": 1,
        "functions": [{
          "id": "add",
          "name": "PluginAdd",
          "title": "相加",
          "description": "返回两个数的和。",
          "signature": "PluginAdd(a, b)",
          "module": "math",
          "export": "add",
          "parameters": [
            {"name": "a", "type": "number", "required": true, "description": "第一个加数"},
            {"name": "b", "type": "number", "required": true, "description": "第二个加数"}
          ],
          "returns": {"type": "number", "description": "两个数的和。"},
          "errors": ["参数类型不匹配", "插件函数执行失败"],
          "behavior": {
            "pauseBehavior": "沿用当前 Lua Worker 的指令检查点。",
            "cancellationBehavior": "Stop 终止本次 Worker。",
            "sideEffects": [],
            "coordinateSpace": "不适用。",
            "permissionNotes": "纯 Lua helper，不获得宿主自动化权限。"
          },
          "example": "PluginAdd(2, 3)"
        }]
      }
    }

contributions 是可选字段；存在时必须是对象并明确声明 schemaVersion=1。未知字段、未来 schema、缺失或类型不一致的必需字段均拒绝。每个插件最多 64 个函数，每个函数最多 16 个参数。函数 ID 在本插件内稳定唯一；名称必须是合法 Lua identifier、不能是 Lua reserved/standard global/宿主 API，也不能与其他启用插件贡献冲突。

module 是 modulesRoot 下的点分 Lua 模块名，export 是模块返回 table 中的函数标识符。每个 module 必须唯一映射到 modulesRoot 之下的一个已登记 .lua 或 init.lua 文件，并由 manifest files SHA-256 锁定。signature 必须与函数名和有序参数名完全一致。

参数目前只支持必填 scalar string、number、integer、boolean，禁止可选参数、默认值、可变参数、范围约束和隐式转换。返回值只支持 string、number、integer、boolean、table、nil。函数必须声明错误列表、暂停/取消语义、权限说明与示例；纯 Lua helper 的 sideEffects 必须为空。

## 执行与权限边界

- 插件 helper 在普通脚本的独立 Lua Worker 内执行，绝不在主 UI 线程执行；模块调用时才惰性加载，避免启用插件即执行第三方代码。
- 每个插件模块使用独立 Lua 环境，只暴露受限基础函数及 math、string、table、utf8 标准库副本。没有 _G、MoveTo、键鼠、文件、剪贴板、对话框、进程、窗口、图像、OCR 或 HTTP 宿主能力；自定义 require 只加载同插件、manifest 已列出并 hash 已验证的 Lua 模块。
- 每次普通脚本 Worker 启动重查 catalog 的 installed/enabled 状态、manifest/API 兼容、闭合文件清单和模块 SHA-256。停用、卸载或损坏的插件不能进入下一次 registry 视图或调用计划；重验失败时 fail-closed。
- 对外名称与内置 API、Lua 保留名称、运行时宿主全局以及其他已启用插件名称按大小写不敏感检测冲突。启用操作先校验完整启用集，若冲突则拒绝且不保存 catalog。插件 ID 与函数 ID 组成稳定 registry ID：plugin:{pluginId}:{id}。
- 参数数量/类型及返回类型由 Worker 运行时包装器逐次校验。插件异常会作为当前 Worker 中的 Lua 错误传播并终止当前脚本，不应该退出主 UI。Stop 继续使用现有 Worker 终止路径。
- contributions 不开放新增 IPC、不读取/扩大脚本 Grant、不加载任意 DLL/native code、不自动联网。SHA-256 不是发布者数字签名；受限 Lua 环境/独立 Worker 也不是完整 OS 沙箱。

## 统一元数据来源

内置 src/Anjian.Core/Api/lua-api-registry.json 保持 API 1.2 / 103 个内置函数，插件不修改该文件或静态 FUNCTION-REFERENCE-CN.md。启用插件在内存合成 registry extension view，编辑器 completion/signature、API 命令面板和本机生成的动态参考文档读取同一视图。停用后重新加载的视图不再包含该插件贡献。

生成本机插件 API 参考文件（只写入指定输出路径，不改仓库内静态内置手册）：

    dotnet run --project tools/Anjian.ApiDocsGenerator -- --write-plugin-reference "$env:LOCALAPPDATA\Anjian2026\Plugins" .\PLUGIN-API-REFERENCE-CN.md

## 验收状态

自动化验收应覆盖 schema/未知与未来字段、内置/跨插件名称冲突与启用原子性、disabled/hash tamper、实际 NLua 调用、参数数量/类型和错误传播、隔离环境、IDE 与 registry consistency、Windows Actions 构建测试及发布包 Worker smoke。Windows 10/11 插件管理、命令面板、DPI/多显示器和真实用户交互由 M14 另行验收；CI 通过不能作为桌面实测通过的证据。
