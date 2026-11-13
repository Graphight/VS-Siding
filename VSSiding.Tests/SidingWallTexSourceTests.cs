using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using SkiaSharp;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Xunit;

namespace VSSiding.Tests;

public class SidingWallTexSourceTests
{
    private static JsonObject Dict(string json) => new(JToken.Parse(json));

    private static readonly JsonObject Framings = Dict("""
    { "oak": { "Texture": "game:block/wood/planks/oak1" } }
    """);

    private static readonly JsonObject Infills = Dict("""
    { "wattle": { "Texture": "game:block/wood/wattle" } }
    """);

    private static readonly JsonObject Finishes = Dict("""
    {
        "daub": { "Texture": "game:block/clay/daub/browngolden/normal1" },
        "planks": { "Texture": "game:block/wood/planks/aged1" },
        "shakes": {
            "Texture": "game:block/wood/shingles/oak-top",
            "StyleTextures": { "logs": "game:block/wood/debarked/oak" },
            "Elements": { "front": "front-shakes", "back": "back-logs" }
        },
        "brick": { "Texture": "game:block/clay/brick/four/running/red1" },
        "overlay-brick": {
            "Texture": {
                "base": "game:block/clay/brick/four/running/cream1",
                "overlays": ["game:block/clay/brick/four/running/blue1"]
            }
        },
        "no-texture": { },
        "no-base": { "Texture": { "overlays": ["game:block/clay/brick/four/running/blue1"] } },
        "blended-clay": {
            "Texture": {
                "base": "game:block/clay/blueclay",
                "blendedOverlays": [
                    { "base": "game:block/clay/daub/browngolden/normal1", "blendMode": "Overlay" }
                ]
            }
        }
    }
    """);

    // CompositeTexture has no value equality, so tests compare Base plus the blended
    // overlays a consumer would read, rather than the instance itself.
    private static IEnumerable<(AssetLocation Base, EnumColorBlendMode BlendMode)> Overlays(CompositeTexture texture) =>
        texture.BlendedOverlays?.Select(o => (o.Base, o.BlendMode)) ?? [];

    [Fact]
    public void UnbuiltSlotResolvesToNull()
    {
        CompositeTexture? texture = SidingWallTexSource.ResolveTexture("framing", null, null, null, null, null, Framings, Infills, Finishes);
        Assert.Null(texture);
    }

    [Fact]
    public void UnknownKeyResolvesToNull()
    {
        CompositeTexture? texture = SidingWallTexSource.ResolveTexture("infill", null, "uninstalled", null, null, null, Framings, Infills, Finishes);
        Assert.Null(texture);
    }

    [Theory]
    [InlineData("no-texture")]
    [InlineData("no-base")]
    public void MalformedTextureResolvesToNull(string finish)
    {
        CompositeTexture? texture = SidingWallTexSource.ResolveTexture("front", null, null, finish, null, null, Framings, Infills, Finishes);
        Assert.Null(texture);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("oak")]
    [InlineData("sticks")]
    public void LashingResolvesToRopeWhateverTheFraming(string? framing)
    {
        CompositeTexture? texture = SidingWallTexSource.ResolveTexture("lashing", framing, null, null, null, null, Framings, Infills, Finishes);
        Assert.Equal(new AssetLocation("game:item/resource/rope"), texture!.Base);
    }

    [Fact]
    public void DeckResolvesFromTheFramingsDictionaryByItsOwnKey()
    {
        CompositeTexture? texture = SidingWallTexSource.ResolveTexture(
            "deck", "wattle", null, null, null, null, Framings, Infills, Finishes, deck: "oak");
        Assert.Equal(new AssetLocation("game:block/wood/planks/oak1"), texture!.Base);
    }

    [Fact]
    public void DeckLayersResolveFromTheDictionariesTheFloorsDo()
    {
        var actual = new[] { "deckinfill", "deckfront", "deckback" }
            .Select(slot => SidingWallTexSource.ResolveTexture(
                slot, "oak", "wattle", "daub", null, "daub", Framings, Infills, Finishes, deck: "oak",
                deckInfill: "wattle", deckFront: "planks", deckBack: "brick")!.Base);

        Assert.Equal(new[]
        {
            new AssetLocation("game:block/wood/wattle"),
            new AssetLocation("game:block/wood/planks/aged1"),
            new AssetLocation("game:block/clay/brick/four/running/red1"),
        }, actual);
    }

