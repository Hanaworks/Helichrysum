namespace Helichrysum.Core.Scope;

using System.IO;
using System.Collections.Generic;
using System.Text.RegularExpressions;

/// <summary>
/// Defines the scanning scope: a set of root paths with optional exclude patterns.
/// All file system operations within Helichrysum are scoped-aware.
/// </summary>
public sealed class ScopeConfiguration
{
    private readonly List<string> _rootPaths = new();
    private readonly List<string> _canonicalRoots = new();
    private readonly List<string> _excludePatterns = new();

    /// <summary>
    /// Gets the list of original root paths added by the user.
    /// </summary>
    public IReadOnlyList<string> RootPaths => _rootPaths.AsReadOnly();

    /// <summary>
    /// Gets the list of resolved canonical root paths.
    /// </summary>
    public IReadOnlyList<string> CanonicalRoots => _canonicalRoots.AsReadOnly();

    /// <summary>
    /// Gets the list of glob exclude patterns.
    /// </summary>
    public IReadOnlyList<string> ExcludePatterns => _excludePatterns.AsReadOnly();

    /// <summary>
    /// Adds a root path to the scope. Stores both the original and canonical form.
    /// </summary>
    /// <param name="path">The root path to add.</param>
    public void AddRoot(string path)
    {
        string canonical = CanonicalizePath(path);
        _rootPaths.Add(path);
        _canonicalRoots.Add(canonical);
    }

    /// <summary>
    /// Adds a glob exclude pattern. Uses simple wildcard matching (* and ?).
    /// </summary>
    /// <param name="pattern">The glob pattern to exclude.</param>
    public void AddExclude(string pattern)
    {
        _excludePatterns.Add(pattern);
    }

    /// <summary>
    /// Returns true if the given canonical path is within any scope root.
    /// </summary>
    /// <param name="canonicalPath">The canonical path to check.</param>
    public bool Contains(string canonicalPath)
    {
        foreach (string root in _canonicalRoots)
        {
            if (canonicalPath.StartsWith(root, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Returns true if the given relative path or filename matches any exclude pattern.
    /// Supports basic wildcard patterns (*, ?).
    /// </summary>
    /// <param name="relativePath">The relative path or filename to check.</param>
    public bool IsExcluded(string relativePath)
    {
        string fileName = Path.GetFileName(relativePath);

        foreach (string pattern in _excludePatterns)
        {
            if (MatchesGlob(fileName, pattern) || MatchesGlob(relativePath, pattern))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Resolves the given path to its canonical absolute form.
    /// </summary>
    /// <param name="path">The path to resolve.</param>
    /// <returns>The canonical absolute path.</returns>
    public string CanonicalizePath(string path)
    {
        return Path.GetFullPath(path);
    }

    private static bool MatchesGlob(string input, string pattern)
    {
        string regexPattern = "^" + Regex.Escape(pattern)
                                         .Replace("\\*", ".*")
                                         .Replace("\\?", ".")
                                         + "$";

        return Regex.IsMatch(input, regexPattern, RegexOptions.IgnoreCase);
    }
}