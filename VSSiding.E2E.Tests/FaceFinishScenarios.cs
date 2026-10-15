using System.Collections.Generic;
using System.Linq;
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

        Assert.Equal(("vssiding:facefinish-oak@0", 2), (Decors(cell), HeldPlanks(player)));
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

        Assert.Equal(("", 4), (Decors(cell, BlockFacing.WEST), HeldPlanks(player)));
    }

    [AtlasScenario(FreshWorld = true)]
    public async Task Log_Should_SwapItsFaceFinishAndDropTheOldPlanks_When_AnotherWoodIsPressedOnItsSkinnedTop()
    {
        (BlockPos cell, ITestPlayer player) = await RaiseLog();
        await Press(player, cell, "game:plank-oak", 4);

        await Press(player, cell, "game:plank-birch", 4);
        await World.Ticks(2);

        Assert.Equal(("vssiding:facefinish-birch@0", 2, 2), (Decors(cell), HeldPlanks(player), GroundPlanks(cell, "game:plank-oak")));
    }

    [AtlasScenario(FreshWorld = true)]
    public async Task Log_Should_TurnItsFaceFinishForFree_When_ItsOwnWoodIsPressedWithAnotherBoardsPicked()
    {
        (BlockPos cell, ITestPlayer player) = await RaiseLog();
        await Press(player, cell, "game:plank-oak", 4);
        player.Entity.WatchedAttributes.SetString("vssidingBoards", "boards");

        await Press(player, cell, "game:plank-oak", 4);
        await World.Ticks(2);

        Assert.Equal(("vssiding:facefinish-oak@1", 4, 0), (Decors(cell), HeldPlanks(player), GroundPlanks(cell, "game:plank-oak")));
    }

    [AtlasScenario(FreshWorld = true)]
    public async Task Log_Should_KeepItsFaceFinishAndSpendNothing_When_ItsOwnWoodIsPressedWithTheSameBoards()
    {
        (BlockPos cell, ITestPlayer player) = await RaiseLog();
        await Press(player, cell, "game:plank-oak", 4);

        await Press(player, cell, "game:plank-oak", 4);

        Assert.Equal(("vssiding:facefinish-oak@0", 4), (Decors(cell), HeldPlanks(player)));
    }

    [AtlasScenario(FreshWorld = true)]
    public async Task Log_Should_KeepItsWallpaperAndSpendNothing_When_PlanksArePressedOnIt()
    {
        (BlockPos cell, ITestPlayer player) = await RaiseLog();
        World.Api.World.BlockAccessor.SetDecor(World.Api.World.GetBlock(new AssetLocation("game:wallpaper-brown")), cell, new DecorBits(BlockFacing.UP));

        await Press(player, cell, "game:plank-oak", 4);

        Assert.Equal(("game:wallpaper-brown@0", 4), (Decors(cell), HeldPlanks(player)));
    }

    [AtlasScenario(FreshWorld = true)]
    public async Task Log_Should_TakeNoFaceFinish_When_SticksArePressedWithFacePicked()
    {
        (BlockPos cell, ITestPlayer player) = await RaiseLog();

        await Press(player, cell, "game:stick", 4);

        Assert.Equal("", Decors(cell));
    }

    private async Task<(BlockPos Cell, ITestPlayer Player)> RaiseLog()
    {
        BlockPos cell = World.Spawn.Offset(1, 2, 0);
        World.SetBlock("game:log-placed-oak-ud", cell);
        ITestPlayer player = await WallBuilder.JoinBuilder(World);
        player.Entity.WatchedAttributes.SetString("vssidingFraming", "face");
        // Away from the log, so planks it drops stay on the ground to be counted.
        await player.TeleportTo(cell.EastCopy(6).Up());
        return (cell, player);
    }

    private Task Press(ITestPlayer player, BlockPos cell, string item, int quantity)
        => WallBuilder.Press(World, player, cell, item, BlockFacing.UP, TopHit, quantity);

    // Every decor on the face as code@rotation, so a swap that left the old one under the new shows as two.
    private string Decors(BlockPos cell, BlockFacing? face = null)
        => string.Join(", ", (World.Api.World.BlockAccessor.GetSubDecors(cell) ?? new Dictionary<int, Block>())
            .Where(entry => new DecorBits(entry.Key).Face == (face ?? BlockFacing.UP).Index)
            .Select(entry => $"{entry.Value.Code}@{new DecorBits(entry.Key).Rotation}"));

    private int GroundPlanks(BlockPos cell, string code)
        => World.Api.World.GetEntitiesAround(cell.ToVec3d().Add(0.5, 0.5, 0.5), 3, 3, entity => entity is EntityItem)
            .Cast<EntityItem>().Where(item => item.Itemstack.Collectible.Code.ToString() == code).Sum(item => item.Itemstack.StackSize);

    private static int HeldPlanks(ITestPlayer player)
        => player.Player.InventoryManager.ActiveHotbarSlot.Itemstack?.StackSize ?? 0;
}
