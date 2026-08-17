namespace Helichrysum.Core.Analysis;

using System.IO;

/// <summary>
/// Result of a directory time-trust evaluation (F-Resolve-4a).
/// </summary>
public sealed record TimeTrustResult
{
    /// <summary>True if the directory's timestamps are unreliable (bulk-copy artifacts).</summary>
    public required bool IsCopyArtifact { get; init; }

    /// <summary>Fraction of files whose ctime fell inside the tight cluster window.</summary>
    public required double ClusterRatio { get; init; }

    /// <summary>Ratio of files with ctime ≈ mtime (created then never modified).</summary>
    public required double UnmodifiedRatio { get; init; }

    /// <summary>Explanation for the verdict.</summary>
    public required string Evidence { get; init; }
}

/// <summary>
/// Evaluates whether a directory's ctime/mtime stamps can be trusted as
/// newness evidence, or whether they reflect a one-time bulk copy / migration
/// (where every file received nearly the same ctime). When a tight cluster
/// covers the vast majority of files, the stamps are downgraded (F-Resolve-4a).
/// </summary>
public static class TimeTrustEvaluator
{
    /// <summary>Default cluster window in minutes (files within this → likely same copy).</summary>
    public const double DefaultClusterWindowMinutes = 60;

    /// <summary>Default cluster threshold: this ratio of files in-window → copy artifact.</summary>
    public const double DefaultClusterThreshold = 0.7;

    /// <summary>ctime ≈ mtime tolerance in seconds.</summary>
    private const double UnmodifiedToleranceSeconds = 5;

    /// <summary>
    /// Evaluates a directory using default thresholds.
    /// </summary>
    public static TimeTrustResult Evaluate(string directoryPath)
    {
        return Evaluate(directoryPath, DefaultClusterWindowMinutes, DefaultClusterThreshold);
    }

    /// <summary>
    /// Evaluates a directory, scanning all regular files directly inside it.
    /// </summary>
    public static TimeTrustResult Evaluate(
        string directoryPath,
        double clusterWindowMinutes,
        double clusterThreshold)
    {
        if (!Directory.Exists(directoryPath))
        {
            return new TimeTrustResult
            {
                IsCopyArtifact = false,
                ClusterRatio = 0,
                UnmodifiedRatio = 0,
                Evidence = "Directory does not exist",
            };
        }

        var files = Directory.EnumerateFiles(directoryPath).ToList();
        if (files.Count == 0)
        {
            return new TimeTrustResult
            {
                IsCopyArtifact = false,
                ClusterRatio = 0,
                UnmodifiedRatio = 0,
                Evidence = "No files to evaluate",
            };
        }

        // Collect ctime stamps.
        var stamps = new List<DateTimeOffset>(files.Count);
        int unmodifiedCount = 0;

        foreach (string file in files)
        {
            try
            {
                var info = new FileInfo(file);
                var ctime = info.CreationTimeUtc;
                var mtime = info.LastWriteTimeUtc;
                stamps.Add(ctime);

                if (Math.Abs((ctime - mtime).TotalSeconds) < UnmodifiedToleranceSeconds)
                {
                    unmodifiedCount++;
                }
            }
            catch
            {
                // Skip unreadable files.
            }
        }

        if (stamps.Count == 0)
        {
            return new TimeTrustResult
            {
                IsCopyArtifact = false,
                ClusterRatio = 0,
                UnmodifiedRatio = 0,
                Evidence = "No readable files",
            };
        }

        // Find the tightest one-hour window containing the most stamps.
        var median = stamps
            .OrderBy(s => s)
            .ToList();
        int best = 0;
        for (int i = 0; i < median.Count; i++)
        {
            var windowEnd = median[i].AddMinutes(clusterWindowMinutes);
            int count = 0;
            for (int j = i; j < median.Count && median[j] <= windowEnd; j++)
            {
                count++;
            }
            if (count > best) best = count;
        }

        double clusterRatio = (double)best / stamps.Count;
        double unmodifiedRatio = (double)unmodifiedCount / stamps.Count;

        bool isCopyArtifact = clusterRatio >= clusterThreshold;

        string evidence = isCopyArtifact
            ? $"ctime clustering: {best}/{stamps.Count} files within {clusterWindowMinutes:0} min window"
            : $"ctime spread: cluster ratio {clusterRatio:P0} below threshold {clusterThreshold:P0}";

        return new TimeTrustResult
        {
            IsCopyArtifact = isCopyArtifact,
            ClusterRatio = clusterRatio,
            UnmodifiedRatio = unmodifiedRatio,
            Evidence = evidence,
        };
    }
}