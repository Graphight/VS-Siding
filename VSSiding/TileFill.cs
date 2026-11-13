using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;
using Vintagestory.GameContent;

namespace VSSiding;

// Added in code to the tiles a wall takes (decision 0095). Vanilla's GroundStorable takes a sneak-click on an up face it
// can attach to before the block sees it, so this goes ahead of it and hands that click to a Siding wall or floor, which
// fills the face (SidingWallBlock.TilesToLay). The client only claims the click; the server's run does the work.
public class TileFill : CollectibleBehavior
{
    private static readonly AssetLocation[] TileCodes = [new("game", "claytile-fired-*-*"), new("game", "tile-stone-travertine")];

    public TileFill(CollectibleObject collObj) : base(collObj)
    {
    }

    internal static bool IsTile(AssetLocation? code) => code != null && TileCodes.Any(tile => WildcardUtil.Match(tile, code));

    internal static CollectibleBehavior[] BeforeGroundStorable(CollectibleBehavior[] behaviors, CollectibleObject tile)
    {
        var rest = behaviors.Where(behavior => behavior is not TileFill).ToList();
        rest.Insert(System.Math.Max(0, rest.FindIndex(behavior => behavior is CollectibleBehaviorGroundStorable)), new TileFill(tile));
        return rest.ToArray();
    }

    public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handHandling, ref EnumHandling handling)
    {
        if (!firstEvent || blockSel == null || !byEntity.Controls.ShiftKey) return;

        var byPlayer = (byEntity as EntityPlayer)?.Player;
        if (byPlayer == null || !SidingWallBlock.HasBuildSignal(byPlayer)) return;

        Block block = byEntity.World.BlockAccessor.GetBlock(blockSel.Position);
        if (block is not (SidingWallBlock or SidingFloorBlock)) return;

        handling = EnumHandling.PreventSubsequent;
        handHandling = EnumHandHandling.PreventDefault;
        // The held-item path tests range alone; the block's own layering tests the claim (ClaimRefuses).
        if (byEntity.World.Side == EnumAppSide.Server) block.OnBlockInteractStart(byEntity.World, byPlayer, blockSel);
    }
}
