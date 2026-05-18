// BackupPilot v1.2.8
// アプリ全体の保存設定を表します。

using System.Collections.ObjectModel;

namespace BackupPilot.Models;

public sealed class BackupSettings
{
    // 旧JSONとの互換性のために残します。新しいUIと実行処理では使用しません。
    public string CommonDestinationRoot { get; set; } = string.Empty;

    public CopyMode CopyMode { get; set; } = CopyMode.Differential;

    public OverwriteMode OverwriteMode { get; set; } = OverwriteMode.Allow;

    public AppThemeMode ThemeMode { get; set; } = AppThemeMode.System;

    public ObservableCollection<BackupJob> Jobs { get; set; } = [];
}
