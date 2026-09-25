using System;
using HarmonyLib;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace VSSiding;

// Once furniture hosts a wall's cell, vanilla asks the host about that cell's retention, liquid
// barrier and attachability, so on the guest's claimed faces the answer stays the wall's (decision 0035).
internal static class GuestSealingPatches
{
    // One counter for all three: none calls another, so only base calls nest.
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

    // Null unless the host holds a guest that claims faceCode.
    private static SidingWallEntity? ClaimingGuestAt(Block host, BlockPos pos, string faceCode, bool restorePending = false)
    {
        if (!MayHoldGuest(host, SidingModSystem.Hostable, restorePending)) return null;

        ICoreAPI? api = SidingModSystem.ApiRef(host);
        if (api == null) return null;

        SidingWallEntity? entity = GuestWalls.GuestAt(api, pos);
        return entity?.Block is SidingWallBlock wall && SidingWallBlock.ClaimsFace(wall.Variant["layout"], wall.Variant["side"], faceCode)
            ? entity : null;
    }

    // A hosted block holds a guest, and so does the air a broken host leaves until the deferred
    // restore. Only CanAttachBlockAt asks about that air: it's off the hot paths, and a wrong answer
    // in that tick drops a torch on the wall's far side for good.
    internal static bool MayHoldGuest(Block block, bool[]? hostable, bool restorePending)
        => (hostable != null && block.BlockId < hostable.Length && hostable[block.BlockId])
            || (restorePending && (block.BlockId == 0 || block.Replaceable >= 6000));

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
        if (depth != 1 || __args[0] is not IBlockAccessor blockAccessor
            || __args[2] is not BlockPos pos || __args[3] is not BlockFacing blockFace)
        {
            return;
        }

        SidingWallEntity? entity = ClaimingGuestAt(__instance, pos, blockFace.Code, restorePending: true);
        if (entity == null && !__result) entity = GuestAcrossFace(blockAccessor, pos, blockFace);
        if (entity == null) return;

        var wall = (SidingWallBlock)entity.Block;
        __result = SidingWallBlock.ComputeRetention(true, entity.Framing, entity.Infill, wall.Attributes["Framings"], wall.Attributes["Infills"]) != 0;
    }

    // A torch or sign placed on the panel's inner face asks the cell past the panel (pos, often air)
    // whether it can attach, with blockFace pointing back across it. The panel lives in pos + blockFace,
    // so that cell's wall answers. WallAt, not just the guest store: the check runs before the
    // SetBlock that turns the wall into a guest.
    private static SidingWallEntity? GuestAcrossFace(IBlockAccessor blockAccessor, BlockPos pos, BlockFacing blockFace)
    {
        if (SidingWallBlock.WallAt(blockAccessor, pos.AddCopy(blockFace)) is not { } found) return null;
        var (wall, entity) = found;
        return ClaimsFaceTowardPos(wall.Variant["layout"], wall.Variant["side"], blockFace.Code) ? entity : null;
    }

    // Whether the wall's panel faces back across blockFaceCode, i.e. claims its opposite.
    internal static bool ClaimsFaceTowardPos(string layout, string side, string blockFaceCode)
        => SidingWallBlock.ClaimsFace(layout, side, BlockFacing.FromCode(blockFaceCode).Opposite.Code);
}
