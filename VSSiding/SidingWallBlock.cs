using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;

[assembly: InternalsVisibleTo("VSSiding.Tests")]

namespace VSSiding;

public class SidingWallBlock : Block
{
    // cornerout's L covers `side` plus the face counter-clockwise from it:
    // west+north, south+west, east+south, north+east.
    private static readonly Dictionary<string, string> CorneroutSecondFace = new()
    {
        ["west"] = "north",
        ["south"] = "west",
        ["east"] = "south",
        ["north"] = "east",
    };

    // Saws come in per-metal variants (saw-copper, saw-meteoriciron, ...) - there is no bare
    // "saw" item, so this has to be a wildcard match, not an exact AssetLocation comparison.
    private static readonly AssetLocation SawCode = new("game", "saw-*");

    // Shared "are we in build mode" check for both framing (PlaceWallFrame) and layering
    // (below). A plain right-click, not shift - see decision 0006 for why shift was dropped.
    internal static bool HasSawInOffhand(IPlayer byPlayer)
    {
        AssetLocation? offhandCode = byPlayer.InventoryManager.OffhandHotbarSlot?.Itemstack?.Collectible.Code;
        return offhandCode != null && WildcardUtil.Match(SawCode, offhandCode);
    }

    // A saw in the off hand layers infill onto a framed wall, then finishes onto a filled
    // one - which face was clicked picks Front vs Back. Returns true for every handled
    // branch (including the wrong-face error) so vanilla's "place block against" fallthrough
    // doesn't also fire. Plain right-click, not shift - see decision 0006.
    public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
    {
        if (!HasSawInOffhand(byPlayer)) return base.OnBlockInteractStart(world, byPlayer, blockSel);

        ItemSlot slot = byPlayer.InventoryManager.ActiveHotbarSlot;
        AssetLocation? heldCode = slot.Itemstack?.Collectible.Code;
        if (heldCode == null) return base.OnBlockInteractStart(world, byPlayer, blockSel);

        var entity = world.BlockAccessor.GetBlockEntity<SidingWallEntity>(blockSel.Position);
        if (entity == null || entity.Framing == null) return base.OnBlockInteractStart(world, byPlayer, blockSel);

        bool isCreative = byPlayer.WorldData.CurrentGameMode == EnumGameMode.Creative;

        if (entity.Infill == null)
        {
            string? infillKey = MatchConsumes(heldCode, Attributes["Infills"]);
            if (infillKey == null) return base.OnBlockInteractStart(world, byPlayer, blockSel);

            var consumes = Attributes["Infills"][infillKey]["Consumes"];
            if (!TryAffordOrError(byPlayer, isCreative, slot.StackSize, consumes)) return true;

            entity.Infill = infillKey;
            entity.MarkDirty(true);
            ConsumeHeld(slot, consumes, isCreative);
            return true;
        }

        string? finishKey = MatchConsumes(heldCode, Attributes["Finishes"]);
        if (finishKey == null) return base.OnBlockInteractStart(world, byPlayer, blockSel);

        string side = Variant["side"];
        string? face = ResolveFinishFace(Variant["layout"], side, blockSel.Face);
        // Planks that can't finish this face still extend the wall via PlaceWallFrame.
        bool heldIsFraming = MatchConsumes(heldCode, Attributes["Framings"]) != null;
        if (face == null)
        {
            if (heldIsFraming) return base.OnBlockInteractStart(world, byPlayer, blockSel);
            (byPlayer as IServerPlayer)?.SendIngameError("vssiding:wrongface", Lang.Get("vssiding:build-wrong-face"));
            return true;
        }

        bool alreadyFinished = face == "front" ? entity.Front != null : entity.Back != null;
        if (alreadyFinished)
        {
            if (heldIsFraming) return base.OnBlockInteractStart(world, byPlayer, blockSel);
            (byPlayer as IServerPlayer)?.SendIngameError("vssiding:alreadyfinished", Lang.Get("vssiding:build-already-finished"));
            return true;
        }

        var finishConsumes = Attributes["Finishes"][finishKey]["Consumes"];
        if (!TryAffordOrError(byPlayer, isCreative, slot.StackSize, finishConsumes)) return true;

        if (face == "front") entity.Front = finishKey; else entity.Back = finishKey;
        entity.MarkDirty(true);
        ConsumeHeld(slot, finishConsumes, isCreative);
        return true;
    }

    // Shared by both build-flow steps (this class's layering, and PlaceWallFrame's framing)
    // so the afford-check-and-error path lives in exactly one place.
    internal static bool TryAffordOrError(IPlayer byPlayer, bool isCreative, int stackSize, JsonObject consumes)
    {
        if (CanAfford(isCreative, stackSize, consumes)) return true;
        (byPlayer as IServerPlayer)?.SendIngameError("vssiding:cantafford", Lang.Get("vssiding:build-cant-afford"));
        return false;
    }

    internal static void ConsumeHeld(ItemSlot slot, JsonObject consumes, bool isCreative)
    {
        if (isCreative) return;
        slot.TakeOut(ConsumeQuantity(consumes));
        slot.MarkDirty();
    }

