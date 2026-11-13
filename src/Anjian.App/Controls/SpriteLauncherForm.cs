using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Anjian.App.Services;
using Anjian.Core;
using Anjian.Core.Api;
using Anjian.Core.Packaging;
using Anjian.Core.Parameters;
using Anjian.Core.Permissions;

namespace Anjian.App.Controls;

internal sealed class SpriteLauncherForm : Form
{
    private const int HotkeyMessage = 0x0312;
    private const int HotkeyId = 0xA26;
    private const uint ModifierAlt = 0x0001;
    private const uint ModifierControl = 0x0002;
    private const uint ModifierShift = 0x0004;
    private const uint ModifierWindows = 0x0008;
    private const uint ModifierNoRepeat = 0x4000;

    private sealed record EntryChoice(SpritePackageEntry Entry)
    {
        public override string ToString() => Entry.DisplayName;
    }

    private readonly string _packageRoot;
    private readonly SpritePackageManifest _manifest;
    private readonly PlaybackRunner _runner = new();
    private readonly ComboBox _entry = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 260 };
    private readonly NumericUpDown _repeat = new() { Minimum = 0, Maximum = PlaybackOptions.MaximumRepeatCount, Width = 100 };
    private readonly NumericUpDown _interval = new() { Minimum = 0, Maximum = PlaybackOptions.MaximumIntervalMilliseconds, Increment = 100, Width = 100 };
    private readonly Button _start = new() { Text = "开始运行", AutoSize = true };
    private readonly Button _pause = new() { Text = "暂停", AutoSize = true, Enabled = false };
    private readonly Button _stop = new() { Text = "停止", AutoSize = true, Enabled = false };
    private readonly Button _about = new() { Text = "关于", AutoSize = true };
    private readonly Label _status = new() { AutoSize = true, Text = "就绪" };
    private readonly RichTextBox _log = new()
    {
        Dock = DockStyle.Fill,
        ReadOnly = true,
        DetectUrls = false,
        WordWrap = true,
        BackColor = Color.FromArgb(24, 28, 34),
        ForeColor = Color.Gainsboro,
        Font = new Font("Consolas", 9F)
    };
    private Mutex? _instanceMutex;
    private bool _ownsMutex;
    private bool _duplicateInstance;
    private bool _hotkeyRegistered;
    private CancellationTokenSource? _launchCts;
    private Icon? _packageIcon;

    public SpriteLauncherForm(string manifestPath)
    {
        _packageRoot = Path.GetFullPath(Path.GetDirectoryName(manifestPath)
            ?? throw new InvalidDataException("manifest 路径无效。"));
        var issues = SpritePackageBuilder.Verify(_packageRoot);
        if (issues.Count > 0)
            throw new InvalidDataException("发布包完整性校验失败：" + string.Join(Environment.NewLine, issues.Take(20)));

        var jsonOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        _manifest = JsonSerializer.Deserialize<SpritePackageManifest>(File.ReadAllText(manifestPath), jsonOptions)
            ?? throw new InvalidDataException("小精灵 manifest 为空。");
        ValidateMinimumVersions(_manifest);

        Text = _manifest.ProductName + " · 小精灵";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(680, 440);
        Size = new Size(820, 560);
        MinimizeBox = true;
        MaximizeBox = true;

        if (!string.IsNullOrWhiteSpace(_manifest.IconPath))
        {
            var iconPath = ResolvePackagePath(_manifest.IconPath);
            if (File.Exists(iconPath))
            {
                try
                {
                    _packageIcon = new Icon(iconPath);
                    Icon = _packageIcon;
                }
                catch (Exception)
                {
                    // A malformed optional icon must not block an otherwise valid package.
                }
            }
        }

        foreach (var entry in _manifest.Entries)
            _entry.Items.Add(new EntryChoice(entry));
        if (_entry.Items.Count > 0)
            _entry.SelectedIndex = 0;

        _repeat.Value = Math.Clamp(_manifest.RuntimeOptions.RepeatCount, 0, PlaybackOptions.MaximumRepeatCount);
        _interval.Value = Math.Clamp(_manifest.RuntimeOptions.IntervalMilliseconds, 0, PlaybackOptions.MaximumIntervalMilliseconds);
        _log.Visible = _manifest.RuntimeOptions.AdvancedLog;
        _log.Height = 300;

        var toolbar = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 92,
            Padding = new Padding(12, 10, 12, 6),
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = true
        };
        toolbar.Controls.Add(new Label { Text = "入口", AutoSize = true, Margin = new Padding(3, 8, 3, 0) });
        toolbar.Controls.Add(_entry);
        toolbar.Controls.Add(new Label { Text = "次数(0=无限)", AutoSize = true, Margin = new Padding(8, 8, 3, 0) });
        toolbar.Controls.Add(_repeat);
        toolbar.Controls.Add(new Label { Text = "间隔(ms)", AutoSize = true, Margin = new Padding(8, 8, 3, 0) });
        toolbar.Controls.Add(_interval);
        toolbar.Controls.Add(_start);
        toolbar.Controls.Add(_pause);
        toolbar.Controls.Add(_stop);
        toolbar.Controls.Add(_about);

        var statusPanel = new Panel { Dock = DockStyle.Bottom, Height = 36, Padding = new Padding(12, 6, 12, 4) };
        statusPanel.Controls.Add(_status);
        Controls.Add(_log);
        Controls.Add(statusPanel);
        Controls.Add(toolbar);
        _runner.SetUiOwner(this);

        _runner.Log += AppendLog;
        _start.Click += async (_, _) => await StartSelectedEntryAsync();
        _pause.Click += (_, _) => TogglePause();
        _stop.Click += (_, _) =>
        {
            _launchCts?.Cancel();
            _runner.Stop();
        };
        _about.Click += (_, _) => ShowAbout();

        if (_manifest.RuntimeOptions.SingleInstance)
        {
            var packageKey = HashKey(_manifest.PackageId);
            _instanceMutex = new Mutex(initiallyOwned: true, @"Local\Anjian2026.Sprite." + packageKey, out _ownsMutex);
            _duplicateInstance = !_ownsMutex;
        }

        Shown += (_, _) =>
        {
            if (_duplicateInstance)
            {
                MessageBox.Show(this, "该小精灵已在运行。本次启动不会再打开第二个实例。",
                    _manifest.ProductName, MessageBoxButtons.OK, MessageBoxIcon.Information);
                BeginInvoke(new Action(Close));
                return;
            }

            RegisterStartupHotkey();
            if (_manifest.RuntimeOptions.AutoStart)
                BeginInvoke(new Action(async () => await StartSelectedEntryAsync()));
        };

        FormClosing += (_, _) =>
        {
            _launchCts?.Cancel();
            // Input release and worker termination are mandatory safety cleanup.
            _runner.Stop();
            if (_hotkeyRegistered)
            {
                UnregisterHotKey(Handle, HotkeyId);
                _hotkeyRegistered = false;
            }
        };
        FormClosed += (_, _) =>
        {
            _runner.Dispose();
            _instanceMutex?.Dispose();
            _packageIcon?.Dispose();
        };
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == HotkeyMessage && m.WParam.ToInt32() == HotkeyId)
        {
            if (_launchCts is null && !_runner.IsRunning)
                _ = StartSelectedEntryAsync();
            return;
        }
        base.WndProc(ref m);
    }

    private void RegisterStartupHotkey()
    {
        var hotkey = _manifest.RuntimeOptions.StartupHotkey;
        if (string.IsNullOrWhiteSpace(hotkey))
            return;
        if (!TryParseHotkey(hotkey, out var modifiers, out var key))
        {
            AppendLog("启动热键格式无效：" + hotkey);
            return;
        }
        if (!RegisterHotKey(Handle, HotkeyId, modifiers | ModifierNoRepeat, (uint)key))
        {
            AppendLog($"启动热键注册失败：{hotkey}（Win32 {Marshal.GetLastWin32Error()}）。");
            return;
        }
        _hotkeyRegistered = true;
        AppendLog("已注册启动热键：" + hotkey);
    }

    private static bool TryParseHotkey(string value, out uint modifiers, out Keys key)
    {
        modifiers = 0;
        key = Keys.None;
        var parts = value.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0)
            return false;

        foreach (var part in parts.Take(parts.Length - 1))
        {
            switch (part.ToLowerInvariant())
            {
                case "ctrl":
                case "control": modifiers |= ModifierControl; break;
                case "alt": modifiers |= ModifierAlt; break;
                case "shift": modifiers |= ModifierShift; break;
                case "win":
                case "windows": modifiers |= ModifierWindows; break;
                default: return false;
            }
        }

        if (!Enum.TryParse<Keys>(parts[^1], ignoreCase: true, out key) ||
            key == Keys.None || (key & Keys.Modifiers) != 0)
            return false;
        return modifiers != 0;
    }

    private async Task StartSelectedEntryAsync()
    {
        if (_runner.IsRunning || _launchCts is not null)
            return;
        if (_entry.SelectedItem is not EntryChoice choice)
        {
            MessageBox.Show(this, "没有可运行的入口脚本。", _manifest.ProductName,
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        using var launchCts = new CancellationTokenSource();
        _launchCts = launchCts;
        SetRunningState(true);
        try
        {
            var scriptPath = ResolvePackagePath(choice.Entry.ScriptPath);
            var sidecarIssues = SpritePackageBuilder.VerifyEntryPermissionSidecar(_packageRoot, _manifest, choice.Entry);
            if (sidecarIssues.Count > 0)
                throw new InvalidDataException("小精灵入口权限 sidecar 校验失败，未启动脚本：" +
                    string.Join("；", sidecarIssues));

            if (!ScriptPermissionService.TryReviewPackagedEntry(
                    this, scriptPath,
                    choice.Entry.PermissionManifestPath is null
                        ? null
                        : ResolvePackagePath(choice.Entry.PermissionManifestPath),
                    out var permissions))
            {
                AppendLog("本次权限审阅被拒绝或取消；入口未启动。");
                return;
            }
            if (launchCts.IsCancellationRequested)
                return;

            var parameters = LoadAndCollectParameters(choice.Entry);
            if (parameters is null || launchCts.IsCancellationRequested)
                return;

            var moduleRoot = ResolvePackagePath(choice.Entry.ModuleRootPath);
            var ocrRoot = _manifest.OcrModels.Count > 0
                ? ResolvePackagePath(_manifest.OcrModels[0].RelativePath)
                : null;
            var runtimeOptions = new SpriteWorkerRuntimeOptions(moduleRoot, ocrRoot);
            var playback = new PlaybackOptions
            {
                RepeatCount = (int)_repeat.Value,
                IntervalMilliseconds = (int)_interval.Value
            };

            await RunWithRetriesAsync(scriptPath, playback, parameters, runtimeOptions, permissions, launchCts.Token);
        }
        catch (OperationCanceledException)
        {
            AppendLog("启动/重试已取消。");
        }
        catch (Exception ex)
        {
            AppendLog("启动失败：" + ex);
            MessageBox.Show(this, ex.Message, _manifest.ProductName,
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            _launchCts = null;
            SetRunningState(false);
        }
    }

    private IReadOnlyDictionary<string, JsonElement>? LoadAndCollectParameters(SpritePackageEntry entry)
    {
        if (entry.ParameterSchemaPath is null)
            return new Dictionary<string, JsonElement>(StringComparer.Ordinal);

        var scriptPath = ResolvePackagePath(entry.ScriptPath);
        var schemaPath = ResolvePackagePath(entry.ParameterSchemaPath);
        var schema = ScriptParameterService.LoadSchema(schemaPath);
        if (schema.Parameters.Count == 0)
            return new Dictionary<string, JsonElement>(StringComparer.Ordinal);

        var configurationPath = GetUserConfigurationPath(entry.Id);
        var loaded = ParameterConfigurationStore.LoadOrRecover(configurationPath);
        if (loaded.Warning is not null)
            AppendLog(loaded.Warning);

        var validation = ScriptParameterValidator.ValidateValues(
            schema, loaded.Configuration.Values, Path.GetDirectoryName(scriptPath));
        var initial = validation.IsValid
            ? validation.Values
            : new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        if (!validation.IsValid)
            AppendLog("已保存参数与当前定义不匹配，将在本次参数窗口中重新填写。");

        using var dialog = new ScriptParameterDialog(scriptPath, schema, initial, configurationPath);
        return dialog.ShowDialog(this) == DialogResult.OK ? dialog.Values : null;
    }

    private async Task RunWithRetriesAsync(
        string scriptPath,
        PlaybackOptions playback,
        IReadOnlyDictionary<string, JsonElement> parameters,
        SpriteWorkerRuntimeOptions runtimeOptions,
        ScriptPermissionGrant permissions,
        CancellationToken token)
    {
        var retries = _manifest.RuntimeOptions.MaximumRetries;
        for (var attempt = 0; attempt <= retries; attempt++)
        {
            try
            {
                token.ThrowIfCancellationRequested();
                if (attempt > 0)
                    AppendLog($"运行失败后重试，第 {attempt}/{retries} 次。");
                await _runner.RunAsync(scriptPath, playback, parameters: parameters, spriteRuntimeOptions: runtimeOptions, permissions: permissions);
                token.ThrowIfCancellationRequested();
                return;
            }
            catch (Exception ex)
            {
                AppendLog("脚本运行失败：" + ex.Message);
                if (token.IsCancellationRequested)
                    throw new OperationCanceledException(token);
                if (attempt >= retries)
                    throw;
                await Task.Delay(500, token);
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
            _pause.Text = "暂停";
            _status.Text = "运行中";
            AppendLog("继续运行。");
        }
        else
        {
            _runner.Pause();
            _pause.Text = "继续";
            _status.Text = "已暂停";
            AppendLog("已暂停。");
        }
    }

    private void SetRunningState(bool running)
    {
        if (IsDisposed) return;
        _start.Enabled = !running;
        _entry.Enabled = !running;
        _repeat.Enabled = !running;
        _interval.Enabled = !running;
        _pause.Enabled = running;
        _stop.Enabled = running;
        if (!running)
        {
            _pause.Text = "暂停";
            _status.Text = "就绪";
        }
        else
        {
            _status.Text = "运行中";
        }
    }

    private void AppendLog(string message)
    {
        if (IsDisposed || Disposing) return;
        if (InvokeRequired)
        {
            try { BeginInvoke(new Action<string>(AppendLog), message); } catch (InvalidOperationException) { }
            return;
        }
        _log.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}");
        _log.SelectionStart = _log.TextLength;
        _log.ScrollToCaret();
    }

    private void ShowAbout()
    {
        var lines = new List<string>
        {
            _manifest.ProductName + " " + _manifest.Version,
            _manifest.Description,
            "作者：" + _manifest.Author,
            _manifest.Copyright,
            "",
            _manifest.AboutText ?? ""
        };
        MessageBox.Show(this, string.Join(Environment.NewLine, lines.Where(x => x.Length > 0)),
            "关于 " + _manifest.ProductName, MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private string ResolvePackagePath(string relativePath)
    {
        var fullRoot = Path.GetFullPath(_packageRoot);
        var fullPath = Path.GetFullPath(Path.Combine(fullRoot, relativePath.Replace('/', Path.DirectorySeparatorChar)));
        var prefix = fullRoot.EndsWith(Path.DirectorySeparatorChar) ? fullRoot : fullRoot + Path.DirectorySeparatorChar;
        if (!fullPath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("小精灵文件路径越界：" + relativePath);
        return fullPath;
    }

    private string GetUserConfigurationPath(string entryId)
    {
        var packageKey = HashKey(_manifest.PackageId);
        var entryKey = HashKey(entryId);
        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Anjian2026", "Sprites", packageKey, entryKey + ".json");
    }

    private static string HashKey(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();

    private static void ValidateMinimumVersions(SpritePackageManifest manifest)
    {
        if (!Version.TryParse(manifest.MinimumApiVersion, out var minimumApi))
            throw new InvalidDataException("manifest 的最低 API 版本无效：" + manifest.MinimumApiVersion);
        if (!Version.TryParse(LuaApiRegistry.Load().ApiVersion, out var currentApi))
            throw new InvalidOperationException("当前程序 API 版本无效。");
        if (currentApi < minimumApi)
            throw new InvalidDataException($"该小精灵要求 API {minimumApi}，当前仅支持 {currentApi}。");

        if (!Version.TryParse(manifest.MinimumRuntimeVersion, out var minimumRuntime))
            throw new InvalidDataException("manifest 的最低 .NET 运行时版本无效：" + manifest.MinimumRuntimeVersion);
        if (Environment.Version < minimumRuntime)
            throw new InvalidDataException($"该小精灵要求 .NET {minimumRuntime} 或更高版本。当前运行时：{Environment.Version}。");
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _packageIcon?.Dispose();
            _instanceMutex?.Dispose();
        }
        base.Dispose(disposing);
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(nint windowHandle, int id, uint modifiers, uint virtualKey);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(nint windowHandle, int id);
}