using Anjian.App.Services;
using System.Runtime.InteropServices;

namespace Anjian.App;

internal sealed class VisualDebugOverlayForm : Form
{
    private const int WsExTransparent = 0x00000020;
    private const int WsExLayered = 0x00080000;
    private const int WsExToolWindow = 0x00000080;
    private const int WsExNoActivate = 0x08000000;
    private const int WmNcHitTest = 0x0084;
    private const int HtTransparent = -1;
    private const uint WdaExcludeFromCapture = 0x00000011;
    private readonly List<ImageVisualDebugEvent> _items = [];
    public bool CaptureExclusionSupported { get; private set; }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowDisplayAffinity(IntPtr hwnd, uint affinity);

    public VisualDebugOverlayForm()
    {
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        TopMost = true;
        BackColor = Color.Magenta;
        TransparencyKey = Color.Magenta;
        Bounds = SystemInformation.VirtualScreen;
        DoubleBuffered = true;
    }

    protected override bool ShowWithoutActivation => true;

    protected override CreateParams CreateParams
    {
        get
        {
            var parameters = base.CreateParams;
            parameters.ExStyle |= WsExTransparent | WsExLayered | WsExToolWindow | WsExNoActivate;
            return parameters;
        }
    }

    public void ShowOverlay()
    {
        Bounds = SystemInformation.VirtualScreen;
        if (!Visible) Show();
        if (IsHandleCreated)
            CaptureExclusionSupported = SetWindowDisplayAffinity(Handle, WdaExcludeFromCapture);
        Invalidate();
    }

    public void HideOverlay()
    {
        _items.Clear();
        Hide();
    }

    public void Add(ImageVisualDebugEvent value)
    {
        if (!Visible || !value.HasScreenCoordinates) return;
        _items.Add(value);
        if (_items.Count > 40) _items.RemoveRange(0, _items.Count - 40);
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var virtualScreen = SystemInformation.VirtualScreen;
        foreach (var item in _items)
        {
            using var pen = new Pen(item.Kind == "block" ? Color.Orange : Color.LimeGreen, 2);
            using var brush = new SolidBrush(Color.FromArgb(235, 20, 25, 30));
            using var textBrush = new SolidBrush(item.Kind == "block" ? Color.Orange : Color.White);
            var x = item.X - virtualScreen.Left;
            var y = item.Y - virtualScreen.Top;
            var rect = new Rectangle(x, y, Math.Max(1, item.Width), Math.Max(1, item.Height));
            e.Graphics.DrawRectangle(pen, rect);
            var label = item.Label;
            if (item.Similarity.HasValue) label += $" · {item.Similarity.Value:P1}";
            if (item.Color.HasValue)
            {
                var red = (item.Color.Value >> 16) & 0xFF;
                var green = (item.Color.Value >> 8) & 0xFF;
                var blue = item.Color.Value & 0xFF;
                label += $" · RGB({red}, {green}, {blue}) · #{item.Color.Value:X6}";
            }
            if (item.DurationMilliseconds > 0) label += $" · {item.DurationMilliseconds}ms";
            var labelSize = e.Graphics.MeasureString(label, Font);
            var labelRect = new RectangleF(rect.Left, Math.Max(0, rect.Top - labelSize.Height - 4),
                Math.Min(ClientSize.Width - Math.Max(0, rect.Left), labelSize.Width + 8), labelSize.Height + 4);
            e.Graphics.FillRectangle(brush, labelRect);
            e.Graphics.DrawString(label, Font, textBrush, labelRect.Left + 4, labelRect.Top + 2);
        }
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WmNcHitTest)
        {
            m.Result = new IntPtr(HtTransparent);
            return;
        }
        base.WndProc(ref m);
    }
}
