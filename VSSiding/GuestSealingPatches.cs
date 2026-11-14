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

        EveryOverridePatches.PatchEveryOverride(harmony, api, nameof(Block.SideIsSolid),
            new[] { typeof(BlockPos), typeof(int) },
            new HarmonyMethod(typeof(GuestSealingPatches), nameof(Prefix)),
            new HarmonyMethod(typeof(GuestSealingPatches), nameof(SideSolidPostfix)),
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
        SidingWallEntity? entity = GuestAtHost(host, pos, restorePending);
        return entity?.Block is SidingWallBlock wall && SidingWallBlock.ClaimsFace(wall.Variant["layout"], wall.Variant["side"], faceCode)
            ? entity : null;
    }

    // Null unless the host holds a guest whose wall carries a deck: the deck seals and takes attachments on its own faces.
    private static SidingWallEntity? DeckedGuestAt(Block host, BlockPos pos, bool restorePending = false)
        => GuestAtHost(host, pos, restorePending) is { Deck: not null } entity && entity.Block is SidingWallBlock ? entity : null;

    private static SidingWallEntity? GuestAtHost(Block host, BlockPos pos, bool restorePending)
    {
        if (!MayHoldGuest(host, SidingModSystem.Hostable, restorePending)) return null;

        ICoreAPI? api = SidingModSystem.ApiRef(host);
        return api == null ? null : GuestWalls.GuestAt(api, pos);
    }

    // A hosted block holds a guest, and so does the air a broken host leaves until the deferred
    // restore. Only CanAttachBlockAt asks about that air: it's off the hot paths, and a wrong answer
    // in that tick drops a torch on the wall's far side for good.
    internal static bool MayHoldGuest(Block block, bool[]? hostable, bool restorePending)
        => (hostable != null && block.BlockId < hostable.Length && hostable[block.BlockId])
            || (restorePending && (block.BlockId == 0 || block.Replaceable >= 6000));

    private static void RetentionPostfix(Block __instance, BlockPos __0, BlockFacing __1, ref int __result)
    {
        if (depth != 1 || __0 == null || __1 == null) return;
        var (pos, facing) = (__0, __1);

        SidingWallEntity? entity = facing == BlockFacing.UP ? DeckedGuestAt(__instance, pos) : ClaimingGuestAt(__instance, pos, facing.Code);
        if (entity == null) return;

        __result = ((SidingWallBlock)entity.Block).RetentionOf(entity, facing);
    }

    private static void LiquidBarrierPostfix(Block __instance, BlockFacing __0, BlockPos __1, ref float __result)
    {
        if (depth != 1 || __0 == null || __1 == null) return;
        var (face, pos) = (__0, __1);

        SidingWallEntity? entity = ClaimingGuestAt(__instance, pos, face.Code);
        if (entity == null) return;

        var wall = (SidingWallBlock)entity.Block;
        __result = SidingWallBlock.ComputeLiquidBarrier(true, entity.Framing, entity.Infill, wall.Attributes);
    }

    private static void SideSolidPostfix(Block __instance, BlockPos __0, int __1, ref bool __result)
    {
        if (depth != 1 || __0 == null || __1 < 0 || __1 > 5) return;

        SidingWallEntity? entity = ClaimingGuestAt(__instance, __0, BlockFacing.ALLFACES[__1].Code);
        if (entity == null) return;

        var wall = (SidingWallBlock)entity.Block;
        __result = SidingWallBlock.ComputeLiquidBarrier(true, entity.Framing, entity.Infill, wall.Attributes) >= 1f;
    }

    private static void CanAttachPostfix(Block __instance, IBlockAccessor __0, BlockPos __2, BlockFacing __3, ref bool __result)
    {
        if (depth != 1 || __0 == null || __2 == null || __3 == null) return;
        var (blockAccessor, pos, blockFace) = (__0, __2, __3);

        bool vertical = !blockFace.IsHorizontal;
        SidingWallEntity? entity = vertical
            ? DeckedGuestAt(__instance, pos, restorePending: true)
            : ClaimingGuestAt(__instance, pos, blockFace.Code, restorePending: true);
        if (entity == null && !__result && !vertical) entity = GuestAcrossFace(blockAccessor, pos, blockFace);
        if (entity == null) return;

        var wall = (SidingWallBlock)entity.Block;
        __result = vertical
            ? wall.CanAttachOf(entity, blockFace)
            : SidingWallBlock.TraitsOf(wall.Attributes, entity.Framing, entity.Infill).Retention != 0;
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
