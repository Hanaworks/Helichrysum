namespace Helichrysum.Core.Analysis;

using System.Text;

/// <summary>
/// Detects near-duplicate text files by comparing content after normalizing
/// line endings (EOL), BOM, and trailing whitespace (F-Relation-2).
/// </summary>
public static class NearDuplicateDetector
{
    /// <summary>
    /// Normalizes text by removing a UTF BOM, unifying line endings to '\n',
    /// and trimming trailing whitespace on each line.
    /// </summary>
    public static string NormalizeText(string content)
    {
        if (string.IsNullOrEmpty(content)) return string.Empty;

        // Strip UTF-8 / UTF-16 BOM if present.
        if (content.Length >= 1 && content[0] == '\uFEFF')
        {
            content = content[1..];
        }

        // Unify line endings.
        content = content.Replace("\r\n", "\n").Replace('\r', '\n');

        // Trim trailing whitespace per line and drop trailing empty lines.
        var normalized = content
            .Split('\n')
            .Select(line => line.TrimEnd())
            .ToList();

        while (normalized.Count > 0 && normalized[^1].Length == 0)
        {
            normalized.RemoveAt(normalized.Count - 1);
        }

        return string.Join('\n', normalized);
    }

    /// <summary>
    /// Returns true if the two text contents are near-duplicates
    /// (identical after normalization).
    /// </summary>
    public static bool AreNearDuplicates(string contentA, string contentB)
    {
        return NormalizeText(contentA) == NormalizeText(contentB);
    }

    /// <summary>
    /// Compares two files as text and reports whether they are near-duplicates.
    /// Returns false for binary files (detected via null bytes).
    /// </summary>
    public static bool AreFilesNearDuplicates(string filePathA, string filePathB)
    {
        if (!File.Exists(filePathA) || !File.Exists(filePathB)) return false;

        if (IsBinary(filePathA) || IsBinary(filePathB)) return false;

        string contentA = File.ReadAllText(filePathA);
        string contentB = File.ReadAllText(filePathB);

        return AreNearDuplicates(contentA, contentB);
    }

    private static bool IsBinary(string filePath)
    {
        try
        {
            byte[] sample = new byte[Math.Min(4096, new FileInfo(filePath).Length)];
            using var stream = File.OpenRead(filePath);
            int read = stream.Read(sample, 0, sample.Length);
            return sample.AsSpan(0, read).Contains((byte)0);
        }
        catch
        {
            return true;
        }
    }
}