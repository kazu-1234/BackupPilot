// BackupPilot v1.2.0
// 1件分のバックアップ対象を表すモデルです。

using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace BackupPilot.Models;

public sealed class BackupJob : INotifyPropertyChanged
{
    private bool isEnabled = true;
    private string name = "新しいバックアップ";
    private BackupItemType itemType = BackupItemType.Folder;
    private string sourcePath = string.Empty;
    private string destinationPath = string.Empty;
    private DestinationMode destinationMode = DestinationMode.CommonRoot;
    private string individualDestinationPath = string.Empty;

    public event PropertyChangedEventHandler? PropertyChanged;

    public bool IsEnabled
    {
        get => isEnabled;
        set => SetField(ref isEnabled, value);
    }

    public string Name
    {
        get => name;
        set => SetField(ref name, value);
    }

    public BackupItemType ItemType
    {
        get => itemType;
        set => SetField(ref itemType, value);
    }

    public string SourcePath
    {
        get => sourcePath;
        set => SetField(ref sourcePath, value);
    }

    public string DestinationPath
    {
        get => destinationPath;
        set => SetField(ref destinationPath, value);
    }

    // 旧JSONとの互換性のために残します。新しいUIと実行処理では使用しません。
    public DestinationMode DestinationMode
    {
        get => destinationMode;
        set => SetField(ref destinationMode, value);
    }

    // 旧JSONとの互換性のために残します。読み込み時に DestinationPath へ移行します。
    public string IndividualDestinationPath
    {
        get => individualDestinationPath;
        set => SetField(ref individualDestinationPath, value);
    }

    private void SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