    [Fact]
    public void AnUnbuiltDeckLayerResolvesToNull()
    {
        Assert.Null(SidingWallTexSource.ResolveTexture("deckfront", "oak", "wattle", "daub", null, "daub", Framings, Infills, Finishes, deck: "oak"));
    }

    [Fact]
    public void EachSlotResolvesFromItsOwnDictionary()
    {
        var actual = new[] { "framing", "infill", "front", "secondfront", "back", "secondback" }
            .Select(slot => SidingWallTexSource.ResolveTexture(slot, "oak", "wattle", "daub", "planks", "brick", Framings, Infills, Finishes,
                secondBack: "blended-clay")!.Base);

        Assert.Equal(new[]
        {
            new AssetLocation("game:block/wood/planks/oak1"),
            new AssetLocation("game:block/wood/wattle"),
            new AssetLocation("game:block/clay/daub/browngolden/normal1"),
            new AssetLocation("game:block/wood/planks/aged1"),
            new AssetLocation("game:block/clay/brick/four/running/red1"),
            new AssetLocation("game:block/clay/blueclay"),
        }, actual);
    }

    // A corner's second back takes the back's default, and a post's backs the front's (decisions 0082, 0090).
    [Fact]
    public void StyleTexturesFollowEachSlotsDefaultStyle()
    {
        CompositeTexture Resolve(string slot, bool post = false) => SidingWallTexSource.ResolveTexture(
            slot, "oak", "wattle", "shakes", "shakes", "shakes", Framings, Infills, Finishes, secondBack: "shakes", post: post)!;
        var actual = new[] { Resolve("front"), Resolve("secondfront"), Resolve("back"), Resolve("secondback"), Resolve("secondback", post: true) }
            .Select(texture => texture.Base);

        Assert.Equal(new[]
        {
            new AssetLocation("game:block/wood/shingles/oak-top"),
            new AssetLocation("game:block/wood/shingles/oak-top"),
            new AssetLocation("game:block/wood/debarked/oak"),
            new AssetLocation("game:block/wood/debarked/oak"),
            new AssetLocation("game:block/wood/shingles/oak-top"),
        }, actual);
    }

    // A corner front's strip in the second face's plane shows that face's finish and style, or the front's while it is bare (decision 0090).
    [Fact]
    public void AFrontEdgeShowsTheSecondFrontOrTheFrontWhileItIsBare()
    {
        var actual = new (string Front, string? SecondFront)[] { ("shakes", "planks"), ("shakes", null), ("planks", "shakes") }
            .Select(c => SidingWallTexSource.ResolveTexture("frontedge", "oak", "wattle", c.Front, c.SecondFront, null, Framings, Infills, Finishes,
                frontStyle: c.Front == "shakes" ? "logs" : null, secondFrontStyle: c.SecondFront == "shakes" ? "logs" : null)!.Base);

        Assert.Equal(new[]
        {
            new AssetLocation("game:block/wood/planks/aged1"),
            new AssetLocation("game:block/wood/debarked/oak"),
            new AssetLocation("game:block/wood/debarked/oak"),
        }, actual);
    }

    [Fact]
    public void ExplicitStyleOverridesTheDefaultOnEachSlot()
    {
        var actual = new[] { "front", "secondfront", "back" }
            .Select(slot => SidingWallTexSource.ResolveTexture(
                slot, "oak", "wattle", "shakes", "shakes", "shakes", Framings, Infills, Finishes,
                frontStyle: "logs", secondFrontStyle: "logs", backStyle: "shakes")!.Base);

        Assert.Equal(new[]
        {
            new AssetLocation("game:block/wood/debarked/oak"),
            new AssetLocation("game:block/wood/debarked/oak"),
            new AssetLocation("game:block/wood/shingles/oak-top"),
        }, actual);
    }

    [Fact]
    public void ADecksStyleOverridesTheDefaultOnEachFace()
    {
        var actual = new[] { "deckfront", "deckback" }
            .Select(slot => SidingWallTexSource.ResolveTexture(
                slot, "oak", "wattle", null, null, null, Framings, Infills, Finishes,
                deck: "oak", deckFront: "shakes", deckBack: "shakes", deckFrontStyle: "logs", deckBackStyle: "shakes")!.Base);

        Assert.Equal(new[]
        {
            new AssetLocation("game:block/wood/debarked/oak"),
            new AssetLocation("game:block/wood/shingles/oak-top"),
        }, actual);
    }

