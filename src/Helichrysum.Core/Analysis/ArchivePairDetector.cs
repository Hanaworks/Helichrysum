namespace Helichrysum.Core.Analysis;

using System.IO;
using System.IO.Compression;
using SharpCompress.Common;
using SharpCompress.Readers;

/// <summary>
/// Result of an archive pair detection.
/// </summary>
public sealed record ArchivePairResult
{
    public required string Status { get; init; }
    public required string ArchivePath { get; init; }
    public required string DirectoryPath { get; init; }
    public int ArchiveFileCount { get; init; }
    public int DirectoryFileCount { get; init; }

    /// <summary>Latest entry timestamp inside the archive (the "extraction anchor", F-Resolve-16).</summary>
    public DateTimeOffset? AnchorTimestamp { get; init; }

    /// <summary>True if the extracted directory's files are newer than the anchor (modified after extraction).</summary>
    public bool? ModifiedAfterExtraction { get; init; }
}

/// <summary>
/// Information about an archive's internal entries (F-Resolve-16 anchor support).
/// </summary>
public sealed record ArchiveAnchorInfo
{
    /// <summary>Latest modification timestamp among the archive's entries.</summary>
    public DateTimeOffset? LatestEntryTimestamp { get; init; }

    /// <summary>Number of readable entries.</summary>
    public int EntryCount { get; init; }
}

/// <summary>
/// Detects relationships between compressed archives and their extracted sibling directories.
/// </summary>
public static class ArchivePairDetector
{
    /// <summary>
    /// Detects whether an archive has a matching extracted directory sibling.
    /// </summary>
    /// <param name="archivePath">Path to the archive file.</param>
    /// <param name="parentDirectory">The directory containing the archive (to search for siblings).</param>
    /// <returns>An ArchivePairResult if a match is found, null otherwise.</returns>
    public static ArchivePairResult? Detect(string archivePath, string parentDirectory)
    {
        if (!File.Exists(archivePath) || !Directory.Exists(parentDirectory))
        {
            return null;
        }

        // Extract the archive name without extension.
        string archiveName = Path.GetFileNameWithoutExtension(archivePath);

        // Find candidate sibling directories with the same name.
        string? siblingDir = FindSiblingDirectory(archiveName, parentDirectory);

        if (siblingDir == null || !Directory.Exists(siblingDir))
        {
            return null;
        }

        // Get archive entry names + anchor info (F-Resolve-16).
        var archiveEntries = GetArchiveEntryNames(archivePath);
        var anchorInfo = GetArchiveAnchorInfo(archivePath);

        if (archiveEntries == null || archiveEntries.Count == 0)
        {
            return null;
        }

        // Get directory file names.
        var dirFiles = GetDirectoryFileNames(siblingDir);

        // Compare.
        int intersection = archiveEntries.Intersect(dirFiles).Count();
        int union = archiveEntries.Union(dirFiles).Count();

        if (union == 0)
        {
            return null;
        }

        double similarity = (double)intersection / union;

        if (similarity < 0.5)
        {
            // Not similar enough to be considered a pair.
            return null;
        }

        string status;
        bool? modifiedAfterExtraction = null;

        if (intersection == archiveEntries.Count && intersection == dirFiles.Count)
        {
            status = "FullyExtracted";
        }
        else if (dirFiles.Count > archiveEntries.Count)
        {
            status = "ModifiedAfterExtraction";
            modifiedAfterExtraction = true;
        }
        else
        {
            status = "PartialExtraction";
        }

        // F-Resolve-16: Use the archive's latest entry timestamp as an "extraction anchor".
        // If the sibling directory's newest file is much newer than the anchor, the
        // directory was modified after extraction → upgrade FullyExtracted to
        // ModifiedAfterExtraction (stronger evidence). Otherwise keep the spec status
        // and just record the anchor + flag.
        if (anchorInfo.LatestEntryTimestamp is { } anchor)
        {
            var newestDirFile = GetNewestDirectoryFileTimestamp(siblingDir);
            if (newestDirFile is { } newest && status != "PartialExtraction")
            {
                bool modified = newest > anchor.AddMinutes(TimeTrustEvaluator.DefaultClusterWindowMinutes);
                modifiedAfterExtraction = modified;

                if (status == "FullyExtracted" && modified)
                {
                    status = "ModifiedAfterExtraction";
                }
            }
        }

        return new ArchivePairResult
        {
            Status = status,
            ArchivePath = archivePath,
            DirectoryPath = siblingDir,
            ArchiveFileCount = archiveEntries.Count,
            DirectoryFileCount = dirFiles.Count,
            AnchorTimestamp = anchorInfo.LatestEntryTimestamp,
            ModifiedAfterExtraction = modifiedAfterExtraction,
        };
    }

