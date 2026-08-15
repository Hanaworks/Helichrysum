namespace Helichrysum.Filesystem;

using System.IO;

/// <summary>
/// Cross-platform link inspector using .NET built-in APIs.
/// Uses <see cref="File.GetLinkTarget"/> for symlink detection,
/// and <see cref="FileInfo"/> for hardlink inode detection.
/// </summary>
public sealed class PlatformLinkInspector : ILinkInspector
{
    /// <summary>
    /// Inspects the given path and returns link information.
    /// </summary>
    public LinkInfo Inspect(string path)
    {
        var fileInfo = new FileInfo(path);

        if (!fileInfo.Exists && !Directory.Exists(path))
        {
            // Check if it's a broken symlink (the link itself exists but target doesn't).
            try
            {
                var linkTarget = File.ResolveLinkTarget(path, false);
                if (linkTarget != null)
                {
                    string? resolved = linkTarget.FullName;
                    return new LinkInfo
                    {
                        IsLink = true,
                        Kind = LinkKind.Symlink,
                        Target = resolved,
                        ResolvedTarget = resolved,
                        InodeGroup = null,
                    };
                }
            }
            catch
            {
                // Not a symlink, truly missing.
            }

            return new LinkInfo
            {
                IsLink = false,
                Kind = LinkKind.None,
                Target = null,
                ResolvedTarget = null,
                InodeGroup = null,
            };
        }

        // Check for symlink via attributes.
        bool isReparsePoint = fileInfo.Attributes.HasFlag(FileAttributes.ReparsePoint);

        if (isReparsePoint)
        {
            // Get the raw link target before resolving.
            string? rawTarget = fileInfo.LinkTarget;

            // Resolve the link target to get the canonical path.
            var resolvedLink = File.ResolveLinkTarget(path, false);
            string? resolved = resolvedLink?.FullName;

            return new LinkInfo
            {
                IsLink = true,
                Kind = LinkKind.Symlink,
                Target = rawTarget,
                ResolvedTarget = resolved,
                InodeGroup = null,
            };
        }

        // Get inode for hardlink detection (on Linux/macOS, this is the inode number).
        ulong inode = 0;
        try
        {
            // Use file system info to get the inode if available.
            // On Windows, this is the file index (nFileIndexHigh + nFileIndexLow).
            // On Linux, this is the inode number.
            inode = GetFileId(fileInfo);
        }
        catch
        {
            // Fallback: use a hash of the full path as a weak inode surrogate.
            inode = (ulong)path.GetHashCode();
        }

        // Check if it's a hard link (nlink > 1 on Linux).
        bool isHardlink = false;
        try
        {
            isHardlink = GetHardlinkCount(fileInfo) > 1;
        }
        catch
        {
            // Not available on all platforms.
        }

        return new LinkInfo
        {
            IsLink = isHardlink,
            Kind = isHardlink ? LinkKind.Hardlink : LinkKind.None,
            Target = null,
            ResolvedTarget = null,
            InodeGroup = isHardlink ? inode : null,
        };
    }

    private static ulong GetFileId(FileInfo fileInfo)
    {
        // On Unix, use the inode. On Windows, use the file index.
        // .NET doesn't expose inode directly, so we use a workaround.
        // For now, return 0 which means "no inode available".
        // Platform-specific P/Invoke will be added in a future slice.
        try
        {
            // Attempt to use the file system entry's metadata.
            fileInfo.Refresh();
            return 0; // Placeholder: platform-specific P/Invoke needed.
        }
        catch
        {
            return 0;
        }
    }

    private static int GetHardlinkCount(FileInfo fileInfo)
    {
        // .NET doesn't expose hardlink count directly.
        // Platform-specific P/Invoke needed.
        return 1;
    }
}