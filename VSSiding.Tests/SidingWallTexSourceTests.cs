using Newtonsoft.Json.Linq;
using Vintagestory.API.Datastructures;
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
        "brick": { "Texture": "game:block/clay/brick/four/running/red1" }
    }
    """);

    [Fact]
    public void UnbuiltSlotResolvesToNull()
    {
        string? path = SidingWallTexSource.ResolveTexturePath("framing", null, null, null, null, Framings, Infills, Finishes);
        Assert.Null(path);
    }

    [Fact]
    public void UnknownKeyResolvesToNull()
    {
        string? path = SidingWallTexSource.ResolveTexturePath("infill", null, "uninstalled", null, null, Framings, Infills, Finishes);
        Assert.Null(path);
    }

    [Fact]
    public void EachSlotResolvesFromItsOwnDictionary()
    {
        Assert.Equal("game:block/wood/planks/oak1",
            SidingWallTexSource.ResolveTexturePath("framing", "oak", "wattle", "daub", "brick", Framings, Infills, Finishes));
        Assert.Equal("game:block/wood/wattle",
            SidingWallTexSource.ResolveTexturePath("infill", "oak", "wattle", "daub", "brick", Framings, Infills, Finishes));
        Assert.Equal("game:block/clay/daub/browngolden/normal1",
            SidingWallTexSource.ResolveTexturePath("front", "oak", "wattle", "daub", "brick", Framings, Infills, Finishes));
        Assert.Equal("game:block/clay/brick/four/running/red1",
            SidingWallTexSource.ResolveTexturePath("back", "oak", "wattle", "daub", "brick", Framings, Infills, Finishes));
    }
}
