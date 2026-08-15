namespace Helichrysum.Core.Scanning;

using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using Helichrysum.Core.Manifest;
using Helichrysum.Core.Scope;
using Microsoft.Extensions.Logging;

/// <summary>
/// Recursively traverses directories within a configured scope,
/// producing FilesystemObject records for each discovered entry.
/// </summary>
public sealed class Scanner
{
    private readonly ScopeConfiguration _scope;
    private readonly ILogger<Scanner> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="Scanner"/> class.
    /// </summary>
    /// <param name="scope">The scope configuration defining the scan boundaries.</param>
    /// <param name="logger">The logger for diagnostic output.</param>
    public Scanner(ScopeConfiguration scope, ILogger<Scanner> logger)
    {
        _scope = scope ?? throw new ArgumentNullException(nameof(scope));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Scans all files within the configured scope and yields FilesystemObject records.
    /// </summary>
    /// <param name="options">The scan options.</param>
    /// <param name="progress">Optional progress reporter.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>An async enumerable of FilesystemObject records.</returns>
    public async IAsyncEnumerable<FilesystemObject> ScanAsync(
        ScanOptions options,
        IProgress<ScanProgress>? progress,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        int filesScanned = 0;
        int dirsScanned = 0;
        var visitedCanonicalPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Use a stack for iterative directory traversal to avoid deep recursion.
        var directoryStack = new Stack<string>();

        foreach (string root in _scope.CanonicalRoots)
        {
            if (Directory.Exists(root))
            {
                directoryStack.Push(root);
            }
            else
            {
                _logger.LogWarning("Scope root does not exist: {Root}", root);
            }
        }

        while (directoryStack.Count > 0 && !cancellationToken.IsCancellationRequested)
        {
            string currentDirectory = directoryStack.Pop();

            // Check if the directory itself is excluded.
            if (_scope.IsExcluded(Path.GetFileName(currentDirectory)) ||
                _scope.IsExcluded(currentDirectory))
            {
                continue;
            }

            dirsScanned++;

            // Yield the directory itself as a FilesystemObject.
            var directoryInfo = new DirectoryInfo(currentDirectory);
            var directoryObject = new FilesystemObject
            {
                Id = 0,
                ScopeId = 0,
                Path = directoryInfo.FullName,
                CanonicalPath = directoryInfo.FullName,
                Kind = "Directory",
                Size = null,
                ModifiedTime = directoryInfo.LastWriteTimeUtc,
                CreatedTime = directoryInfo.CreationTimeUtc,
                InodeGroup = null,
                DeviceId = 0,
                ScopeRelation = "InScope",
                LinkTarget = null,
                ResolvedLinkTarget = null,
            };

            yield return directoryObject;

            // Enumerate entries in this directory.
            FileSystemInfo[] entries;
            try
            {
                entries = new DirectoryInfo(currentDirectory).GetFileSystemInfos();
            }
            catch (UnauthorizedAccessException)
            {
                _logger.LogWarning("Access denied to directory: {Directory}", currentDirectory);
                continue;
            }
            catch (DirectoryNotFoundException)
            {
                _logger.LogWarning("Directory not found (may have been deleted): {Directory}", currentDirectory);
                continue;
            }

            foreach (FileSystemInfo entry in entries)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    yield break;
                }

                // Check exclude pattern against the file name.
                if (_scope.IsExcluded(entry.Name))
                {
                    continue;
                }

                // Check if this is a symlink or junction.
                bool isLink = entry.Attributes.HasFlag(FileAttributes.ReparsePoint);
                string? linkTarget = null;
                string? resolvedTarget = null;

                if (isLink && entry is DirectoryInfo dirInfo)
                {
                    linkTarget = dirInfo.LinkTarget;
                }
                else if (isLink && entry is FileInfo fileInfo)
                {
                    linkTarget = fileInfo.LinkTarget;
                }

                // Resolve the link target if it's a symlink within scope.
                if (linkTarget != null)
                {
                    resolvedTarget = Path.GetFullPath(Path.Combine(currentDirectory, linkTarget));
                }

                // Skip symlinks that point outside the scope.
                if (linkTarget != null && !_scope.Contains(resolvedTarget!))
                {
                    var outOfScopeLink = new FilesystemObject
                    {
                        Id = 0,
                        ScopeId = 0,
                        Path = entry.FullName,
                        CanonicalPath = entry.FullName,
                        Kind = "Symlink",
                        Size = null,
                        ModifiedTime = entry.LastWriteTimeUtc,
                        CreatedTime = entry.CreationTimeUtc,
                        InodeGroup = null,
                        DeviceId = 0,
                        ScopeRelation = "OutOfScope",
                        LinkTarget = linkTarget,
                        ResolvedLinkTarget = resolvedTarget,
                    };

                    yield return outOfScopeLink;
                    continue;
                }

                // Skip symlinks that form cycles (already visited canonical path).
                if (resolvedTarget != null && visitedCanonicalPaths.Contains(resolvedTarget))
                {
                    var circularLink = new FilesystemObject
                    {
                        Id = 0,
                        ScopeId = 0,
                        Path = entry.FullName,
                        CanonicalPath = entry.FullName,
                        Kind = "Symlink",
                        Size = null,
                        ModifiedTime = entry.LastWriteTimeUtc,
                        CreatedTime = entry.CreationTimeUtc,
                        InodeGroup = null,
                        DeviceId = 0,
                        ScopeRelation = "Circular",
                        LinkTarget = linkTarget,
                        ResolvedLinkTarget = resolvedTarget,
                    };

                    yield return circularLink;
                    continue;
                }

                if ((entry.Attributes & FileAttributes.Directory) == FileAttributes.Directory)
                {
                    // Recurse into subdirectories.
                    if (resolvedTarget != null)
                    {
                        // Symlink directory within scope: follow it.
                        visitedCanonicalPaths.Add(resolvedTarget);
                        directoryStack.Push(entry.FullName);
                    }
                    else
                    {
                        directoryStack.Push(entry.FullName);
                    }
                }
                else
                {
                    // Regular file.
                    var fileInfo = new FileInfo(entry.FullName);
                    filesScanned++;

                    var fileObject = new FilesystemObject
                    {
                        Id = 0,
                        ScopeId = 0,
                        Path = entry.FullName,
                        CanonicalPath = entry.FullName,
                        Kind = "RegularFile",
                        Size = fileInfo.Length,
                        ModifiedTime = fileInfo.LastWriteTimeUtc,
                        CreatedTime = fileInfo.CreationTimeUtc,
                        InodeGroup = null,
                        DeviceId = 0,
                        ScopeRelation = "InScope",
                        LinkTarget = linkTarget,
                        ResolvedLinkTarget = isLink ? resolvedTarget : null,
                    };

                    yield return fileObject;

                    progress?.Report(new ScanProgress
                    {
                        FilesScanned = filesScanned,
                        DirectoriesScanned = dirsScanned,
                        CurrentPath = entry.FullName,
                    });
                }
            }
        }
    }
}