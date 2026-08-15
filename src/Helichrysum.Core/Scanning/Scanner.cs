namespace Helichrysum.Core.Scanning;

using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using Helichrysum.Core.Links;
using Helichrysum.Core.Manifest;
using Helichrysum.Core.Scope;
using Helichrysum.Filesystem;
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
        var linkInspector = new PlatformLinkInspector();
        var linkResolver = new LinkResolver(_scope, linkInspector, visitedCanonicalPaths,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<LinkResolver>.Instance);

        // Use a stack for iterative directory traversal to avoid deep recursion.
        var directoryStack = new Stack<string>();

        foreach (string root in _scope.CanonicalRoots)
        {
            if (Directory.Exists(root))
            {
                directoryStack.Push(root);
                visitedCanonicalPaths.Add(root);
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

                // Use LinkResolver to detect and classify links.
                var linkResult = linkResolver.Resolve(entry.FullName);

                if (linkResult.IsLink)
                {
                    // Handle based on the link's scope relation.
                    yield return new FilesystemObject
                    {
                        Id = 0,
                        ScopeId = 0,
                        Path = entry.FullName,
                        CanonicalPath = entry.FullName,
                        Kind = "Symlink",
                        Size = null,
                        ModifiedTime = entry.LastWriteTimeUtc,
                        CreatedTime = entry.CreationTimeUtc,
                        InodeGroup = (long?)linkResult.InodeGroup,
                        DeviceId = 0,
                        ScopeRelation = linkResult.ScopeRelation,
                        LinkTarget = linkResult.LinkTarget,
                        ResolvedLinkTarget = linkResult.ResolvedLinkTarget,
                    };

                    // For in-scope directory symlinks, push to the traversal stack.
                    if (linkResult.ScopeRelation == "InScope"
                        && linkResult.ResolvedLinkTarget != null
                        && Directory.Exists(linkResult.ResolvedLinkTarget))
                    {
                        visitedCanonicalPaths.Add(linkResult.ResolvedLinkTarget);
                        directoryStack.Push(entry.FullName);
                    }

                    continue;
                }

                // Not a link: handle as regular file or directory.
                if ((entry.Attributes & FileAttributes.Directory) == FileAttributes.Directory)
                {
                    directoryStack.Push(entry.FullName);
                }
                else
                {
                    // Regular file.
                    var fileInfo = new FileInfo(entry.FullName);
                    filesScanned++;

                    yield return new FilesystemObject
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
                        LinkTarget = null,
                        ResolvedLinkTarget = null,
                    };

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