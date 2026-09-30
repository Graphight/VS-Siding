using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace VSSiding;

// A thin floor: the wall's layers laid flat at the top of the cell (proposal thin-floor-framing).
// Deliberately not a SidingWallBlock, so none of the wall's patches ever see one.
public class SidingFloorBlock : Block
{
    // The wall's build flow laid flat: infill on any face of a framed floor, then a finish on the
    // top or the underside. Anything unclaimed falls through, so planks still frame the next
    // floor over (PlaceWallFrame).
    public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
    {
        if (!SidingWallBlock.HasSawInOffhand(byPlayer)) return base.OnBlockInteractStart(world, byPlayer, blockSel);

        ItemSlot slot = byPlayer.InventoryManager.ActiveHotbarSlot;
        AssetLocation? heldCode = slot.Itemstack?.Collectible.Code;
        var entity = world.BlockAccessor.GetBlockEntity<SidingFloorEntity>(blockSel.Position);
        if (heldCode == null || entity?.Framing == null) return base.OnBlockInteractStart(world, byPlayer, blockSel);

        bool isCreative = byPlayer.WorldData.CurrentGameMode == EnumGameMode.Creative;

        if (entity.Infill == null)
        {
            string? infillKey = SidingWallBlock.MatchConsumes(heldCode, Attributes["Infills"]);
            if (infillKey == null) return base.OnBlockInteractStart(world, byPlayer, blockSel);

            // A glass floor is thin-floor-finishes' problem.
            if (SidingWallBlock.IsTransparent(infillKey, Attributes["Infills"]))
            {
                (byPlayer as IServerPlayer)?.SendIngameError("vssiding:glazedfloor", Lang.Get("vssiding:build-glazed-floor"));
                return true;
            }

            var consumes = Attributes["Infills"][infillKey]["Consumes"];
            if (!SidingWallBlock.TryAffordOrError(byPlayer, isCreative, slot.StackSize, consumes)) return true;

            entity.Infill = infillKey;
            OnInfillChanged(world, entity, blockSel.Position, null);
            SidingWallBlock.ConsumeHeld(slot, consumes, isCreative);
            return true;
        }

        string? finishKey = SidingWallBlock.MatchConsumes(heldCode, Attributes["Finishes"]);
        if (finishKey == null) return base.OnBlockInteractStart(world, byPlayer, blockSel);

        bool heldPlaces = slot.Itemstack!.Class == EnumItemClass.Block || SidingWallBlock.MatchConsumes(heldCode, Attributes["Framings"]) != null;
        string? face = FinishFace(blockSel.Face);
        if (face == null)
        {
            if (heldPlaces) return base.OnBlockInteractStart(world, byPlayer, blockSel);
            (byPlayer as IServerPlayer)?.SendIngameError("vssiding:wrongface", Lang.Get("vssiding:build-floor-wrong-face"));
            return true;
        }

        if ((face == "front" ? entity.Front : entity.Back) != null)
        {
            if (heldPlaces) return base.OnBlockInteractStart(world, byPlayer, blockSel);
            (byPlayer as IServerPlayer)?.SendIngameError("vssiding:alreadyfinished", Lang.Get("vssiding:build-already-finished"));
            return true;
        }

        var finishConsumes = Attributes["Finishes"][finishKey]["Consumes"];
        if (!SidingWallBlock.TryAffordOrError(byPlayer, isCreative, slot.StackSize, finishConsumes)) return true;

        if (face == "front") entity.Front = finishKey;
        else entity.Back = finishKey;
        entity.MarkDirty(true);
        SidingWallBlock.ConsumeHeld(slot, finishConsumes, isCreative);
        return true;
    }

    // The top is the floor's front and the underside its back; the edges take no finish.
    internal static string? FinishFace(BlockFacing clickedFace)
        => clickedFace == BlockFacing.UP ? "front" : clickedFace == BlockFacing.DOWN ? "back" : null;

