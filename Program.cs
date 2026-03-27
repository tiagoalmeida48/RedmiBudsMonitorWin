using System.Windows.Forms;
using RedmiBudsMonitor;
using System.Threading;

internal class Program
{
    private static Mutex mutex = null;

    [STAThread]
    static void Main()
    {
        const string appName = "RedmiBudsMonitorSingleInstanceMutex";
        bool createdNew;

        mutex = new Mutex(true, appName, out createdNew);

        if (!createdNew)
        {
            // App já está rodando, sai silenciosamente.
            return;
        }

        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        using var app = new TrayApp();
        app.Start();
        Application.Run(); // bloqueia até Application.Exit()
    }
}