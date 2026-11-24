using HarmonyLib;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace VSSiding;

// A click on a hosted cell's panel reaches nothing (decision 0035), bar a roof's, which layers its wall; a break aimed at it breaks the
// host (0093), bar a roof's, which peels its wall.
internal static class PanelInteractionPatches
{
    internal static void PatchAll(Harmony harmony, ICoreAPI api)
    {
        EveryOverridePatches.PatchEveryOverride(harmony, api, nameof(Block.OnBlockInteractStart),
            new[] { typeof(IWorldAccessor), typeof(IPlayer), typeof(BlockSelection) },
            new HarmonyMethod(typeof(PanelInteractionPatches), nameof(InteractPrefix)), null, null);
        EveryOverridePatches.PatchEveryOverride(harmony, api, nameof(Block.OnBlockBroken),
            new[] { typeof(IWorldAccessor), typeof(BlockPos), typeof(IPlayer), typeof(float) },
            new HarmonyMethod(typeof(PanelInteractionPatches), nameof(BreakPrefix)), null, null);
    }

    // No depth guard: a panel hit skips the original, and the guest wall's own base call selects no panel box.
    private static bool InteractPrefix(Block __instance, IWorldAccessor __0, IPlayer __1, BlockSelection __2, ref bool __result)
    {
        if (__0 == null || __2 == null) return true;
        if (!GapShiftCollisionPatches.IsPanelHit(__instance, __0.BlockAccessor, __2))
        {
            // Roofing adds its base fill on a ctrl click with a block in hand; the probe holds the ctrl key and the sprint action
            // together, so either one counts.
            if (!SidingModSystem.IsRoofId(__instance.BlockId) || __1?.Entity.Controls is not { } controls || !(controls.CtrlKey || controls.Sprint)
                || __1.InventoryManager.ActiveHotbarSlot?.Itemstack?.Block == null || GuestWalls.GuestAt(__0.Api, __2.Position) == null)
                return true;
            SidingWallBlock.RefuseBaseFill(__1);
            __result = true;
            return false;
        }

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

    // A roof's panel peels its wall a layer per break, as a wall's own break does, and the bare frame goes last, the roof staying.
    // Both sides run it; the wall's own break then reads and clears the server's selection.
    private static bool BreakPrefix(Block __instance, IWorldAccessor __0, BlockPos __1, IPlayer __2, float __3)
    {
        if (!SidingModSystem.IsRoofId(__instance.BlockId) || __0 == null || __1 == null || __2 == null) return true;
        bool server = __0.Side == EnumAppSide.Server;
        BlockSelection? hit = server ? SidingWallBlock.ServerBreakSelection : __2.CurrentBlockSelection;
        if (hit?.Position?.Equals(__1) != true || !GapShiftCollisionPatches.IsPanelHit(__instance, __0.BlockAccessor, hit)
            || GuestWalls.GuestAt(__0.Api, __1) is not { Block: SidingWallBlock wall } guest)
        {
            if (server) SidingWallBlock.ServerBreakSelection = null;
            return true;
        }

        GuestWalls.Edit(__0, __1, guest, () =>
        {
            wall.OnBlockBroken(__0, __1, __2, __3);
            return true;
        });
        return false;
    }
}
