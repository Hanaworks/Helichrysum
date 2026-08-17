namespace Helichrysum.Core.Analysis;

/// <summary>
/// The processing intent assigned to a relation group by the decision model.
/// Maps to the F-Resolve decision model (Equality / Compatibility / Conflict).
/// </summary>
public enum ResolutionKind
{
    /// <summary>Content is identical — safe to auto-deduplicate.</summary>
    Equality,

    /// <summary>Newer version fully contains older content — auto keep-newer.</summary>
    Compatibility,

    /// <summary>Contents differ and neither contains the other — requires human review.</summary>
    Conflict,

    /// <summary>Not enough evidence to determine — requires human review.</summary>
    Unknown,
}

/// <summary>
/// The result of applying the decision model to a relation group.
/// </summary>
public sealed record ResolutionResult
{
    /// <summary>The processing intent.</summary>
    public required ResolutionKind Kind { get; init; }

    /// <summary>Confidence in the resolution (0.0 to 1.0).</summary>
    public required double Confidence { get; init; }

    /// <summary>Human-readable explanation of the judgement.</summary>
    public required string Evidence { get; init; }

    /// <summary>Serializes the result to a string for manifest storage.</summary>
    public string ToStorageString()
    {
        return Newtonsoft.Json.JsonConvert.SerializeObject(this);
    }

    /// <summary>Deserializes from a storage string.</summary>
    public static ResolutionResult FromStorageString(string json)
    {
        return Newtonsoft.Json.JsonConvert.DeserializeObject<ResolutionResult>(json)
            ?? new ResolutionResult { Kind = ResolutionKind.Unknown, Confidence = 0, Evidence = "deserialization failed" };
    }
}