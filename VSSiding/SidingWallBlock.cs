using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
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
    // Merging never reaches here: it needs transparent infill, and any infill means the full slab.
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

        var joins = NeighbourJoins(blockAccessor, pos, entity.Infill);
        return ComputeCollisionBoxes(Variant["layout"], Variant["side"], entity.Framing, entity.Infill, joins.above, fullBoxes);
    }

    // Which neighbours this cell shares a member with, i.e. draws no plate or post against.
    internal (bool above, bool below, bool left, bool right) NeighbourJoins(
        IBlockAccessor blockAccessor, BlockPos pos, string? infill)
    {
        // Glazing merges with the glazing around it in every direction, with no member between,
        // so a run of it reads as one sheet however large. Opaque fill keeps decision 0008's
        // alternating cross-beam. That is why this asks the infill and not the layout: merging
        // belongs to glass, not to a shape. A cornerout's three posts stay put either way -
        // corners are structural and a corner has nothing to merge along.
        if (IsTransparent(infill, Attributes["Infills"]))
        {
            bool above = ContinuesGlazing(blockAccessor, pos.UpCopy(), infill);
            bool below = ContinuesGlazing(blockAccessor, pos.DownCopy(), infill);
            if (Variant["layout"] == "cornerout") return (above, below, false, false);
            var (left, right) = RunNeighbours(Variant["side"]);
            return (above, below,
                ContinuesGlazing(blockAccessor, pos.AddCopy(left), infill),
                ContinuesGlazing(blockAccessor, pos.AddCopy(right), infill));
        }

        int cellsBelow = 0;
        for (BlockPos p = pos.DownCopy(); ContinuesFrame(blockAccessor, p, infill); p.Down()) cellsBelow++;
        return (JoinsAbove(ContinuesFrame(blockAccessor, pos.UpCopy(), infill), cellsBelow), cellsBelow > 0, false, false);
    }

    private bool ContinuesGlazing(IBlockAccessor blockAccessor, BlockPos neighbourPos, string? infill)
        => SameRun(blockAccessor, neighbourPos)
            && ContinuesGlazing(infill, blockAccessor.GetBlockEntity<SidingWallEntity>(neighbourPos), Attributes["Infills"]);

    // Glazing only merges into more glazing: against a wattle-filled neighbour, or a bare frame,
    // the post stays - that is a join between two different walls, not one continuous sheet.
    internal static bool ContinuesGlazing(string? infill, SidingWallEntity? neighbour, JsonObject infills)
        => SharesStack(infill, neighbour) && IsTransparent(neighbour?.Infill, infills);

    internal static bool JoinsAbove(bool continuesAbove, int cellsBelow) => continuesAbove && cellsBelow % 2 == 0;

    // The two horizontal directions a run extends along - the ones in the wall's own plane.
    // "Left" is the z = 0 end of the unrotated shape, which is the face counter-clockwise from
    // `side`: exactly where cornerout's second leg sits, so that table already names it.
    internal static (BlockFacing left, BlockFacing right) RunNeighbours(string side)
    {
        BlockFacing left = BlockFacing.FromCode(CorneroutSecondFace[side]);
        return (left, left.Opposite);
    }

    // Which cornerout a wall becomes when it's upgraded in place (decision 0026): the new leg
    // goes on the end of the run clicked nearer. cornerout-`side` puts its second leg on the
    // left end; cornerout-`right` puts its own second leg back on `side`, so the leg it adds
    // is the right one. Nearness has to be a dot product rather than a fixed "coordinate <
    // 0.5" because the run's axis and its direction both change with `side`. Ties go right.
    internal static string ResolveCornerUpgrade(string side, Vec3d hitPosition)
    {
        var (left, right) = RunNeighbours(side);
        double towardsLeft = (hitPosition.X - 0.5) * left.Normali.X + (hitPosition.Z - 0.5) * left.Normali.Z;
        return towardsLeft > 0 ? side : right.Code;
    }

    // Same shape, same face: a wall only ever joins another leg of the same run.
    private bool SameRun(IBlockAccessor blockAccessor, BlockPos neighbourPos)
        => blockAccessor.GetBlock(neighbourPos) is SidingWallBlock neighbour
            && neighbour.Variant["layout"] == Variant["layout"]
            && neighbour.Variant["side"] == Variant["side"];

    private bool ContinuesFrame(IBlockAccessor blockAccessor, BlockPos neighbourPos, string? infill)
        => SameRun(blockAccessor, neighbourPos)
            && SharesStack(infill, blockAccessor.GetBlockEntity<SidingWallEntity>(neighbourPos));

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

        // A style mode only finishes (decision 0027), so every refusal below is an error the
        // player sees rather than a silent fallthrough. Resolved above the infill branch, which
        // would otherwise return on "held item is not an infill" and say nothing at all.
        string? style = ResolveStyle(PlaceWallFrame.ToolModeOf(slot));
        if (style != null && entity.Infill == null)
        {
            (byPlayer as IServerPlayer)?.SendIngameError("vssiding:needsinfill", Lang.Get("vssiding:build-needs-infill"));
            return true;
        }

        if (entity.Infill == null)
        {
            // A bare frame clicked in corner mode becomes a cornerout in place, for a T-junction
            // found once a partition reaches it (decision 0026). Nothing is charged: a fresh
            // cornerout frame costs the same as a fresh wall frame. Everything this doesn't
            // claim falls through to the infill match below, then to PlaceWallFrame.
            if (Variant["layout"] == "wall"
                && ResolveLayout(PlaceWallFrame.ToolModeOf(slot)) == "cornerout"
                && MatchConsumes(heldCode, Attributes["Framings"]) != null
                && ResolveFinishFace("wall", Variant["side"], blockSel.Face) != null)
            {
                string cornerSide = ResolveCornerUpgrade(Variant["side"], blockSel.HitPosition);
                var corner = world.GetBlock(new AssetLocation("vssiding", $"wall-cornerout-{cornerSide}"));
                if (corner != null)
                {
                    // Keeps the block entity, and the engine repoints its Block at the new
                    // type, so Framing survives and OnTesselation reads the cornerout layout.
                    world.BlockAccessor.ExchangeBlock(corner.Id, blockSel.Position);
                    entity.MarkDirty(true);
                    // Plates key off the cells above and below sharing this one's layout
                    // (decision 0008), which the swap just changed.
                    MarkNeighboursDirty(world, blockSel.Position);
                    return true;
                }
            }

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
            OnInfillChanged(world, entity, blockSel.Position);
            ConsumeHeld(slot, consumes, isCreative);
            return true;
        }

        string? finishKey = MatchConsumes(heldCode, Attributes["Finishes"]);
        if (finishKey == null) return base.OnBlockInteractStart(world, byPlayer, blockSel);

        // Planks that can't finish this face still extend the wall via PlaceWallFrame, and held blocks still place.
        bool heldPlaces = style == null
            && (slot.Itemstack!.Class == EnumItemClass.Block || MatchConsumes(heldCode, Attributes["Framings"]) != null);

        // Glazing takes no finish: a slab over it would just hide the glass. Refusing here rather
        // than in ResolveFinishFace keeps breaking unchanged - PeelLayer still finds no finish on
        // a glazed cell and peels the glass out (decision 0013).
        if (IsTransparent(entity.Infill, Attributes["Infills"]))
        {
            if (heldPlaces) return base.OnBlockInteractStart(world, byPlayer, blockSel);
            (byPlayer as IServerPlayer)?.SendIngameError("vssiding:glazed", Lang.Get("vssiding:build-glazed"));
            return true;
        }

        string side = Variant["side"];
        string? face = ResolveFinishFace(Variant["layout"], side, blockSel.Face);
        if (face == null)
        {
            if (heldPlaces) return base.OnBlockInteractStart(world, byPlayer, blockSel);
            (byPlayer as IServerPlayer)?.SendIngameError("vssiding:wrongface", Lang.Get("vssiding:build-wrong-face"));
            return true;
        }

        if (style != null && !HasStyle(Attributes["Finishes"][finishKey], style))
        {
            (byPlayer as IServerPlayer)?.SendIngameError("vssiding:nostyle", Lang.Get("vssiding:build-no-style"));
            return true;
        }

        string? currentKey = face switch { "front" => entity.Front, "secondfront" => entity.SecondFront, _ => entity.Back };
        string? currentStyle = face switch { "front" => entity.FrontStyle, "secondfront" => entity.SecondFrontStyle, _ => entity.BackStyle };
        if (currentKey != null)
        {
            // Restyling the same material is free: only the profile changes, so there is nothing
            // to charge for and nothing to drop.
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
        if (!TryAffordOrError(byPlayer, isCreative, slot.StackSize, finishConsumes)) return true;

        SetFinish(entity, face, finishKey, style);
        ConsumeHeld(slot, finishConsumes, isCreative);
        return true;
    }

    private static void SetFinish(SidingWallEntity entity, string face, string finishKey, string? style)
    {
        switch (face)
        {
            case "front": entity.Front = finishKey; entity.FrontStyle = style; break;
            case "secondfront": entity.SecondFront = finishKey; entity.SecondFrontStyle = style; break;
            default: entity.Back = finishKey; entity.BackStyle = style; break;
        }
        entity.MarkDirty(true);
    }

    // Only a finish that lists a style can be asked for it, so a style mode refuses daub and
    // brick rather than naming an element their shape hasn't got.
    internal static bool HasStyle(JsonObject finish, string style)
        => Array.IndexOf(finish["Styles"].AsArray<string>([]) ?? [], style) >= 0;

    private void OnInfillChanged(IWorldAccessor world, SidingWallEntity entity, BlockPos pos)
    {
        entity.MarkDirty(true);
        world.BlockAccessor.MarkAbsorptionChanged(0, GetLightAbsorption(world.BlockAccessor, pos), pos);
        // Infill changes retention, but rooms only recompute on a chunk-dirty event; exchanging the block for itself fires one.
        world.BlockAccessor.ExchangeBlock(Id, pos);
        // It changes the liquid barrier too, and the block itself never changed, so the water
        // beside it has no idea. Without this a wall only starts damming once something else
        // nearby happens to make the neighbours recalculate - and only stops damming then too.
        world.BlockAccessor.TriggerNeighbourBlockUpdate(pos);
        MarkNeighboursDirty(world, pos);
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
        if (neibpos.X != pos.X || neibpos.Z != pos.Z)
        {
            // Only glazing merges sideways, and only along its own run, so those are the only
            // horizontal neighbours that can change what this cell draws - an opaque wall never
            // joins one. Just this cell: merging is local, nothing propagates past the neighbour.
            if (neibpos.Y != pos.Y || Variant["layout"] == "cornerout") return;
            var entity = world.BlockAccessor.GetBlockEntity<SidingWallEntity>(pos);
            if (entity == null || !IsTransparent(entity.Infill, Attributes["Infills"])) return;
            var (left, right) = RunNeighbours(Variant["side"]);
            if (neibpos.Equals(pos.AddCopy(left)) || neibpos.Equals(pos.AddCopy(right))) entity.MarkDirty(true);
            return;
        }
        if (neibpos.Y == pos.Y + 1) world.BlockAccessor.GetBlockEntity<SidingWallEntity>(pos)?.MarkDirty(true);
        if (neibpos.Y == pos.Y - 1) MarkStackDirtyFrom(world, pos);
    }

    // Setting Framing or Infill isn't a block change, so the neighbours around it have to be told.
    internal static void MarkNeighboursDirty(IWorldAccessor world, BlockPos pos)
    {
        world.BlockAccessor.GetBlockEntity<SidingWallEntity>(pos.DownCopy())?.MarkDirty(true);
        MarkStackDirtyFrom(world, pos.UpCopy());

        // Unconditional, unlike OnNeighbourBlockChange's check on this cell's own glazing: this
        // fires when infill changes, and peeling glass out has to redraw the neighbours that were
        // merged with it - by which point this cell is no longer glazed. No walk either way.
        if (world.BlockAccessor.GetBlock(pos) is not SidingWallBlock block || block.Variant["layout"] == "cornerout") return;
        var (left, right) = RunNeighbours(block.Variant["side"]);
        world.BlockAccessor.GetBlockEntity<SidingWallEntity>(pos.AddCopy(left))?.MarkDirty(true);
        world.BlockAccessor.GetBlockEntity<SidingWallEntity>(pos.AddCopy(right))?.MarkDirty(true);
    }

    // Cross-beams alternate up a stack, so a change low down shifts every cell above it.
    private static void MarkStackDirtyFrom(IWorldAccessor world, BlockPos pos)
    {
        for (BlockPos p = pos.Copy(); world.BlockAccessor.GetBlockEntity<SidingWallEntity>(p) is { Framing: not null } entity; p.Up())
        {
            entity.MarkDirty(true);
        }
    }

    // The faces this block's panels actually cover: the hugged side, plus a cornerout's second leg.
    internal bool ClaimsFace(BlockFacing facing) => ClaimsFace(Variant["layout"], Variant["side"], facing.Code);

    internal static bool ClaimsFace(string layout, string side, string faceCode)
        => faceCode == side || (layout == "cornerout" && faceCode == CorneroutSecondFace[side]);

    public override int GetRetention(BlockPos pos, BlockFacing facing, EnumRetentionType type)
    {
        var entity = api.World.BlockAccessor.GetBlockEntity<SidingWallEntity>(pos);
        return ComputeRetention(ClaimsFace(facing), entity?.Framing, entity?.Infill, Attributes["Framings"], Attributes["Infills"]);
    }

    // Vanilla derives this from SideSolid, which is false on every face (decision 0002) so a thin
    // wall doesn't cull its neighbours - leaving every siding wall with a barrier of 0 and water
    // pouring through the fluid layer. A wall that seals air seals water too, so this asks exactly
    // what GetRetention asks. Glazing counts: it retains, so it dams, light notwithstanding.
    public override float GetLiquidBarrierHeightOnSide(BlockFacing face, BlockPos pos)
    {
        var entity = api.World.BlockAccessor.GetBlockEntity<SidingWallEntity>(pos);
        return ComputeLiquidBarrier(ClaimsFace(face), entity?.Framing, entity?.Infill, Attributes["Framings"], Attributes["Infills"]);
    }

    // Full height or nothing: a wall either dams its face or it doesn't. A cooling infill retains
    // negatively but still dams, so this asks whether retention is non-zero, not what sign it has.
    internal static float ComputeLiquidBarrier(bool claimed, string? framingKey, string? infillKey, JsonObject framings, JsonObject infills)
        => ComputeRetention(claimed, framingKey, infillKey, framings, infills) != 0 ? 1f : 0f;

    // Another SideSolid consumer (decision 0020): with sidesolid off, nothing could be hung on any
    // siding wall. attachmentArea is ignored - a sealed face is solid across its whole 16x16.
    public override bool CanAttachBlockAt(IBlockAccessor blockAccessor, Block block, BlockPos pos, BlockFacing blockFace, Cuboidi? attachmentArea = null)
    {
        var entity = blockAccessor.GetBlockEntity<SidingWallEntity>(pos);
        return ComputeRetention(ClaimsFace(blockFace), entity?.Framing, entity?.Infill, Attributes["Framings"], Attributes["Infills"]) != 0;
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

    // Looking at a built wall names its layers - otherwise a boarded infill is unreadable
    // short of breaking it, and the infill is what decides cellar vs warm room (decision 0015).
    // Takes a translate delegate (the entity passes Lang.GetIfExists) so this needs no loaded Lang.
    // System.Func is spelled out throughout: Vintagestory.API.Common declares a Func of its own.
    internal static string Describe(
        string? framing, string? infill, JsonObject framings, JsonObject infills,
        string layout, string side, string? front, string? secondFront, string? back, JsonObject finishes,
        System.Func<string, string?> translate)
    {
        string? builtFraming = Installed(framing, framings);
        string? builtInfill = Installed(infill, infills);

        var sb = new StringBuilder();
        sb.AppendLine();
        sb.AppendLine("  " + DescribeLayer(builtFraming, framings, "vssiding:tooltip-no-framing", translate));
        sb.AppendLine("  " + DescribeLayer(builtInfill, infills, "vssiding:tooltip-no-infill", translate));
        foreach (string line in DescribeFaces(layout, side, front, secondFront, back, finishes, translate))
            sb.AppendLine("  " + line);
        sb.AppendLine("  " + Translate(SealKey(builtFraming, builtInfill, framings, infills), translate));
        return sb.ToString();
    }

    // The line a cellar builder actually reads: whether the wall seals the room at all, and
    // if so, whether the infill makes it a cooling wall (decision 0015).
    private static string SealKey(string? framing, string? infill, JsonObject framings, JsonObject infills)
        => ComputeRetention(true, framing, infill, framings, infills) switch
        {
            1 => "vssiding:tooltip-sealed",
            -1 => "vssiding:tooltip-sealed-cool",
            _ => "vssiding:tooltip-unsealed",
        };

    // Faces are named by the direction they point - the only vocabulary that covers a cornerout's
    // three finishable faces without inventing words for them. Both of its legs share one Back
    // layer, so two directions carry the same finish; grouping by first appearance is what puts
    // them on one line even with a SecondFront direction between them.
    private static IEnumerable<string> DescribeFaces(
        string layout, string side, string? front, string? secondFront, string? back, JsonObject finishes,
        System.Func<string, string?> translate)
    {
        string? builtFront = Installed(front, finishes), builtBack = Installed(back, finishes);
        var directions = new List<(string direction, string? finish)> { (side, builtFront), (Opposite(side), builtBack) };
        if (layout == "cornerout")
        {
            string secondSide = CorneroutSecondFace[side];
            directions.Add((secondSide, Installed(secondFront, finishes)));
            directions.Add((Opposite(secondSide), builtBack));
        }

        foreach (var group in directions.GroupBy(d => d.finish))
        {
            string labels = string.Join(", ", group.Select(d => Translate($"game:facing-{d.direction}", translate)));
            yield return $"{labels}: {DescribeLayer(group.Key, finishes, "vssiding:tooltip-unfinished", translate)}";
        }
    }

    private static string Opposite(string side) => BlockFacing.FromCode(side).Opposite.Code;

    // An uninstalled material counts as not built, which is the invariant ComputeRetention already
    // holds to. Without this a stale key renders as a title-cased pseudo-material while the seal
    // line two rows below calls the same wall empty. Normalizing before the faces are grouped also
    // keeps a stale finish in the same group as an unfinished one, rather than on a line of its own.
    private static string? Installed(string? key, JsonObject materials) => key != null && materials[key].Exists ? key : null;

    // A gap prints the "no-x" line; a built layer resolves its DisplayName, falling back to a
    // title-cased material key when the entry has no DisplayName or the key has no translation.
    private static string DescribeLayer(string? key, JsonObject materials, string missingLangKey, System.Func<string, string?> translate)
    {
        if (key == null) return Translate(missingLangKey, translate);

        string? displayName = materials[key]["DisplayName"].AsString(null!);
        return (displayName != null ? translate(displayName) : null) ?? TitleCase(key);
    }

    // Our own keys ship in en.json beside the code, so a miss means a broken install: show the
    // raw key rather than dressing it up. A material's DisplayName is the case worth dressing up,
    // since a game update can add a wood the lang file has never heard of.
    private static string Translate(string langKey, System.Func<string, string?> translate) => translate(langKey) ?? langKey;

    private static string TitleCase(string key)
        => string.Join(' ', key.Split('-').Select(word => word.Length == 0 ? word : char.ToUpperInvariant(word[0]) + word[1..]));

    public override int GetLightAbsorption(IBlockAccessor blockAccessor, BlockPos pos)
        => GetLightAbsorption(blockAccessor.GetChunkAtBlockPos(pos), pos);

    // lightAbsorption is 0 in wall.json so an open frame lets light through; a sealed wall
    // is opaque, or sunlight through it warms the room and cancels a cellar.
    public override int GetLightAbsorption(IWorldChunk chunk, BlockPos pos)
    {
        var entity = chunk?.GetLocalBlockEntityAtBlockPos(pos) as SidingWallEntity;
        return ComputeLightAbsorption(entity?.Framing, entity?.Infill, Attributes["Framings"], Attributes["Infills"]);
    }

    // Sealing a room and blocking light are separate questions: glazing does the first and must
    // not do the second. An infill marked Transparent absorbs nothing, which also takes its cell
    // out of the room skylight patch (decision 0015), side AO (0016) and both 0018 patches, since
    // all of them ask this.
    internal static int ComputeLightAbsorption(string? framingKey, string? infillKey, JsonObject framings, JsonObject infills)
        => ComputeRetention(true, framingKey, infillKey, framings, infills) != 0
            && !IsTransparent(infillKey, infills) ? 99 : 0;

    // Whether an infill is see-through: it decides both that absorption and which render pass
    // the infill's mesh goes in (SidingWallEntity.OnTesselation).
    internal static bool IsTransparent(string? infillKey, JsonObject infills)
        => infillKey != null && infills[infillKey]["Transparent"].AsBool(false);

    // A sealed wall's cell stores outside sunlight (decision 0015); emitting side AO stops smooth lighting averaging it into neighbouring faces' corners.
    public override bool DoEmitSideAo(IGeometryTester caller, BlockFacing facing)
        => IsSealed(caller.GetCurrentBlockEntityOnSide(facing.Opposite)) || base.DoEmitSideAo(caller, facing);

    public override bool DoEmitSideAoByFlag(IGeometryTester caller, Vec3iAndFacingFlags vec, int flags)
        => IsSealed(caller.GetCurrentBlockEntityOnSide(vec)) || base.DoEmitSideAoByFlag(caller, vec, flags);

    internal bool IsSealed(BlockEntity? be)
        => be is SidingWallEntity entity && ComputeLightAbsorption(entity.Framing, entity.Infill, Attributes["Framings"], Attributes["Infills"]) > 0;

    // The horizontal step from a wall cell to the cell its dead space opens onto: away from the panel, diagonally for a cornerout.
    internal static (int dx, int dz) OpenSide(string layout, string side)
    {
        var open = BlockFacing.FromCode(side).Opposite.Normali;
        if (layout != "cornerout") return (open.X, open.Z);
        var second = BlockFacing.FromCode(CorneroutSecondFace[side]).Opposite.Normali;
        return (open.X + second.X, open.Z + second.Z);
    }

    // A sealed wall's cell stores the sunlight flowing in from outside, which RoomRegistry would count as sky (decision 0015).
    internal static int RoomSunlight(IBlockAccessor accessor, BlockPos pos, EnumLightLevelType type)
        => accessor.GetBlock(pos) is SidingWallBlock wall && wall.GetLightAbsorption(accessor, pos) > 0 ? 0 : accessor.GetLightLevel(pos, type);

    // Keyed by EnumBlockMaterial name (LayerSounds in wall.json), parsed once here rather than
    // AsObject<BlockSounds>() per hit - that's a full Newtonsoft parse and GetSounds runs every tick.
    private Dictionary<EnumBlockMaterial, BlockSounds>? layerSounds;

    // Keyed by EnumBlockMaterial name (LayerResistance in wall.json); multiplies Resistance.
    private Dictionary<EnumBlockMaterial, float>? layerResistance;

    public override void OnLoaded(ICoreAPI api)
    {
        base.OnLoaded(api);
        layerSounds = new Dictionary<EnumBlockMaterial, BlockSounds>();
        var soundEntries = Attributes["LayerSounds"];
        if (soundEntries.Exists)
        {
            foreach (var keyToken in soundEntries)
            {
                string key = keyToken.AsString()!;
                if (Enum.TryParse(key, true, out EnumBlockMaterial material))
                    layerSounds[material] = soundEntries[key].AsObject(new BlockSounds());
            }
        }

        layerResistance = new Dictionary<EnumBlockMaterial, float>();
        var resistanceEntries = Attributes["LayerResistance"];
        if (resistanceEntries.Exists)
        {
            foreach (var keyToken in resistanceEntries)
            {
                string key = keyToken.AsString()!;
                if (Enum.TryParse(key, true, out EnumBlockMaterial material))
                    layerResistance[material] = resistanceEntries[key].AsFloat(1f);
            }
        }
    }

    // Which EnumBlockMaterial is under the cursor: resolves the clicked face to a finish layer
    // (or the infill, or the frame) the same way OnBlockBroken decides what to peel.
    internal EnumBlockMaterial HitLayerMaterial(IBlockAccessor accessor, BlockPos pos, BlockFacing? hitFace)
    {
        var entity = accessor.GetBlockEntity<SidingWallEntity>(pos);
        if (entity == null) return BlockMaterial;

        string? face = hitFace == null ? null : ResolveFinishFace(Variant["layout"], Variant["side"], hitFace);
        string? layer = PeelLayer(face, entity.Infill, entity.Front, entity.SecondFront, entity.Back);
        return LayerMaterialAt(layer, entity.Infill, entity.Front, entity.SecondFront, entity.Back);
    }

    private EnumBlockMaterial LayerMaterialAt(string? layer, string? infill, string? front, string? secondFront, string? back)
        => LayerMaterial(layer, LayerKey(layer, infill, front, secondFront, back), Attributes["Infills"], Attributes["Finishes"], BlockMaterial);

    // The fallback is a parameter rather than read from Sounds here, so GetSounds can defer to
    // base.GetSounds while OnBlockBroken defers to Sounds.
    internal static BlockSounds ResolveLayerSounds(EnumBlockMaterial material, Dictionary<EnumBlockMaterial, BlockSounds>? layerSounds, BlockSounds fallback)
        => layerSounds != null && layerSounds.TryGetValue(material, out var sounds) ? sounds : fallback;

    public override BlockSounds GetSounds(IBlockAccessor blockAccessor, BlockSelection blockSel, ItemStack? stack = null)
    {
        // Vanilla's own GetSounds ignores its arguments, so a caller is free to pass no selection.
        if (blockSel?.Position == null) return base.GetSounds(blockAccessor, blockSel, stack);

        var material = HitLayerMaterial(blockAccessor, blockSel.Position, blockSel.Face);
        return ResolveLayerSounds(material, layerSounds, base.GetSounds(blockAccessor, blockSel, stack));
    }

    // Unknown/missing material falls through to the block's own Resistance, passed in by the caller.
    internal static float ResolveLayerResistance(EnumBlockMaterial material, Dictionary<EnumBlockMaterial, float>? layerResistance, float resistance)
        => layerResistance != null && layerResistance.TryGetValue(material, out var multiplier) ? resistance * multiplier : resistance;

    // No hit face reaches this hook, so the layer under the cursor falls back to the topmost finish,
    // then infill, then frame - PeelLayer's own fallback order with a null face.
    public override float GetResistance(IBlockAccessor blockAccessor, BlockPos pos)
    {
        if (pos == null) return base.GetResistance(blockAccessor, pos);

        var material = HitLayerMaterial(blockAccessor, pos, null);
        return ResolveLayerResistance(material, layerResistance, base.GetResistance(blockAccessor, pos));
    }

    // pos may be null, with only a stack to go on, so that falls back to base. The API warns this
    // may run off the main thread; the entity reads below are all immutable strings, so a racing
    // build reads either the old layer or the new one, never a torn value.
    public override EnumBlockMaterial GetBlockMaterial(IBlockAccessor blockAccessor, BlockPos pos, ItemStack? stack = null)
    {
        if (pos == null) return base.GetBlockMaterial(blockAccessor, pos, stack);

        return HitLayerMaterial(blockAccessor, pos, null);
    }

    // A player's break peels one layer (decision 0013); anything else, or a bare frame, breaks the block.
    internal static BlockSelection? ServerBreakSelection;

    public override void OnBlockBroken(IWorldAccessor world, BlockPos pos, IPlayer byPlayer, float dropQuantityMultiplier = 1f)
    {
        var entity = world.BlockAccessor.GetBlockEntity<SidingWallEntity>(pos);
        BlockSelection? selection = byPlayer?.CurrentBlockSelection;
        if (world.Side == EnumAppSide.Server)
        {
            selection = ServerBreakSelection;
            ServerBreakSelection = null;
        }
        BlockFacing? hitFace = selection?.Position.Equals(pos) == true ? selection.Face : null;
        string? face = hitFace == null ? null : ResolveFinishFace(Variant["layout"], Variant["side"], hitFace);
        string? layer = entity == null ? null : PeelLayer(face, entity.Infill, entity.Front, entity.SecondFront, entity.Back);
        if (entity == null || byPlayer == null || layer == null)
        {
            base.OnBlockBroken(world, pos, byPlayer, dropQuantityMultiplier);
            return;
        }

        if (world.Side == EnumAppSide.Server && byPlayer.WorldData.CurrentGameMode != EnumGameMode.Creative)
        {
            string? key = LayerKey(layer, entity.Infill, entity.Front, entity.SecondFront, entity.Back);
            var drops = new List<BlockDropItemStack>();
            AddDrops(drops, key, Attributes[layer == "infill" ? "Infills" : "Finishes"]);
            foreach (var stack in ResolveDrops(world, drops, dropQuantityMultiplier)) world.SpawnItemEntity(stack, pos);

            if (Sounds != null)
            {
                var material = LayerMaterialAt(layer, entity.Infill, entity.Front, entity.SecondFront, entity.Back);
                var breakSounds = ResolveLayerSounds(material, layerSounds, Sounds);
                world.PlaySoundAt(breakSounds.GetBreakSound(byPlayer), pos, 0.0, byPlayer);
            }
        }
        SpawnBlockBrokenParticles(pos, byPlayer);

        switch (layer)
        {
            case "front": entity.Front = null; entity.FrontStyle = null; break;
            case "secondfront": entity.SecondFront = null; entity.SecondFrontStyle = null; break;
            case "back": entity.Back = null; entity.BackStyle = null; break;
            default:
                entity.Infill = null;
                OnInfillChanged(world, entity, pos);
                return;
        }
        entity.MarkDirty(true);
    }

    public override ItemStack[] GetDrops(IWorldAccessor world, BlockPos pos, IPlayer byPlayer, float dropQuantityMultiplier)
    {
        var entity = world.BlockAccessor.GetBlockEntity<SidingWallEntity>(pos);
        var drops = ComputeDrops(
            entity?.Framing, entity?.Infill, entity?.Front, entity?.SecondFront, entity?.Back,
            Attributes["Framings"], Attributes["Infills"], Attributes["Finishes"]);

        // Nothing built yet - fall back to the base drops so HorizontalOrientable still
        // hands back the placed block.
        if (drops.Count == 0) return base.GetDrops(world, pos, byPlayer, dropQuantityMultiplier);

        return ResolveDrops(world, drops, dropQuantityMultiplier);
    }

    private ItemStack[] ResolveDrops(IWorldAccessor world, List<BlockDropItemStack> drops, float dropQuantityMultiplier)
    {
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
        string? framing, string? infill, string? front, string? secondFront, string? back,
        JsonObject framings, JsonObject infills, JsonObject finishes)
    {
        var drops = new List<BlockDropItemStack>();
        AddDrops(drops, framing, framings);
        AddDrops(drops, infill, infills);
        AddDrops(drops, front, finishes);
        AddDrops(drops, secondFront, finishes);
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

    // Same layer names PeelLayer returns, resolved to the material key installed there.
    internal static string? LayerKey(string? layer, string? infill, string? front, string? secondFront, string? back)
        => layer switch
        {
            "front" => front,
            "secondfront" => secondFront,
            "back" => back,
            _ => infill,
        };

    // Only "infill" and the finish layers look a material up. Framings are all planks and carry
    // no BlockMaterial, so a frame falls through to the caller's fallback - the block's own Wood.
    internal static EnumBlockMaterial LayerMaterial(string? layer, string? key, JsonObject infills, JsonObject finishes, EnumBlockMaterial fallback)
    {
        if (key == null) return fallback;
        var dictionary = layer == "infill" ? infills : finishes;
        string? materialName = dictionary[key]["BlockMaterial"].AsString(null!);
        return Enum.TryParse(materialName, true, out EnumBlockMaterial material) ? material : fallback;
    }

    // Reverse build order: the hit face's finish, then any finish, then infill; null leaves only the frame.
    internal static string? PeelLayer(string? face, string? infill, string? front, string? secondFront, string? back)
    {
        string? hit = face switch { "front" => front, "secondfront" => secondFront, "back" => back, _ => null };
        if (hit != null) return face;
        if (front != null) return "front";
        if (secondFront != null) return "secondfront";
        if (back != null) return "back";
        return infill != null ? "infill" : null;
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

    // Tool mode 0 is "wall", 1 is "corner" - see decision 0005. Modes 2 and 3 pick a plank
    // finish style (decision 0027) and frame nothing at all, so they resolve to no layout.
    // Anything else falls back to "wall" rather than throwing on a stale/out-of-range mode.
    internal static string? ResolveLayout(int toolMode)
        => ResolveStyle(toolMode) != null ? null : toolMode == 1 ? "cornerout" : "wall";

    // The style modes are appended after the frame modes, never inserted among them: the mode is
    // stored as an index on the item stack, so renumbering wakes saved stacks up in another mode.
    internal static string? ResolveStyle(int toolMode) => toolMode switch
    {
        2 => "weatherboard",
        3 => "boards",
        _ => null,
    };

    // Which finish layer a build-flow click's clicked face targets - the hugged side is
    // "front", the opposite side is "back", an end/top/bottom face is neither. A cornerout's
    // second leg has its own hugged-side layer, "secondfront", but still shares "back" with
    // the first leg.
    internal static string? ResolveFinishFace(string layout, string side, BlockFacing clickedFace)
    {
        if (FinishFaceFor(side, clickedFace, "front") is string face) return face;
        return layout == "cornerout" ? FinishFaceFor(CorneroutSecondFace[side], clickedFace, "secondfront") : null;
    }

    private static string? FinishFaceFor(string side, BlockFacing clickedFace, string frontLayer)
    {
        if (clickedFace.Code == side) return frontLayer;
        if (clickedFace == BlockFacing.FromCode(side).Opposite) return "back";
        return null;
    }
}
