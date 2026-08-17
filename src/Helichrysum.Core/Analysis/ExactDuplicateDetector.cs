namespace Helichrysum.Core.Analysis;

using System.Collections.Generic;
using Newtonsoft.Json;
using Helichrysum.Core.Manifest;

/// <summary>
/// Detects exact duplicate files by comparing their SHA256 hashes.
/// Files with the same size are grouped, then hash-matched into duplicate groups.
/// </summary>
public sealed class ExactDuplicateDetector
{
    private readonly ManifestRepository _repository;

    /// <summary>
    /// Initializes a new instance of the <see cref="ExactDuplicateDetector"/> class.
    /// </summary>
    /// <param name="repository">The manifest repository to query.</param>
    public ExactDuplicateDetector(ManifestRepository repository)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    /// <summary>
    /// Runs the duplicate detection algorithm and returns the detected relation groups.
    /// </summary>
    /// <returns>A list of detected duplicate relation groups.</returns>
    public List<DetectedRelation> Detect()
    {
        var results = new List<DetectedRelation>();
        var duplicateGroups = _repository.GetDuplicateGroups();

        foreach (var group in duplicateGroups)
        {
            var evidence = new List<EvidenceEntry>
            {
                new EvidenceEntry
                {
                    Type = "HashMatch",
                    Details = "SHA256",
                },
                new EvidenceEntry
                {
                    Type = "SizeMatch",
                    Details = group.Size.ToString(),
                },
            };

            string evidenceJson = JsonConvert.SerializeObject(evidence);

            // Persist relation to manifest.
            var relation = new Relation
            {
                Id = 0,
                Kind = "ExactDuplicate",
                Confidence = 1.0,
                Evidence = evidenceJson,
            };

            long relationId = _repository.InsertRelation(relation, group.Members);

            results.Add(new DetectedRelation
            {
                Id = relationId,
                Kind = "ExactDuplicate",
                Confidence = 1.0,
                Evidence = evidenceJson,
                Members = group.Members,
            });
        }

        return results;
    }
}

/// <summary>
/// Represents a detected relation with full member information.
/// </summary>
public sealed class DetectedRelation
{
    public required long Id { get; init; }
    public required string Kind { get; init; }
    public required double Confidence { get; init; }
    public required string Evidence { get; init; }
    public required List<long> Members { get; init; }
}

/// <summary>
/// A single piece of evidence supporting a relation.
/// </summary>
public sealed class EvidenceEntry
{
    public required string Type { get; init; }
    public required string Details { get; init; }
}