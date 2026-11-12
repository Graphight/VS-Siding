using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using Vintagestory.API.Datastructures;
using Xunit;

namespace VSSiding.Tests;

public class SidingWallTooltipTests
{
    private static JsonObject Dict(string json) => new(JToken.Parse(json));

    private static readonly JsonObject Framings = Dict("""
    {
        "oak": { "DisplayName": "vssiding:framing-oak" }
    }
    """);

    private static readonly JsonObject Infills = Dict("""
    {
        "wattle": { "DisplayName": "vssiding:infill-wattle" },
        "rubble": { "DisplayName": "vssiding:infill-rubble", "BlockMaterial": "Stone" },
        "nolangentry": { "DisplayName": "vssiding:infill-nolangentry" },
        "nodisplayname": {}
    }
    """);

    private static readonly JsonObject Finishes = Dict("""
    {
        "daub": { "DisplayName": "vssiding:finish-daub" },
        "planks": { "DisplayName": "vssiding:finish-planks" },
        "tiles": { "DisplayName": "vssiding:finish-tiles" },
        "tile-blue": { "Tile": true, "DisplayName": "vssiding:tile-blue" },
        "tile-red": { "Tile": true, "DisplayName": "vssiding:tile-red" }
    }
    """);

    private static readonly Dictionary<string, string> Lang = new()
    {
        ["vssiding:framing-oak"] = "Oak Framing",
        ["vssiding:infill-wattle"] = "Wattle Infill",
        ["vssiding:infill-rubble"] = "Rubble Infill",
        ["vssiding:finish-daub"] = "Daub Finish",
        ["vssiding:finish-planks"] = "Planks Finish",
        ["vssiding:finish-tiles"] = "Tile Finish",
        ["vssiding:tile-blue"] = "Blue tile",
        ["vssiding:tile-red"] = "Red tile",
        ["vssiding:tooltip-no-framing"] = "No framing",
        ["vssiding:tooltip-no-infill"] = "No infill",
        ["vssiding:tooltip-deck"] = "Deck: {0}",
        ["vssiding:tooltip-step"] = "Step: {0}",
        ["vssiding:tooltip-unfinished"] = "unfinished",
        ["vssiding:tooltip-top"] = "Top: {0}",
        ["vssiding:tooltip-underside"] = "Underside: {0}",
        ["vssiding:tooltip-sealed"] = "Seals the room",
        ["vssiding:tooltip-sealed-cool"] = "Seals the room and keeps it cool",
        ["vssiding:tooltip-unsealed"] = "Does not seal the room",
        ["game:facing-north"] = "North",
        ["game:facing-east"] = "East",
        ["game:facing-south"] = "South",
        ["game:facing-west"] = "West",
    };

    // Every wall here hugs "west", so "west" is the front face and "east" the back; a cornerout
    // adds "north" as its second front, leaving "south" as its second back.
    private static string Describe(
        string? framing = "oak", string? infill = "wattle", string? deck = null, string layout = "wall",
        string? front = null, string? secondFront = null, string? back = null, string? stepName = null,
        string? deckInfill = null, string? deckFront = null, string? deckBack = null, bool full = true,
        System.Func<string, bool>? buried = null, string? secondBack = null,
        string? frontStyle = null, string? secondFrontStyle = null, string? backStyle = null, string? deckFrontStyle = null)
        => SidingWallBlock.Describe(
            framing, infill, deck, Framings, Infills, layout, "west", front, secondFront, back, Finishes,
            key => Lang.GetValueOrDefault(key), stepName, deckInfill, deckFront, deckBack, full, buried, secondBack,
            frontStyle, secondFrontStyle, backStyle, null, deckFrontStyle);

    [Fact]
    public void ATiledFaceCountsEachKindInSlotOrder()
    {
        Assert.Equal("\n  Oak Framing\n  Wattle Infill\n  West: 3 Blue tile, 1 Red tile\n  East: unfinished\n  Seals the room\n",
            Describe(front: "tiles", frontStyle: "tile-blue,tile-blue,tile-red,tile-blue"));
    }

    [Fact]
    public void TiledFacesShareALineOnlyWhenTheirTilesMatch()
    {
        Assert.Equal("\n  Oak Framing\n  Wattle Infill\n  West: 1 Blue tile\n  East: 1 Red tile\n  Seals the room\n",
            Describe(front: "tiles", frontStyle: "tile-blue,,,", back: "tiles", backStyle: ",,,tile-red"));
        Assert.Equal("\n  Oak Framing\n  Wattle Infill\n  West, East: 1 Blue tile\n  Seals the room\n",
            Describe(front: "tiles", frontStyle: "tile-blue,,,", back: "tiles", backStyle: "tile-blue,,,"));
    }

