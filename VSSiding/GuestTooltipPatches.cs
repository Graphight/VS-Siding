using System;
using System.Text;
using HarmonyLib;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace VSSiding;

// Hosted furniture's tooltip appends its guest wall's description (decision 0035).
internal static class GuestTooltipPatches
{
    // An override calling base.GetPlacedBlockInfo runs the postfix twice; only the outermost appends.
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

    // __args, because Harmony binds by name and a mod's override may spell the parameters differently.
    private static void Postfix(Block __instance, object[] __args, ref string __result)
    {
        if (depth != 1 || __args[0] is not IWorldAccessor world || __args[1] is not BlockPos pos || __args[2] is not IPlayer forPlayer) return;
        if (!SidingModSystem.IsHostableId(__instance.BlockId)) return;

        SidingWallEntity? guest = GuestWalls.GuestAt(world.Api, pos);
        if (guest == null) return;

        var sb = new StringBuilder();
        guest.GetBlockInfo(forPlayer, sb);
        __result = (__result + "\n" + sb).TrimEnd();
    }
}
