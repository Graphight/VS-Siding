using System.Linq;
using Vintagestory.API.MathTools;
using Xunit;

namespace VSSiding.Tests;

public class SidingFloorTests
{
    private static readonly Vintagestory.API.Datastructures.JsonObject Finishes = SidingWallEntityTests.Dict("""
    {
        "daub": {},
        "planks": { "Elements": { "front": "front-weatherboard", "back": "back-boards" } }
    }
    """);

    [Fact]
    public void AStyledFinishDrawsAsThePlainSlab()
    {
        Assert.Equal(
            ["front", "framing-left", "framing-right", "framing-top", "framing-bottom", "infill", "back"],
            SidingFloorEntity.SelectiveElements("oak", "wattle", "planks", "planks", Finishes, (false, false, false, false)));
    }

    [Fact]
    public void AJoinedRimDropsAndTheInfillCarriesAcross()
    {
        Assert.Equal(
            ["framing-left", "framing-right", "framing-bottom", "infill", "infill-top"],
            SidingFloorEntity.SelectiveElements("oak", "wattle", null, null, Finishes, (true, false, false, false)));
    }

    [Theory]
    [InlineData("west", "east", true)]
    [InlineData("north", "south", true)]
    [InlineData("west", "west", true)]
    [InlineData("west", "north", false)]
    [InlineData("south", "east", false)]
    public void JoistsAlignOnlyAlongOneAxis(string side, string neighbourSide, bool expected)
    {
        Assert.Equal(expected, SidingFloorBlock.JoistsAlign(side, neighbourSide));
    }

    private static readonly Vintagestory.API.Datastructures.JsonObject Attributes = SidingWallEntityTests.Dict("""
    {
        "Framings": { "oak": {} },
        "Infills": { "wattle": { "BlockMaterial": "Wood" }, "stone": { "BlockMaterial": "Stone" } }
    }
    """);

    // Only the top seals, only once filled, and the infill still decides cooling.
    [Fact]
    public void OnlyAFilledFloorsTopRetains()
    {
        Assert.Equal(
            new[] { "up oak wattle 1", "down oak wattle 0", "north oak wattle 0", "up oak - 0", "up oak stone -1" },
            new[]
            {
                (BlockFacing.UP, "wattle"), (BlockFacing.DOWN, "wattle"), (BlockFacing.NORTH, "wattle"),
                (BlockFacing.UP, null), (BlockFacing.UP, "stone"),
            }.Select(c => $"{c.Item1.Code} oak {c.Item2 ?? "-"} {SidingFloorBlock.ComputeRetention(c.Item1, "oak", c.Item2, Attributes)}"));
    }

    [Fact]
    public void OnlyTheTopAndUndersideTakeAFinish()
    {
        Assert.Equal(
            new string?[] { "front", "back", null, null },
            new[] { BlockFacing.UP, BlockFacing.DOWN, BlockFacing.NORTH, BlockFacing.EAST }.Select(SidingFloorBlock.FinishFace));
    }
}
