using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using Vintagestory.API.Datastructures;
using Xunit;

namespace VSSiding.Tests;

public class SidingWallEntityTests
{
    internal static JsonObject Dict(string json) => new(JToken.Parse(json));

    private static readonly JsonObject NoElementFinishes = Dict("""{ "daub": {} }""");

    private static readonly JsonObject PlankFinishes = Dict("""
    {
        "planks": { "Elements": { "front": "front-weatherboard", "back": "back-boards" } }
    }
    """);

    // Three stacked infill boxes share a face at each seam; for glass that blends twice and draws
    // a bright line where a cross-beam would be. Glazing must take the single full-cell pane
    // instead, in every join state, and the pane must still count as infill for the render split.
    [Fact]
    public void GlazingTakesOneFullCellPaneInsteadOfTheFillerStack()
    {
        var joins = new[] { (false, false, false, false), (true, false, false, false), (true, true, false, false) };

        Assert.Equal(
            joins.ToDictionary(j => j, _ => new[] { "infill-pane" }),
            joins.ToDictionary(j => j, j => SidingWallEntity.SelectiveElements(
                "wall", "oak", "glass", null, null, null, NoElementFinishes, j, glazed: true)
                .Where(SidingWallEntity.IsInfillElement).ToArray()));

        // Opaque infill is untouched: it still gets the slab plus a filler per merged side.
        Assert.Equal(
            new[] { "infill", "infill-top", "infill-bottom" },
            SidingWallEntity.SelectiveElements(
                "wall", "oak", "wattle", null, null, null, NoElementFinishes, (true, true, false, false), glazed: false)
                .Where(SidingWallEntity.IsInfillElement).ToArray());
    }

    // Glazing splits the mesh in two by this predicate, so a name landing on the wrong side
    // renders the frame see-through or the glass solid - neither of which throws.
    [Fact]
    public void OnlyInfillElementsGoInTheTransparentHalf()
    {
        string[] elements = SidingWallEntity.SelectiveElements(
            "wall", "oak", "glass", null, null, null, NoElementFinishes, (false, false, false, false), glazed: true);

        Assert.Equal(
            new Dictionary<string, bool>
            {
                ["glazing-left"] = false,
                ["glazing-right"] = false,
                ["glazing-top"] = false,
                ["glazing-bottom"] = false,
                ["infill-pane"] = true,
            },
            elements.ToDictionary(name => name, SidingWallEntity.IsInfillElement));
    }

    // Merging belongs to glazing, not to a layout: a glazed cell drops the post or plate against
    // every glazed neighbour, so a run of them is one sheet with framing only round the outside.
    [Fact]
    public void GlazedRunDropsEveryMemberItMergesAgainst()
    {
        var joins = new (bool above, bool below, bool left, bool right)[]
        {
            (false, false, false, false),
            (false, false, true, false),
            (true, false, false, true),
            (true, true, true, true),
        };

        Assert.Equal(
            new Dictionary<(bool, bool, bool, bool), string[]>
            {
                [(false, false, false, false)] = ["glazing-left", "glazing-right", "glazing-top", "glazing-bottom", "infill-pane"],
                [(false, false, true, false)] = ["glazing-right", "glazing-top", "glazing-bottom", "infill-pane"],
                [(true, false, false, true)] = ["glazing-left", "glazing-bottom", "infill-pane"],
                // Surrounded by glazing: nothing but the pane, so the run reads as one sheet.
                [(true, true, true, true)] = ["infill-pane"],
            },
            joins.ToDictionary(j => j, j => SidingWallEntity.SelectiveElements(
                "wall", "oak", "glass", null, null, null, NoElementFinishes, j, glazed: true)));
    }

