// BackupPilot v1.1.4
// UTF-8 JSON でアプリ設定を保存・読み込みします。

using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;
using BackupPilot.Models;

namespace BackupPilot.Services;

public sealed class SettingsService
{
    private readonly SemaphoreSlim fileAccessLock = new(1, 1);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.Create(UnicodeRanges.All)
    };

    public string SettingsFilePath { get; }

    public SettingsService()
    {
        string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        string settingsDirectory = Path.Combine(appData, "BackupPilot");
        SettingsFilePath = Path.Combine(settingsDirectory, "settings.json");
    }

    public async Task<BackupSettings> LoadAsync()
    {
        if (!File.Exists(SettingsFilePath))
        {
            return new BackupSettings();
        }

        return await LoadFromFileAsync(SettingsFilePath);
    }

    public async Task SaveAsync(BackupSettings settings)
    {
        await SaveToFileAsync(SettingsFilePath, settings);
    }

    public async Task<BackupSettings> LoadFromFileAsync(string filePath)
    {
        await using FileStream stream = new(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        BackupSettings? settings = await JsonSerializer.DeserializeAsync<BackupSettings>(stream, JsonOptions);
        return settings ?? new BackupSettings();
    }

    public async Task SaveToFileAsync(string filePath, BackupSettings settings)
    {
        await fileAccessLock.WaitAsync();
        try
        {
            string? directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            await using FileStream stream = new(filePath, FileMode.Create, FileAccess.Write, FileShare.Read);
            await JsonSerializer.SerializeAsync(stream, settings, JsonOptions);
        }
        finally
        {
            fileAccessLock.Release();
        }
    }
}
