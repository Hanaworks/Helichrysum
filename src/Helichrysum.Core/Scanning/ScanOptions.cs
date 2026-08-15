namespace Helichrysum.Core.Scanning;

/// <summary>
/// Options for configuring a scan operation.
/// </summary>
public sealed class ScanOptions
{
    /// <summary>
    /// Gets or sets the maximum degree of parallelism for file system traversal.
    /// Defaults to the number of logical processors.
    /// </summary>
    public int MaxDegreeOfParallelism { get; set; } = Environment.ProcessorCount;
}