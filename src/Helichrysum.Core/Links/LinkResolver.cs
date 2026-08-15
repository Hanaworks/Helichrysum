namespace Helichrysum.Core.Links;

using Helichrysum.Core.Manifest;
using Helichrysum.Core.Scope;
using Helichrysum.Filesystem;
using Microsoft.Extensions.Logging;

/// <summary>
/// Resolves link information into scope-aware classifications.
/// Determines whether a link points within scope, outside scope,
/// is broken, or is circular.
/// </summary>
public sealed class LinkResolver
{
    private readonly ScopeConfiguration _scope;
    private readonly ILinkInspector _linkInspector;
    private readonly HashSet<string> _visitedCanonicalPaths;
    private readonly ILogger<LinkResolver> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="LinkResolver"/> class.
    /// </summary>
    public LinkResolver(
        ScopeConfiguration scope,
        ILinkInspector linkInspector,
        HashSet<string> visitedCanonicalPaths,
        ILogger<LinkResolver> logger)
    {
        _scope = scope ?? throw new ArgumentNullException(nameof(scope));
        _linkInspector = linkInspector ?? throw new ArgumentNullException(nameof(linkInspector));
        _visitedCanonicalPaths = visitedCanonicalPaths ?? throw new ArgumentNullException(nameof(visitedCanonicalPaths));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Resolves a path and returns a classified link result.
    /// </summary>
    /// <param name="path">The absolute path to resolve.</param>
    /// <returns>A resolved link result with scope classification.</returns>
    public LinkResolutionResult Resolve(string path)
    {
        var linkInfo = _linkInspector.Inspect(path);

        if (!linkInfo.IsLink)
        {
            return new LinkResolutionResult
            {
                IsLink = false,
                Kind = LinkKind.None,
                ScopeRelation = "InScope",
                LinkTarget = null,
                ResolvedLinkTarget = null,
            };
        }

        // Check for hardlinks (same inode, multiple paths).
        if (linkInfo.Kind == LinkKind.Hardlink)
        {
            return new LinkResolutionResult
            {
                IsLink = true,
                Kind = LinkKind.Hardlink,
                ScopeRelation = "InScope",
                LinkTarget = null,
                ResolvedLinkTarget = null,
                InodeGroup = linkInfo.InodeGroup,
            };
        }

        // Symlink processing.
        string? resolvedTarget = linkInfo.ResolvedTarget;

        // Check if the target exists.
        if (resolvedTarget == null || (!File.Exists(resolvedTarget) && !Directory.Exists(resolvedTarget)))
        {
            // Broken link: target doesn't exist.
            // But first check if it's a broken symlink (the link file itself exists).
            if (File.Exists(path) || Directory.Exists(path))
            {
                _logger.LogWarning("Broken symlink: {Path} → {Target}", path, linkInfo.Target);
                return new LinkResolutionResult
                {
                    IsLink = true,
                    Kind = LinkKind.Symlink,
                    ScopeRelation = "Broken",
                    LinkTarget = linkInfo.Target,
                    ResolvedLinkTarget = null,
                };
            }

            // The link itself doesn't exist either.
            return new LinkResolutionResult
            {
                IsLink = false,
                Kind = LinkKind.None,
                ScopeRelation = "InScope",
                LinkTarget = null,
                ResolvedLinkTarget = null,
            };
        }

        // Check if the resolved target is within scope.
        if (!_scope.Contains(resolvedTarget))
        {
            return new LinkResolutionResult
            {
                IsLink = true,
                Kind = LinkKind.Symlink,
                ScopeRelation = "OutOfScope",
                LinkTarget = linkInfo.Target,
                ResolvedLinkTarget = resolvedTarget,
            };
        }

        // Check for circular links (already visited canonical path).
        string canonicalTarget = resolvedTarget;
        if (_visitedCanonicalPaths.Contains(canonicalTarget))
        {
            _logger.LogWarning("Circular symlink detected: {Path} → {Target} (already visited)", path, resolvedTarget);
            return new LinkResolutionResult
            {
                IsLink = true,
                Kind = LinkKind.Symlink,
                ScopeRelation = "Circular",
                LinkTarget = linkInfo.Target,
                ResolvedLinkTarget = resolvedTarget,
            };
        }

        // Link is within scope and not circular.
        _visitedCanonicalPaths.Add(canonicalTarget);

        return new LinkResolutionResult
        {
            IsLink = true,
            Kind = LinkKind.Symlink,
            ScopeRelation = "InScope",
            LinkTarget = linkInfo.Target,
            ResolvedLinkTarget = resolvedTarget,
        };
    }
}

/// <summary>
/// Result of a link resolution operation, including scope classification.
/// </summary>
public sealed record LinkResolutionResult
{
    public required bool IsLink { get; init; }
    public required LinkKind Kind { get; init; }
    public required string ScopeRelation { get; init; }
    public string? LinkTarget { get; init; }
    public string? ResolvedLinkTarget { get; init; }
    public ulong? InodeGroup { get; init; }
}