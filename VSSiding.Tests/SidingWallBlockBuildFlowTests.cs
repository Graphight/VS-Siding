using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Xunit;

namespace VSSiding.Tests;

public class SidingWallBlockBuildFlowTests
{
    private static JsonObject Dict(string json) => new(JToken.Parse(json));

    private static readonly JsonObject Framings = Dict("""
    {
        "oak": { "Consumes": { "type": "item", "code": "game:plank-oak", "quantity": 2 } },
        "nocodeframe": { "Consumes": { "type": "item", "quantity": 2 } }
    }
    """);

    private static readonly JsonObject DyedFinishes = Dict("""
    {
        "red-oak": { "Consumes": { "type": "block", "code": "dyedwood:planks", "attributes": { "types": { "color": "red", "wood": "oak" } } } },
        "red-acacia": { "Consumes": { "type": "block", "code": "dyedwood:planks", "attributes": { "types": { "color": "red", "wood": "acacia" } } } },
        "clay": { "Consumes": { "type": "item", "code": "game:clay-blue" } },
        "malformed": { "Consumes": { "type": "item", "code": "game:stick", "attributes": "red" } }
    }
    """);

    private static TreeAttribute Held(IAttribute types)
    {
        var held = new TreeAttribute();
        held["types"] = types;
        return held;
    }

    private static TreeAttribute Types(params (string Key, string Value)[] entries)
    {
        var types = new TreeAttribute();
        foreach (var (key, value) in entries) types.SetString(key, value);
        return Held(types);
    }

    [Fact]
    public void ConsumesAttributesPickTheEntryTheStackCarries()
    {
        var dyed = new AssetLocation("dyedwood:planks");

        var redOak = Types(("color", "red"), ("wood", "oak"));

        var keys = new[]
        {
            SidingWallBlock.MatchConsumes(dyed, DyedFinishes, redOak),
            SidingWallBlock.MatchConsumes(dyed, DyedFinishes, Types(("color", "red"), ("wood", "acacia"))),
            SidingWallBlock.MatchConsumes(dyed, DyedFinishes, Types(("color", "red"), ("wood", "oak"), ("grain", "fine"))),
            SidingWallBlock.MatchConsumes(dyed, DyedFinishes, Types(("color", "red"))),
            SidingWallBlock.MatchConsumes(dyed, DyedFinishes, Types()),
            SidingWallBlock.MatchConsumes(dyed, DyedFinishes, Held(new StringAttribute("red"))),
            SidingWallBlock.MatchConsumes(dyed, DyedFinishes, null),
            SidingWallBlock.MatchConsumes(dyed, DyedFinishes, new TreeAttribute()),
            SidingWallBlock.MatchConsumes(new AssetLocation("game:clay-blue"), DyedFinishes, redOak),
            SidingWallBlock.MatchConsumes(new AssetLocation("game:stick"), DyedFinishes, redOak),
        };

        Assert.Equal(new string?[] { "red-oak", "red-acacia", "red-oak", null, null, null, null, null, "clay", null }, keys);
    }

    [Theory]
    [InlineData("game:saw-copper", true)]
    [InlineData("game:stone-granite", true)]
    [InlineData("game:stick", false)]
    [InlineData(null, false)]
    public void ASawOrAStoneInTheOffHandIsTheBuildSignal(string? offhandCode, bool expected)
    {
        Assert.Equal(expected, SidingWallBlock.IsBuildSignal(offhandCode == null ? null : new AssetLocation(offhandCode)));
    }

    [Fact]
    public void MatchConsumesFindsExactCodeMatch()
    {
        string? key = SidingWallBlock.MatchConsumes(new AssetLocation("game:plank-oak"), Framings);
        Assert.Equal("oak", key);
    }

    [Fact]
    public void MatchConsumesReturnsNullWhenNothingMatches()
    {
        string? key = SidingWallBlock.MatchConsumes(new AssetLocation("game:plank-pine"), Framings);
        Assert.Null(key);
    }

    [Fact]
    public void MatchConsumesSkipsEntriesWithNoCode()
    {
        string? key = SidingWallBlock.MatchConsumes(new AssetLocation("game:stick"), Framings);
        Assert.Null(key);
    }

    [Fact]
    public void ConsumeQuantityReadsQuantity()
    {
        int quantity = SidingWallBlock.ConsumeQuantity(Framings["oak"]["Consumes"]);
        Assert.Equal(2, quantity);
    }

