using Vintagestory.API.Common;
using Xunit;

namespace VSSiding.Tests;

public class SidingWallBlockLayerCombustibleTests
{
    private static readonly CombustibleProperties Props = new() { BurnTemperature = 600, BurnDuration = 20 };

    [Fact]
    public void WoodKeepsTheBlocksCombustibleProps()
    {
        Assert.Same(Props, SidingWallBlock.ResolveLayerCombustible(EnumBlockMaterial.Wood, Props));
    }

    [Theory]
    [InlineData(EnumBlockMaterial.Soil)]
    [InlineData(EnumBlockMaterial.Glass)]
    [InlineData(EnumBlockMaterial.Ceramic)]
    [InlineData(EnumBlockMaterial.Stone)]
    public void NonWoodDoesNotBurn(EnumBlockMaterial material)
    {
        Assert.Null(SidingWallBlock.ResolveLayerCombustible(material, Props));
    }
}
