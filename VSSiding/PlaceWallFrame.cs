using System.Linq;
using Newtonsoft.Json.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
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

    // Behaviors answer a click in array order, so this one moves to just after vanilla's GroundStorable, ahead of
    // anything another mod appended for the same click (#143). Done in code: a JSON patch at a fixed index fails
    // once another mod has shortened the array. Without GroundStorable it goes first.
    internal static CollectibleBehavior[] AfterGroundStorable(CollectibleBehavior[] behaviors)
    {
        var rest = behaviors.Where(behavior => behavior is not PlaceWallFrame).ToList();
        rest.InsertRange(rest.FindIndex(behavior => behavior is CollectibleBehaviorGroundStorable) + 1, behaviors.OfType<PlaceWallFrame>());
        return rest.ToArray();
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

        // The build signal makes the click ours even when nothing is placed, so a later behavior such as
        // Immersive Woodworking's sawhorse never runs instead of a failed frame (#143).
        handling = EnumHandling.PreventSubsequent;
        handHandling = EnumHandHandling.PreventDefault;

        string layout = SidingModePicker.Layout(byPlayer);
        if (layout == "face")
        {
            TrySkinFace(slot, byPlayer, blockSel, heldCode, wallBlock);
            return;
        }

        // A filled wall whose face this click could not board turns into a corner in place (decision 0069).
        // Here and not in the wall's own interaction, which vanilla repeats while the button is held: the
        // repeat of the press that boarded a face would find it finished. Sneaking still frames the next cell.
        if (!byEntity.Controls.ShiftKey
            && world.BlockAccessor.GetBlock(blockSel.Position) is SidingWallBlock hitWall
            && world.BlockAccessor.GetBlockEntity<SidingWallEntity>(blockSel.Position) is { Infill: not null } hitEntity
            && !hitWall.IsDeckHit(world.BlockAccessor, blockSel, hitEntity)
            && hitWall.TryUpgradeLayout(world, byPlayer, blockSel, hitEntity, heldCode)) return;

        bool post = layout == "cornerin";
        bool withDeck = layout != "floor" && SidingModePicker.Deck(byPlayer);

        int frame = SidingWallBlock.ConsumeQuantity(wallBlock.Attributes["Framings"][framingKey]["Consumes"]);
        // A post's frame costs a quarter of a wall's; a deck costs a full frame on either.
        JsonObject Cost(bool deck) => new(new JObject { ["quantity"] = (post ? SidingWallBlock.PostQuarter(frame) : frame) + (deck ? frame : 0) });
        bool isCreative = byPlayer.WorldData.CurrentGameMode == EnumGameMode.Creative;
        if (!SidingWallBlock.TryAffordOrError(byPlayer, isCreative, slot.Itemstack.StackSize, Cost(withDeck))) return;
        var placeholder = world.GetBlock(new AssetLocation("vssiding", layout == "floor" ? "floor" : $"wall-{layout}-west"));
        if (placeholder == null) return;

        BlockPos targetPos = blockSel.Position;
        bool didOffset;
        if (layout == "floor" && blockSel.Face == BlockFacing.UP && world.BlockAccessor.GetBlock(targetPos) is SidingFloorBlock)
        {
            // Stacking a floor 12/16 above another is never wanted, so a click on a floor's top
            // extends its run instead, the way a rope ladder extends down: over water, a floor's
            // edge is only reachable by crouching out past it and looking back.
            BlockPos? end = SidingFloorBlock.RunEndFor(world, byPlayer, targetPos, placeholder, heldCode);
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
        // A floor laid up to a wall decks that wall; a click on the wall itself already offset to the cell beside it.
        if (layout == "floor" && world.BlockAccessor.GetBlock(targetPos) is SidingWallBlock deckWall
            && world.BlockAccessor.GetBlockEntity<SidingWallEntity>(targetPos) is { } deckEntity
            && deckWall.TryAddDeck(world, byPlayer, targetPos, deckEntity, slot, heldCode, isCreative))
            return;
        BlockSelection placeSel = blockSel.Clone();
        placeSel.Position = targetPos;
        placeSel.DidOffset = didOffset;

        string failureCode = "";
        bool placed;
        if (post)
        {
            // HorizontalOrientable would orient the post to the player; its corner is the click's instead.
            Vec3d inCell = blockSel.HitPosition.AddCopy(blockSel.Position.X - targetPos.X, blockSel.Position.Y - targetPos.Y, blockSel.Position.Z - targetPos.Z);
            var postBlock = world.GetBlock(new AssetLocation("vssiding", $"wall-cornerin-{PostSide(inCell.X, inCell.Z)}"));
            placed = postBlock != null && postBlock.CanPlaceBlock(world, byPlayer, placeSel, ref failureCode);
            if (placed) postBlock!.DoPlaceBlock(world, byPlayer, placeSel, new ItemStack(postBlock));
        }
        else
        {
            placed = placeholder.TryPlaceBlock(world, byPlayer, new ItemStack(placeholder), placeSel, ref failureCode);
        }
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
                withDeck = false;
            if (withDeck) entity.Deck = framingKey;
            entity.MarkDirty(true);
            SidingWallBlock.MarkNeighboursDirty(world, targetPos);
        }

        SidingWallBlock.ConsumeHeld(slot, Cost(withDeck), isCreative);
    }

    // The wall variant whose post corner (its side face plus the face counter-clockwise from it) is the cell
    // corner a click at x, z lands nearest; west and north are the low x and z.
    internal static string PostSide(double x, double z) => (x < 0.5, z < 0.5) switch
    {
        (true, true) => "west",
        (true, false) => "south",
        (false, false) => "east",
        (false, true) => "north",
    };

    // Face mode puts a plank's vanilla decor block on the clicked face of any solid block (decision 0077).
    // A face finish already there is swapped: the same wood only turns to the picked boards, for free, and
    // another wood drops the old planks. Siding's own blocks, chiselled hosts and any other decor are left alone.
    private static void TrySkinFace(ItemSlot slot, IPlayer byPlayer, BlockSelection blockSel, AssetLocation heldCode, SidingWallBlock wallBlock)
    {
        IWorldAccessor world = byPlayer.Entity.World;
        IBlockAccessor accessor = world.BlockAccessor;
        BlockPos pos = blockSel.Position;
        Block host = accessor.GetBlock(pos);
        if (host is SidingWallBlock or SidingFloorBlock || host.GetInterface<IAcceptsDecor>(world, pos) != null) return;

        EnumBlockMaterial material = host.GetBlockMaterial(accessor, pos);
        if (material is EnumBlockMaterial.Snow or EnumBlockMaterial.Ice) return;

        string? wood = slot.Itemstack.Collectible.Variant["wood"];
        if (wood == null) return;
        Block? decor = world.GetBlock(new AssetLocation("vssiding", $"facefinish-planks-{wood}"));
        if (decor == null || !host.CanAttachBlockAt(accessor, decor, pos, blockSel.Face)) return;

        var bits = new DecorBits(blockSel.Face) { Rotation = SidingModePicker.Boards(byPlayer) == "boards" ? 1 : 0 };
        // The chunk keys a decor by its rotation too, so the old one is found and removed by its own key.
        var old = accessor.GetSubDecors(pos)?.FirstOrDefault(entry => new DecorBits(entry.Key).Face == blockSel.Face.Index) ?? default;
        if (old.Value != null && (old.Value.Code.Domain != "vssiding" || old.Value.FirstCodePart() != "facefinish")) return;
        bool sameWood = old.Value?.Id == decor.Id;
        if (sameWood && new DecorBits(old.Key).Rotation == bits.Rotation) return;

        bool isCreative = byPlayer.WorldData.CurrentGameMode == EnumGameMode.Creative;
        JsonObject? consumes = null;
        if (!sameWood)
        {
            string? finishKey = SidingWallBlock.MatchConsumes(heldCode, wallBlock.Attributes["Finishes"]);
            if (finishKey == null) return;
            consumes = wallBlock.Attributes["Finishes"][finishKey]["Consumes"];
            if (!SidingWallBlock.TryAffordOrError(byPlayer, isCreative, slot.Itemstack.StackSize, consumes)) return;
        }

        if (old.Value != null)
        {
            accessor.SetDecor(world.GetBlock(0), pos, old.Key);
            if (!sameWood && !isCreative) old.Value.OnBrokenAsDecor(world, pos, blockSel.Face);
        }
        accessor.SetDecor(decor, pos, bits);

        if (consumes != null) SidingWallBlock.ConsumeHeld(slot, consumes, isCreative);
    }
}
