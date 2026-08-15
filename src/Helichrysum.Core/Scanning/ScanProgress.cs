namespace Helichrysum.Core.Scanning;

/// <summary>
/// Reports progress of a scan operation.
/// </summary>
public sealed record ScanProgress
{
    /// <summary>
    /// Gets the total number of files scanned so far.
    /// </summary>
    public int FilesScanned { get; init; }

    /// <summary>
    /// Gets the total number of directories scanned so far.
    /// </summary>
    public int DirectoriesScanned { get; init; }

    /// <summary>
    /// Gets the current path being scanned.
    /// </summary>
    public string? CurrentPath { get; init; }
}