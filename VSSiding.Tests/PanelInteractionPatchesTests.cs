using System.Reflection;
using Vintagestory.API.Common;
using Vintagestory.GameContent;
using Xunit;

namespace VSSiding.Tests;

public class PanelInteractionPatchesTests
{
    [Fact]
    public void OnBlockInteractStartTargetsStillExistOnBlockAndAKnownVanillaOverride()
    {
        var signature = new[] { typeof(IWorldAccessor), typeof(IPlayer), typeof(BlockSelection) };
        Assert.NotNull(typeof(Block).GetMethod(nameof(Block.OnBlockInteractStart),
            BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly, null, signature, null));

        // BlockCrate declares its own override - proof the enumeration reaches a real vanilla
        // Block subclass, not just Block itself.
        Assert.NotNull(typeof(BlockCrate).GetMethod(nameof(Block.OnBlockInteractStart),
            BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly, null, signature, null));
    }

    [Fact]
    public void OnGettingBrokenTargetsStillExistOnBlockAndAKnownVanillaOverride()
    {
        var signature = new[] { typeof(IPlayer), typeof(BlockSelection), typeof(ItemSlot), typeof(float), typeof(float), typeof(int) };
        Assert.NotNull(typeof(Block).GetMethod(nameof(Block.OnGettingBroken),
            BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly, null, signature, null));

        Assert.NotNull(typeof(BlockSupportBeam).GetMethod(nameof(Block.OnGettingBroken),
            BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly, null, signature, null));
    }
}