    [Fact]
    public void ConsumeQuantityDefaultsToOne()
    {
        int quantity = SidingWallBlock.ConsumeQuantity(Dict("{}"));
        Assert.Equal(1, quantity);
    }

    // A picked style only applies to a finish that lists it, so a masonry entry falls back to its
    // default rather than asking its shape for a plank element it hasn't got.
    [Fact]
    public void OnlyAFinishListingAStyleOffersIt()
    {
        var planks = Dict("""{ "Styles": ["weatherboard", "boards", "hboards"] }""");
        var daub = Dict("{}");
        Assert.Equal(
            new[] { true, true, true, false, false, false, false },
            new[]
            {
                SidingWallBlock.HasStyle(planks, "weatherboard"),
                SidingWallBlock.HasStyle(planks, "boards"),
                SidingWallBlock.HasStyle(planks, "hboards"),
                SidingWallBlock.HasStyle(planks, "shakes"),
                SidingWallBlock.HasStyle(daub, "weatherboard"),
                SidingWallBlock.HasStyle(daub, "boards"),
                SidingWallBlock.HasStyle(daub, "hboards"),
            });
    }

    private static readonly JsonObject StickFramings = Dict("""
    {
        "oak": { "Consumes": { "type": "item", "code": "game:plank-oak", "quantity": 2 } },
        "sticks": { "Consumes": { "type": "item", "code": "game:stick", "quantity": 4 } }
    }
    """);

    private static readonly JsonObject Infills = Dict("""
    {
        "wattle": { "Consumes": { "type": "item", "code": "game:stick", "quantity": 4 } }
    }
    """);

    [Fact]
    public void MatchConsumesFindsInfillMatch()
    {
        string? key = SidingWallBlock.MatchConsumes(new AssetLocation("game:stick"), Infills);
        Assert.Equal("wattle", key);
    }

    [Fact]
    public void MatchConsumesReturnsNullForNonMatchingInfill()
    {
        string? key = SidingWallBlock.MatchConsumes(new AssetLocation("game:drygrass"), Infills);
        Assert.Null(key);
    }

    [Theory]
    [InlineData("game:stick", false, null)]
    [InlineData("game:stick", true, "sticks")]
    [InlineData("game:plank-oak", false, "oak")]
    [InlineData("game:plank-oak", true, "oak")]
    public void AnItemThatIsAlsoAnInfillFramesOnlyAFilledWall(string held, bool filled, string? expected)
    {
        Assert.Equal(expected, SidingWallBlock.MatchFraming(new AssetLocation(held), filled, StickFramings, Infills));
    }

    [Theory]
    [InlineData("game:stick", false, true)]
    [InlineData("game:stick", true, false)]
    [InlineData("game:clay-blue", false, false)]
    [InlineData("game:plank-oak", false, true)]
    public void AFrameAndFillItemFramesOnFromAFaceThatTakesNoFinish(string held, bool finishFace, bool expected)
    {
        Assert.Equal(expected, SidingWallBlock.FramesOnFrom(new AssetLocation(held), finishFace, StickFramings));
    }

    [Theory]
    [InlineData("wall", "game:stick", "vssiding:tooltip-fill-hint-wall")]
    [InlineData("cornerout", "game:stick", "vssiding:tooltip-fill-hint-corner")]
    [InlineData("diagonal", "game:stick", "vssiding:tooltip-fill-hint-corner")]
    [InlineData("floor", "game:stick", "vssiding:tooltip-fill-hint-floor")]
    [InlineData("cornerin", "game:stick", null)]
    [InlineData("wall", "game:plank-oak", null)]
    [InlineData("wall", "game:clay-blue", null)]
    [InlineData("wall", null, null)]
    public void FillHintKeyNamesTheRuleForAFrameAndFillItemOnly(string layout, string? held, string? expected)
    {
        Assert.Equal(expected, SidingWallBlock.FillHintKey(layout, held == null ? null : new AssetLocation(held), StickFramings, Infills));
    }

    private static readonly JsonObject Finishes = Dict("""
    {
        "daub": { "Consumes": { "type": "item", "code": "game:clay-blue", "quantity": 2 } }
    }
    """);

    [Fact]
    public void MatchConsumesFindsFinishMatch()
    {
        string? key = SidingWallBlock.MatchConsumes(new AssetLocation("game:clay-blue"), Finishes);
        Assert.Equal("daub", key);
    }

