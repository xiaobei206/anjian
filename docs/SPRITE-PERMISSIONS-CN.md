# 小精灵权限 sidecar 契约（M12-11）

## 1. 源文件与清单绑定

- 每个 Lua 入口的可选权限请求文件名固定为 `<entry.lua>.permissions.json`，即源路径 `entryScriptPath + ".permissions.json"`；例如 `main.lua.permissions.json`。
- 打包器只收集所选入口的精确 sidecar，不扫描/合并同目录其他脚本的权限，也不因复制 sidecar 扩大授权。
- `SpritePackageEntry` 使用可空的 `permissionManifestPath` 字段。字段非空时，包内路径必须精确等于该 entry 的 `scriptPath + ".permissions.json"`（按包内正斜线相对路径比较）；不同入口不能交叉引用同一 sidecar。
- Sidecar 内容文件必须进入 `Anjian.Sprite.json` 的 `files` 闭合清单，记录实际大小与 SHA-256，并由 `SHA256SUMS.txt` 覆盖。缺失 sidecar、哈希不匹配、路径不匹配、reparse point/符号链接或无效 JSON/schema 均使包校验失败。
- 若 `permissionManifestPath` 为空，包内不得悄悄存在该入口的规范 sidecar 路径。入口没有 sidecar 是合法情形，但它不会继承其他入口的权限。

## 2. 内容契约

Sidecar 使用现有脚本权限 schema v1，最大 256 KiB UTF-8。它只包含请求字段：`schemaVersion`、`fileReadRoots`、`fileWriteRoots`、`clipboardRead`、`clipboardWrite`、`dialogs`、`processExecutables`、`httpHosts`、`httpMethods` 与 `allowInsecureHttp`。路径仍相对所属 Lua 入口目录解析；主机和方法遵循现有精确白名单规则。打包时须验证 JSON/schema 并拒绝 reparse point，不能自行修正、补全或扩大请求。

## 3. 启动与用户授权

- 启动器构造时检查整个发布包；每次开始某个入口时，还须再次验证该入口 sidecar 的存在、绑定、文件大小、manifest 记录、SHA-256 与 schema，避免启动前 sidecar 被替换后仍使用旧校验结果。
- 选中入口的 sidecar 非空且有效时，复用已有 `ScriptPermissionService` / 权限审阅窗体，对**本次运行**请求逐项审阅。用户拒绝时不启动 Worker。空请求或无 sidecar 不弹出空白审批窗体。
- 入口没有 sidecar时，传给 Worker 的权限 Grant 必须是 `ScriptPermissionGrant.Empty`。Sidecar 的请求内容本身不构成用户批准，包 manifest / hash 清单同样不构成授权；不得把上一次运行的批准持久化或跨入口复用。
- 有 sidecar 时，有效 Grant 只能由现有权限策略基于该 sidecar request 与本次用户显式选择创建。PlaybackRunner 主进程继续在每条 IPC 上独立复核有效 Grant；未知/伪造 IPC fail-closed。

## 4. 验收边界

Core 自动化测试覆盖每入口 sidecar 收集与绑定、清单和 SHA-256、无效/缺失/篡改处理。Windows CI 在自包含发布包中启动真实 apphost/Worker，验证即使侧车声明写入请求，未传递用户批准 Grant 仍无法写入。该 smoke 不自动批准权限。WinForms 审阅、拒绝/批准用户体验、DPI、多显示器与真实 Windows 10/11 交互仍需 M14 实机验收；CI 不替代该验收。

安全说明：SHA-256 可检测相对于清单的修改，但不是发布者签名或来源真实性证明；独立 Worker 也不是完整操作系统沙箱。
