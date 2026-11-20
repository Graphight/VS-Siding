using System.Threading.Tasks;
using Atlas.Api;
using Atlas.XUnit;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Xunit;

namespace VSSiding.E2E.Tests;

// Grass and dirt are finishes for a floor's or a deck's top only (decision 0104).
[AtlasWorld(StrictBootDiagnostics = true)]
public class SoilScenarios : AtlasScenarioBase
{
    private static readonly Vec3d TopHit = new(0.5, 1, 0.5);

    [AtlasScenario(RollbackWorld = true, StrictIsolation = true)]
    public async Task Floor_Should_TakeGrassOnItsTop_When_SoilIsPressedOnIt()
    {
        (BlockPos cell, ITestPlayer player) = await RaiseFloor();

        await WallBuilder.Press(World, player, cell, "game:soil-medium-none", BlockFacing.UP, TopHit, quantity: 3);

        Assert.Equal((("oak", "clay", "grass", null, (string?)null), 2), (WallBuilder.Layers(World, cell), Held(player)));
    }

    [AtlasScenario(RollbackWorld = true, StrictIsolation = true)]
    public async Task Floor_Should_TakeNoSoil_When_ItIsPressedOnItsUnderside()
    {
        (BlockPos cell, ITestPlayer player) = await RaiseFloor();

        await WallBuilder.Press(World, player, cell, "game:soil-medium-none", BlockFacing.DOWN, new Vec3d(0.5, 0, 0.5), quantity: 3);

        Assert.Equal(("oak", "clay", (string?)null, (string?)null, (string?)null), WallBuilder.Layers(World, cell));
    }

    [AtlasScenario(RollbackWorld = true, StrictIsolation = true)]
    public async Task Wall_Should_TakeNoSoil_When_ItIsPressedOnItsFace()
    {
        BlockPos cell = World.Spawn.Offset(1, 2, 0);
        World.SetBlock("game:planks-aged-ud", cell.DownCopy());
        ITestPlayer player = await WallBuilder.JoinBuilder(World);
        await WallBuilder.Raise(World, player, cell, cell.WestCopy(), "game:plank-oak", "game:clay-blue");

        await WallBuilder.Press(World, player, cell, "game:soil-low-none", BlockFacing.EAST, new Vec3d(0.5, 0.5, 0.5), quantity: 3);

        Assert.Equal((("oak", "clay", (string?)null, (string?)null, (string?)null), 3), (WallBuilder.Layers(World, cell), Held(player)));
    }

    [AtlasScenario(RollbackWorld = true, StrictIsolation = true)]
    public async Task Diagonal_Should_TakeDirtOnItsOuterDeck_When_SoilIsPressedOnTheDecksTop()
    {
        BlockPos cell = World.Spawn.Offset(1, 2, 0);
        World.SetBlock("game:planks-aged-ud", cell.DownCopy());
        ITestPlayer player = await WallBuilder.JoinBuilder(World);
        player.Entity.WatchedAttributes.SetString("vssidingFraming", "diagonal");
        await WallBuilder.Raise(World, player, cell, cell.WestCopy(), "game:plank-oak", null);
        player.Entity.WatchedAttributes.SetString("vssidingDeck", "deck");
        await player.GiveItem("game:plank-oak", 1);
        World.BlockAt(cell).OnBlockInteractStart(player.Entity.World, player.Player,
            new BlockSelection { Position = cell.Copy(), Face = BlockFacing.FromCode(World.BlockAt(cell).Variant["side"]), HitPosition = new Vec3d(0.5, 0.5, 0.5) });
        var stripTop = new BlockSelection { Position = cell.Copy(), Face = BlockFacing.UP, HitPosition = TopHit, SelectionBoxIndex = World.BlockAt(cell).SelectionBoxes.Length };
        await player.GiveItem("game:clay-blue", 2);
        World.BlockAt(cell).OnBlockInteractStart(player.Entity.World, player.Player, stripTop);

        await player.GiveItem("game:soil-low-none", 1);
        World.BlockAt(cell).OnBlockInteractStart(player.Entity.World, player.Player, stripTop);

        var tree = new TreeAttribute();
        World.BlockEntityAt<BlockEntity>(cell)!.ToTreeAttributes(tree);
        Assert.Equal(("dirt", (ItemStack?)null), (tree.GetString("outerdeckfront"), player.Player.InventoryManager.ActiveHotbarSlot.Itemstack));
    }

    private async Task<(BlockPos Cell, ITestPlayer Player)> RaiseFloor()
    {
        BlockPos cell = World.Spawn.Offset(1, 2, 0);
        World.SetBlock("game:planks-aged-ud", cell.DownCopy());
        ITestPlayer player = await WallBuilder.JoinBuilder(World);
        player.Entity.WatchedAttributes.SetString("vssidingFraming", "floor");
        await WallBuilder.Raise(World, player, cell, cell.WestCopy(), "game:plank-oak", "game:clay-blue");
        return (cell, player);
    }

    private static int Held(ITestPlayer player)
        => player.Player.InventoryManager.ActiveHotbarSlot.Itemstack?.StackSize ?? 0;
}