    [Fact]
    public void MatchConsumesReturnsNullForNonMatchingFinish()
    {
        string? key = SidingWallBlock.MatchConsumes(new AssetLocation("game:burnedbrick-red"), Finishes);
        Assert.Null(key);
    }

    [Theory]
    [InlineData("wall", "west", "west", "front")]
    [InlineData("wall", "west", "east", "back")]
    [InlineData("wall", "west", "north", null)]
    [InlineData("wall", "west", "up", null)]
    [InlineData("cornerout", "west", "west", "front")]
    [InlineData("cornerout", "west", "east", "back")]
    [InlineData("cornerout", "west", "north", "secondfront")]
    [InlineData("cornerout", "west", "south", "secondback")]
    [InlineData("cornerout", "west", "up", null)]
    [InlineData("cornerout", "north", "east", "secondfront")]
    [InlineData("cornerout", "north", "west", "secondback")]
    [InlineData("diagonal", "west", "west", "front")]
    [InlineData("diagonal", "west", "north", "front")]
    [InlineData("diagonal", "west", "east", "back")]
    [InlineData("diagonal", "west", "south", "back")]
    [InlineData("diagonal", "west", "up", null)]
    [InlineData("diagonal", "north", "east", "front")]
    [InlineData("diagonal", "north", "west", "back")]
    [InlineData("cornerin", "west", "east", "front")]
    [InlineData("cornerin", "west", "south", "secondfront")]
    [InlineData("cornerin", "west", "west", "back")]
    [InlineData("cornerin", "west", "north", "secondback")]
    [InlineData("cornerin", "west", "up", null)]
    [InlineData("cornerin", "north", "south", "front")]
    [InlineData("cornerin", "north", "west", "secondfront")]
    public void ResolveFinishFaceMapsClickedFaceToLayer(string layout, string side, string clicked, string? expected)
    {
        Assert.Equal(expected, SidingWallBlock.ResolveFinishFace(layout, side, BlockFacing.FromCode(clicked)));
    }

    [Theory]
    [InlineData("wall", "west", "east", true)]
    [InlineData("wall", "west", "west", false)]
    [InlineData("cornerout", "west", "north", false)]
    [InlineData("cornerout", "west", "east", true)]
    [InlineData("cornerout", "west", "south", true)]
    [InlineData("cornerin", "west", "east", true)]
    [InlineData("cornerin", "west", "south", true)]
    [InlineData("cornerin", "west", "west", false)]
    [InlineData("cornerin", "west", "north", false)]
    [InlineData("cornerin", "west", "up", false)]
    public void HostsClickTakesAWallsBackOrEitherOfAPostsFaces(string layout, string side, string clicked, bool expected)
    {
        Assert.Equal(expected, SidingWallBlock.HostsClick(layout, side, BlockFacing.FromCode(clicked)));
    }

    // Diagonal plaster laid across a corner's edge from itself takes the other turn; stripes, another finish, a face
    // that is not the partner and a post keep the picked style, which a null answer leaves in place (decision 0090).
    [Theory]
    [InlineData("cornerout", "secondfront", "plaster-diagonal", "front", "plaster-diagonal", "boards", "hboards")]
    [InlineData("cornerout", "front", "plaster-diagonal", "secondfront", "plaster-diagonal", "hboards", "boards")]
    [InlineData("cornerout", "secondback", "plaster-diagonal", "back", "plaster-diagonal", null, "boards")]
    [InlineData("cornerout", "back", "plaster-diagonal", "secondback", "plaster-diagonal", "boards", "hboards")]
    [InlineData("cornerout", "secondfront", "plaster-diagonal", "back", "plaster-diagonal", "boards", null)]
    [InlineData("cornerout", "secondfront", "plaster-diagonal", "front", "plaster-stripes", "boards", null)]
    [InlineData("cornerout", "secondfront", "plaster-stripes", "front", "plaster-stripes", "boards", null)]
    [InlineData("cornerin", "secondfront", "plaster-diagonal", "front", "plaster-diagonal", "boards", null)]
    public void MirroredStyleTurnsDiagonalPlasterAgainstItsPartnerAcrossACorner(
        string layout, string face, string finishKey, string onFace, string onKey, string? onStyle, string? expected)
    {
        var finishes = new JsonObject(MaterialTextureOpacityTests.BlockAttributes("wall.json")["Finishes"]!);
        var entity = new SidingWallEntity();
        switch (onFace)
        {
            case "front": (entity.Front, entity.FrontStyle) = (onKey, onStyle); break;
            case "secondfront": (entity.SecondFront, entity.SecondFrontStyle) = (onKey, onStyle); break;
            case "back": (entity.Back, entity.BackStyle) = (onKey, onStyle); break;
            case "secondback": (entity.SecondBack, entity.SecondBackStyle) = (onKey, onStyle); break;
        }

        Assert.Equal(expected, SidingWallBlock.MirroredStyle(layout, face, entity, finishes, finishKey));
    }

