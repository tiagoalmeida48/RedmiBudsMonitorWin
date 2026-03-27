using Windows.Devices.Bluetooth.Advertisement;
using Windows.Storage.Streams;

namespace RedmiBudsMonitor;

/// <summary>
/// Scanner BLE focado no Redmi Buds 5.
///
/// Protocolo confirmado por engenharia reversa:
///   Company ID : 0xFFFF
///   Header     : payload[0]=0x16, payload[1]=0x01, payload[2]=0x18
///   Bateria    : payload[5]=esquerdo, payload[6]=direito, payload[7]=caixinha
///
/// O endereço BLE é aleatório e muda a cada sessão — não é usado como filtro.
/// A identificação é feita pelo header fixo + range válido dos bytes de bateria.
/// </summary>
internal sealed class BleScanner : IDisposable
{
    private const ushort RedmiBudsCompanyId   = 0xFFFF;
    private const byte   ManufacturerDataType = 0xFF;

    private readonly BluetoothLEAdvertisementWatcher _watcher;

    public event Action<BudsAdvertisement>? OnBudsData;
    public event Action<byte[]>?             OnRawPayload;

    public BleScanner()
    {
        _watcher = new BluetoothLEAdvertisementWatcher
        {
            ScanningMode = BluetoothLEScanningMode.Active
        };
        _watcher.Received += HandleReceived;
        _watcher.Stopped  += (_, e) => Logger.Warn($"Watcher parou: {e.Error}");
    }

    public void Start()
    {
        Logger.Section("Redmi Buds 5 Monitor");
        Logger.Info("Aguardando advertisement do fone...");
        Logger.Info("Pressione CTRL+C para encerrar.");
        _watcher.Start();
    }

    public void Stop() => _watcher.Stop();

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
            if (section.DataType != ManufacturerDataType) continue;

            var raw = ReadBuffer(section.Data);
            if (raw.Length < 3) continue;

            ushort companyId = (ushort)(raw[0] | (raw[1] << 8));
            if (companyId != RedmiBudsCompanyId) continue;

            var payload = raw[2..];

            // Dispara o evento raw ANTES do filtro de header (modo debug)
            OnRawPayload?.Invoke(payload);

            var buds = BudsAdvertisement.TryParse(payload);
            if (buds is null) continue;

            OnBudsData?.Invoke(buds);
        }
    }

    private static byte[] ReadBuffer(IBuffer buffer)
    {
        var reader = DataReader.FromBuffer(buffer);
        var bytes  = new byte[buffer.Length];
        reader.ReadBytes(bytes);
        return bytes;
    }

    public static string ToHex(ReadOnlySpan<byte> bytes) =>
        string.Join(' ', bytes.ToArray().Select(b => b.ToString("X2")));
}