using System.Linq;
using System.Threading.Tasks;
using Atlas.Api;
using Atlas.XUnit;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Xunit;

namespace VSSiding.E2E.Tests;

// A filled wall turns into a cornerout in place where a fresh Corner press cannot board the face (decision 0069).
// Every wall here hugs the west face of its cell, so west is its front, east its back, and south the run's right end.
[AtlasWorld(StrictBootDiagnostics = true)]
public class JunctionScenarios : AtlasScenarioBase
{
    private static readonly Vec3d SouthEnd = new(0, 0.5, 0.8);
    private static readonly Vec3d NorthEnd = new(0, 0.5, 0.2);

    [AtlasScenario(FreshWorld = true)]
    public async Task FilledWall_Should_BecomeACornerMovingItsFrontFinishToTheSecondFace_When_ItsFinishedFrontIsPressedAtTheRightEnd()
    {
        (BlockPos cell, ITestPlayer player) = await RaiseFinishedDeckedWall();

        await WallBuilder.Press(World, player, cell, "game:plank-oak", BlockFacing.WEST, SouthEnd);

        Assert.Equal(("cornerout", "south,west", "oak", ("oak", "clay", null, "planks-birch", "planks-birch")), Describe(cell));
    }

    [AtlasScenario(FreshWorld = true)]
    public async Task FilledWall_Should_BecomeACornerKeepingItsFrontFinish_When_ItsFinishedFrontIsPressedAtTheLeftEnd()
    {
        (BlockPos cell, ITestPlayer player) = await RaiseFinishedDeckedWall();

        await WallBuilder.Press(World, player, cell, "game:plank-oak", BlockFacing.WEST, NorthEnd);

        Assert.Equal(("cornerout", "north,west", "oak", ("oak", "clay", "planks-birch", null, "planks-birch")), Describe(cell));
    }

    // The wall passes this press on, so vanilla repeats it through the held planks, which frame nothing on a repeat.
    [AtlasScenario(FreshWorld = true)]
    public async Task FilledWall_Should_BecomeACornerAndFrameNothingBeside_When_ThePressOnItsFinishedFrontIsHeld()
    {
        (BlockPos cell, ITestPlayer player) = await RaiseFinishedDeckedWall();

        await WallBuilder.Press(World, player, cell, "game:plank-oak", BlockFacing.WEST, NorthEnd, held: true);

        Assert.Equal(("cornerout", "air"), (World.BlockAt(cell).Variant["layout"], World.BlockAt(cell.WestCopy()).Code.Path));
    }

    [AtlasScenario(FreshWorld = true)]
    public async Task FilledWall_Should_BecomeACorner_When_SticksArePressedOnItsUnfinishedFront()
    {
        (BlockPos cell, ITestPlayer player) = await RaiseWall("game:clay-blue");

        await WallBuilder.Press(World, player, cell, "game:stick", BlockFacing.WEST, NorthEnd);

        Assert.Equal(("cornerout", "north,west", null, ("oak", "clay", null, null, null)), Describe(cell));
    }

    [AtlasScenario(FreshWorld = true)]
    public async Task GlazedWall_Should_TakePlanksAsItsFrontFinishKeepingItsGlass_When_PlanksArePressedOnItsFront()
    {
        (BlockPos cell, ITestPlayer player) = await RaiseWall(infill: null);
        await WallBuilder.Press(World, player, cell, "game:glass-plain", BlockFacing.WEST, NorthEnd, quantity: 1);

        await WallBuilder.Press(World, player, cell, "game:plank-oak", BlockFacing.WEST, NorthEnd);

        Assert.Equal(("wall", "west", null, ("oak", "glass-plain", "planks", null, null)), Describe(cell));
    }