    /// <summary>
    /// Extracts the latest entry timestamp inside the archive — the "extraction
    /// anchor" (F-Resolve-16): a trusted content timestamp that is not affected
    /// by filesystem copy operations on the extracted directory.
    /// </summary>
    public static ArchiveAnchorInfo GetArchiveAnchorInfo(string archivePath)
    {
        try
        {
            string extension = Path.GetExtension(archivePath).ToLowerInvariant();
            DateTimeOffset? latest = null;
            int count = 0;

            if (extension is ".zip" or ".jar")
            {
                using var archive = ZipFile.OpenRead(archivePath);
                foreach (var entry in archive.Entries)
                {
                    // ZipArchiveEntry.LastWriteTime is already a DateTimeOffset in .NET Core.
                    var t = entry.LastWriteTime;
                    if (latest is null || t > latest) latest = t;
                    count++;
                }
            }
            else if (extension is ".tar" or ".tar.gz" or ".tgz" or ".7z" or ".rar")
            {
                using var stream = File.OpenRead(archivePath);
                using var reader = ReaderFactory.OpenReader(stream);
                while (reader.MoveToNextEntry())
                {
                    if (reader.Entry.LastModifiedTime is { } t)
                    {
                        if (latest is null || t > latest) latest = t;
                    }
                    count++;
                }
            }

            return new ArchiveAnchorInfo { LatestEntryTimestamp = latest, EntryCount = count };
        }
        catch
        {
            return new ArchiveAnchorInfo { LatestEntryTimestamp = null, EntryCount = 0 };
        }
    }

    private static DateTimeOffset? GetNewestDirectoryFileTimestamp(string directoryPath)
    {
        try
        {
            DateTimeOffset? newest = null;
            foreach (string file in Directory.EnumerateFiles(directoryPath))
            {
                var t = File.GetLastWriteTimeUtc(file);
                if (newest is null || t > newest) newest = t;
            }
            return newest;
        }
        catch
        {
            return null;
        }
    }

    private static string? FindSiblingDirectory(string archiveName, string parentDirectory)
    {
        // Look for a directory with the same name as the archive.
        string candidate = Path.Combine(parentDirectory, archiveName);

        if (Directory.Exists(candidate))
        {
            return candidate;
        }

        // Try common suffixes.
        string[] suffixes = ["-1", "_extracted", "_extracted(1)", ""];

        foreach (string suffix in suffixes)
        {
            candidate = Path.Combine(parentDirectory, archiveName + suffix);
            if (Directory.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    private static HashSet<string>? GetArchiveEntryNames(string archivePath)
    {
        try
        {
            string extension = Path.GetExtension(archivePath).ToLowerInvariant();

            if (extension is ".zip" or ".jar")
            {
                using var archive = ZipFile.OpenRead(archivePath);
                return archive.Entries
                    .Select(e => e.FullName.TrimEnd('/'))
                    .Where(n => !string.IsNullOrEmpty(n))
                    .Select(n => Path.GetFileName(n))
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);
            }

            // For tar/7z/rar, use SharpCompress.
            if (extension is ".tar" or ".tar.gz" or ".tgz" or ".7z" or ".rar")
            {
                using var stream = File.OpenRead(archivePath);
                using var reader = ReaderFactory.OpenReader(stream);
                var entries = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                while (reader.MoveToNextEntry())
                {
                    if (!reader.Entry.IsDirectory)
                    {
                        string? name = Path.GetFileName(reader.Entry.Key);
                        if (name != null)
                        {
                            entries.Add(name);
                        }
                    }
                }
                return entries;
            }

            return [];
        }
        catch
        {
            return null;
        }
    }

    private static HashSet<string> GetDirectoryFileNames(string directoryPath)
    {
        try
        {
            return Directory.EnumerateFiles(directoryPath)
                .Select(Path.GetFileName)
                .Where(n => n != null)
                .ToHashSet(StringComparer.OrdinalIgnoreCase)!;
        }
        catch
        {
            return [];
        }
    }
}