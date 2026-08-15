using Helichrysum.Core.Planning;
using Helichrysum.Core.Manifest;

namespace Helichrysum.Core.Tests;

public sealed class PlanTests
{
    [Fact]
    public void Generate_FromDuplicateGroups_CreatesActions()
    {
        var groups = new List<DuplicateGroup>
        {
            new DuplicateGroup
            {
                HashValue = "abc123",
                Members = [1, 2, 3],
                Size = 100,
                Count = 3,
            },
        };

        var plan = PlanGenerator.Generate(groups);

        Assert.NotNull(plan);
        Assert.NotEmpty(plan.Actions);
        Assert.Equal(2, plan.Actions.Count); // Keep 1, trash 2
    }

    [Fact]
    public void Plan_Serialization_RoundTrip()
    {
        var groups = new List<DuplicateGroup>
        {
            new DuplicateGroup
            {
                HashValue = "def456",
                Members = [10, 20],
                Size = 200,
                Count = 2,
            },
        };

        var plan = PlanGenerator.Generate(groups);
        string json = plan.ToJson();
        var deserialized = ProcessingPlan.FromJson(json);

        Assert.NotNull(deserialized);
        Assert.Equal(plan.Id, deserialized!.Id);
        Assert.Equal(plan.Actions.Count, deserialized.Actions.Count);
    }

    [Fact]
    public void Generate_EmptyGroups_NoActions()
    {
        var plan = PlanGenerator.Generate([]);

        Assert.NotNull(plan);
        Assert.Empty(plan.Actions);
    }

    [Fact]
    public void SingleMemberGroup_NoActions()
    {
        var groups = new List<DuplicateGroup>
        {
            new DuplicateGroup
            {
                HashValue = "xyz789",
                Members = [1],
                Size = 50,
                Count = 1,
            },
        };

        var plan = PlanGenerator.Generate(groups);

        Assert.Empty(plan.Actions);
    }
}