using System.Threading.Tasks;
using Atlas.Api;
using Atlas.XUnit;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Xunit;

namespace VSSiding.E2E.Tests;

// A plank pressed in face mode puts a vanilla decor block on the clicked face of any solid block (decision 0077).
[AtlasWorld(StrictBootDiagnostics = true)]
public class FaceFinishScenarios : AtlasScenarioBase
{
    private static readonly Vec3d TopHit = new(0.5, 1, 0.5);
    private static readonly Vec3d Middle = new(0.5, 0.5, 0.5);

    [AtlasScenario(FreshWorld = true)]
    public async Task Log_Should_TakeAFaceFinishOnItsTop_When_PlanksArePressedWithFacePicked()
    {
        (BlockPos cell, ITestPlayer player) = await RaiseLog();

        await Press(player, cell, "game:plank-oak", 4);

        Assert.Equal(("vssiding:facefinish-oak", 2), (DecorCode(cell), HeldPlanks(player)));
    }

    // A wall finished on both faces passes a plank press on to PlaceWallFrame, which leaves the wall alone.
    [AtlasScenario(FreshWorld = true)]
    public async Task Wall_Should_TakeNoFaceFinishAndSpendNothing_When_PlanksArePressedOnItsFinishedFaceWithFacePicked()
    {
        BlockPos cell = World.Spawn.Offset(1, 2, 0);
        World.SetBlock("game:planks-aged-ud", cell.DownCopy());
        ITestPlayer player = await WallBuilder.JoinBuilder(World);
        await WallBuilder.Raise(World, player, cell, cell.WestCopy(), "game:plank-oak", "game:clay-blue", "game:plank-birch");
        await WallBuilder.Press(World, player, cell, "game:plank-birch", BlockFacing.WEST, Middle);
        player.Entity.WatchedAttributes.SetString("vssidingFraming", "face");

        await WallBuilder.Press(World, player, cell, "game:plank-oak", BlockFacing.WEST, Middle);

        Assert.Equal(((string?)null, 4), (DecorCode(cell, BlockFacing.WEST), HeldPlanks(player)));
    }

    [AtlasScenario(FreshWorld = true)]
    public async Task Log_Should_KeepItsFirstFaceFinishAndSpendNothing_When_PlanksArePressedOnItsSkinnedTop()
    {
        (BlockPos cell, ITestPlayer player) = await RaiseLog();
        await Press(player, cell, "game:plank-oak", 4);

        await Press(player, cell, "game:plank-birch", 4);

        Assert.Equal(("vssiding:facefinish-oak", 4), (DecorCode(cell), HeldPlanks(player)));
    }

    [AtlasScenario(FreshWorld = true)]
    public async Task Log_Should_TakeNoFaceFinish_When_SticksArePressedWithFacePicked()
    {
        (BlockPos cell, ITestPlayer player) = await RaiseLog();

        await Press(player, cell, "game:stick", 4);

        Assert.Null(DecorCode(cell));
    }

    private async Task<(BlockPos Cell, ITestPlayer Player)> RaiseLog()
    {
        BlockPos cell = World.Spawn.Offset(1, 2, 0);
        World.SetBlock("game:log-placed-oak-ud", cell);
        ITestPlayer player = await WallBuilder.JoinBuilder(World);
        player.Entity.WatchedAttributes.SetString("vssidingFraming", "face");
        await player.TeleportTo(cell.UpCopy());
        return (cell, player);
    }

    private Task Press(ITestPlayer player, BlockPos cell, string item, int quantity)
        => WallBuilder.Press(World, player, cell, item, BlockFacing.UP, TopHit, quantity);

    private string? DecorCode(BlockPos cell, BlockFacing? face = null)
        => World.Api.World.BlockAccessor.GetDecor(cell, new DecorBits(face ?? BlockFacing.UP))?.Code.ToString();

    private static int HeldPlanks(ITestPlayer player)
        => player.Player.InventoryManager.ActiveHotbarSlot.Itemstack?.StackSize ?? 0;
}
