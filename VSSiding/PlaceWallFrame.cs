using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

namespace VSSiding;

// Patched onto game:itemtypes/resource/plank.json - see docs/decisions/0005/0006. A saw in
// the off hand tells this apart from Roofing's own plank-placing behavior (and is also the
// whole build flow's "you're building" signal, see SidingWallBlock.HasSawInOffhand); the
// tool mode picks wall vs cornerout, and placement itself is handed to the placeholder wall
// block so its existing HorizontalOrientable behavior does the "hug the player's side"
// orientation.
public class PlaceWallFrame : CollectibleBehavior
{
    private SkillItem[]? toolModes;

    public PlaceWallFrame(CollectibleObject collObj) : base(collObj)
    {
    }

    public override void OnLoaded(ICoreAPI api)
    {
        base.OnLoaded(api);
        if (api is not ICoreClientAPI capi) return;

        // The icon file is named after the mode's code, so the four tiles wire up in one pass.
        // ModeIconsTests keeps the two sets in step - LoadSvg returns null on a missing file and
        // the tile just draws blank. -1 is white: the source art is black like vanilla's own
        // icons, and every vanilla mode picker tints it at load rather than in the file.
        toolModes = ObjectCacheUtil.GetOrCreate(capi, "vssidingPlaceWallFrameToolModes", () => new[]
        {
            new SkillItem { Code = new AssetLocation("wall"), Name = Lang.Get("vssiding:toolmode-wall") },
            new SkillItem { Code = new AssetLocation("corner"), Name = Lang.Get("vssiding:toolmode-corner") },
            new SkillItem { Code = new AssetLocation("weatherboard"), Name = Lang.Get("vssiding:toolmode-weatherboard") },
            new SkillItem { Code = new AssetLocation("boards"), Name = Lang.Get("vssiding:toolmode-boards") },
        }.Select(item => item.WithIcon(capi, capi.Gui.LoadSvgWithPadding(
            new AssetLocation("vssiding", $"textures/icons/{item.Code.Path}.svg"), 48, 48, 5, -1))).ToArray());
    }

    public override SkillItem[] GetToolModes(ItemSlot slot, IClientPlayer forPlayer, BlockSelection blockSel)
        => toolModes!;

    public override int GetToolMode(ItemSlot slot, IPlayer byPlayer, BlockSelection blockSel)
        => ToolModeOf(slot);

    // SidingWallBlock reads the mode off a stack rather than a behavior instance, so the
    // attribute key lives here once instead of being spelled out at both call sites.
    internal static int ToolModeOf(ItemSlot slot) => slot.Itemstack?.Attributes.GetInt("toolMode", 0) ?? 0;

    public override void SetToolMode(ItemSlot slot, IPlayer byPlayer, BlockSelection blockSel, int toolMode)
        => slot.Itemstack.Attributes.SetInt("toolMode", toolMode);

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

        // A style mode never frames (decision 0027). Checked above the afford check, or a style
        // mode with too few planks would error about a framing cost nobody is being charged.
        string? layout = SidingWallBlock.ResolveLayout(GetToolMode(slot, byPlayer, blockSel));
        if (layout == null) return;

        var consumes = wallBlock.Attributes["Framings"][framingKey]["Consumes"];
        bool isCreative = byPlayer.WorldData.CurrentGameMode == EnumGameMode.Creative;
        if (!SidingWallBlock.TryAffordOrError(byPlayer, isCreative, slot.Itemstack.StackSize, consumes)) return;
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
            entity.MarkDirty(true);
            SidingWallBlock.MarkNeighboursDirty(world, targetPos);
        }

        SidingWallBlock.ConsumeHeld(slot, consumes, isCreative);

        handling = EnumHandling.PreventSubsequent;
        handHandling = EnumHandHandling.PreventDefault;
    }
}
