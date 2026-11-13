using System;
using System.Threading.Tasks;
using Atlas.Api;
using Atlas.XUnit;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Xunit;

namespace VSSiding.E2E.Tests;

// Every layer a click adds to a wall or floor needs build rights in a land claim (decision 0098). Each scenario
// builds on open land, then presses the next layer on inside another player's claim.
[AtlasWorld(StrictBootDiagnostics = true)]
public class ClaimScenarios : AtlasScenarioBase
{
    private static readonly Vec3d Middle = new(0.5, 0.5, 0.5);
    private static readonly Vec3d Top = new(0.5, 1, 0.5);

    [AtlasScenario(RollbackWorld = true, StrictIsolation = true)]
    public async Task FramedWall_Should_TakeNoInfill_When_ClayIsPressedOnItInAnotherPlayersClaim()
    {
        (BlockPos cell, ITestPlayer player) = await Raise(infill: null);

        await AssertRefused(cell, player, "game:clay-blue", BlockFacing.EAST, Middle, 4);
    }

    [AtlasScenario(RollbackWorld = true, StrictIsolation = true)]
    public async Task FilledWall_Should_TakeNoFinish_When_PlanksArePressedOnItInAnotherPlayersClaim()
    {
        (BlockPos cell, ITestPlayer player) = await Raise();

        await AssertRefused(cell, player, "game:plank-oak", BlockFacing.EAST, Middle, 2);
    }

    [AtlasScenario(RollbackWorld = true, StrictIsolation = true)]
    public async Task FilledWall_Should_TakeNoTile_When_ATileIsPressedOnItInAnotherPlayersClaim()
    {
        (BlockPos cell, ITestPlayer player) = await Raise();

        await AssertRefused(cell, player, "game:claytile-fired-plain-blue", BlockFacing.EAST, new Vec3d(1, 0.25, 0.25), 4);
    }

    [AtlasScenario(RollbackWorld = true, StrictIsolation = true)]
    public async Task FilledWall_Should_TakeNoStep_When_AStairIsPressedOnItInAnotherPlayersClaim()
    {
        (BlockPos cell, ITestPlayer player) = await Raise();

        await AssertRefused(cell, player, "game:plankstairs-oak-up-north-free", BlockFacing.WEST, new Vec3d(0, 0.5, 0.5), 1);
    }

    [AtlasScenario(RollbackWorld = true, StrictIsolation = true)]
    public async Task Deck_Should_TakeNoInfill_When_ClayIsPressedOnItInAnotherPlayersClaim()
    {
        (BlockPos cell, ITestPlayer player) = await Raise();
        player.Entity.WatchedAttributes.SetString("vssidingDeck", "deck");
        await WallBuilder.Press(World, player, cell, "game:plank-oak", BlockFacing.EAST, new Vec3d(1, 0.5, 0.5), quantity: 2);
        Assert.Equal("oak", WallBuilder.DeckLayers(World, cell).Deck);

        await AssertRefused(cell, player, "game:clay-blue", BlockFacing.UP, Top, 4, onDeck: true);
    }

    [AtlasScenario(RollbackWorld = true, StrictIsolation = true)]
    public async Task FramedFloor_Should_TakeNoInfill_When_ClayIsPressedOnItInAnotherPlayersClaim()
    {
        (BlockPos cell, ITestPlayer player) = await Raise(infill: null, floor: true);

        await AssertRefused(cell, player, "game:clay-blue", BlockFacing.UP, Top, 4);
    }

    [AtlasScenario(RollbackWorld = true, StrictIsolation = true)]
    public async Task FilledFloor_Should_TakeNoFinish_When_PlanksArePressedOnItInAnotherPlayersClaim()
    {
        (BlockPos cell, ITestPlayer player) = await Raise(floor: true);

        await AssertRefused(cell, player, "game:plank-oak", BlockFacing.UP, Top, 2);
    }

    private async Task<(BlockPos Cell, ITestPlayer Player)> Raise(string? infill = "game:clay-blue", bool floor = false)
    {
        BlockPos cell = World.Spawn.Offset(1, 2, 0);
        World.SetBlock("game:planks-aged-ud", cell.DownCopy());
        ITestPlayer player = await WallBuilder.JoinBuilder(World);
        if (floor) player.Entity.WatchedAttributes.SetString("vssidingFraming", "floor");
        await WallBuilder.Raise(World, player, cell, cell.WestCopy(), "game:plank-oak", infill);
        return (cell, player);
    }

    // The cell's whole saved state and the held stack, before and after the press.
    private async Task AssertRefused(BlockPos cell, ITestPlayer player, string item, BlockFacing face, Vec3d hit, int quantity, bool onDeck = false)
    {
        string before = Saved(cell);
        LandClaim claim = WallBuilder.ClaimAround(World, cell);
        try
        {
            await WallBuilder.Press(World, player, cell, item, face, hit, quantity, onDeck: onDeck);

            Assert.Equal((before, quantity), (Saved(cell), player.Player.InventoryManager.ActiveHotbarSlot.Itemstack?.StackSize ?? 0));
        }
        finally
        {
            World.Api.World.Claims.Remove(claim);
        }
    }

    private string Saved(BlockPos cell)
    {
        var tree = new TreeAttribute();
        World.Api.World.BlockAccessor.GetBlockEntity(cell).ToTreeAttributes(tree);
        return tree.ToJsonToken();
    }
}
