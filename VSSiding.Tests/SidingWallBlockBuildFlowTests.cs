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
    [InlineData("cornerout", "west", "south", "back")]
    [InlineData("cornerout", "west", "up", null)]
    [InlineData("cornerout", "north", "east", "secondfront")]
    [InlineData("cornerout", "north", "west", "back")]
    public void ResolveFinishFaceMapsClickedFaceToLayer(string layout, string side, string clicked, string? expected)
    {
        Assert.Equal(expected, SidingWallBlock.ResolveFinishFace(layout, side, BlockFacing.FromCode(clicked)));
    }

    // Every wall is one leg of two possible corners, and the click picks which by which end of
    // the run it landed nearer. The run's axis and its direction both change with `side`, so all
    // four sides are here at both ends: a fixed "coordinate below 0.5" gets east and south wrong.
    [Fact]
    public void CornerUpgradePutsTheNewLegOnTheEndClicked()
    {
        var hits = new Dictionary<(string side, string end), Vec3d>
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
            hits.ToDictionary(hit => hit.Key, hit => SidingWallBlock.ResolveCornerUpgrade(hit.Key.side, hit.Value)));
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
}
