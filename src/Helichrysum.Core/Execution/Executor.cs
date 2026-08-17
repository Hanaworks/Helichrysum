namespace Helichrysum.Core.Execution;

using System.IO;
using Helichrysum.Core.Configuration;
using Helichrysum.Core.Hashing;
using Helichrysum.Core.Planning;

/// <summary>
/// Executes a processing plan with safety checks, staging, and logging.
/// Implements F-Exec-7~12: staging backup, integrity verification, TOCTOU prevention,
/// configurable backup strategy, and post-execution manifest generation.
/// </summary>
public sealed class Executor
{
    private readonly string _trashDirectory;
    private readonly string _stagingDirectory;
    private readonly List<ExecutionLogEntry> _log = [];

    /// <summary>
    /// Initializes a new instance of the <see cref="Executor"/> class.
    /// </summary>
    /// <param name="trashDirectory">Optional custom trash directory.</param>
    /// <param name="stagingDirectory">Optional custom staging directory.</param>
    /// <param name="strategy">Deletion backup strategy (F-Exec-9).</param>
    /// <param name="verifyBeforeExec">Whether to verify object hash before execution (F-Exec-11).</param>
    public Executor(
        string? trashDirectory = null,
        string? stagingDirectory = null,
        DeletionStrategy strategy = DeletionStrategy.DoubleBackup,
        bool verifyBeforeExec = true)
    {
        string baseDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".helichrysum");

        _trashDirectory = trashDirectory ?? Path.Combine(baseDir, "trash");
        _stagingDirectory = stagingDirectory ?? Path.Combine(baseDir, "staging");
        _strategy = strategy;
        _verifyBeforeExec = verifyBeforeExec;

        if (_strategy is DeletionStrategy.DoubleBackup or DeletionStrategy.StagingOnly)
        {
            Directory.CreateDirectory(_stagingDirectory);
        }