    // A cornerout's three posts are structural, so glazing never takes them - only its plates.
    [Fact]
    public void GlazedCorneroutKeepsItsPosts()
    {
        Assert.Equal(
            ["framing", "infill-pane"],
            SidingWallEntity.SelectiveElements(
                "cornerout", "oak", "glass", null, null, null, NoElementFinishes, (true, true, false, false), glazed: true));
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
        Assert.Equal(new string[0], SidingWallEntity.SelectiveElements("wall", null, null, null, null, null, NoElementFinishes, (false, false, false, false), glazed: false));
        Assert.Equal(new[] { "front", "framing-left", "framing-right", "framing-top", "framing-bottom", "infill", "back" },
            SidingWallEntity.SelectiveElements("wall", "oak", "wattle", "daub", null, "daub", NoElementFinishes, (false, false, false, false), glazed: false));
        Assert.Equal(new[] { "framing-left", "framing-right", "framing-top", "framing-bottom" },
            SidingWallEntity.SelectiveElements("wall", "oak", null, null, null, null, NoElementFinishes, (false, false, false, false), glazed: false));
    }

    [Fact]
    public void SelectiveElementsUsesFinishNamedElementsPerFace()
    {
        Assert.Equal(new[] { "front-weatherboard", "secondfront-weatherboard", "framing-left", "framing-right", "framing-top", "framing-bottom", "infill", "back-boards" },
            SidingWallEntity.SelectiveElements("wall", "oak", "wattle", "planks", "planks", "planks", PlankFinishes, (false, false, false, false), glazed: false));
    }

    [Fact]
    public void SelectiveElementsForBottomOfAStackSkipsOnlyTheTopPlate()
    {
        Assert.Equal(new[] { "framing-left", "framing-right", "framing-bottom", "infill", "infill-top" },
            SidingWallEntity.SelectiveElements("wall", "oak", "wattle", null, null, null, NoElementFinishes, (true, false, false, false), glazed: false));
    }

    [Fact]
    public void SelectiveElementsForTopOfAStackSkipsOnlyTheBottomPlate()
    {
        Assert.Equal(new[] { "framing-left", "framing-right", "framing-top", "infill", "infill-bottom" },
            SidingWallEntity.SelectiveElements("wall", "oak", "wattle", null, null, null, NoElementFinishes, (false, true, false, false), glazed: false));
    }

    [Fact]
    public void SelectiveElementsForAFilledMiddleCellSkipsBothPlatesAndExtendsInfillBothWays()
    {
        Assert.Equal(new[] { "framing-left", "framing-right", "infill", "infill-top", "infill-bottom" },
            SidingWallEntity.SelectiveElements("wall", "oak", "wattle", null, null, null, NoElementFinishes, (true, true, false, false), glazed: false));
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

    // Anything the mesh is built from has to reach the key, or two differently-shaped walls share
    // one cached mesh - a merged glazed cell and an unmerged one being the newest way to get that wrong.
    [Fact]
    public void CacheKeyDistinguishesEveryMeshInput()
    {
        string[] keys =
        [
            SidingWallEntity.CacheKey("wall", "west", "oak", "wattle", "daub", "planks", "brick", (false, false, false, false)),
            SidingWallEntity.CacheKey("cornerout", "west", "oak", "wattle", "daub", "planks", "brick", (false, false, false, false)),
            SidingWallEntity.CacheKey("wall", "south", "oak", "wattle", "daub", "planks", "brick", (false, false, false, false)),
            SidingWallEntity.CacheKey("wall", "west", "veryaged", "wattle", "daub", "planks", "brick", (false, false, false, false)),
            SidingWallEntity.CacheKey("wall", "west", "oak", "glass-plain", "daub", "planks", "brick", (false, false, false, false)),
            SidingWallEntity.CacheKey("wall", "west", "oak", "wattle", null, "planks", "brick", (false, false, false, false)),
            SidingWallEntity.CacheKey("wall", "west", "oak", "wattle", "daub", null, "brick", (false, false, false, false)),
            SidingWallEntity.CacheKey("wall", "west", "oak", "wattle", "daub", "planks", null, (false, false, false, false)),
            SidingWallEntity.CacheKey("wall", "west", "oak", "wattle", "daub", "planks", "brick", (true, false, false, false)),
            SidingWallEntity.CacheKey("wall", "west", "oak", "wattle", "daub", "planks", "brick", (false, true, false, false)),
            SidingWallEntity.CacheKey("wall", "west", "oak", "wattle", "daub", "planks", "brick", (false, false, true, false)),
            SidingWallEntity.CacheKey("wall", "west", "oak", "wattle", "daub", "planks", "brick", (false, false, false, true)),
        ];

        Assert.Equal(keys, keys.Distinct());
    }
}
