// BackupPilot v1.0.0
// バックアップ設定で使用する選択肢を定義します。

namespace BackupPilot.Models;

public enum BackupItemType
{
    File,
    Folder
}

public enum DestinationMode
{
    CommonRoot,
    Individual
}

public enum CopyMode
{
    Differential,
    Force
}

public enum OverwriteMode
{
    Allow,
    SkipExisting
}

public enum AppThemeMode
{
    System,
    Light,
    Dark
}

public enum PlanAction
{
    Copy,
    Skip,
    Error
}
