using HarmonyLib;
using Vintagestory.API.Common;

namespace VSSiding;

// A click on a hosted cell's panel reaches nothing (decision 0035). A break aimed at the panel reaches
// the host (decision 0092), except a support beam's, swallowed here and in the server's BreakBlock
// handler (SidingModSystem.StartServerSide).
internal static class PanelInteractionPatches
{
    internal static void PatchAll(Harmony harmony, ICoreAPI api)
    {
        EveryOverridePatches.PatchEveryOverride(harmony, api, nameof(Block.OnBlockInteractStart),
            new[] { typeof(IWorldAccessor), typeof(IPlayer), typeof(BlockSelection) },
            new HarmonyMethod(typeof(PanelInteractionPatches), nameof(InteractPrefix)), null, null);

        EveryOverridePatches.PatchEveryOverride(harmony, api, nameof(Block.OnGettingBroken),
            new[] { typeof(IPlayer), typeof(BlockSelection), typeof(ItemSlot), typeof(float), typeof(float), typeof(int) },
            null, new HarmonyMethod(typeof(PanelInteractionPatches), nameof(GettingBrokenPostfix)), null);
    }

    // No depth guard: a panel hit skips the original, so no base call runs underneath.
    private static bool InteractPrefix(Block __instance, object[] __args, ref bool __result)
    {
        if (__args[0] is not IWorldAccessor world || __args[2] is not BlockSelection blockSel) return true;
        if (!GapShiftCollisionPatches.IsPanelHit(__instance, world.BlockAccessor, blockSel)) return true;

        // Vanilla falls through to the held item's own OnHeldInteractStart when the block returns
        // false, so placing a block against the panel face still works like any other wall.
        __result = false;
        return false;
    }

    // Mining a support beam's panel hit never advances. Idempotent, so no depth guard.
    private static void GettingBrokenPostfix(Block __instance, object[] __args, ref float __result)
    {
        if (__args[0] is not IPlayer player || __args[1] is not BlockSelection blockSel || __args[3] is not float remainingResistance) return;
        if (!GapShiftCollisionPatches.SwallowsPanelBreak(__instance, player.Entity.World.BlockAccessor, blockSel)) return;

        __result = remainingResistance;
    }
}
