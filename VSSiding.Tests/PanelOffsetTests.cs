using Newtonsoft.Json.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
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

    // A south post stands in the south-west corner, so furniture shifts north and east off it.
    [Fact]
    public void APostGuestShiftsDiagonallyAwayFromItsTwoCornerFaces()
    {
        Assert.Equal((0.25, -0.25), SidingModSystem.PanelOffset(FullCube, SidingWallBlock.PostFaces("south")));
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

    private static Block Trapdoor()
    {
        var trapdoor = new Block
        {
            Attributes = new JsonObject(new JObject()),
            SideSolid = new SmallBoolArray(0),
            SelectionBoxes = new[] { new Cuboidf(0, 0, 0, 1, 0.1875f, 1) },
        };
        trapdoor.BlockBehaviors = new BlockBehavior[] { new BlockBehaviorTrapDoor(trapdoor) };
        return trapdoor;
    }

    // A hatch is squeezed to the open part instead of shifted off the panel, and never fits under a deck.
    [Fact]
    public void ATrapdoorTakesNoFaceShiftAndFitsUnderNoDeck()
    {
        var trapdoor = Trapdoor();

        Assert.Equal(new[] { 0.0, 0.0, 0.0, 0.0 }, SidingModSystem.FaceShifts(trapdoor));
        Assert.Null(SidingModSystem.UnderDeckDropFor(trapdoor));
    }

    // A Roofing eave piece's boxes are its upper step alone, half a block high, so only the marker keeps it from a deck's drop.
    [Fact]
    public void ARoofTakesNoFaceShiftAndFitsUnderNoDeck()
    {
        static Block Eave(string attributes) => new()
        {
            Attributes = new JsonObject(JObject.Parse(attributes)),
            SideSolid = new SmallBoolArray(SmallBoolArray.OnAllSides),
            SelectionBoxes = new[] { new Cuboidf(0, 0.5f, 0, 1, 1, 0.5f) },
        };
        var unmarked = Eave("{ \"vssidingHostable\": true }");
        var roof = Eave("{ \"vssidingHostable\": true, \"vssidingRoof\": true }");

        Assert.Equal(new[] { 0.0, 0.0, 0.0, 0.0 }, SidingModSystem.FaceShifts(roof));
        Assert.Equal(((double?)0.25, (double?)null), (SidingModSystem.UnderDeckDropFor(unmarked), SidingModSystem.UnderDeckDropFor(roof)));
    }

    [Theory]
    [InlineData("north", 0.0, 0.0, 0.25, 1.0, 1.0, 1.0)]
    [InlineData("south", 0.0, 0.0, 0.0, 1.0, 1.0, 0.75)]
    [InlineData("west", 0.25, 0.0, 0.0, 1.0, 1.0, 1.0)]
    [InlineData("east", 0.0, 0.0, 0.0, 0.75, 1.0, 1.0)]
    public void TheOpenPartSquashMapsTheCellOntoWhatThePanelLeavesOpen(string side, double x1, double y1, double z1, double x2, double y2, double z2)
    {
        var squash = SidingModSystem.OpenPartSquash("wall", side)!;
        var box = new Cuboidf(0, 0, 0, 1, 1, 1).TransformedCopy(squash.Values);

        Assert.Equal((x1, y1, z1, x2, y2, z2), ((double)box.X1, (double)box.Y1, (double)box.Z1, (double)box.X2, (double)box.Y2, (double)box.Z2));
    }

    [Fact]
    public void OnlyAStraightWallHasAnOpenPartSquash()
        => Assert.Null(SidingModSystem.OpenPartSquash("cornerout", "north"));
}