    [Fact]
    public void ADeckNamesItsTiledTop()
    {
        Assert.Equal("\n  Oak Framing\n  Wattle Infill\n  Deck: Oak Framing\n    No infill\n    Top: 2 Red tile\n    Underside: unfinished\n  West, East: unfinished\n  Seals the room\n",
            Describe(deck: "oak", deckFront: "tiles", deckFrontStyle: "tile-red,tile-red,,"));
    }

    [Fact]
    public void BuiltFramingAndInfillNameTheirMaterials()
    {
        Assert.Equal("\n  Oak Framing\n  Wattle Infill\n  West, East: unfinished\n  Seals the room\n", Describe());
    }

    [Fact]
    public void WithoutTheBuildSignalOnlyTheSealLineShows()
    {
        Assert.Equal(
            "Seals the room\n",
            Describe(deck: "oak", deckInfill: "wattle", deckFront: "planks", front: "daub", stepName: "Oak Stairs", full: false));
    }

    [Fact]
    public void BuiltDeckNamesItsFramingMaterial()
    {
        Assert.Equal(
            "\n  Oak Framing\n  Wattle Infill\n  Deck: Oak Framing\n    No infill\n    Top: unfinished\n    Underside: unfinished\n  West, East: unfinished\n  Seals the room\n",
            Describe(deck: "oak"));
    }

    [Fact]
    public void BuiltDeckListsItsInfillAndFinishes()
    {
        Assert.Equal(
            "\n  Oak Framing\n  Wattle Infill\n  Deck: Oak Framing\n    Wattle Infill\n    Top: Planks Finish\n    Underside: unfinished\n  West, East: unfinished\n  Seals the room\n",
            Describe(deck: "oak", deckInfill: "wattle", deckFront: "planks"));
    }

    [Fact]
    public void BareDeckListsNoInfillAndNoFinishes()
    {
        Assert.Equal(
            "\n  Oak Framing\n  Wattle Infill\n  Deck: Oak Framing\n    No infill\n    Top: unfinished\n    Underside: unfinished\n  West, East: unfinished\n  Seals the room\n",
            Describe(deck: "oak"));
    }

    [Fact]
    public void BuiltStepNamesTheStairBlock()
    {
        Assert.Equal(
            "\n  Oak Framing\n  Wattle Infill\n  Step: Oak Stairs\n  West, East: unfinished\n  Seals the room\n",
            Describe(stepName: "Oak Stairs"));
    }

    [Fact]
    public void MissingFramingAndInfillPrintTheGapKeys()
    {
        Assert.Equal(
            "\n  No framing\n  No infill\n  West, East: unfinished\n  Does not seal the room\n",
            Describe(framing: null, infill: null));
    }

    [Fact]
    public void NoDisplayNameFallsBackToTitleCasedKey()
    {
        Assert.Equal(
            "\n  Oak Framing\n  Nodisplayname\n  West, East: unfinished\n  Seals the room\n",
            Describe(infill: "nodisplayname"));
    }

    [Fact]
    public void UntranslatedDisplayNameFallsBackToTitleCasedKey()
    {
        Assert.Equal(
            "\n  Oak Framing\n  Nolangentry\n  West, East: unfinished\n  Seals the room\n",
            Describe(infill: "nolangentry"));
    }

    [Fact]
    public void CoolingInfillSealsAndCools()
    {
        Assert.Equal(
            "\n  Oak Framing\n  Rubble Infill\n  West, East: unfinished\n  Seals the room and keeps it cool\n",
            Describe(infill: "rubble"));
    }

    [Fact]
    public void WallNamesEachFaceSeparatelyWhenTheyDiffer()
    {
        Assert.Equal(
            "\n  Oak Framing\n  Wattle Infill\n  West: Daub Finish\n  East: unfinished\n  Seals the room\n",
            Describe(front: "daub"));
    }

    // Every direction answers buried here, so the back staying listed is the front-only rule.
    [Fact]
    public void BuriedFrontIsLeftOutUnlessItIsFinished()
    {
        Assert.Equal(
            (
                "\n  Oak Framing\n  Wattle Infill\n  East: unfinished\n  Seals the room\n",
                "\n  Oak Framing\n  Wattle Infill\n  West: Daub Finish\n  East: unfinished\n  Seals the room\n"),
            (
                Describe(buried: _ => true),
                Describe(front: "daub", buried: _ => true)));
    }