    // Every wall is one leg of two possible corners, and the click picks which by which end of
    // the run it landed nearer. The run's axis and its direction both change with `side`, so all
    // four sides are here at both ends: a fixed "coordinate below 0.5" gets east and south wrong.
    private static readonly Dictionary<(string side, string end), Vec3d> EndHits = new()
    {
        [("west", "north")] = new(0, 0.5, 0.2),
        [("west", "south")] = new(0, 0.5, 0.8),
        [("east", "north")] = new(1, 0.5, 0.2),
        [("east", "south")] = new(1, 0.5, 0.8),
        [("north", "west")] = new(0.2, 0.5, 0),
        [("north", "east")] = new(0.8, 0.5, 0),
        [("south", "west")] = new(0.2, 0.5, 1),
        [("south", "east")] = new(0.8, 0.5, 1),
    };

    [Fact]
    public void CornerUpgradePutsTheNewLegOnTheEndClicked()
    {
        // Each value is the cornerout whose two legs are the wall's own face plus the end clicked.
        Assert.Equal(
            new Dictionary<(string, string), string>
            {
                [("west", "north")] = "west",
                [("west", "south")] = "south",
                [("east", "north")] = "north",
                [("east", "south")] = "east",
                [("north", "west")] = "west",
                [("north", "east")] = "north",
                [("south", "west")] = "south",
                [("south", "east")] = "east",
            },
            EndHits.ToDictionary(hit => hit.Key, hit => SidingWallBlock.ResolveCornerUpgrade(hit.Key.side, hit.Value)));
    }

    // Only the cornerout on the run's right end has the wall's old face as its second face, so only
    // there does the old front finish change slot.
    [Fact]
    public void FilledWallUpgradeMovesTheFrontFinishOnlyWhenItsFaceBecomesTheSecond()
    {
        Assert.Equal(
            new Dictionary<(string, string), bool>
            {
                [("west", "north")] = false,
                [("west", "south")] = true,
                [("east", "north")] = true,
                [("east", "south")] = false,
                [("north", "west")] = true,
                [("north", "east")] = false,
                [("south", "west")] = false,
                [("south", "east")] = true,
            },
            EndHits.ToDictionary(
                hit => hit.Key,
                hit => SidingWallBlock.FrontMovesToSecondFace(hit.Key.side, SidingWallBlock.ResolveCornerUpgrade(hit.Key.side, hit.Value))));
    }

    // A wall takes either, a cornerout only a diagonal, on its own side; nothing turns back and a
    // pick that is not a corner or diagonal upgrades nothing.
    [Fact]
    public void FramingUpgradeOnlyTurnsAWallOrCorneroutForward()
    {
        var hit = new Vec3d(0, 0.5, 0.8);
        var expected = new Dictionary<(string Layout, string Picked), string?>
        {
            [("wall", "wall")] = null,
            [("wall", "cornerout")] = "wall-cornerout-south",
            [("wall", "diagonal")] = "wall-diagonal-south",
            [("wall", "floor")] = null,
            [("cornerout", "wall")] = null,
            [("cornerout", "cornerout")] = null,
            [("cornerout", "diagonal")] = "wall-diagonal-west",
            [("diagonal", "wall")] = null,
            [("diagonal", "cornerout")] = null,
            [("diagonal", "diagonal")] = null,
        };

        Assert.Equal(expected, expected.Keys.ToDictionary(c => c, c => SidingWallBlock.ResolveFramingUpgrade(c.Layout, "west", c.Picked, hit)));
    }

    [Fact]
    public void CanAffordIsTrueWhenStackCoversQuantity()
    {
        Assert.True(SidingWallBlock.CanAfford(false, 2, Framings["oak"]["Consumes"]));
    }

    [Fact]
    public void CanAffordIsFalseWhenStackFallsShort()
    {
        Assert.False(SidingWallBlock.CanAfford(false, 1, Framings["oak"]["Consumes"]));
    }

