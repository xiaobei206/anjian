using PaddleOcrNet.Extraction;
using PaddleOcrNet.Models;
using PaddleOcrNet.Services;

namespace Anjian.App.Services;

/// <summary>
/// Explicit, user-controlled offline OCR model installation. Recognition never downloads models implicitly.
/// </summary>
internal sealed class OcrModelManagerForm : Form
{
    private sealed record LanguageChoice(string Code, string Label)
    {
        public override string ToString() => Label;
    }

    private readonly ComboBox _language = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 220 };
    private readonly Label _status = new() { AutoSize = true, MaximumSize = new Size(600, 0) };
    private readonly ProgressBar _progress = new() { Width = 580, Height = 20 };
    private readonly Button _download = new() { Text = "下载/补齐模型", Width = 130 };
    private readonly Button _cancel = new() { Text = "取消", Width = 90, Enabled = false };
    private CancellationTokenSource? _downloadCts;
    private bool _closeAfterDownload;

    public OcrModelManagerForm()
    {
        Text = "按键精灵 2026 · OCR 模型管理";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MinimizeBox = false;
        MaximizeBox = false;
        ShowInTaskbar = false;
        ClientSize = new Size(620, 230);

        _language.Items.AddRange([
            new LanguageChoice("ch", "简体中文（ch）"),
            new LanguageChoice("en", "英语（en）"),
            new LanguageChoice("japan", "日语（japan）")
        ]);
        _language.SelectedIndex = 0;
        _language.SelectedIndexChanged += (_, _) => RefreshModelState();

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(12),
            ColumnCount = 1,
            RowCount = 5
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        root.Controls.Add(_language, 0, 0);
        root.Controls.Add(_status, 0, 1);
        root.Controls.Add(_progress, 0, 2);

        var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight };
        buttons.Controls.Add(_download);
        buttons.Controls.Add(_cancel);
        root.Controls.Add(buttons, 0, 4);
        Controls.Add(root);

        _download.Click += async (_, _) => await DownloadSelectedAsync();
        _cancel.Click += (_, _) => _downloadCts?.Cancel();
        Shown += (_, _) => RefreshModelState();
    }

    private OcrLanguage SelectedLanguage()
    {
        var choice = _language.SelectedItem as LanguageChoice
            ?? throw new InvalidOperationException("请选择 OCR 语言。");
        return OcrLanguageExtensions.FromCode(choice.Code);
    }

    private void RefreshModelState()
    {
        try
        {
            var options = OcrModelPaths.CreateServiceOptions(offline: true);
            var files = PaddleOcrModels.GetModelFileNames(
                new[] { SelectedLanguage() }, PaddleModelSet.Ocr, options);
            var directory = PaddleOcrModels.GetCacheDirectory(options);
            var present = files.Count(file => File.Exists(Path.Combine(directory, file)));
            _status.Text = $"模型目录：{directory}{Environment.NewLine}已缓存 {present}/{files.Count} 个所需文件。模型只会在你点击下载时联网获取；脚本执行默认离线。";
        }
        catch (Exception ex)
        {
            _status.Text = "读取模型状态失败：" + ex.Message;
        }
    }

    private async Task DownloadSelectedAsync()
    {
        if (_downloadCts is not null) return;
        var language = SelectedLanguage();
        _downloadCts = new CancellationTokenSource();
        _download.Enabled = false;
        _cancel.Enabled = true;
        _language.Enabled = false;
        _progress.Value = 0;
        _status.Text = "正在按 SHA-256 校验并下载所选语言模型……";

        try
        {
            var options = OcrModelPaths.CreateServiceOptions(offline: false);
            var progress = new Progress<ModelDownloadProgress>(value =>
            {
                if (IsDisposed) return;
                _status.Text = value.TotalBytes > 0
                    ? $"下载 {value.FileName}：{value.BytesDownloaded:N0}/{value.TotalBytes:N0} bytes"
                    : $"下载 {value.FileName}：{value.BytesDownloaded:N0} bytes";
                if (value.Fraction is { } fraction)
                    _progress.Value = (int)Math.Clamp(Math.Round(fraction * 100), 0, 100);
            });
            var report = await PaddleOcrModels.DownloadAsync(
                options, new[] { language }, PaddleModelSet.Ocr, progress, _downloadCts.Token);
            report.EnsureSuccess();
            _progress.Value = 100;
            _status.Text = $"完成：缓存 {report.CachedCount} 个、下载 {report.DownloadedCount} 个文件，共 {report.TotalBytes:N0} bytes。";
        }
        catch (OperationCanceledException)
        {
            _status.Text = "模型下载已取消。";
        }
        catch (Exception ex)
        {
            _status.Text = "模型下载失败：" + ex.Message;
        }
        finally
        {
            _downloadCts?.Dispose();
            _downloadCts = null;
            if (!IsDisposed && !Disposing)
            {
                _download.Enabled = true;
                _cancel.Enabled = false;
                _language.Enabled = true;
                if (_closeAfterDownload)
                {
                    _closeAfterDownload = false;
                    BeginInvoke(new Action(Close));
                }
                else
                {
                    RefreshModelState();
                }
            }
        }
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        base.OnFormClosing(e);
        if (e.Cancel || _downloadCts is null)
            return;

        _closeAfterDownload = true;
        _status.Text = "正在取消模型下载，完成后关闭窗口……";
        _downloadCts.Cancel();
        e.Cancel = true;
    }
}
