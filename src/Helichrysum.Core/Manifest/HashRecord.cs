namespace Helichrysum.Core.Manifest;

/// <summary>
/// Represents a hash record for a filesystem object.
/// </summary>
public sealed record HashRecord
{
    public required long ObjectId { get; init; }
    public required string Tier { get; init; }
    public string? HashValue { get; init; }
    public required long BytesRead { get; init; }
    public required DateTimeOffset ComputedAt { get; init; }
}