using System;
using HarmonyLib;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.Client.NoObf;

namespace VSSiding;

// Once furniture hosts a wall's cell, vanilla asks the host about that cell's light absorption and
// side AO (decision 0035), so a hosted chest would light a sealed room and stop the corner shading
// decision 0016 relies on. Every result is combined with Math.Max or |=, so a base call running the
// postfix twice is harmless and no depth guard is needed.
internal static class GuestLightPatches
{
    internal static void PatchAll(Harmony harmony, ICoreAPI api)
    {
        EveryOverridePatches.PatchEveryOverride(harmony, api, nameof(Block.GetLightAbsorption),
            new[] { typeof(IBlockAccessor), typeof(BlockPos) },
            null, new HarmonyMethod(typeof(GuestLightPatches), nameof(AbsorptionAccessorPostfix)), null);

        EveryOverridePatches.PatchEveryOverride(harmony, api, nameof(Block.GetLightAbsorption),
            new[] { typeof(IWorldChunk), typeof(BlockPos) },
            null, new HarmonyMethod(typeof(GuestLightPatches), nameof(AbsorptionChunkPostfix)), null);

        EveryOverridePatches.PatchEveryOverride(harmony, api, nameof(Block.DoEmitSideAo),
            new[] { typeof(IGeometryTester), typeof(BlockFacing) },
            null, new HarmonyMethod(typeof(GuestLightPatches), nameof(SideAoPostfix)), null);

        EveryOverridePatches.PatchEveryOverride(harmony, api, nameof(Block.DoEmitSideAoByFlag),
            new[] { typeof(IGeometryTester), typeof(Vec3iAndFacingFlags), typeof(int) },
            null, new HarmonyMethod(typeof(GuestLightPatches), nameof(SideAoByFlagPostfix)), null);
    }

    // The Hostable read comes first: every chunk mesh and relight runs through here.
    private static void AbsorptionAccessorPostfix(Block __instance, object[] __args, ref int __result)
    {
        if (!SidingModSystem.IsHostableId(__instance.BlockId) || __args[1] is not BlockPos pos) return;

        ICoreAPI? api = SidingModSystem.ApiRef(__instance);
        if (api != null) __result = Math.Max(__result, GuestWalls.Absorption(GuestWalls.GuestAt(api, pos)));
    }

    private static void AbsorptionChunkPostfix(Block __instance, object[] __args, ref int __result)
    {
        if (!SidingModSystem.IsHostableId(__instance.BlockId) || __args[0] is not IWorldChunk chunk || __args[1] is not BlockPos pos) return;

        ICoreAPI? api = SidingModSystem.ApiRef(__instance);
        if (api != null) __result = Math.Max(__result, GuestWalls.Absorption(GuestWalls.GuestAt(api, chunk, pos)));
    }

    // The caller is always the tessellator's TCTCache, and the block asked sits at its position
    // offset by facing.Opposite, where SidingWallBlock's own override reads its entity.
    private static void SideAoPostfix(Block __instance, IGeometryTester caller, BlockFacing facing, ref bool __result)
    {
        if (__result || !SidingModSystem.IsHostableId(__instance.BlockId)
            || SidingModSystem.capi is not { } capi || caller is not TCTCache tct) return;

        var pos = new BlockPos(tct.posX, tct.posY, tct.posZ, tct.dimension).Offset(facing.Opposite);
        SidingWallEntity? guest = GuestWalls.GuestAt(capi, pos);
        if (guest?.Block is SidingWallBlock wall) __result |= wall.IsSealed(guest);
    }

    private static void SideAoByFlagPostfix(Block __instance, IGeometryTester caller, Vec3iAndFacingFlags vec, ref bool __result)
    {
        if (__result || !SidingModSystem.IsHostableId(__instance.BlockId)
            || SidingModSystem.capi is not { } capi || caller is not TCTCache tct) return;

        var pos = new BlockPos(tct.posX + vec.X, tct.posY + vec.Y, tct.posZ + vec.Z, tct.dimension);
        SidingWallEntity? guest = GuestWalls.GuestAt(capi, pos);
        if (guest?.Block is SidingWallBlock wall) __result |= wall.IsSealed(guest);
    }
}
