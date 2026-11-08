using System;
using System.Threading.Tasks;
using Atlas.Api;
using Atlas.XUnit;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Xunit;

namespace VSSiding.E2E.Tests;

// A guest wall comes back whichever way its host goes (#64).
[AtlasWorld(StrictBootDiagnostics = true)]
public class GuestRestoreScenarios : AtlasScenarioBase
{
    private static readonly (string?, string?, string?, string?, string?) Expected = ("oak", "clay", null, null, "planks");

    private BlockPos Cell => World.Spawn.Offset(1, 2, 0);

    [AtlasScenario(RollbackWorld = true, StrictIsolation = true)]
    public async Task Wall_Should_ComeBack_When_APlayerBreaksItsChest()
    {
        var (cell, player) = await HostChest();

        World.Api.World.BlockAccessor.BreakBlock(cell, player.Player);

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