    internal void OnInfillChanged(IWorldAccessor world, SidingFloorEntity entity, BlockPos pos, string? oldInfill)
    {
        entity.MarkDirty(true);
        MarkAbsorptionChanged(world.BlockAccessor, pos, entity.Framing, oldInfill);
        // Rooms only recompute on a chunk-dirty event, as on the wall; exchanging the block for itself fires one.
        world.BlockAccessor.ExchangeBlock(Id, pos);
    }

    // Only the top is ever claimed: the room below walks into the open part and meets it there.
    public override int GetRetention(BlockPos pos, BlockFacing facing, EnumRetentionType type)
    {
        var entity = api.World.BlockAccessor.GetBlockEntity<SidingFloorEntity>(pos);
        return ComputeRetention(facing, entity?.Framing, entity?.Infill, Attributes);
    }

    internal static int ComputeRetention(BlockFacing facing, string? framing, string? infill, JsonObject attributes)
        => SidingWallBlock.ComputeRetention(facing == BlockFacing.UP, framing, infill, attributes["Framings"], attributes["Infills"]);

    // sidesolid on UP would let anything stand on bare joists; only a sealed top holds it.
    public override bool CanAttachBlockAt(IBlockAccessor blockAccessor, Block block, BlockPos pos, BlockFacing blockFace, Cuboidi? attachmentArea = null)
    {
        var entity = blockAccessor.GetBlockEntity<SidingFloorEntity>(pos);
        return ComputeRetention(blockFace, entity?.Framing, entity?.Infill, Attributes) != 0;
    }

    public override int GetLightAbsorption(IBlockAccessor blockAccessor, BlockPos pos)
        => GetLightAbsorption(blockAccessor.GetChunkAtBlockPos(pos), pos);

    // As on the wall: an open frame lets light through, a sealed floor is opaque.
    public override int GetLightAbsorption(IWorldChunk chunk, BlockPos pos)
    {
        var entity = chunk?.GetLocalBlockEntityAtBlockPos(pos) as SidingFloorEntity;
        return SidingWallBlock.ComputeLightAbsorption(entity?.Framing, entity?.Infill, Attributes["Framings"], Attributes["Infills"]);
    }

    // The relight skips when old and new absorption match, so it needs the old infill's real value (decision 0034).
    internal void MarkAbsorptionChanged(IBlockAccessor accessor, BlockPos pos, string? framing, string? oldInfill)
        => accessor.MarkAbsorptionChanged(
            SidingWallBlock.ComputeLightAbsorption(framing, oldInfill, Attributes["Framings"], Attributes["Infills"]),
            GetLightAbsorption(accessor, pos), pos);

    // The unrotated floor's joists run along x, and RotationYDeg turns its west end to face `side`,
    // so the rim drawn as framing-bottom faces `side` and framing-top faces the opposite way. Each
    // drops where the next floor carries the joists on, the way a stacked wall drops its plates.
    internal (bool above, bool below, bool left, bool right) Joins(IBlockAccessor accessor, BlockPos pos)
    {
        var side = BlockFacing.FromCode(Variant["side"]);
        return (ContinuesJoists(accessor, pos.AddCopy(side.Opposite)), ContinuesJoists(accessor, pos.AddCopy(side)), false, false);
    }

    private bool ContinuesJoists(IBlockAccessor accessor, BlockPos neighbourPos)
        => accessor.GetBlock(neighbourPos) is SidingFloorBlock neighbour
            && JoistsAlign(Variant["side"], neighbour.Variant["side"])
            && accessor.GetBlockEntity<SidingFloorEntity>(neighbourPos)?.Framing != null;

    // Setting Framing isn't a block change, so the floors whose rims it drops have to be told.
    internal static void MarkNeighboursDirty(IWorldAccessor world, BlockPos pos)
    {
        foreach (var face in BlockFacing.HORIZONTALS)
        {
            world.BlockAccessor.GetBlockEntity<SidingFloorEntity>(pos.AddCopy(face))?.MarkDirty(true);
        }
    }

    internal static bool JoistsAlign(string side, string neighbourSide)
        => BlockFacing.FromCode(side).Axis == BlockFacing.FromCode(neighbourSide).Axis;
}
