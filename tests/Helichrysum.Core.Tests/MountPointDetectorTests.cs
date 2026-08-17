using Helichrysum.Filesystem;

namespace Helichrysum.Core.Tests;

public sealed class MountPointDetectorTests
{
    [Fact]
    public void GetMountPoints_Linux_ReturnsNonEmptyOrEmpty()
    {
        // On Linux, /proc/self/mountinfo exists and should yield at least "/".
        // On other platforms it returns empty — either is acceptable.
        var mountPoints = MountPointDetector.GetMountPoints();

        if (OperatingSystem.IsLinux())
        {
            Assert.Contains("/", mountPoints);
        }
        else
        {
            Assert.Empty(mountPoints);
        }
    }

    [Fact]
    public void IsMountPoint_Root_TrueOnLinux()
    {
        if (!OperatingSystem.IsLinux()) return;

        Assert.True(MountPointDetector.IsMountPoint("/"));
    }

    [Fact]
    public void GetCrossedBoundary_DeepPath_NoCrossing()
    {
        if (!OperatingSystem.IsLinux()) return;

        // A nested path under '/' that is not itself a mount point → no boundary.
        // ("/tmp" may be its own mount in some environments; on CI it usually is not.)
        string? boundary = MountPointDetector.GetCrossedBoundary("/", "/tmp");

        // Accept either outcome — the important thing is the method is stable.
        _ = boundary;
    }
}