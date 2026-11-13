using Anjian.App.Services;
using Anjian.Core;
using Anjian.Core.Api;
using Anjian.Core.Permissions;
using Anjian.Core.Plugins;

namespace Anjian.App.Controls;

internal sealed class PluginManagerForm : Form
{
    private sealed class PackageChoice(PluginPackageSummary summary)
    {
        public PluginPackageSummary Summary { get; } = summary;

        public override string ToString()
        {
            var state = Summary.Enabled ? "已启用" : "已停用";
            var validity = Summary.ValidationError is null ? "" : " · 校验失败";
            return $"{Summary.Name} · {Summary.PluginId} {Summary.Version} · {state}{validity}";
        }
    }

    private readonly PluginPackageStore _store;
    private readonly PlaybackRunner _runner = new();
    private readonly string _currentApiVersion;
    private readonly ListBox _packages = new() { Dock = DockStyle.Fill, IntegralHeight = false };
    private readonly RichTextBox _details = new() { Dock = DockStyle.Fill, ReadOnly = true, DetectUrls = false };
    private readonly RichTextBox _log = new() { Dock = DockStyle.Fill, ReadOnly = true, DetectUrls = false };
    private readonly Label _status = new() { Dock = DockStyle.Fill, AutoSize = false, TextAlign = ContentAlignment.MiddleLeft };
    private readonly Button _enableButton = new() { Text = "启用" };
    private readonly Button _disableButton = new() { Text = "停用" };
    private readonly Button _uninstallButton = new() { Text = "卸载" };
    private readonly Button _runButton = new() { Text = "运行插件" };
    private readonly Button _pauseButton = new() { Text = "暂停" };
    private readonly Button _stopButton = new() { Text = "停止" };
    private readonly Button _refreshButton = new() { Text = "刷新" };
    private readonly Button _installDirectoryButton = new() { Text = "导入目录" };
    private readonly Button _installZipButton = new() { Text = "导入 ZIP" };
    private bool _closeWhenStopped;

    public PluginManagerForm(string rootDirectory, string currentApiVersion)
    {
        _currentApiVersion = currentApiVersion;
        _store = new PluginPackageStore(rootDirectory, currentApiVersion);
        Text = "本地插件管理";
        StartPosition = FormStartPosition.CenterParent;
        MinimumSize = new Size(760, 560);
        Size = new Size(920, 700);
        Font = new Font("Microsoft YaHei UI", 9F);
        _runner.SetUiOwner(this);
        _runner.Log += message => PostToUi(() => AppendLog(message));

        var toolbar = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            AutoScroll = true,
            Padding = new Padding(2)
        };
        _installDirectoryButton.Click += (_, _) => ImportDirectory();
        _installZipButton.Click += (_, _) => ImportZip();
        _enableButton.Click += (_, _) => EnableSelected();
        _disableButton.Click += (_, _) => DisableSelected();
        _uninstallButton.Click += (_, _) => UninstallSelected();
        _runButton.Click += async (_, _) => await RunSelectedAsync();
        _pauseButton.Click += (_, _) => TogglePause();
        _stopButton.Click += (_, _) => StopRun();
        _refreshButton.Click += (_, _) => RefreshPackages();
        foreach (var button in new[]
        {
            _installDirectoryButton, _installZipButton, _refreshButton,
            _enableButton, _disableButton, _uninstallButton, _runButton, _pauseButton, _stopButton
        })
        {
            button.AutoSize = true;
            button.Margin = new Padding(3);
            toolbar.Controls.Add(button);
        }

