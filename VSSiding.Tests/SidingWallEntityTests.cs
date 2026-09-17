using Newtonsoft.Json.Linq;
using Vintagestory.API.Datastructures;
using Xunit;

namespace VSSiding.Tests;

public class SidingWallEntityTests
{
    private static JsonObject Dict(string json) => new(JToken.Parse(json));

    private static readonly JsonObject NoElementFinishes = Dict("""
    {
        "daub": { "Consumes": { "type": "item", "code": "game:clay-blue-raw", "quantity": 2 } }
    }
    """);

    private static readonly JsonObject PlankFinishes = Dict("""
    {
        "planks": {
            "Elements": { "front": "front-weatherboard", "back": "back-boards" },
            "Consumes": { "type": "item", "code": "game:plank-oak", "quantity": 2 }
        }
    }
    """);

    [Fact]
    public void UnsetKeySurvivesByteRoundTripAsNullNotEmptyString()
    {
        var tree = new TreeAttribute();
        tree.SetString("framing", null);

        var reloaded = new TreeAttribute();
        reloaded.FromBytes(tree.ToBytes());

        Assert.Null(SidingWallEntity.NullIfEmpty(reloaded.GetString("framing", null)));
    }

    [Fact]
    public void SetKeySurvivesByteRoundTrip()
    {
        var tree = new TreeAttribute();
        tree.SetString("framing", "oak");

        var reloaded = new TreeAttribute();
        reloaded.FromBytes(tree.ToBytes());

        Assert.Equal("oak", SidingWallEntity.NullIfEmpty(reloaded.GetString("framing", null)));
    }

    [Fact]
    public void SelectiveElementsSkipsUnbuiltParts()
    {
        Assert.Equal(new string[0], SidingWallEntity.SelectiveElements(null, null, null, null, NoElementFinishes));
        Assert.Equal(new[] { "front", "framing", "infill", "back" },
            SidingWallEntity.SelectiveElements("oak", "wattle", "daub", "daub", NoElementFinishes));
        Assert.Equal(new[] { "framing" }, SidingWallEntity.SelectiveElements("oak", null, null, null, NoElementFinishes));
    }

    [Fact]
    public void SelectiveElementsUsesFinishNamedElementsPerFace()
    {
        Assert.Equal(new[] { "front-weatherboard", "framing", "infill", "back-boards" },
            SidingWallEntity.SelectiveElements("oak", "wattle", "planks", "planks", PlankFinishes));
    }

    [Theory]
    [InlineData("west", 0)]
    [InlineData("south", 90)]
    [InlineData("east", 180)]
    [InlineData("north", 270)]
    public void RotationYDegMatchesCollisionBoxRotation(string side, float expectedDegrees)
    {
        Assert.Equal(expectedDegrees, SidingWallEntity.RotationYDeg(side));
    }

    [Fact]
    public void CacheKeyDiffersByLayoutForTheSameMaterials()
    {
        string wallKey = SidingWallEntity.CacheKey("wall", "west", "oak", "wattle", "daub", "brick");
        string cornerKey = SidingWallEntity.CacheKey("cornerout", "west", "oak", "wattle", "daub", "brick");

        Assert.NotEqual(wallKey, cornerKey);
    }
}
