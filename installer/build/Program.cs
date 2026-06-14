using System.Diagnostics;
using System.IO.Compression;
using System.Reflection;
using System.Runtime.InteropServices;

namespace BackupPilotInstaller;

internal static class Program
{
    private const string AppName = "BackupPilot";
    private static readonly string Version = GetVersionLabel();

    [STAThread]
    private static int Main()
    {
        try
        {
            string installDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Programs",
                AppName);

            CloseRunningApp();
            InstallPayload(installDirectory);
            string appPath = Path.Combine(installDirectory, $"{AppName}.exe");
            CreateShortcut(GetStartMenuShortcutPath(), appPath);
            CreateShortcut(GetDesktopShortcutPath(), appPath);
            Process.Start(new ProcessStartInfo
            {
                FileName = appPath,
                UseShellExecute = true,
                WorkingDirectory = installDirectory
            });
            return 0;
        }
        catch (Exception ex)
        {
            ShowMessage($"インストールに失敗しました。{Environment.NewLine}{ex.Message}", $"{AppName} セットアップ");
            return 1;
        }
    }

    private static string GetVersionLabel()
    {
        string name = Assembly.GetExecutingAssembly().GetName().Name ?? "BackupPilot_Setup";
        const string prefix = "BackupPilot_Setup_v";
        return name.StartsWith(prefix, StringComparison.Ordinal)
            ? "v" + name[prefix.Length..]
            : "v1.0.0";
    }

    private static void CloseRunningApp()
    {
        foreach (Process process in Process.GetProcessesByName(AppName))
        {
            try
            {
                if (!process.CloseMainWindow() || !process.WaitForExit(5000))
                {
                    process.Kill(entireProcessTree: true);
                    process.WaitForExit(5000);
                }
            }
            catch
            {
            }
            finally
            {
                process.Dispose();
            }
        }
    }

    private static void InstallPayload(string installDirectory)
    {
        string temporaryDirectory = Path.Combine(Path.GetTempPath(), $"{AppName}_installer_{Guid.NewGuid():N}");
        Directory.CreateDirectory(temporaryDirectory);

        try
        {
            string payloadPath = Path.Combine(temporaryDirectory, "payload.zip");
            using Stream? payloadStream = Assembly.GetExecutingAssembly().GetManifestResourceStream("payload.zip");
            if (payloadStream is null)
            {
                throw new InvalidOperationException("インストール用データが見つかりません。");
            }

            using (FileStream payloadFile = File.Create(payloadPath))
            {
                payloadStream.CopyTo(payloadFile);
            }

            string extractDirectory = Path.Combine(temporaryDirectory, "app");
            ZipFile.ExtractToDirectory(payloadPath, extractDirectory);

            if (Directory.Exists(installDirectory))
            {
                Directory.Delete(installDirectory, recursive: true);
            }

            Directory.CreateDirectory(Path.GetDirectoryName(installDirectory)!);
            Directory.Move(extractDirectory, installDirectory);
        }
        finally
        {
            try
            {
                Directory.Delete(temporaryDirectory, recursive: true);
            }
            catch
            {
            }
        }
    }

    private static string GetStartMenuShortcutPath()
    {
        string startMenuPrograms = Environment.GetFolderPath(Environment.SpecialFolder.StartMenu);
        return Path.Combine(startMenuPrograms, "Programs", $"{AppName}.lnk");
    }

    private static string GetDesktopShortcutPath()
    {
        string desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        return Path.Combine(desktop, $"{AppName}.lnk");
    }

    private static void CreateShortcut(string shortcutPath, string targetPath)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(shortcutPath)!);
        Type? shellType = Type.GetTypeFromProgID("WScript.Shell");
        if (shellType is null)
        {
            return;
        }

        dynamic? shell = null;
        dynamic? shortcut = null;
        try
        {
            shell = Activator.CreateInstance(shellType);
            shortcut = shell?.CreateShortcut(shortcutPath);
            if (shortcut is null)
            {
                return;
            }

            shortcut.TargetPath = targetPath;
            shortcut.WorkingDirectory = Path.GetDirectoryName(targetPath);
            shortcut.IconLocation = targetPath;
            shortcut.Description = $"{AppName} {Version}";
            shortcut.Save();
        }
        finally
        {
            ReleaseComObject(shortcut);
            ReleaseComObject(shell);
        }
    }

    private static void ReleaseComObject(object? value)
    {
        if (value is not null && Marshal.IsComObject(value))
        {
            Marshal.FinalReleaseComObject(value);
        }
    }

    private static void ShowMessage(string message, string title)
    {
        _ = MessageBox(IntPtr.Zero, message, title, 0x00000010);
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int MessageBox(IntPtr hWnd, string text, string caption, uint type);
}
