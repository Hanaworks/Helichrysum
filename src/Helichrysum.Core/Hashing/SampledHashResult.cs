namespace Helichrysum.Core.Hashing;

/// <summary>
/// Result of a sampled hash computation.
/// </summary>
public sealed record SampledHashResult
{
    public required string HashValue { get; init; }
    public required long BytesRead { get; init; }
}