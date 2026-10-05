using System.Collections.Concurrent;
using OemAi.Core.Models;

namespace OemAi.Api;

/// <summary>Latest snapshot of one device, as listed by GET /api/devices.</summary>
public sealed record DeviceRecord(string DeviceId, DeviceTelemetry Telemetry, DateTimeOffset LastSeen, bool IsSimulated);

/// <summary>
/// Latest telemetry per device, fed by device uploads and the demo fleet simulator.
/// ponytail: in-memory, lost on restart, single node — PostgreSQL in OA-3; any caller can write
/// any deviceId until auth lands in OA-5/OA-10. The device cap is approximate under concurrent
/// inserts of new ids (may overshoot by a few) — fine for a memory guard.
/// </summary>
public sealed class DeviceStore
{
    public const int MaxDevices = 500;

    private readonly ConcurrentDictionary<string, DeviceRecord> _devices = new();

    /// <returns>false when <paramref name="deviceId"/> is new and the store is full; known ids always update.</returns>
    public bool TryUpsert(string deviceId, DeviceTelemetry telemetry, bool isSimulated, DateTimeOffset? lastSeen = null)
    {
        if (_devices.Count >= MaxDevices && !_devices.ContainsKey(deviceId))
            return false;
        _devices[deviceId] = new DeviceRecord(deviceId, telemetry, lastSeen ?? DateTimeOffset.UtcNow, isSimulated);
        return true;
    }

    public DeviceRecord? Find(string deviceId) => _devices.GetValueOrDefault(deviceId);

    public IReadOnlyList<DeviceRecord> All() =>
        _devices.Values.OrderBy(d => d.DeviceId, StringComparer.Ordinal).ToList();
}
