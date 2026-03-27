using Windows.Devices.Bluetooth.Advertisement;
using Windows.Storage.Streams;

namespace RedmiBudsMonitor;

internal sealed class BleScanner : IDisposable
{
    private const ushort CompanyId = 0xFFFF;
    private const byte ManufacturerType = 0xFF;

    private readonly BluetoothLEAdvertisementWatcher _watcher;

    public event Action<BudsAdvertisement>? OnBudsData;

    public BleScanner()
    {
        _watcher = new BluetoothLEAdvertisementWatcher
        {
            ScanningMode = BluetoothLEScanningMode.Active
        };
        _watcher.Received += HandleReceived;
    }

    public void Start() => _watcher.Start();

    public void Dispose()
    {
        _watcher.Stop();
        _watcher.Received -= HandleReceived;
    }

    private void HandleReceived(
        BluetoothLEAdvertisementWatcher _,
        BluetoothLEAdvertisementReceivedEventArgs args)
    {
        foreach (var section in args.Advertisement.DataSections)
        {
            if (section.DataType != ManufacturerType) continue;

            var raw = ReadBuffer(section.Data);
            if (raw.Length < 3) continue;

            var companyId = (ushort)(raw[0] | (raw[1] << 8));
            if (companyId != CompanyId) continue;

            var buds = BudsAdvertisement.TryParse(raw[2..]);
            if (buds is not null) OnBudsData?.Invoke(buds);
        }
    }

    private static byte[] ReadBuffer(IBuffer buffer)
    {
        var reader = DataReader.FromBuffer(buffer);
        var bytes = new byte[buffer.Length];
        reader.ReadBytes(bytes);
        return bytes;
    }
}