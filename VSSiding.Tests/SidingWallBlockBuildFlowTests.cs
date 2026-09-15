using Newtonsoft.Json.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
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
}
