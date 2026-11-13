using Anjian.Core.Runtime;
using Xunit;

namespace Anjian.Core.Tests;

public sealed class WorkerSessionIdentityPolicyTests
{
    [Fact]
    public void AcceptsOnlyCanonicalInstanceAttemptAndNonceIdentity()
    {
        var instanceId = RunInstanceId.Create().Value;
        var attemptId = Guid.NewGuid().ToString("N");
        var nonce = Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();

        Assert.True(WorkerSessionIdentityPolicy.IsValid(instanceId, attemptId, nonce));
        Assert.False(WorkerSessionIdentityPolicy.IsValid(instanceId.ToUpperInvariant(), attemptId, nonce));
        Assert.False(WorkerSessionIdentityPolicy.IsValid(instanceId, attemptId.ToUpperInvariant(), nonce));
        Assert.False(WorkerSessionIdentityPolicy.IsValid(instanceId, attemptId, nonce[..62]));
        Assert.False(WorkerSessionIdentityPolicy.IsValid(instanceId, attemptId, new string('z', 64)));
        Assert.False(WorkerSessionIdentityPolicy.IsValid(null, attemptId, nonce));
    }

    [Fact]
    public void RejectsCrossInstanceCrossAttemptAndForgedOrExpiredNonce()
    {
        var instanceId = RunInstanceId.Create().Value;
        var otherInstanceId = RunInstanceId.Create().Value;
        var attemptId = Guid.NewGuid().ToString("N");
        var otherAttemptId = Guid.NewGuid().ToString("N");
        var nonce = Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
        var forgedNonce = Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();

        Assert.True(WorkerSessionIdentityPolicy.Matches(
            instanceId, attemptId, nonce, instanceId, attemptId, nonce));
        Assert.False(WorkerSessionIdentityPolicy.Matches(
            instanceId, attemptId, nonce, otherInstanceId, attemptId, nonce));
        Assert.False(WorkerSessionIdentityPolicy.Matches(
            instanceId, attemptId, nonce, instanceId, otherAttemptId, nonce));
        Assert.False(WorkerSessionIdentityPolicy.Matches(
            instanceId, attemptId, nonce, instanceId, attemptId, forgedNonce));
        Assert.False(WorkerSessionIdentityPolicy.Matches(
            instanceId, attemptId, nonce, instanceId, attemptId, null));
    }
}
