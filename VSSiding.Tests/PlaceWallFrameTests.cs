using System.Linq;
using Xunit;

namespace VSSiding.Tests;

public class PlaceWallFrameTests
{
    [Fact]
    public void PostSideIsTheVariantWhoseCornerIsNearestTheClick()
    {
        Assert.Equal(
            new[] { "west", "south", "east", "north" },
            new[] { PlaceWallFrame.PostSide(0.1, 0.1), PlaceWallFrame.PostSide(0.1, 0.9), PlaceWallFrame.PostSide(0.9, 0.9), PlaceWallFrame.PostSide(0.9, 0.1) });
    }

    [Fact]
    public void PostChargesAQuarterOfTheFramingRoundedUpToOne()
    {
        Assert.Equal(new[] { 1, 1, 2, 1 }, new[] { 2, 4, 8, 1 }.Select(SidingWallBlock.PostQuarter));
    }
}
