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

    // Unrotated ("west") framing boxes per layout, matching the framing elements in
    // wall.json/cornerout.json: full-height posts, then top plates. Bottom plates don't
    // collide - standing on one would lift the player into a two-high doorway's top plate.
    private static readonly Dictionary<string, (Cuboidf[] posts, Cuboidf[] top)> UnrotatedFramingBoxes = new()
    {
        ["wall"] = (
            new[]
            {
                new Cuboidf(1f / 16, 0, 0, 3f / 16, 1, 1f / 16),
                new Cuboidf(1f / 16, 0, 15f / 16, 3f / 16, 1, 1),
            },
            new[] { new Cuboidf(1f / 16, 15f / 16, 1f / 16, 3f / 16, 1, 15f / 16) }),
        ["cornerout"] = (
            new[]
            {
                new Cuboidf(1f / 16, 0, 1f / 16, 3f / 16, 1, 3f / 16),
                new Cuboidf(1f / 16, 0, 15f / 16, 3f / 16, 1, 1),
                new Cuboidf(15f / 16, 0, 1f / 16, 1, 1, 3f / 16),
            },
            new[]
            {
                new Cuboidf(1f / 16, 15f / 16, 3f / 16, 3f / 16, 1, 15f / 16),
                new Cuboidf(3f / 16, 15f / 16, 1f / 16, 15f / 16, 1, 3f / 16),
            }),
    };

    // Built once up front so collision calls from client and server threads only ever read it.
    private static readonly Dictionary<(string layout, string side, bool joinsAbove), Cuboidf[]> FramingBoxes = BuildFramingBoxes();

    private static Dictionary<(string layout, string side, bool joinsAbove), Cuboidf[]> BuildFramingBoxes()
    {
        var origin = new Vec3d(0.5, 0.5, 0.5);
        var boxes = new Dictionary<(string layout, string side, bool joinsAbove), Cuboidf[]>();
        foreach (var (layout, (posts, top)) in UnrotatedFramingBoxes)
        {
            foreach (string side in CorneroutSecondFace.Keys)
            {
                float rotationYDeg = SidingWallEntity.RotationYDeg(side);
                foreach (bool joinsAbove in new[] { false, true })
                {
                    var unrotated = new List<Cuboidf>(posts);
                    if (!joinsAbove) unrotated.AddRange(top);
                    boxes[(layout, side, joinsAbove)] =
                        unrotated.ConvertAll(box => box.RotatedCopy(0, rotationYDeg, 0, origin)).ToArray();
                }
            }
        }
        return boxes;
    }

    // A frame with framing but no infill collides only on its posts and top plate (decision 0008).
    internal static Cuboidf[] ComputeCollisionBoxes(
        string layout, string side, string? framing, string? infill, bool joinsAbove, Cuboidf[] fullBoxes)
        => framing != null && infill == null ? FramingBoxes[(layout, side, joinsAbove)] : fullBoxes;

    public override Cuboidf[] GetCollisionBoxes(IBlockAccessor blockAccessor, BlockPos pos)
        => FramedCollisionBoxes(blockAccessor, pos, base.GetCollisionBoxes(blockAccessor, pos));

    public override Cuboidf[] GetParticleCollisionBoxes(IBlockAccessor blockAccessor, BlockPos pos)
        => FramedCollisionBoxes(blockAccessor, pos, base.GetParticleCollisionBoxes(blockAccessor, pos));

    private Cuboidf[] FramedCollisionBoxes(IBlockAccessor blockAccessor, BlockPos pos, Cuboidf[] fullBoxes)
    {
        var entity = blockAccessor.GetBlockEntity<SidingWallEntity>(pos);
        if (entity?.Framing == null || entity.Infill != null) return fullBoxes;

        var (joinsAbove, _) = StackJoins(blockAccessor, pos, entity.Infill);
        return ComputeCollisionBoxes(Variant["layout"], Variant["side"], entity.Framing, entity.Infill, joinsAbove, fullBoxes);
    }

    // Whether the cell at pos shares its top/bottom with the frame above/below, i.e. draws no
    // plate there. Every second cell up a stack keeps its top plate as a cross-beam.
    internal (bool joinsAbove, bool joinsBelow) StackJoins(IBlockAccessor blockAccessor, BlockPos pos, string? infill)
    {
        int cellsBelow = 0;
        for (BlockPos p = pos.DownCopy(); ContinuesFrame(blockAccessor, p, infill); p.Down()) cellsBelow++;
        return (JoinsAbove(ContinuesFrame(blockAccessor, pos.UpCopy(), infill), cellsBelow), cellsBelow > 0);
    }

    internal static bool JoinsAbove(bool continuesAbove, int cellsBelow) => continuesAbove && cellsBelow % 2 == 0;

    private bool ContinuesFrame(IBlockAccessor blockAccessor, BlockPos neighbourPos, string? infill)
    {
        if (blockAccessor.GetBlock(neighbourPos) is not SidingWallBlock neighbourBlock) return false;
        if (neighbourBlock.Variant["layout"] != Variant["layout"]) return false;
        if (neighbourBlock.Variant["side"] != Variant["side"]) return false;
        return SharesStack(infill, blockAccessor.GetBlockEntity<SidingWallEntity>(neighbourPos));
    }

    // Any framing counts, so mixed woods are one stack, but open and filled cells aren't:
    // a plate marks where a doorway frame meets filled wall (decision 0008).
    internal static bool SharesStack(string? infill, SidingWallEntity? neighbour)
        => neighbour?.Framing != null && (neighbour.Infill == null) == (infill == null);

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

            // The full slab, not the open frame's collision: infill would seal in anyone standing in it.
            var occupants = world.GetIntersectingEntities(blockSel.Position, base.GetCollisionBoxes(world.BlockAccessor, blockSel.Position), e => e.IsInteractable);
            if (occupants is { Length: > 0 })
            {
                (byPlayer as IServerPlayer)?.SendIngameError("vssiding:occupied", Lang.Get("vssiding:build-occupied"));
                return true;
            }

            var consumes = Attributes["Infills"][infillKey]["Consumes"];
            if (!TryAffordOrError(byPlayer, isCreative, slot.StackSize, consumes)) return true;

            entity.Infill = infillKey;
            entity.MarkDirty(true);
            MarkVerticalNeighboursDirty(world, blockSel.Position);
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

    // Which plates a cell draws depends on the cells above and below it (decision 0008).
    public override void OnNeighbourBlockChange(IWorldAccessor world, BlockPos pos, BlockPos neibpos)
    {
        base.OnNeighbourBlockChange(world, pos, neibpos);
        if (neibpos.X != pos.X || neibpos.Z != pos.Z) return;
        if (neibpos.Y == pos.Y + 1) world.BlockAccessor.GetBlockEntity<SidingWallEntity>(pos)?.MarkDirty(true);
        if (neibpos.Y == pos.Y - 1) MarkStackDirtyFrom(world, pos);
    }

    // Setting Framing or Infill isn't a block change, so the stack around it has to be told.
    internal static void MarkVerticalNeighboursDirty(IWorldAccessor world, BlockPos pos)
    {
        world.BlockAccessor.GetBlockEntity<SidingWallEntity>(pos.DownCopy())?.MarkDirty(true);
        MarkStackDirtyFrom(world, pos.UpCopy());
    }

    // Cross-beams alternate up a stack, so a change low down shifts every cell above it.
    private static void MarkStackDirtyFrom(IWorldAccessor world, BlockPos pos)
    {
        for (BlockPos p = pos.Copy(); world.BlockAccessor.GetBlockEntity<SidingWallEntity>(p) is { Framing: not null } entity; p.Up())
        {
            entity.MarkDirty(true);
        }
    }

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
