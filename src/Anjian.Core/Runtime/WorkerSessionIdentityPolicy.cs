using System.Security.Cryptography;

namespace Anjian.Core.Runtime;

/// <summary>
/// Shared validation for one Worker attempt's pipe identity. The host still binds
/// this identity to the OS named-pipe client PID and the runner-owned pipe handle.
/// </summary>
public static class WorkerSessionIdentityPolicy
{
    public static bool IsValid(string? instanceId, string? workerAttemptId, string? sessionNonce) =>
        IsCanonicalGuidN(instanceId) &&
        IsCanonicalGuidN(workerAttemptId) &&
        IsValidNonce(sessionNonce);

    public static bool Matches(
        string? expectedInstanceId,
        string? expectedWorkerAttemptId,
        string? expectedSessionNonce,
        string? actualInstanceId,
        string? actualWorkerAttemptId,
        string? actualSessionNonce)
    {
        if (!string.Equals(expectedInstanceId, actualInstanceId, StringComparison.Ordinal) ||
            !string.Equals(expectedWorkerAttemptId, actualWorkerAttemptId, StringComparison.Ordinal) ||
            !IsValid(expectedInstanceId, expectedWorkerAttemptId, expectedSessionNonce) ||
            !IsValid(actualInstanceId, actualWorkerAttemptId, actualSessionNonce))
            return false;

        try
        {
            return CryptographicOperations.FixedTimeEquals(
                Convert.FromHexString(expectedSessionNonce!),
                Convert.FromHexString(actualSessionNonce!));
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static bool IsCanonicalGuidN(string? value) =>
        value is not null &&
        Guid.TryParseExact(value, "N", out var parsed) &&
        string.Equals(parsed.ToString("N"), value, StringComparison.Ordinal);

    private static bool IsValidNonce(string? value) =>
        value is { Length: 64 } && value.All(Uri.IsHexDigit);
}
