using Anjian.Core.Imaging;
using System.ComponentModel;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace Anjian.App;

internal sealed class ImageResourceManagerForm : Form
{
    private readonly ImageResourceCatalog _catalog;
    private readonly string _scriptText;
    private readonly DataGridView _grid = new();
    private readonly ResourcePreviewPanel _preview = new();
    private readonly Label _status = new();
    private ImageResourceManifest _manifest;
    public bool HasChanges { get; private set; }

    public ImageResourceManagerForm(string scriptPath, string scriptText)
    {
        _catalog = new ImageResourceCatalog(scriptPath);
        _scriptText = scriptText;
        _manifest = _catalog.Load();
        Text = "图像模板资源管理";
        StartPosition = FormStartPosition.CenterParent;
        MinimumSize = new Size(900, 560);
        Size = new Size(1100, 680);
        Font = new Font("Microsoft YaHei UI", 9F);
        BuildUi();
        RefreshResources();
    }

    private void BuildUi()
    {
        var toolbar = new FlowLayoutPanel
        {
            Dock = DockStyle.Top, Height = 44, Padding = new Padding(8, 6, 8, 4),
            FlowDirection = FlowDirection.LeftToRight, WrapContents = false, AutoScroll = true
        };
        AddButton(toolbar, "导入 PNG", ImportImages);
        AddButton(toolbar, "重命名", RenameSelected);
        AddButton(toolbar, "移除", RemoveSelected);
        AddButton(toolbar, "校验全部", ValidateAll);
        AddButton(toolbar, "复制相对路径", CopySelectedPath);

        var split = new SplitContainer
        {
            Dock = DockStyle.Fill, SplitterDistance = 670, Panel1MinSize = 420, Panel2MinSize = 220
        };
        _grid.Dock = DockStyle.Fill;
        _grid.ReadOnly = true;
        _grid.AllowUserToAddRows = false;
        _grid.AllowUserToDeleteRows = false;
        _grid.AllowUserToResizeRows = false;
        _grid.MultiSelect = false;
        _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        _grid.RowHeadersVisible = false;
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Name", HeaderText = "名称", FillWeight = 20 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Size", HeaderText = "尺寸", FillWeight = 14 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Path", HeaderText = "相对路径", FillWeight = 40 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Hash", HeaderText = "SHA-256（前 12 位）", FillWeight = 25 });
        _grid.SelectionChanged += (_, _) => ShowSelectedPreview();
        split.Panel1.Controls.Add(_grid);

        var previewPanel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(8) };
        _preview.Dock = DockStyle.Fill;
        _preview.BackColor = Color.FromArgb(245, 246, 248);
        var previewTitle = new Label { Text = "模板预览", Dock = DockStyle.Top, Height = 28, Font = new Font(Font, FontStyle.Bold) };
        previewPanel.Controls.Add(_preview);
        previewPanel.Controls.Add(previewTitle);
        split.Panel2.Controls.Add(previewPanel);

        _status.Dock = DockStyle.Bottom;
        _status.Height = 42;
        _status.Padding = new Padding(10, 6, 10, 4);
        _status.TextAlign = ContentAlignment.MiddleLeft;
        Controls.Add(split);
        Controls.Add(_status);
        Controls.Add(toolbar);
    }

    private static void AddButton(Control parent, string text, Action action)
    {
        var button = new Button { Text = text, AutoSize = true, Height = 29, Margin = new Padding(2) };
        button.Click += (_, _) => action();
        parent.Controls.Add(button);
    }

    private void ImportImages()
    {
        using var dialog = new OpenFileDialog
        {
            Title = "选择 PNG 模板",
            Filter = "PNG 图像 (*.png)|*.png",
            Multiselect = true,
            CheckFileExists = true
        };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        try
        {
            foreach (var path in dialog.FileNames)
                _catalog.ImportPng(path, Path.GetFileNameWithoutExtension(path));
            HasChanges = true;
            RefreshResources();
            _status.Text = $"已导入 {dialog.FileNames.Length} 个模板。资源保存在脚本旁的 .assets 目录。";
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "导入失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void RenameSelected()
    {
        if (SelectedEntry() is not { } entry) return;
        using var dialog = new ResourceNameDialog(entry.Name);
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        try
        {
            _manifest = _catalog.Rename(entry.Id, dialog.Value);
            HasChanges = true;
            RefreshResources(entry.Id);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "重命名失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void RemoveSelected()
    {
        if (SelectedEntry() is not { } entry) return;
        var assetPath = Path.GetFullPath(_catalog.ResolveEntryPath(entry.RelativePath));
        var dependencies = ImageResourceDependencyScanner.Scan(_scriptText);
        var references = dependencies.LiteralPaths.Any(literal =>
            Path.IsPathRooted(literal)
                ? string.Equals(Path.GetFullPath(literal), assetPath, StringComparison.OrdinalIgnoreCase)
                : string.Equals(Path.GetFullPath(Path.Combine(Path.GetDirectoryName(_catalog.ManifestPath)!, literal)),
                    assetPath, StringComparison.OrdinalIgnoreCase));
        if (references)
        {
            MessageBox.Show(this, "当前脚本的 LoadImage 字面路径仍引用此文件。请先修改脚本依赖，再移除资源。",
                "资源仍被脚本引用", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        if (dependencies.DynamicLoadImageCalls > 0
            && MessageBox.Show(this,
                $"当前脚本还有 {dependencies.DynamicLoadImageCalls} 个动态 LoadImage 调用，无法静态确认是否引用该资源。仍要从清单移除“{entry.Name}”吗？PNG 文件会保留。",
                "存在无法静态解析的资源依赖", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
            return;
        if (MessageBox.Show(this, $"从资源清单移除“{entry.Name}”？原 PNG 文件会保留在资产目录中，不会自动删除。",
            "移除资源", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
        try
        {
            _manifest = _catalog.Remove(entry.Id);
            HasChanges = true;
            RefreshResources();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "移除失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void ValidateAll()
    {
        try
        {
            var results = _catalog.ValidateAll();
            var valid = results.Count(x => x.Status == "valid");
            var failures = results.Count - valid;
            _status.Text = $"资源清单 schema={ImageResourceCatalog.CurrentSchemaVersion}；有效 {valid} / {results.Count}，异常 {failures}；动态 LoadImage 调用 {ImageResourceDependencyScanner.Scan(_scriptText).DynamicLoadImageCalls} 个无法静态解析。";
            if (failures > 0)
            {
                var details = string.Join(Environment.NewLine, results.Where(x => x.Status != "valid")
                    .Select(x => $"{x.Name}: {x.Status} - {x.Message}"));
                MessageBox.Show(this, details, "资源校验结果", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "校验失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void CopySelectedPath()
    {
        if (SelectedEntry() is not { } entry) return;
        Clipboard.SetText(entry.RelativePath);
        _status.Text = $"已复制资源相对路径；在脚本中可使用 LoadImage({(char)34}{entry.RelativePath}{(char)34})。";
    }

    private void RefreshResources(string? selectId = null)
    {
        _manifest = _catalog.Load();
        _grid.Rows.Clear();
        foreach (var entry in _manifest.Resources)
        {
            var index = _grid.Rows.Add(entry.Name, $"{entry.Width} × {entry.Height}", entry.RelativePath,
                entry.Sha256[..12]);
            _grid.Rows[index].Tag = entry;
            if (entry.Id == selectId) _grid.Rows[index].Selected = true;
        }
        if (_grid.Rows.Count > 0 && _grid.SelectedRows.Count == 0)
            _grid.Rows[0].Selected = true;
        _status.Text = $"schemaVersion={_manifest.SchemaVersion}；资源 {_manifest.Resources.Count} 项；清单 {_catalog.ManifestPath}";
        if (_grid.Rows.Count == 0)
            _preview.Image = null;
        else
            ShowSelectedPreview();
    }

    private ImageResourceEntry? SelectedEntry() =>
        _grid.SelectedRows.Count == 0 ? null : _grid.SelectedRows[0].Tag as ImageResourceEntry;

    private void ShowSelectedPreview()
    {
        if (SelectedEntry() is not { } entry)
        {
            _preview.Image = null;
            return;
        }
        try
        {
            var validation = _catalog.ValidateAll().FirstOrDefault(x => x.ResourceId == entry.Id);
            if (validation?.Status != "valid")
            {
                _preview.Image = null;
                _status.Text = $"资源不可预览：{validation?.Status ?? "missing"} - {validation?.Message}";
                return;
            }
            _preview.Image = Services.WindowsImageService.ReadPreviewPixels(_catalog.ResolveEntryPath(entry.RelativePath));
            _preview.Invalidate();
        }
        catch (Exception ex)
        {
            _preview.Image = null;
            _status.Text = "预览失败：" + ex.Message;
        }
    }
}

internal sealed class ResourcePreviewPanel : Panel
{
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public PixelImage? Image { get; set; }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        if (Image is null) return;
        var pixels = Image.CopyArgbPixels();
        using var bitmap = new Bitmap(Image.Width, Image.Height, PixelFormat.Format32bppArgb);
        var data = bitmap.LockBits(new Rectangle(0, 0, bitmap.Width, bitmap.Height),
            ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
        try
        {
            var row = new byte[checked(bitmap.Width * 4)];
            for (var y = 0; y < bitmap.Height; y++)
            {
                for (var x = 0; x < bitmap.Width; x++)
                {
                    var pixel = pixels[y * bitmap.Width + x];
                    var offset = x * 4;
                    row[offset] = (byte)pixel;
                    row[offset + 1] = (byte)(pixel >> 8);
                    row[offset + 2] = (byte)(pixel >> 16);
                    row[offset + 3] = (byte)(pixel >> 24);
                }
                Marshal.Copy(row, 0, IntPtr.Add(data.Scan0, y * data.Stride), row.Length);
            }
        }
        finally { bitmap.UnlockBits(data); }

        var available = ClientRectangle;
        available.Inflate(-14, -14);
        if (available.Width <= 0 || available.Height <= 0) return;
        var scale = Math.Min((double)available.Width / Image.Width, (double)available.Height / Image.Height);
        var width = Math.Max(1, (int)(Image.Width * scale));
        var height = Math.Max(1, (int)(Image.Height * scale));
        var target = new Rectangle(available.Left + (available.Width - width) / 2,
            available.Top + (available.Height - height) / 2, width, height);
        e.Graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
        e.Graphics.DrawImage(bitmap, target);
    }
}

internal sealed class ResourceNameDialog : Form
{
    private readonly TextBox _name = new();
    public string Value => _name.Text.Trim();

    public ResourceNameDialog(string initial)
    {
        Text = "重命名图像资源";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MinimizeBox = false;
        MaximizeBox = false;
        ShowInTaskbar = false;
        ClientSize = new Size(380, 118);
        Controls.Add(new Label { Text = "资源显示名称（最多 80 个字符）：", Left = 12, Top = 12, AutoSize = true });
        _name.SetBounds(12, 38, 356, 24);
        _name.MaxLength = 80;
        _name.Text = initial;
        Controls.Add(_name);
        var ok = new Button { Text = "确定", DialogResult = DialogResult.OK, Left = 210, Top = 78, Width = 76 };
        var cancel = new Button { Text = "取消", DialogResult = DialogResult.Cancel, Left = 292, Top = 78, Width = 76 };
        Controls.Add(ok); Controls.Add(cancel);
        AcceptButton = ok; CancelButton = cancel;
    }
}
