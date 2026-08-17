using Helichrysum.Core.Analysis;

namespace Helichrysum.Core.Tests;

public sealed class ConflictArbiterTests
{
    [Fact]
    public void UnanimousKeep_NoConflict()
    {
        var candidates = new[]
        {
            new IntentCandidate { Source = "Moved", Action = "Keep", Priority = ConflictArbiter.GetPriority("Moved") },
            new IntentCandidate { Source = "StructuralSibling", Action = "Keep", Priority = ConflictArbiter.GetPriority("StructuralSibling") },
        };

        var result = ConflictArbiter.Arbitrate(candidates);

        Assert.False(result.HadConflict);
        Assert.Equal("Keep", result.ResolvedAction);
    }

    [Fact]
    public void MovedKeep_Beats_DuplicateClean()
    {
        var candidates = new[]
        {
            new IntentCandidate { Source = "Duplicate", Action = "Clean", Priority = ConflictArbiter.GetPriority("Duplicate") },
            new IntentCandidate { Source = "Moved", Action = "Keep", Priority = ConflictArbiter.GetPriority("Moved") },
        };

        var result = ConflictArbiter.Arbitrate(candidates);

        Assert.True(result.HadConflict);
        Assert.Equal("Keep", result.ResolvedAction);
        Assert.Equal("Moved", result.WinningSource);
    }

    [Fact]
    public void StructuralSiblingKeep_Beats_ArchivePairClean()
    {
        var candidates = new[]
        {
            new IntentCandidate { Source = "ArchivePair", Action = "Clean", Priority = ConflictArbiter.GetPriority("ArchivePair") },
            new IntentCandidate { Source = "StructuralSibling", Action = "Keep", Priority = ConflictArbiter.GetPriority("StructuralSibling") },
        };

        var result = ConflictArbiter.Arbitrate(candidates);

        Assert.True(result.HadConflict);
        Assert.Equal("Keep", result.ResolvedAction);
        Assert.Equal("StructuralSibling", result.WinningSource);
    }

    [Fact]
    public void UnanimousClean_NoConflict()
    {
        var candidates = new[]
        {
            new IntentCandidate { Source = "Duplicate", Action = "Clean", Priority = ConflictArbiter.GetPriority("Duplicate") },
            new IntentCandidate { Source = "ArchivePair", Action = "Clean", Priority = ConflictArbiter.GetPriority("ArchivePair") },
        };

        var result = ConflictArbiter.Arbitrate(candidates);

        Assert.False(result.HadConflict);
        Assert.Equal("Clean", result.ResolvedAction);
    }

    [Fact]
    public void EmptyIntents_Unknown()
    {
        var result = ConflictArbiter.Arbitrate([]);

        Assert.False(result.HadConflict);
        Assert.Equal("Unknown", result.ResolvedAction);
    }
}