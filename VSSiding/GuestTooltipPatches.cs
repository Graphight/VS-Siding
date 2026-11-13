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

    // Positional __0, __1, __2, because Harmony binds named parameters by name and a mod's override may spell them differently.
    private static void Postfix(Block __instance, IWorldAccessor __0, BlockPos __1, IPlayer __2, ref string __result)
    {
        if (depth != 1 || __0 == null || __1 == null || __2 == null) return;
        var (world, pos, forPlayer) = (__0, __1, __2);
        if (!SidingModSystem.IsHostableId(__instance.BlockId)) return;

        SidingWallEntity? guest = GuestWalls.GuestAt(world.Api, pos);
        if (guest == null) return;

        var sb = new StringBuilder();
        guest.GetBlockInfo(forPlayer, sb);
        __result = (__result + "\n" + sb).TrimEnd();
    }
}
