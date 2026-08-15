namespace Helichrysum.Core.Hashing;

using System.IO;

/// <summary>
/// Defines the hash computation tiers, from cheapest to most expensive.
/// </summary>
public enum HashTier
{
    /// <summary>Metadata only (size, mtime). No file content is read.</summary>
    Metadata = 0,

    /// <summary>Sampled hash (head, middle, tail sections). Partial file read.</summary>
    SampledHash = 1,

    /// <summary>Full SHA256 hash. Entire file read.</summary>
    FullHash = 2,
}

/// <summary>
/// Determines the appropriate hash tier for comparing two files.
/// Implements the monotonic upgrade strategy: never downgrade.
/// </summary>
public static class HashTierManager
{
    /// <summary>
    /// Determines the minimum hash tier needed to compare two files.
    /// </summary>
    /// <param name="fileInfoA">File info for the first file.</param>
    /// <param name="fileInfoB">File info for the second file.</param>
    /// <returns>The minimum hash tier that should be computed.</returns>
    public static HashTier DetermineTier(FileInfo fileInfoA, FileInfo fileInfoB)
    {
        // If sizes differ, files are definitely different.
        if (fileInfoA.Length != fileInfoB.Length)
        {
            return HashTier.Metadata;
        }

        // Same size: check if mtime matches.
        long mtimeA = fileInfoA.LastWriteTimeUtc.Ticks;
        long mtimeB = fileInfoB.LastWriteTimeUtc.Ticks;

        if (mtimeA != mtimeB)
        {
            // Different mtime but same size → could be a version change.
            // Need at least sampled hash to confirm.
            return HashTier.SampledHash;
        }

        // Same size + same mtime → likely identical. Need full hash to confirm.
        return HashTier.FullHash;
    }
}