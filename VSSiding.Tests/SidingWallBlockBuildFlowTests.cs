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

    [Fact]
    public void ResolveLayoutZeroIsWall()
    {
        Assert.Equal("wall", SidingWallBlock.ResolveLayout(0));
    }

    [Fact]
    public void ResolveLayoutOneIsCornerout()
    {
        Assert.Equal("cornerout", SidingWallBlock.ResolveLayout(1));
    }

    [Fact]
    public void ResolveLayoutOutOfRangeFallsBackToWall()
    {
        Assert.Equal("wall", SidingWallBlock.ResolveLayout(2));
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
    [InlineData("cornerout", "west", "north", "front")]
    [InlineData("cornerout", "west", "south", "back")]
    [InlineData("cornerout", "west", "up", null)]
    public void ResolveFinishFaceMapsClickedFaceToLayer(string layout, string side, string clicked, string? expected)
    {
        Assert.Equal(expected, SidingWallBlock.ResolveFinishFace(layout, side, BlockFacing.FromCode(clicked)));
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