    [AtlasScenario(FreshWorld = true)]
    public async Task GlazedWall_Should_BecomeACornerKeepingItsGlassAndFinish_When_ItsFinishedFrontIsPressedAtTheLeftEnd()
    {
        (BlockPos cell, ITestPlayer player) = await RaiseWall(infill: null);
        await WallBuilder.Press(World, player, cell, "game:glass-plain", BlockFacing.WEST, NorthEnd, quantity: 1);
        await WallBuilder.Press(World, player, cell, "game:plank-oak", BlockFacing.WEST, NorthEnd);

        await WallBuilder.Press(World, player, cell, "game:plank-oak", BlockFacing.WEST, NorthEnd);

        Assert.Equal(("cornerout", "north,west", null, ("oak", "glass-plain", "planks", null, null)), Describe(cell));
    }

    // Vanilla repeats a held right-click, and the repeat of the press that boarded the face finds it finished.
    [AtlasScenario(FreshWorld = true)]
    public async Task FilledWall_Should_StayAWall_When_ThePressThatBoardsItsFaceIsHeld()
    {
        (BlockPos cell, ITestPlayer player) = await RaiseWall("game:clay-blue");

        await WallBuilder.Press(World, player, cell, "game:plank-oak", BlockFacing.EAST, SouthEnd, held: true);

        Assert.Equal(("wall", "west", null, ("oak", "clay", null, null, "planks")), Describe(cell));
    }

    [AtlasScenario(FreshWorld = true)]
    public async Task BareFrame_Should_StayAWall_When_ThePressThatFillsItWithSticksIsHeld()
    {
        (BlockPos cell, ITestPlayer player) = await RaiseWall(infill: null);

        await WallBuilder.Press(World, player, cell, "game:stick", BlockFacing.WEST, SouthEnd, quantity: 8, held: true);

        Assert.Equal(("wall", "west", null, ("oak", "wattle", null, null, null)), Describe(cell));
    }

    // Sneaking skips the wall's own interaction, and still frames the next cell (decision 0058).
    [AtlasScenario(FreshWorld = true)]
    public async Task FilledWall_Should_StayAWallBesideANewCorner_When_ItsFinishedFrontIsSneakPressed()
    {
        (BlockPos cell, ITestPlayer player) = await RaiseFinishedDeckedWall();
        player.Entity.Controls.ShiftKey = true;

        await WallBuilder.Press(World, player, cell, "game:plank-oak", BlockFacing.WEST, SouthEnd);

        Assert.Equal(("wall", "cornerout"), (World.BlockAt(cell).Variant["layout"], World.BlockAt(cell.WestCopy()).Variant["layout"]));
    }

    [AtlasScenario(FreshWorld = true)]
    public async Task FilledWall_Should_StayAWall_When_ItsBareDeckIsPressedWithPlanks()
    {
        (BlockPos cell, ITestPlayer player) = await RaiseFinishedDeckedWall();

        await WallBuilder.Press(World, player, cell, "game:plank-oak", BlockFacing.EAST, SouthEnd, onDeck: true);

        Assert.Equal("wall", World.BlockAt(cell).Variant["layout"]);
    }

    [AtlasScenario(FreshWorld = true)]
    public async Task FilledWall_Should_StayAWall_When_ItCarriesAStep()
    {
        (BlockPos cell, ITestPlayer player) = await RaiseWall("game:clay-blue", back: "game:plank-birch");
        await WallBuilder.Press(World, player, cell, "game:plankstairs-oak-up-north-free", BlockFacing.EAST, SouthEnd, quantity: 1);

        await WallBuilder.Press(World, player, cell, "game:plank-oak", BlockFacing.EAST, SouthEnd);

        Assert.Equal(("wall", "game:plankstairs-oak-up-north-free"), (World.BlockAt(cell).Variant["layout"], Read(cell, "step")));
    }

    // The corner's west leg is clicked from the west and takes the stair standing east of the cell.
    [AtlasScenario(FreshWorld = true)]
    public async Task Corner_Should_TakeAStepFromTheStairBesideItsWestLeg_When_AStairIsPressedOnThatLeg()
    {
        (BlockPos cell, ITestPlayer player) = await RaiseCorner();
        World.SetBlock("game:plankstairs-oak-down-south-free", cell.EastCopy());

        await WallBuilder.Press(World, player, cell, "game:plankstairs-oak-up-north-free", BlockFacing.WEST, new Vec3d(0, 0.5, 0.5), quantity: 1);

        Assert.Equal(("cornerout", "game:plankstairs-oak-up-north-free", "down-south"), (World.BlockAt(cell).Variant["layout"], Read(cell, "step"), Read(cell, "steporientation")));
    }

