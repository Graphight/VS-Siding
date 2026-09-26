using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace VSSiding;

// Patched onto game:itemtypes/resource/plank.json - see docs/decisions/0005/0006. A saw in
// the off hand tells this apart from Roofing's own plank-placing behavior (and is also the
// whole build flow's "you're building" signal, see SidingWallBlock.HasSawInOffhand); the
// picker's framing row picks wall vs cornerout, and placement itself is handed to the
// placeholder wall block so its existing HorizontalOrientable behavior does the "hug the
// player's side" orientation.
public class PlaceWallFrame : CollectibleBehavior
{
    public PlaceWallFrame(CollectibleObject collObj) : base(collObj)
    {
    }

    public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handHandling, ref EnumHandling handling)
    {
        if (!firstEvent || blockSel == null) return;

        var byPlayer = (byEntity as EntityPlayer)?.Player;
        if (byPlayer == null || !SidingWallBlock.HasSawInOffhand(byPlayer)) return;

        IWorldAccessor world = byEntity.World;
        var wallBlock = world.GetBlock(new AssetLocation("vssiding", "wall-wall-west")) as SidingWallBlock;
        if (wallBlock == null) return;

        string? framingKey = SidingWallBlock.MatchConsumes(slot.Itemstack.Collectible.Code, wallBlock.Attributes["Framings"]);
        if (framingKey == null) return;

        string layout = SidingModePicker.Layout(byPlayer);
        bool withDeck = SidingModePicker.Deck(byPlayer);

        var consumes = wallBlock.Attributes["Framings"][framingKey]["Consumes"];
        int times = withDeck ? 2 : 1;
        bool isCreative = byPlayer.WorldData.CurrentGameMode == EnumGameMode.Creative;
        if (!SidingWallBlock.TryAffordOrError(byPlayer, isCreative, slot.Itemstack.StackSize, consumes, times)) return;
        var placeholder = world.GetBlock(new AssetLocation("vssiding", $"wall-{layout}-west"));
        if (placeholder == null) return;

        BlockPos targetPos = blockSel.Position;
        bool didOffset = !world.BlockAccessor.GetBlock(targetPos).IsReplacableBy(placeholder);
        if (didOffset)
        {
            targetPos = targetPos.AddCopy(blockSel.Face);
        }
        BlockSelection placeSel = blockSel.Clone();
        placeSel.Position = targetPos;
        placeSel.DidOffset = didOffset;

        string failureCode = "";
        bool placed = placeholder.TryPlaceBlock(world, byPlayer, new ItemStack(placeholder), placeSel, ref failureCode);
        if (!placed) return;

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
}
