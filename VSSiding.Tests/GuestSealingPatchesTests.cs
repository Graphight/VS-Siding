using System.Reflection;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;
using Xunit;

namespace VSSiding.Tests;

public class GuestSealingPatchesTests
{
    [Fact]
    public void GetRetentionTargetsStillExistOnBlockAndAKnownVanillaOverride()
    {
        var signature = new[] { typeof(BlockPos), typeof(BlockFacing), typeof(EnumRetentionType) };
        Assert.NotNull(typeof(Block).GetMethod(nameof(Block.GetRetention),
            BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly, null, signature, null));

        // BlockFarmland declares its own override - proof the enumeration reaches a real vanilla
        // Block subclass, not just Block itself.
        Assert.NotNull(typeof(BlockFarmland).GetMethod(nameof(Block.GetRetention),
            BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly, null, signature, null));
    }

    [Fact]
    public void GetLiquidBarrierHeightOnSideTargetsStillExistOnBlockAndAKnownVanillaOverride()
    {
        var signature = new[] { typeof(BlockFacing), typeof(BlockPos) };
        Assert.NotNull(typeof(Block).GetMethod(nameof(Block.GetLiquidBarrierHeightOnSide),
            BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly, null, signature, null));

        Assert.NotNull(typeof(BlockMultiblock).GetMethod(nameof(Block.GetLiquidBarrierHeightOnSide),
            BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly, null, signature, null));
    }

    [Fact]
    public void CanAttachBlockAtTargetsStillExistOnBlockAndAKnownVanillaOverride()
    {
        var signature = new[] { typeof(IBlockAccessor), typeof(Block), typeof(BlockPos), typeof(BlockFacing), typeof(Cuboidi) };
        Assert.NotNull(typeof(Block).GetMethod(nameof(Block.CanAttachBlockAt),
            BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly, null, signature, null));

        Assert.NotNull(typeof(BlockGroundStorage).GetMethod(nameof(Block.CanAttachBlockAt),
            BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly, null, signature, null));
    }
}
