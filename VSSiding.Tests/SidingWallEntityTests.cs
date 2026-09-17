using System.Linq;
using Newtonsoft.Json.Linq;
using Vintagestory.API.Datastructures;
using Xunit;

namespace VSSiding.Tests;

public class SidingWallEntityTests
{
    private static JsonObject Dict(string json) => new(JToken.Parse(json));

    private static readonly JsonObject NoElementFinishes = Dict("""{ "daub": {} }""");

    private static readonly JsonObject PlankFinishes = Dict("""
    {
        "planks": { "Elements": { "front": "front-weatherboard", "back": "back-boards" } }
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
        Assert.Equal(new string[0], SidingWallEntity.SelectiveElements(null, null, null, null, NoElementFinishes, false, false));
        Assert.Equal(new[] { "front", "framing", "framing-top", "framing-bottom", "infill", "back" },
            SidingWallEntity.SelectiveElements("oak", "wattle", "daub", "daub", NoElementFinishes, false, false));
        Assert.Equal(new[] { "framing", "framing-top", "framing-bottom" },
            SidingWallEntity.SelectiveElements("oak", null, null, null, NoElementFinishes, false, false));
    }

    [Fact]
    public void SelectiveElementsUsesFinishNamedElementsPerFace()
    {
        Assert.Equal(new[] { "front-weatherboard", "framing", "framing-top", "framing-bottom", "infill", "back-boards" },
            SidingWallEntity.SelectiveElements("oak", "wattle", "planks", "planks", PlankFinishes, false, false));
    }

    [Fact]
    public void SelectiveElementsForBottomOfAStackSkipsOnlyTheBottomPlate()
    {
        Assert.Equal(new[] { "framing", "framing-bottom", "infill", "infill-top" },
            SidingWallEntity.SelectiveElements("oak", "wattle", null, null, NoElementFinishes, true, false));
    }

    [Fact]
    public void SelectiveElementsForTopOfAStackSkipsOnlyTheTopPlate()
    {
        Assert.Equal(new[] { "framing", "framing-top", "infill", "infill-bottom" },
            SidingWallEntity.SelectiveElements("oak", "wattle", null, null, NoElementFinishes, false, true));
    }

    [Fact]
    public void SelectiveElementsForAFilledMiddleCellSkipsBothPlatesAndExtendsInfillBothWays()
    {
        Assert.Equal(new[] { "framing", "infill", "infill-top", "infill-bottom" },
            SidingWallEntity.SelectiveElements("oak", "wattle", null, null, NoElementFinishes, true, true));
    }

    [Theory]
    [InlineData(1, new[] { true })]
    [InlineData(2, new[] { false, true })]
    [InlineData(3, new[] { false, true, true })]
    [InlineData(4, new[] { false, true, false, true })]
    public void EverySecondCellUpAStackKeepsItsTopPlateAsACrossBeam(int height, bool[] expectedTopPlates)
    {
        bool[] topPlates = Enumerable.Range(0, height)
            .Select(cellsBelow => !SidingWallBlock.JoinsAbove(cellsBelow < height - 1, cellsBelow))
            .ToArray();

        Assert.Equal(expectedTopPlates, topPlates);
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
        string wallKey = SidingWallEntity.CacheKey("wall", "west", "oak", "wattle", "daub", "brick", false, false);
        string cornerKey = SidingWallEntity.CacheKey("cornerout", "west", "oak", "wattle", "daub", "brick", false, false);

        Assert.NotEqual(wallKey, cornerKey);
    }
}
