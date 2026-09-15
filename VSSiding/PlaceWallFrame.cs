using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;

namespace VSSiding;

// Patched onto game:itemtypes/resource/plank.json - see docs/decisions/0005. A saw in the
// off hand tells this apart from Roofing's own plank-placing behavior; the tool mode picks
// wall vs cornerout, and placement itself is handed to the placeholder wall block so its
// existing HorizontalOrientable behavior does the "hug the player's side" orientation.
public class PlaceWallFrame : CollectibleBehavior
{
    private static readonly AssetLocation SawCode = new("game", "saw");

    private SkillItem[]? toolModes;

    public PlaceWallFrame(CollectibleObject collObj) : base(collObj)
    {
    }

    public override void OnLoaded(ICoreAPI api)
    {
        base.OnLoaded(api);
        if (api is not ICoreClientAPI capi) return;

        toolModes = ObjectCacheUtil.GetOrCreate(capi, "vssidingPlaceWallFrameToolModes", () => new[]
        {
            new SkillItem { Code = new AssetLocation("wall"), Name = Lang.Get("vssiding:toolmode-wall") },
            new SkillItem { Code = new AssetLocation("corner"), Name = Lang.Get("vssiding:toolmode-corner") },
        });
    }

    public override SkillItem[] GetToolModes(ItemSlot slot, IClientPlayer forPlayer, BlockSelection blockSel)
        => toolModes!;

    public override int GetToolMode(ItemSlot slot, IPlayer byPlayer, BlockSelection blockSel)
        => slot.Itemstack.Attributes.GetInt("toolMode", 0);

    public override void SetToolMode(ItemSlot slot, IPlayer byPlayer, BlockSelection blockSel, int toolMode)
        => slot.Itemstack.Attributes.SetInt("toolMode", toolMode);

    public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handHandling, ref EnumHandling handling)
    {
        if (!firstEvent || blockSel == null) return;

        AssetLocation? offhandCode = byEntity.LeftHandItemSlot?.Itemstack?.Collectible.Code;
        if (offhandCode == null || offhandCode != SawCode) return;

        IWorldAccessor world = byEntity.World;
        var wallBlock = world.GetBlock(new AssetLocation("vssiding", "wall-wall-west")) as SidingWallBlock;
        if (wallBlock == null) return;

        string? framingKey = SidingWallBlock.MatchConsumes(slot.Itemstack.Collectible.Code, wallBlock.Attributes["Framings"]);
        if (framingKey == null) return;

        var byPlayer = (byEntity as EntityPlayer)?.Player;
        if (byPlayer == null) return;

        var consumes = wallBlock.Attributes["Framings"][framingKey]["Consumes"];
        bool isCreative = byPlayer.WorldData.CurrentGameMode == EnumGameMode.Creative;
        if (!SidingWallBlock.CanAfford(isCreative, slot.Itemstack.StackSize, consumes))
        {
            (byPlayer as IServerPlayer)?.SendIngameError("vssiding:cantafford", Lang.Get("vssiding:build-cant-afford"));
            return;
        }

        string layout = SidingWallBlock.ResolveLayout(GetToolMode(slot, byPlayer, blockSel));
        var placeholder = world.GetBlock(new AssetLocation("vssiding", $"wall-{layout}-west"));
        if (placeholder == null) return;

        BlockPos targetPos = blockSel.Position;
        if (!world.BlockAccessor.GetBlock(targetPos).IsReplacableBy(placeholder))
        {
            targetPos = targetPos.AddCopy(blockSel.Face);
        }
        BlockSelection placeSel = blockSel.Clone();
        placeSel.Position = targetPos;
        placeSel.DidOffset = true;

        string failureCode = "";
        bool placed = placeholder.TryPlaceBlock(world, byPlayer, new ItemStack(placeholder), placeSel, ref failureCode);
        if (!placed) return;

        var entity = world.BlockAccessor.GetBlockEntity<SidingWallEntity>(targetPos);
        if (entity != null)
        {
            entity.Framing = framingKey;
            entity.MarkDirty(true);
        }

        if (!isCreative)
        {
            slot.TakeOut(SidingWallBlock.ConsumeQuantity(consumes));
            slot.MarkDirty();
        }

        handling = EnumHandling.PreventSubsequent;
        handHandling = EnumHandHandling.PreventDefault;
    }
}
