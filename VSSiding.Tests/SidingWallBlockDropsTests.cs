using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Xunit;

namespace VSSiding.Tests;

public class SidingWallBlockDropsTests
{
    private static JsonObject Dict(string json) => new(JToken.Parse(json));

    private static readonly JsonObject Framings = Dict("""
    { "oak": { "Drops": [ { "type": "item", "code": "game:plank-oak", "quantity": { "avg": 2, "var": 0 } } ] } }
    """);

    private static readonly JsonObject Infills = Dict("""
    { "wattle": { "Drops": [ { "type": "item", "code": "game:stick", "quantity": { "avg": 4, "var": 0 } } ] } }
    """);

    private static readonly JsonObject Finishes = Dict("""
    {
        "daub": { "Drops": [ { "type": "item", "code": "game:clay-blue", "quantity": { "avg": 2, "var": 0 } } ] },
        "planks": { "Drops": [ { "type": "item", "code": "game:plank-oak", "quantity": { "avg": 2, "var": 0 } } ] },
        "brick": { "Drops": [ { "type": "item", "code": "game:burnedbrick-red", "quantity": { "avg": 2, "var": 0 } } ] },
        "nocodefinish": { "Drops": [ { "type": "item", "quantity": { "avg": 2, "var": 0 } } ] }
    }
    """);

    private static string[] Codes(List<BlockDropItemStack> drops) =>
        drops.ConvertAll(d => d.Code!.ToString()).ToArray();

    [Fact]
    public void NoPartsBuiltDropsNothing()
    {
        var drops = SidingWallBlock.ComputeDrops(null, null, null, null, null, null, Framings, Infills, Finishes);
        Assert.Equal(Array.Empty<string>(), Codes(drops));
    }

    [Fact]
    public void UnknownKeysDropNothing()
    {
        var drops = SidingWallBlock.ComputeDrops("uninstalled", "uninstalled", "uninstalled", "uninstalled", "uninstalled", "uninstalled", Framings, Infills, Finishes);
        Assert.Equal(Array.Empty<string>(), Codes(drops));
    }

    [Fact]
    public void OnlyValidPartsDrop()
    {
        var drops = SidingWallBlock.ComputeDrops("oak", "wattle", "uninstalled", null, null, null, Framings, Infills, Finishes);
        Assert.Equal(new[] { "game:plank-oak", "game:stick" }, Codes(drops));
    }

    [Fact]
    public void AllFivePartsDrop()
    {
        var drops = SidingWallBlock.ComputeDrops("oak", "wattle", "daub", "planks", "brick", null, Framings, Infills, Finishes);
        Assert.Equal(new[] { "game:plank-oak", "game:stick", "game:clay-blue", "game:plank-oak", "game:burnedbrick-red" }, Codes(drops));
    }

    [Fact]
    public void DeckDropsItsFramingsMaterial()
    {
        var drops = SidingWallBlock.ComputeDrops(null, null, null, null, null, "oak", Framings, Infills, Finishes);
        Assert.Equal(new[] { "game:plank-oak" }, Codes(drops));
    }

    [Fact]
    public void DropEntryWithNoCodeIsSkippedNotThrown()
    {
        var drops = SidingWallBlock.ComputeDrops(null, null, "nocodefinish", null, null, null, Framings, Infills, Finishes);
        Assert.Equal(Array.Empty<string>(), Codes(drops));
    }

    [Fact]
    public void DropsKeepTheirItemOrBlockType()
    {
        var finishes = Dict("""
        { "cobblestone-granite": { "Drops": [ { "type": "block", "code": "game:cobblestone-granite", "quantity": { "avg": 1, "var": 0 } } ] } }
        """);

        var drops = SidingWallBlock.ComputeDrops("oak", null, "cobblestone-granite", null, null, null, Framings, Infills, finishes);

        Assert.Equal(new[] { (EnumItemClass.Item, "game:plank-oak"), (EnumItemClass.Block, "game:cobblestone-granite") }, drops.ConvertAll(d => (d.Type, d.Code!.ToString())).ToArray());
    }
}
