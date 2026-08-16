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

        // Get archive entry names.
        var archiveEntries = GetArchiveEntryNames(archivePath);

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

        if (intersection == archiveEntries.Count && intersection == dirFiles.Count)
        {
            status = "FullyExtracted";
        }
        else if (dirFiles.Count > archiveEntries.Count)
        {
            status = "ModifiedAfterExtraction";
        }
        else
        {
            status = "PartialExtraction";
        }

        return new ArchivePairResult
        {
            Status = status,
            ArchivePath = archivePath,
            DirectoryPath = siblingDir,
            ArchiveFileCount = archiveEntries.Count,
            DirectoryFileCount = dirFiles.Count,
        };
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