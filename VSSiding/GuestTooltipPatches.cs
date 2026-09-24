using System;
using System.Text;
using HarmonyLib;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace VSSiding;

// Furniture-against-thin-walls (decision 0035 pending): furniture hosting a wall's cell hides that
// wall from a look-block query as much as it hides it from view, so the tooltip needs its own
// reminder. A postfix on every declaring override of GetPlacedBlockInfo across loaded assemblies
// (EveryOverridePatches) appends the guest's own description, reusing SidingWallEntity.GetBlockInfo
// (which already wraps SidingWallBlock.Describe) rather than calling Describe with ten arguments
// again.
internal static class GuestTooltipPatches
{
    // An override that calls base.GetPlacedBlockInfo runs both the base's patched method and its
    // own; only the outermost frame should look up and append the guest, same reasoning as
    // GapShiftCollisionPatches - a separate counter because a tooltip call must not read as nested
    // inside an unrelated collision query, or vice versa.
    [ThreadStatic] private static int depth;

    internal static void PatchAll(Harmony harmony, ICoreAPI api)
    {
        EveryOverridePatches.PatchEveryOverride(harmony, api, nameof(Block.GetPlacedBlockInfo),
            new[] { typeof(IWorldAccessor), typeof(BlockPos), typeof(IPlayer) },
            new HarmonyMethod(typeof(GuestTooltipPatches), nameof(Prefix)),
            new HarmonyMethod(typeof(GuestTooltipPatches), nameof(Postfix)),
            new HarmonyMethod(typeof(GuestTooltipPatches), nameof(Finalizer)));
    }

    private static void Prefix() => depth++;

    private static void Finalizer() => depth--;

    // __args rather than named parameters, as in GapShiftCollisionPatches: Harmony binds by name, and
    // a mod's override that spells them differently would refuse the whole patch.
    private static void Postfix(Block __instance, object[] __args, ref string __result)
    {
        if (depth != 1 || __args[0] is not IWorldAccessor world || __args[1] is not BlockPos pos || __args[2] is not IPlayer forPlayer) return;
        if (SidingModSystem.Hostable is not { } hostable || __instance.BlockId >= hostable.Length || !hostable[__instance.BlockId]) return;

        SidingWallEntity? guest = GuestWalls.GuestAt(world.Api, pos);
        if (guest == null) return;

        var sb = new StringBuilder();
        guest.GetBlockInfo(forPlayer, sb);
        __result = (__result + "\n" + sb).TrimEnd();
    }
}
