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
}
