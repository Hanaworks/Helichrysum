namespace Helichrysum.Filesystem;

using System.Text.RegularExpressions;

/// <summary>
/// Detects mount points (F-Link-4) so the scanner can avoid crossing device
/// boundaries unless explicitly allowed. Reads /proc/self/mountinfo on Linux.
/// </summary>
public static class MountPointDetector
{
    private static readonly Regex MountInfoLine = new(
        @"^\d+\s+\d+\s+\d+:\d+\s+\S*\s+(?<mountPoint>\S+)\s+",
        RegexOptions.Compiled);

    private static HashSet<string>? _cachedMountPoints;

    /// <summary>
    /// Returns the set of mount point paths currently mounted on this system
    /// (Linux). On other platforms, returns an empty set (no boundary detection).
    /// </summary>
    public static IReadOnlySet<string> GetMountPoints()
    {
        if (_cachedMountPoints is not null)
        {
            return _cachedMountPoints;
        }

        var mountPoints = new HashSet<string>(StringComparer.Ordinal);

        if (OperatingSystem.IsLinux())
        {
            try
            {
                foreach (string line in File.ReadLines("/proc/self/mountinfo"))
                {
                    var match = MountInfoLine.Match(line);
                    if (match.Success)
                    {
                        string mp = Uri.UnescapeDataString(match.Groups["mountPoint"].Value);
                        mountPoints.Add(mp);
                    }
                }
            }
            catch
            {
                // mountinfo unavailable — treat as no mount points.
            }
        }

        _cachedMountPoints = mountPoints;
        return mountPoints;
    }

    /// <summary>
    /// Determines whether the given path is a mount point root.
    /// </summary>
    public static bool IsMountPoint(string canonicalPath)
    {
        return GetMountPoints().Contains(canonicalPath);
    }

    /// <summary>
    /// Determines whether crossing from parent to child would cross a device boundary.
    /// A child path is "on another device" when it is a mount point OR setuid-based mount
    /// detection (st_dev differs) is unavailable. Returns the mount point's root if a
    /// boundary was crossed, otherwise null.
    /// </summary>
    public static string? GetCrossedBoundary(string parentPath, string childPath)
    {
        if (!OperatingSystem.IsLinux()) return null;

        // If the child itself is a mount point, crossing into it crosses devices.
        if (IsMountPoint(childPath))
        {
            return childPath;
        }

        // A mount point added after parent scan would have this prefix match fail;
        // also guard against boundary under the parent.
        foreach (string mp in GetMountPoints())
        {
            if (mp != parentPath && mp.StartsWith(childPath, StringComparison.Ordinal))
            {
                return mp;
            }
        }

        return null;
    }
}