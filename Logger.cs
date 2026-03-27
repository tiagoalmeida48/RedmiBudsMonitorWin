namespace RedmiBudsMonitor;

internal static class Logger
{
    private static readonly object _lock = new();

    public static void Info(string msg)  => Write("[INFO]", ConsoleColor.Cyan, msg);
    public static void Ok(string msg)    => Write("[OK]  ", ConsoleColor.Green, msg);
    public static void Warn(string msg)  => Write("[WARN]", ConsoleColor.Yellow, msg);
    public static void Error(string msg) => Write("[ERR] ", ConsoleColor.Red, msg);
    public static void Data(string msg)  => Write("[DATA]", ConsoleColor.Magenta, msg);
    public static void Raw(string msg)   => Write("[RAW] ", ConsoleColor.DarkYellow, msg);

    public static void Section(string title)
    {
        lock (_lock)
        {
            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.DarkCyan;
            Console.WriteLine(new string('─', 60));
            Console.WriteLine($"  {title}");
            Console.WriteLine(new string('─', 60));
            Console.ResetColor();
        }
    }

    public static void Battery(string device, int percent)
    {
        lock (_lock)
        {
            var ts = DateTime.Now.ToString("HH:mm:ss.fff");

            var barColor = percent >= 50 ? ConsoleColor.Green
                         : percent >= 20 ? ConsoleColor.Yellow
                         : ConsoleColor.Red;

            Console.Write($"{ts} ");
            Console.ForegroundColor = ConsoleColor.White;
            Console.Write($"[BATT] {device,-20} ");
            Console.ForegroundColor = barColor;
            Console.Write($"{percent,3}% ");
            Console.Write(BuildBar(percent));
            Console.WriteLine();
            Console.ResetColor();
        }
    }

    private static void Write(string tag, ConsoleColor color, string msg)
    {
        lock (_lock)
        {
            var ts = DateTime.Now.ToString("HH:mm:ss.fff");
            Console.Write($"{ts} ");
            Console.ForegroundColor = color;
            Console.Write(tag);
            Console.ResetColor();
            Console.WriteLine($" {msg}");
        }
    }

    private static string BuildBar(int percent)
    {
        int filled = Math.Clamp(percent, 0, 100) / 10;
        return $"[{new string('█', filled)}{new string('░', 10 - filled)}]";
    }
}