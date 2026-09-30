using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Xunit;

namespace VSSiding.Tests;

public class SidingFloorTests
{
    private static readonly JsonObject Finishes = SidingWallEntityTests.Dict("""
    {
        "daub": { "FloorElements": { "back": "back-lath" } },
        "planks": { "Elements": { "front": "front-weatherboard", "back": "back-boards" }, "FloorElements": { "front": "front-hboards", "back": "back-hboards" }, "FloorStyles": ["boards", "hboards"] }
    }
    """);

    [Fact]
    public void AFinishDrawsTheElementsItsFloorElementsName()
    {
        Assert.Equal(
            new string[][]
            {
                ["front-hboards", "framing-left", "framing-right", "framing-top", "framing-bottom", "infill", "back-hboards"],
                ["front", "framing-left", "framing-right", "framing-top", "framing-bottom", "infill", "back-lath"],
            },
            new[] { ("planks", "planks"), ("daub", "daub") }.Select(f =>
                SidingFloorEntity.SelectiveElements("oak", "wattle", f.Item1, f.Item2, Finishes, (false, false, false, false))));
    }

    [Fact]
    public void AStyleNamesItsElementsOutrightAndNoneKeepsTheDefault()
    {
        Assert.Equal(
            new string[][]
            {
                ["front-boards", "framing-left", "framing-right", "framing-top", "framing-bottom", "infill", "back-boards"],
                ["front-hboards", "framing-left", "framing-right", "framing-top", "framing-bottom", "infill", "back-hboards"],
                ["front-boards", "framing-left", "framing-right", "framing-top", "framing-bottom", "infill", "back-hboards"],
            },
            new (string?, string?)[] { ("boards", "boards"), (null, null), ("boards", null) }.Select(s =>
                SidingFloorEntity.SelectiveElements("oak", "wattle", "planks", "planks", Finishes, (false, false, false, false), s)));
    }

    [Fact]
    public void AFinishTakesOnlyTheStylesItsFloorStylesList()
    {
        var planks = Finishes["planks"];
        Assert.Equal(
            [true, false, true],
            new[] { "boards", "weatherboard", "hboards" }.Select(s => SidingWallBlock.HasStyle(planks, s, "FloorStyles")));
    }

    [Fact]
    public void AJoinedRimDropsAndTheInfillCarriesAcross()
    {
        Assert.Equal(
            ["framing-left", "framing-right", "framing-bottom", "infill", "infill-top"],
            SidingFloorEntity.SelectiveElements("oak", "wattle", null, null, Finishes, (true, false, false, false)));
    }

    // Glass is one pane through the joists, with no slivers to seam and no finish-dependent infill.
    [Fact]
    public void AGlazedFloorDrawsTheFramingAndOnePane()
    {
        Assert.Equal(
            new string[][]
            {
                ["framing-left", "framing-right", "framing-top", "framing-bottom", "infill-pane"],
                ["framing-left", "framing-right", "framing-bottom", "infill-pane"],
                ["framing-left", "framing-right", "infill-pane"],
            },
            new[] { (false, false), (true, false), (true, true) }.Select(j =>
                SidingFloorEntity.SelectiveElements("oak", "glass", null, null, Finishes, (j.Item1, j.Item2, false, false), glazed: true)));
    }

    private static readonly JsonObject Attributes = SidingWallEntityTests.Dict("""
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

    // A top hit takes the floorboards, a bottom hit the ceiling, an edge hit whichever finish is on top.
    [Fact]
    public void BreakingPeelsTheHitFacesFinishThenTheInfill()
    {
        var full = new SidingFloorEntity { Framing = "oak", Infill = "wattle", Front = "planks", Back = "daub" };
        var ceilingOnly = new SidingFloorEntity { Framing = "oak", Infill = "wattle", Back = "daub" };
        var filled = new SidingFloorEntity { Framing = "oak", Infill = "wattle" };
        var bare = new SidingFloorEntity { Framing = "oak" };

        Assert.Equal(
            new string?[] { "front", "back", "front", "back", "infill", null },
            new[]
            {
                SidingFloorBlock.PeelLayer(BlockFacing.UP, full),
                SidingFloorBlock.PeelLayer(BlockFacing.DOWN, full),
                SidingFloorBlock.PeelLayer(BlockFacing.NORTH, full),
                SidingFloorBlock.PeelLayer(BlockFacing.UP, ceilingOnly),
                SidingFloorBlock.PeelLayer(BlockFacing.UP, filled),
                SidingFloorBlock.PeelLayer(BlockFacing.UP, bare),
            });
    }

    [Fact]
    public void TheTooltipNamesEachLayerAndTheSeal()
    {
        var attributes = SidingWallEntityTests.Dict("""
        {
            "Framings": { "oak": { "DisplayName": "Oak Framing" } },
            "Infills": { "stone": { "DisplayName": "Stone Infill", "BlockMaterial": "Stone" } },
            "Finishes": { "planks": { "DisplayName": "Oak Planks" } }
        }
        """);
        var lang = new Dictionary<string, string>
        {
            ["vssiding:tooltip-top"] = "Top: {0}",
            ["vssiding:tooltip-underside"] = "Underside: {0}",
            ["vssiding:tooltip-unfinished"] = "unfinished",
            ["vssiding:tooltip-sealed-cool"] = "Seals the room and keeps it cool",
        };

        Assert.Equal(
            "\n  Oak Framing\n  Stone Infill\n  Top: Oak Planks\n  Underside: unfinished\n  Seals the room and keeps it cool\n".Replace("\n", System.Environment.NewLine),
            SidingFloorBlock.Describe("oak", "stone", "planks", null, attributes, key => lang.GetValueOrDefault(key) ?? (key.StartsWith("vssiding:") ? null : key)));
    }

    // From a clicked floor at x 0 looking east: the first gap past the run, unless it is solid or out of reach.
    [Fact]
    public void ARunExtendsIntoItsFirstGapWithinReach()
    {
        var start = new BlockPos(0, 0, 0);
        var shortRun = new HashSet<BlockPos> { new(1, 0, 0), new(2, 0, 0) };
        var longRun = new HashSet<BlockPos> { new(1, 0, 0), new(2, 0, 0), new(3, 0, 0), new(4, 0, 0) };
        var wallAtGap = new HashSet<BlockPos> { new(3, 0, 0) };

        Assert.Equal(
            new BlockPos?[] { new(3, 0, 0), null, null },
            new[]
            {
                SidingFloorBlock.RunEnd(shortRun.Contains, _ => true, start, BlockFacing.EAST),
                SidingFloorBlock.RunEnd(shortRun.Contains, pos => !wallAtGap.Contains(pos), start, BlockFacing.EAST),
                SidingFloorBlock.RunEnd(longRun.Contains, _ => true, start, BlockFacing.EAST),
            });
    }

    [Fact]
    public void AheadIsTheDominantHorizontalDirectionOfTheView()
    {
        Assert.Equal(
            new[] { BlockFacing.EAST, BlockFacing.WEST, BlockFacing.SOUTH, BlockFacing.NORTH },
            new[] { new Vec3f(0.9f, -0.8f, 0.2f), new Vec3f(-0.5f, -0.9f, 0.1f), new Vec3f(0.1f, -0.9f, 0.3f), new Vec3f(0.2f, 0.5f, -0.6f) }
                .Select(SidingFloorBlock.Ahead));
    }
}
