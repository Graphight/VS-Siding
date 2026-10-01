using System;
using System.Reflection;
using System.Threading.Tasks;
using Atlas.Api;
using Atlas.XUnit;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Xunit;

namespace VSSiding.E2E.Tests;

// A guest wall comes back whichever way its host goes (#64). The A/B pair is one scenario split by a restart, as in GuestWallScenarios.
[TestCaseOrderer("VSSiding.E2E.Tests.MethodNameOrderer", "VSSiding.E2E.Tests")]
[AtlasWorld(StrictBootDiagnostics = true)]
public class GuestRestoreScenarios : AtlasScenarioBase
{
    private static readonly (string?, string?, string?, string?, string?) Expected = ("oak", "clay", null, null, "planks");

    private BlockPos Cell => World.Spawn.Offset(1, 2, 0);

    // The state #64's save was found in: air in the cell, the wall still in the guest store, and no change at the cell left to restore it.
    // Built directly, since every vanilla path tried restores the wall when its host goes.
    [AtlasScenario(FreshWorld = true)]
    public async Task A_Wall_Should_BeStranded_When_ItsGuestOutlivesItsHost()
    {
        BlockPos cell = Cell;
        World.SetBlock("game:planks-aged-ud", cell.DownCopy());
        ITestPlayer player = await WallBuilder.JoinBuilder(World);
        await WallBuilder.Raise(World, player, cell, cell.WestCopy(), "game:plank-oak", "game:clay-blue", "game:plank-oak");

        Type guestWalls = World.Api.ModLoader.GetModSystem("VSSiding.SidingModSystem").GetType().Assembly.GetType("VSSiding.GuestWalls")!;
        object record = guestWalls.GetMethod("Encode", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, new object[] { World.BlockEntityAt<BlockEntity>(cell)! })!;
        World.Api.World.BlockAccessor.SetBlock(0, cell);
        // After the cell's own neighbour update, which would otherwise restore the wall straight away.
        await World.Ticks(5);
        guestWalls.GetMethod("Set")!.Invoke(null, new[] { World.Api.World, World.Api.World.BlockAccessor.GetChunkAtBlockPos(cell), cell, record });
        await World.Ticks(5);

        Assert.Equal("air", World.BlockAt(cell).Code.ToShortString());
        Assert.Equal(Expected, WallBuilder.GuestLayers(World, cell));

        player.Player.Disconnect();
        await World.Until(() => !player.IsConnected);
    }

    [AtlasScenario(RestartWorld = true)]
    public async Task B_Wall_Should_ComeBack_When_ItsChunkLoads()
    {
        BlockPos cell = Cell;

        await World.Until(() => World.BlockAt(cell).Code.Path.StartsWith("wall"), 100);
        Assert.Equal(Expected, WallBuilder.Layers(World, cell));
        Assert.Null(WallBuilder.GuestLayers(World, cell));
    }

    [AtlasScenario(FreshWorld = true)]
    public async Task Wall_Should_ComeBack_When_APlayerBreaksItsChest()
    {
        var (cell, player) = await HostChest();

        World.Api.World.BlockAccessor.BreakBlock(cell, player.Player);

        await World.Until(() => World.BlockAt(cell).Code.Path.StartsWith("wall"), 100);
        Assert.Equal(Expected, WallBuilder.Layers(World, cell));
        Assert.Null(WallBuilder.GuestLayers(World, cell));
    }

    // Callers such as EntityElevator clear a cell through a BlockPos they keep and move on.
    [AtlasScenario(FreshWorld = true)]
    public async Task Wall_Should_ComeBack_When_ItsChestIsClearedThroughAReusedPosition()
    {
        var (cell, _) = await HostChest();

        BlockPos reused = cell.Copy();
        World.Api.World.BlockAccessor.SetBlock(0, reused);
        reused.Y += 5;

        await World.Until(() => World.BlockAt(cell).Code.Path.StartsWith("wall"), 100);
        Assert.Equal(Expected, WallBuilder.Layers(World, cell));
        Assert.Null(WallBuilder.GuestLayers(World, cell));
    }

    private async Task<(BlockPos Cell, ITestPlayer Player)> HostChest()
    {
        BlockPos cell = Cell;
        World.SetBlock("game:planks-aged-ud", cell.DownCopy());

        ITestPlayer player = await WallBuilder.JoinBuilder(World);
        await WallBuilder.Raise(World, player, cell, cell.WestCopy(), "game:plank-oak", "game:clay-blue", "game:plank-oak");

        // A saw in the off hand layers the wall instead of hosting (SidingWallBlock.OnBlockInteractStart).
        player.Entity.LeftHandItemSlot.Itemstack = null;
        await player.GiveItem("game:chest-east", 1);
        var selection = new BlockSelection { Position = cell.Copy(), Face = BlockFacing.EAST, HitPosition = new Vec3d(0.5, 0.5, 0.5) };
        Assert.True(World.BlockAt(cell).OnBlockInteractStart(player.Entity.World, player.Player, selection));
        Assert.Equal(Expected, WallBuilder.GuestLayers(World, cell));
        return (cell, player);
    }
}
