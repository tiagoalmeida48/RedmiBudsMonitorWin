using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using Windows.Devices.Enumeration;
using Windows.Devices.Enumeration.Pnp;
using Windows.Storage.Streams;

namespace TrayBatt;

internal static class BluetoothBatteryEnumerator
{
    private const string IsConnectedProperty = "System.Devices.Aep.IsConnected";
    private const string AddressProperty     = "System.Devices.Aep.DeviceAddress";

    private const string BatteryProperty = "{104EA319-6EE2-4701-BD47-8DDBF425BBE5} 2";

    public static async Task<IReadOnlyList<DeviceBattery>> QueryConnectedAsync()
    {
        var batteries = await QueryPnpBatteriesAsync();
        var devices   = new List<DeviceBattery>();
        var seen      = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        await CollectAsync(BluetoothDevice.GetDeviceSelectorFromPairingState(true), false, batteries, seen, devices);
        await CollectAsync(BluetoothLEDevice.GetDeviceSelectorFromPairingState(true), true, batteries, seen, devices);

        devices.Sort(static (a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
        return devices;
    }

    private static async Task CollectAsync(
        string selector,
        bool isLowEnergy,
        IReadOnlyList<(string InstanceId, byte Pct)> batteries,
        HashSet<string> seen,
        List<DeviceBattery> devices)
    {
        var infos = await DeviceInformation.FindAllAsync(selector, new[] { IsConnectedProperty, AddressProperty });
        foreach (var info in infos)
        {
            if (!ReadBool(info.Properties, IsConnectedProperty)) continue;

            var address = ReadAddress(info.Properties);
            if (address is null || !seen.Add(address)) continue;

            var pct = FindPnpBattery(batteries, address);
            if (pct is null && isLowEnergy) pct = await ReadGattBatteryAsync(info.Id);

            var name = string.IsNullOrWhiteSpace(info.Name) ? address : info.Name;
            devices.Add(new DeviceBattery(name, pct ?? BatterySnapshot.Unavailable));
        }
    }

    private static async Task<IReadOnlyList<(string InstanceId, byte Pct)>> QueryPnpBatteriesAsync()
    {
        var result = new List<(string, byte)>();
        try
        {
            var objects = await PnpObject.FindAllAsync(PnpObjectType.Device, new[] { BatteryProperty });
            foreach (var obj in objects)
            {
                if (!obj.Properties.TryGetValue(BatteryProperty, out var raw)) continue;

                var pct = raw switch
                {
                    byte b => (byte?)b,
                    int i and >= 0 and <= 100 => (byte)i,
                    _ => null,
                };
                if (pct is not null) result.Add((obj.Id, pct.Value));
            }
        }
        catch
        {
        }
        return result;
    }

    private static async Task<byte?> ReadGattBatteryAsync(string deviceId)
    {
        try
        {
            using var device = await BluetoothLEDevice.FromIdAsync(deviceId);
            if (device is null) return null;

            var services = await device.GetGattServicesForUuidAsync(GattServiceUuids.Battery, BluetoothCacheMode.Cached);
            if (services.Status != GattCommunicationStatus.Success || services.Services.Count == 0) return null;

            using var service = services.Services[0];
            var characteristics = await service.GetCharacteristicsForUuidAsync(
                GattCharacteristicUuids.BatteryLevel, BluetoothCacheMode.Cached);
            if (characteristics.Status != GattCommunicationStatus.Success ||
                characteristics.Characteristics.Count == 0) return null;

            var read = await characteristics.Characteristics[0].ReadValueAsync(BluetoothCacheMode.Uncached);
            if (read.Status != GattCommunicationStatus.Success || read.Value.Length == 0) return null;

            return DataReader.FromBuffer(read.Value).ReadByte();
        }
        catch
        {
            return null;
        }
    }

    private static byte? FindPnpBattery(IReadOnlyList<(string InstanceId, byte Pct)> batteries, string address)
    {
        foreach (var (instanceId, pct) in batteries)
            if (instanceId.Contains(address, StringComparison.OrdinalIgnoreCase)) return pct;
        return null;
    }

    private static string? ReadAddress(IReadOnlyDictionary<string, object> props)
    {
        if (!props.TryGetValue(AddressProperty, out var v) || v is not string address) return null;
        var normalized = address.Replace(":", "").ToUpperInvariant();
        return normalized.Length == 0 ? null : normalized;
    }

    private static bool ReadBool(IReadOnlyDictionary<string, object> props, string key)
        => props.TryGetValue(key, out var v) && v is true;
}
