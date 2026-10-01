using System.Collections.Generic;
using System.Linq;
using System.Text;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace VSSiding;

// A thin floor: the wall's layers laid flat at the top of the cell (decision 0050).
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
    // top or the underside. Anything unclaimed falls through, so planks still extend the run (PlaceWallFrame).
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
        if (SidingWallBlock.IsTransparent(entity.Infill, Attributes["Infills"]))
        {
            if (heldPlaces) return base.OnBlockInteractStart(world, byPlayer, blockSel);
            (byPlayer as IServerPlayer)?.SendIngameError("vssiding:glazed", Lang.Get("vssiding:build-glazed"));
            return true;
        }

        string? face = FinishFace(blockSel.Face);
        if (face == null)
        {
            if (heldPlaces) return base.OnBlockInteractStart(world, byPlayer, blockSel);
            (byPlayer as IServerPlayer)?.SendIngameError("vssiding:wrongface", Lang.Get("vssiding:build-floor-wrong-face"));
            return true;
        }

        // Only a face whose FloorElements lists the picked style takes it, so weatherboard, or boards
        // on a daub top, falls back to the face's default.
        string? style = SidingModePicker.FinishChoices(byPlayer).FirstOrDefault(s => SidingFloorEntity.HasFloorStyle(Attributes["Finishes"][finishKey], face, s));

        string? currentKey = face == "front" ? entity.Front : entity.Back;
        string? currentStyle = face == "front" ? entity.FrontStyle : entity.BackStyle;
        if (currentKey != null)
        {
            // Restyling the same material is free: only the boards' direction changes.
            if (style != null && currentKey == finishKey && currentStyle != style)
            {
                SetFinish(entity, face, finishKey, style);
                return true;
            }

            if (heldPlaces) return base.OnBlockInteractStart(world, byPlayer, blockSel);
            (byPlayer as IServerPlayer)?.SendIngameError("vssiding:alreadyfinished", Lang.Get("vssiding:build-already-finished"));
            return true;
        }

        var finishConsumes = Attributes["Finishes"][finishKey]["Consumes"];
        if (!SidingWallBlock.TryAffordOrError(byPlayer, isCreative, slot.StackSize, finishConsumes)) return true;

        SetFinish(entity, face, finishKey, style);
        SidingWallBlock.ConsumeHeld(slot, finishConsumes, isCreative);
        return true;
    }

    private static void SetFinish(SidingFloorEntity entity, string face, string finishKey, string? style)
    {
        if (face == "front") { entity.Front = finishKey; entity.FrontStyle = style; }
        else { entity.Back = finishKey; entity.BackStyle = style; }
        entity.MarkDirty(true);
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
        MarkNeighboursDirty(world, pos);
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
    // wall drops its plates. A glazed floor has no joists: its bezel merges on all four sides,
    // and only with glazed floors, as glazing does on a wall (decision 0019). A wall's deck is
    // laid the same way and counts as a neighbour wherever it reaches the shared edge.
    internal static (bool above, bool below, bool left, bool right) Joins(IBlockAccessor accessor, BlockPos pos, bool glazed)
        => glazed
            ? (Continues(accessor, pos, BlockFacing.NORTH, true), Continues(accessor, pos, BlockFacing.SOUTH, true),
                Continues(accessor, pos, BlockFacing.WEST, true), Continues(accessor, pos, BlockFacing.EAST, true))
            : (Continues(accessor, pos, BlockFacing.NORTH, false), Continues(accessor, pos, BlockFacing.SOUTH, false), false, false);

    // Whether the cell `direction` of `pos` carries on the joists, or the glazing when `glazing`.
    private static bool Continues(IBlockAccessor accessor, BlockPos pos, BlockFacing direction, bool glazing)
    {
        BlockPos neighbourPos = pos.AddCopy(direction);
        if (accessor.GetBlockEntity<SidingFloorEntity>(neighbourPos) is { } floor)
            return Continues(glazing, floor.Framing, floor.Infill, floor.Block.Attributes["Infills"]);
        return accessor.GetBlockEntity<SidingWallEntity>(neighbourPos) is { } wall
            && DeckReaches(wall.Block.Variant["layout"], wall.Block.Variant["side"], direction)
            && Continues(glazing, wall.Deck, wall.DeckInfill, wall.Block.Attributes["Infills"]);
    }

    internal static bool Continues(bool glazing, string? framing, string? infill, JsonObject infills)
        => framing != null && (!glazing || SidingWallBlock.IsTransparent(infill, infills));

    // A wall's panel stands on the faces it claims, and its deck stops short of them; `direction`
    // is from the cell looking on to the wall, so the face it would touch is the opposite one.
    internal static bool DeckReaches(string layout, string side, BlockFacing direction)
        => !SidingWallBlock.ClaimsFace(layout, side, direction.Opposite.Code);

    // How far past the clicked floor a run can be extended, so a stray click cannot frame one across a lake.
    internal const int RunReach = 4;

    // The first cell past the end of the floor run from `start`, walking `ahead`, if it can be built
    // into and lies within RunReach of `start`; null otherwise.
    internal static BlockPos? RunEnd(System.Func<BlockPos, bool> isFloor, System.Func<BlockPos, bool> replaceable, BlockPos start, BlockFacing ahead)
    {
        for (int step = 1; step <= RunReach; step++)
        {
            BlockPos pos = start.AddCopy(ahead, step);
            if (isFloor(pos)) continue;
            return replaceable(pos) ? pos : null;
        }
        return null;
    }

    // The horizontal face the player is looking toward, whatever the pitch.
    internal static BlockFacing Ahead(Vec3f view)
        => System.Math.Abs(view.X) >= System.Math.Abs(view.Z)
            ? (view.X > 0 ? BlockFacing.EAST : BlockFacing.WEST)
            : (view.Z > 0 ? BlockFacing.SOUTH : BlockFacing.NORTH);

    // Setting Framing or Infill isn't a block change, so the floors and wall decks whose rims or bezel it drops have to be told.
    internal static void MarkNeighboursDirty(IWorldAccessor world, BlockPos pos)
    {
        foreach (BlockFacing side in BlockFacing.HORIZONTALS)
        {
            world.BlockAccessor.GetBlockEntity<SidingFloorEntity>(pos.AddCopy(side))?.MarkDirty(true);
            world.BlockAccessor.GetBlockEntity<SidingWallEntity>(pos.AddCopy(side))?.MarkDirty(true);
        }
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
        // A hit on a rug breaks the rug first (OnBlockBroken), so it sounds like one: vanilla's own lookup.
        if (blockSel.Face != null && blockAccessor.GetDecor(blockSel.Position, new DecorBits(blockSel.Face)) != null)
            return base.GetSounds(blockAccessor, blockSel, stack);
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

        // A rug or carpet on the hit face comes off before any layer, the way furniture in a wall's
        // cell does. Vanilla only breaks decor first in survival, after a quarter second of hitting.
        if (hitFace != null && world.BlockAccessor.GetDecor(pos, new DecorBits(hitFace)) != null)
        {
            world.BlockAccessor.BreakDecor(pos, hitFace);
            return;
        }

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
        RemoveLayer(world, entity, pos, layer);
    }

    // As on the wall (decision 0043): a fire that burns out on a floor takes its topmost layer, and
    // only a wood one; bare joists have none left, so vanilla deletes the block.
    internal bool TryBurnLayer(IWorldAccessor world, BlockPos pos)
    {
        var entity = world.BlockAccessor.GetBlockEntity<SidingFloorEntity>(pos);
        string? layer = entity == null ? null : PeelLayer(null, entity);
        if (layer == null) return false;

        if (LayerMaterialAt(layer, entity!) == EnumBlockMaterial.Wood && world.Side == EnumAppSide.Server) RemoveLayer(world, entity!, pos, layer);
        return true;
    }

    private void RemoveLayer(IWorldAccessor world, SidingFloorEntity entity, BlockPos pos, string layer)
    {
        switch (layer)
        {
            case "front": entity.Front = null; entity.FrontStyle = null; entity.MarkDirty(true); break;
            case "back": entity.Back = null; entity.BackStyle = null; entity.MarkDirty(true); break;
            default:
                string? oldInfill = entity.Infill;
                entity.Infill = null;
                OnInfillChanged(world, entity, pos, oldInfill);
                // Decor only goes on a sealed top (CanAttachBlockAt), so a rug left on bare joists
                // after peeling from below or a fire comes off with the infill.
                world.BlockAccessor.BreakDecor(pos, BlockFacing.UP);
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
