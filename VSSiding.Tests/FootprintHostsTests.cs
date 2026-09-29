using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Xunit;

namespace VSSiding.Tests;

// Pins the footprint rule the CanPlaceBlock postfix enforces: a trunk may only take a wall run's
// cells when every cell is a straight wall on one side that runs along the trunk's own long axis.
public class FootprintHostsTests
{
    private static readonly string[] Sides = { "north", "east", "south", "west" };

    internal static SidingWallBlock Wall(string layout, string side)
    {
        var wall = new SidingWallBlock();
        wall.VariantStrict["layout"] = layout;
        wall.VariantStrict["side"] = side;
        return wall;
    }

    [Fact]
    public void ATwoWallFootprintHostsOnlyAlongTheTrunksLongAxis()
    {
        var hosts = new HashSet<(string trunk, string wall)>
        {
            ("north", "north"), ("north", "south"), ("south", "north"), ("south", "south"),
            ("east", "east"), ("east", "west"), ("west", "east"), ("west", "west"),
        };

        var expected = new Dictionary<(string trunk, string wall), bool>();
        var actual = new Dictionary<(string trunk, string wall), bool>();
        foreach (var trunkSide in Sides)
        foreach (var wallSide in Sides)
        {
            expected[(trunkSide, wallSide)] = hosts.Contains((trunkSide, wallSide));
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

    [Fact]
    public void ATwoWallBedHostsOnlyAcrossItsLongAxis()
    {
        var hosts = new HashSet<(string bed, string wall)>
        {
            ("north", "east"), ("north", "west"), ("south", "east"), ("south", "west"),
            ("east", "north"), ("east", "south"), ("west", "north"), ("west", "south"),
        };

        var expected = new Dictionary<(string bed, string wall), bool>();
        var actual = new Dictionary<(string bed, string wall), bool>();
        foreach (var bedSide in Sides)
        foreach (var wallSide in Sides)
        {
            expected[(bedSide, wallSide)] = hosts.Contains((bedSide, wallSide));
            actual[(bedSide, wallSide)] = SidingModSystem.BedFootprintHosts(bedSide, Wall("wall", wallSide), Wall("wall", wallSide));
        }

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void AHeadOnBedHostsOnlyWhenTheHeadWallClaimsTheHeadboardFace()
    {
        var opposite = new Dictionary<string, string> { ["north"] = "south", ["east"] = "west", ["south"] = "north", ["west"] = "east" };

        var expected = new Dictionary<(string bed, string wall), bool>();
        var actual = new Dictionary<(string bed, string wall), bool>();
        foreach (var bedSide in Sides)
        foreach (var wallSide in Sides)
        {
            expected[(bedSide, wallSide)] = wallSide == opposite[bedSide];
            actual[(bedSide, wallSide)] = SidingModSystem.BedFootprintHosts(bedSide, Wall("wall", wallSide), new Block());
        }

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void MixedBedFootprints()
    {
        var air = new Block();
        var footprints = new Dictionary<string, (Block head, Block feet)>
        {
            ["feet to the panel"] = (air, Wall("wall", "north")),
            ["wall and cornerout"] = (Wall("wall", "east"), Wall("cornerout", "east")),
            ["opposite faces"] = (Wall("wall", "east"), Wall("wall", "west")),
            ["head on with a wall in the feet cell"] = (Wall("wall", "south"), Wall("wall", "east")),
            ["head on with a cornerout"] = (Wall("cornerout", "south"), air),
            ["no walls"] = (air, air),
        };

        var expected = new Dictionary<string, bool>
        {
            ["feet to the panel"] = false,
            ["wall and cornerout"] = false,
            ["opposite faces"] = false,
            ["head on with a wall in the feet cell"] = false,
            ["head on with a cornerout"] = false,
            ["no walls"] = true,
        };

        var actual = new Dictionary<string, bool>();
        foreach (var (name, (head, feet)) in footprints) actual[name] = SidingModSystem.BedFootprintHosts("north", head, feet);

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void AClickOnAPanelStepsTheFeetBackSoTheHeadLandsInTheWall()
    {
        var pos = new BlockPos(10, 5, 10);
        var clicked = new Dictionary<string, Block>
        {
            ["wall the player faces into"] = Wall("wall", "north"),
            ["wall on the opposite side"] = Wall("wall", "south"),
            ["wall on a perpendicular side"] = Wall("wall", "east"),
            ["cornerout on the facing side"] = Wall("cornerout", "north"),
            ["air"] = new Block(),
        };

        var expected = new Dictionary<string, BlockPos>
        {
            ["wall the player faces into"] = new BlockPos(10, 5, 11),
            ["wall on the opposite side"] = pos,
            ["wall on a perpendicular side"] = pos,
            ["cornerout on the facing side"] = pos,
            ["air"] = pos,
        };

        var actual = new Dictionary<string, BlockPos>();
        foreach (var (name, block) in clicked) actual[name] = SidingModSystem.BedFeetPos(block, BlockFacing.NORTH, pos);

        Assert.Equal(expected, actual);
    }
}
