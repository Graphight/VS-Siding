using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;
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
    public void ARunCellIsFreeOnlyAsAStraightWallOfTheMainCellsOwnRun()
    {
        var main = new BlockPos(10, 5, 10);
        var cases = new Dictionary<string, (Block main, Block cell, BlockPos pos, bool openPartFilled)>
        {
            ["wall above"] = (Wall("wall", "north"), Wall("wall", "north"), main.UpCopy(), false),
            ["wall along a north run"] = (Wall("wall", "north"), Wall("wall", "north"), main.EastCopy(), false),
            ["wall along an east run"] = (Wall("wall", "east"), Wall("wall", "east"), main.NorthCopy(), false),
            ["wall on the opposite side"] = (Wall("wall", "north"), Wall("wall", "south"), main.EastCopy(), false),
            ["parallel wall behind"] = (Wall("wall", "north"), Wall("wall", "north"), main.NorthCopy(), false),
            ["cornerout"] = (Wall("wall", "north"), Wall("cornerout", "north"), main.EastCopy(), false),
            ["decked or stepped wall"] = (Wall("wall", "north"), Wall("wall", "north"), main.UpCopy(), true),
            ["air"] = (Wall("wall", "north"), new Block(), main.EastCopy(), false),
            ["main cell a cornerout"] = (Wall("cornerout", "north"), Wall("wall", "north"), main.EastCopy(), false),
        };

        var expected = new Dictionary<string, bool>
        {
            ["wall above"] = true,
            ["wall along a north run"] = true,
            ["wall along an east run"] = true,
            ["wall on the opposite side"] = false,
            ["parallel wall behind"] = false,
            ["cornerout"] = false,
            ["decked or stepped wall"] = false,
            ["air"] = false,
            ["main cell a cornerout"] = false,
        };

        var actual = new Dictionary<string, bool>();
        foreach (var (name, c) in cases) actual[name] = SidingModSystem.RunCellFree(c.main, main, c.cell, c.pos, c.openPartFilled);

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
    public void ADoubleBedHostsHeadboardOnOrEitherLongSideWithThePanelAwayFromTheOpenRow()
    {
        // An east bed, feet east of the head, fillers to the north: the headboard faces west.
        var air = new Block();
        var footprints = new Dictionary<string, (Block head, Block feet, Block headFiller, Block feetFiller)>
        {
            ["headboard"] = (Wall("wall", "west"), air, Wall("wall", "west"), air),
            ["bed row in the wall"] = (Wall("wall", "south"), Wall("wall", "south"), air, air),
            ["filler row in the wall"] = (air, air, Wall("wall", "north"), Wall("wall", "north")),
            ["bed row with the panel toward the fillers"] = (Wall("wall", "north"), Wall("wall", "north"), air, air),
            ["filler row with the panel toward the bed"] = (air, air, Wall("wall", "south"), Wall("wall", "south")),
            ["headboard on the wrong face"] = (Wall("wall", "east"), air, Wall("wall", "east"), air),
            ["head alone in the wall"] = (Wall("wall", "west"), air, air, air),
            ["headboard with the feet row in a wall too"] = (Wall("wall", "west"), Wall("wall", "west"), Wall("wall", "west"), Wall("wall", "west")),
            ["bed row with a cornerout"] = (Wall("wall", "south"), Wall("cornerout", "south"), air, air),
            ["no walls"] = (air, air, air, air),
        };

        var expected = new Dictionary<string, bool>
        {
            ["headboard"] = true,
            ["bed row in the wall"] = true,
            ["filler row in the wall"] = true,
            ["bed row with the panel toward the fillers"] = false,
            ["filler row with the panel toward the bed"] = false,
            ["headboard on the wrong face"] = false,
            ["head alone in the wall"] = false,
            ["headboard with the feet row in a wall too"] = false,
            ["bed row with a cornerout"] = false,
            ["no walls"] = true,
        };

        var actual = new Dictionary<string, bool>();
        foreach (var (name, (head, feet, headFiller, feetFiller)) in footprints)
            actual[name] = SidingModSystem.DoubleBedFootprintHosts("east", "north", head, feet, headFiller, feetFiller);

        Assert.Equal(expected, actual);
    }

    internal static BlockBed DoubleBedHead(string side, string multiblock)
    {
        var head = new BlockBed();
        head.VariantStrict["part"] = "head";
        head.VariantStrict["side"] = side;
        var behavior = new BlockBehaviorMultiblock(head);
        behavior.Initialize(new JsonObject(JObject.Parse(multiblock)));
        head.BlockBehaviors = new BlockBehavior[] { behavior };
        return head;
    }

    // Kevins Furniture 1.9.0's celticbed.json Multiblock properties per head, copied, so this pins how
    // vanilla's Multiblock reads them, not the mod's file.
    internal static readonly Dictionary<string, string> CelticBedMultiblock = new()
    {
        ["east"] = "{ sizex: 2, sizey: 1, sizez: 1, cposition: { x: 0, y: 0, z: 1 } }",
        ["west"] = "{ sizex: 2, sizey: 1, sizez: 1, cposition: { x: 1, y: 0, z: 1 } }",
        ["south"] = "{ sizex: 1, sizey: 1, sizez: 2, cposition: { x: 1, y: 0, z: 0 } }",
        ["north"] = "{ sizex: 1, sizey: 1, sizez: 2, cposition: { x: 1, y: 0, z: 1 } }",
    };

    [Fact]
    public void TheCelticBedsFillersStandNorthOfAnEastWestBedAndWestOfANorthSouthOne()
    {
        var headPos = new BlockPos(10, 5, 10);
        var expected = new Dictionary<string, string?> { ["east"] = "north", ["west"] = "north", ["south"] = "west", ["north"] = "west" };

        var actual = new Dictionary<string, string?>();
        foreach (var (side, multiblock) in CelticBedMultiblock)
        {
            var fillers = SidingModSystem.MultiblockCells(DoubleBedHead(side, multiblock), headPos);
            actual[side] = SidingModSystem.FillerSide(headPos, headPos.AddCopy(BlockFacing.FromCode(side)), fillers)?.Code;
        }

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

    // Vanilla recomputes the facing from the retargeted selection with SuggestedHVOrientation, which reads
    // only the player's eye and Position + HitPosition (offset by Face when DidOffset is set). Keeping
    // that point, Face and DidOffset fixed keeps the facing the prefix checked. IPlayer has an internal
    // member a DispatchProxy can't implement, so the invariant is pinned rather than the facing itself.
    [Fact]
    public void ARetargetedClickKeepsThePointThePlayerHit()
    {
        var wallCell = new BlockPos(10, 5, 10);
        var expected = new Dictionary<(string facing, bool didOffset), (BlockPos, Vec3d, BlockFacing, bool)>();
        var actual = new Dictionary<(string facing, bool didOffset), (BlockPos, Vec3d, BlockFacing, bool)>();
        foreach (var facing in BlockFacing.HORIZONTALS)
        foreach (bool didOffset in new[] { false, true })
        {
            var click = new BlockSelection { Position = wallCell, Face = facing.Opposite, HitPosition = new Vec3d(0.3, 0.5, 0.7), DidOffset = didOffset };
            var feetPos = wallCell.AddCopy(facing.Opposite);

            var moved = SidingModSystem.Retargeted(click, feetPos);

            expected[(facing.Code, didOffset)] = (feetPos, click.FullPosition, click.Face, didOffset);
            actual[(facing.Code, didOffset)] = (moved.Position, moved.FullPosition, moved.Face, moved.DidOffset);
        }

        Assert.Equal(expected, actual);
    }
}
