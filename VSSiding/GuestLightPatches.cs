using System;
using HarmonyLib;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.Client.NoObf;
using Vintagestory.Common;

namespace VSSiding;

// Once furniture hosts a wall's cell, vanilla asks the host about that cell's light absorption and
// side AO (decision 0035), so a hosted chest would light a sealed room and stop the corner shading
// decision 0016 relies on. Every result is combined with Math.Max or |=, so a base call running the
// postfix twice is harmless and no depth guard is needed.
internal static class GuestLightPatches
{
    // The light source vanilla is walking or taking away on this thread. ChunkIlluminator subtracts a
    // source cell's own absorption before its light leaves, so a hosted torch's cell must read the
    // torch's absorption there, not the wall's, or the light never gets out.
    [ThreadStatic] private static (int X, int Y, int Z)? source;

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

        try
        {
            var prefix = new HarmonyMethod(typeof(GuestLightPatches), nameof(SourcePrefix));
            var finalizer = new HarmonyMethod(typeof(GuestLightPatches), nameof(SourceFinalizer));
            var collectPrefix = new HarmonyMethod(typeof(GuestLightPatches), nameof(CollectPrefix));
            harmony.Patch(AccessTools.Method(typeof(ChunkIlluminator), "CollectLightValuesForLightSource"), collectPrefix, finalizer: finalizer);
            harmony.Patch(AccessTools.Method(typeof(ChunkIlluminator), nameof(ChunkIlluminator.RemoveBlockLight)), prefix, finalizer: finalizer);
        }
        catch (Exception e)
        {
            api.Logger.Error("vssiding: hosted light source patch skipped, a light hosted in a sealed wall will not light the room: {0}", e);
        }

        try
        {
            harmony.Patch(AccessTools.Method(typeof(ChunkIlluminator), nameof(ChunkIlluminator.UpdateSunLight)),
                postfix: new HarmonyMethod(typeof(GuestLightPatches), nameof(SunLightPostfix)));
        }
        catch (Exception e)
        {
            api.Logger.Error("vssiding: block light relight patch skipped, torch light through a wall will be stale after its layers change: {0}", e);
        }
    }

    // A wall's layers change its absorption without changing its block, which reaches vanilla as an
    // absorption-changed task, and both relight systems redo only sunlight for one. UpdateSunLight is
    // the call they share for it, and each marks every chunk it returns. A block change that vanilla
    // already relit runs the block light update a second time here, to the same result.
    private static void SunLightPostfix(ChunkIlluminator __instance, int posX, int posY, int posZ, int oldAbsorb, int newAbsorb,
        FastSetOfLongs __result, IBlockAccessor ___readBlockAccess)
    {
        if (oldAbsorb == newAbsorb) return;

        var pos = new BlockPos(posX, posY, posZ);
        if (___readBlockAccess.GetBlock(pos) is not SidingFloorBlock && SidingWallBlock.WallAt(___readBlockAccess, pos) == null) return;

        foreach (long chunk in __instance.UpdateBlockLight(oldAbsorb, newAbsorb, posX, posY, posZ)) __result.Add(chunk);
    }

    // RemoveBlockLight walks every other nearby source, so each walk puts back the one it replaced.
    private static void SourcePrefix(int posX, int posY, int posZ, out (int X, int Y, int Z)? __state)
    {
        __state = source;
        source = (posX, posY, posZ);
    }

    // A hosted light sits on the open side of a sealed wall's panel, so the cell beyond each face the
    // panel claims gets none of it. The walk skips a neighbour whose slot already holds its walk
    // number, so the slot is written before the walk starts. The mark lasts the whole walk: that cell
    // stays dark even where the light could wrap round a free-standing wall's end (decision 0072).
    private static void CollectPrefix(int posX, int posY, int posZ, out (int X, int Y, int Z)? __state,
        int[]? ___currentVisited, int ___iteration, IBlockAccessor ___readBlockAccess)
    {
        SourcePrefix(posX, posY, posZ, out __state);
        if (___currentVisited == null) return;

        var pos = new BlockPos(posX, posY, posZ);
        Block block = ___readBlockAccess.GetBlock(pos);
        if (!SidingModSystem.IsHostableId(block.BlockId) || SidingModSystem.ApiRef(block) is not { } api) return;

        SidingWallEntity? guest = GuestWalls.GuestAt(api, pos);
        if (guest?.Block is not SidingWallBlock wall || GuestWalls.Absorption(guest) <= 0) return;

        foreach (BlockFacing facing in BlockFacing.HORIZONTALS)
        {
            if (!wall.ClaimsFace(facing)) continue;

            Vec3i behind = facing.Normali;
            ___currentVisited[NeighbourSlot(behind.X, behind.Y, behind.Z)] = ___iteration + 1;
        }
    }

    // ChunkIlluminator keeps the cells around a walk's source in a 63-wide grid, the source at its centre.
    internal static int NeighbourSlot(int dx, int dy, int dz) => 125023 + dx + dz * 63 + dy * 3969;

    private static void SourceFinalizer((int X, int Y, int Z)? __state) => source = __state;

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
        if (source == (pos.X, pos.Y, pos.Z)) return;

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
