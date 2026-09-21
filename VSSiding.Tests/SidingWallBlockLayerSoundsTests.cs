using System.Collections.Generic;
using Vintagestory.API.Common;
using Xunit;

namespace VSSiding.Tests;

public class SidingWallBlockLayerSoundsTests
{
    private static readonly BlockSounds WoodSounds = new();

    private static readonly BlockSounds StoneSounds = new();

    private static readonly Dictionary<EnumBlockMaterial, BlockSounds> LayerSounds = new()
    {
        [EnumBlockMaterial.Wood] = WoodSounds,
        [EnumBlockMaterial.Stone] = StoneSounds,
    };

    [Fact]
    public void KnownMaterialReturnsItsSounds()
    {
        Assert.Same(StoneSounds, SidingWallBlock.ResolveLayerSounds(EnumBlockMaterial.Stone, LayerSounds, WoodSounds));
    }

    [Fact]
    public void UnknownMaterialFallsBackToBlockSounds()
    {
        var fallback = new BlockSounds();
        Assert.Same(fallback, SidingWallBlock.ResolveLayerSounds(EnumBlockMaterial.Glass, LayerSounds, fallback));
    }

    [Fact]
    public void NullLayerSoundsFallsBackToBlockSounds()
    {
        var fallback = new BlockSounds();
        Assert.Same(fallback, SidingWallBlock.ResolveLayerSounds(EnumBlockMaterial.Wood, null, fallback));
    }
}
