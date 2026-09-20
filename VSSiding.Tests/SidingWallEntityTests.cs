using System.Collections.Generic;
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

    // Every name here has to exist in window.json, or that part of the window draws nothing at
    // all - no error, just a missing post. WindowShapeHasEveryElementSelectiveElementsAsksFor
    // holds the other end of that.
    [Fact]
    public void WindowTakesNeitherFinishesNorInfillFillers()
    {
        var actual = new[] { (false, false), (true, false), (false, true), (true, true) }
            .ToDictionary(j => j, j => SidingWallEntity.SelectiveElements(
                "window", "oak", "glass", "planks", "planks", "planks", PlankFinishes, j.Item1, j.Item2));

        Assert.Equal(
            new Dictionary<(bool, bool), string[]>
            {
                [(false, false)] = ["framing", "framing-top", "framing-bottom", "infill"],
                [(true, false)] = ["framing", "framing-bottom", "infill"],
                [(false, true)] = ["framing", "framing-top", "infill"],
                [(true, true)] = ["framing", "infill"],
            },
            actual);
    }

    // Glazing splits the mesh in two by this predicate, so a name landing on the wrong side
    // renders the frame see-through or the glass solid - neither of which throws.
    [Fact]
    public void OnlyInfillElementsGoInTheTransparentHalf()
    {
        string[] elements = SidingWallEntity.SelectiveElements(
            "wall", "oak", "glass", "planks", "planks", "planks", PlankFinishes, continuesAbove: true, continuesBelow: true);

        Assert.Equal(
            new Dictionary<string, bool>
            {
                ["front-weatherboard"] = false,
                ["secondfront-weatherboard"] = false,
                ["framing"] = false,
                ["infill"] = true,
                ["infill-top"] = true,
                ["infill-bottom"] = true,
                ["back-boards"] = false,
            },
            elements.ToDictionary(name => name, SidingWallEntity.IsInfillElement));
    }

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
        Assert.Equal(new string[0], SidingWallEntity.SelectiveElements("wall", null, null, null, null, null, NoElementFinishes, false, false));
        Assert.Equal(new[] { "front", "framing", "framing-top", "framing-bottom", "infill", "back" },
            SidingWallEntity.SelectiveElements("wall", "oak", "wattle", "daub", null, "daub", NoElementFinishes, false, false));
        Assert.Equal(new[] { "framing", "framing-top", "framing-bottom" },
            SidingWallEntity.SelectiveElements("wall", "oak", null, null, null, null, NoElementFinishes, false, false));
    }

    [Fact]
    public void SelectiveElementsUsesFinishNamedElementsPerFace()
    {
        Assert.Equal(new[] { "front-weatherboard", "secondfront-weatherboard", "framing", "framing-top", "framing-bottom", "infill", "back-boards" },
            SidingWallEntity.SelectiveElements("wall", "oak", "wattle", "planks", "planks", "planks", PlankFinishes, false, false));
    }

    [Fact]
    public void SelectiveElementsForBottomOfAStackSkipsOnlyTheTopPlate()
    {
        Assert.Equal(new[] { "framing", "framing-bottom", "infill", "infill-top" },
            SidingWallEntity.SelectiveElements("wall", "oak", "wattle", null, null, null, NoElementFinishes, true, false));
    }

    [Fact]
    public void SelectiveElementsForTopOfAStackSkipsOnlyTheBottomPlate()
    {
        Assert.Equal(new[] { "framing", "framing-top", "infill", "infill-bottom" },
            SidingWallEntity.SelectiveElements("wall", "oak", "wattle", null, null, null, NoElementFinishes, false, true));
    }

    [Fact]
    public void SelectiveElementsForAFilledMiddleCellSkipsBothPlatesAndExtendsInfillBothWays()
    {
        Assert.Equal(new[] { "framing", "infill", "infill-top", "infill-bottom" },
            SidingWallEntity.SelectiveElements("wall", "oak", "wattle", null, null, null, NoElementFinishes, true, true));
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
        string wallKey = SidingWallEntity.CacheKey("wall", "west", "oak", "wattle", "daub", "planks", "brick", false, false);
        string cornerKey = SidingWallEntity.CacheKey("cornerout", "west", "oak", "wattle", "daub", "planks", "brick", false, false);

        Assert.NotEqual(wallKey, cornerKey);
    }

    [Fact]
    public void CacheKeyDiffersBySecondFront()
    {
        string withPlanks = SidingWallEntity.CacheKey("cornerout", "west", "oak", "wattle", "daub", "planks", "brick", false, false);
        string withoutSecondFront = SidingWallEntity.CacheKey("cornerout", "west", "oak", "wattle", "daub", null, "brick", false, false);

        Assert.NotEqual(withPlanks, withoutSecondFront);
    }
}