    [Fact]
    public void CompositeTextureWithOverlayResolves()
    {
        CompositeTexture? texture = SidingWallTexSource.ResolveTexture(
            "front", "oak", "wattle", "overlay-brick", null, null, Framings, Infills, Finishes);

        // The vanilla "overlays" shorthand becomes a BlendedOverlays entry with the default blend mode.
        Assert.NotNull(texture);
        Assert.Equal(new AssetLocation("game:block/clay/brick/four/running/cream1"), texture!.Base);
        Assert.Equal([(new AssetLocation("game:block/clay/brick/four/running/blue1"), EnumColorBlendMode.Normal)], Overlays(texture));
    }

    [Fact]
    public void CompositeTextureWithBlendedOverlayResolves()
    {
        CompositeTexture? texture = SidingWallTexSource.ResolveTexture(
            "front", "oak", "wattle", "blended-clay", null, null, Framings, Infills, Finishes);

        Assert.NotNull(texture);
        Assert.Equal(new AssetLocation("game:block/clay/blueclay"), texture!.Base);
        Assert.Equal([(new AssetLocation("game:block/clay/daub/browngolden/normal1"), EnumColorBlendMode.Overlay)], Overlays(texture));
    }

    // Variant 0 is the base, then each alternate in turn, wrapping past the last.
    [Fact]
    public void PickAlternateWalksBaseThenAlternates()
    {
        var two = new CompositeTexture(new AssetLocation("game:block/wood/planks/oak2"));
        var three = new CompositeTexture(new AssetLocation("game:block/wood/planks/oak3"));
        var baked = new CompositeTexture(new AssetLocation("game:block/wood/planks/oak1")) { Alternates = [two, three] };

        Assert.Equal(
            new[] { baked, two, three, baked },
            new[] { 0, 1, 2, 3 }.Select(alternate => SidingWallTexSource.PickAlternate(baked, alternate)));
    }

    [Fact]
    public void AStairTextureVariesWhenWildcardOrAlternated()
    {
        var wildcard = new CompositeTexture(new AssetLocation("game:block/wood/planks/oak*"));
        var alternated = new CompositeTexture(new AssetLocation("game:block/wood/planks/oak1")) { Alternates = [new CompositeTexture(new AssetLocation("game:block/wood/planks/oak2"))] };
        var plain = new CompositeTexture(new AssetLocation("game:block/stone/path/normal1"));

        Assert.Equal(
            new[] { true, true, false, false },
            new[] { wildcard, alternated, plain, null }.Select(SidingWallTexSource.Varies));
    }

    [Fact]
    public void AnyVariesForAWildcardFinish()
    {
        var finishes = Dict("""
        { "planks": { "Texture": "game:block/wood/planks/oak*" }, "daub": { "Texture": "game:block/clay/daub/browngolden/normal1" } }
        """);

        Assert.Equal(
            new[] { true, false, false },
            new[] { SidingWallTexSource.AnyVaries(finishes, "daub", "planks"), SidingWallTexSource.AnyVaries(finishes, "daub", null), SidingWallTexSource.AnyVaries(finishes) });
    }

    [Fact]
    public void AnyVariesForAFinishTakenFromABlock()
    {
        var finishes = Dict("""
        { "stained": { "TextureBlock": "woodstain:stainedplanks-red-oak-ns" }, "daub": { "Texture": "game:block/clay/daub/browngolden/normal1" } }
        """);

        Assert.Equal(
            new[] { true, false },
            new[] { SidingWallTexSource.AnyVaries(finishes, "stained"), SidingWallTexSource.AnyVaries(finishes, "daub") });
    }

    [Fact]
    public void TextureBlockResolvesThroughTheBlockLookup()
    {
        var finishes = Dict("""
        { "stained": { "TextureBlock": "woodstain:stainedplanks-red-oak-ns", "Texture": "game:block/wood/planks/oak1" } }
        """);
        var blockTexture = new CompositeTexture(new AssetLocation("woodstain:block/wood/stain/red-oak"));
        var asked = new List<AssetLocation>();

        CompositeTexture? resolved = SidingWallTexSource.ResolveTexture(
            "front", null, null, "stained", null, null, Framings, Infills, finishes,
            blockTexture: code => { asked.Add(code); return blockTexture; });

        Assert.Same(blockTexture, resolved);
        Assert.Equal([new AssetLocation("woodstain:stainedplanks-red-oak-ns")], asked);
    }

    private static CompositeTexture Turned(string path, int rotation) => new(new AssetLocation(path)) { Rotation = rotation };

