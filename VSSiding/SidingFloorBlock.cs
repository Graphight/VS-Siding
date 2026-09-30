using System.Collections.Generic;
using System.Text;
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
    private Dictionary<EnumBlockMaterial, BlockSounds>? layerSounds;
    private Dictionary<EnumBlockMaterial, float>? layerResistance;

    public override void OnLoaded(ICoreAPI api)
    {
        base.OnLoaded(api);
        layerSounds = SidingWallBlock.LayerTable(Attributes["LayerSounds"], entry => entry.AsObject(new BlockSounds()));
        layerResistance = SidingWallBlock.LayerTable(Attributes["LayerResistance"], entry => entry.AsFloat(1f));
    }

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

    // Every floor's joists run north-south, so the north rim (framing-top) and the south rim
    // (framing-bottom) each drop where a framed floor carries the joists on, the way a stacked
    // wall drops its plates.
    internal static (bool above, bool below, bool left, bool right) Joins(IBlockAccessor accessor, BlockPos pos)
        => (ContinuesJoists(accessor, pos.NorthCopy()), ContinuesJoists(accessor, pos.SouthCopy()), false, false);

    private static bool ContinuesJoists(IBlockAccessor accessor, BlockPos neighbourPos)
        => accessor.GetBlockEntity<SidingFloorEntity>(neighbourPos)?.Framing != null;

    // Setting Framing isn't a block change, so the floors whose rims it drops have to be told.
    internal static void MarkNeighboursDirty(IWorldAccessor world, BlockPos pos)
    {
        world.BlockAccessor.GetBlockEntity<SidingFloorEntity>(pos.NorthCopy())?.MarkDirty(true);
        world.BlockAccessor.GetBlockEntity<SidingFloorEntity>(pos.SouthCopy())?.MarkDirty(true);
    }

    // The wall's peel order with the top as front and the underside as back (decision 0013):
    // the hit face's finish, then any finish, then the infill; null leaves only the joists.
    internal static string? PeelLayer(BlockFacing? hitFace, SidingFloorEntity entity)
        => SidingWallBlock.PeelLayer(hitFace == null ? null : FinishFace(hitFace), entity.Infill, entity.Front, null, entity.Back, null);

    private EnumBlockMaterial LayerMaterialAt(string? layer, SidingFloorEntity entity)
        => SidingWallBlock.LayerMaterial(layer, SidingWallBlock.LayerKey(layer, entity.Infill, entity.Front, null, entity.Back, null),
            Attributes["Infills"], Attributes["Finishes"], BlockMaterial);

    private EnumBlockMaterial HitLayerMaterial(IBlockAccessor accessor, BlockPos pos, BlockFacing? hitFace)
        => accessor.GetBlockEntity<SidingFloorEntity>(pos) is { } entity ? LayerMaterialAt(PeelLayer(hitFace, entity), entity) : BlockMaterial;

    public override BlockSounds GetSounds(IBlockAccessor blockAccessor, BlockSelection blockSel, ItemStack? stack = null)
    {
        if (blockSel?.Position == null) return base.GetSounds(blockAccessor, blockSel, stack);
        var material = HitLayerMaterial(blockAccessor, blockSel.Position, blockSel.Face);
        return SidingWallBlock.ResolveLayerSounds(material, layerSounds, base.GetSounds(blockAccessor, blockSel, stack));
    }

    public override float GetResistance(IBlockAccessor blockAccessor, BlockPos pos)
    {
        if (pos == null) return base.GetResistance(blockAccessor, pos);
        return SidingWallBlock.ResolveLayerResistance(HitLayerMaterial(blockAccessor, pos, null), layerResistance, base.GetResistance(blockAccessor, pos));
    }

    public override CombustibleProperties? GetCombustibleProperties(IWorldAccessor world, ItemStack? itemstack, BlockPos? pos)
    {
        if (pos == null) return base.GetCombustibleProperties(world, itemstack, pos);
        return SidingWallBlock.ResolveLayerCombustible(HitLayerMaterial(world.BlockAccessor, pos, null), base.GetCombustibleProperties(world, itemstack, pos));
    }

    public override EnumBlockMaterial GetBlockMaterial(IBlockAccessor blockAccessor, BlockPos pos, ItemStack? stack = null)
    {
        if (pos == null) return base.GetBlockMaterial(blockAccessor, pos, stack);
        return HitLayerMaterial(blockAccessor, pos, null);
    }

    // A player's break peels one layer, as on the wall; anything else, or bare joists, breaks the block.
    public override void OnBlockBroken(IWorldAccessor world, BlockPos pos, IPlayer byPlayer, float dropQuantityMultiplier = 1f)
    {
        var entity = world.BlockAccessor.GetBlockEntity<SidingFloorEntity>(pos);
        BlockSelection? selection = byPlayer?.CurrentBlockSelection;
        if (world.Side == EnumAppSide.Server)
        {
            selection = SidingWallBlock.ServerBreakSelection;
            SidingWallBlock.ServerBreakSelection = null;
        }
        BlockFacing? hitFace = selection?.Position.Equals(pos) == true ? selection.Face : null;
        string? layer = entity == null ? null : PeelLayer(hitFace, entity);
        if (entity == null || byPlayer == null || layer == null)
        {
            base.OnBlockBroken(world, pos, byPlayer, dropQuantityMultiplier);
            MarkNeighboursDirty(world, pos);
            return;
        }

        if (world.Side == EnumAppSide.Server && byPlayer.WorldData.CurrentGameMode != EnumGameMode.Creative)
        {
            var drops = new List<BlockDropItemStack>();
            SidingWallBlock.AddDrops(drops, SidingWallBlock.LayerKey(layer, entity.Infill, entity.Front, null, entity.Back, null),
                Attributes[layer == "infill" ? "Infills" : "Finishes"]);
            foreach (var stack in SidingWallBlock.ResolveDrops(world, drops, dropQuantityMultiplier, Code)) world.SpawnItemEntity(stack, pos);

            if (Sounds != null)
            {
                var breakSounds = SidingWallBlock.ResolveLayerSounds(LayerMaterialAt(layer, entity), layerSounds, Sounds);
                world.PlaySoundAt(breakSounds.GetBreakSound(byPlayer), pos, 0.0, byPlayer);
            }
        }
        SpawnBlockBrokenParticles(pos, byPlayer);

        switch (layer)
        {
            case "front": entity.Front = null; entity.MarkDirty(true); break;
            case "back": entity.Back = null; entity.MarkDirty(true); break;
            default:
                string? oldInfill = entity.Infill;
                entity.Infill = null;
                OnInfillChanged(world, entity, pos, oldInfill);
                break;
        }
    }

    public override ItemStack[] GetDrops(IWorldAccessor world, BlockPos pos, IPlayer byPlayer, float dropQuantityMultiplier)
    {
        var entity = world.BlockAccessor.GetBlockEntity<SidingFloorEntity>(pos);
        var drops = SidingWallBlock.ComputeDrops(entity?.Framing, entity?.Infill, entity?.Front, null, entity?.Back, null,
            Attributes["Framings"], Attributes["Infills"], Attributes["Finishes"]);
        // Nothing built yet: the base drops hand back the placed block.
        if (drops.Count == 0) return base.GetDrops(world, pos, byPlayer, dropQuantityMultiplier);
        return SidingWallBlock.ResolveDrops(world, drops, dropQuantityMultiplier, Code);
    }

    internal static string Describe(string? framing, string? infill, string? front, string? back, JsonObject attributes, System.Func<string, string?> translate)
    {
        JsonObject framings = attributes["Framings"], infills = attributes["Infills"], finishes = attributes["Finishes"];
        string? builtFraming = SidingWallBlock.Installed(framing, framings);
        string? builtInfill = SidingWallBlock.Installed(infill, infills);

        var sb = new StringBuilder();
        sb.AppendLine();
        sb.AppendLine("  " + SidingWallBlock.DescribeLayer(builtFraming, framings, "vssiding:tooltip-no-framing", translate));
        sb.AppendLine("  " + SidingWallBlock.DescribeLayer(builtInfill, infills, "vssiding:tooltip-no-infill", translate));
        sb.AppendLine("  " + string.Format(SidingWallBlock.Translate("vssiding:tooltip-top", translate),
            SidingWallBlock.DescribeLayer(SidingWallBlock.Installed(front, finishes), finishes, "vssiding:tooltip-unfinished", translate)));
        sb.AppendLine("  " + string.Format(SidingWallBlock.Translate("vssiding:tooltip-underside", translate),
            SidingWallBlock.DescribeLayer(SidingWallBlock.Installed(back, finishes), finishes, "vssiding:tooltip-unfinished", translate)));
        sb.AppendLine("  " + SidingWallBlock.Translate(SidingWallBlock.SealKey(builtFraming, builtInfill, framings, infills), translate));
        return sb.ToString();
    }
}
