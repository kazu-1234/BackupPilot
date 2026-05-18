// BackupPilot v1.0.0
// プレビューと実行で共有するコピー計画の1行分です。

namespace BackupPilot.Models;

public sealed class BackupPlanEntry
{
    public required string JobName { get; init; }

    public required string SourcePath { get; init; }

    public required string DestinationPath { get; init; }

    public required string RelativePath { get; init; }

    public required PlanAction Action { get; init; }

    public required string Reason { get; init; }

    public long SizeBytes { get; init; }

    public bool IsDirectory { get; init; }
}
