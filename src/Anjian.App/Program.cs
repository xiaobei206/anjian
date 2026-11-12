using Anjian.App.Services;

namespace Anjian.App;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        if (args.Length > 0 && args[0].Equals("--worker", StringComparison.OrdinalIgnoreCase))
            return WorkerProgram.Run(args);

        ApplicationConfiguration.Initialize();
        if (args.Length > 0 && args[0].Equals("--automation-probe", StringComparison.OrdinalIgnoreCase))
            Application.Run(new AutomationProbeForm());
        else
            Application.Run(new MainForm());
        return 0;
    }
}