    [AtlasScenario(FreshWorld = true)]
    public async Task Corner_Should_TakeAStepFromTheStairBesideItsNorthLeg_When_AStairIsPressedOnThatLeg()
    {
        (BlockPos cell, ITestPlayer player) = await RaiseCorner();
        World.SetBlock("game:plankstairs-oak-down-east-free", cell.SouthCopy());

        await WallBuilder.Press(World, player, cell, "game:plankstairs-oak-up-north-free", BlockFacing.NORTH, new Vec3d(0.5, 0.5, 0), quantity: 1);

        Assert.Equal(("cornerout", "game:plankstairs-oak-up-north-free", "down-east"), (World.BlockAt(cell).Variant["layout"], Read(cell, "step"), Read(cell, "steporientation")));
    }

    private async Task<(BlockPos Cell, ITestPlayer Player)> RaiseCorner()
    {
        (BlockPos cell, ITestPlayer player) = await RaiseWall("game:clay-blue");
        await WallBuilder.Press(World, player, cell, "game:stick", BlockFacing.WEST, NorthEnd);
        Assert.Equal(("cornerout", "north,west"), (World.BlockAt(cell).Variant["layout"], Describe(cell).Retaining));
        return (cell, player);
    }

    [AtlasScenario(FreshWorld = true)]
    public async Task FilledWall_Should_StayAWall_When_AnAnimalStandsWhereTheNewLegGoes()
    {
        (BlockPos cell, ITestPlayer player) = await RaiseWall("game:clay-blue", back: "game:plank-birch");
        World.SpawnEntity("game:sheep-bighorn-adult-female", cell);
        await World.Ticks(5);

        await WallBuilder.Press(World, player, cell, "game:plank-oak", BlockFacing.EAST, SouthEnd);

        Assert.Equal("wall", World.BlockAt(cell).Variant["layout"]);
    }

    private async Task<(BlockPos Cell, ITestPlayer Player)> RaiseFinishedDeckedWall()
    {
        (BlockPos cell, ITestPlayer player) = await RaiseWall("game:clay-blue", back: "game:plank-birch", deck: true);
        await WallBuilder.Press(World, player, cell, "game:plank-birch", BlockFacing.WEST, SouthEnd);
        return (cell, player);
    }

    private async Task<(BlockPos Cell, ITestPlayer Player)> RaiseWall(string? infill, string? back = null, bool deck = false)
    {
        BlockPos cell = World.Spawn.Offset(1, 2, 0);
        World.SetBlock("game:planks-aged-ud", cell.DownCopy());
        ITestPlayer player = await WallBuilder.JoinBuilder(World);
        if (deck) player.Entity.WatchedAttributes.SetString("vssidingDeck", "deck");
        await WallBuilder.Raise(World, player, cell, cell.WestCopy(), "game:plank-oak", infill, back, framingCost: deck ? 4 : 2);
        player.Entity.WatchedAttributes.SetString("vssidingFraming", "corner");
        return (cell, player);
    }

    private (string Layout, string Retaining, string? Deck, (string? Framing, string? Infill, string? Front, string? SecondFront, string? Back) Layers) Describe(BlockPos cell)
    {
        Block wall = World.BlockAt(cell);
        string retaining = string.Join(",", BlockFacing.HORIZONTALS
            .Where(face => wall.GetRetention(cell, face, EnumRetentionType.Heat) != 0)
            .Select(face => face.Code)
            .OrderBy(face => face));
        return (wall.Variant["layout"], retaining, Read(cell, "deck"), WallBuilder.Layers(World, cell));
    }

    private string? Read(BlockPos cell, string key)
    {
        var tree = new TreeAttribute();
        World.BlockEntityAt<BlockEntity>(cell)!.ToTreeAttributes(tree);
        return tree.GetString(key) is { Length: > 0 } value ? value : null;
    }
}
