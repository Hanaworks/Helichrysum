namespace Helichrysum.Core.Analysis;

using System.Text;

/// <summary>
/// Applies the F-Resolve decision model to relation groups.
/// Determines Equality / Compatibility / Conflict for file and directory pairs.
/// </summary>
public static class ResolutionResolver
{
    /// <summary>
    /// Resolves the processing intent for a pair of files.
    /// </summary>
    /// <param name="filePathA">Path to the first file (assumed older).</param>
    /// <param name="filePathB">Path to the second file (assumed newer).</param>
    /// <returns>The resolution result.</returns>
    public static ResolutionResult ResolveFilePair(string filePathA, string filePathB)
    {
        if (!File.Exists(filePathA) || !File.Exists(filePathB))
        {
            return Unknown("文件不存在");
        }

        string hashA = Hashing.HashService.ComputeSha256(filePathA);
        string hashB = Hashing.HashService.ComputeSha256(filePathB);

        // Equality: identical content.
        if (hashA == hashB)
        {
            return new ResolutionResult
            {
                Kind = ResolutionKind.Equality,
                Confidence = 1.0,
                Evidence = "HashMatch:SHA256",
            };
        }

        // Check size first — if sizes differ wildly, likely not compatible.
        var infoA = new FileInfo(filePathA);
        var infoB = new FileInfo(filePathB);

        if (infoA.Length == infoB.Length)
        {
            // Same size, different hash → different content, not compatible.
            return Unknown("同大小但内容不同");
        }

        // Compatibility: older content fully contained in newer (only for text files).
        if (IsTextFile(filePathA) && IsTextFile(filePathB))
        {
            string contentA = File.ReadAllText(filePathA);
            string contentB = File.ReadAllText(filePathB);

            // Old ⊆ New → newer is a superset.
            if (contentB.Contains(contentA, StringComparison.Ordinal) && contentA.Length < contentB.Length)
            {
                return new ResolutionResult
                {
                    Kind = ResolutionKind.Compatibility,
                    Confidence = 1.0,
                    Evidence = "ContentContainment:old_contained_in_new",
                };
            }

            // New ⊆ Old → direction is reversed, not compatible as defined.
            if (contentA.Contains(contentB, StringComparison.Ordinal) && contentB.Length < contentA.Length)
            {
                return Unknown("方向相反：旧版包含新版");
            }
        }

        // Contents differ and neither contains the other → conflict.
        return new ResolutionResult
        {
            Kind = ResolutionKind.Conflict,
            Confidence = 0.7,
            Evidence = "ContentDiverged",
        };
    }

    /// <summary>
    /// Resolves the processing intent for a pair of directories.
    /// Directory compatibility: all files in the old directory exist in the new directory.
    /// </summary>
    /// <param name="dirPathA">Path to the first directory (assumed older).</param>
    /// <param name="dirPathB">Path to the second directory (assumed newer).</param>
    /// <returns>The resolution result.</returns>
    public static ResolutionResult ResolveDirectoryPair(string dirPathA, string dirPathB)
    {
        if (!Directory.Exists(dirPathA) || !Directory.Exists(dirPathB))
        {
            return Unknown("目录不存在");
        }

        var filesA = Directory.EnumerateFiles(dirPathA).ToDictionary(
            path => Path.GetFileName(path) ?? string.Empty,
            path => Hashing.HashService.ComputeSha256(path),
            StringComparer.OrdinalIgnoreCase);

        var filesB = Directory.EnumerateFiles(dirPathB).ToDictionary(
            path => Path.GetFileName(path) ?? string.Empty,
            path => Hashing.HashService.ComputeSha256(path),
            StringComparer.OrdinalIgnoreCase);

        int missingCount = 0;
        int changedCount = 0;

        foreach (var (name, hashA) in filesA)
        {
            if (!filesB.TryGetValue(name, out string? hashB))
            {
                missingCount++; // Old file not in new directory.
            }
            else if (hashA != hashB)
            {
                changedCount++; // Same name, different content.
            }
        }

        if (missingCount == 0 && changedCount == 0)
        {
            // All old files present and identical → new directory is a superset.
            return new ResolutionResult
            {
                Kind = ResolutionKind.Compatibility,
                Confidence = 1.0,
                Evidence = $"DirectoryContainment:all {filesA.Count} files present and identical",
            };
        }

        if (missingCount > 0)
        {
            // Old has files not in new → high-risk, requires human review.
            return new ResolutionResult
            {
                Kind = ResolutionKind.Conflict,
                Confidence = 0.9,
                Evidence = $"DirectoryMissing:{missingCount} files in old not present in new",
            };
        }

        return new ResolutionResult
        {
            Kind = ResolutionKind.Conflict,
            Confidence = 0.6,
            Evidence = $"DirectoryChanged:{changedCount} files differ between versions",
        };
    }

    private static ResolutionResult Unknown(string reason)
    {
        return new ResolutionResult
        {
            Kind = ResolutionKind.Unknown,
            Confidence = 0,
            Evidence = reason,
        };
    }

    private static bool IsTextFile(string path)
    {
        string extension = Path.GetExtension(path).ToLowerInvariant();
        string[] textExtensions =
        [
            ".txt", ".md", ".log", ".json", ".xml", ".yaml", ".yml", ".csv",
            ".cs", ".xaml", ".csproj", ".html", ".css", ".js", ".ts",
            ".ps1", ".sh", ".bat", ".ini", ".cfg", ".config", ".dockerfile",
            ".gitignore", ".editorconfig", ".sln", ".props", ".proj",
        ];

        if (textExtensions.Contains(extension))
        {
            return true;
        }

        // Fallback: try reading as UTF-8 text and check for invalid bytes.
        try
        {
            var detector = new ByteOrderMarkDetector();
            using var stream = File.OpenRead(path);
            byte[] sample = new byte[Math.Min(4096, stream.Length)];
            stream.ReadExactly(sample, 0, sample.Length);
            return detector.IsProbablyText(sample);
        }
        catch
        {
            return false;
        }
    }

    private sealed class ByteOrderMarkDetector
    {
        public bool IsProbablyText(byte[] data)
        {
            if (data.Length == 0) return true;

            // Check for UTF-8 BOM or UTF-16 BOM.
            if (data.Length >= 3 && data[0] == 0xEF && data[1] == 0xBB && data[2] == 0xBF) return true;
            if (data.Length >= 2 && data[0] == 0xFF && data[1] == 0xFE) return true;
            if (data.Length >= 2 && data[0] == 0xFE && data[1] == 0xFF) return true;

            // Null byte in first 4KB → binary.
            int nullCount = 0;
            foreach (byte b in data)
            {
                if (b == 0) nullCount++;
            }

            // If >5% null bytes → likely binary.
            return nullCount < data.Length / 20;
        }
    }
}