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
        "daub": { "FloorElements": { "back": { "hboards": "back-lath-hboards", "boards": "back-lath-boards" } } },
        "planks": {
            "Elements": { "front": "front-weatherboard", "back": "back-boards" },
            "FloorElements": {
                "front": { "hboards": "front-hboards", "boards": "front-boards" },
                "back": { "hboards": "back-hboards", "boards": "back-boards" }
            }
        }
    }
    """);

    [Fact]
    public void AFinishDrawsTheElementsItsFloorElementsName()
    {
        Assert.Equal(
            new string[][]
            {
                ["front-hboards", "framing-left", "framing-right", "framing-top", "framing-bottom", "infill", "back-hboards"],
                ["front", "framing-left", "framing-right", "framing-top", "framing-bottom", "infill", "back-lath-hboards"],
            },
            new[] { ("planks", "planks"), ("daub", "daub") }.Select(f =>
                SidingFloorEntity.SelectiveElements("oak", "wattle", f.Item1, f.Item2, Finishes, (false, false, false, false))));
    }

    [Fact]
    public void AStyleNamesItsFacesElementAndNoneKeepsTheDefault()
    {
        Assert.Equal(
            new string[][]
            {
                ["front-boards", "framing-left", "framing-right", "framing-top", "framing-bottom", "infill", "back-boards"],
                ["front-hboards", "framing-left", "framing-right", "framing-top", "framing-bottom", "infill", "back-hboards"],
                ["front-boards", "framing-left", "framing-right", "framing-top", "framing-bottom", "infill", "back-hboards"],
                ["front", "framing-left", "framing-right", "framing-top", "framing-bottom", "infill", "back-lath-boards"],
            },
            new (string, (string?, string?))[] { ("planks", ("boards", "boards")), ("planks", (null, null)), ("planks", ("boards", null)), ("daub", (null, "boards")) }.Select(c =>
                SidingFloorEntity.SelectiveElements("oak", "wattle", c.Item1, c.Item1, Finishes, (false, false, false, false), c.Item2)));
    }

    // A style counts per face, so boards picked while daubing the top leave the plain slab there.
    [Fact]
    public void AFaceTakesOnlyTheStylesItsFloorElementsList()
    {
        Assert.Equal(
            ["planks front boards True", "planks front weatherboard False", "planks back hboards True", "daub front boards False", "daub back boards True"],
            new[] { ("planks", "front", "boards"), ("planks", "front", "weatherboard"), ("planks", "back", "hboards"), ("daub", "front", "boards"), ("daub", "back", "boards") }
                .Select(c => $"{c.Item1} {c.Item2} {c.Item3} {SidingFloorEntity.HasFloorStyle(Finishes[c.Item1], c.Item2, c.Item3)}"));
    }

    [Fact]
    public void ADeckDrawsTheFloorsGroupsUnderItsSidesPrefix()
    {
        Assert.Equal(
            new string[][]
            {
                ["deck-west-framing-left", "deck-west-framing-right", "deck-west-framing-top", "deck-west-framing-bottom"],
                ["deck-south-front-boards", "deck-south-framing-left", "deck-south-framing-right", "deck-south-framing-bottom", "deck-south-infill",
                    "deck-south-infill-top", "deck-south-back-hboards"],
                ["deck-north-glazing-top", "deck-north-glazing-bottom", "deck-north-infill-pane"],
                [],
            },
            new[]
            {
                SidingWallEntity.DeckElements("west", "oak", null, null, null, Finishes, (false, false, false, false), default, false),
                SidingWallEntity.DeckElements("south", "oak", "wattle", "planks", "planks", Finishes, (true, false, false, false), ("boards", null), false),
                SidingWallEntity.DeckElements("north", "oak", "glass", null, null, Finishes, (false, false, true, true), default, true),
                SidingWallEntity.DeckElements("east", null, "wattle", "planks", "planks", Finishes, (false, false, false, false), default, false),
            });
    }

    [Fact]
    public void AJoinedRimDropsAndTheInfillCarriesAcross()
    {
        Assert.Equal(
            ["framing-left", "framing-right", "framing-bottom", "infill", "infill-top"],
            SidingFloorEntity.SelectiveElements("oak", "wattle", null, null, Finishes, (true, false, false, false)));
    }

    [Fact]
    public void ANeighbourContinuesJoistsOnFramingAndGlazingOnTransparentInfill()
    {
        JsonObject infills = SidingWallEntityTests.Dict("""{ "glass": { "Transparent": true }, "wattle": {} }""");
        Assert.Equal(
            [false, true, true, false, true, false],
            new[]
            {
                SidingFloorBlock.Continues(false, null, null, infills),
                SidingFloorBlock.Continues(false, "oak", "wattle", infills),
                SidingFloorBlock.Continues(true, "oak", "glass", infills),
                SidingFloorBlock.Continues(true, "oak", "wattle", infills),
                SidingFloorBlock.Continues(false, "oak", null, infills),
                SidingFloorBlock.Continues(true, "oak", null, infills),
            });
    }

    // A deck stops short of the faces its wall claims, so only a neighbour on another face carries it on.
    [Fact]
    public void ADeckReachesTheEdgesItsWallDoesNotClaim()
    {
        Assert.Equal(
            [false, true, true, true, false, true],
            new[]
            {
                SidingFloorBlock.DeckReaches("wall", "west", BlockFacing.EAST),
                SidingFloorBlock.DeckReaches("wall", "west", BlockFacing.WEST),
                SidingFloorBlock.DeckReaches("wall", "west", BlockFacing.NORTH),
                SidingFloorBlock.DeckReaches("cornerout", "west", BlockFacing.WEST),
                SidingFloorBlock.DeckReaches("cornerout", "west", BlockFacing.SOUTH),
                SidingFloorBlock.DeckReaches("cornerout", "west", BlockFacing.NORTH),
            });
    }

    // Glazing merges east and west too, so a key without those joins handed one cell's bezel to its neighbours.
    [Fact]
    public void EveryJoinStateMeshesUnderItsOwnKey()
    {
        var keys =
            from above in new[] { false, true }
            from below in new[] { false, true }
            from left in new[] { false, true }
            from right in new[] { false, true }
            select SidingFloorEntity.CacheKey("oak", "glass", null, null, default, (above, below, left, right));

        Assert.Equal(16, keys.Distinct().Count());
    }

    // Glass is one pane in a bezel with no joists, each member dropping where the next floor is glazed too.
    [Fact]
    public void AGlazedFloorDrawsItsBezelAndOnePane()
    {
        Assert.Equal(
            new string[][]
            {
                ["glazing-left", "glazing-right", "glazing-top", "glazing-bottom", "infill-pane"],
                ["glazing-right", "glazing-bottom", "infill-pane"],
                ["infill-pane"],
            },
            new[] { (false, false, false, false), (true, false, true, false), (true, true, true, true) }.Select(j =>
                SidingFloorEntity.SelectiveElements("oak", "glass", null, null, Finishes, j, glazed: true)));
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

    // Bare joists are enough to hang from; standing on the top still wants it sealed.
    [Fact]
    public void AFloorHoldsAHangerOnceFramedButASolidTopOnlyOnceFilled()
    {
        Assert.Equal(
            new[] { "down oak - True", "down - - False", "up oak - False", "up oak wattle True", "north oak wattle False" },
            new[]
            {
                (BlockFacing.DOWN, "oak", null), (BlockFacing.DOWN, null, null), (BlockFacing.UP, "oak", null),
                (BlockFacing.UP, "oak", "wattle"), (BlockFacing.NORTH, "oak", "wattle"),
            }.Select(c => $"{c.Item1.Code} {c.Item2 ?? "-"} {c.Item3 ?? "-"} {SidingFloorBlock.CanAttach(c.Item1, c.Item2, c.Item3, Attributes)}"));
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
