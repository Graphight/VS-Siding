using System;
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

        Assert.Equal(expected, GapShiftCollisionPatches.Shifted(original, 0, 0.75));
    }

    [Fact]
    public void ShiftedCachesTheResultPerOriginalArrayAndExactOffset()
    {
        var original = new[] { new Cuboidf(0, 0, 0, 1, 1, 1) };

        var first = GapShiftCollisionPatches.Shifted(original, 0, -0.25);
        var second = GapShiftCollisionPatches.Shifted(original, 0, -0.25);

        Assert.Same(first, second);
    }

    [Fact]
    public void PatchTargetsStillExistOnBlockAndAKnownVanillaOverride()
    {
        var signature = new[] { typeof(IBlockAccessor), typeof(BlockPos) };
        Assert.NotNull(typeof(Block).GetMethod(nameof(Block.GetCollisionBoxes),
            BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly, null, signature, null));
        Assert.NotNull(typeof(Block).GetMethod(nameof(Block.GetParticleCollisionBoxes),
            BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly, null, signature, null));
        Assert.NotNull(typeof(Block).GetMethod(nameof(Block.GetSelectionBoxes),
            BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly, null, signature, null));

        // BlockMultiblock declares all three overrides - proof the enumeration in PatchAll reaches
        // a real vanilla Block subclass, not just Block itself.
        Assert.NotNull(typeof(BlockMultiblock).GetMethod(nameof(Block.GetCollisionBoxes),
            BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly, null, signature, null));
        Assert.NotNull(typeof(BlockMultiblock).GetMethod(nameof(Block.GetParticleCollisionBoxes),
            BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly, null, signature, null));
        Assert.NotNull(typeof(BlockMultiblock).GetMethod(nameof(Block.GetSelectionBoxes),
            BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly, null, signature, null));
    }

    [Fact]
    public void CombinedConcatenatesHostAndPanelBoxes()
    {
        var host = new[] { new Cuboidf(0, 0, 0, 1, 1, 1) };
        var panel = new[] { new Cuboidf(0, 0, 0, 1, 1, 0.25f) };

        var expected = new[]
        {
            new Cuboidf(0, 0, 0, 1, 1, 1),
            new Cuboidf(0, 0, 0, 1, 1, 0.25f),
        };

        Assert.Equal(expected, GapShiftCollisionPatches.Combined(host, panel));
    }

    [Fact]
    public void CombinedReturnsHostBoxesUnchangedWhenThereIsNoPanel()
    {
        var host = new[] { new Cuboidf(0, 0, 0, 1, 1, 1) };

        Assert.Same(host, GapShiftCollisionPatches.Combined(host, Array.Empty<Cuboidf>()));
    }

    [Fact]
    public void CombinedIsJustThePanelWhenTheHostCollidesWithNothing()
    {
        var panel = new[] { new Cuboidf(0, 0, 0, 0.25f, 1, 1) };

        Assert.Same(panel, GapShiftCollisionPatches.Combined(null, panel));
    }

    [Fact]
    public void CombinedCachesTheResultPerHostAndPanelArrayPair()
    {
        var host = new[] { new Cuboidf(0, 0, 0, 1, 1, 1) };
        var panel = new[] { new Cuboidf(0, 0, 0, 1, 1, 0.25f) };

        var first = GapShiftCollisionPatches.Combined(host, panel);
        var second = GapShiftCollisionPatches.Combined(host, panel);

        Assert.Same(first, second);
    }
}
