namespace Helichrysum.Core.Analysis;

/// <summary>
/// A single node in the processing chain (F-Resolve-15).
/// Records which layer produced a decision and what it depended on.
/// </summary>
public sealed record ProcessingChainNode
{
    /// <summary>Which layer produced the decision (File / Directory / Structural).</summary>
    public required string Layer { get; init; }

    /// <summary>The subject (file path or directory path).</summary>
    public required string Subject { get; init; }

    /// <summary>The resolution applied (Equality / Compatibility / Conflict).</summary>
    public required string Resolution { get; init; }

    /// <summary>How many lower-layer resolutions this decision consumed.</summary>
    public required int BasedOnCount { get; init; }
}

/// <summary>
/// Models the processing chain and its dependencies, enabling the report
/// to visualise "directory-level decision ← resolved-from N file-level
/// decisions" (F-Resolve-15).
/// </summary>
public sealed class DependencyChainBuilder
{
    private readonly List<ProcessingChainNode> _nodes = [];
    private readonly Dictionary<string, int> _resolvedBySubject = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Records a file-level (or other leaf) resolution that is now settled.
    /// </summary>
    public void RecordLeafResolution(string subject, string resolution)
    {
        _resolvedBySubject[subject] = _resolvedBySubject.GetValueOrDefault(subject) + 1;
        _nodes.Add(new ProcessingChainNode
        {
            Layer = "File",
            Subject = subject,
            Resolution = resolution,
            BasedOnCount = 0,
        });
    }

    /// <summary>
    /// Records a directory-level (or higher) decision whose verdict depended on
    /// previously recorded lower-layer resolutions for the member subjects.
    /// </summary>
    public void RecordCompositeDecision(
        string layer,
        string subject,
        string resolution,
        IReadOnlyCollection<string> dependentSubjects)
    {
        int basedOn = dependentSubjects.Sum(s => _resolvedBySubject.GetValueOrDefault(s));
        _nodes.Add(new ProcessingChainNode
        {
            Layer = layer,
            Subject = subject,
            Resolution = resolution,
            BasedOnCount = basedOn,
        });
    }

    /// <summary>All recorded nodes, in insertion order (bottom-up).</summary>
    public IReadOnlyList<ProcessingChainNode> Nodes => _nodes.AsReadOnly();

    /// <summary>
    /// Serialises the chain as JSON for embedding in reports (F-Resolve-15).
    /// </summary>
    public string ToJson()
    {
        return Serialization.JsonService.SerializeIndented(_nodes);
    }
}