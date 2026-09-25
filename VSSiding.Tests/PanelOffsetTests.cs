using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;
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

    // A cabinet's default boxes stand 5/16 off its front (south) face, but its block entity turns it.
    [Fact]
    public void ARotateablePlaceableBlockTakesItsLargestShiftOnEveryFace()
    {
        var cabinetBoxes = new[] { new Cuboidf(0, 0, 0, 1, 1, 0.6875f) };
        var fixedCabinet = new Block { SelectionBoxes = cabinetBoxes };
        var turningCabinet = new Block { SelectionBoxes = cabinetBoxes };
        turningCabinet.BlockBehaviors = new BlockBehavior[] { new BlockBehaviorRotateablePlaceable(turningCabinet) };

        Assert.Equal(new[] { 0.25, 0.25, 0.0, 0.25 }, SidingModSystem.FaceShifts(fixedCabinet));
        Assert.Equal(new[] { 0.25, 0.25, 0.25, 0.25 }, SidingModSystem.FaceShifts(turningCabinet));
    }
}
