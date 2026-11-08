using System;
using System.Linq;
using System.Threading.Tasks;
using Atlas.Api;
using Atlas.XUnit;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Xunit;

namespace VSSiding.E2E.Tests;

// Runs with `just probe butchering`, the only run that loads Butchering. Its ceiling hook declares vssidingHanger,
// so it rides up to the underside of the thin floor above it (#133).
[Trait("Category", "Probe")]
[AtlasWorld(Mods = new[] { "../../../../compat/mods/slot0.zip", "../../../../compat/mods/slot1.zip", "../../../../compat/mods/slot2.zip", "../../../../compat/mods/slot3.zip", "../../../../compat/mods/slot4.zip", "../../../../compat/mods/slot5.zip" })]
public class ButcheringScenarios : AtlasScenarioBase
{
    private static bool ButcheringLoaded => Environment.GetEnvironmentVariable("SIDING_PROBE") == "butchering";

    // The builder's picks are player data, which the world rollback leaves as the last scenario set them.
    private async Task<ITestPlayer> Join()
    {
        ITestPlayer player = await WallBuilder.JoinBuilder(World);
        player.Entity.WatchedAttributes.SetString("vssidingFraming", "wall");
        player.Entity.WatchedAttributes.SetString("vssidingDeck", "");
        return player;
    }

    [AtlasScenario(RollbackWorld = true, StrictIsolation = true)]
    public async Task CeilingHook_Should_RiseToAThinFloorAndBeSelectableThroughIt()
    {
        if (!ButcheringLoaded) return;

        BlockPos hook = World.Spawn.Offset(1, 2, 0);
        BlockPos floor = hook.UpCopy();
        World.SetBlock("game:planks-aged-ud", hook);
        ITestPlayer player = await Join();
        try
        {
            player.Entity.WatchedAttributes.SetString("vssidingFraming", "floor");
            await WallBuilder.Raise(World, player, floor, floor.WestCopy(), "game:plank-oak", null);
            World.SetBlock("butchering:butcherhookceiling-iron-north", hook);

            Block hookBlock = World.BlockAt(hook);
            Type modSystem = World.Api.ModLoader.GetModSystem("VSSiding.SidingModSystem").GetType();
            var shift = (double)modSystem.GetMethod("HangShiftAt", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public)!
                .Invoke(null, new object[] { hook, hookBlock })!;
            Cuboidf[] boxes = World.BlockAt(floor).GetSelectionBoxes(World.Api.World.BlockAccessor, floor);

            Assert.Equal((0.75, true), (shift, boxes.Any(box => box.GetType().Name == "DecorSelectionBox")));
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
        ITestPlayer player = await Join();
        try
        {
            await WallBuilder.Raise(World, player, cell, cell.WestCopy(), "game:plank-oak", "game:clay-blue");
            player.Entity.WatchedAttributes.SetString("vssidingDeck", "deck");
            await player.GiveItem("game:plank-oak", 2);
            var sideSel = new BlockSelection { Position = cell.Copy(), Face = BlockFacing.EAST, HitPosition = new Vec3d(1, 0.5, 0.5) };
            Assert.True(World.BlockAt(cell).OnBlockInteractStart(player.Entity.World, player.Player, sideSel));
            var deck = ("oak", (string?)null, (string?)null, (string?)null);
            Assert.Equal(deck, WallBuilder.DeckLayers(World, cell));

            World.SetBlock("butchering:butcherhook-iron-west", cell);
            Type modSystem = World.Api.ModLoader.GetModSystem("VSSiding.SidingModSystem").GetType();
            var shift = (double)modSystem.GetMethod("HangShiftAt", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public)!
                .Invoke(null, new object[] { cell, World.BlockAt(cell) })!;
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

    [AtlasScenario(RollbackWorld = true, StrictIsolation = true)]
    public async Task CeilingHook_Should_RiseToTheUndersideOfADeckedWall()
    {
        if (!ButcheringLoaded) return;

        BlockPos cell = World.Spawn.Offset(1, 3, 0);
        BlockPos hook = cell.DownCopy();
        World.SetBlock("game:planks-aged-ud", hook.DownCopy());
        ITestPlayer player = await Join();
        try
        {
            await WallBuilder.Raise(World, player, hook, hook.WestCopy(), "game:plank-oak", "game:clay-blue", "game:plank-oak");
            await WallBuilder.Raise(World, player, cell, cell.WestCopy(), "game:plank-oak", "game:clay-blue");
            player.Entity.WatchedAttributes.SetString("vssidingDeck", "deck");
            await player.GiveItem("game:plank-oak", 2);
            var sideSel = new BlockSelection { Position = cell.Copy(), Face = BlockFacing.EAST, HitPosition = new Vec3d(1, 0.5, 0.5) };
            Assert.True(World.BlockAt(cell).OnBlockInteractStart(player.Entity.World, player.Player, sideSel));
            World.SetBlock("butchering:butcherhookceiling-iron-north", hook);

            Type modSystem = World.Api.ModLoader.GetModSystem("VSSiding.SidingModSystem").GetType();
            var shift = (double)modSystem.GetMethod("HangShiftAt", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public)!
                .Invoke(null, new object[] { hook, World.BlockAt(hook) })!;
            Cuboidf[] boxes = World.BlockAt(cell).GetSelectionBoxes(World.Api.World.BlockAccessor, cell);

            Assert.Equal((0.75, true), (shift, boxes.Any(box => box.GetType().Name == "DecorSelectionBox")));
        }
        finally
        {
            player.Player.Disconnect();
            await World.Until(() => !player.IsConnected);
        }
    }
}
