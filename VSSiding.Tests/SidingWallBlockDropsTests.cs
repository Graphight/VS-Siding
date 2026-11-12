using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
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
    public void DropsOfCarriesEveryDeckLayer()
    {
        var wall = new SidingWallBlock
        {
            Attributes = new JsonObject(JToken.Parse("""{ "Framings": { "oak": { "Drops": [ { "type": "item", "code": "game:plank-oak" } ] } }, "Infills": { "wattle": { "Drops": [ { "type": "item", "code": "game:stick" } ] } }, "Finishes": { "daub": { "Drops": [ { "type": "item", "code": "game:clay-blue" } ] }, "brick": { "Drops": [ { "type": "item", "code": "game:burnedbrick-red" } ] } } }""")),
        };
        wall.VariantStrict["layout"] = "wall";
        var entity = new SidingWallEntity { Deck = "oak", DeckInfill = "wattle", DeckFront = "daub", DeckBack = "brick" };
        Assert.Equal(new[] { "game:plank-oak", "game:stick", "game:clay-blue", "game:burnedbrick-red" }, Codes(wall.DropsOf(entity)));
    }

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
    public void PostRefundsAQuarterOfItsFraming()
    {
        var drops = SidingWallBlock.ComputeDrops("oak", null, null, null, null, null, Framings, Infills, Finishes, layout: "cornerin");
        Assert.Equal(new[] { ("game:plank-oak", 1f) }, drops.ConvertAll(d => (d.Code!.ToString(), d.Quantity.avg)));
    }

    [Fact]
    public void PostRefundsAQuarterOfEachFinishToo()
    {
        var drops = SidingWallBlock.ComputeDrops("oak", null, "daub", "brick", null, null, Framings, Infills, Finishes, layout: "cornerin");
        Assert.Equal(
            new[] { ("game:plank-oak", 1f), ("game:clay-blue", 1f), ("game:burnedbrick-red", 1f) },
            drops.ConvertAll(d => (d.Code!.ToString(), d.Quantity.avg)));
    }

    // A corner's inner faces each refund half a wall's finish; its frame, infill and outer faces refund in full (decision 0090).
    [Fact]
    public void CornerRefundsHalfOfEachInnerFace()
    {
        var drops = SidingWallBlock.ComputeDrops("oak", "wattle", "daub", "planks", "brick", null, Framings, Infills, Finishes, layout: "cornerout", secondBack: "daub");
        Assert.Equal(
            new[] { ("game:plank-oak", 2f), ("game:stick", 4f), ("game:clay-blue", 2f), ("game:plank-oak", 2f), ("game:burnedbrick-red", 1f), ("game:clay-blue", 1f) },
            drops.ConvertAll(d => (d.Code!.ToString(), d.Quantity.avg)));
    }

    [Fact]
    public void DeckDropsItsFramingsMaterial()
    {
        var drops = SidingWallBlock.ComputeDrops(null, null, null, null, null, "oak", Framings, Infills, Finishes);
        Assert.Equal(new[] { "game:plank-oak" }, Codes(drops));
    }

    [Fact]
    public void DeckLayersDropTheirOwnMaterials()
    {
        var drops = SidingWallBlock.ComputeDrops(null, null, null, null, null, "oak", Framings, Infills, Finishes, null, "wattle", "daub", "brick");
        Assert.Equal(new[] { "game:plank-oak", "game:stick", "game:clay-blue", "game:burnedbrick-red" }, Codes(drops));
    }

    // A diagonal's deck covers one triangle, so each layer refunds half a wall's amount (decision 0096).
    [Fact]
    public void DiagonalDeckRefundsHalfOfEachLayer()
    {
        var drops = SidingWallBlock.ComputeDrops(null, null, null, null, null, "oak", Framings, Infills, Finishes, null, "wattle", "daub", "brick", layout: "diagonal");
        Assert.Equal(
            new[] { ("game:plank-oak", 1f), ("game:stick", 2f), ("game:clay-blue", 1f), ("game:burnedbrick-red", 1f) },
            drops.ConvertAll(d => (d.Code!.ToString(), d.Quantity.avg)));
    }

    [Fact]
    public void DiagonalOuterDeckDropsHalfOfEachLayer()
    {
        var drops = SidingWallBlock.ComputeDrops(null, null, null, null, null, null, Framings, Infills, Finishes, null, layout: "diagonal",
            outerDeck: "oak", outerDeckInfill: "wattle", outerDeckFront: "daub", outerDeckBack: "brick");
        Assert.Equal(
            new[] { ("game:plank-oak", 1f), ("game:stick", 2f), ("game:clay-blue", 1f), ("game:burnedbrick-red", 1f) },
            drops.ConvertAll(d => (d.Code!.ToString(), d.Quantity.avg)));
    }

    [Fact]
    public void DiagonalPeelsInnerDeckThenOuterDeckThenFrame()
    {
        var wall = FootprintHostsTests.Wall("diagonal", "west");
        var entity = new SidingWallEntity { Framing = "oak", Deck = "oak", OuterDeck = "oak", OuterDeckInfill = "wattle", OuterDeckFront = "daub" };

        Assert.Equal("deck", wall.PeelAt(entity, null, false));
        entity.Deck = null;
        Assert.Equal("outerdeckfront", wall.PeelAt(entity, null, false));
        entity.OuterDeckFront = null;
        Assert.Equal("outerdeckinfill", wall.PeelAt(entity, null, false));
        entity.OuterDeckInfill = null;
        Assert.Equal("outerdeck", wall.PeelAt(entity, null, false));
        entity.OuterDeck = null;
        Assert.Null(wall.PeelAt(entity, null, false));
    }

    [Fact]
    public void AnOuterStripHitPeelsTheOuterDeckWhateverTheInnerHolds()
    {
        var wall = FootprintHostsTests.Wall("diagonal", "west");
        var entity = new SidingWallEntity { Framing = "oak", Deck = "oak", DeckFront = "brick", OuterDeck = "oak", OuterDeckBack = "daub" };

        Assert.Equal("outerdeckback", wall.PeelAt(entity, null, true, DeckHalf.Outer));
        Assert.Equal("deckfront", wall.PeelAt(entity, null, true));
    }

    // A west diagonal claims north and west, so a hit there is on the outer side; south and east are the room's.
    [Theory]
    [InlineData("north", "outerdeck")]
    [InlineData("west", "outerdeck")]
    [InlineData("south", "deck")]
    [InlineData(null, "deck")]
    public void APanelHitPeelsTheDeckOnItsOwnSide(string? face, string expected)
    {
        var wall = FootprintHostsTests.Wall("diagonal", "west");
        var entity = new SidingWallEntity { Framing = "oak", Front = "daub", Deck = "oak", OuterDeck = "oak" };

        Assert.Equal(expected, wall.PeelAt(entity, face == null ? null : BlockFacing.FromCode(face), false));
    }

    [Fact]
    public void AFacelessPeelTakesTheOuterDeckBeforeTheWallsLayers()
    {
        var wall = FootprintHostsTests.Wall("diagonal", "west");

        Assert.Equal("outerdeck", wall.PeelAt(new SidingWallEntity { Framing = "oak", Front = "daub", Infill = "wattle", OuterDeck = "oak" }, null, false));
    }

    // Fire catches only on wood (decision 0043): a daub front, or a bone inner deck, leaves the outer deck's planks next.
    [Theory]
    [InlineData(null)]
    [InlineData("bone")]
    public void FireReachesAWoodOuterDeckPastLayersThatDoNotBurn(string? innerDeck)
    {
        var wall = new SidingWallBlock
        {
            Attributes = new JsonObject(JToken.Parse("""{ "Framings": { "oak": { "BlockMaterial": "Wood" }, "bone": { "BlockMaterial": "Bone" } }, "Infills": {}, "Finishes": { "daub": { "BlockMaterial": "Soil" } } }""")),
        };
        wall.VariantStrict["layout"] = "diagonal";
        wall.VariantStrict["side"] = "west";

        Assert.Equal("outerdeck", wall.BurnLayer(new SidingWallEntity { Framing = "oak", Front = "daub", Deck = innerDeck, OuterDeck = "oak" }));
    }

    [Fact]
    public void DropsOfCarriesTheOuterDeckLayers()
    {
        var wall = new SidingWallBlock
        {
            Attributes = new JsonObject(JToken.Parse("""{ "Framings": { "oak": { "Drops": [ { "type": "item", "code": "game:plank-oak" } ] } }, "Infills": {}, "Finishes": { "daub": { "Drops": [ { "type": "item", "code": "game:clay-blue" } ] } } }""")),
        };
        wall.VariantStrict["layout"] = "diagonal";
        var entity = new SidingWallEntity { OuterDeck = "oak", OuterDeckFront = "daub" };
        Assert.Equal(new[] { "game:plank-oak", "game:clay-blue" }, Codes(wall.DropsOf(entity)));
    }

    [Theory]
    [InlineData("diagonal", 4, 2)]
    [InlineData("diagonal", 1, 1)]
    [InlineData("wall", 4, 4)]
    [InlineData("cornerin", 4, 4)]
    public void DeckCostFollowsTheLayout(string layout, int wall, int deck)
        => Assert.Equal(deck, SidingWallBlock.Share(wall, SidingWallBlock.LayerParts(layout, "deck")));

    [Fact]
    public void StepDropsExactlyOneOfTheStoredStair()
    {
        var drops = SidingWallBlock.ComputeDrops(null, null, null, null, null, null, Framings, Infills, Finishes, "game:plankstairs-oak-up-north-free");
        Assert.Equal(
            new[] { (EnumItemClass.Block, "game:plankstairs-oak-up-north-free", 1f, 0f) },
            drops.ConvertAll(d => (d.Type, d.Code!.ToString(), d.Quantity.avg, d.Quantity.var)).ToArray());
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

    [Fact]
    public void DropsKeepTheirAttributes()
    {
        var finishes = Dict("""
        { "red-oak": { "Drops": [ { "type": "block", "code": "dyedwood:planks", "attributes": { "types": { "color": "red", "wood": "oak" } }, "quantity": { "avg": 1, "var": 0 } } ] } }
        """);
        var drops = new List<BlockDropItemStack>();

        SidingWallBlock.AddDrops(drops, "red-oak", finishes);

        Assert.Equal(JToken.Parse("""{ "types": { "color": "red", "wood": "oak" } }"""), Assert.Single(drops).Attributes?.Token);
    }

    // A layer gives back what it took, or peeling one is a recipe. A stained plank goes on in
    // any orientation and comes back as its ns block.
    [Fact]
    public void EveryShippedMaterialDropsWhatItConsumes()
    {
        var attributes = MaterialTextureOpacityTests.BlockAttributes("wall.json");

        var differing = new Dictionary<string, string>();
        foreach (string dictName in new[] { "Framings", "FramingFamilies", "Infills", "InfillFamilies", "Finishes", "FinishFamilies" })
        {
            foreach (var entry in ((JObject)attributes[dictName]!).Properties().Where(entry => entry.Name != SidingWallEntity.TilesKey))
            {
                var returned = (JObject)entry.Value["Consumes"]!.DeepClone();
                returned["quantity"] = new JObject { ["avg"] = returned["quantity"], ["var"] = 0 };
                var drops = entry.Value["Drops"]!;
                if (!JToken.DeepEquals(new JArray(returned), drops)) differing[$"{dictName}.{entry.Name}"] = drops.ToString(Formatting.None);
            }
        }

        Assert.Equal(
            new Dictionary<string, string>
            {
                ["FinishFamilies.stained-{stain}-{wood}"] = """[{"type":"block","code":"woodstain:stainedplanks-{stain}-{wood}-ns","quantity":{"avg":1,"var":0}}]""",
            },
            differing);
    }
}
