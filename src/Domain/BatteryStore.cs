using System.Text.Json;

namespace TrayBatt;

internal static class BatteryStore
{
    internal sealed record StoredLevels(byte Left, byte Right, byte Case);

    private static readonly string FilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "TrayBatt",
        "battery-state.json");

    public static StoredLevels? TryLoad()
    {
        try
        {
            return File.Exists(FilePath)
                ? JsonSerializer.Deserialize<StoredLevels>(File.ReadAllText(FilePath))
                : null;
        }
        catch
        {
            return null;
        }
    }

    public static void Save(StoredLevels levels)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(levels));
        }
        catch
        {
        }
    }
}
