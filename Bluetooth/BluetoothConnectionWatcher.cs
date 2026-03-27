using Windows.Devices.Bluetooth;
using Windows.Devices.Enumeration;

namespace RedmiBudsMonitor;

internal sealed class BluetoothConnectionWatcher : IDisposable
{
    private const string IsConnectedProperty = "System.Devices.Aep.IsConnected";

    private readonly DeviceWatcher _watcher;
    private readonly string _deviceNameFilter;
    private volatile string? _deviceId;

    public event Action<bool>? ConnectionChanged;

    public BluetoothConnectionWatcher(string deviceNameFilter)
    {
        _deviceNameFilter = deviceNameFilter;

        var aqs = BluetoothDevice.GetDeviceSelectorFromPairingState(true);
        _watcher = DeviceInformation.CreateWatcher(aqs, [IsConnectedProperty]);
        _watcher.Added += (_, info) => OnAdded(info);
        _watcher.Updated += (_, update) => OnUpdated(update);
        _watcher.Removed += (_, update) => OnRemoved(update);
    }

    public void Start() => _watcher.Start();

    public void Dispose()
    {
        if (_watcher.Status is DeviceWatcherStatus.Started or DeviceWatcherStatus.EnumerationCompleted)
            _watcher.Stop();
    }

    private void OnAdded(DeviceInformation info)
    {
        if (!info.Name.Contains(_deviceNameFilter, StringComparison.OrdinalIgnoreCase)) return;
        _deviceId = info.Id;
        ConnectionChanged?.Invoke(ReadConnected(info.Properties));
    }

    private void OnUpdated(DeviceInformationUpdate update)
    {
        if (update.Id != _deviceId) return;
        if (!update.Properties.TryGetValue(IsConnectedProperty, out var v)) return;
        ConnectionChanged?.Invoke(v is true);
    }

    private void OnRemoved(DeviceInformationUpdate update)
    {
        if (update.Id != _deviceId) return;
        ConnectionChanged?.Invoke(false);
    }

    private static bool ReadConnected(IReadOnlyDictionary<string, object> props)
        => props.TryGetValue(IsConnectedProperty, out var v) && v is true;
}