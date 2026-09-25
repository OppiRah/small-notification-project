using System.Security.Cryptography;
using System.Text;

namespace NotificationBridge.Windows.Security;

// Manages the short-lived pairing window: a fresh 6-digit code the user reads off the PC and
// types into the phone. VerifyProof binds the phone's response to the exact TLS certificate it
// negotiated with (HMAC-SHA256(code, certFingerprint)), which is what defeats a simple
// man-in-the-middle -- an attacker presenting a different certificate would produce a fingerprint
// the phone can't forge a matching proof for without knowing the code. Per ADR-009.
//
// A 6-digit code is only 1,000,000 possibilities, so the window is also protected against guessing:
// the code is single-use (a successful pairing consumes it) and is discarded after
// MaxFailedAttempts wrong proofs, after which the user must start a new pairing.
public sealed class PairingSession
{
    private static readonly TimeSpan PairingWindow = TimeSpan.FromMinutes(2);
    private const int MaxFailedAttempts = 5;

    // VerifyProof runs on network threads while Start/Stop/IsActive are called from the UI thread.
    private readonly object _lock = new();
    private string? _code;
    private DateTimeOffset _expiresAt;
    private int _failedAttempts;

    public bool IsActive
    {
        get { lock (_lock) return IsActiveLocked(); }
    }

    public string? CurrentCode
    {
        get { lock (_lock) return IsActiveLocked() ? _code : null; }
    }

    public DateTimeOffset ExpiresAt
    {
        get { lock (_lock) return _expiresAt; }
    }

    public string Start()
    {
        lock (_lock)
        {
            _code = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");
            _expiresAt = DateTimeOffset.UtcNow.Add(PairingWindow);
            _failedAttempts = 0;
            return _code;
        }
    }

    public void Stop()
    {
        lock (_lock) _code = null;
    }

    public bool VerifyProof(string certFingerprint, string proofBase64)
    {
        lock (_lock)
        {
            if (!IsActiveLocked())
                return false;

            byte[] proof;
            try
            {
                proof = Convert.FromBase64String(proofBase64);
            }
            catch (FormatException)
            {
                return CountFailure();
            }

            var expected = HMACSHA256.HashData(Encoding.UTF8.GetBytes(_code!), Encoding.UTF8.GetBytes(certFingerprint));
            if (!CryptographicOperations.FixedTimeEquals(expected, proof))
                return CountFailure();

            _code = null;
            return true;
        }
    }

    private bool IsActiveLocked() => _code is not null && DateTimeOffset.UtcNow < _expiresAt;

    // Always returns false so callers can `return CountFailure();`.
    private bool CountFailure()
    {
        if (++_failedAttempts >= MaxFailedAttempts)
            _code = null;
        return false;
    }
}
