using System.Threading.Tasks;
using Atlas.Api;
using Atlas.XUnit;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Xunit;

namespace VSSiding.E2E.Tests;

// Grass and dirt are finishes for a floor's or a deck's top only (decision 0104). A click the block answers false goes
// on to vanilla placement; a refusal answers true.
[AtlasWorld(StrictBootDiagnostics = true)]
public class SoilScenarios : AtlasScenarioBase
{
    private static readonly Vec3d TopHit = new(0.5, 1, 0.5);

    [AtlasScenario(RollbackWorld = true, StrictIsolation = true)]
    public async Task Floor_Should_TakeGrassOnItsTop_When_SoilIsPressedOnIt()
    {
        (BlockPos cell, ITestPlayer player) = await Raise("floor");

        await WallBuilder.Press(World, player, cell, "game:soil-medium-none", BlockFacing.UP, TopHit, quantity: 3);

        Assert.Equal((("oak", "clay", "grass", null, (string?)null), 2), (WallBuilder.Layers(World, cell), Held(player)));
    }

    [AtlasScenario(RollbackWorld = true, StrictIsolation = true)]
    public async Task Floor_Should_PassTheClickOn_When_SoilIsPressedOnItsUnderside()
    {
        (BlockPos cell, ITestPlayer player) = await Raise("floor");

        bool claimed = await Click(cell, player, "game:soil-medium-none", BlockFacing.DOWN, new Vec3d(0.5, 0, 0.5));

        Assert.Equal((false, ("oak", "clay", (string?)null, (string?)null, (string?)null)), (claimed, WallBuilder.Layers(World, cell)));
    }

    // A finish on a finished top goes to the underside (decision 0075), which soil does not take.
    [AtlasScenario(RollbackWorld = true, StrictIsolation = true)]
    public async Task Floor_Should_PassTheClickOn_When_SoilIsPressedOnAGrassedTop()
    {
        (BlockPos cell, ITestPlayer player) = await Raise("floor");
        await Click(cell, player, "game:soil-medium-none", BlockFacing.UP, TopHit);

        bool claimed = await Click(cell, player, "game:soil-medium-none", BlockFacing.UP, TopHit);

        Assert.Equal((false, ("oak", "clay", "grass", (string?)null, (string?)null)), (claimed, WallBuilder.Layers(World, cell)));
    }

    [AtlasScenario(RollbackWorld = true, StrictIsolation = true)]
    public async Task Wall_Should_RefuseSoil_When_ItIsPressedOnItsFace()
    {
        (BlockPos cell, ITestPlayer player) = await Raise("wall");

        bool claimed = await Click(cell, player, "game:soil-low-none", BlockFacing.EAST, new Vec3d(0.5, 0.5, 0.5));

        Assert.Equal((true, ("oak", "clay", (string?)null, (string?)null, (string?)null), 1), (claimed, WallBuilder.Layers(World, cell), Held(player)));
    }

    private async Task<(BlockPos Cell, ITestPlayer Player)> Raise(string layout)
    {
        BlockPos cell = World.Spawn.Offset(1, 2, 0);
        World.SetBlock("game:planks-aged-ud", cell.DownCopy());
        ITestPlayer player = await WallBuilder.JoinBuilder(World);
        player.Entity.WatchedAttributes.SetString("vssidingFraming", layout);
        await WallBuilder.Raise(World, player, cell, cell.WestCopy(), "game:plank-oak", "game:clay-blue");
        return (cell, player);
    }

    private async Task<bool> Click(BlockPos cell, ITestPlayer player, string item, BlockFacing face, Vec3d hit)
    {
        player.Player.InventoryManager.ActiveHotbarSlot.Itemstack = null;
        await player.GiveItem(item, 1);
        return World.BlockAt(cell).OnBlockInteractStart(player.Entity.World, player.Player, new BlockSelection { Position = cell.Copy(), Face = face, HitPosition = hit });
    }

    private static int Held(ITestPlayer player)
        => player.Player.InventoryManager.ActiveHotbarSlot.Itemstack?.StackSize ?? 0;
}
