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
