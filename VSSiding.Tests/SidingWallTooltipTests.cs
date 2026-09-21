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
        "planks": { "DisplayName": "vssiding:finish-planks" }
    }
    """);

    private static readonly Dictionary<string, string> Lang = new()
    {
        ["vssiding:framing-oak"] = "Oak Framing",
        ["vssiding:infill-wattle"] = "Wattle Infill",
        ["vssiding:infill-rubble"] = "Rubble Infill",
        ["vssiding:finish-daub"] = "Daub Finish",
        ["vssiding:finish-planks"] = "Planks Finish",
        ["vssiding:tooltip-no-framing"] = "No framing",
        ["vssiding:tooltip-no-infill"] = "No infill",
        ["vssiding:tooltip-unfinished"] = "unfinished",
        ["vssiding:tooltip-sealed"] = "Seals the room",
        ["vssiding:tooltip-sealed-cool"] = "Seals the room and keeps it cool",
        ["vssiding:tooltip-unsealed"] = "Doesn't seal the room",
        ["game:facing-north"] = "North",
        ["game:facing-east"] = "East",
        ["game:facing-south"] = "South",
        ["game:facing-west"] = "West",
    };

    // Every wall here hugs "west", so "west" is the front face and "east" the back; a cornerout
    // adds "north" as its second front, leaving "south" as that leg's share of the same back.
    private static string Describe(
        string? framing = "oak", string? infill = "wattle", string layout = "wall",
        string? front = null, string? secondFront = null, string? back = null)
        => SidingWallBlock.Describe(
            framing, infill, Framings, Infills, layout, "west", front, secondFront, back, Finishes,
            key => Lang.GetValueOrDefault(key));

    [Fact]
    public void BuiltFramingAndInfillNameTheirMaterials()
    {
        Assert.Equal("\n  Oak Framing\n  Wattle Infill\n  West, East: unfinished\n  Seals the room\n", Describe());
    }

    [Fact]
    public void MissingFramingAndInfillPrintTheGapKeys()
    {
        Assert.Equal(
            "\n  No framing\n  No infill\n  West, East: unfinished\n  Doesn't seal the room\n",
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
    public void CorneroutSharesOneBackLineAcrossBothLegs()
    {
        Assert.Equal(
            "\n  Oak Framing\n  Wattle Infill\n  West: Daub Finish\n  East, South: Planks Finish\n  North: unfinished\n  Seals the room\n",
            Describe(layout: "cornerout", front: "daub", back: "planks"));
    }

    [Fact]
    public void CorneroutGroupsEveryUnfinishedFaceTogether()
    {
        Assert.Equal(
            "\n  Oak Framing\n  Wattle Infill\n  West: Daub Finish\n  East, North, South: unfinished\n  Seals the room\n",
            Describe(layout: "cornerout", front: "daub"));
    }

    // ComputeRetention already treats a key missing from its dictionary as not built, so the
    // layer lines have to agree - otherwise a wall reads "Ghostwattle" and "Doesn't seal the
    // room" at once, with nothing on screen connecting the two.
    [Fact]
    public void UninstalledInfillReadsAsNotBuilt()
    {
        Assert.Equal(
            "\n  Oak Framing\n  No infill\n  West, East: unfinished\n  Doesn't seal the room\n",
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
