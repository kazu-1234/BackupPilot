// BackupPilot v1.2.15
// メイン画面の操作、設定保存、バックアップ実行を管理します。

using System.Collections.ObjectModel;
using System.Text.Json;
using BackupPilot.Models;
using BackupPilot.Services;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Foundation;
using Windows.Storage.Pickers;
using Windows.System;
using WinRT.Interop;

namespace BackupPilot;

public sealed partial class MainWindow : Window
{
    private const string CurrentVersion = "v1.2.24";
    private const string LatestReleaseApiUrl = "https://api.github.com/repos/kazu-1234/BackupPilot/releases/latest";
    private const string ReleasesUrl = "https://github.com/kazu-1234/BackupPilot/releases";

    private readonly SettingsService settingsService = new();
    private readonly FileComparisonService comparisonService = new();
    private readonly BackupService backupService = new();
    private BackupSettings settings = new();
    private BackupJob? selectedJob;
    private CancellationTokenSource? backupCancellation;
    private bool isUiUpdating;

    public MainWindow()
    {
        InitializeComponent();
        Title = "BackupPilot";
        ApplyWindowIcon();
        _ = LoadSettingsAsync();
    }

    private void OverwriteModeComboBox_Loaded(object sender, RoutedEventArgs e)
    {
        QueueFitComboBoxWidth(OverwriteModeComboBox);
    }

    private void QueueFitComboBoxWidth(ComboBox comboBox)
    {
        DispatcherQueue.TryEnqueue(() => FitComboBoxWidthToSelectedItem(comboBox));
    }

    private void FitComboBoxWidthToSelectedItem(ComboBox comboBox)
    {
        if (Content is not Panel rootPanel)
        {
            return;
        }

        string text = GetComboBoxItemText(comboBox.SelectedItem);
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        StackPanel measureHost = new()
        {
            Visibility = Visibility.Collapsed,
            IsHitTestVisible = false
        };
        rootPanel.Children.Add(measureHost);

        try
        {
            const double chromeWidth = 52;
            double fontSize = double.IsNaN(comboBox.FontSize) || comboBox.FontSize <= 0 ? 14 : comboBox.FontSize;

            TextBlock measureText = new()
            {
                Text = text,
                FontSize = fontSize,
                FontFamily = comboBox.FontFamily,
                FontWeight = comboBox.FontWeight
            };

            measureHost.Children.Add(measureText);
            measureText.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));

            if (measureText.DesiredSize.Width <= 0)
            {
                return;
            }

