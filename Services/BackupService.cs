// BackupPilot v1.0.0
// コピー計画を順番に実行し、進捗と結果を返します。

using BackupPilot.Models;

namespace BackupPilot.Services;

public sealed class BackupService
{
    public async Task<BackupRunSummary> ExecuteAsync(
        IReadOnlyList<BackupPlanEntry> plan,
        IProgress<string> log,
        IProgress<double> progress,
        CancellationToken cancellationToken)
    {
        return await Task.Run(() =>
        {
            BackupRunSummary summary = new()
            {
                PlannedCount = plan.Count
            };

            int processed = 0;
            foreach (BackupPlanEntry entry in plan)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    switch (entry.Action)
                    {
                        case PlanAction.Copy:
                            CopyEntry(entry);
                            summary.CopiedCount++;
                            summary.CopiedBytes += entry.SizeBytes;
                            log.Report($"コピー: {entry.JobName} / {entry.RelativePath}");
                            break;

                        case PlanAction.Skip:
                            summary.SkippedCount++;
                            log.Report($"スキップ: {entry.JobName} / {entry.RelativePath} ({entry.Reason})");
                            break;

                        case PlanAction.Error:
                            summary.ErrorCount++;
                            log.Report($"エラー: {entry.JobName} ({entry.Reason})");
                            break;
                    }
                }
                catch (Exception ex)
                {
                    summary.ErrorCount++;
                    log.Report($"エラー: {entry.JobName} / {entry.RelativePath} ({ex.Message})");
                }
                finally
                {
                    processed++;
                    progress.Report(plan.Count == 0 ? 1.0 : processed / (double)plan.Count);
                }
            }

            return summary;
        }, cancellationToken);
    }

    private static void CopyEntry(BackupPlanEntry entry)
    {
        if (entry.IsDirectory)
        {
            Directory.CreateDirectory(entry.DestinationPath);
            return;
        }

        string? directory = Path.GetDirectoryName(entry.DestinationPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.Copy(entry.SourcePath, entry.DestinationPath, overwrite: true);

        FileInfo sourceInfo = new(entry.SourcePath);
        File.SetCreationTimeUtc(entry.DestinationPath, sourceInfo.CreationTimeUtc);
        File.SetLastWriteTimeUtc(entry.DestinationPath, sourceInfo.LastWriteTimeUtc);
        File.SetLastAccessTimeUtc(entry.DestinationPath, sourceInfo.LastAccessTimeUtc);
    }
}
