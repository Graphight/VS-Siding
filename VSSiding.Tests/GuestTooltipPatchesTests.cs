using System.Reflection;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;
using Xunit;

namespace VSSiding.Tests;

public class GuestTooltipPatchesTests
{
    [Fact]
    public void PatchTargetsStillExistOnBlockAndAKnownVanillaOverride()
    {
        var signature = new[] { typeof(IWorldAccessor), typeof(BlockPos), typeof(IPlayer) };
        Assert.NotNull(typeof(Block).GetMethod(nameof(Block.GetPlacedBlockInfo),
            BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly, null, signature, null));

        // BlockCrate declares its own override - proof the enumeration reaches a real vanilla
        // Block subclass, not just Block itself.
        Assert.NotNull(typeof(BlockCrate).GetMethod(nameof(Block.GetPlacedBlockInfo),
            BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly, null, signature, null));
    }
}
