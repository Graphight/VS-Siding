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
        ["vssiding:tooltip-no-framing"] = "No framing",
        ["vssiding:tooltip-no-infill"] = "No infill",
        ["vssiding:tooltip-unfinished"] = "unfinished",
        ["vssiding:tooltip-sealed"] = "Seals the room",
        ["vssiding:tooltip-sealed-cool"] = "Seals the room and keeps it cool",
        ["vssiding:tooltip-unsealed"] = "Doesn't seal the room",
        ["vssiding:finish-daub"] = "Daub Finish",
        ["vssiding:finish-planks"] = "Planks Finish",
        ["game:facing-north"] = "North",
        ["game:facing-east"] = "East",
        ["game:facing-south"] = "South",
        ["game:facing-west"] = "West",
    };

    private static string? Translate(string key) => Lang.GetValueOrDefault(key);

    [Fact]
    public void BuiltFramingAndInfillNameTheirMaterials()
    {
        Assert.Equal(
            "\n  Oak Framing\n  Wattle Infill\n  West, East: unfinished\n  Seals the room\n",
            SidingWallBlock.Describe("oak", "wattle", Framings, Infills, "wall", "west", null, null, null, Finishes, Translate));
    }

    [Fact]
    public void MissingFramingAndInfillPrintTheGapKeys()
    {
        Assert.Equal(
            "\n  No framing\n  No infill\n  West, East: unfinished\n  Doesn't seal the room\n",
            SidingWallBlock.Describe(null, null, Framings, Infills, "wall", "west", null, null, null, Finishes, Translate));
    }

    [Fact]
    public void NoDisplayNameFallsBackToTitleCasedKey()
    {
        Assert.Equal(
            "\n  Oak Framing\n  Nodisplayname\n  West, East: unfinished\n  Seals the room\n",
            SidingWallBlock.Describe("oak", "nodisplayname", Framings, Infills, "wall", "west", null, null, null, Finishes, Translate));
    }

    [Fact]
    public void UntranslatedDisplayNameFallsBackToTitleCasedKey()
    {
        Assert.Equal(
            "\n  Oak Framing\n  Nolangentry\n  West, East: unfinished\n  Seals the room\n",
            SidingWallBlock.Describe("oak", "nolangentry", Framings, Infills, "wall", "west", null, null, null, Finishes, Translate));
    }

    [Fact]
    public void WallWithOneFaceFinished()
    {
        Assert.Equal(
            "\n  Oak Framing\n  Wattle Infill\n  West: Daub Finish\n  East: unfinished\n  Seals the room\n",
            SidingWallBlock.Describe("oak", "wattle", Framings, Infills, "wall", "west", "daub", null, null, Finishes, Translate));
    }

    [Fact]
    public void CorneroutWithBothLegsFinished()
    {
        Assert.Equal(
            "\n  Oak Framing\n  Wattle Infill\n  West: Daub Finish\n  East, South: unfinished\n  North: Planks Finish\n  Seals the room\n",
            SidingWallBlock.Describe("oak", "wattle", Framings, Infills, "cornerout", "west", "daub", "planks", null, Finishes, Translate));
    }

    [Fact]
    public void CorneroutWithOnlyOneLegFinished()
    {
        Assert.Equal(
            "\n  Oak Framing\n  Wattle Infill\n  West: Daub Finish\n  East, North, South: unfinished\n  Seals the room\n",
            SidingWallBlock.Describe("oak", "wattle", Framings, Infills, "cornerout", "west", "daub", null, null, Finishes, Translate));
    }

    [Fact]
    public void CoolingInfillSealsAndCools()
    {
        var infills = Dict("""
        {
            "rubble": { "DisplayName": "vssiding:infill-stone", "BlockMaterial": "Stone" }
        }
        """);
        Assert.Equal(
            "\n  Oak Framing\n  Rubble\n  West, East: unfinished\n  Seals the room and keeps it cool\n",
            SidingWallBlock.Describe("oak", "rubble", Framings, infills, "wall", "west", null, null, null, Finishes, Translate));
    }
}
