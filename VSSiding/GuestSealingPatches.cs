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
        if (depth != 1 || __args[0] is not IBlockAccessor blockAccessor
            || __args[2] is not BlockPos pos || __args[3] is not BlockFacing blockFace)
        {
            return;
        }

        SidingWallEntity? entity = ClaimingGuestAt(__instance, pos, blockFace.Code);
        if (entity == null && !__result) entity = GuestAcrossFace(__instance, blockAccessor, pos, blockFace);
        if (entity == null) return;

        var wall = (SidingWallBlock)entity.Block;
        __result = SidingWallBlock.ComputeRetention(true, entity.Framing, entity.Infill, wall.Attributes["Framings"], wall.Attributes["Infills"]) != 0;
    }

    // A hosted torch attaches by clicking the panel's own inner face, so vanilla's TryAttachTo
    // (BlockGroundAndSideAttachable) and BlockSign.TryPlaceBlock both resolve the attaching cell to
    // the one past the panel and ask THAT block whether it can hold the attachment - pos is that far
    // cell, blockFace points back across the panel. The far cell (often air) knows nothing about the
    // panel, so this looks at the neighbouring cell in that same direction (pos + blockFace, the
    // furniture's own cell) instead: if a guest lives there and its panel faces back at pos, the
    // wall's own rule answers, exactly as ClaimingGuestAt does for the furniture's own claimed faces.
    // __instance here is whatever stands in the far cell, so unlike ClaimingGuestAt this never checks
    // Hostable against it - only the neighbour, where the guest itself lives, is checked.
    private static SidingWallEntity? GuestAcrossFace(Block queriedBlock, IBlockAccessor blockAccessor, BlockPos pos, BlockFacing blockFace)
    {
        BlockPos neighbourPos = pos.AddCopy(blockFace);
        ICoreAPI? api = SidingModSystem.ApiRef(queriedBlock) ?? SidingModSystem.ApiRef(blockAccessor.GetBlock(neighbourPos));
        if (api == null) return null;

        SidingWallEntity? guest = GuestWalls.GuestAt(api, neighbourPos);
        return guest?.Block is SidingWallBlock wall && ClaimsFaceTowardPos(wall.Variant["layout"], wall.Variant["side"], blockFace.Code)
            ? guest : null;
    }

    // The geometric half of GuestAcrossFace, pulled out so it can be checked without a world: does
    // the guest's panel stand behind the queried face as seen from the far side, i.e. does the guest
    // claim the face pointing back the other way.
    internal static bool ClaimsFaceTowardPos(string layout, string side, string blockFaceCode)
        => SidingWallBlock.ClaimsFace(layout, side, BlockFacing.FromCode(blockFaceCode).Opposite.Code);
}
