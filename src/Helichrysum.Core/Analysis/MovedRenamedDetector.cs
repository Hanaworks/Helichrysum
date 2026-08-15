namespace Helichrysum.Core.Analysis;

using System.IO;
using Helichrysum.Core.Hashing;

/// <summary>
/// Result of a moved/renamed detection.
/// </summary>
public sealed record MovedRenamedResult
{
    public bool IsMoved { get; init; }
    public bool IsRenamed { get; init; }
    public double Confidence { get; init; }
}

/// <summary>
/// Detects files that have been moved (same name, same content, different directory)
/// or renamed (same content, different name).
/// </summary>
public static class MovedRenamedDetector
{
    /// <summary>
    /// Detects whether a file was moved or renamed from another file.
    /// </summary>
    /// <param name="filePath">The current file path.</param>
    /// <param name="otherPath">The other file path to compare against.</param>
    /// <returns>A detection result.</returns>
    public static MovedRenamedResult Detect(string filePath, string otherPath)
    {
        if (!File.Exists(filePath) || !File.Exists(otherPath))
        {
            return new MovedRenamedResult();
        }

        string currentName = Path.GetFileName(filePath);
        string otherName = Path.GetFileName(otherPath);

        // Check if content is identical.
        string hashA = HashService.ComputeSha256(filePath);
        string hashB = HashService.ComputeSha256(otherPath);

        if (hashA != hashB)
        {
            return new MovedRenamedResult();
        }

        // Same content — check path relationship.
        string currentDir = Path.GetDirectoryName(filePath) ?? "";
        string otherDir = Path.GetDirectoryName(otherPath) ?? "";

        bool sameName = currentName.Equals(otherName, StringComparison.OrdinalIgnoreCase);
        bool sameDir = currentDir.Equals(otherDir, StringComparison.OrdinalIgnoreCase);

        if (sameName && !sameDir)
        {
            return new MovedRenamedResult
            {
                IsMoved = true,
                Confidence = 1.0,
            };
        }

        if (!sameName && !sameDir)
        {
            return new MovedRenamedResult
            {
                IsRenamed = true,
                Confidence = 1.0,
            };
        }

        return new MovedRenamedResult();
    }
}