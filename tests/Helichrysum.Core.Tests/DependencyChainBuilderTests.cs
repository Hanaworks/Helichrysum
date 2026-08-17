using Helichrysum.Core.Analysis;

namespace Helichrysum.Core.Tests;

public sealed class DependencyChainBuilderTests
{
    [Fact]
    public void LeafResolutions_IncreaseBasedOnCount()
    {
        var chain = new DependencyChainBuilder();
        chain.RecordLeafResolution("/a.txt", "Equality");
        chain.RecordLeafResolution("/b.txt", "Compatibility");

        chain.RecordCompositeDecision("Directory", "/backup", "Compatibility", ["/a.txt", "/b.txt"]);

        var nodes = chain.Nodes;
        Assert.Equal(3, nodes.Count);

        var composite = nodes[2];
        Assert.Equal("Directory", composite.Layer);
        Assert.Equal(2, composite.BasedOnCount);
    }

    [Fact]
    public void UnresolvedSubjects_CountZero()
    {
        var chain = new DependencyChainBuilder();
        chain.RecordCompositeDecision("Structural", "/Archive", "Conflict", ["/unknown.txt"]);

        var composite = chain.Nodes[0];
        Assert.Equal(0, composite.BasedOnCount);
    }

    [Fact]
    public void ToJson_SerialisesNodes()
    {
        var chain = new DependencyChainBuilder();
        chain.RecordLeafResolution("/x.txt", "Equality");
        chain.RecordCompositeDecision("Directory", "/d", "Compatibility", ["/x.txt"]);

        string json = chain.ToJson();

        Assert.Contains("File", json);
        Assert.Contains("Directory", json);
        Assert.Contains("BasedOnCount", json);
    }
}