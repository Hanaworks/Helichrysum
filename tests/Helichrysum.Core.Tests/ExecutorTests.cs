using Helichrysum.Core.Execution;
using Helichrysum.Core.Planning;

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
    public void MoveToTrash_FileMoved()
    {
        string filePath = Path.Combine(_tempDir, "test.txt");
        File.WriteAllText(filePath, "content");

        var action = new PlannedAction
        {
            ActionType = "MoveToTrash",
            ObjectId = 1,
        };

        bool result = _executor.ExecuteAction(action, filePath);

        Assert.True(result);
        Assert.False(File.Exists(filePath)); // Original file gone
        Assert.Single(_executor.Log);
        Assert.Equal("Completed", _executor.Log[0].Status);
    }

    [Fact]
    public void Keep_NoActionTaken()
    {
        string filePath = Path.Combine(_tempDir, "keep.txt");
        File.WriteAllText(filePath, "keep me");

        var action = new PlannedAction
        {
            ActionType = "Keep",
            ObjectId = 2,
        };

        bool result = _executor.ExecuteAction(action, filePath);

        Assert.True(result);
        Assert.True(File.Exists(filePath)); // File still exists
    }

    [Fact]
    public void MissingFile_Skipped()
    {
        var action = new PlannedAction
        {
            ActionType = "MoveToTrash",
            ObjectId = 99,
        };

        bool result = _executor.ExecuteAction(action, "/nonexistent/file.txt");

        Assert.False(result);
        Assert.Equal("Skipped", _executor.Log[0].Status);
    }

    [Fact]
    public void ExecutePlan_MultipleActions()
    {
        string fileA = Path.Combine(_tempDir, "a.txt");
        string fileB = Path.Combine(_tempDir, "b.txt");
        File.WriteAllText(fileA, "a");
        File.WriteAllText(fileB, "b");

        var plan = new ProcessingPlan
        {
            Id = "test-plan",
            CreatedAt = DateTimeOffset.UtcNow,
            Actions =
            [
                new PlannedAction { ActionType = "MoveToTrash", ObjectId = 10 },
                new PlannedAction { ActionType = "Keep", ObjectId = 20 },
            ],
        };

        int successCount = _executor.ExecutePlan(plan, id => id switch
        {
            10 => fileA,
            20 => fileB,
            _ => null,
        });

        Assert.Equal(2, successCount);
        Assert.False(File.Exists(fileA)); // Trashed
        Assert.True(File.Exists(fileB));  // Kept
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir)) Directory.Delete(_tempDir, true);
    }
}