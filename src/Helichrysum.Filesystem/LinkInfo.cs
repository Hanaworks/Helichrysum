namespace Helichrysum.Filesystem;

/// <summary>
/// Information about a filesystem link, returned by <see cref="ILinkInspector.Inspect"/>.
/// </summary>
public sealed record LinkInfo
{
    /// <summary>Whether this path is a link of any kind.</summary>
    public required bool IsLink { get; init; }

    /// <summary>The specific kind of link.</summary>
    public required LinkKind Kind { get; init; }

    /// <summary>The raw link target path (as stored in the link).</summary>
    public string? Target { get; init; }

    /// <summary>The resolved canonical target path (after following symlinks).</summary>
    public string? ResolvedTarget { get; init; }

    /// <summary>The inode / file identifier for hardlink detection.</summary>
    public ulong? InodeGroup { get; init; }
}