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

    private static readonly Dictionary<string, string> Lang = new()
    {
        ["vssiding:framing-oak"] = "Oak Framing",
        ["vssiding:infill-wattle"] = "Wattle Infill",
        ["vssiding:tooltip-no-framing"] = "No framing",
        ["vssiding:tooltip-no-infill"] = "No infill",
    };

    private static string? Translate(string key) => Lang.GetValueOrDefault(key);

    [Fact]
    public void BuiltFramingAndInfillNameTheirMaterials()
    {
        Assert.Equal(
            "\n  Oak Framing\n  Wattle Infill\n",
            SidingWallBlock.Describe("oak", "wattle", Framings, Infills, Translate));
    }

    [Fact]
    public void MissingFramingAndInfillPrintTheGapKeys()
    {
        Assert.Equal(
            "\n  No framing\n  No infill\n",
            SidingWallBlock.Describe(null, null, Framings, Infills, Translate));
    }

    [Fact]
    public void NoDisplayNameFallsBackToTitleCasedKey()
    {
        Assert.Equal(
            "\n  Oak Framing\n  Nodisplayname\n",
            SidingWallBlock.Describe("oak", "nodisplayname", Framings, Infills, Translate));
    }

    [Fact]
    public void UntranslatedDisplayNameFallsBackToTitleCasedKey()
    {
        Assert.Equal(
            "\n  Oak Framing\n  Nolangentry\n",
            SidingWallBlock.Describe("oak", "nolangentry", Framings, Infills, Translate));
    }
}
