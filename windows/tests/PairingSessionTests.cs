using System.Security.Cryptography;
using System.Text;
using NotificationBridge.Windows.Security;
using Xunit;

namespace NotificationBridge.Windows.Tests;

public class PairingSessionTests
{
    [Fact]
    public void VerifyProof_AcceptsCorrectCodeAndFingerprint()
    {
        var session = new PairingSession();
        var code = session.Start();
        var fingerprint = "AABBCCDD";
        var proof = Convert.ToBase64String(HMACSHA256.HashData(Encoding.UTF8.GetBytes(code), Encoding.UTF8.GetBytes(fingerprint)));

        Assert.True(session.VerifyProof(fingerprint, proof));
    }

    [Fact]
    public void VerifyProof_RejectsWrongFingerprint()
    {
        var session = new PairingSession();
        var code = session.Start();
        var proof = Convert.ToBase64String(HMACSHA256.HashData(Encoding.UTF8.GetBytes(code), Encoding.UTF8.GetBytes("real-fingerprint")));

        // Simulates a MITM presenting a different certificate: the phone would have computed its
        // proof against that attacker's fingerprint, not the PC's real one.
        Assert.False(session.VerifyProof("attacker-fingerprint", proof));
    }

    [Fact]
    public void VerifyProof_RejectsWhenNoSessionActive()
    {
        var session = new PairingSession();
        var proof = Convert.ToBase64String(HMACSHA256.HashData(Encoding.UTF8.GetBytes("123456"), Encoding.UTF8.GetBytes("fp")));

        Assert.False(session.VerifyProof("fp", proof));
    }

    private static string Proof(string code, string fingerprint) =>
        Convert.ToBase64String(HMACSHA256.HashData(Encoding.UTF8.GetBytes(code), Encoding.UTF8.GetBytes(fingerprint)));

    [Fact]
    public void VerifyProof_CodeIsSingleUse()
    {
        var session = new PairingSession();
        var code = session.Start();

        Assert.True(session.VerifyProof("fp", Proof(code, "fp")));

        Assert.False(session.IsActive);
        Assert.False(session.VerifyProof("fp", Proof(code, "fp")));
    }

    [Fact]
    public void VerifyProof_CodeIsDiscardedAfterTooManyWrongProofs()
    {
        var session = new PairingSession();
        var code = session.Start();

        for (var i = 0; i < 5; i++)
            Assert.False(session.VerifyProof("fp", Proof("not-the-code", "fp")));

        // Even the right proof no longer works: an attacker cannot keep guessing within one window.
        Assert.False(session.IsActive);
        Assert.False(session.VerifyProof("fp", Proof(code, "fp")));
    }

    [Fact]
    public void VerifyProof_AFewMistypedAttemptsDoNotLockOutTheRealUser()
    {
        var session = new PairingSession();
        var code = session.Start();

        for (var i = 0; i < 4; i++)
            Assert.False(session.VerifyProof("fp", Proof("not-the-code", "fp")));

        Assert.True(session.VerifyProof("fp", Proof(code, "fp")));
    }

    [Fact]
    public void Start_ResetsTheFailedAttemptCount()
    {
        var session = new PairingSession();
        session.Start();
        for (var i = 0; i < 5; i++)
            session.VerifyProof("fp", Proof("not-the-code", "fp"));

        var newCode = session.Start();

        Assert.True(session.VerifyProof("fp", Proof(newCode, "fp")));
    }

    [Fact]
    public void Stop_InvalidatesTheSession()
    {
        var session = new PairingSession();
        var code = session.Start();
        var proof = Convert.ToBase64String(HMACSHA256.HashData(Encoding.UTF8.GetBytes(code), Encoding.UTF8.GetBytes("fp")));

        session.Stop();

        Assert.False(session.VerifyProof("fp", proof));
    }
}
