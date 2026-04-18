using System.Windows;
using JanLabel.WindowsShell.Core;

namespace JanLabel.WindowsShell;

public partial class App : Application
{
    public WindowsShellPlatformContext PlatformContext { get; private set; } = null!;

    protected override void OnStartup(StartupEventArgs e)
    {
        try
        {
            PlatformContext = WindowsShellPlatform.Initialize();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Failed to initialize the JAN Label local runtime.\n\n{ex.Message}",
                "JAN Label startup failed",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown(-1);
            return;
        }

        base.OnStartup(e);
    }
}
