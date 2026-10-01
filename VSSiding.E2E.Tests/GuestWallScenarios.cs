using System;
using System.Collections.Generic;
using System.Linq;
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

// A restart needs no joined player and can only happen before a scenario, so the build is one scenario and the check another, run in name order.
[TestCaseOrderer("VSSiding.E2E.Tests.MethodNameOrderer", "VSSiding.E2E.Tests")]
[AtlasWorld(StrictBootDiagnostics = true)]
public class GuestWallScenarios : AtlasScenarioBase
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
}
