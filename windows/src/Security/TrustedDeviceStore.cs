using System.IO;
using System.Text.Json;

namespace NotificationBridge.Windows.Security;

public sealed record TrustedDevice(string DeviceId, string DeviceName, string SecretBase64, DateTimeOffset PairedAtUtc);

// Persists trusted devices per SECURITY.md #2 ("pairing should create a persistent trust
// relationship; unpairing must revoke that relationship") so the user doesn't have to re-pair on
// every app restart, per PRODUCT.md section 5.
public sealed class TrustedDeviceStore
{
    private static readonly string StorePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "NotificationBridge",
        "trusted-devices.json");

    private readonly object _lock = new();
    private readonly List<TrustedDevice> _devices;

    public TrustedDeviceStore()
    {
        _devices = Load();
    }

    public IReadOnlyList<TrustedDevice> List()
    {
        lock (_lock) return _devices.ToList();
    }

    public byte[]? TryGetSecret(string deviceId)
    {
        lock (_lock)
        {
            var device = _devices.FirstOrDefault(d => d.DeviceId == deviceId);
            return device is null ? null : Convert.FromBase64String(device.SecretBase64);
        }
    }

    public void Add(TrustedDevice device)
    {
        lock (_lock)
        {
            _devices.RemoveAll(d => d.DeviceId == device.DeviceId);
            _devices.Add(device);
            Save();
        }
    }

    public void Remove(string deviceId)
    {
        lock (_lock)
        {
            _devices.RemoveAll(d => d.DeviceId == deviceId);
            Save();
        }
    }

    private static List<TrustedDevice> Load()
    {
        if (!File.Exists(StorePath))
            return new List<TrustedDevice>();

        try
        {
            var json = File.ReadAllText(StorePath);
            return JsonSerializer.Deserialize<List<TrustedDevice>>(json) ?? new List<TrustedDevice>();
        }
        catch (Exception)
        {
            return new List<TrustedDevice>();
        }
    }

    private void Save()
    {
        var dir = Path.GetDirectoryName(StorePath)!;
        Directory.CreateDirectory(dir);
        File.WriteAllText(StorePath, JsonSerializer.Serialize(_devices));
    }
}
