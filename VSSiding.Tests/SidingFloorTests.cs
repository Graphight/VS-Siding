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
}
