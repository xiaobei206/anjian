using Anjian.Core;
using Anjian.Core.Api;
using Anjian.Core.Packaging;
using PaddleOcrNet.Extraction;
using PaddleOcrNet.Models;
using PaddleOcrNet.Services;

namespace Anjian.App.Controls;

internal sealed class SpritePackageBuilderForm : Form
{
    private readonly TextBox _projectRoot = new() { ReadOnly = true, Dock = DockStyle.Fill };
    private readonly TextBox _runtimeRoot = new() { ReadOnly = true, Dock = DockStyle.Fill };
    private readonly TextBox _outputRoot = new() { ReadOnly = true, Dock = DockStyle.Fill };
    private readonly TextBox _productName = new() { Text = "小精灵", Dock = DockStyle.Fill };
    private readonly TextBox _version = new() { Text = "1.0.0", Dock = DockStyle.Fill };
    private readonly TextBox _description = new() { Dock = DockStyle.Fill };
    private readonly TextBox _author = new() { Dock = DockStyle.Fill };
    private readonly TextBox _copyright = new() { Dock = DockStyle.Fill };
    private readonly TextBox _about = new() { Dock = DockStyle.Fill, Multiline = true, Height = 58, ScrollBars = ScrollBars.Vertical };
    private readonly TextBox _iconPath = new() { Dock = DockStyle.Fill, ReadOnly = true };
    private readonly ListBox _entries = new() { Dock = DockStyle.Fill, Height = 92, IntegralHeight = false };
    private readonly CheckBox _ocrChinese = new() { Text = "简体中文（ch）", AutoSize = true };
    private readonly CheckBox _ocrEnglish = new() { Text = "英语（en）", AutoSize = true };
    private readonly CheckBox _ocrJapanese = new() { Text = "日语（japan）", AutoSize = true };
    private readonly NumericUpDown _repeat = new() { Minimum = 0, Maximum = PlaybackOptions.MaximumRepeatCount, Value = 1, Width = 110 };
    private readonly NumericUpDown _interval = new() { Minimum = 0, Maximum = PlaybackOptions.MaximumIntervalMilliseconds, Increment = 100, Value = 2000, Width = 110 };
    private readonly NumericUpDown _retries = new() { Minimum = 0, Maximum = 20, Value = 0, Width = 70 };
    private readonly TextBox _hotkey = new() { Text = "Ctrl+Alt+F10", Width = 150 };
    private readonly CheckBox _autoStart = new() { Text = "启动后自动运行", AutoSize = true };
    private readonly CheckBox _singleInstance = new() { Text = "仅允许一个实例", Checked = true, AutoSize = true };
    private readonly CheckBox _advancedLog = new() { Text = "默认显示详细日志", AutoSize = true };
    private readonly CheckBox _cleanup = new() { Text = "退出时清理运行资源（安全必需）", Checked = true, Enabled = false, AutoSize = true };
    private readonly Label _status = new() { AutoSize = true, MaximumSize = new Size(820, 0) };
    private readonly Button _build = new() { Text = "生成文件夹包 + EXE 启动包", AutoSize = true };
    private bool _building;

    public SpritePackageBuilderForm(string projectRoot)
    {
        Text = "按键精灵 2026 · 小精灵打包器";
        StartPosition = FormStartPosition.CenterParent;
        MinimumSize = new Size(800, 720);
        Size = new Size(920, 850);
        _projectRoot.Text = Path.GetFullPath(projectRoot);
        _runtimeRoot.Text = AppContext.BaseDirectory;
        _outputRoot.Text = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "AnjianSprites");

