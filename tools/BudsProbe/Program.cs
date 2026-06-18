using Windows.Devices.Bluetooth.Advertisement;
using Windows.Storage.Streams;

const ushort CompanyId = 0xFFFF;
const byte ManufacturerType = 0xFF;
const byte Header0 = 0x16;
const byte Header1 = 0x01;
const byte BatteryMask = 0x7F;
const byte StatusBit = 0x80;

var lastSeen = new Dictionary<ulong, string>();

var watcher = new BluetoothLEAdvertisementWatcher
{
    ScanningMode = BluetoothLEScanningMode.Active
};

watcher.Received += (_, args) =>
{
    foreach (var section in args.Advertisement.DataSections)
    {
        if (section.DataType != ManufacturerType) continue;

        var raw = ReadBuffer(section.Data);
        if (raw.Length < 3) continue;

        var company = (ushort)(raw[0] | (raw[1] << 8));
        if (company != CompanyId) continue;

        var payload = raw[2..];
        if (payload.Length < 8) continue;
        if (payload[0] != Header0 || payload[1] != Header1) continue;

        var addr = args.BluetoothAddress;
        var hex = string.Join(' ', payload.Select(b => b.ToString("X2")));

        if (lastSeen.TryGetValue(addr, out var prev) && prev == hex) continue;
        lastSeen[addr] = hex;

        var ts = DateTime.Now.ToString("HH:mm:ss.fff");
        var tampa = (payload[3] & 0x01) != 0 ? "ABERTA " : "fechada";
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine($"\n[{ts}] id={FormatMac(addr)}");
        Console.ResetColor();
        Console.WriteLine($"  payload: {hex}");
        Console.WriteLine($"  idx3 (tampa): 0x{payload[3]:X2} [{tampa}]");
        Console.WriteLine($"  idx5 (esq): {Describe(payload[5])}");
        Console.WriteLine($"  idx6 (dir): {Describe(payload[6])}");
        Console.WriteLine($"  idx7 (caixa): 0x{payload[7]:X2} bat={payload[7] & BatteryMask}");
    }
};

watcher.Start();

Console.ForegroundColor = ConsoleColor.Green;
Console.WriteLine("Monitorando advertisements dos Redmi Buds... (Enter para sair)");
Console.ResetColor();
Console.ForegroundColor = ConsoleColor.Yellow;
Console.WriteLine("Deixe UM fone em uso e o outro na caixa; mexa na caixa para forcar o broadcast.");
Console.WriteLine("bit80 ligado = na caixa | desligado = EM USO (fora da caixa)");
Console.ResetColor();

Console.ReadLine();
watcher.Stop();

static byte[] ReadBuffer(IBuffer buffer)
{
    var reader = DataReader.FromBuffer(buffer);
    var bytes = new byte[buffer.Length];
    reader.ReadBytes(bytes);
    return bytes;
}

static string FormatMac(ulong addr)
{
    var parts = new string[6];
    for (var i = 0; i < 6; i++)
        parts[i] = ((addr >> ((5 - i) * 8)) & 0xFF).ToString("X2");
    return string.Join(':', parts);
}

static string Describe(byte b)
{
    var bat = b & BatteryMask;
    var inCase = (b & StatusBit) != 0;
    var estado = inCase ? "na caixa" : "EM USO  ";
    return $"0x{b:X2} bat={bat,3} [{estado}]";
}
