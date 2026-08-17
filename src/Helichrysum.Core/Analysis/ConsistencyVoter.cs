namespace Helichrysum.Core.Analysis;

using Helichrysum.Core.Manifest;

/// <summary>
/// Result of a consistency vote among copies of a logically-same file.
/// Implements F-Resolve-18: majority-vs-outlier detection.
/// </summary>
public sealed record ConsistencyVoteResult
{
    /// <summary>The hash value carried by the majority group (if any).</summary>
    public string? MajorityHash { get; init; }

    /// <summary>Number of copies in the majority group.</summary>
    public int MajorityCount { get; init; }

    /// <summary>Object IDs that differ from the majority (suspected corruption).</summary>
    public required List<long> OutlierIds { get; init; }

    /// <summary>True if a majority exists and there are outliers (Integrity_Suspected).</summary>
    public bool HasSuspectedOutliers => MajorityCount >= 2 && OutlierIds.Count > 0;
}

/// <summary>
/// Detects likely-corrupted copies by voting on content hashes.
/// For a set of copies of the same logical file, the hash held by ≥2 copies
/// wins; lone differing copies are marked as Integrity_Suspected (F-Resolve-18).
/// </summary>
public static class ConsistencyVoter
{
    /// <summary>
    /// Runs the majority-vote on a set of duplicate member objects.
    /// </summary>
    /// <param name="memberObjects">Filesystem objects sharing the same logical file.</param>
    /// <param name="hashProvider">Function mapping an object to its content hash.</param>
    /// <returns>A vote result with suspected outliers.</returns>
    public static ConsistencyVoteResult Vote(
        IReadOnlyList<FilesystemObject> memberObjects,
        Func<FilesystemObject, string?> hashProvider)
    {
        // Group members by their content hash.
        var hashCounts = new Dictionary<string, int>(StringComparer.Ordinal);
        var membersByHash = new Dictionary<string, List<long>>(StringComparer.Ordinal);

        foreach (var obj in memberObjects)
        {
            string? hash = hashProvider(obj);
            if (hash is null) continue;

            hashCounts[hash] = hashCounts.GetValueOrDefault(hash) + 1;
            if (!membersByHash.TryGetValue(hash, out var list))
            {
                list = [];
                membersByHash[hash] = list;
            }
            list.Add(obj.Id);
        }

        if (hashCounts.Count == 0)
        {
            return new ConsistencyVoteResult { OutlierIds = [] };
        }

        // Find the majority hash (highest count, tie → first).
        var majority = hashCounts
            .OrderByDescending(kv => kv.Value)
            .First();

        // Outliers = all hashes that are not the majority.
        var outliers = new List<long>();
        foreach (var kv in membersByHash)
        {
            if (kv.Key != majority.Key)
            {
                outliers.AddRange(kv.Value);
            }
        }

        return new ConsistencyVoteResult
        {
            MajorityHash = majority.Key,
            MajorityCount = majority.Value,
            OutlierIds = outliers,
        };
    }
}