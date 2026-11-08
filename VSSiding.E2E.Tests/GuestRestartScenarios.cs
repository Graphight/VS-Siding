using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Atlas.Api;
using Atlas.XUnit;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Xunit;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace VSSiding.E2E.Tests;

internal class MethodNameOrderer : ITestCaseOrderer
{
    public IEnumerable<TTestCase> OrderTestCases<TTestCase>(IEnumerable<TTestCase> testCases) where TTestCase : ITestCase
        => testCases.OrderBy(testCase => testCase.TestMethod.Method.Name, StringComparer.Ordinal);
}

// A guest wall survives a restart (0035) and comes back when its chunk loads (#64).
// A restart needs no joined player and runs before a scenario, so each pair is one scenario split in two: A_ or C_ builds, the next name checks.
// Kept out of the rollback classes (0091): a restart boots a new host, whose next rollback scenario would snapshot the world the build left.
[TestCaseOrderer("VSSiding.E2E.Tests.MethodNameOrderer", "VSSiding.E2E.Tests")]
[AtlasWorld(StrictBootDiagnostics = true)]
public class GuestRestartScenarios : AtlasScenarioBase
{
    private static readonly (string?, string?, string?, string?, string?) Expected = ("oak", "clay", null, null, "planks");

    private BlockPos Cell => World.Spawn.Offset(1, 2, 0);

    [AtlasScenario(FreshWorld = true)]
    public async Task A_Wall_Should_BecomeAGuest_When_AChestIsPlacedInItsCell()
    {
        BlockPos cell = Cell;
        World.SetBlock("game:planks-aged-ud", cell.DownCopy());

        ITestPlayer player = await WallBuilder.JoinBuilder(World);
        try
        {
            await WallBuilder.Raise(World, player, cell, cell.WestCopy(), "game:plank-oak", "game:clay-blue", "game:plank-oak");
            Assert.Equal(Expected, WallBuilder.Layers(World, cell));
            Assert.Null(WallBuilder.GuestLayers(World, cell));

            // A saw in the off hand layers the wall instead of hosting (SidingWallBlock.OnBlockInteractStart).
            player.Entity.LeftHandItemSlot.Itemstack = null;
            await player.GiveItem("game:chest-east", 1);
            var selection = new BlockSelection { Position = cell.Copy(), Face = BlockFacing.EAST, HitPosition = new Vec3d(0.5, 0.5, 0.5) };
            Assert.True(World.BlockAt(cell).OnBlockInteractStart(player.Entity.World, player.Player, selection));

            Assert.StartsWith("chest", World.BlockAt(cell).Code.Path);
            Assert.Equal(Expected, WallBuilder.GuestLayers(World, cell));
        }
        finally
        {
            player.Player.Disconnect();
            await World.Until(() => !player.IsConnected);
        }
    }

    [AtlasScenario(RestartWorld = true)]
    public Task B_Wall_Should_StayAGuest_When_TheWorldRestarts()
    {
        BlockPos cell = Cell;

        Assert.StartsWith("chest", World.BlockAt(cell).Code.Path);
        Assert.Equal(Expected, WallBuilder.GuestLayers(World, cell));
        Assert.Null(WallBuilder.GuestLayers(World, cell.NorthCopy()));
        return Task.CompletedTask;
    }

    // The state #64's save was found in: air in the cell, the wall still in the guest store, and no change at the cell left to restore it.
    // Built directly, since every vanilla path tried restores the wall when its host goes.
    [AtlasScenario(FreshWorld = true)]
    public async Task C_Wall_Should_BeStranded_When_ItsGuestOutlivesItsHost()
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
        guestWalls.GetMethod("Set")!.Invoke(null, new[] { World.Api.World, World.Api.World.BlockAccessor.GetChunkAtBlockPos(cell), cell, record, true });
        await World.Ticks(5);

        Assert.Equal("air", World.BlockAt(cell).Code.ToShortString());
        Assert.Equal(Expected, WallBuilder.GuestLayers(World, cell));

        player.Player.Disconnect();
        await World.Until(() => !player.IsConnected);
    }

    [AtlasScenario(RestartWorld = true)]
    public async Task D_Wall_Should_ComeBack_When_ItsChunkLoads()
    {
        BlockPos cell = Cell;

        await World.Until(() => World.BlockAt(cell).Code.Path.StartsWith("wall"), 100);
        Assert.Equal(Expected, WallBuilder.Layers(World, cell));
        Assert.Null(WallBuilder.GuestLayers(World, cell));
    }
}
