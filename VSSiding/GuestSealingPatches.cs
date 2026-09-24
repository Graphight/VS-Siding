using System;
using HarmonyLib;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace VSSiding;

// Furniture-against-thin-walls (decision 0035 pending): once furniture hosts a wall's cell, the
// host block is what vanilla asks about that cell's retention, liquid barrier and attachability -
// so on the faces the guest wall claims, the answer has to keep being the wall's, or a hosted
// chest would leak a sealed room and pass water through it. Postfixes on every declaring override
// of the three methods, same EveryOverridePatches trick as GuestTooltipPatches.
internal static class GuestSealingPatches
{
    // One depth counter shared by all three methods rather than one each: nothing on this branch
    // ever calls one of these three from within another, so there is no cross-method re-entrancy to
    // tell apart, only the base-calls-base kind EveryOverridePatches.PatchEveryOverride's own
    // pattern already guards against per method (GuestTooltipPatches explains why that guard is
    // needed at all).
    [ThreadStatic] private static int depth;

    internal static void PatchAll(Harmony harmony, ICoreAPI api)
    {
        EveryOverridePatches.PatchEveryOverride(harmony, api, nameof(Block.GetRetention),
            new[] { typeof(BlockPos), typeof(BlockFacing), typeof(EnumRetentionType) },
            new HarmonyMethod(typeof(GuestSealingPatches), nameof(Prefix)),
            new HarmonyMethod(typeof(GuestSealingPatches), nameof(RetentionPostfix)),
            new HarmonyMethod(typeof(GuestSealingPatches), nameof(Finalizer)));

        EveryOverridePatches.PatchEveryOverride(harmony, api, nameof(Block.GetLiquidBarrierHeightOnSide),
            new[] { typeof(BlockFacing), typeof(BlockPos) },
            new HarmonyMethod(typeof(GuestSealingPatches), nameof(Prefix)),
            new HarmonyMethod(typeof(GuestSealingPatches), nameof(LiquidBarrierPostfix)),
            new HarmonyMethod(typeof(GuestSealingPatches), nameof(Finalizer)));

        EveryOverridePatches.PatchEveryOverride(harmony, api, nameof(Block.CanAttachBlockAt),
            new[] { typeof(IBlockAccessor), typeof(Block), typeof(BlockPos), typeof(BlockFacing), typeof(Cuboidi) },
            new HarmonyMethod(typeof(GuestSealingPatches), nameof(Prefix)),
            new HarmonyMethod(typeof(GuestSealingPatches), nameof(CanAttachPostfix)),
            new HarmonyMethod(typeof(GuestSealingPatches), nameof(Finalizer)));
    }

    private static void Prefix() => depth++;

    private static void Finalizer() => depth--;

    // None of the three overrides carries a world accessor, so the guest lookup goes through the
    // host block's own api field, exactly as GapShiftAt's does off a physics tick's accessor. Null
    // unless the host is hostable, has a guest, and that guest claims faceCode.
    private static SidingWallEntity? ClaimingGuestAt(Block host, BlockPos pos, string faceCode)
    {
        if (SidingModSystem.Hostable is not { } hostable || host.BlockId >= hostable.Length || !hostable[host.BlockId]) return null;

        ICoreAPI? api = SidingModSystem.ApiRef(host);
        if (api == null) return null;

        SidingWallEntity? entity = GuestWalls.GuestAt(api, pos);
        return entity?.Block is SidingWallBlock wall && SidingWallBlock.ClaimsFace(wall.Variant["layout"], wall.Variant["side"], faceCode)
            ? entity : null;
    }

    private static void RetentionPostfix(Block __instance, object[] __args, ref int __result)
    {
        if (depth != 1 || __args[0] is not BlockPos pos || __args[1] is not BlockFacing facing) return;

        SidingWallEntity? entity = ClaimingGuestAt(__instance, pos, facing.Code);
        if (entity == null) return;

        var wall = (SidingWallBlock)entity.Block;
        __result = SidingWallBlock.ComputeRetention(true, entity.Framing, entity.Infill, wall.Attributes["Framings"], wall.Attributes["Infills"]);
    }

    private static void LiquidBarrierPostfix(Block __instance, object[] __args, ref float __result)
    {
        if (depth != 1 || __args[0] is not BlockFacing face || __args[1] is not BlockPos pos) return;

        SidingWallEntity? entity = ClaimingGuestAt(__instance, pos, face.Code);
        if (entity == null) return;

        var wall = (SidingWallBlock)entity.Block;
        __result = SidingWallBlock.ComputeLiquidBarrier(true, entity.Framing, entity.Infill, wall.Attributes["Framings"], wall.Attributes["Infills"]);
    }

    private static void CanAttachPostfix(Block __instance, object[] __args, ref bool __result)
    {
        if (depth != 1 || __args[2] is not BlockPos pos || __args[3] is not BlockFacing blockFace) return;

        SidingWallEntity? entity = ClaimingGuestAt(__instance, pos, blockFace.Code);
        if (entity == null) return;

        var wall = (SidingWallBlock)entity.Block;
        __result = SidingWallBlock.ComputeRetention(true, entity.Framing, entity.Infill, wall.Attributes["Framings"], wall.Attributes["Infills"]) != 0;
    }
}
