using System;
using System.Linq;
using System.Threading.Tasks;
using Atlas.Api;
using Atlas.XUnit;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Xunit;

namespace VSSiding.E2E.Tests;

// Runs with `just probe butchering`, the only run that loads Butchering (#133). Its ceiling hook declares vssidingHanger,
// so it rides up to the underside of a thin floor or deck above it, and both hooks declare vssidingUnderDeck, so a
// decked cell takes them although their boxes are a full cell high.
[Trait("Category", "Probe")]
[AtlasWorld(Mods = new[] { "../../../../compat/mods/slot0.zip", "../../../../compat/mods/slot1.zip", "../../../../compat/mods/slot2.zip", "../../../../compat/mods/slot3.zip", "../../../../compat/mods/slot4.zip", "../../../../compat/mods/slot5.zip" })]
public class ButcheringScenarios : AtlasScenarioBase
{
    private static bool ButcheringLoaded => Environment.GetEnvironmentVariable("SIDING_PROBE") == "butchering";

    private async Task Deck(ITestPlayer player, BlockPos cell)
    {
        player.Entity.WatchedAttributes.SetString("vssidingDeck", "deck");
        await player.GiveItem("game:plank-oak", 2);
        var sideSel = new BlockSelection { Position = cell.Copy(), Face = BlockFacing.EAST, HitPosition = new Vec3d(1, 0.5, 0.5) };
        Assert.True(World.BlockAt(cell).OnBlockInteractStart(player.Entity.World, player.Player, sideSel));
    }

    [AtlasScenario(RollbackWorld = true, StrictIsolation = true)]
    public async Task CeilingHook_Should_RiseToAThinFloorAndBeSelectableThroughIt()
    {
        if (!ButcheringLoaded) return;

        BlockPos hook = World.Spawn.Offset(1, 2, 0);
        BlockPos floor = hook.UpCopy();
        World.SetBlock("game:planks-aged-ud", hook);
        ITestPlayer player = await WallBuilder.JoinBuilder(World);
        try
        {
            player.Entity.WatchedAttributes.SetString("vssidingFraming", "floor");
            await WallBuilder.Raise(World, player, floor, floor.WestCopy(), "game:plank-oak", null);
            World.SetBlock("butchering:butcherhookceiling-iron-north", hook);

            Cuboidf[] boxes = World.BlockAt(floor).GetSelectionBoxes(World.Api.World.BlockAccessor, floor);

            Assert.Equal((0.75, true), (WallBuilder.HangShiftAt(World, hook), boxes.Any(box => box.GetType().Name == "DecorSelectionBox")));
        }
        finally
        {
            player.Player.Disconnect();
            await World.Until(() => !player.IsConnected);
        }
    }

    [AtlasScenario(RollbackWorld = true, StrictIsolation = true)]
    public async Task WallHook_Should_HangUnderTheDeckOfItsCellAndLeaveTheDeckOnTheWallWhenBroken()
    {
        if (!ButcheringLoaded) return;

        BlockPos cell = World.Spawn.Offset(1, 2, 0);
        World.SetBlock("game:planks-aged-ud", cell.DownCopy());
        ITestPlayer player = await WallBuilder.JoinBuilder(World);
        try
        {
            await WallBuilder.Raise(World, player, cell, cell.WestCopy(), "game:plank-oak", "game:clay-blue");
            await Deck(player, cell);
            var deck = ("oak", (string?)null, (string?)null, (string?)null);
            Assert.Equal(deck, WallBuilder.DeckLayers(World, cell));

            World.SetBlock("butchering:butcherhook-iron-west", cell);
            double shift = WallBuilder.HangShiftAt(World, cell);
            var hosted = WallBuilder.GuestDeckLayers(World, cell);

            World.Api.World.BlockAccessor.BreakBlock(cell, player.Player);
            await World.Until(() => World.BlockEntityAt<BlockEntity>(cell) != null, 100);

            Assert.Equal((-0.25, deck, deck), (shift, hosted, WallBuilder.DeckLayers(World, cell)));
        }
        finally
        {
            player.Player.Disconnect();
            await World.Until(() => !player.IsConnected);
        }
    }

