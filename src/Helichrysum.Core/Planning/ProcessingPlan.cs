namespace Helichrysum.Core.Planning;

using System.Text.Json;

/// <summary>
/// A planned action to be executed on a filesystem object.
/// </summary>
public sealed class PlannedAction
{
    public required string ActionType { get; init; }  // Keep, MoveToTrash, MoveTo, Rename, ReplaceWithLink
    public required long ObjectId { get; init; }
    public string? DestinationPath { get; init; }
    public string? NewName { get; init; }
}

/// <summary>
/// A conflict between two planned actions.
/// </summary>
public sealed record PlanConflict
{
    public required string Description { get; init; }
    public required List<long> ConflictingActionIds { get; init; }
}

/// <summary>
/// A complete processing plan with actions, conflicts, and rollback information.
/// </summary>
public sealed class ProcessingPlan
{
    public required string Id { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
    public required List<PlannedAction> Actions { get; init; }
    public List<PlanConflict> Conflicts { get; init; } = [];
    public List<string> RollbackSteps { get; init; } = [];

    /// <summary>
    /// Serializes the plan to JSON.
    /// </summary>
    public string ToJson()
    {
        return JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
    }

    /// <summary>
    /// Deserializes a plan from JSON.
    /// </summary>
    public static ProcessingPlan? FromJson(string json)
    {
        return JsonSerializer.Deserialize<ProcessingPlan>(json);
    }
}

/// <summary>
/// Generates a processing plan from a set of detected relations.
/// Automatically assigns actions for Equality (auto-deduplicate) and
/// Compatibility (keep-newer), and flags Conflict for manual resolution.
/// </summary>
public static class PlanGenerator
{
    /// <summary>
    /// Generates a plan from a list of detected relations.
    /// </summary>
    /// <param name="duplicateGroups">Duplicate groups from the analysis phase.</param>
    /// <param name="repository">The manifest repository for object lookup.</param>
    /// <returns>A complete processing plan.</returns>
    public static ProcessingPlan Generate(
        List<Manifest.DuplicateGroup> duplicateGroups,
        Manifest.ManifestRepository? repository = null)
    {
        var actions = new List<PlannedAction>();

        foreach (var group in duplicateGroups)
        {
            // For each duplicate group, keep the first member and move the rest to trash.
            for (int i = 1; i < group.Members.Count; i++)
            {
                actions.Add(new PlannedAction
                {
                    ActionType = "MoveToTrash",
                    ObjectId = group.Members[i],
                });
            }
        }

        var plan = new ProcessingPlan
        {
            Id = Guid.NewGuid().ToString("N")[..12],
            CreatedAt = DateTimeOffset.UtcNow,
            Actions = actions,
        };

        // Detect conflicts: multiple actions on the same object.
var objectActionMap = new Dictionary<long, List<long>>();

            for (int i = 0; i < plan.Actions.Count; i++)
        {
            var action = plan.Actions[i];

            if (!objectActionMap.ContainsKey(action.ObjectId))
            {
                objectActionMap[action.ObjectId] = [];
            }

            objectActionMap[action.ObjectId].Add((long)i);
        }

        foreach (var kvp in objectActionMap)
        {
            if (kvp.Value.Count > 1)
            {
                plan.Conflicts.Add(new PlanConflict
                {
                    Description = $"对象 {kvp.Key} 被多个操作引用: {string.Join(", ", kvp.Value)}",
                    ConflictingActionIds = kvp.Value,
                });
            }
        }

        return plan;
    }
}