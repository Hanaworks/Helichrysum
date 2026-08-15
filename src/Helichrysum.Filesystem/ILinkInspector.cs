namespace Helichrysum.Filesystem;

/// <summary>
/// Inspects filesystem paths to detect links (symlinks, hardlinks, junctions, reparse points).
/// Platform-specific implementations handle the P/Invoke details.
/// </summary>
public interface ILinkInspector
{
    /// <summary>
    /// Inspects the given path and returns link information.
    /// </summary>
    /// <param name="path">The absolute filesystem path to inspect.</param>
    /// <returns>Link information for the path.</returns>
    LinkInfo Inspect(string path);
}