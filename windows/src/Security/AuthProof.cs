using System.Security.Cryptography;
using System.Text;

namespace NotificationBridge.Windows.Security;

// HMAC-SHA256 proof-of-possession for the AUTHENTICATE step, using only .NET's built-in
// primitives (SECURITY.md #7). Comparisons use CryptographicOperations.FixedTimeEquals to avoid
// leaking timing information about how much of the proof matched.
public static class AuthProof
{
    public static byte[] Compute(byte[] secret, string deviceId, string nonce, string timestamp)
    {
        var message = Encoding.UTF8.GetBytes($"{deviceId}|{nonce}|{timestamp}");
        return HMACSHA256.HashData(secret, message);
    }

    public static bool Verify(byte[] secret, string deviceId, string nonce, string timestamp, string proofBase64)
    {
        byte[] proof;
        try
        {
            proof = Convert.FromBase64String(proofBase64);
        }
        catch (FormatException)
        {
            return false;
        }

        var expected = Compute(secret, deviceId, nonce, timestamp);
        return CryptographicOperations.FixedTimeEquals(expected, proof);
    }
}