    // What a client holds for woodstain:stainedplanks-red-aged-ns, read from its log.
    [Fact]
    public void SideTextureIsASideFaceNotTheFirstTexture()
    {
        var side = Turned("game:block/wood/planks/aged/aged1", 0);
        var pillar = new Dictionary<string, CompositeTexture>
        {
            ["verticals"] = Turned("game:block/wood/planks/aged/aged1", 90),
            ["up"] = Turned("game:block/wood/planks/aged/aged1", 90),
            ["north"] = side,
            ["all"] = Turned("game:unknown", 0),
        };
        var faceless = new Dictionary<string, CompositeTexture> { ["wood"] = side };

        Assert.Equal(
            new CompositeTexture?[] { side, side, null, null },
            new[]
            {
                SidingWallTexSource.SideTexture(pillar), SidingWallTexSource.SideTexture(faceless),
                SidingWallTexSource.SideTexture(new Dictionary<string, CompositeTexture>()), SidingWallTexSource.SideTexture(null),
            });
    }

    [Fact]
    public void AStyleTextureWinsOverTextureBlock()
    {
        var finishes = Dict("""
        { "stained": { "TextureBlock": "woodstain:stainedplanks-red-oak-ns", "StyleTextures": { "logs": "game:block/wood/debarked/oak" } } }
        """);

        CompositeTexture? resolved = SidingWallTexSource.ResolveTexture(
            "front", null, null, "stained", null, null, Framings, Infills, finishes, frontStyle: "logs",
            blockTexture: _ => new CompositeTexture(new AssetLocation("woodstain:block/wood/stain/red-oak")));

        Assert.Equal(new AssetLocation("game:block/wood/debarked/oak"), resolved?.Base);
    }

    [Fact]
    public void TextureBlockWithoutALookupResolvesToNull()
    {
        var finishes = Dict("""
        { "stained": { "TextureBlock": "woodstain:stainedplanks-red-oak-ns" } }
        """);

        Assert.Null(SidingWallTexSource.ResolveTexture("front", null, null, "stained", null, null, Framings, Infills, finishes));
    }

    // An infill's Elements names its groups, not a style, so the bamboo pole infill draws the bamboo frame's texture, as a deck too.
    [Fact]
    public void BambooPoleInfillDrawsTheBambooFramesTexture()
    {
        var attributes = MaterialTextureOpacityTests.BlockAttributes("wall.json");
        var bamboo = new AssetLocation("game:block/wood/planks/bamboo-side");

        Assert.Equal(
            new[] { bamboo, bamboo, bamboo },
            new[] { "framing", "infill", "deckinfill" }.Select(slot => SidingWallTexSource.ResolveTexture(
                slot, "bamboo", "bamboopoles", null, null, null, new JsonObject(attributes["Framings"]), new JsonObject(attributes["Infills"]),
                new JsonObject(attributes["Finishes"]), deck: "bamboo", deckInfill: "bamboopoles")?.Base));
    }

    // A cell's framing and plank finish share one alternate, so they must name the same variants.
    [Fact]
    public void PlankFramingDrawsTheSameTexturesAsItsFinish()
    {
        var attributes = MaterialTextureOpacityTests.BlockAttributes("wall.json");
        string? Texture(string dictionary, string key) => (string?)attributes[dictionary]![key]!["Texture"];

        Assert.Equal(
            new[] { Texture("Finishes", "planks"), Texture("Finishes", "planks-veryaged"), Texture("FinishFamilies", "planks-{wood}") },
            new[] { Texture("Framings", "oak"), Texture("Framings", "veryaged"), Texture("FramingFamilies", "{wood}") });
    }

    private static readonly JsonObject TileFinishes = Dict("""
    {
        "tiles": { },
        "tile-a": { "Tile": true, "Texture": "game:block/clay/tile/2x2/plain/blue*" },
        "tile-b": { "Tile": true, "Texture": "game:block/clay/tile/2x2/plain/red*" }
    }
    """);

    private static string? Tile(string slot, string? front = "tiles", string? secondFront = null, string? frontStyle = "tile-a,,tile-b,", string? secondFrontStyle = null)
        => SidingWallTexSource.ResolveTexture(slot, null, null, front, secondFront, null, Framings, Infills, TileFinishes,
            frontStyle, secondFrontStyle)?.Base.ToString();

    [Fact]
    public void ATileSlotTakesTheTextureOfTheKindItHolds()
    {
        Assert.Equal(["game:block/clay/tile/2x2/plain/blue*", null, "game:block/clay/tile/2x2/plain/red*", null],
            new[] { "fronttile0", "fronttile1", "fronttile2", "fronttile3" }.Select(slot => Tile(slot)));
        Assert.Null(Tile("front"));
    }

