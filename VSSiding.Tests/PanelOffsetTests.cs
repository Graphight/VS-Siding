using Vintagestory.API.MathTools;
using Xunit;

namespace VSSiding.Tests;

public class PanelOffsetTests
{
    private static readonly Cuboidf[] FullCube = { new(0, 0, 0, 1, 1, 1) };

    [Fact]
    public void AStraightWallShiftsAwayFromItsOneClaimedFace()
    {
        Assert.Equal((0.0, 0.25), SidingModSystem.PanelOffset(FullCube, new[] { "north" }));
    }

    [Fact]
    public void ACorneroutGuestShiftsOnBothAxes()
    {
        Assert.Equal((0.25, 0.25), SidingModSystem.PanelOffset(FullCube, new[] { "north", "west" }));
    }

    [Fact]
    public void ABlockAlreadyInsetPastThePanelThicknessDoesNotShift()
    {
        var insetBoxes = new[] { new Cuboidf(0, 0, 0.3f, 1, 1, 1) };

        Assert.Equal((0.0, 0.0), SidingModSystem.PanelOffset(insetBoxes, new[] { "north" }));
    }

    [Fact]
    public void AThinBoxFlushAgainstTheFaceGetsTheFullPanelThickness()
    {
        var torchBox = new[] { new Cuboidf(0.4375f, 0, 0, 0.5625f, 0.5f, 0.125f) };

        Assert.Equal((0.0, 0.25), SidingModSystem.PanelOffset(torchBox, new[] { "north" }));
    }
}
