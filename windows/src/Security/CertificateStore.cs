using System.IO;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace NotificationBridge.Windows.Security;

// Generates and persists a self-signed TLS certificate for the local receiver. This is
// trust-on-first-use, not CA-validated: paired phones pin this certificate's fingerprint during
// pairing (ADR-009) rather than trusting a certificate authority. Uses only .NET's built-in
// certificate/crypto primitives per SECURITY.md #7 ("do not implement cryptographic primitives
// manually").
public static class CertificateStore
{
    private static readonly string CertPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "NotificationBridge",
        "server-cert.pfx");

    public static X509Certificate2 LoadOrCreate()
    {
        var dir = Path.GetDirectoryName(CertPath)!;
        Directory.CreateDirectory(dir);

        if (File.Exists(CertPath))
            return X509CertificateLoader.LoadPkcs12FromFile(CertPath, null, X509KeyStorageFlags.PersistKeySet);

        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest("CN=NotificationBridge", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using var cert = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(10));

        var exported = cert.Export(X509ContentType.Pfx);
        File.WriteAllBytes(CertPath, exported);
        return X509CertificateLoader.LoadPkcs12(exported, null, X509KeyStorageFlags.PersistKeySet);
    }

    public static string Sha256Fingerprint(X509Certificate2 cert)
    {
        var hash = SHA256.HashData(cert.RawData);
        return Convert.ToHexString(hash);
    }
}
