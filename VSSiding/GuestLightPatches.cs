using System;
using HarmonyLib;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.Client.NoObf;

namespace VSSiding;

// Furniture-against-thin-walls (decision 0035): once furniture hosts a wall's cell, the
// host block is what vanilla asks about that cell's light absorption and side AO - so a hosted
// chest would let daylight through a sealed room, and its cell would stop shading the corners
// decision 0016 relies on. Postfixes on every declaring override of the four methods
// (EveryOverridePatches), same trick as GuestSealingPatches and GuestTooltipPatches. Unlike those
// two, none of these four calls another of the four on the same instance, and every result here is
// combined with Math.Max or `|=` rather than replaced outright, so a base-calls-base double
// application is harmless and no re-entrancy depth guard is needed.
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

    // The Hostable table read comes first on both absorption postfixes - this is a very hot path
    // (every chunk mesh, every relight), and the table turns almost every call into one array read.
    private static void AbsorptionAccessorPostfix(Block __instance, object[] __args, ref int __result)
    {
        if (SidingModSystem.Hostable is not { } hostable || __instance.BlockId >= hostable.Length || !hostable[__instance.BlockId]) return;
        if (__args[0] is not IBlockAccessor || __args[1] is not BlockPos pos) return;

        ICoreAPI? api = SidingModSystem.ApiRef(__instance);
        SidingWallEntity? guest = api == null ? null : GuestWalls.GuestAt(api, pos);
        if (guest?.Block is not SidingWallBlock wall) return;
        __result = Math.Max(__result, SidingWallBlock.ComputeLightAbsorption(guest.Framing, guest.Infill, wall.Attributes["Framings"], wall.Attributes["Infills"]));
    }

    private static void AbsorptionChunkPostfix(Block __instance, object[] __args, ref int __result)
    {
        if (SidingModSystem.Hostable is not { } hostable || __instance.BlockId >= hostable.Length || !hostable[__instance.BlockId]) return;
        if (__args[0] is not IWorldChunk chunk || __args[1] is not BlockPos pos) return;

        ICoreAPI? api = SidingModSystem.ApiRef(__instance);
        SidingWallEntity? guest = api == null ? null : GuestWalls.GuestAt(api, chunk, pos);
        if (guest?.Block is not SidingWallBlock wall) return;
        __result = Math.Max(__result, SidingWallBlock.ComputeLightAbsorption(guest.Framing, guest.Infill, wall.Attributes["Framings"], wall.Attributes["Infills"]));
    }

    // The caller is always the tessellator's TCTCache (TCTCache.cs, decompiled ~189/~216-247), and
    // the block being asked sits at its (posX, posY, posZ) offset by facing.Opposite - exactly where
    // SidingWallBlock's own override reads its entity via GetCurrentBlockEntityOnSide. Client-only:
    // side AO only ever runs on the tessellation thread.
    private static void SideAoPostfix(IGeometryTester caller, BlockFacing facing, ref bool __result)
    {
        if (__result || SidingModSystem.capi is not { } capi || caller is not TCTCache tct) return;

        var pos = new BlockPos(tct.posX, tct.posY, tct.posZ, tct.dimension).Offset(facing.Opposite);
        SidingWallEntity? guest = GuestWalls.GuestAt(capi, pos);
        if (guest?.Block is SidingWallBlock wall) __result |= wall.IsSealed(guest);
    }

    private static void SideAoByFlagPostfix(IGeometryTester caller, Vec3iAndFacingFlags vec, ref bool __result)
    {
        if (__result || SidingModSystem.capi is not { } capi || caller is not TCTCache tct) return;

        var pos = new BlockPos(tct.posX + vec.X, tct.posY + vec.Y, tct.posZ + vec.Z, tct.dimension);
        SidingWallEntity? guest = GuestWalls.GuestAt(capi, pos);
        if (guest?.Block is SidingWallBlock wall) __result |= wall.IsSealed(guest);
    }
}
