namespace Helichrysum.Core.Execution;

using System.IO;
using Helichrysum.Core.Hashing;
using Helichrysum.Core.Planning;

/// <summary>
/// Executes a processing plan with safety checks, staging, and logging.
/// Implements F-Exec-7~12: staging backup, integrity verification, TOCTOU prevention.
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
    public Executor(string? trashDirectory = null, string? stagingDirectory = null)
    {
        string baseDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".helichrysum");

        _trashDirectory = trashDirectory ?? Path.Combine(baseDir, "trash");
        _stagingDirectory = stagingDirectory ?? Path.Combine(baseDir, "staging");

        Directory.CreateDirectory(_trashDirectory);
        Directory.CreateDirectory(_stagingDirectory);
    }

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

        // F-Exec-11: Pre-execution verification (TOCTOU prevention).
        if (expectedHash != null && File.Exists(sourcePath))
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
        // F-Exec-7: Two-phase — copy to staging first.
        string stagingId = Guid.NewGuid().ToString("N")[..12];
        string stagingPath = Path.Combine(_stagingDirectory, $"{stagingId}_{Path.GetFileName(sourcePath)}");

        if (File.Exists(sourcePath))
        {
            File.Copy(sourcePath, stagingPath, overwrite: true);
        }

        // F-Exec-8: Verify staging copy integrity before removing original.
        if (File.Exists(stagingPath))
        {
            string stagingHash = HashService.ComputeSha256(stagingPath);
            string originalHash = expectedHash ?? HashService.ComputeSha256(sourcePath);

            if (stagingHash != originalHash)
            {
                // F-Exec-8: Rollback — staging copy is corrupted.
                File.Delete(stagingPath);
                LogAction(action, sourcePath, stagingPath, "RolledBack", "Integrity check failed: staging hash mismatch");
                return false;
            }
        }

        // Move to trash.
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

        LogAction(action, sourcePath, trashPath, "Completed", "Moved to trash with staging backup");
        return true;
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