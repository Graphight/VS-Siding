using System.Linq;
using System.Threading.Tasks;
using Atlas.Api;
using Atlas.XUnit;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Xunit;

namespace VSSiding.E2E.Tests;

// A finish pressed on a finished face goes on the face behind it (decision 0075).
// Every wall here hugs the west face of its cell, so west is its front and east its back.
[AtlasWorld(StrictBootDiagnostics = true)]
public class FarFaceScenarios : AtlasScenarioBase
{
    private static readonly Vec3d Middle = new(0.5, 0.5, 0.5);
    private static readonly Vec3d TopHit = new(0.5, 1, 0.5);
    private static readonly Vec3d NorthEnd = new(0, 0.5, 0.2);

    [AtlasScenario(FreshWorld = true)]
    public async Task Wall_Should_TakeDaubOnItsFront_When_DaubIsPressedOnItsFinishedBack()
    {
        (BlockPos cell, ITestPlayer player) = await RaiseWall();

        await WallBuilder.Press(World, player, cell, "game:daubraw-browngolden", BlockFacing.EAST, Middle);

        Assert.Equal(("oak", "clay", "daub", null, "planks-birch"), WallBuilder.Layers(World, cell));
    }

    [AtlasScenario(FreshWorld = true)]
    public async Task Wall_Should_TakePlanksOnItsFront_When_PlanksArePressedOnItsFinishedBackWithWallPicked()
    {
        (BlockPos cell, ITestPlayer player) = await RaiseWall();
        player.Entity.WatchedAttributes.SetString("vssidingFraming", "wall");

        await WallBuilder.Press(World, player, cell, "game:plank-oak", BlockFacing.EAST, Middle);

        Assert.Equal(("oak", "clay", "planks", null, "planks-birch"), WallBuilder.Layers(World, cell));
    }

    [AtlasScenario(FreshWorld = true)]
    public async Task Corner_Should_ReachItsFrontAndSecondFront_When_DaubIsPressedOnItsTwoFinishedBacks()
    {
        (BlockPos cell, ITestPlayer player) = await RaiseWall();
        player.Entity.WatchedAttributes.SetString("vssidingFraming", "corner");
        await WallBuilder.Press(World, player, cell, "game:stick", BlockFacing.WEST, NorthEnd);

        await WallBuilder.Press(World, player, cell, "game:daubraw-browngolden", BlockFacing.EAST, Middle);
        await WallBuilder.Press(World, player, cell, "game:daubraw-browngolden", BlockFacing.SOUTH, Middle);

        Assert.Equal(("oak", "clay", "daub", "daub", "planks-birch"), WallBuilder.Layers(World, cell));
    }

    [AtlasScenario(FreshWorld = true)]
    public async Task Wall_Should_FinishOnlyTheClickedFace_When_PlanksAreHeldOnItsBareFace()
    {
        (BlockPos cell, ITestPlayer player) = await RaiseWall(back: null);

        await WallBuilder.Press(World, player, cell, "game:plank-oak", BlockFacing.EAST, Middle, held: true);

        Assert.Equal(("oak", "clay", null, null, "planks"), WallBuilder.Layers(World, cell));
    }

    [AtlasScenario(FreshWorld = true)]
    public async Task Floor_Should_TakeDaubOnItsUnderside_When_DaubIsPressedOnItsBoardedTop()
    {
        (BlockPos cell, ITestPlayer player) = await RaiseBoardedFloor();

        await WallBuilder.Press(World, player, cell, "game:daubraw-browngolden", BlockFacing.UP, TopHit);

        Assert.Equal(("oak", "clay", "planks", null, "daub"), WallBuilder.Layers(World, cell));
    }

    [AtlasScenario(FreshWorld = true)]
    public async Task Floor_Should_ExtendItsRunAndLeaveItsUndersideBare_When_PlanksArePressedOnItsBoardedTop()
    {
        (BlockPos cell, ITestPlayer player) = await RaiseBoardedFloor();

        await WallBuilder.Press(World, player, cell, "game:plank-oak", BlockFacing.UP, TopHit);

        int floorsBeside = BlockFacing.HORIZONTALS.Count(face => World.BlockAt(cell.AddCopy(face)).Code.Path == "floor");
        Assert.Equal((("oak", "clay", "planks", null, (string?)null), 1), (WallBuilder.Layers(World, cell), floorsBeside));
    }

    [AtlasScenario(FreshWorld = true)]
    public async Task Floor_Should_TakePlanksOnItsUnderside_When_PlanksArePressedOnItsBoardedTopAndItsRunCannotExtend()
    {
        (BlockPos cell, ITestPlayer player) = await RaiseBoardedFloor();
        foreach (BlockFacing face in BlockFacing.HORIZONTALS) World.SetBlock("game:planks-aged-ud", cell.AddCopy(face));

        await WallBuilder.Press(World, player, cell, "game:plank-oak", BlockFacing.UP, TopHit);

        Assert.Equal(("oak", "clay", "planks", null, "planks"), WallBuilder.Layers(World, cell));
    }

    [AtlasScenario(FreshWorld = true)]
    public async Task Deck_Should_TakeDaubOnItsUnderside_When_DaubIsPressedOnItsBoardedTop()
    {
        (BlockPos cell, ITestPlayer player) = await RaiseWall(back: null);
        player.Entity.WatchedAttributes.SetString("vssidingDeck", "deck");
        await WallBuilder.Press(World, player, cell, "game:plank-oak", BlockFacing.EAST, Middle, quantity: 2);
        await WallBuilder.Press(World, player, cell, "game:clay-blue", BlockFacing.UP, TopHit, onDeck: true);
        player.Entity.WatchedAttributes.SetString("vssidingFraming", "floor");
        await WallBuilder.Press(World, player, cell, "game:plank-oak", BlockFacing.UP, TopHit, quantity: 2, onDeck: true);

        await WallBuilder.Press(World, player, cell, "game:daubraw-browngolden", BlockFacing.UP, TopHit, onDeck: true);

        var tree = new TreeAttribute();
        World.BlockEntityAt<BlockEntity>(cell)!.ToTreeAttributes(tree);
        Assert.Equal(("planks", "daub"), (tree.GetString("deckfront"), tree.GetString("deckback")));
    }

    private async Task<(BlockPos Cell, ITestPlayer Player)> RaiseBoardedFloor()
    {
        BlockPos cell = World.Spawn.Offset(1, 2, 0);
        World.SetBlock("game:planks-aged-ud", cell.DownCopy());
        ITestPlayer player = await WallBuilder.JoinBuilder(World);
        player.Entity.WatchedAttributes.SetString("vssidingFraming", "floor");
        await WallBuilder.Raise(World, player, cell, cell.WestCopy(), "game:plank-oak", "game:clay-blue");
        await WallBuilder.Press(World, player, cell, "game:plank-oak", BlockFacing.UP, TopHit, quantity: 2);
        return (cell, player);
    }

    private async Task<(BlockPos Cell, ITestPlayer Player)> RaiseWall(string? back = "game:plank-birch")
    {
        BlockPos cell = World.Spawn.Offset(1, 2, 0);
        World.SetBlock("game:planks-aged-ud", cell.DownCopy());
        ITestPlayer player = await WallBuilder.JoinBuilder(World);
        await WallBuilder.Raise(World, player, cell, cell.WestCopy(), "game:plank-oak", "game:clay-blue", back);
        return (cell, player);
    }
}
