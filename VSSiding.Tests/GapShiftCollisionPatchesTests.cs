using System.Reflection;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;
using Xunit;

namespace VSSiding.Tests;

public class GapShiftCollisionPatchesTests
{
    [Fact]
    public void ShiftedOffsetsEveryBoxByTheFacingsDistance()
    {
        var original = new[]
        {
            new Cuboidf(0, 0, 0, 1, 1, 1),
            new Cuboidf(0.25f, 0, 0.25f, 0.75f, 0.5f, 0.75f),
        };

        var expected = new[]
        {
            new Cuboidf(0, 0, 0.75f, 1, 1, 1.75f),
            new Cuboidf(0.25f, 0, 1f, 0.75f, 0.5f, 1.5f),
        };

        Assert.Equal(expected, GapShiftCollisionPatches.Shifted(original, 0, 1));
    }

    [Fact]
    public void ShiftedCachesTheResultPerOriginalArrayAndDirection()
    {
        var original = new[] { new Cuboidf(0, 0, 0, 1, 1, 1) };

        var first = GapShiftCollisionPatches.Shifted(original, 0, -1);
        var second = GapShiftCollisionPatches.Shifted(original, 0, -1);

        Assert.Same(first, second);
    }

    [Fact]
    public void PatchTargetsStillExistOnBlockAndAKnownVanillaOverride()
    {
        var signature = new[] { typeof(IBlockAccessor), typeof(BlockPos) };
        Assert.NotNull(typeof(Block).GetMethod(nameof(Block.GetCollisionBoxes),
            BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly, null, signature, null));
        Assert.NotNull(typeof(Block).GetMethod(nameof(Block.GetSelectionBoxes),
            BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly, null, signature, null));

        // BlockMultiblock declares both overrides - proof the enumeration in PatchAll reaches a
        // real vanilla Block subclass, not just Block itself.
        Assert.NotNull(typeof(BlockMultiblock).GetMethod(nameof(Block.GetCollisionBoxes),
            BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly, null, signature, null));
        Assert.NotNull(typeof(BlockMultiblock).GetMethod(nameof(Block.GetSelectionBoxes),
            BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly, null, signature, null));
    }
}