    [Fact]
    public void CanAffordIsTrueInCreativeRegardlessOfStack()
    {
        Assert.True(SidingWallBlock.CanAfford(true, 0, Framings["oak"]["Consumes"]));
    }

    [Theory]
    [InlineData("wall", "west", "NORTH", 0.5, 0.5, "west")]
    [InlineData("cornerout", "west", "WEST", 0.5, 0.5, "west")]
    [InlineData("cornerout", "west", "EAST", 0.5, 0.5, "west")]
    [InlineData("cornerout", "west", "NORTH", 0.5, 0.5, "north")]
    [InlineData("cornerout", "west", "SOUTH", 0.5, 0.5, "north")]
    [InlineData("cornerout", "south", "SOUTH", 0.5, 0.5, "south")]
    [InlineData("cornerout", "south", "NORTH", 0.5, 0.5, "south")]
    [InlineData("cornerout", "south", "EAST", 0.5, 0.5, "west")]
    [InlineData("cornerout", "east", "WEST", 0.5, 0.5, "east")]
    [InlineData("cornerout", "east", "NORTH", 0.5, 0.5, "south")]
    [InlineData("cornerout", "north", "EAST", 0.5, 0.5, "east")]
    [InlineData("cornerout", "north", "SOUTH", 0.5, 0.5, "north")]
    [InlineData("cornerout", "west", "DOWN", 0.1, 0.9, "west")]
    [InlineData("cornerout", "west", "DOWN", 0.9, 0.1, "north")]
    [InlineData("cornerout", "south", "DOWN", 0.9, 0.9, "south")]
    [InlineData("cornerout", "south", "DOWN", 0.1, 0.1, "west")]
    public void AStepContinuesTheLegOnTheClickedFacesAxisOrNearestTheHitBelow(string layout, string side, string clickedFace, double hitX, double hitZ, string expected)
    {
        Assert.Equal(
            expected,
            SidingWallBlock.ResolveStepLeg(layout, side, BlockFacing.FromFirstLetter(clickedFace[..1]), new Vec3d(hitX, 0, hitZ)));
    }

    // Hits at (x, z) = (.25, .25), (.25, .75), (.75, .25), (.75, .75): a front or back reads its column from the unrotated
    // z axis, a corner's second leg from the unrotated x axis, and the row is always the hit's height.
    [Theory]
    [InlineData("west", "front", "0,1,0,1")]
    [InlineData("west", "secondfront", "0,0,1,1")]
    [InlineData("south", "back", "0,0,1,1")]
    [InlineData("south", "secondback", "1,0,1,0")]
    [InlineData("east", "front", "1,0,1,0")]
    [InlineData("east", "secondfront", "1,1,0,0")]
    [InlineData("north", "back", "1,1,0,0")]
    [InlineData("north", "secondback", "0,1,0,1")]
    public void TileSlotReadsTheColumnOffTheUnrotatedAxis(string side, string face, string columns)
    {
        var hits = new[] { (0.25, 0.25), (0.25, 0.75), (0.75, 0.25), (0.75, 0.75) };
        Assert.Equal(columns, string.Join(",", hits.Select(hit => SidingWallBlock.TileSlot("cornerout", side, face, new Vec3d(hit.Item1, 0.25, hit.Item2)))));
        Assert.Equal(columns, string.Join(",", hits.Select(hit => SidingWallBlock.TileSlot("cornerout", side, face, new Vec3d(hit.Item1, 0.75, hit.Item2)) - 2)));
    }

    [Fact]
    public void APostFaceOnlyHasItsTwoColumnZeroSlots()
    {
        var hits = new[] { new Vec3d(0.1, 0.2, 0.1), new Vec3d(0.9, 0.2, 0.9), new Vec3d(0.1, 0.8, 0.9), new Vec3d(0.9, 0.8, 0.1) };
        Assert.Equal([0, 0, 2, 2], hits.Select(hit => SidingWallBlock.TileSlot("cornerin", "west", "front", hit)));
    }

    [Fact]
    public void ABreakTakesTheSlotUnderTheCrosshairElseTheFirstTile()
    {
        Assert.Equal([2, 0, 2, -1], new[] { SidingWallBlock.TileToBreak("a,,b,", 2), SidingWallBlock.TileToBreak("a,,b,", 0), SidingWallBlock.TileToBreak(",,b,", 1), SidingWallBlock.TileToBreak(",,,", 1) });
    }

