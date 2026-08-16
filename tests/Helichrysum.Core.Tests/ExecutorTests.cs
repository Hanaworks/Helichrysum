using Helichrysum.Core.Execution;
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

    public void Dispose()
    {
        if (Directory.Exists(_tempDir)) Directory.Delete(_tempDir, true);
    }
}