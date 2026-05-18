// BackupPilot v1.2.0
// コピー前の差分判定とコピー計画作成を担当します。

using BackupPilot.Models;

namespace BackupPilot.Services;

public sealed class FileComparisonService
{
    private const double TimestampToleranceSeconds = 2.0;

    public IReadOnlyList<BackupPlanEntry> CreatePlan(BackupSettings settings)
    {
        List<BackupPlanEntry> plan = [];

        foreach (BackupJob job in settings.Jobs.Where(job => job.IsEnabled))
        {
            plan.AddRange(CreatePlanForJob(settings, job));
        }

        return plan
            .OrderBy(entry => entry.JobName, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(entry => entry.RelativePath, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    private IEnumerable<BackupPlanEntry> CreatePlanForJob(BackupSettings settings, BackupJob job)
    {
        string sourcePath = job.SourcePath.Trim();
        if (string.IsNullOrWhiteSpace(sourcePath))
        {
            yield return CreateError(job, sourcePath, "コピー元が未設定です。");
            yield break;
        }

        if (!File.Exists(sourcePath) && !Directory.Exists(sourcePath))
        {
            yield return CreateError(job, sourcePath, "コピー元が存在しません。");
            yield break;
        }

        string? destinationRoot = ResolveDestinationRoot(job, sourcePath);
        if (string.IsNullOrWhiteSpace(destinationRoot))
        {
            yield return CreateError(job, sourcePath, "コピー先が未設定です。");
            yield break;
        }

        if (File.Exists(sourcePath))
        {
            string fileName = Path.GetFileName(sourcePath);
            string destinationPath = Path.Combine(destinationRoot, fileName);
            yield return CreateFileEntry(settings, job, sourcePath, destinationPath, fileName);
            yield break;
        }

        foreach (string directoryPath in Directory.EnumerateDirectories(sourcePath, "*", SearchOption.AllDirectories))
        {
            string relativePath = Path.GetRelativePath(sourcePath, directoryPath);
            string destinationPath = Path.Combine(destinationRoot, relativePath);

            if (!Directory.Exists(destinationPath))
            {
                yield return new BackupPlanEntry
                {
                    JobName = job.Name,
                    SourcePath = directoryPath,
                    DestinationPath = destinationPath,
                    RelativePath = relativePath,
                    Action = PlanAction.Copy,
                    Reason = "フォルダを作成します。",
                    SizeBytes = 0,
                    IsDirectory = true
                };
            }
        }

        foreach (string filePath in Directory.EnumerateFiles(sourcePath, "*", SearchOption.AllDirectories))
        {
            string relativePath = Path.GetRelativePath(sourcePath, filePath);
            string destinationPath = Path.Combine(destinationRoot, relativePath);
            yield return CreateFileEntry(settings, job, filePath, destinationPath, relativePath);
        }
    }

    private static string? ResolveDestinationRoot(BackupJob job, string sourcePath)
    {
        string rawDestination = job.DestinationPath;

        if (string.IsNullOrWhiteSpace(rawDestination))
        {
            return null;
        }

        return ResolveDestinationParent(rawDestination.Trim(), sourcePath);
    }

    private static string ResolveDestinationParent(string destinationParent, string sourcePath)
    {
        if (Directory.Exists(sourcePath))
        {
            string folderName = Path.GetFileName(sourcePath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            return string.IsNullOrWhiteSpace(folderName)
                ? destinationParent
                : Path.Combine(destinationParent, folderName);
        }

        return destinationParent;
    }

    private static BackupPlanEntry CreateFileEntry(
        BackupSettings settings,
        BackupJob job,
        string sourcePath,
        string destinationPath,
        string relativePath)
    {
        FileInfo sourceInfo = new(sourcePath);
        FileInfo destinationInfo = new(destinationPath);

        if (settings.OverwriteMode == OverwriteMode.SkipExisting && destinationInfo.Exists)
        {
            return CreateEntry(job, sourcePath, destinationPath, relativePath, PlanAction.Skip, "上書き禁止のためスキップします。", sourceInfo.Length);
        }

        if (settings.CopyMode == CopyMode.Force)
        {
            return CreateEntry(job, sourcePath, destinationPath, relativePath, PlanAction.Copy, "強制コピー対象です。", sourceInfo.Length);
        }

        if (!destinationInfo.Exists)
        {
            return CreateEntry(job, sourcePath, destinationPath, relativePath, PlanAction.Copy, "バックアップ先に存在しません。", sourceInfo.Length);
        }

        if (sourceInfo.Length != destinationInfo.Length)
        {
            return CreateEntry(job, sourcePath, destinationPath, relativePath, PlanAction.Copy, "サイズが異なります。", sourceInfo.Length);
        }

        double seconds = Math.Abs((sourceInfo.LastWriteTimeUtc - destinationInfo.LastWriteTimeUtc).TotalSeconds);
        if (seconds > TimestampToleranceSeconds)
        {
            return CreateEntry(job, sourcePath, destinationPath, relativePath, PlanAction.Copy, "更新日時が異なります。", sourceInfo.Length);
        }

        return CreateEntry(job, sourcePath, destinationPath, relativePath, PlanAction.Skip, "変更なしのためスキップします。", sourceInfo.Length);
    }

    private static BackupPlanEntry CreateEntry(
        BackupJob job,
        string sourcePath,
        string destinationPath,
        string relativePath,
        PlanAction action,
        string reason,
        long sizeBytes)
    {
        return new BackupPlanEntry
        {
            JobName = job.Name,
            SourcePath = sourcePath,
            DestinationPath = destinationPath,
            RelativePath = relativePath,
            Action = action,
            Reason = reason,
            SizeBytes = sizeBytes
        };
    }

    private static BackupPlanEntry CreateError(BackupJob job, string sourcePath, string reason)
    {
        return new BackupPlanEntry
        {
            JobName = job.Name,
            SourcePath = sourcePath,
            DestinationPath = string.Empty,
            RelativePath = string.Empty,
            Action = PlanAction.Error,
            Reason = reason,
            SizeBytes = 0
        };
    }

}
