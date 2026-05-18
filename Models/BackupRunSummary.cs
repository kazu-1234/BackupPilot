// BackupPilot v1.0.0
// バックアップ実行結果の集計情報です。

namespace BackupPilot.Models;

public sealed class BackupRunSummary
{
    public int PlannedCount { get; set; }

    public int CopiedCount { get; set; }

    public int SkippedCount { get; set; }

    public int ErrorCount { get; set; }

    public long CopiedBytes { get; set; }
}
