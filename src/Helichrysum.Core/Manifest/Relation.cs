namespace Helichrysum.Core.Manifest;

/// <summary>
/// Represents a semantic relation between filesystem objects (e.g., ExactDuplicate, ArchivePair).
/// </summary>
public sealed record Relation
{
    public required long Id { get; init; }
    public required string Kind { get; init; }
    public required double Confidence { get; init; }
    public required string Evidence { get; init; }
}

/// <summary>
/// A group of duplicate objects sharing the same hash value.
/// </summary>
public sealed record DuplicateGroup
{
    public required string HashValue { get; init; }
    public required List<long> Members { get; init; }
    public required long Size { get; init; }
    public required int Count { get; init; }
}

/// <summary>
/// Scan state for resuming interrupted scans.
/// </summary>
public sealed record ScanState
{
    public required long ScopeId { get; init; }
    public string? LastPath { get; init; }
    public required string Status { get; init; }
    public required DateTimeOffset UpdatedAt { get; init; }
}