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
        "shakes": { "Texture": "game:block/wood/shingles/oak-top", "BackTexture": "game:block/wood/debarked/oak" },
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

    [Fact]
    public void EachSlotResolvesFromItsOwnDictionary()
    {
        var actual = new[] { "framing", "infill", "front", "secondfront", "back" }
            .Select(slot => SidingWallTexSource.ResolveTexture(slot, "oak", "wattle", "daub", "planks", "brick", Framings, Infills, Finishes)!.Base);

        Assert.Equal(new[]
        {
            new AssetLocation("game:block/wood/planks/oak1"),
            new AssetLocation("game:block/wood/wattle"),
            new AssetLocation("game:block/clay/daub/browngolden/normal1"),
            new AssetLocation("game:block/wood/planks/aged1"),
            new AssetLocation("game:block/clay/brick/four/running/red1"),
        }, actual);
    }

    [Fact]
    public void BackTextureOverridesTextureOnTheBackOnly()
    {
        var actual = new[] { "front", "secondfront", "back" }
            .Select(slot => SidingWallTexSource.ResolveTexture(slot, "oak", "wattle", "shakes", "shakes", "shakes", Framings, Infills, Finishes)!.Base);

        Assert.Equal(new[]
        {
            new AssetLocation("game:block/wood/shingles/oak-top"),
            new AssetLocation("game:block/wood/shingles/oak-top"),
            new AssetLocation("game:block/wood/debarked/oak"),
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
}