    [Fact]
    public void AFrontEdgeTileTakesTheSecondFrontsTileElseTheFronts()
    {
        Assert.Equal("game:block/clay/tile/2x2/plain/blue*", Tile("frontedgetile0"));
        Assert.Equal("game:block/clay/tile/2x2/plain/red*", Tile("frontedgetile0", secondFront: "tiles", secondFrontStyle: "tile-b,,,"));
    }

    [Fact]
    public void ATileKindsVaryingTextureVariesTheCell()
    {
        var attributes = Dict("""{ "Framings": {}, "Infills": {}, "Finishes": { "tiles": {}, "tile-a": { "Texture": "game:block/clay/tile/2x2/plain/blue*" } } }""");

        // Only a tiled face's style lists tile kinds; another face's names its look.
        Assert.Equal(
            (77, 0, 0),
            (SidingWallEntity.CellAlternate(attributes, 77, null, [], [], ["tiles"], styles: ["tile-a,,,"]),
             SidingWallEntity.CellAlternate(attributes, 77, null, [], [], ["tiles"], styles: [",,,"]),
             SidingWallEntity.CellAlternate(attributes, 77, null, [], [], ["daub"], styles: ["tile-a"])));
    }

    [Fact]
    public void RollingAClayTileTextureRightPutsEachTilesGroutOnItsFirstColumnAsItsRowsAlreadyAre()
    {
        var textures = Path.Combine(MaterialTextureOpacityTests.GetAssemblyMetadata("VintageStoryPath"), "assets", "survival", "textures");
        var files = Directory.GetFiles(Path.Combine(textures, "block", "clay", "tile"), "*.png", SearchOption.AllDirectories).Order().ToList();
        Assert.NotEmpty(files);

        var groutAt = files.ToDictionary(file => Path.GetRelativePath(textures, file), file =>
        {
            // From bytes, as NoMaterialTextureHasPartialAlpha does since #120: SKBitmap.Decode(path) now and then gave null.
            using var codec = SKCodec.Create(new MemoryStream(File.ReadAllBytes(file)), out var result);
            using var bitmap = codec == null ? null : SKBitmap.Decode(codec);
            if (bitmap == null) return $"did not decode ({result})";
            var rolled = SidingWallTexSource.RollRight(Array.ConvertAll(bitmap.Pixels, p => (int)(uint)p), bitmap.Width);
            // The grout texel is a neutral grey; per tile, the column and the row nearest it all along are the joint.
            int Off(int x, int y)
            {
                var c = new SKColor((uint)rolled[y * bitmap.Width + x]);
                return Math.Abs(c.Red - 99) + Math.Abs(c.Green - 98) + Math.Abs(c.Blue - 97);
            }
            int Nearest(int start, System.Func<int, int, int> at)
                => Enumerable.Range(start, 16).MinBy(line => Enumerable.Range(0, 32).Sum(along => at(line, along)));
            int[] Lines(System.Func<int, int, int> at) => [Nearest(0, at), Nearest(16, at)];
            return $"columns {string.Join(",", Lines((x, y) => Off(x, y)))}, rows {string.Join(",", Lines((y, x) => Off(x, y)))}";
        });

        Assert.Equal(files.ToDictionary(file => Path.GetRelativePath(textures, file), _ => "columns 0,16, rows 0,16"), groutAt);
    }

    [Fact]
    public void OnlyVanillasClayTileTexturesRoll()
    {
        Assert.Equal(
            [true, false, false],
            new[] { "game:block/clay/tile/2x2/plain/blue1", "game:block/stone/tiles/travertine1", "game:block/wood/planks/oak1" }
                .Select(code => SidingWallTexSource.RollsRight(new AssetLocation(code))));
    }

    [Fact]
    public void ARolledTextureKeepsItsColoursInTheBgraBytesTheAtlasUploads()
    {
        var source = new SKBitmap(new SKImageInfo(2, 1, SKColorType.Bgra8888, SKAlphaType.Unpremul));
        source.SetPixel(0, 0, SKColors.Red);
        source.SetPixel(1, 0, SKColors.Blue);

        Assert.Equal(new byte[] { 255, 0, 0, 255, 0, 0, 255, 255 }, SidingWallTexSource.RolledBitmap(new BitmapExternal(source)).bmp.Bytes);
    }
}
