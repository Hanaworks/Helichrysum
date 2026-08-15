namespace Helichrysum.Core.Analysis;

using System.IO;

/// <summary>
/// Result of a versioned file detection.
/// </summary>
public sealed record VersionedResult
{
    public bool IsVersioned { get; init; }
    public double Confidence { get; init; }
}

/// <summary>
/// Detects files that are different versions of the same document
/// (same name, close size, content partially similar).
/// </summary>
public static class VersionedDetector
{
    private const double MaxSizeRatio = 3.0;  // Max size ratio to still be considered versioned
    private const double MinSizeRatio = 0.5;  // Min size ratio

    /// <summary>
    /// Detects whether two files are different versions of the same document.
    /// </summary>
    public static VersionedResult Detect(string filePathA, string filePathB)
    {
        if (!File.Exists(filePathA) || !File.Exists(filePathB))
        {
            return new VersionedResult();
        }

        string nameA = Path.GetFileName(filePathA);
        string nameB = Path.GetFileName(filePathB);

        // Must have the same name.
        if (!nameA.Equals(nameB, StringComparison.OrdinalIgnoreCase))
        {
            return new VersionedResult();
        }

        var infoA = new FileInfo(filePathA);
        var infoB = new FileInfo(filePathB);

        long sizeA = infoA.Length;
        long sizeB = infoB.Length;

        // Same size → probably exact duplicate, not versioned.
        if (sizeA == sizeB)
        {
            return new VersionedResult();
        }

        // Check size ratio.
        double ratio = (double)Math.Max(sizeA, sizeB) / Math.Min(sizeA, sizeB);

        if (ratio > MaxSizeRatio || ratio < MinSizeRatio)
        {
            // Too different in size.
            return new VersionedResult();
        }

        // Same name, close size → likely versioned.
        double confidence = Math.Min(1.0, 1.0 / ratio);

        return new VersionedResult
        {
            IsVersioned = true,
            Confidence = confidence,
        };
    }
}