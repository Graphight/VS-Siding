using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using Vintagestory.API.Common;
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

    [Fact]
    public void ShippedInfillsSealWithTheirCoolingSign()
    {
        var wallJsonPath = Path.Combine(MaterialTextureOpacityTests.GetAssemblyMetadata("RepoRoot"), "VSSiding", "assets", "vssiding", "blocktypes", "wall.json");
        var attributes = (JObject)JToken.Parse(File.ReadAllText(wallJsonPath))["attributes"]!;
        var infills = MaterialFamilies.Expand((JObject)attributes["InfillFamilies"]!, (JObject)attributes["Infills"]!,
            [("item", new AssetLocation("game:stone-granite"), new Dictionary<string, string> { ["rock"] = "granite" })]);

        var actual = infills.Properties().ToDictionary(p => p.Name,
            p => SidingWallBlock.ComputeRetention(true, "oak", p.Name, new JsonObject(attributes["Framings"]), new JsonObject(infills)));

        Assert.Equal(new Dictionary<string, int> { ["wattle"] = 1, ["straw"] = 1, ["clay"] = -1, ["stone-granite"] = -1 }, actual);
    }

    [Fact]
    public void OnlyASealedWallAbsorbsLight()
    {
        var actual = new[] { (null, null), ("oak", null), ("oak", "wattle"), ("oak", "clay"), ("oak", "uninstalled") }
            .Select(w => SidingWallBlock.ComputeLightAbsorption(w.Item1, w.Item2, Framings, Infills));

        Assert.Equal(new[] { 0, 0, 99, 99, 0 }, actual);
    }
}
