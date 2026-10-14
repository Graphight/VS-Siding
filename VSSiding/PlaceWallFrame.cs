using System;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace VSSiding;

// Patched onto game:itemtypes/resource/plank.json, stick.json and bone.json - see docs/decisions/0005/0006/0058/0060.
// A saw or a stone in the off hand tells this apart from Roofing's own plank-placing behavior (and is also the
// whole build flow's "you're building" signal, see SidingWallBlock.HasBuildSignal); the
// picker's framing row picks wall, cornerout or diagonal, and placement itself is handed to the
// placeholder wall block so its existing HorizontalOrientable behavior does the "hug the
// player's side" orientation. With floor picked the placeholder is the floor, which has no orientation.
public class PlaceWallFrame : CollectibleBehavior
{
    public PlaceWallFrame(CollectibleObject collObj) : base(collObj)
    {
    }

    public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handHandling, ref EnumHandling handling)
    {
        if (!firstEvent || blockSel == null) return;

        var byPlayer = (byEntity as EntityPlayer)?.Player;
        if (byPlayer == null || !SidingWallBlock.HasBuildSignal(byPlayer)) return;

        IWorldAccessor world = byEntity.World;
        var wallBlock = world.GetBlock(new AssetLocation("vssiding", "wall-wall-west")) as SidingWallBlock;
        if (wallBlock == null) return;

        AssetLocation heldCode = slot.Itemstack.Collectible.Code;
        string? framingKey = SidingWallBlock.MatchConsumes(heldCode, wallBlock.Attributes["Framings"]);
        if (framingKey == null) return;

        string layout = SidingModePicker.Layout(byPlayer);
        if (layout == "face")
        {
            if (TrySkinFace(slot, byPlayer, blockSel, heldCode, wallBlock))
            {
                handling = EnumHandling.PreventSubsequent;
                handHandling = EnumHandHandling.PreventDefault;
            }
            return;
        }

        // A filled wall whose face this click could not board turns into a corner in place (decision 0069).
        // Here and not in the wall's own interaction, which vanilla repeats while the button is held: the
        // repeat of the press that boarded a face would find it finished. Sneaking still frames the next cell.
        if (!byEntity.Controls.ShiftKey
            && world.BlockAccessor.GetBlock(blockSel.Position) is SidingWallBlock hitWall
            && world.BlockAccessor.GetBlockEntity<SidingWallEntity>(blockSel.Position) is { Infill: not null } hitEntity
            && !hitWall.IsDeckHit(world.BlockAccessor, blockSel, hitEntity)
            && hitWall.TryUpgradeLayout(world, byPlayer, blockSel, hitEntity, heldCode))
        {
            handling = EnumHandling.PreventSubsequent;
            handHandling = EnumHandHandling.PreventDefault;
            return;
        }

        bool withDeck = layout != "floor" && SidingModePicker.Deck(byPlayer);

        var consumes = wallBlock.Attributes["Framings"][framingKey]["Consumes"];
        int times = withDeck ? 2 : 1;
        bool isCreative = byPlayer.WorldData.CurrentGameMode == EnumGameMode.Creative;
        if (!SidingWallBlock.TryAffordOrError(byPlayer, isCreative, slot.Itemstack.StackSize, consumes, times)) return;
        var placeholder = world.GetBlock(new AssetLocation("vssiding", layout == "floor" ? "floor" : $"wall-{layout}-west"));
        if (placeholder == null) return;

        BlockPos targetPos = blockSel.Position;
        bool didOffset;
        if (layout == "floor" && blockSel.Face == BlockFacing.UP && world.BlockAccessor.GetBlock(targetPos) is SidingFloorBlock)
        {
            // Stacking a floor 12/16 above another is never wanted, so a click on a floor's top
            // extends its run instead, the way a rope ladder extends down: over water, a floor's
            // edge is only reachable by crouching out past it and looking back.
            BlockPos? end = SidingFloorBlock.RunEndFor(world, byPlayer, targetPos, placeholder);
            if (end == null) return;
            targetPos = end;
            didOffset = true;
        }
        else
        {
            didOffset = !world.BlockAccessor.GetBlock(targetPos).IsReplacableBy(placeholder);
            if (didOffset)
            {
                targetPos = targetPos.AddCopy(blockSel.Face);
            }
        }
        BlockSelection placeSel = blockSel.Clone();
        placeSel.Position = targetPos;
        placeSel.DidOffset = didOffset;

        string failureCode = "";
        bool placed = placeholder.TryPlaceBlock(world, byPlayer, new ItemStack(placeholder), placeSel, ref failureCode);
        if (!placed) return;

        if (world.BlockAccessor.GetBlockEntity<SidingFloorEntity>(targetPos) is { } floor)
        {
            floor.Framing = framingKey;
            floor.MarkDirty(true);
            SidingFloorBlock.MarkNeighboursDirty(world, targetPos);
        }

        var entity = world.BlockAccessor.GetBlockEntity<SidingWallEntity>(targetPos);
        if (entity != null)
        {
            entity.Framing = framingKey;
            // Vanilla's placement check ran before the entity existed, so it only saw the panel.
            if (withDeck && world.BlockAccessor.GetBlock(targetPos) is SidingWallBlock placedWall && placedWall.DeckOccupied(world, targetPos))
            {
                withDeck = false;
                times = 1;
            }
            if (withDeck) entity.Deck = framingKey;
            entity.MarkDirty(true);
            SidingWallBlock.MarkNeighboursDirty(world, targetPos);
        }

        SidingWallBlock.ConsumeHeld(slot, consumes, isCreative, times);

        handling = EnumHandling.PreventSubsequent;
        handHandling = EnumHandHandling.PreventDefault;
    }

    // Face mode puts a plank's vanilla decor block on the clicked face of any solid block (decision 0077).
    // Siding's own blocks, chiselled hosts and a face already carrying a decor are left alone.
    private static bool TrySkinFace(ItemSlot slot, IPlayer byPlayer, BlockSelection blockSel, AssetLocation heldCode, SidingWallBlock wallBlock)
    {
        IWorldAccessor world = byPlayer.Entity.World;
        IBlockAccessor accessor = world.BlockAccessor;
        BlockPos pos = blockSel.Position;
        Block host = accessor.GetBlock(pos);
        if (host is SidingWallBlock or SidingFloorBlock || host.GetInterface<IAcceptsDecor>(world, pos) != null) return false;

        EnumBlockMaterial material = host.GetBlockMaterial(accessor, pos);
        if (material is EnumBlockMaterial.Snow or EnumBlockMaterial.Ice) return false;

        string? wood = slot.Itemstack.Collectible.Variant["wood"];
        if (wood == null) return false;
        Block? decor = world.GetBlock(new AssetLocation("vssiding", $"facefinish-{wood}"));
        if (decor == null || !host.CanAttachBlockAt(accessor, decor, pos, blockSel.Face)) return false;

        var bits = new DecorBits(blockSel.Face);
        if (accessor.GetDecor(pos, bits) != null) return false;

        string? finishKey = SidingWallBlock.MatchConsumes(heldCode, wallBlock.Attributes["Finishes"]);
        if (finishKey == null) return false;
        var consumes = wallBlock.Attributes["Finishes"][finishKey]["Consumes"];
        bool isCreative = byPlayer.WorldData.CurrentGameMode == EnumGameMode.Creative;
        if (!SidingWallBlock.TryAffordOrError(byPlayer, isCreative, slot.Itemstack.StackSize, consumes)) return false;

        bool boards = SidingModePicker.ChoiceOf(byPlayer, Array.FindIndex(SidingModePicker.Rows, row => row.Key == "vssidingBoards")) == "boards";
        bits.Rotation = boards ? 1 : 0;
        accessor.SetDecor(decor, pos, bits);

        SidingWallBlock.ConsumeHeld(slot, consumes, isCreative);
        return true;
    }
}
