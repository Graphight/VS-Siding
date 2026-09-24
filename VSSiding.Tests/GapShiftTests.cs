using System.Collections.Generic;
using Vintagestory.API.MathTools;
using Xunit;

namespace VSSiding.Tests;

public class GapShiftTests
{
    private static readonly Dictionary<BlockFacing, (string, string)?> NoNeighbours = new()
    {
        [BlockFacing.NORTH] = null,
        [BlockFacing.EAST] = null,
        [BlockFacing.SOUTH] = null,
        [BlockFacing.WEST] = null,
    };

    private static Dictionary<BlockFacing, (string, string)?> With(BlockFacing facing, string layout, string side)
    {
        var neighbours = new Dictionary<BlockFacing, (string, string)?>(NoNeighbours) { [facing] = (layout, side) };
        return neighbours;
    }

    [Fact]
    public void OneQualifyingNeighbourShiftsTowardIt()
    {
        // A wall to the north whose panel hugs its own north face opens its dead space south, back onto this cell.
        var neighbours = With(BlockFacing.NORTH, "wall", "north");
        Assert.Equal((0d, -0.75d), SidingWallBlock.GapShift(neighbours, null));
    }

    [Fact]
    public void NoNeighboursDontShift()
        => Assert.Equal((0d, 0d), SidingWallBlock.GapShift(NoNeighbours, null));

    [Fact]
    public void ANeighbourWhoseOpenSideFacesAwayDoesNotQualify()
    {
        // A wall to the north whose panel hugs the south face opens north, away from this cell.
        var neighbours = With(BlockFacing.NORTH, "wall", "south");
        Assert.Equal((0d, 0d), SidingWallBlock.GapShift(neighbours, null));
    }

    [Fact]
    public void ACorneroutNeighbourNeverQualifies()
    {
        var neighbours = With(BlockFacing.NORTH, "cornerout", "north");
        Assert.Equal((0d, 0d), SidingWallBlock.GapShift(neighbours, null));
    }

    [Fact]
    public void TwoQualifyingNeighboursCancelOut()
    {
        var neighbours = With(BlockFacing.NORTH, "wall", "north");
        neighbours[BlockFacing.SOUTH] = ("wall", "south");
        Assert.Equal((0d, 0d), SidingWallBlock.GapShift(neighbours, null));
    }

    [Fact]
    public void AttachedBlockShiftsOnlyTowardItsOwnFace()
    {
        var neighbours = With(BlockFacing.NORTH, "wall", "north");
        Assert.Equal((0d, -0.75d), SidingWallBlock.GapShift(neighbours, BlockFacing.NORTH));
        Assert.Equal((0d, 0d), SidingWallBlock.GapShift(neighbours, BlockFacing.EAST));
    }

    [Fact]
    public void EveryFacingShiftsTowardItsOwnQualifyingWall()
    {
        var expected = new Dictionary<BlockFacing, (double, double)>
        {
            [BlockFacing.NORTH] = (0d, -0.75d),
            [BlockFacing.EAST] = (0.75d, 0d),
            [BlockFacing.SOUTH] = (0d, 0.75d),
            [BlockFacing.WEST] = (-0.75d, 0d),
        };

        var actual = new Dictionary<BlockFacing, (double, double)>();
        foreach (var facing in BlockFacing.HORIZONTALS)
        {
            var neighbours = With(facing, "wall", facing.Code);
            actual[facing] = SidingWallBlock.GapShift(neighbours, null);
        }

        Assert.Equal(expected, actual);
    }
}
