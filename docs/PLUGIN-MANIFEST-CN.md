# 本地插件包格式（M12-10，schema v1）

本文冻结按键精灵 2026 本地 Lua 插件目录/ZIP 格式。它定义 M12-10 的校验、安装、版本与 Worker 执行边界；不表示后续插件命令注册 UI、第三方文档索引或 Windows 桌面验收已经完成。

## 包结构

每个目录包的根目录必须包含 Anjian.Plugin.json，ZIP 解压后也必须是这个布局（文件位于 ZIP 根目录，不允许在外层再套任意文件夹）。manifest 本身不列入 files，避免自引用 hash；除此之外，包内容必须与 files 精确闭合，不能有未登记文件或缺失项。

JSON manifest 示例（示例 hash 为占位文字，真实包必须填入文件实际 SHA-256）：

    {
      "schemaVersion": 1,
      "pluginId": "com.example.sample",
      "name": "示例插件",
      "version": "1.0.0",
      "description": "本地 Lua 插件示例",
      "author": "Example",
      "minApiVersion": "1.2",
      "maxApiVersion": "1.2",
      "entry": "entry.lua",
      "modulesRoot": "Modules",
      "permissions": {
        "schemaVersion": 1,
        "fileReadRoots": ["data"],
        "fileWriteRoots": ["output"],
        "clipboardRead": false,
        "clipboardWrite": false,
        "dialogs": [],
        "processExecutables": [],
        "httpHosts": [],
        "httpMethods": [],
        "allowInsecureHttp": false
      },
      "files": [
        { "path": "entry.lua", "sha256": "REPLACE_WITH_64_HEX_CHARACTERS" },
        { "path": "Modules/sample.lua", "sha256": "REPLACE_WITH_64_HEX_CHARACTERS" }
      ]
    }

manifest JSON 最大 256 KiB，UTF-8。

## 字段与兼容

- schemaVersion 当前必须为整数 1。未知、缺失或损坏的版本拒绝安装/运行，不自动重写。
- pluginId 是稳定、全小写 ASCII ID，符合 ^[a-z0-9]+(?:[.-][a-z0-9]+)*$，最长 160 字符；同 ID 的不同版本安装到独立目录，不互相覆盖。
- version 使用 major.minor.patch 语义版本形式，可带 prerelease/build metadata。名称、说明和作者为普通显示文本，名称不可为空。
- minApiVersion / maxApiVersion 使用 major.minor；运行时当前 API 版本来自唯一注册表 src/Anjian.Core/Api/lua-api-registry.json（当前为 1.2）。必须满足 min 不高于当前版本，且 max（若提供）不低于当前版本。
- entry 必须指向包内已列入 files 的 Lua 文件。主入口和模块都只在独立 Worker 的 Lua 5.4 state 中运行，绝不在主 UI 中执行。
- modulesRoot 为包内可选的模块目录，目录下仅可 require manifest 已登记、hash 已匹配的 Lua 文件；模块名采用点分名映射到相对路径。未登记或运行前被篡改的模块拒绝加载。
- permissions 使用现有脚本权限 schema v1 的相同字段和含义。文件读写根目录与相对 processExecutables 路径以包根目录解析；processExecutables 也可列出精确、完全限定的绝对路径。相对路径拒绝点段/越界，绝对路径仍需脚本 sidecar 请求并经用户逐次批准；不支持通配符。HTTP 主机/方法仍遵守现有精确白名单政策。它只是插件的上限声明，不是授权。
- 可选 contributions 字段可声明受限纯 Lua helper 的稳定 ID、公开名称、签名、参数/返回/错误契约、示例和模块导出；具体 schema、命名冲突、隔离与生命周期见 [插件 Lua API 贡献契约](PLUGIN-API-CONTRIBUTIONS-CN.md)。贡献只能引用 modulesRoot 下已登记并由 SHA-256 固定的 Lua 模块，不请求额外宿主权限。
- files 需覆盖每个内容文件，路径大小写不敏感去重、使用相对正斜线分隔、不能含盘符/UNC、冒号、空段、点段、父目录段、控制字符或根外路径。每项为 64 位十六进制 SHA-256 字符串。manifest 本身不包含在此列表。

## 文件边界与资源上限

- 只接受 Lua、JSON、Markdown、TXT 和普通 PNG/JPEG/ICO 资源；拒绝 DLL、EXE、Shell 脚本、驱动、安装器和所有未列入白名单的扩展名。M12-10 不加载任何原生 DLL，也不提供下载或联网安装。
- 最多 2,048 个文件；单文件最多 8 MiB；内容文件总大小最多 64 MiB；每次 ZIP 解包与目录安装都校验实际读取字节数，ZIP 链接、重复路径、路径穿越、绝对路径和资源上限越界都拒绝。
- 源目录、包内目录与文件不得为 reparse point/符号链接。安装时复制到目标同卷 staging 目录，完整校验通过后再原子移动为版本目录。
- 安装后每次启动仍重新验证 manifest、闭合文件列表和 hash；不把“安装时已校验”当成永久可信。用户数据与插件包分离，停用/卸载不自动清除用户文件/输出目录。

## 权限交集与默认行为

运行有效权限为三层交集：

1. 插件 manifest 的 permissions 声明；
2. 插件入口脚本旁的 entry.lua.permissions.json（若插件需要某项能力，则必须明确声明；该 sidecar 本身也必须在 manifest files 中并提供 hash）；
3. 用户本次在既有权限审阅窗体明确批准的 grant。

有效权限不能比任一层更宽。没有脚本 sidecar、用户拒绝、插件 manifest 不含某项能力、schema/hash 错误或版本不兼容时，相关能力默认为拒绝。文件根目录的交集是重叠根中更窄的子路径；进程路径/HTTP host/method/dialog token 是精确集合交集；布尔能力用逻辑 AND。未知 IPC 仍由主进程 fail-closed 检查。

## 生命周期

- 本地目录和 ZIP 安装；ZIP 只在用户显式选择后读取。安装不自动启用，默认 disabled。
- 每条 catalog 记录使用 pluginId + version + enabled；启停通过原子写入本地 catalog。损坏或未来 schema 的 catalog 不会被静默覆盖。
- 同一 pluginId/version 已安装时，如果现有内容与 hash 有效则保持幂等；如果目标目录存在但内容不符则失败，不自动覆盖。升级安装到新版本目录；切换版本需明确启用对应版本。
- 禁用后可保留文件但不得运行；卸载只能对已禁用项执行。加载、模块 require、脚本运行或 Stop 失败仅影响该 Worker/插件，不应使主 UI 退出。
- M12-12 在 main 开发版增加可选 contributions schema v1，允许启用插件贡献纯 Lua helper；其 API metadata 合入内存 registry extension view，供 IDE 补全/签名、API 命令面板与本机动态参考生成器使用。内置 registry 与静态函数手册仍保持 1.2 / 103，不更改已发布 preview.4。


## 信任与安全边界

SHA-256 用于检测包文件与 manifest 不一致，不是发布者数字签名，也不证明来源可信；可以同时改写 manifest 和内容的人仍能构造 hash 自洽的包。独立 Worker 与受限 Lua require 用于隔离插件 Lua state 和约束公开 API/文件加载入口，但不是完整操作系统沙箱，不能阻止同一用户权限下的全部恶意行为。只安装可信来源的插件；M12-12 贡献只开放受限纯 Lua helper，不加载 DLL/EXE/native module，不自动联网，不开放宿主自动化 API/IPC，也不扩大 Grant。真实 Windows 10/11 管理窗口、命令面板、权限审阅、DPI、多屏和用户交互仍需 M14 实机验收。
