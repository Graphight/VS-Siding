using System.Collections.Generic;
using Vintagestory.API.Common;
using Xunit;

namespace VSSiding.Tests;

public class SidingWallBlockLayerResistanceTests
{
    private static readonly Dictionary<EnumBlockMaterial, float> LayerResistance = new()
    {
        [EnumBlockMaterial.Wood] = 1.0f,
        [EnumBlockMaterial.Stone] = 2.5f,
    };

    [Fact]
    public void KnownMaterialMultipliesResistance()
    {
        Assert.Equal(3.0f, SidingWallBlock.ResolveLayerResistance(EnumBlockMaterial.Stone, LayerResistance, 1.2f));
    }

    [Fact]
    public void UnknownMaterialFallsBackToPlainResistance()
    {
        Assert.Equal(1.2f, SidingWallBlock.ResolveLayerResistance(EnumBlockMaterial.Glass, LayerResistance, 1.2f));
    }

    [Fact]
    public void NullLayerResistanceFallsBackToPlainResistance()
    {
        Assert.Equal(1.2f, SidingWallBlock.ResolveLayerResistance(EnumBlockMaterial.Wood, null, 1.2f));
    }
}
