using Newtonsoft.Json.Linq;
using Vintagestory.API.Datastructures;
using Xunit;

namespace VSSiding.Tests;

public class SidingWallBlockRetentionTests
{
    private static JsonObject Dict(string json) => new(JToken.Parse(json));

    private static readonly JsonObject Framings = Dict("""{ "oak": {} }""");

    private static readonly JsonObject Infills = Dict("""
    {
        "wattle": { "BlockMaterial": "Wood" },
        "clay": { "BlockMaterial": "Soil" }
    }
    """);

    [Fact]
    public void NotClaimedIsZeroEvenWhenBothBuilt()
    {
        Assert.Equal(0, SidingWallBlock.ComputeRetention(false, "oak", "wattle", Framings, Infills));
    }

    [Fact]
    public void MissingFramingIsZero()
    {
        Assert.Equal(0, SidingWallBlock.ComputeRetention(true, null, "wattle", Framings, Infills));
    }

    [Fact]
    public void MissingInfillIsZero()
    {
        Assert.Equal(0, SidingWallBlock.ComputeRetention(true, "oak", null, Framings, Infills));
    }

    [Fact]
    public void UnknownFramingKeyIsZero()
    {
        Assert.Equal(0, SidingWallBlock.ComputeRetention(true, "uninstalled", "wattle", Framings, Infills));
    }

    [Fact]
    public void UnknownInfillKeyIsZero()
    {
        Assert.Equal(0, SidingWallBlock.ComputeRetention(true, "oak", "uninstalled", Framings, Infills));
    }

    [Fact]
    public void CoolingInfillSealsNegative()
    {
        Assert.Equal(-1, SidingWallBlock.ComputeRetention(true, "oak", "clay", Framings, Infills));
    }

    [Fact]
    public void NonCoolingInfillSealsPositive()
    {
        Assert.Equal(1, SidingWallBlock.ComputeRetention(true, "oak", "wattle", Framings, Infills));
    }
}
