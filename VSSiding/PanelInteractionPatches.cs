using HarmonyLib;
using Vintagestory.API.Common;

namespace VSSiding;

// A click on a hosted cell's panel reaches nothing (decision 0035), bar a roof's, which layers its wall; a break aimed at it breaks the host (0093).
internal static class PanelInteractionPatches
{
    internal static void PatchAll(Harmony harmony, ICoreAPI api)
    {
        EveryOverridePatches.PatchEveryOverride(harmony, api, nameof(Block.OnBlockInteractStart),
            new[] { typeof(IWorldAccessor), typeof(IPlayer), typeof(BlockSelection) },
            new HarmonyMethod(typeof(PanelInteractionPatches), nameof(InteractPrefix)), null, null);
    }

    // No depth guard: a panel hit skips the original, and the guest wall's own base call selects no panel box.
    private static bool InteractPrefix(Block __instance, IWorldAccessor __0, IPlayer __1, BlockSelection __2, ref bool __result)
    {
        if (__0 == null || __2 == null) return true;
        if (!GapShiftCollisionPatches.IsPanelHit(__instance, __0.BlockAccessor, __2)) return true;

        // Both sides run it, as a wall's own click: the client predicts the guest the server's record then replaces.
        if (SidingModSystem.IsRoofId(__instance.BlockId) && __1 != null && SidingWallBlock.HasBuildSignal(__1)
            && GuestWalls.GuestAt(__0.Api, __2.Position) is { Block: SidingWallBlock wall } guest)
        {
            __result = GuestWalls.Edit(__0, __2.Position, guest, () => wall.OnBlockInteractStart(__0, __1, __2));
            return false;
        }

        // Vanilla falls through to the held item's own OnHeldInteractStart when the block returns
        // false, so placing a block against the panel face still works like any other wall.
        __result = false;
        return false;
    }
}
