using Anjian.App.Controls;
using Anjian.App.Services;
using Anjian.Core.Packaging;

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
        {
            Application.Run(new AutomationProbeForm());
        }
        else
        {
            var spriteManifest = Path.Combine(AppContext.BaseDirectory, SpritePackageBuilder.ManifestFileName);
            if (File.Exists(spriteManifest))
            {
                try
                {
                    var issues = SpritePackageBuilder.Verify(AppContext.BaseDirectory);
                    if (issues.Count > 0)
                    {
                        MessageBox.Show("小精灵包校验失败，脚本不会启动：" + Environment.NewLine +
                            string.Join(Environment.NewLine, issues.Take(20)),
                            "按键精灵 2026", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return 2;
                    }
                    Application.Run(new SpriteLauncherForm(spriteManifest));
                }
                catch (Exception ex)
                {
                    MessageBox.Show("小精灵包检查失败，脚本不会启动：" + Environment.NewLine + ex.Message,
                        "按键精灵 2026", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return 2;
                }
            }
            else
            {
                Application.Run(new MainForm());
            }
        }
        return 0;
    }
}
