using Helichrysum.Core.Execution;
using Helichrysum.Core.Configuration;
using Helichrysum.Core.Planning;
using Helichrysum.Core.Hashing;

namespace Helichrysum.Core.Tests;

public sealed class ExecutorTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _trashDir;
    private readonly string _stagingDir;
    private readonly Executor _executor;

    public ExecutorTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"helichrysum_exec_{Guid.NewGuid():N}");
        _trashDir = Path.Combine(_tempDir, "trash");
        _stagingDir = Path.Combine(_tempDir, "staging");
        Directory.CreateDirectory(_tempDir);
        _executor = new Executor(_trashDir, _stagingDir);
    }

    [Fact]
    public void MoveToTrash_WithStagingBackup()
    {
        string filePath = Path.Combine(_tempDir, "test.txt");
        File.WriteAllText(filePath, "content");
        string hash = HashService.ComputeSha256(filePath);

        var action = new PlannedAction { ActionType = "MoveToTrash", ObjectId = 1 };
        bool result = _executor.ExecuteAction(action, filePath, hash);

        Assert.True(result);
        Assert.False(File.Exists(filePath));
        Assert.Equal("Completed", _executor.Log[0].Status);
        Assert.True(Directory.GetFiles(_stagingDir).Length > 0, "Staging backup should exist");
    }

    [Fact]
    public void TOCTOU_HashMismatch_Aborts()
    {
        string filePath = Path.Combine(_tempDir, "secret.txt");
        File.WriteAllText(filePath, "original content");
        string wrongHash = HashService.ComputeSha256(filePath);

        File.WriteAllText(filePath, "modified content");

        var action = new PlannedAction { ActionType = "MoveToTrash", ObjectId = 1 };
        bool result = _executor.ExecuteAction(action, filePath, wrongHash);

        Assert.False(result);
        Assert.Equal("Aborted", _executor.Log[0].Status);
        Assert.True(File.Exists(filePath), "File should still exist after abort");
    }

    [Fact]
    public void IntegrityCheck_Fails_Rollback()
    {
        string filePath = Path.Combine(_tempDir, "important.txt");
        File.WriteAllText(filePath, "critical data");
        string fakeHash = "0000000000000000000000000000000000000000000000000000000000000000";

        var action = new PlannedAction { ActionType = "MoveToTrash", ObjectId = 1 };
        bool result = _executor.ExecuteAction(action, filePath, fakeHash);

        Assert.False(result);
        Assert.True(File.Exists(filePath), "Original should remain on failure");
    }

    [Fact]
    public void ExecutePlan_WithHashVerification()
    {
        string fileA = Path.Combine(_tempDir, "a.txt");
        File.WriteAllText(fileA, "file a");
        string hashA = HashService.ComputeSha256(fileA);

        var plan = new ProcessingPlan
        {
            Id = "test-plan", CreatedAt = DateTimeOffset.UtcNow,
            Actions = [new PlannedAction { ActionType = "Keep", ObjectId = 10 }],
        };

        int success = _executor.ExecutePlan(plan, id => id == 10 ? (fileA, hashA) : null);
        Assert.Equal(1, success);
    }

    [Fact]
    public void MissingObject_Skipped()
    {
        var plan = new ProcessingPlan
        {
            Id = "test-plan", CreatedAt = DateTimeOffset.UtcNow,
            Actions = [new PlannedAction { ActionType = "MoveToTrash", ObjectId = 99 }],
        };

        int success = _executor.ExecutePlan(plan, _ => null);
        Assert.Equal(0, success);
        Assert.Equal("Skipped", _executor.Log[0].Status);
    }

    [Fact]
    public void TrashOnly_Strategy_NoStagingCopy()
    {
        string filePath = Path.Combine(_tempDir, "trashonly.txt");
        File.WriteAllText(filePath, "content");
        string hash = HashService.ComputeSha256(filePath);

        var executor = new Executor(_trashDir, _stagingDir, DeletionStrategy.TrashOnly);
        var action = new PlannedAction { ActionType = "MoveToTrash", ObjectId = 1 };
        bool result = executor.ExecuteAction(action, filePath, hash);

        Assert.True(result);
        Assert.False(File.Exists(filePath));
        Assert.Empty(Directory.GetFiles(_stagingDir)); // No staging copy in TrashOnly mode
    }

    [Fact]
    public void StagingOnly_Strategy_SourcePreserved()
    {
        string filePath = Path.Combine(_tempDir, "stagingonly.txt");
        File.WriteAllText(filePath, "content");
        string hash = HashService.ComputeSha256(filePath);

        var executor = new Executor(_trashDir, _stagingDir, DeletionStrategy.StagingOnly);
        var action = new PlannedAction { ActionType = "MoveToTrash", ObjectId = 1 };
        bool result = executor.ExecuteAction(action, filePath, hash);

        Assert.True(result);
        Assert.True(File.Exists(filePath)); // Source preserved
        Assert.True(Directory.GetFiles(_stagingDir).Length > 0); // Staging backup made
    }

    [Fact]
    public void DoubleBackup_Strategy_BothCopiesMade()
    {
        string filePath = Path.Combine(_tempDir, "double.txt");
        File.WriteAllText(filePath, "content");
        string hash = HashService.ComputeSha256(filePath);

        var executor = new Executor(_trashDir, _stagingDir, DeletionStrategy.DoubleBackup);
        var action = new PlannedAction { ActionType = "MoveToTrash", ObjectId = 1 };
        bool result = executor.ExecuteAction(action, filePath, hash);

        Assert.True(result);
        Assert.False(File.Exists(filePath)); // Source moved to trash
        Assert.NotEmpty(Directory.GetFiles(_trashDir));   // Trash copy
        Assert.NotEmpty(Directory.GetFiles(_stagingDir)); // Staging copy
    }

    [Fact]
    public void VerifyBeforeExec_Disabled_SkipsHashCheck()
    {
        string filePath = Path.Combine(_tempDir, "noverify.txt");
        File.WriteAllText(filePath, "original");
        string originalHash = HashService.ComputeSha256(filePath);

        // Modify the file after recording hash — should NOT abort when verify disabled.
        File.WriteAllText(filePath, "modified");

        var executor = new Executor(_trashDir, _stagingDir, DeletionStrategy.DoubleBackup, verifyBeforeExec: false);
        var action = new PlannedAction { ActionType = "MoveToTrash", ObjectId = 1 };
        bool result = executor.ExecuteAction(action, filePath, originalHash);

        Assert.True(result);
        Assert.Equal("Completed", executor.Log[0].Status);
    }

    private static PlannedAction Action(string type, long id) => new()
    {
        ActionType = type,
        ObjectId = id,
    };

    [Fact]
    public void SaveAndLoadExecutionLog_RoundTrips()
    {
        string filePath = Path.Combine(_tempDir, "resume.txt");
        File.WriteAllText(filePath, "content");
        string hash = HashService.ComputeSha256(filePath);

        var action = new PlannedAction { ActionType = "MoveToTrash", ObjectId = 7 };
        Assert.True(_executor.ExecuteAction(action, filePath, hash));

        string logPath = Path.Combine(_tempDir, "exec_log.json");
        _executor.SaveExecutionLog(logPath);

        var completed = Executor.LoadCompletedObjectIds(logPath);

        Assert.Contains(7L, completed);
    }

    [Fact]
    public void ExecutePlan_WithSkipSet_SkipsCompletedObjects()
    {
        string filePath = Path.Combine(_tempDir, "skip.txt");
        File.WriteAllText(filePath, "content");
        string hash = HashService.ComputeSha256(filePath);

        var plan = new ProcessingPlan
        {
            Id = "resume-plan",
            CreatedAt = DateTimeOffset.UtcNow,
            Actions =
            [
                new PlannedAction { ActionType = "MoveToTrash", ObjectId = 10 },
                new PlannedAction { ActionType = "MoveToTrash", ObjectId = 11 },
            ],
        };

        var skipSet = new HashSet<long> { 10 };
        var executor = new Executor(_trashDir, _stagingDir);

        int success = executor.ExecutePlan(plan, id =>
        {
            if (id == 10) return (filePath, hash);
            if (id == 11) return (filePath, hash);
            return null;
        }, skipSet);

        // Object 10 skipped (resume), object 11 executed → 1 success.
        Assert.Equal(1, success);
        Assert.Contains(executor.Log, e => e.Status == "Skipped" && e.Message.Contains("resume"));
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir)) TestFileHelper.DeleteDirectoryWithRetry(_tempDir);
    }
}