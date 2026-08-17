namespace Helichrysum.Core.Analysis;

/// <summary>
/// An intent raised by a relation detector for a single object.
/// </summary>
public sealed record IntentCandidate
{
    /// <summary>Which relation raised this intent (e.g. "Moved", "StructuralSibling", "ArchivePair", "Duplicate").</summary>
    public required string Source { get; init; }

    /// <summary>The desired action ("Keep" or "Clean").</summary>
    public required string Action { get; init; }

    /// <summary>Priority of this source — higher wins in a conflict (F-Resolve-13).</summary>
    public required int Priority { get; init; }
}

/// <summary>
/// Result of applying the same-layer arbitration rule.
/// </summary>
public sealed record ArbitrationOutcome
{
    /// <summary>Whether a conflict existed (opposing Keep/Clean intents).</summary>
    public required bool HadConflict { get; init; }

    /// <summary>The winning action, or the sole action if no conflict.</summary>
    public required string ResolvedAction { get; init; }

    /// <summary>Source that won, or null if unambiguous.</summary>
    public string? WinningSource { get; init; }
}

/// <summary>
/// Resolves conflicting intents on the same object using the F-Resolve-13
/// priority ladder: Moved > StructuralSibling > ArchivePair > Duplicate.
/// Intents that agree are merged without conflict; only opposing intents
/// are arbitrated.
/// </summary>
public static class ConflictArbiter
{
    /// <summary>Priority ladder per source — higher wins (F-Resolve-13).</summary>
    private static readonly IReadOnlyDictionary<string, int> Priorities = new Dictionary<string, int>
    {
        ["Moved"] = 4,
        ["Renamed"] = 4,
        ["StructuralSibling"] = 3,
        ["ArchivePair"] = 2,
        ["Duplicate"] = 1,
        ["Versioned"] = 1,
        ["LinkReference"] = 5,
    };

    /// <summary>Gets the priority for a source, defaulting to 0 for unknown sources.</summary>
    public static int GetPriority(string source)
    {
        return Priorities.TryGetValue(source, out int p) ? p : 0;
    }

    /// <summary>
    /// Arbitrates a set of intents for one object.
    /// </summary>
    /// <param name="candidates">Intents raised by various detectors for the same object.</param>
    /// <returns>The resolved decisions.</returns>
    public static ArbitrationOutcome Arbitrate(IReadOnlyList<IntentCandidate> candidates)
    {
        if (candidates.Count == 0)
        {
            return new ArbitrationOutcome
            {
                HadConflict = false,
                ResolvedAction = "Unknown",
                WinningSource = null,
            };
        }

        bool anyKeep = candidates.Any(c => c.Action == "Keep");
        bool anyClean = candidates.Any(c => c.Action == "Clean");
        bool hadConflict = anyKeep && anyClean;

        if (!hadConflict)
        {
            // No conflict — merge the unanimous intent.
            string soleAction = candidates[0].Action;
            return new ArbitrationOutcome
            {
                HadConflict = false,
                ResolvedAction = soleAction,
                WinningSource = null,
            };
        }

        // Conflict: pick the highest-priority Keep (preserving data beats cleanup).
        var winning = candidates
            .Where(c => c.Action == "Keep")
            .OrderByDescending(c => c.Priority)
            .ThenByDescending(c => GetPriority(c.Source))
            .First();

        return new ArbitrationOutcome
        {
            HadConflict = true,
            ResolvedAction = "Keep",
            WinningSource = winning.Source,
        };
    }
}