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
        "clay": { "BlockMaterial": "Soil" },
        "glass": { "BlockMaterial": "Glass", "Transparent": true }
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
            [
                ("item", new AssetLocation("game:stone-granite"), new Dictionary<string, string> { ["rock"] = "granite" }),
                ("item", new AssetLocation("game:clay-blue"), new Dictionary<string, string> { ["type"] = "blue" }),
                ("item", new AssetLocation("game:clay-red"), new Dictionary<string, string> { ["type"] = "red" }),
                ("item", new AssetLocation("game:clay-fire"), new Dictionary<string, string> { ["type"] = "fire" }),
                ("block", new AssetLocation("game:glass-plain"), new Dictionary<string, string> { ["color"] = "plain" }),
                ("block", new AssetLocation("game:glass-smoky"), new Dictionary<string, string> { ["color"] = "smoky" }),
            ]);

        var actual = infills.Properties().ToDictionary(p => p.Name,
            p => SidingWallBlock.ComputeRetention(true, "oak", p.Name, new JsonObject(attributes["Framings"]), new JsonObject(infills)));

        Assert.Equal(
            new Dictionary<string, int>
            {
                ["wattle"] = 1, ["straw"] = 1, ["clay"] = -1, ["clay-red"] = -1, ["clay-fire"] = -1,
                ["stone-granite"] = -1, ["glass-plain"] = 1, ["glass-smoky"] = 1,
            },
            actual);
    }

    [Fact]
    public void TransparentInfillSealsLikeAnyOther()
    {
        Assert.Equal(1, SidingWallBlock.ComputeRetention(true, "oak", "glass", Framings, Infills));
    }

    // Vanilla reads the liquid barrier off SideSolid, which decision 0002 turned off on every
    // face - so without the override every wall leaked. Anything that seals air seals water,
    // cooling infill and glazing included; an open frame and an unclaimed face do not.
    [Fact]
    public void OnlyASealedClaimedFaceDamsWater()
    {
        var cases = new[]
        {
            (claimed: true, framing: (string?)null, infill: (string?)null),
            (true, "oak", null),
            (true, "oak", "wattle"),
            (true, "oak", "clay"),
            (true, "oak", "glass"),
            (true, "oak", "uninstalled"),
            (false, "oak", "wattle"),
        };

        Assert.Equal(
            new[] { 0f, 0f, 1f, 1f, 1f, 0f, 0f },
            cases.Select(c => SidingWallBlock.ComputeLiquidBarrier(c.Item1, c.Item2, c.Item3, Framings, Infills)));
    }

    // Glazing is the case that splits these two apart: sealed, so it retains, but not opaque.
    [Fact]
    public void OnlyAnOpaqueSealedWallAbsorbsLight()
    {
        var actual = new[] { (null, null), ("oak", null), ("oak", "wattle"), ("oak", "clay"), ("oak", "glass"), ("oak", "uninstalled") }
            .Select(w => SidingWallBlock.ComputeLightAbsorption(w.Item1, w.Item2, Framings, Infills));

        Assert.Equal(new[] { 0, 0, 99, 99, 0, 0 }, actual);
    }
}