        _packages.SelectedIndexChanged += (_, _) =>
        {
            UpdateDetails();
            UpdateButtons();
        };
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 5,
            Padding = new Padding(8)
        };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 34));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 25));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 41));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        layout.Controls.Add(toolbar, 0, 0);
        layout.Controls.Add(_packages, 0, 1);
        layout.Controls.Add(_details, 0, 2);
        layout.Controls.Add(_log, 0, 3);
        layout.Controls.Add(_status, 0, 4);
        Controls.Add(layout);

        _status.Text = "本地插件存储：" + _store.RootDirectory;
        RefreshPackages(showErrors: false);
        UpdateButtons();
        FormClosing += HandleClosing;
        FormClosed += (_, _) => _runner.Dispose();
    }

    private PluginPackageSummary? SelectedSummary =>
        (_packages.SelectedItem as PackageChoice)?.Summary;

    private void ImportDirectory()
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = "选择包含 Anjian.Plugin.json 的插件包根目录。"
        };
        if (dialog.ShowDialog(this) != DialogResult.OK)
            return;
        try
        {
            var package = _store.InstallDirectory(dialog.SelectedPath);
            AppendLog($"已导入插件 {package.PluginId} {package.Version}（初始停用）。");
            RefreshPackages();
            SelectPackage(package.PluginId, package.Version);
        }
        catch (Exception ex)
        {
            ShowError("导入插件目录失败", ex);
        }
    }

    private void ImportZip()
    {
        using var dialog = new OpenFileDialog
        {
            Title = "导入本地插件 ZIP",
            Filter = "插件 ZIP (*.zip)|*.zip",
            CheckFileExists = true,
            Multiselect = false
        };
        if (dialog.ShowDialog(this) != DialogResult.OK)
            return;
        try
        {
            var package = _store.InstallZip(dialog.FileName);
            AppendLog($"已导入插件 {package.PluginId} {package.Version}（初始停用）。");
            RefreshPackages();
            SelectPackage(package.PluginId, package.Version);
        }
        catch (Exception ex)
        {
            ShowError("导入插件 ZIP 失败", ex);
        }
    }

    private void EnableSelected()
    {
        var package = SelectedSummary;
        if (package is null)
            return;
        var answer = MessageBox.Show(this,
            $"启用本地插件？\n\n名称：{package.Name}\nID：{package.PluginId}\n版本：{package.Version}\n目录：{package.PackageDirectory}\n\n启用不会自动授予权限；运行时仍须通过脚本请求、插件声明和本次批准的交集。",
            "启用插件", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
        if (answer != DialogResult.Yes)
            return;
        try
        {
            _store.Enable(package.PluginId, package.Version);
            AppendLog($"已启用插件 {package.PluginId} {package.Version}。同 ID 的其他版本已停用。");
            RefreshPackages();
            SelectPackage(package.PluginId, package.Version);
        }
        catch (Exception ex)
        {
            ShowError("启用插件失败", ex);
        }
    }

    private void DisableSelected()
    {
        var package = SelectedSummary;
        if (package is null)
            return;
        try
        {
            _store.Disable(package.PluginId, package.Version);
            AppendLog($"已停用插件 {package.PluginId} {package.Version}。");
            RefreshPackages();
            SelectPackage(package.PluginId, package.Version);
        }
        catch (Exception ex)
        {
            ShowError("停用插件失败", ex);
        }
    }

    private void UninstallSelected()
    {
        var package = SelectedSummary;
        if (package is null)
            return;
        if (package.Enabled)
        {
            MessageBox.Show(this, "请先停用插件，再卸载。", "卸载插件",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        if (MessageBox.Show(this,
                $"卸载插件 {package.PluginId} {package.Version}？\n只删除插件包，不会删除其请求访问的用户数据目录。",
                "卸载插件", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
            return;
        try
        {
            _store.Uninstall(package.PluginId, package.Version);
            AppendLog($"已卸载插件 {package.PluginId} {package.Version}。");
            RefreshPackages();
        }
        catch (Exception ex)
        {
            ShowError("卸载插件失败", ex);
        }
    }

    private async Task RunSelectedAsync()
    {
        var package = SelectedSummary;
        if (package is null || _runner.IsRunning)
            return;
        if (!package.Enabled)
        {
            MessageBox.Show(this, "请先显式启用插件。", "运行插件", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        if (package.ValidationError is not null || string.IsNullOrWhiteSpace(package.EntryPath))
        {
            MessageBox.Show(this, "插件包校验失败，不能运行：" + package.ValidationError,
                "运行插件", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        if (!ScriptPermissionService.TryReview(this, package.EntryPath, out var scriptGrant))
            return;

        try
        {
            var plan = _store.ResolveExecutionPlan(package.PluginId, package.Version, scriptGrant);
            var workerRuntime = new PluginWorkerRuntimeOptions
            {
                PackageRootPath = plan.PackageRoot,
                EntryRelativePath = plan.EntryRelativePath,
                EntrySha256 = plan.EntrySha256,
                ModulesRootRelativePath = plan.ModulesRootRelativePath,
                ModuleHashes = plan.ModuleHashes.ToDictionary(
                    pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase)
            };
            AppendLog($"启动插件 {plan.PluginId} {plan.Version}；入口文件与模块将在 Worker 内再次按 SHA-256 校验。");
            _status.Text = $"插件运行中：{plan.PluginId} {plan.Version}";
            UpdateButtons();
            await _runner.RunAsync(
                plan.EntryPath,
                new PlaybackOptions { RepeatCount = 1, IntervalMilliseconds = 0 },
                permissions: plan.Permissions,
                pluginRuntimeOptions: workerRuntime);
            AppendLog($"插件 {plan.PluginId} {plan.Version} 运行会话结束。");
        }
        catch (Exception ex)
        {
            AppendLog("插件运行失败：" + ex.Message);
            MessageBox.Show(this, "插件运行失败（仅此 Worker 会话失败，主界面仍可继续使用）：\n" + ex.Message,
                "插件运行", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            if (!IsDisposed)
            {
                _status.Text = "插件运行会话已结束。";
                RefreshPackages(showErrors: false);
                UpdateButtons();
                if (_closeWhenStopped)
                    PostToUi(Close);
            }
        }
    }

    private void TogglePause()
    {
        if (!_runner.IsRunning)
            return;
        if (_runner.IsPaused)
        {
            _runner.Resume();
            _pauseButton.Text = "暂停";
            _status.Text = "插件已继续。";
        }
        else
        {
            _runner.Pause();
            _pauseButton.Text = "继续";
            _status.Text = "插件将在下一个命令边界暂停。";
        }
        UpdateButtons();
    }

    private void StopRun()
    {
        if (!_runner.IsRunning)
            return;
        _runner.Stop();
        _status.Text = "正在停止插件 Worker…";
        _stopButton.Enabled = false;
    }

    private void RefreshPackages(bool showErrors = true)
    {
        try
        {
            var previous = SelectedSummary;
            var packages = _store.ListInstalled();
            _packages.BeginUpdate();
            _packages.Items.Clear();
            foreach (var package in packages)
                _packages.Items.Add(new PackageChoice(package));
            _packages.EndUpdate();
            if (previous is not null)
                SelectPackage(previous.PluginId, previous.Version);
            if (_packages.SelectedIndex < 0 && _packages.Items.Count > 0)
                _packages.SelectedIndex = 0;
            _status.Text = $"已安装 {packages.Count} 个插件版本。存储目录：{_store.RootDirectory}";
            UpdateDetails();
            UpdateButtons();
        }
        catch (Exception ex)
        {
            _status.Text = "插件列表不可用：" + ex.Message;
            if (showErrors)
                ShowError("读取插件列表失败", ex);
        }
    }

    private void UpdateDetails()
    {
        var package = SelectedSummary;
        if (package is null)
        {
            _details.Clear();
            return;
        }

        if (package.ValidationError is not null)
        {
            _details.Text = $"包目录：{package.PackageDirectory}\n校验失败：{package.ValidationError}\n为避免执行未经验证的内容，此版本不可启用或运行。";
            return;
        }

        try
        {
            var inspection = PluginPackageValidator.Inspect(package.PackageDirectory, _currentApiVersion);
            var manifest = inspection.Manifest;
            var permissions = inspection.ManifestPermissions;
            _details.Text =
                $"来源：本地目录/ZIP 导入\n包目录：{package.PackageDirectory}\n" +
                $"API 兼容：{manifest.MinApiVersion} – {manifest.MaxApiVersion ?? "无上限"}；入口：{manifest.Entry}\n" +
                $"Lua API 贡献：{manifest.Contributions?.Functions.Count ?? 0} 个（仅纯 Lua helper；不授予额外宿主权限）\n" +
                $"插件声明的能力上限：文件读 [{string.Join(", ", permissions.FileReadRoots)}]；文件写 [{string.Join(", ", permissions.FileWriteRoots)}]；" +
                $"剪贴板读/写 [{permissions.ClipboardRead}/{permissions.ClipboardWrite}]；对话框 [{string.Join(", ", permissions.Dialogs)}]；" +
                $"进程 [{string.Join(", ", permissions.ProcessExecutables)}]；HTTP 主机 [{string.Join(", ", permissions.HttpHosts)}]；" +
                $"HTTP 方法 [{string.Join(", ", permissions.HttpMethods)}]；允许明文 HTTP [{permissions.AllowInsecureHttp}]。\n" +
                "以上仅为插件声明，不等同于授权。实际权限还必须同时被入口脚本 sidecar 请求并由用户本次明确批准。";
        }
        catch (Exception ex)
        {
            _details.Text = "无法读取插件详情：" + ex.Message;
        }
    }

    private void UpdateButtons()
    {
        var package = SelectedSummary;
        var running = _runner.IsRunning;
        var valid = package is not null && package.ValidationError is null;
        _installDirectoryButton.Enabled = !running;
        _installZipButton.Enabled = !running;
        _refreshButton.Enabled = !running;
        _enableButton.Enabled = !running && package is not null && !package.Enabled && valid;
        _disableButton.Enabled = !running && package is not null && package.Enabled;
        _uninstallButton.Enabled = !running && package is not null && !package.Enabled;
        _runButton.Enabled = running == false && package is not null && package.Enabled && valid;
        _pauseButton.Enabled = running;
        _pauseButton.Text = _runner.IsPaused ? "继续" : "暂停";
        _stopButton.Enabled = running;
    }

    private void SelectPackage(string pluginId, string version)
    {
        for (var i = 0; i < _packages.Items.Count; i++)
        {
            if (_packages.Items[i] is PackageChoice choice &&
                choice.Summary.PluginId == pluginId && choice.Summary.Version == version)
            {
                _packages.SelectedIndex = i;
                return;
            }
        }
    }

    private void AppendLog(string message)
    {
        if (IsDisposed)
            return;
        _log.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}");
        _log.SelectionStart = _log.TextLength;
        _log.ScrollToCaret();
    }

    private void PostToUi(Action action)
    {
        if (IsDisposed || !IsHandleCreated)
            return;
        try
        {
            if (InvokeRequired)
                BeginInvoke(action);
            else
                action();
        }
        catch (ObjectDisposedException) { }
        catch (InvalidOperationException) { }
    }

    private void ShowError(string title, Exception ex) =>
        MessageBox.Show(this, ex.Message, title, MessageBoxButtons.OK, MessageBoxIcon.Error);

    private void HandleClosing(object? sender, FormClosingEventArgs e)
    {
        if (!_runner.IsRunning)
            return;
        e.Cancel = true;
        _closeWhenStopped = true;
        StopRun();
    }
}