            double fittedWidth = Math.Ceiling(measureText.DesiredSize.Width) + chromeWidth;
            comboBox.Width = fittedWidth;
            comboBox.MinWidth = fittedWidth;
        }
        finally
        {
            rootPanel.Children.Remove(measureHost);
        }
    }

    private static string GetComboBoxItemText(object? item)
    {
        return item is ComboBoxItem comboBoxItem
            ? comboBoxItem.Content?.ToString() ?? string.Empty
            : item?.ToString() ?? string.Empty;
    }

    private void ApplyWindowIcon()
    {
        string iconPath = Path.Combine(AppContext.BaseDirectory, "BackupPilot.ico");
        if (!File.Exists(iconPath))
        {
            return;
        }

        IntPtr windowHandle = WindowNative.GetWindowHandle(this);
        WindowId windowId = Win32Interop.GetWindowIdFromWindow(windowHandle);
        AppWindow appWindow = AppWindow.GetFromWindowId(windowId);
        appWindow.SetIcon(iconPath);
    }

    private async Task LoadSettingsAsync()
    {
        try
        {
            settings = await settingsService.LoadAsync();
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            settings = new BackupSettings();
            await ShowMessageAsync("設定を読み込めませんでした。", $"新しい設定で起動します。{Environment.NewLine}{ex.Message}");
        }

        NormalizeSettings();
        ApplySettingsToUi();

        AppendLog("BackupPilot を起動しました。");
    }

    private async Task SaveSettingsAsync()
    {
        if (isUiUpdating)
        {
            return;
        }

        try
        {
            await settingsService.SaveAsync(settings);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            AppendLog($"設定を保存できませんでした: {ex.Message}");
        }
    }

    private void ApplySettingsToUi()
    {
        BackupJob? jobToSelect = settings.Jobs.FirstOrDefault();

        isUiUpdating = true;
        JobsListView.ItemsSource = settings.Jobs;
        JobsListView.SelectedItem = null;
        CopyModeComboBox.SelectedIndex = settings.CopyMode == CopyMode.Force ? 1 : 0;
        OverwriteModeComboBox.SelectedIndex = settings.OverwriteMode == OverwriteMode.SkipExisting ? 1 : 0;
        ThemeModeComboBox.SelectedIndex = GetThemeModeIndex(settings.ThemeMode);
        VersionTextBlock.Text = $"BackupPilot {CurrentVersion}";
        ApplyTheme();
        selectedJob = null;
        JobNameTextBox.Text = string.Empty;
        SourcePathTextBox.Text = string.Empty;
        DestinationPathTextBox.Text = string.Empty;
        isUiUpdating = false;

        if (jobToSelect is not null)
        {
            JobsListView.SelectedItem = jobToSelect;
        }

        QueueFitComboBoxWidth(OverwriteModeComboBox);
    }

    private void NormalizeSettings()
    {
        settings.Jobs ??= new ObservableCollection<BackupJob>();

        foreach (BackupJob job in settings.Jobs)
        {
            if (string.IsNullOrWhiteSpace(job.DestinationPath))
            {
                job.DestinationPath = ResolveLegacyDestinationPath(job);
            }

            if (IsUnsetJobName(job.Name))
            {
                job.Name = GenerateDefaultJobName(job.SourcePath);
            }
        }
    }

    private string ResolveLegacyDestinationPath(BackupJob job)
    {
        if (job.DestinationMode == DestinationMode.Individual && !string.IsNullOrWhiteSpace(job.IndividualDestinationPath))
        {
            return job.IndividualDestinationPath.Trim();
        }

        if (!string.IsNullOrWhiteSpace(settings.CommonDestinationRoot))
        {
            return settings.CommonDestinationRoot.Trim();
        }

        if (!string.IsNullOrWhiteSpace(job.IndividualDestinationPath))
        {
            return job.IndividualDestinationPath.Trim();
        }

        return string.Empty;
    }

    private void AddJobButton_Click(object sender, RoutedEventArgs e)
    {
        BackupJob job = new()
        {
            Name = string.Empty
        };

        settings.Jobs.Add(job);
        JobsListView.SelectedItem = job;
        _ = SaveSettingsAsync();
    }

    private async void DeleteJobButton_Click(object sender, RoutedEventArgs e)
    {
        if (selectedJob is null)
        {
            return;
        }

        settings.Jobs.Remove(selectedJob);
        selectedJob = null;
        ClearJobEditor();
        await SaveSettingsAsync();
    }

    private async void ThemeModeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (isUiUpdating)
        {
            return;
        }

        settings.ThemeMode = GetSelectedThemeMode();
        ApplyTheme();
        await SaveSettingsAsync();
    }

    private async void CheckUpdateButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            using HttpClient httpClient = new();
            httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("BackupPilot");

            using HttpResponseMessage response = await httpClient.GetAsync(LatestReleaseApiUrl);
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                await ShowUpdateMessageAsync(
                    "アップデート確認",
                    $"現在のバージョン: {CurrentVersion}{Environment.NewLine}GitHub Releases に公開済みアップデートはまだありません。",
                    ReleasesUrl);
                return;
            }

            response.EnsureSuccessStatusCode();

            await using Stream stream = await response.Content.ReadAsStreamAsync();
            using JsonDocument releaseJson = await JsonDocument.ParseAsync(stream);
            JsonElement root = releaseJson.RootElement;
            string latestVersion = root.TryGetProperty("tag_name", out JsonElement tagNameElement)
                ? tagNameElement.GetString() ?? "不明"
                : "不明";
            string releaseUrl = root.TryGetProperty("html_url", out JsonElement htmlUrlElement)
                ? htmlUrlElement.GetString() ?? ReleasesUrl
                : ReleasesUrl;
            string updateStatus = IsNewerVersion(latestVersion, CurrentVersion)
                ? "新しいバージョンがあります。"
                : "現在のバージョンは最新です。";

            await ShowUpdateMessageAsync(
                "アップデート確認",
                $"現在のバージョン: {CurrentVersion}{Environment.NewLine}最新のRelease: {latestVersion}{Environment.NewLine}{updateStatus}",
                releaseUrl);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            await ShowUpdateMessageAsync(
                "アップデート確認",
                $"GitHub Releases を確認できませんでした。{Environment.NewLine}{ex.Message}",
                ReleasesUrl);
        }
    }

    private void JobsListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        selectedJob = JobsListView.SelectedItem as BackupJob;
        LoadSelectedJobToEditor();
    }

    private async void SettingsComboBox_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (sender == OverwriteModeComboBox)
        {
            QueueFitComboBoxWidth(OverwriteModeComboBox);
        }

        await UpdateSettingsFromUiAsync();
    }

    private async Task UpdateSettingsFromUiAsync()
    {
        if (isUiUpdating)
        {
            return;
        }

        ApplySettingsFromUi();
        await SaveSettingsAsync();
    }

    private async void JobTextBox_Changed(object sender, TextChangedEventArgs e)
    {
        await UpdateSelectedJobFromUiAsync();
    }

    private async void JobEnabledCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        await SaveSettingsAsync();
    }

    private async Task UpdateSelectedJobFromUiAsync()
    {
        if (isUiUpdating || selectedJob is null)
        {
            return;
        }

        ApplySelectedJobFromUi();
        await SaveSettingsAsync();
    }

    private async void BrowseDestinationButton_Click(object sender, RoutedEventArgs e)
    {
        string? path = await PickFolderAsync();
        if (path is null)
        {
            return;
        }

        DestinationPathTextBox.Text = path;
    }

    private async void BrowseSourceFolderButton_Click(object sender, RoutedEventArgs e)
    {
        string? path = await PickFolderAsync();
        if (path is null)
        {
            return;
        }

        SourcePathTextBox.Text = path;
        FillDefaultJobNameIfBlank();
    }

    private async void BrowseSourceFileButton_Click(object sender, RoutedEventArgs e)
    {
        FileOpenPicker picker = new()
        {
            ViewMode = PickerViewMode.List,
            SuggestedStartLocation = PickerLocationId.DocumentsLibrary
        };
        picker.FileTypeFilter.Add("*");
        InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(this));

        Windows.Storage.StorageFile? file = await picker.PickSingleFileAsync();
        if (file is null)
        {
            return;
        }

        SourcePathTextBox.Text = file.Path;
        FillDefaultJobNameIfBlank();
    }

    private async void ExportJsonButton_Click(object sender, RoutedEventArgs e)
    {
        string? path = await PickSaveJsonFileAsync();
        if (path is null)
        {
            return;
        }

        try
        {
            ApplySettingsFromUi();
            ApplySelectedJobFromUi();
            await settingsService.SaveToFileAsync(path, settings);
            AppendLog($"JSON保存: {path}");
            await ShowMessageAsync("JSONを保存しました。", path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            await ShowMessageAsync("JSONを保存できませんでした。", ex.Message);
        }
    }

    private async void ImportJsonButton_Click(object sender, RoutedEventArgs e)
    {
        string? path = await PickOpenJsonFileAsync();
        if (path is null)
        {
            return;
        }

        try
        {
            BackupSettings importedSettings = await settingsService.LoadFromFileAsync(path);
            settings = importedSettings;
            NormalizeSettings();
            ApplySettingsToUi();
            await SaveSettingsAsync();
            AppendLog($"JSON読込: {path}");
            await ShowMessageAsync("JSONを読み込みました。", "読み込んだ内容を通常設定にも保存しました。");
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException or NotSupportedException)
        {
            await ShowMessageAsync("JSONを読み込めませんでした。", ex.Message);
        }
    }

    private async void RunButton_Click(object sender, RoutedEventArgs e)
    {
        if (!await PreparePlanAsync("開始"))
        {
            return;
        }

        IReadOnlyList<BackupPlanEntry> plan = CreateCurrentPlan();
        int copyCount = plan.Count(entry => entry.Action == PlanAction.Copy);
        if (copyCount == 0)
        {
            WritePlanSummary(plan, "実行前確認");
            await ShowMessageAsync("コピー対象はありません。", "変更がないか、設定にエラーがあります。");
            return;
        }

        WritePlanSummary(plan, "実行前確認");
        SetBusy(true);
        backupCancellation = new CancellationTokenSource();

        try
        {
            Progress<string> logProgress = new(AppendLog);
            Progress<double> progress = new(value => ProgressBar.Value = value);
            BackupRunSummary summary = await backupService.ExecuteAsync(plan, logProgress, progress, backupCancellation.Token);
            SummaryTextBlock.Text = $"コピー {summary.CopiedCount} / スキップ {summary.SkippedCount} / エラー {summary.ErrorCount}";
            AppendLog($"完了: コピー {summary.CopiedCount} 件、スキップ {summary.SkippedCount} 件、エラー {summary.ErrorCount} 件、コピー容量 {FormatBytes(summary.CopiedBytes)}");
        }
        catch (OperationCanceledException)
        {
            AppendLog("キャンセルしました。");
        }
        finally
        {
            backupCancellation?.Dispose();
            backupCancellation = null;
            SetBusy(false);
        }
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        backupCancellation?.Cancel();
    }

    private async Task<bool> PreparePlanAsync(string actionName)
    {
        ApplySettingsFromUi();
        ApplySelectedJobFromUi();

        if (settings.Jobs.Any(job => job.IsEnabled && !string.IsNullOrWhiteSpace(job.SourcePath)))
        {
            return true;
        }

        SummaryTextBlock.Text = "実行対象なし";
        ProgressBar.Value = 0;
        await ShowMessageAsync($"{actionName}できる対象がありません。", "左のバックアップ対象で、実行したい項目にチェックを入れてください。");
        return false;
    }

    private IReadOnlyList<BackupPlanEntry> CreateCurrentPlan()
    {
        ApplySettingsFromUi();
        ApplySelectedJobFromUi();
        return comparisonService.CreatePlan(settings);
    }

    private void ApplySettingsFromUi()
    {
        settings.CopyMode = CopyModeComboBox.SelectedIndex == 1 ? CopyMode.Force : CopyMode.Differential;
        settings.OverwriteMode = OverwriteModeComboBox.SelectedIndex == 1 ? OverwriteMode.SkipExisting : OverwriteMode.Allow;
        settings.ThemeMode = GetSelectedThemeMode();
    }

    private void ApplyTheme()
    {
        if (Content is FrameworkElement rootElement)
        {
            rootElement.RequestedTheme = settings.ThemeMode switch
            {
                AppThemeMode.Light => ElementTheme.Light,
                AppThemeMode.Dark => ElementTheme.Dark,
                _ => ElementTheme.Default
            };
        }
    }

    private AppThemeMode GetSelectedThemeMode()
    {
        return ThemeModeComboBox.SelectedIndex switch
        {
            1 => AppThemeMode.Light,
            2 => AppThemeMode.Dark,
            _ => AppThemeMode.System
        };
    }

    private static int GetThemeModeIndex(AppThemeMode themeMode)
    {
        return themeMode switch
        {
            AppThemeMode.Light => 1,
            AppThemeMode.Dark => 2,
            _ => 0
        };
    }

    private void ApplySelectedJobFromUi()
    {
        if (selectedJob is null)
        {
            return;
        }

        selectedJob.SourcePath = SourcePathTextBox.Text.Trim();
        selectedJob.DestinationPath = DestinationPathTextBox.Text.Trim();
        selectedJob.Name = IsUnsetJobName(JobNameTextBox.Text)
            ? GenerateDefaultJobName(selectedJob.SourcePath)
            : JobNameTextBox.Text.Trim();
        selectedJob.ItemType = File.Exists(selectedJob.SourcePath) ? BackupItemType.File : BackupItemType.Folder;
    }

    private void WritePlanSummary(IReadOnlyList<BackupPlanEntry> plan, string title)
    {
        int copyCount = plan.Count(entry => entry.Action == PlanAction.Copy);
        int skipCount = plan.Count(entry => entry.Action == PlanAction.Skip);
        int errorCount = plan.Count(entry => entry.Action == PlanAction.Error);
        long copyBytes = plan.Where(entry => entry.Action == PlanAction.Copy).Sum(entry => entry.SizeBytes);

        LogTextBox.Text = string.Empty;
        AppendLog($"■ {title}");
        AppendLog($"コピー予定: {copyCount} 件 / スキップ: {skipCount} 件 / エラー: {errorCount} 件 / 予定容量: {FormatBytes(copyBytes)}");

        foreach (BackupPlanEntry entry in plan.Take(300))
        {
            AppendLog($"{entry.Action}: {entry.JobName} / {entry.RelativePath} - {entry.Reason}");
        }

        if (plan.Count > 300)
        {
            AppendLog($"表示件数を超えたため、残り {plan.Count - 300} 件は省略しました。");
        }

        SummaryTextBlock.Text = $"コピー予定 {copyCount} 件";
        ProgressBar.Value = 0;
    }

    private async Task<string?> PickFolderAsync()
    {
        FolderPicker picker = new()
        {
            SuggestedStartLocation = PickerLocationId.DocumentsLibrary
        };
        picker.FileTypeFilter.Add("*");
        InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(this));

        Windows.Storage.StorageFolder? folder = await picker.PickSingleFolderAsync();
        return folder?.Path;
    }

    private async Task<string?> PickSaveJsonFileAsync()
    {
        FileSavePicker picker = new()
        {
            SuggestedStartLocation = PickerLocationId.DocumentsLibrary,
            SuggestedFileName = "BackupPilot_settings"
        };
        picker.FileTypeChoices.Add("JSON ファイル", new List<string> { ".json" });
        InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(this));

        Windows.Storage.StorageFile? file = await picker.PickSaveFileAsync();
        return string.IsNullOrWhiteSpace(file?.Path) ? null : file.Path;
    }

    private async Task<string?> PickOpenJsonFileAsync()
    {
        FileOpenPicker picker = new()
        {
            ViewMode = PickerViewMode.List,
            SuggestedStartLocation = PickerLocationId.DocumentsLibrary
        };
        picker.FileTypeFilter.Add(".json");
        InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(this));

        Windows.Storage.StorageFile? file = await picker.PickSingleFileAsync();
        return string.IsNullOrWhiteSpace(file?.Path) ? null : file.Path;
    }

    private void LoadSelectedJobToEditor()
    {
        isUiUpdating = true;

        if (selectedJob is null)
        {
            ClearJobEditor();
            isUiUpdating = false;
            return;
        }

        JobNameTextBox.Text = selectedJob.Name;
        SourcePathTextBox.Text = selectedJob.SourcePath;
        DestinationPathTextBox.Text = selectedJob.DestinationPath;

        isUiUpdating = false;
    }

    private void ClearJobEditor()
    {
        isUiUpdating = true;
        JobNameTextBox.Text = string.Empty;
        SourcePathTextBox.Text = string.Empty;
        DestinationPathTextBox.Text = string.Empty;
        isUiUpdating = false;
    }

    private void FillDefaultJobNameIfBlank()
    {
        if (selectedJob is null || !IsUnsetJobName(JobNameTextBox.Text))
        {
            return;
        }

        JobNameTextBox.Text = GenerateDefaultJobName(SourcePathTextBox.Text.Trim());
    }

    private static string GenerateDefaultJobName(string sourcePath)
    {
        return GetSourceDisplayName(sourcePath);
    }

    private static string GetSourceDisplayName(string sourcePath)
    {
        if (string.IsNullOrWhiteSpace(sourcePath))
        {
            return "バックアップ対象";
        }

        string trimmedPath = sourcePath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        string name = Path.GetFileName(trimmedPath);
        return string.IsNullOrWhiteSpace(name) ? trimmedPath : name;
    }

    private static bool IsUnsetJobName(string value)
    {
        return string.IsNullOrWhiteSpace(value) || string.Equals(value.Trim(), "名称未設定", StringComparison.CurrentCulture);
    }

    private void SetBusy(bool isBusy)
    {
        ExportJsonButton.IsEnabled = !isBusy;
        ImportJsonButton.IsEnabled = !isBusy;
        RunButton.IsEnabled = !isBusy;
        CancelButton.IsEnabled = isBusy;
        JobsListView.IsEnabled = !isBusy;
    }

    private void AppendLog(string message)
    {
        string timestamp = DateTime.Now.ToString("HH:mm:ss");
        LogTextBox.Text += $"[{timestamp}] {message}{Environment.NewLine}";
        LogTextBox.SelectionStart = LogTextBox.Text.Length;
    }

    private async Task ShowMessageAsync(string title, string message)
    {
        ContentDialog dialog = new()
        {
            Title = title,
            Content = message,
            CloseButtonText = "OK",
            XamlRoot = ((FrameworkElement)Content).XamlRoot
        };

        await dialog.ShowAsync();
    }

    private async Task ShowUpdateMessageAsync(string title, string message, string releaseUrl)
    {
        ContentDialog dialog = new()
        {
            Title = title,
            Content = message,
            PrimaryButtonText = "Releasesを開く",
            CloseButtonText = "閉じる",
            XamlRoot = ((FrameworkElement)Content).XamlRoot
        };

        ContentDialogResult result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Primary)
        {
            await Launcher.LaunchUriAsync(new Uri(releaseUrl));
        }
    }

    private static bool IsNewerVersion(string latestVersionText, string currentVersionText)
    {
        string latest = latestVersionText.Trim().TrimStart('v', 'V');
        string current = currentVersionText.Trim().TrimStart('v', 'V');

        return Version.TryParse(latest, out Version? latestVersion)
            && Version.TryParse(current, out Version? currentVersion)
            && latestVersion > currentVersion;
    }

    private static string FormatBytes(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB", "TB"];
        double value = bytes;
        int unitIndex = 0;

        while (value >= 1024 && unitIndex < units.Length - 1)
        {
            value /= 1024;
            unitIndex++;
        }

        return $"{value:0.##} {units[unitIndex]}";
    }
}