        if (_strategy is DeletionStrategy.DoubleBackup or DeletionStrategy.TrashOnly)
        {
            Directory.CreateDirectory(_trashDirectory);
        }
    }

    private readonly DeletionStrategy _strategy;
    private readonly bool _verifyBeforeExec;

    /// <summary>
    /// Gets the execution log (read-only).
    /// </summary>
    public IReadOnlyList<ExecutionLogEntry> Log => _log.AsReadOnly();

    /// <summary>
    /// Executes a single action on a file with pre-execution verification.
    /// </summary>
    public bool ExecuteAction(PlannedAction action, string sourcePath, string? expectedHash = null)
    {
        if (!File.Exists(sourcePath) && !Directory.Exists(sourcePath))
        {
            LogAction(action, sourcePath, null, "Skipped", "Path does not exist");
            return false;
        }

        // F-Exec-11: Pre-execution verification (TOCTOU prevention, config-controlled).
        if (_verifyBeforeExec && expectedHash != null && File.Exists(sourcePath))
        {
            string currentHash = HashService.ComputeSha256(sourcePath);
            if (currentHash != expectedHash)
            {
                LogAction(action, sourcePath, null, "Aborted", $"TOCTOU: hash changed (expected {expectedHash[..12]}..., got {currentHash[..12]}...)");
                return false;
            }
        }

        try
        {
            switch (action.ActionType)
            {
                case "MoveToTrash":
                    return MoveToTrash(action, sourcePath, expectedHash);

                case "Keep":
                    LogAction(action, sourcePath, null, "Skipped", "Marked as keep");
                    return true;

                default:
                    LogAction(action, sourcePath, null, "Failed", $"Unknown action type: {action.ActionType}");
                    return false;
            }
        }
        catch (Exception ex)
        {
            LogAction(action, sourcePath, null, "Failed", ex.Message);
            return false;
        }
    }

    /// <summary>
    /// Executes a complete processing plan with integrity verification.
    /// </summary>
    public int ExecutePlan(ProcessingPlan plan, Func<long, (string? path, string? hash)?> resolveObject)
    {
        int successCount = 0;

        foreach (var action in plan.Actions)
        {
            var resolved = resolveObject(action.ObjectId);

            if (resolved == null)
            {
                LogAction(action, "unknown", null, "Skipped", "Object not found in manifest");
                continue;
            }

            var (path, hash) = resolved.Value;

            if (path == null)
            {
                LogAction(action, path, null, "Skipped", "Object path not found");
                continue;
            }

            if (ExecuteAction(action, path, hash))
            {
                successCount++;
            }
        }

        return successCount;
    }

    private bool MoveToTrash(PlannedAction action, string sourcePath, string? expectedHash)
    {
        // TOCTOU verification is handled once at ExecuteAction level (F-Exec-11),
        // controlled by the VerifyBeforeExec configuration.

        string stagingId = Guid.NewGuid().ToString("N")[..12];

        // F-Exec-7: Two-phase — copy to staging first (DoubleBackup / StagingOnly).
        string? stagingPath = null;
        if (_strategy is DeletionStrategy.DoubleBackup or DeletionStrategy.StagingOnly
            && File.Exists(sourcePath))
        {
            stagingPath = Path.Combine(_stagingDirectory, $"{stagingId}_{Path.GetFileName(sourcePath)}");
            File.Copy(sourcePath, stagingPath, overwrite: true);

            // F-Exec-8: Verify the staging copy is faithful to the source content.
            // Compare against the current source file — this validates the copy
            // itself, independent of any earlier-recorded hash.
            string stagingHash = HashService.ComputeSha256(stagingPath);
            string sourceHash = HashService.ComputeSha256(sourcePath);

            if (stagingHash != sourceHash)
            {
                // F-Exec-8: Rollback — staging copy is not faithful.
                File.Delete(stagingPath);
                LogAction(action, sourcePath, stagingPath, "RolledBack", "Integrity check failed: staging copy does not match source");
                return false;
            }
        }

        // TrashOnly or DoubleBackup: move to trash.
        if (_strategy is DeletionStrategy.TrashOnly or DeletionStrategy.DoubleBackup)
        {
            string trashPath = Path.Combine(_trashDirectory, $"{stagingId}_{Path.GetFileName(sourcePath)}");

            if (File.Exists(sourcePath))
            {
                File.Move(sourcePath, trashPath);
            }
            else if (Directory.Exists(sourcePath))
            {
                Directory.Move(sourcePath, trashPath);
            }

            // F-Exec-8: Verify move succeeded.
            if (!File.Exists(trashPath) && !Directory.Exists(trashPath))
            {
                LogAction(action, sourcePath, stagingPath, "Failed", "Move to trash failed");
                return false;
            }

            LogAction(action, sourcePath, trashPath, "Completed",
                _strategy == DeletionStrategy.DoubleBackup
                    ? "Moved to trash with staging backup"
                    : "Moved to trash (trash only)");
            return true;
        }

        // StagingOnly strategy: keep source in place, only staging backup is made.
        if (stagingPath != null && File.Exists(stagingPath))
        {
            LogAction(action, sourcePath, stagingPath, "Completed", "Staging backup created (staging only mode, source preserved)");
            return true;
        }

        LogAction(action, sourcePath, null, "Failed", "No backup made under current strategy");
        return false;
    }

    private void LogAction(PlannedAction action, string? sourcePath, string? destinationPath, string status, string message)
    {
        _log.Add(new ExecutionLogEntry
        {
            ActionType = action.ActionType,
            ObjectId = action.ObjectId,
            SourcePath = sourcePath ?? "unknown",
            DestinationPath = destinationPath,
            Status = status,
            Message = message,
            Timestamp = DateTimeOffset.UtcNow,
        });
    }
}

public sealed record ExecutionLogEntry
{
    public required string ActionType { get; init; }
    public required long ObjectId { get; init; }
    public required string SourcePath { get; init; }
    public string? DestinationPath { get; init; }
    public required string Status { get; init; }
    public required string Message { get; init; }
    public required DateTimeOffset Timestamp { get; init; }
}