    // A held stack too small to pay Consumes.quantity must not place/build - ItemSlot.TakeOut
    // silently takes whatever is available rather than failing, so the caller has to check first.
    // Creative players aren't charged at all.
    internal static bool CanAfford(bool isCreative, int stackSize, JsonObject consumes)
        => isCreative || stackSize >= ConsumeQuantity(consumes);

    public override int GetRetention(BlockPos pos, BlockFacing facing, EnumRetentionType type)
    {
        string side = Variant["side"];
        string layout = Variant["layout"];

        bool claimed = facing.Code == side;
        if (!claimed && layout == "cornerout")
        {
            claimed = facing.Code == CorneroutSecondFace[side];
        }

        var entity = api.World.BlockAccessor.GetBlockEntity<SidingWallEntity>(pos);
        return ComputeRetention(claimed, entity?.Framing, entity?.Infill, Attributes["Framings"], Attributes["Infills"]);
    }

    // sidesolid is false on every face (decision 0002), so base.GetRetention can't be
    // delegated to. A wall seals only once framing and infill are both built and still
    // valid in their dictionary; an uninstalled material counts as not built.
    internal static int ComputeRetention(bool claimed, string? framingKey, string? infillKey, JsonObject framings, JsonObject infills)
    {
        if (!claimed || framingKey == null || infillKey == null) return 0;

        var infill = infills[infillKey];
        if (!framings[framingKey].Exists || !infill.Exists) return 0;

        string? materialName = infill["BlockMaterial"].AsString(null!);
        bool cooling = Enum.TryParse(materialName, true, out EnumBlockMaterial material)
            && material is EnumBlockMaterial.Stone or EnumBlockMaterial.Ore
                or EnumBlockMaterial.Soil or EnumBlockMaterial.Ceramic;
        return cooling ? -1 : 1;
    }

    public override ItemStack[] GetDrops(IWorldAccessor world, BlockPos pos, IPlayer byPlayer, float dropQuantityMultiplier)
    {
        var entity = world.BlockAccessor.GetBlockEntity<SidingWallEntity>(pos);
        var drops = ComputeDrops(
            entity?.Framing, entity?.Infill, entity?.Front, entity?.Back,
            Attributes["Framings"], Attributes["Infills"], Attributes["Finishes"]);

        // Nothing built yet - fall back to the base drops so HorizontalOrientable still
        // hands back the placed block.
        if (drops.Count == 0) return base.GetDrops(world, pos, byPlayer, dropQuantityMultiplier);

        var stacks = new List<ItemStack>();
        foreach (var drop in drops)
        {
            drop.Resolve(world, "vssiding:wall drops", Code);
            var stack = drop.GetNextItemStack(dropQuantityMultiplier);
            if (stack != null) stacks.Add(stack);
        }
        return stacks.ToArray();
    }

    internal static List<BlockDropItemStack> ComputeDrops(
        string? framing, string? infill, string? front, string? back,
        JsonObject framings, JsonObject infills, JsonObject finishes)
    {
        var drops = new List<BlockDropItemStack>();
        AddDrops(drops, framing, framings);
        AddDrops(drops, infill, infills);
        AddDrops(drops, front, finishes);
        AddDrops(drops, back, finishes);
        return drops;
    }

    private static void AddDrops(List<BlockDropItemStack> drops, string? key, JsonObject dictionary)
    {
        if (key == null) return;
        var entry = dictionary[key];
        if (!entry.Exists) return;

        foreach (var drop in entry["Drops"].AsObject(Array.Empty<BlockDropItemStack>()))
        {
            if (drop.Code != null) drops.Add(drop);
        }
    }

    // Finds the material dictionary entry whose Consumes.code matches the held item, so a
    // build-flow behavior can turn "the player right-clicked with plank-oak" into "oak".
    internal static string? MatchConsumes(AssetLocation heldCode, JsonObject materials)
    {
        if (!materials.Exists) return null;

        foreach (var keyToken in materials)
        {
            string key = keyToken.AsString()!;
            var consumes = materials[key]["Consumes"];
            if (!consumes.Exists) continue;

            string? code = consumes["code"].AsString(null!);
            if (code == null) continue;

            if (WildcardUtil.Match(new AssetLocation(code), heldCode)) return key;
        }

        return null;
    }

    internal static int ConsumeQuantity(JsonObject consumes) => consumes["quantity"].AsInt(1);

    // Tool mode 0 is "wall", 1 is "corner" - see decision 0005. Anything else falls back
    // to "wall" rather than throwing on a stale/out-of-range stored mode.
    internal static string ResolveLayout(int toolMode) => toolMode == 1 ? "cornerout" : "wall";

    // Which finish layer a build-flow click's clicked face targets - the hugged side is
    // "front", the opposite side is "back", an end/top/bottom face is neither. A cornerout's
    // second leg shares the same Front/Back, so its outer and inner faces count too.
    internal static string? ResolveFinishFace(string layout, string side, BlockFacing clickedFace)
    {
        if (FinishFaceFor(side, clickedFace) is string face) return face;
        return layout == "cornerout" ? FinishFaceFor(CorneroutSecondFace[side], clickedFace) : null;
    }

    private static string? FinishFaceFor(string side, BlockFacing clickedFace)
    {
        if (clickedFace.Code == side) return "front";
        if (clickedFace == BlockFacing.FromCode(side).Opposite) return "back";
        return null;
    }
}