        var table = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            ColumnCount = 2,
            Padding = new Padding(12),
        };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        AddPathRow(table, "脚本工程目录", _projectRoot, SelectProjectRoot);
        AddPathRow(table, "自包含运行时目录", _runtimeRoot, SelectRuntimeRoot);
        AddPathRow(table, "输出目录", _outputRoot, SelectOutputRoot);
        AddEntryRow(table);
        AddRow(table, "产品名称", _productName);
        AddRow(table, "产品版本", _version);
        AddRow(table, "产品说明", _description);
        AddRow(table, "作者", _author);
        AddRow(table, "版权信息", _copyright);
        AddRow(table, "关于内容", _about);
        AddPathRow(table, "产品图标（可选）", _iconPath, SelectIcon);

        var ocrChoices = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, WrapContents = true };
        LoadOcrAvailability();
        ocrChoices.Controls.AddRange([_ocrChinese, _ocrEnglish, _ocrJapanese]);
        AddRow(table, "随包携带 OCR", ocrChoices);

        var runtimeOptions = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, WrapContents = true };
        runtimeOptions.Controls.Add(new Label { Text = "重复次数（0=无限）", AutoSize = true, Margin = new Padding(2, 7, 2, 0) });
        runtimeOptions.Controls.Add(_repeat);
        runtimeOptions.Controls.Add(new Label { Text = "轮间隔(ms)", AutoSize = true, Margin = new Padding(10, 7, 2, 0) });
        runtimeOptions.Controls.Add(_interval);
        runtimeOptions.Controls.Add(new Label { Text = "失败重试", AutoSize = true, Margin = new Padding(10, 7, 2, 0) });
        runtimeOptions.Controls.Add(_retries);
        runtimeOptions.Controls.Add(new Label { Text = "启动热键", AutoSize = true, Margin = new Padding(10, 7, 2, 0) });
        runtimeOptions.Controls.Add(_hotkey);
        runtimeOptions.Controls.Add(_autoStart);
        runtimeOptions.Controls.Add(_singleInstance);
        runtimeOptions.Controls.Add(_advancedLog);
        runtimeOptions.Controls.Add(_cleanup);
        AddRow(table, "运行选项", runtimeOptions);

        var bottom = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 78,
            Padding = new Padding(12, 8, 12, 8),
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = true
        };
        bottom.Controls.Add(_build);
        bottom.Controls.Add(_status);
        Controls.Add(table);
        Controls.Add(bottom);

        _build.Click += async (_, _) => await BuildPackageAsync();
        _status.Text = "先选择至少一个入口脚本。运行时目录必须来自完整 self-contained win-x64 发布输出；本窗体不会隐式下载 OCR 模型。";
    }

    private void AddEntryRow(TableLayoutPanel table)
    {
        var panel = new Panel { Dock = DockStyle.Fill, Height = 120 };
        var buttons = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 34 };
        var add = new Button { Text = "添加入口脚本…", AutoSize = true };
        var remove = new Button { Text = "移除选中", AutoSize = true };
        add.Click += (_, _) => AddEntryScripts();
        remove.Click += (_, _) =>
        {
            var selected = _entries.SelectedItem;
            if (selected is not null) _entries.Items.Remove(selected);
        };
        buttons.Controls.Add(add);
        buttons.Controls.Add(remove);
        panel.Controls.Add(_entries);
        panel.Controls.Add(buttons);
        AddRow(table, "入口脚本（可多选）", panel, 126);
    }

    private static void AddRow(TableLayoutPanel table, string label, Control control, int height = 36)
    {
        var row = table.RowCount++;
        table.RowStyles.Add(new RowStyle(SizeType.Absolute, height));
        table.Controls.Add(new Label
        {
            Text = label,
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            Margin = new Padding(3, 8, 8, 4)
        }, 0, row);
        control.Margin = new Padding(3, 3, 6, 3);
        table.Controls.Add(control, 1, row);
    }

    private void AddPathRow(TableLayoutPanel table, string label, TextBox box, Action browse)
    {
        var panel = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2 };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 86));
        var button = new Button { Text = "浏览…", Dock = DockStyle.Fill };
        button.Click += (_, _) => browse();
        panel.Controls.Add(box, 0, 0);
        panel.Controls.Add(button, 1, 0);
        AddRow(table, label, panel);
    }

    private void SelectProjectRoot()
    {
        using var picker = new FolderBrowserDialog { SelectedPath = _projectRoot.Text };
        if (picker.ShowDialog(this) == DialogResult.OK)
            _projectRoot.Text = picker.SelectedPath;
    }

    private void SelectRuntimeRoot()
    {
        using var picker = new FolderBrowserDialog { SelectedPath = _runtimeRoot.Text };
        if (picker.ShowDialog(this) == DialogResult.OK)
            _runtimeRoot.Text = picker.SelectedPath;
    }

    private void SelectOutputRoot()
    {
        using var picker = new FolderBrowserDialog { SelectedPath = _outputRoot.Text };
        if (picker.ShowDialog(this) == DialogResult.OK)
            _outputRoot.Text = picker.SelectedPath;
    }

    private void SelectIcon()
    {
        using var picker = new OpenFileDialog { Filter = "Windows 图标 (*.ico)|*.ico|所有文件 (*.*)|*.*" };
        if (picker.ShowDialog(this) == DialogResult.OK)
            _iconPath.Text = picker.FileName;
    }

    private void AddEntryScripts()
    {
        using var picker = new OpenFileDialog
        {
            Filter = "Lua 脚本 (*.lua)|*.lua|所有文件 (*.*)|*.*",
            Multiselect = true,
            InitialDirectory = Directory.Exists(_projectRoot.Text) ? _projectRoot.Text : AppContext.BaseDirectory
        };
        if (picker.ShowDialog(this) != DialogResult.OK)
            return;

        foreach (var path in picker.FileNames)
        {
            var fullPath = Path.GetFullPath(path);
            if (!_entries.Items.Cast<string>().Any(existing =>
                string.Equals(existing, fullPath, StringComparison.OrdinalIgnoreCase)))
                _entries.Items.Add(fullPath);
        }
    }

    private void LoadOcrAvailability()
    {
        var options = OcrModelPaths.CreateServiceOptions(offline: true);
        var cache = PaddleOcrModels.GetCacheDirectory(options);
        SetAvailability(_ocrChinese, "ch", cache, OcrLanguageExtensions.FromCode("ch"), options);
        SetAvailability(_ocrEnglish, "en", cache, OcrLanguageExtensions.FromCode("en"), options);
        SetAvailability(_ocrJapanese, "japan", cache, OcrLanguageExtensions.FromCode("japan"), options);
    }

    private static void SetAvailability(
        CheckBox checkBox,
        string code,
        string cacheDirectory,
        OcrLanguage language,
        PaddleOcrServiceOptions options)
    {
        try
        {
            var files = PaddleOcrModels.GetModelFileNames(new[] { language }, PaddleModelSet.Ocr, options);
            var present = files.All(file => File.Exists(Path.Combine(cacheDirectory, file)));
            checkBox.Enabled = present;
            checkBox.Text += present ? "（模型已就绪）" : "（未完整下载）";
            checkBox.Tag = code;
        }
        catch
        {
            checkBox.Enabled = false;
            checkBox.Text += "（不可用）";
        }
    }

    private async Task BuildPackageAsync()
    {
        if (_building) return;
        if (_entries.Items.Count == 0)
        {
            MessageBox.Show(this, "至少添加一个 Lua 入口脚本。", "小精灵打包", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        _building = true;
        _build.Enabled = false;
        _status.Text = "正在收集依赖、校验资源并生成发布包……";
        var stageRoot = Path.Combine(Path.GetTempPath(), "AnjianSpriteOcr-" + Guid.NewGuid().ToString("N"));
        try
        {
            var modelDirectories = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var selected = new[]
            {
                (_ocrChinese, "ch"),
                (_ocrEnglish, "en"),
                (_ocrJapanese, "japan")
            }.Where(x => x.Item1.Checked).ToArray();

            if (selected.Length > 0)
                Directory.CreateDirectory(stageRoot);

            var modelOptions = OcrModelPaths.CreateServiceOptions(offline: true);
            var modelCache = PaddleOcrModels.GetCacheDirectory(modelOptions);
            foreach (var (checkBox, code) in selected)
            {
                var language = OcrLanguageExtensions.FromCode(code);
                var files = PaddleOcrModels.GetModelFileNames(new[] { language }, PaddleModelSet.Ocr, modelOptions);
                var missing = files.Where(file => !File.Exists(Path.Combine(modelCache, file))).ToArray();
                if (missing.Length > 0)
                    throw new InvalidDataException($"OCR {code} 模型不完整，缺少：{string.Join("、", missing)}");

                var languageDirectory = Path.Combine(stageRoot, code);
                foreach (var file in files)
                {
                    var source = Path.GetFullPath(Path.Combine(modelCache, file));
                    var relative = Path.GetRelativePath(modelCache, source);
                    if (Path.IsPathRooted(relative) || relative == ".." ||
                        relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal))
                        throw new InvalidDataException("OCR 模型路径越界：" + file);
                    var destination = Path.Combine(languageDirectory, relative);
                    Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                    File.Copy(source, destination, overwrite: false);
                }
                modelDirectories.Add(code, languageDirectory);
            }

            var registry = LuaApiRegistry.Load();
            var options = new SpritePackageBuildOptions
            {
                RuntimeDirectory = _runtimeRoot.Text,
                ProjectRoot = _projectRoot.Text,
                EntryScriptPaths = _entries.Items.Cast<string>().ToArray(),
                OutputDirectory = _outputRoot.Text,
                ProductName = _productName.Text,
                Version = _version.Text,
                Description = _description.Text,
                Author = _author.Text,
                Copyright = _copyright.Text,
                AboutText = string.IsNullOrWhiteSpace(_about.Text) ? null : _about.Text,
                IconPath = string.IsNullOrWhiteSpace(_iconPath.Text) ? null : _iconPath.Text,
                MinimumApiVersion = registry.ApiVersion,
                MinimumRuntimeVersion = "10.0",
                RuntimeOptions = new SpritePackageRuntimeOptions(
                    (int)_repeat.Value,
                    (int)_interval.Value,
                    string.IsNullOrWhiteSpace(_hotkey.Text) ? null : _hotkey.Text.Trim(),
                    _autoStart.Checked,
                    _singleInstance.Checked,
                    (int)_retries.Value,
                    _advancedLog.Checked,
                    _cleanup.Checked),
                OcrModelDirectories = modelDirectories
            };

            var result = await Task.Run(() => SpritePackageBuilder.Build(options));
            var warnings = result.Warnings.Count == 0
                ? "无"
                : string.Join(Environment.NewLine, result.Warnings);
            _status.Text = $"打包成功：{Environment.NewLine}文件夹包：{result.PackageDirectory}{Environment.NewLine}EXE 启动包：{result.LauncherZip}{Environment.NewLine}ZIP SHA-256：{result.LauncherZipChecksumPath}{Environment.NewLine}警告：{warnings}";
            MessageBox.Show(this, _status.Text, "小精灵打包成功", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            _status.Text = "打包失败：" + ex.Message;
            MessageBox.Show(this, ex.Message, "小精灵打包失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            try { if (Directory.Exists(stageRoot)) Directory.Delete(stageRoot, recursive: true); } catch (IOException) { }
            _build.Enabled = true;
            _building = false;
        }
    }
}