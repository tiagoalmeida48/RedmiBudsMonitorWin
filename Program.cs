using RedmiBudsMonitor;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        using var mutex = new Mutex(true, "RedmiBudsMonitor_SingleInstance", out bool created);
        if (!created) return;

        Application.EnableVisualStyles();

        using var app = new TrayApp();
        app.Start();
        Application.Run();
    }
}