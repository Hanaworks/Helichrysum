namespace Helichrysum.Filesystem;

/// <summary>
/// Types of filesystem links that can be detected.
/// </summary>
public enum LinkKind
{
    /// <summary>Not a link.</summary>
    None,

    /// <summary>Symbolic link (symlink).</summary>
    Symlink,

    /// <summary>Hard link (multiple paths, same inode).</summary>
    Hardlink,

    /// <summary>Windows directory junction.</summary>
    Junction,

    /// <summary>Windows reparse point (other types).</summary>
    ReparsePoint,
}