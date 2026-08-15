namespace Helichrysum.Core.Manifest;

/// <summary>
/// Represents a filesystem object discovered during scanning.
/// </summary>
public sealed record FilesystemObject
{
    public required long Id { get; init; }
    public required long ScopeId { get; init; }
    public required string Path { get; init; }
    public required string CanonicalPath { get; init; }
    public required string Kind { get; init; }
    public long? Size { get; init; }
    public DateTimeOffset? ModifiedTime { get; init; }
    public DateTimeOffset? CreatedTime { get; init; }
    public long? InodeGroup { get; init; }
    public required ulong DeviceId { get; init; }
    public required string ScopeRelation { get; init; }
    public string? LinkTarget { get; init; }
    public string? ResolvedLinkTarget { get; init; }
}