namespace Anjian.App.Services;

internal sealed class IpcMessage
{
    public string Type { get; set; } = "";
    public string? InstanceId { get; set; }
    public string? WorkerAttemptId { get; set; }
    public string? SessionNonce { get; set; }
    public string? Name { get; set; }
    public double X { get; set; }
    public double Y { get; set; }
    public int Milliseconds { get; set; }
    public string? Text { get; set; }
}
