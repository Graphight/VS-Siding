using System.Collections.Generic;
using Vintagestory.API.Common;
using Xunit;

namespace VSSiding.Tests;

// Pins the footprint rule the CanPlaceBlock postfix enforces: a trunk may only take a wall run's
// cells when every cell is a straight wall on one side that runs along the trunk's own long axis.
public class FootprintHostsTests
{
    private static readonly string[] Sides = { "north", "east", "south", "west" };

    private static SidingWallBlock Wall(string layout, string side)
    {
        var wall = new SidingWallBlock();
        wall.VariantStrict["layout"] = layout;
        wall.VariantStrict["side"] = side;
        return wall;
    }

    [Fact]
    public void ATwoWallFootprintHostsOnlyAlongTheTrunksLongAxis()
    {
        var expected = new Dictionary<(string trunk, string wall), bool>();
        var actual = new Dictionary<(string trunk, string wall), bool>();
        foreach (var trunkSide in Sides)
        foreach (var wallSide in Sides)
        {
            bool alongX = trunkSide is "north" or "south";
            expected[(trunkSide, wallSide)] = alongX == (wallSide is "north" or "south");
            actual[(trunkSide, wallSide)] = SidingModSystem.FootprintHosts(trunkSide, new Block[] { Wall("wall", wallSide), Wall("wall", wallSide) });
        }

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void MixedFootprints()
    {
        var air = new Block();
        var footprints = new Dictionary<string, Block[]>
        {
            ["opposite faces of the axis"] = new Block[] { Wall("wall", "north"), Wall("wall", "south") },
            ["wall and air"] = new Block[] { Wall("wall", "north"), air },
            ["wall and cornerout"] = new Block[] { Wall("wall", "north"), Wall("cornerout", "north") },
            ["no walls"] = new[] { air, air },
        };

        var expected = new Dictionary<string, bool>
        {
            ["opposite faces of the axis"] = false,
            ["wall and air"] = false,
            ["wall and cornerout"] = false,
            ["no walls"] = true,
        };

        var actual = new Dictionary<string, bool>();
        foreach (var (name, cells) in footprints) actual[name] = SidingModSystem.FootprintHosts("north", cells);

        Assert.Equal(expected, actual);
    }
}
