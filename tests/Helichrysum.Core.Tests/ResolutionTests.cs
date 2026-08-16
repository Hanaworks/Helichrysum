using Helichrysum.Core.Analysis;

namespace Helichrysum.Core.Tests;

public sealed class ResolutionTests : IDisposable
{
    private readonly string _tempDir;

    public ResolutionTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"helichrysum_res_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    [Fact]
    public void Equality_IdenticalFiles_Grouped()
    {
        string fileA = Path.Combine(_tempDir, "a.txt");
        string fileB = Path.Combine(_tempDir, "b.txt");
        File.WriteAllText(fileA, "Hello, World!");
        File.WriteAllText(fileB, "Hello, World!");

        var result = ResolutionResolver.ResolveFilePair(fileA, fileB);

        Assert.Equal(ResolutionKind.Equality, result.Kind);
        Assert.Equal(1.0, result.Confidence);
        Assert.Contains("HashMatch", result.Evidence);
    }

    [Fact]
    public void Compatibility_OldContent_ContainedInNew()
    {
        string oldFile = Path.Combine(_tempDir, "note_v1.txt");
        string newFile = Path.Combine(_tempDir, "note_v2.txt");
        File.WriteAllText(oldFile, "The quick brown fox.");
        File.WriteAllText(newFile, "The quick brown fox. jumps over the lazy dog.");

        var result = ResolutionResolver.ResolveFilePair(oldFile, newFile);

        Assert.Equal(ResolutionKind.Compatibility, result.Kind);
        Assert.Contains("ContentContainment", result.Evidence);
    }

    [Fact]
    public void Conflict_DifferentContent_NotCompatible()
    {
        string fileA = Path.Combine(_tempDir, "a.txt");
        string fileB = Path.Combine(_tempDir, "b.txt");
        File.WriteAllText(fileA, "Alpha content here. Extra text.");
        File.WriteAllText(fileB, "Beta content there.");

        var result = ResolutionResolver.ResolveFilePair(fileA, fileB);

        Assert.Equal(ResolutionKind.Conflict, result.Kind);
    }

    [Fact]
    public void Directory_OldFilesSubsetOfNew_Compatible()
    {
        string oldDir = Directory.CreateDirectory(Path.Combine(_tempDir, "old")).FullName;
        string newDir = Directory.CreateDirectory(Path.Combine(_tempDir, "new")).FullName;

        File.WriteAllText(Path.Combine(oldDir, "readme.md"), "docs");
        File.WriteAllText(Path.Combine(newDir, "readme.md"), "docs");
        File.WriteAllText(Path.Combine(newDir, "extra.txt"), "new file");

        var result = ResolutionResolver.ResolveDirectoryPair(oldDir, newDir);

        Assert.Equal(ResolutionKind.Compatibility, result.Kind);
    }

    [Fact]
    public void Directory_OldHasUniqueFile_NotCompatible()
    {
        string oldDir = Directory.CreateDirectory(Path.Combine(_tempDir, "old")).FullName;
        string newDir = Directory.CreateDirectory(Path.Combine(_tempDir, "new")).FullName;

        File.WriteAllText(Path.Combine(oldDir, "unique.txt"), "old only");
        File.WriteAllText(Path.Combine(newDir, "readme.md"), "docs");

        var result = ResolutionResolver.ResolveDirectoryPair(oldDir, newDir);

        Assert.Equal(ResolutionKind.Conflict, result.Kind);
    }

    [Fact]
    public void Resolution_Serialization_RoundTrip()
    {
        var result = new ResolutionResult
        {
            Kind = ResolutionKind.Compatibility,
            Confidence = 0.9,
            Evidence = "test",
        };

        string json = result.ToStorageString();
        var restored = ResolutionResult.FromStorageString(json);

        Assert.Equal(result.Kind, restored.Kind);
        Assert.Equal(result.Confidence, restored.Confidence);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir)) Directory.Delete(_tempDir, true);
    }
}