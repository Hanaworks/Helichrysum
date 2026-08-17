namespace Helichrysum.Filesystem;

using System.IO;
using System.Runtime.InteropServices;

/// <summary>
/// Cross-platform link inspector.
/// Symlink detection uses .NET built-in APIs; hardlink inode detection
/// uses platform P/Invoke (Linux/macOS stat) with graceful fallback.
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

        // Hardlink / inode detection via stat.
        var stat = TryStat(path);

        if (stat is { } s)
        {
            // Only a regular file (S_IFREG) with nlink > 1 is a hardlink.
            // Directories naturally have nlink = 2 + subdirectories — not a hardlink.
            const uint sIfReg = 0x8000;    // POSIX S_IFREG
            const uint sIfmt = 0xF000;     // POSIX S_IFMT
            bool isRegularFile = (s.Mode & sIfmt) == sIfReg;
            bool isHardlink = isRegularFile && s.Nlink > 1;

            return new LinkInfo
            {
                IsLink = isHardlink,
                Kind = isHardlink ? LinkKind.Hardlink : LinkKind.None,
                Target = null,
                ResolvedTarget = null,
                InodeGroup = isHardlink ? s.Inode : null,
            };
        }

        // Fallback when stat is unavailable — treat as regular non-link file.
        return new LinkInfo
        {
            IsLink = false,
            Kind = LinkKind.None,
            Target = null,
            ResolvedTarget = null,
            InodeGroup = null,
        };
    }

    private static StatData? TryStat(string path)
    {
        if (OperatingSystem.IsLinux())
        {
            var buf = new byte[Marshal.SizeOf<LinuxStat>()];
            int rc = NativeMethods.Stat(path, buf);
            if (rc != 0) return null;

            IntPtr ptr = Marshal.AllocHGlobal(buf.Length);
            try
            {
                Marshal.Copy(buf, 0, ptr, buf.Length);
                var st = Marshal.PtrToStructure<LinuxStat>(ptr);
                return new StatData { Inode = st.st_ino, Nlink = st.st_nlink, Mode = st.st_mode };
            }
            finally
            {
                Marshal.FreeHGlobal(ptr);
            }
        }

        if (OperatingSystem.IsMacOS())
        {
            var buf = new byte[Marshal.SizeOf<MacOsStat>()];
            int rc = NativeMethods.Stat(path, buf);
            if (rc != 0) return null;

            IntPtr ptr = Marshal.AllocHGlobal(buf.Length);
            try
            {
                Marshal.Copy(buf, 0, ptr, buf.Length);
                var st = Marshal.PtrToStructure<MacOsStat>(ptr);
                return new StatData { Inode = st.st_ino, Nlink = st.st_nlink, Mode = st.st_mode };
            }
            finally
            {
                Marshal.FreeHGlobal(ptr);
            }
        }

        // Windows: hardlink detection via BY_HANDLE_FILE_INFORMATION would need
        // CreateFile + GetFileInformationByHandle; leave for future work.
        return null;
    }

private sealed class StatData
{
    public ulong Inode { get; init; }
    public ulong Nlink { get; init; }
    public uint Mode { get; init; }
}

    // Linux x86_64 struct stat (offset layout from kernel headers).
    [StructLayout(LayoutKind.Sequential)]
    private struct LinuxStat
    {
        public ulong st_dev;    // +0
        public ulong st_ino;    // +8
        public ulong st_nlink;  // +16
        public uint st_mode;    // +24
        public uint st_uid;     // +28
        public uint st_gid;     // +32
        public int __pad0;      // +36
        public ulong st_rdev;   // +40
        public long st_size;    // +48
    }

    // macOS x86_64 struct stat (from <sys/stat.h>).
    [StructLayout(LayoutKind.Sequential)]
    private struct MacOsStat
    {
        public int st_dev;          // +0
        public ushort st_mode;      // +4
        public ushort st_nlink;     // +6
        public ulong st_ino;        // +8
        public uint st_uid;         // +16
        public uint st_gid;         // +20
        public int st_rdev;         // +24
        public long st_atime;       // +32
        public long st_mtime;       // +40
        public long st_ctime;       // +48
        public long st_birthtime;   // +56
    }

    private static class NativeMethods
    {
        private const string Libc = "libc";

        [DllImport(Libc, EntryPoint = "stat", SetLastError = true, CharSet = CharSet.Ansi)]
        internal static extern int Stat(string path, [Out] byte[] buf);
    }
}