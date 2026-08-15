namespace Helichrysum.Filesystem;

/// <summary>
/// Abstraction over platform-specific filesystem operations.
/// Implementations provide link inspection, canonical path resolution,
/// and file identity retrieval for each target platform.
/// </summary>
public interface IFilesystemService
{
    /// <summary>
    /// Gets the canonical (resolved) absolute path for the given path,
    /// following all symbolic links, junctions, and reparse points.
    /// </summary>
    string GetCanonicalPath(string path);

    /// <summary>
    /// Returns a unique file identifier (e.g., inode on Linux/macOS,
    /// file index on NTFS) that can be used to detect hard links.
    /// </summary>
    ulong GetFileId(string path);

    /// <summary>
    /// Returns the device / volume identifier where the path resides.
    /// </summary>
    ulong GetDeviceId(string path);
}