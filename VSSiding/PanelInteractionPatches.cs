using HarmonyLib;
using Vintagestory.API.Common;

namespace VSSiding;

// A click on a hosted cell's panel reaches nothing (decision 0035); a break aimed at it breaks the host (0093).
internal static class PanelInteractionPatches
{
    internal static void PatchAll(Harmony harmony, ICoreAPI api)
    {
        EveryOverridePatches.PatchEveryOverride(harmony, api, nameof(Block.OnBlockInteractStart),
            new[] { typeof(IWorldAccessor), typeof(IPlayer), typeof(BlockSelection) },
            new HarmonyMethod(typeof(PanelInteractionPatches), nameof(InteractPrefix)), null, null);
    }

    // No depth guard: a panel hit skips the original, so no base call runs underneath.
    private static bool InteractPrefix(Block __instance, IWorldAccessor __0, BlockSelection __2, ref bool __result)
    {
        if (__0 == null || __2 == null) return true;
        if (!GapShiftCollisionPatches.IsPanelHit(__instance, __0.BlockAccessor, __2)) return true;

        // Vanilla falls through to the held item's own OnHeldInteractStart when the block returns
        // false, so placing a block against the panel face still works like any other wall.
        __result = false;
        return false;
    }
}
