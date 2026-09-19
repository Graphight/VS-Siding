using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
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
        "brick": { "Texture": "game:block/clay/brick/four/running/red1" },
        "overlay-brick": {
            "Texture": {
                "base": "game:block/clay/brick/four/running/cream1",
                "overlays": ["game:block/clay/brick/four/running/blue1"]
            }
        },
        "no-texture": { },
        "no-base": { "Texture": { "overlays": ["game:block/clay/brick/four/running/blue1"] } },
        "blended-daub": {
            "Texture": {
                "base": "game:block/clay/browngoldenclay",
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

    [Fact]
    public void EachSlotResolvesFromItsOwnDictionary()
    {
        Assert.Equal(new AssetLocation("game:block/wood/planks/oak1"),
            SidingWallTexSource.ResolveTexture("framing", "oak", "wattle", "daub", "planks", "brick", Framings, Infills, Finishes)!.Base);
        Assert.Equal(new AssetLocation("game:block/wood/wattle"),
            SidingWallTexSource.ResolveTexture("infill", "oak", "wattle", "daub", "planks", "brick", Framings, Infills, Finishes)!.Base);
        Assert.Equal(new AssetLocation("game:block/clay/daub/browngolden/normal1"),
            SidingWallTexSource.ResolveTexture("front", "oak", "wattle", "daub", "planks", "brick", Framings, Infills, Finishes)!.Base);
        Assert.Equal(new AssetLocation("game:block/wood/planks/aged1"),
            SidingWallTexSource.ResolveTexture("secondfront", "oak", "wattle", "daub", "planks", "brick", Framings, Infills, Finishes)!.Base);
        Assert.Equal(new AssetLocation("game:block/clay/brick/four/running/red1"),
            SidingWallTexSource.ResolveTexture("back", "oak", "wattle", "daub", "planks", "brick", Framings, Infills, Finishes)!.Base);
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
            "front", "oak", "wattle", "blended-daub", null, null, Framings, Infills, Finishes);

        Assert.NotNull(texture);
        Assert.Equal(new AssetLocation("game:block/clay/browngoldenclay"), texture!.Base);
        Assert.Equal([(new AssetLocation("game:block/clay/daub/browngolden/normal1"), EnumColorBlendMode.Overlay)], Overlays(texture));
    }
}