    [Fact]
    public void AnUpgradedFacesTilesSwapColumnsToStayWhereTheyWere()
    {
        Assert.Equal("b,a,d,c", SidingWallEntity.TurnTiles("tiles", "a,b,c,d"));
        Assert.Equal("a,,b,", SidingWallEntity.TurnTiles("daub", "a,,b,"));
        Assert.Equal(",a,,b", SidingWallEntity.TurnTiles("tiles", "a,,b,"));
    }

    [Fact]
    public void ABreakWithNoPlayerDropsEveryTileOnATiledFace()
    {
        var finishes = Dict("""
        {
            "tiles": { },
            "tile-a": { "Tile": true, "Drops": [ { "type": "item", "code": "game:claytile-fired-plain-blue", "quantity": { "avg": 1, "var": 0 } } ] },
            "tile-b": { "Tile": true, "Drops": [ { "type": "item", "code": "game:claytile-fired-plain-red", "quantity": { "avg": 1, "var": 0 } } ] }
        }
        """);
        var drops = SidingWallBlock.ComputeDrops("oak", null, "tiles", null, null, null, Framings, Dict("{}"), finishes, tileLists: ["tile-a,,tile-b,", "tile-a,,,"]);
        Assert.Equal(["game:claytile-fired-plain-blue", "game:claytile-fired-plain-red", "game:claytile-fired-plain-blue"], drops.Select(d => d.Code.ToString()).Where(c => c.Contains("claytile")));
    }

    [Fact]
    public void ATileItemMatchesOnlyAsATile()
    {
        var finishes = Dict("""{ "tile-a": { "Tile": true, "Consumes": { "type": "item", "code": "game:claytile-fired-plain-blue" } }, "clay": { "Consumes": { "type": "item", "code": "game:clay-blue" } } }""");
        var held = new AssetLocation("game:claytile-fired-plain-blue");
        Assert.Equal(("tile-a", null), (SidingWallBlock.MatchConsumes(held, finishes, null, true), SidingWallBlock.MatchConsumes(held, finishes)));
    }

    [Theory]
    [InlineData("west", "down-south", 0, 0, "DOWN", 0.5, true, "down-south")]
    [InlineData("west", "up-east", -1, 0, "NORTH", 0.5, true, "up-south")]
    [InlineData("west", null, 0, -1, "NORTH", 0.5, true, "up-north")]
    [InlineData("west", null, 0, 1, "NORTH", 0.5, true, "up-south")]
    [InlineData("north", null, -1, 0, "NORTH", 0.5, true, "up-west")]
    [InlineData("north", null, 1, 0, "NORTH", 0.5, true, "up-east")]
    [InlineData("west", null, 0, -1, "DOWN", 0.5, true, "down-north")]
    [InlineData("west", null, 0, -1, "NORTH", 0.7, true, "down-north")]
    [InlineData("west", null, 0, -1, "NORTH", 0.3, true, "up-north")]
    [InlineData("west", null, 0, -1, "NORTH", 0.7, false, "up-north")]
    [InlineData("west", null, 0, -1, "DOWN", 0.5, false, "up-north")]
    [InlineData("west", "down-south", 0, 0, "NORTH", 0.5, false, "up-south")]
    public void ResolveStepOrientation(
        string side, string? neighbourOrientation, float lookX, float lookZ, string clickedFace, double hitY, bool hasDownVariant, string expected)
    {
        Assert.Equal(
            expected,
            SidingWallBlock.ResolveStepOrientation(
                side, neighbourOrientation, new Vec3f(lookX, 0, lookZ), BlockFacing.FromFirstLetter(clickedFace[..1]), hitY, hasDownVariant));
    }

    // A west diagonal claims north and west; a deck click there adds the outer half, anywhere else (or on another layout) the inner.
    [Fact]
    public void ADeckClickAddsTheHalfOnTheClickedSide()
    {
        string[] faces = ["north", "east", "south", "west", "up"];

        Assert.Equal(
            [DeckHalf.Outer, DeckHalf.Inner, DeckHalf.Inner, DeckHalf.Outer, DeckHalf.Inner, DeckHalf.Inner, DeckHalf.Inner],
            [.. faces.Select(face => SidingWallBlock.HalfFacing("diagonal", "west", face)),
             SidingWallBlock.HalfFacing("cornerout", "west", "west"), SidingWallBlock.HalfFacing("wall", "west", "west")]);
    }
}
