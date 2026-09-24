using HarmonyLib;
using Vintagestory.API.Common;

namespace VSSiding;

// Furniture-against-thin-walls (decision 0035): the panel GapShiftCollisionPatches adds to
// GetSelectionBoxes gives a hosted cell something to click on beside the furniture, but a click
// landing there has to go nowhere - decision 0013 already faced the breaking half of this (there is
// no BlockSelection on OnBlockBroken, hence ServerBreakSelection), so breaking is swallowed in the
// server's BreakBlock event handler (SidingModSystem.StartServerSide) instead of here. This file
// only swallows interaction and the mining-progress half of breaking.
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

    // No depth guard: an inner base call only runs when the outer frame ran the original (which a
    // panel hit here never does, since __result is set and the prefix returns false), so there is
    // no re-entrant frame to tell apart from the real one - the answer is the same at every level.
    private static bool InteractPrefix(Block __instance, object[] __args, ref bool __result)
    {
        if (__args[0] is not IWorldAccessor world || __args[2] is not BlockSelection blockSel) return true;
        if (!GapShiftCollisionPatches.IsPanelHit(__instance, world.BlockAccessor, blockSel)) return true;

        // Vanilla falls through to the held item's own OnHeldInteractStart when the block returns
        // false, so placing a block against the panel face still works like any other wall.
        __result = false;
        return false;
    }

    // Survival mining time on a panel hit never advances, on either side, so a player can't reach
    // the furniture by grinding down the wall in front of it. Same no-depth-guard reasoning as
    // InteractPrefix: the postfix is idempotent, so running once or three times gives the same
    // remainingResistance either way.
    private static void GettingBrokenPostfix(Block __instance, object[] __args, ref float __result)
    {
        if (__args[0] is not IPlayer player || __args[1] is not BlockSelection blockSel || __args[3] is not float remainingResistance) return;
        if (!GapShiftCollisionPatches.IsPanelHit(__instance, player.Entity.World.BlockAccessor, blockSel)) return;

        __result = remainingResistance;
    }
}
