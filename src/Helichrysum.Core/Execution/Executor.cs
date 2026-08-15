namespace Helichrysum.Core.Execution;

using System.IO;
using Helichrysum.Core.Planning;

/// <summary>
/// Executes a processing plan with safety checks, staging, and logging.
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
    /// Executes a single action on a file.
    /// </summary>
    /// <param name="action">The action to execute.</param>
    /// <param name="sourcePath">The source file path.</param>
    /// <returns>True if the action was executed successfully.</returns>
    public bool ExecuteAction(PlannedAction action, string sourcePath)
    {
        if (!File.Exists(sourcePath) && !Directory.Exists(sourcePath))
        {
            _log.Add(new ExecutionLogEntry
            {
                ActionType = action.ActionType,
                ObjectId = action.ObjectId,
                SourcePath = sourcePath,
                Status = "Skipped",
                Message = "Path does not exist",
                Timestamp = DateTimeOffset.UtcNow,
            });

            return false;
        }

        try
        {
            switch (action.ActionType)
            {
                case "MoveToTrash":
                    return MoveToTrash(action, sourcePath);

                case "Keep":
                    // No action needed.
                    _log.Add(new ExecutionLogEntry
                    {
                        ActionType = "Keep",
                        ObjectId = action.ObjectId,
                        SourcePath = sourcePath,
                        Status = "Skipped",
                        Message = "Marked as keep",
                        Timestamp = DateTimeOffset.UtcNow,
                    });
                    return true;

                default:
                    _log.Add(new ExecutionLogEntry
                    {
                        ActionType = action.ActionType,
                        ObjectId = action.ObjectId,
                        SourcePath = sourcePath,
                        Status = "Failed",
                        Message = $"Unknown action type: {action.ActionType}",
                        Timestamp = DateTimeOffset.UtcNow,
                    });
                    return false;
            }
        }
        catch (Exception ex)
        {
            _log.Add(new ExecutionLogEntry
            {
                ActionType = action.ActionType,
                ObjectId = action.ObjectId,
                SourcePath = sourcePath,
                Status = "Failed",
                Message = ex.Message,
                Timestamp = DateTimeOffset.UtcNow,
            });

            return false;
        }
    }

    /// <summary>
    /// Executes a complete processing plan.
    /// </summary>
    /// <param name="plan">The plan to execute.</param>
    /// <param name="resolvePath">A function to resolve object IDs to file paths.</param>
    /// <returns>The number of successfully executed actions.</returns>
    public int ExecutePlan(ProcessingPlan plan, Func<long, string?> resolvePath)
    {
        int successCount = 0;

        foreach (var action in plan.Actions)
        {
            string? sourcePath = resolvePath(action.ObjectId);

            if (sourcePath == null)
            {
                _log.Add(new ExecutionLogEntry
                {
                    ActionType = action.ActionType,
                    ObjectId = action.ObjectId,
                    SourcePath = "unknown",
                    Status = "Skipped",
                    Message = "Object not found in manifest",
                    Timestamp = DateTimeOffset.UtcNow,
                });

                continue;
            }

            if (ExecuteAction(action, sourcePath))
            {
                successCount++;
            }
        }

        return successCount;
    }

    private bool MoveToTrash(PlannedAction action, string sourcePath)
    {
        string trashPath = Path.Combine(_trashDirectory, $"{Guid.NewGuid():N}_{Path.GetFileName(sourcePath)}");
        string stagingPath = Path.Combine(_stagingDirectory, $"{Guid.NewGuid():N}_{Path.GetFileName(sourcePath)}");

        // Two-phase: copy to staging first, then move to trash.
        if (File.Exists(sourcePath))
        {
            File.Copy(sourcePath, stagingPath, overwrite: true);
            File.Move(sourcePath, trashPath);
        }
        else if (Directory.Exists(sourcePath))
        {
            Directory.Move(sourcePath, trashPath);
        }

        _log.Add(new ExecutionLogEntry
        {
            ActionType = "MoveToTrash",
            ObjectId = action.ObjectId,
            SourcePath = sourcePath,
            DestinationPath = trashPath,
            StagingPath = stagingPath,
            Status = "Completed",
            Message = "Moved to trash with staging backup",
            Timestamp = DateTimeOffset.UtcNow,
        });

        return true;
    }
}

/// <summary>
/// A single entry in the execution log.
/// </summary>
public sealed record ExecutionLogEntry
{
    public required string ActionType { get; init; }
    public required long ObjectId { get; init; }
    public required string SourcePath { get; init; }
    public string? DestinationPath { get; init; }
    public string? StagingPath { get; init; }
    public required string Status { get; init; }
    public required string Message { get; init; }
    public required DateTimeOffset Timestamp { get; init; }
}