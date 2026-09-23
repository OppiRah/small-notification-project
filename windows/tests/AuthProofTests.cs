using NotificationBridge.Windows.Security;
using Xunit;

namespace NotificationBridge.Windows.Tests;

public class AuthProofTests
{
    [Fact]
    public void Verify_AcceptsCorrectProof()
    {
        var secret = new byte[] { 1, 2, 3, 4, 5 };
        var proof = Convert.ToBase64String(AuthProof.Compute(secret, "device-1", "nonce-1", "2026-09-23T10:00:00Z"));

        Assert.True(AuthProof.Verify(secret, "device-1", "nonce-1", "2026-09-23T10:00:00Z", proof));
    }

    [Fact]
    public void Verify_RejectsWrongSecret()
    {
        var secret = new byte[] { 1, 2, 3, 4, 5 };
        var wrongSecret = new byte[] { 9, 9, 9, 9, 9 };
        var proof = Convert.ToBase64String(AuthProof.Compute(secret, "device-1", "nonce-1", "2026-09-23T10:00:00Z"));

        Assert.False(AuthProof.Verify(wrongSecret, "device-1", "nonce-1", "2026-09-23T10:00:00Z", proof));
    }

    [Fact]
    public void Verify_RejectsTamperedField()
    {
        var secret = new byte[] { 1, 2, 3, 4, 5 };
        var proof = Convert.ToBase64String(AuthProof.Compute(secret, "device-1", "nonce-1", "2026-09-23T10:00:00Z"));

        Assert.False(AuthProof.Verify(secret, "device-2", "nonce-1", "2026-09-23T10:00:00Z", proof));
    }

    [Fact]
    public void Verify_RejectsMalformedBase64()
    {
        var secret = new byte[] { 1, 2, 3, 4, 5 };

        Assert.False(AuthProof.Verify(secret, "device-1", "nonce-1", "2026-09-23T10:00:00Z", "not-base64!!"));
    }
}