    // On the wall's outer face beside a thin floor, the hook goes into the cell below and rises to the floor's underside (decision 0092).
    [AtlasScenario(RollbackWorld = true, StrictIsolation = true)]
    public async Task WallHook_Should_RiseUnderAThinFloor_When_ClickedOntoTheWallFaceBesideIt()
    {
        if (!ButcheringLoaded) return;

        (BlockPos wall, BlockPos floor, BlockPos below, ITestPlayer player) = await WallBuilder.FloorBesideWall(World);
        try
        {
            await WallBuilder.Press(World, player, wall, "butchering:butcherhook-iron-north", BlockFacing.WEST, new Vec3d(0, 0.5, 0.5), quantity: 1);

            Assert.Equal(("butcherhook-iron-east", 0.75, true), (World.BlockAt(below).Code.Path, WallBuilder.HangShiftAt(World, below), WallBuilder.RaisedBelow(World, floor)));
        }
        finally
        {
            player.Player.Disconnect();
            await World.Until(() => !player.IsConnected);
        }
    }

    [AtlasScenario(RollbackWorld = true, StrictIsolation = true)]
    public async Task CeilingHook_Should_RiseToTheUndersideOfADeckedWall()
    {
        if (!ButcheringLoaded) return;

        BlockPos cell = World.Spawn.Offset(1, 3, 0);
        BlockPos hook = cell.DownCopy();
        World.SetBlock("game:planks-aged-ud", hook.DownCopy());
        ITestPlayer player = await WallBuilder.JoinBuilder(World);
        try
        {
            await WallBuilder.Raise(World, player, hook, hook.WestCopy(), "game:plank-oak", "game:clay-blue", "game:plank-oak");
            await WallBuilder.Raise(World, player, cell, cell.WestCopy(), "game:plank-oak", "game:clay-blue");
            await Deck(player, cell);
            World.SetBlock("butchering:butcherhookceiling-iron-north", hook);

            Cuboidf[] boxes = World.BlockAt(cell).GetSelectionBoxes(World.Api.World.BlockAccessor, cell);

            Assert.Equal((0.75, true), (WallBuilder.HangShiftAt(World, hook), boxes.Any(box => box.GetType().Name == "DecorSelectionBox")));
        }
        finally
        {
            player.Player.Disconnect();
            await World.Until(() => !player.IsConnected);
        }
    }

    // The footprint postfix asks every cell the table takes, not only the one clicked, so a deck on its middle wall refuses it.
    [AtlasScenario(RollbackWorld = true, StrictIsolation = true)]
    public async Task Table_Should_BeRefused_When_AWallInItsFootprintIsDecked()
    {
        if (!ButcheringLoaded) return;

        BlockPos cell = World.Spawn.Offset(1, 2, 0);
        BlockPos[] run = { cell, cell.NorthCopy(), cell.NorthCopy(2) };
        ITestPlayer player = await WallBuilder.JoinBuilder(World);
        try
        {
            foreach (BlockPos column in run)
            {
                World.SetBlock("game:planks-aged-ud", column.DownCopy());
                await WallBuilder.Raise(World, player, column, column.WestCopy(), "game:plank-oak", "game:clay-blue");
            }
            await Deck(player, run[1]);

            // Standing east of the run facing west, as CompatProbeScenarios.Host does: the table runs north along it.
            player.Entity.LeftHandItemSlot.Itemstack = null;
            player.Entity.Pos.SetPos(cell.X + 2.5, cell.Y, cell.Z + 0.5);
            player.Entity.Pos.Yaw = 3 * GameMath.PIHALF;
            var stack = new ItemStack(World.Api.World.GetBlock(new AssetLocation("butchering:butchertable-simple-north")));
            var selection = new BlockSelection { Position = cell.Copy(), Face = BlockFacing.EAST, HitPosition = new Vec3d(1, 0.5, 0.5) };
            string failureCode = "";
            bool placed = stack.Block.TryPlaceBlock(player.Entity.World, player.Player, stack, selection, ref failureCode);

            Assert.Equal((false, "vssiding-decked", true), (placed, failureCode, run.All(column => World.BlockAt(column).Code.Domain == "vssiding")));
        }
        finally
        {
            player.Player.Disconnect();
            await World.Until(() => !player.IsConnected);
        }
    }
}
