using System.Threading.Tasks;
using Atlas.Api;
using Atlas.XUnit;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Xunit;

namespace VSSiding.E2E.Tests;

// A finish pressed on a finished face goes on the face behind it (decision 0075).
// Every wall here hugs the west face of its cell, so west is its front and east its back.
[AtlasWorld(StrictBootDiagnostics = true)]
public class FarFaceScenarios : AtlasScenarioBase
{
    private static readonly Vec3d Middle = new(0.5, 0.5, 0.5);

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
        await WallBuilder.Press(World, player, cell, "game:stick", BlockFacing.WEST, new Vec3d(0, 0.5, 0.2));
        Assert.Equal("cornerout", World.BlockAt(cell).Variant["layout"]);

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

    private async Task<(BlockPos Cell, ITestPlayer Player)> RaiseWall(string? back = "game:plank-birch")
    {
        BlockPos cell = World.Spawn.Offset(1, 2, 0);
        World.SetBlock("game:planks-aged-ud", cell.DownCopy());
        ITestPlayer player = await WallBuilder.JoinBuilder(World);
        await WallBuilder.Raise(World, player, cell, cell.WestCopy(), "game:plank-oak", "game:clay-blue", back);
        return (cell, player);
    }
}
