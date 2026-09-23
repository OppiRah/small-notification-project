using System.Security.Cryptography;
using System.Text;

namespace NotificationBridge.Windows.Security;

// Manages the short-lived pairing window: a fresh 6-digit code the user reads off the PC and
// types into the phone. VerifyProof binds the phone's response to the exact TLS certificate it
// negotiated with (HMAC-SHA256(code, certFingerprint)), which is what defeats a simple
// man-in-the-middle -- an attacker presenting a different certificate would produce a fingerprint
// the phone can't forge a matching proof for without knowing the code. Per ADR-009.
public sealed class PairingSession
{
    private static readonly TimeSpan PairingWindow = TimeSpan.FromMinutes(2);

    private string? _code;
    private DateTimeOffset _expiresAt;

    public bool IsActive => _code is not null && DateTimeOffset.UtcNow < _expiresAt;
    public string? CurrentCode => IsActive ? _code : null;
    public DateTimeOffset ExpiresAt => _expiresAt;

    public string Start()
    {
        _code = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");
        _expiresAt = DateTimeOffset.UtcNow.Add(PairingWindow);
        return _code;
    }

    public void Stop()
    {
        _code = null;
    }

    public bool VerifyProof(string certFingerprint, string proofBase64)
    {
        if (!IsActive)
            return false;

        byte[] proof;
        try
        {
            proof = Convert.FromBase64String(proofBase64);
        }
        catch (FormatException)
        {
            return false;
        }

        var expected = HMACSHA256.HashData(Encoding.UTF8.GetBytes(_code!), Encoding.UTF8.GetBytes(certFingerprint));
        return CryptographicOperations.FixedTimeEquals(expected, proof);
    }
}
