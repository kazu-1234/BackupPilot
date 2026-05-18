// BackupPilot v1.0.0
// アプリ起動処理。WinUI 3 の標準ライフサイクルを使用します。

using Microsoft.UI.Xaml;

namespace BackupPilot;

public partial class App : Application
{
    private Window? mainWindow;

    public App()
    {
        InitializeComponent();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        mainWindow = new MainWindow();
        mainWindow.Activate();
    }
}
