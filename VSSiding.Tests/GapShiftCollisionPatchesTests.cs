using System;
using System.Linq;
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
    public void ShiftedKeepsASelectionBoxsId()
    {
        var original = new Cuboidf[] { new CuboidfWithId(0, 0.5, 0, 1, 0.5625, 0.6875) { Id = "0-0-0" } };

        var shifted = GapShiftCollisionPatches.Shifted(original, 0, 0.25);

        var expected = (0f, 0.5f, 0.25f, 1f, 0.5625f, 0.9375f, "0-0-0");
        Assert.Equal(expected, shifted.Select(box => box is CuboidfWithId withId
            ? (withId.X1, withId.Y1, withId.Z1, withId.X2, withId.Y2, withId.Z2, withId.Id)
            : default).Single());
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

    [Fact]
    public void CombinedPutsHostBoxesFirstAndPanelSelectionBoxesAfterWithEqualGeometry()
    {
        Cuboidf[] host = { new(0, 0, 0, 1, 1, 1), new(0, 0, 0.25f, 1, 0.5f, 0.75f) };
        Cuboidf[] panel = { new PanelSelectionBox(new Cuboidf(0, 0, 0, 1, 1, 0.25f)) };

        Cuboidf[] result = GapShiftCollisionPatches.Combined(host, panel);

        var expectedGeometry = host.Concat(panel).Select(Geometry);
        Assert.Equal(expectedGeometry, result.Select(Geometry));
        Assert.All(result.Skip(host.Length), box => Assert.IsType<PanelSelectionBox>(box));
    }

    private static (float, float, float, float, float, float) Geometry(Cuboidf box)
        => (box.X1, box.Y1, box.Z1, box.X2, box.Y2, box.Z2);
}