    [Fact]
    public void DiagonalsBuriedSidesDropOneByOne()
    {
        Assert.Equal(
            (
                "\n  Oak Framing\n  Wattle Infill\n  East, South: Daub Finish\n  North: unfinished\n  Seals the room\n",
                "\n  Oak Framing\n  Wattle Infill\n  East, South: Daub Finish\n  Seals the room\n"),
            (
                Describe(layout: "diagonal", back: "daub", buried: direction => direction == "west"),
                Describe(layout: "diagonal", back: "daub", buried: _ => true)));
    }

    [Fact]
    public void WallCollapsesBothFacesOntoOneLineWhenTheyMatch()
    {
        Assert.Equal(
            "\n  Oak Framing\n  Wattle Infill\n  West, East: Daub Finish\n  Seals the room\n",
            Describe(front: "daub", back: "daub"));
    }

    [Fact]
    public void CorneroutNamesAllThreeFinishableFaces()
    {
        Assert.Equal(
            "\n  Oak Framing\n  Wattle Infill\n  West: Daub Finish\n  East, South: unfinished\n  North: Planks Finish\n  Seals the room\n",
            Describe(layout: "cornerout", front: "daub", secondFront: "planks"));
    }

    // The back layer is shared by both legs, so its two directions have to land on one line
    // even though the second leg's front sits between them in build order.
    [Fact]
    public void CorneroutNamesEachInnerFaceAndSharesALineWhereTheyMatch()
    {
        Assert.Equal(
            new[]
            {
                "\n  Oak Framing\n  Wattle Infill\n  West: Daub Finish\n  East, South: Planks Finish\n  North: unfinished\n  Seals the room\n",
                "\n  Oak Framing\n  Wattle Infill\n  West: Daub Finish\n  East: Planks Finish\n  North, South: unfinished\n  Seals the room\n",
            },
            new[]
            {
                Describe(layout: "cornerout", front: "daub", back: "planks", secondBack: "planks"),
                Describe(layout: "cornerout", front: "daub", back: "planks"),
            });
    }

    // A post hugs "west" and "north" like a cornerout: its fronts look the other way and its backs into the corner,
    // where a bare back against a wall is left out.
    [Fact]
    public void PostNamesItsFourFaces()
    {
        Assert.Equal(
            new[]
            {
                "\n  Oak Framing\n  No infill\n  East: Daub Finish\n  South: unfinished\n  West: Planks Finish\n  North: unfinished\n  Does not seal the room\n",
                "\n  Oak Framing\n  No infill\n  East: Daub Finish\n  South: unfinished\n  West: Planks Finish\n  Does not seal the room\n",
            },
            new[]
            {
                Describe(layout: "cornerin", infill: null, front: "daub", back: "planks"),
                Describe(layout: "cornerin", infill: null, front: "daub", back: "planks", buried: direction => direction is "west" or "north"),
            });
    }

    [Fact]
    public void DiagonalSharesItsFrontAcrossBothClaimedFacesAndItsBackAcrossTheOpposites()
    {
        Assert.Equal(
            "\n  Oak Framing\n  Wattle Infill\n  West, North: Daub Finish\n  East, South: Planks Finish\n  Seals the room\n",
            Describe(layout: "diagonal", front: "daub", back: "planks"));
    }

    [Fact]
    public void CorneroutGroupsEveryUnfinishedFaceTogether()
    {
        Assert.Equal(
            "\n  Oak Framing\n  Wattle Infill\n  West: Daub Finish\n  East, North, South: unfinished\n  Seals the room\n",
            Describe(layout: "cornerout", front: "daub"));
    }

    // ComputeRetention already treats a key missing from its dictionary as not built, so the
    // layer lines have to agree - otherwise a wall reads "Ghostwattle" and "Does not seal the
    // room" at once, with nothing on screen connecting the two.
    [Fact]
    public void UninstalledInfillReadsAsNotBuilt()
    {
        Assert.Equal(
            "\n  Oak Framing\n  No infill\n  West, East: unfinished\n  Does not seal the room\n",
            Describe(infill: "ghostwattle"));
    }

    // Normalizing before the grouping is what keeps this on one line: a stale finish key that
    // merely rendered as "unfinished" would still group apart from a genuinely bare face.
    [Fact]
    public void UninstalledFinishGroupsWithTheUnfinishedFaces()
    {
        Assert.Equal(
            "\n  Oak Framing\n  Wattle Infill\n  West, East: unfinished\n  Seals the room\n",
            Describe(front: "ghostdaub"));
    }
}
