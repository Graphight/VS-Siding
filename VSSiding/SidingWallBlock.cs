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
using Vintagestory.GameContent;

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
    // Any stone is the stone-age signal (decision 0058): never consumed, as the saw never is.
    private static readonly AssetLocation[] BuildSignalCodes = { new("game", "saw-*"), new("game", "stone-*") };

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
        ["diagonal"] = (
            new[]
            {
                new Cuboidf(0, 0, 12f / 16, 4f / 16, 1, 1),
                new Cuboidf(12f / 16, 0, 0, 1, 1, 4f / 16),
            },
            Enumerable.Range(1, 5)
                .Select(k => new Cuboidf(2f * k / 16, 15f / 16, (12f - 2 * k) / 16, (2f * k + 4) / 16, 1, (16f - 2 * k) / 16))
                .ToArray()),
    };

    // Unrotated ("west") deck box per layout, matching the area WallShapeGen clips the floor's layers to.
    private static readonly Dictionary<string, Cuboidf> UnrotatedDeckBoxes = new()
    {
        ["wall"] = new Cuboidf(4f / 16, 12f / 16, 0, 1, 1, 1),
        ["cornerout"] = new Cuboidf(4f / 16, 12f / 16, 4f / 16, 1, 1, 1),
    };

    // Unrotated ("west") step boxes per element, matching WallShapeGen's six step elements.
    private static readonly Dictionary<string, Cuboidf> UnrotatedStepBoxes = new()
    {
        ["step-lower"] = new Cuboidf(4f / 16, 0, 0, 1, 8f / 16, 1),
        ["step-upper"] = new Cuboidf(4f / 16, 8f / 16, 0, 1, 1, 1),
        ["step-lower-north"] = new Cuboidf(4f / 16, 0, 0, 1, 8f / 16, 8f / 16),
        ["step-lower-south"] = new Cuboidf(4f / 16, 0, 8f / 16, 1, 8f / 16, 1),
        ["step-upper-north"] = new Cuboidf(4f / 16, 8f / 16, 0, 1, 1, 8f / 16),
        ["step-upper-south"] = new Cuboidf(4f / 16, 8f / 16, 8f / 16, 1, 1, 1),
    };

    // Built once up front so collision calls from client and server threads only ever read it.
    private static readonly Dictionary<(string layout, string side, bool joinsAbove), Cuboidf[]> FramingBoxes = BuildFramingBoxes();

    private static readonly Dictionary<(string layout, string side), Cuboidf> DeckBoxes = BuildDeckBoxes();

    private static readonly Dictionary<(string side, string element), Cuboidf> StepBoxes = BuildStepBoxes();

    private static Dictionary<(string layout, string side), Cuboidf> BuildDeckBoxes()
    {
        var origin = new Vec3d(0.5, 0.5, 0.5);
        var boxes = new Dictionary<(string layout, string side), Cuboidf>();
        foreach (var (layout, box) in UnrotatedDeckBoxes)
        {
            foreach (string side in CorneroutSecondFace.Keys)
            {
                float rotationYDeg = SidingWallEntity.RotationYDeg(side);
                boxes[(layout, side)] = box.RotatedCopy(0, rotationYDeg, 0, origin);
            }
        }
        return boxes;
    }

    private static Dictionary<(string side, string element), Cuboidf> BuildStepBoxes()
    {
        var origin = new Vec3d(0.5, 0.5, 0.5);
        var boxes = new Dictionary<(string side, string element), Cuboidf>();
        foreach (var (element, box) in UnrotatedStepBoxes)
        {
            foreach (string side in CorneroutSecondFace.Keys)
            {
                float rotationYDeg = SidingWallEntity.RotationYDeg(side);
                boxes[(side, element)] = box.RotatedCopy(0, rotationYDeg, 0, origin);
            }
        }
        return boxes;
    }

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

    // The deck and the step both sit in the open 12/16, outside both the frame's boxes and the
    // panel's. A step only applies to layout "wall"; a cornerout never has one.
    internal static Cuboidf[] AddOpenPartBoxes(Cuboidf[] boxes, string layout, string side, string? deck, string? stepOrientation)
    {
        if (deck != null) boxes = boxes.Append(DeckBoxes[(layout, side)]).ToArray();
        if (layout == "wall" && stepOrientation != null)
        {
            foreach (string element in SidingWallEntity.StepElements(side, stepOrientation))
                boxes = boxes.Append(StepBoxes[(side, element)]).ToArray();
        }
        return boxes;
    }

    // wall.json's collisionSelectionBoxesbytype makes the selection box the panel alone, so without
    // this the deck can be stood on but not aimed at, and a break from below lands on whatever
    // wall lies beyond it.
    public override Cuboidf[] GetSelectionBoxes(IBlockAccessor blockAccessor, BlockPos pos)
    {
        var boxes = base.GetSelectionBoxes(blockAccessor, pos);
        var entity = blockAccessor.GetBlockEntity<SidingWallEntity>(pos);
        return entity == null ? boxes : AddOpenPartBoxes(
            boxes, Variant["layout"], Variant["side"], entity.Deck, entity.Step == null ? null : entity.StepOrientation);
    }

    public override Cuboidf[] GetCollisionBoxes(IBlockAccessor blockAccessor, BlockPos pos)
        => FramedCollisionBoxes(blockAccessor, pos, base.GetCollisionBoxes(blockAccessor, pos));

    public override Cuboidf[] GetParticleCollisionBoxes(IBlockAccessor blockAccessor, BlockPos pos)
        => FramedCollisionBoxes(blockAccessor, pos, base.GetParticleCollisionBoxes(blockAccessor, pos));

    private Cuboidf[] FramedCollisionBoxes(IBlockAccessor blockAccessor, BlockPos pos, Cuboidf[] fullBoxes)
    {
        var entity = blockAccessor.GetBlockEntity<SidingWallEntity>(pos);
        return entity == null ? fullBoxes : PanelCollisionBoxes(blockAccessor, pos, entity, fullBoxes);
    }

    // Framing alone collides on its posts and top plate (decision 0008), otherwise the full panel.
    // GapShiftCollisionPatches passes a guest's entity for a hosted cell (decision 0035).
    internal Cuboidf[] PanelCollisionBoxes(IBlockAccessor blockAccessor, BlockPos pos, SidingWallEntity entity, Cuboidf[] fullBoxes)
    {
        Cuboidf[] boxes;
        if (entity.Framing == null || entity.Infill != null)
        {
            boxes = fullBoxes;
        }
        else
        {
            var joins = NeighbourJoins(blockAccessor, pos, entity.Infill);
            boxes = ComputeCollisionBoxes(Variant["layout"], Variant["side"], entity.Framing, entity.Infill, joins.above, fullBoxes);
        }

        return AddOpenPartBoxes(boxes, Variant["layout"], Variant["side"], entity.Deck, entity.Step == null ? null : entity.StepOrientation);
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
            if (ClaimsTwoFaces(Variant["layout"])) return (above, below, false, false);
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
    {
        var neighbour = SameRunNeighbour(blockAccessor, neighbourPos);
        return neighbour != null && ContinuesGlazing(infill, neighbour, Attributes["Infills"]);
    }

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

    // The block a bare frame becomes when the saw picks another layout (decision 0026): a wall turns
    // into a cornerout or a diagonal at the end clicked, a cornerout into a diagonal on the same
    // side, and nothing turns back. Null when the pick offers no upgrade.
    internal static string? ResolveFramingUpgrade(string layout, string side, string picked, Vec3d hitPosition) => (layout, picked) switch
    {
        ("wall", "cornerout" or "diagonal") => $"wall-{picked}-{ResolveCornerUpgrade(side, hitPosition)}",
        ("cornerout", "diagonal") => $"wall-diagonal-{side}",
        _ => null,
    };

    // A stair beside the wall running along it is copied; otherwise the player picks, as vanilla
    // places stairs: look direction for the along-wall facing, clicked face and hit height for upside-down.
    // A stair with no upside-down variant (noDownVariant, like the stone path) always steps upright.
    internal static string ResolveStepOrientation(string side, string? neighbourOrientation, Vec3f look, BlockFacing clickedFace, double hitY, bool hasDownVariant)
    {
        bool alongWallZ = side is "west" or "east";
        string vertical = clickedFace == BlockFacing.DOWN || (clickedFace.IsHorizontal && hitY > 0.5) ? "down" : "up";
        string horizontal = alongWallZ ? (look.Z < 0 ? "north" : "south") : (look.X < 0 ? "west" : "east");

        if (neighbourOrientation?.Split('-') is [var neighbourVertical, var neighbourFacing]
            && (alongWallZ ? neighbourFacing is "north" or "south" : neighbourFacing is "west" or "east"))
        {
            (vertical, horizontal) = (neighbourVertical, neighbourFacing);
        }

        return $"{(hasDownVariant ? vertical : "up")}-{horizontal}";
    }

    // Same shape, same face: a wall only ever joins another leg of the same run. WallAt, so a
    // hosted cell in the middle of a stack doesn't split it in two (decision 0035).
    private SidingWallEntity? SameRunNeighbour(IBlockAccessor blockAccessor, BlockPos neighbourPos)
    {
        var found = WallAt(blockAccessor, neighbourPos);
        if (found == null) return null;
        var (wall, entity) = found.Value;
        return wall.Variant["layout"] == Variant["layout"] && wall.Variant["side"] == Variant["side"] ? entity : null;
    }

    private bool ContinuesFrame(IBlockAccessor blockAccessor, BlockPos neighbourPos, string? infill)
        => SharesStack(infill, SameRunNeighbour(blockAccessor, neighbourPos));

    // Any framing counts, so mixed woods are one stack, but open and filled cells aren't:
    // a plate marks where a doorway frame meets filled wall (decision 0008).
    internal static bool SharesStack(string? infill, SidingWallEntity? neighbour)
        => neighbour?.Framing != null && (neighbour.Infill == null) == (infill == null);

    // The handbook sentence base appends belongs with the layer list: both show only with the build signal.
    public override string GetPlacedBlockInfo(IWorldAccessor world, BlockPos pos, IPlayer forPlayer)
    {
        string info = base.GetPlacedBlockInfo(world, pos, forPlayer);
        return HasBuildSignal(forPlayer) ? info : info.Replace(Lang.GetMatching($"{Code.Domain}:blockdesc-{Code.Path}"), "").TrimEnd();
    }

    // Shared "are we in build mode" check for both framing (PlaceWallFrame) and layering
    // (below). A plain right-click, not shift - see decision 0006 for why shift was dropped.
    internal static bool HasBuildSignal(IPlayer byPlayer)
        => IsBuildSignal(byPlayer.InventoryManager.OffhandHotbarSlot?.Itemstack?.Collectible.Code);

    internal static bool IsBuildSignal(AssetLocation? offhandCode)
        => offhandCode != null && BuildSignalCodes.Any(signal => WildcardUtil.Match(signal, offhandCode));

    // Any hostable block may take a wall's cell, whichever way it's placed - a click on the panel,
    // on the floor in the gap, a sneak-placement, ground storage. SidingModSystem.HostChangePrefix
    // turns the wall's state into a guest in the same SetBlock, so neighbours never see air.
    public override bool IsReplacableBy(Block block)
        => (SidingModSystem.IsHostableId(block.BlockId) && Variant["layout"] != "diagonal") || base.IsReplacableBy(block);

    // Right-click on the wall's open side with a hostable block and no saw places that block in the
    // wall's own cell, where vanilla would put it in the cell in front. The client only reports the
    // interaction handled - TryPlaceBlock must run once, on the server, or the client would place
    // the held block into the front cell itself before the server ever gets to swap the wall out.
    private bool TryHost(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
    {
        ItemSlot slot = byPlayer.InventoryManager.ActiveHotbarSlot;
        Block? heldBlock = slot.Itemstack?.Block;
        if (heldBlock == null || !SidingModSystem.IsHostableId(heldBlock.BlockId) || Variant["layout"] == "diagonal") return false;
        if (ResolveFinishFace(Variant["layout"], Variant["side"], blockSel.Face) != "back") return false;

        // Furniture would sit where the deck or step is. Swallowed rather than returning false,
        // which hands the click to vanilla and hosts anyway. Other ways in drop it
        // (HostChangePrefix).
        var hostEntity = world.BlockAccessor.GetBlockEntity<SidingWallEntity>(blockSel.Position);
        if (hostEntity != null && hostEntity.OpenPartFilled)
        {
            bool decked = hostEntity.Deck != null;
            (byPlayer as IServerPlayer)?.SendIngameError(
                decked ? "vssiding:decked" : "vssiding:stepped",
                Lang.Get(decked ? "vssiding:build-decked" : "vssiding:build-stepped"));
            return true;
        }

        if (world.Side == EnumAppSide.Client) return true;

        string failureCode = "";
        if (!heldBlock.TryPlaceBlock(world, byPlayer, slot.Itemstack!, blockSel, ref failureCode))
        {
            (byPlayer as IServerPlayer)?.SendIngameError(failureCode, Lang.Get("placefailure-" + failureCode));
            return true;
        }

        bool isCreative = byPlayer.WorldData.CurrentGameMode == EnumGameMode.Creative;
        if (!isCreative) slot.TakeOut(1);
        slot.MarkDirty();
        if (heldBlock.Sounds != null) world.PlaySoundAt(heldBlock.Sounds.Place, blockSel.Position, -0.5, byPlayer);

        return true;
    }

    // A saw in the off hand layers infill onto a framed wall, then finishes onto a filled
    // one - which face was clicked picks Front vs Back. Returns true for every handled
    // branch (including the wrong-face error) so vanilla's "place block against" fallthrough
    // doesn't also fire. Plain right-click, not shift - see decision 0006.
    public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
    {
        if (!HasBuildSignal(byPlayer))
        {
            if (TryHost(world, byPlayer, blockSel)) return true;
            return base.OnBlockInteractStart(world, byPlayer, blockSel);
        }

        ItemSlot slot = byPlayer.InventoryManager.ActiveHotbarSlot;
        AssetLocation? heldCode = slot.Itemstack?.Collectible.Code;
        if (heldCode == null) return base.OnBlockInteractStart(world, byPlayer, blockSel);

        var entity = world.BlockAccessor.GetBlockEntity<SidingWallEntity>(blockSel.Position);

        // With floor picked, planks frame a floor beside the wall (PlaceWallFrame) rather than work on it.
        // A filled deck's top or underside still takes them as floorboards or a ceiling, as a floor's does,
        // and sticks still fill the bare wall or deck they hit (MatchFraming).
        bool deckHit = entity != null && IsDeckHit(world.BlockAccessor, blockSel, entity);
        bool hitFilled = (deckHit ? entity!.DeckInfill : entity?.Infill) != null;
        if (SidingModePicker.Layout(byPlayer) == "floor" && MatchFraming(heldCode, hitFilled, Attributes["Framings"], Attributes["Infills"]) != null
            && !(deckHit && hitFilled && SidingFloorBlock.FinishFace(blockSel.Face) != null))
            return base.OnBlockInteractStart(world, byPlayer, blockSel);

        if (entity == null || entity.Framing == null) return base.OnBlockInteractStart(world, byPlayer, blockSel);

        bool isCreative = byPlayer.WorldData.CurrentGameMode == EnumGameMode.Creative;

        // A stair on a side face fills the open part beside the stair in the room (decision 0046).
        if (slot.Itemstack!.Block is BlockStairs && blockSel.Face != BlockFacing.UP)
        {
            if (Variant["layout"] != "wall")
            {
                (byPlayer as IServerPlayer)?.SendIngameError("vssiding:stepcorner", Lang.Get("vssiding:build-step-corner"));
                return true;
            }

            if (entity.Deck != null)
            {
                (byPlayer as IServerPlayer)?.SendIngameError("vssiding:decked", Lang.Get("vssiding:build-decked"));
                return true;
            }

            if (entity.Step != null)
            {
                (byPlayer as IServerPlayer)?.SendIngameError("vssiding:alreadystepped", Lang.Get("vssiding:build-already-stepped"));
                return true;
            }

            BlockPos neighbourPos = blockSel.Position.AddCopy(BlockFacing.FromCode(Variant["side"]).Opposite);
            // A stair with no vertical group, like vanilla's stone path, only comes upright.
            string? neighbourOrientation = world.BlockAccessor.GetBlock(neighbourPos) is BlockStairs neighbourBlock
                && neighbourBlock.Variant["horizontalorientation"] is { } neighbourFacing
                ? $"{neighbourBlock.Variant["verticalorientation"] ?? "up"}-{neighbourFacing}"
                : null;

            string orientation = ResolveStepOrientation(
                Variant["side"], neighbourOrientation, byPlayer.Entity.SidedPos.GetViewVector(), blockSel.Face, blockSel.HitPosition.Y,
                slot.Itemstack.Block.Attributes?.IsTrue("noDownVariant") != true);

            if (StepOccupied(world, blockSel.Position, orientation))
            {
                (byPlayer as IServerPlayer)?.SendIngameError("vssiding:occupied", Lang.Get("vssiding:build-occupied"));
                return true;
            }

            entity.Step = heldCode.ToString();
            entity.StepOrientation = orientation;
            entity.MarkDirty(true);
            if (!isCreative) slot.TakeOut(1);
            slot.MarkDirty();
            return true;
        }

        if (IsDeckHit(world.BlockAccessor, blockSel, entity))
            return LayerDeck(world, byPlayer, blockSel, entity, slot, heldCode, isCreative);

        // With the deck lit, planks on a side face add a deck in place. Ahead of finishing, since
        // planks are a finish too. The top face still stacks the next course (PlaceWallFrame).
        if (entity.Deck == null && blockSel.Face != BlockFacing.UP && SidingModePicker.Deck(byPlayer)
            && MatchFraming(heldCode, entity.Infill != null, Attributes["Framings"], Attributes["Infills"]) is { } deckKey)
        {
            if (Variant["layout"] == "diagonal")
            {
                (byPlayer as IServerPlayer)?.SendIngameError("vssiding:diagonaldeck", Lang.Get("vssiding:build-diagonal-deck"));
                return true;
            }

            if (entity.Step != null)
            {
                (byPlayer as IServerPlayer)?.SendIngameError("vssiding:stepped", Lang.Get("vssiding:build-stepped"));
                return true;
            }

            if (DeckOccupied(world, blockSel.Position))
            {
                (byPlayer as IServerPlayer)?.SendIngameError("vssiding:occupied", Lang.Get("vssiding:build-occupied"));
                return true;
            }

            var deckConsumes = Attributes["Framings"][deckKey]["Consumes"];
            if (!TryAffordOrError(byPlayer, isCreative, slot.StackSize, deckConsumes)) return true;

            SetDeck(world, entity, blockSel.Position, deckKey);
            ConsumeHeld(slot, deckConsumes, isCreative);
            return true;
        }

        if (entity.Infill == null)
        {
            // A bare frame clicked in corner or diagonal mode becomes one in place, for a T-junction
            // found once a partition reaches it (decision 0026). Nothing is charged: a fresh
            // cornerout or diagonal frame costs the same as a fresh wall frame. Everything this
            // doesn't claim falls through to the infill match below, then to PlaceWallFrame.
            string layout = Variant["layout"];
            string? upgrade = ResolveFramingUpgrade(layout, Variant["side"], SidingModePicker.Layout(byPlayer), blockSel.HitPosition);
            if (upgrade != null
                && MatchFraming(heldCode, false, Attributes["Framings"], Attributes["Infills"]) != null
                && ResolveFinishFace(layout, Variant["side"], blockSel.Face) != null)
            {
                if (entity.Step != null)
                {
                    (byPlayer as IServerPlayer)?.SendIngameError("vssiding:stepped", Lang.Get("vssiding:build-stepped"));
                    return true;
                }

                var corner = world.GetBlock(new AssetLocation("vssiding", upgrade));
                if (corner != null)
                {
                    // Keeps the block entity, and the engine repoints its Block at the new
                    // type, so Framing survives and OnTesselation reads the new layout.
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

            string? oldInfill = entity.Infill;
            entity.Infill = infillKey;
            OnInfillChanged(world, entity, blockSel.Position, oldInfill);
            ConsumeHeld(slot, consumes, isCreative);
            return true;
        }

        string? finishKey = MatchConsumes(heldCode, Attributes["Finishes"]);
        if (finishKey == null) return base.OnBlockInteractStart(world, byPlayer, blockSel);

        // The picker's chosen style is used only if this finish lists it; otherwise the entry's
        // default applies.
        string? style = SidingModePicker.FinishChoices(byPlayer).FirstOrDefault(s => HasStyle(Attributes["Finishes"][finishKey], s));

        // Planks that can't finish this face still extend the wall via PlaceWallFrame, and held blocks still place.
        bool heldPlaces = slot.Itemstack!.Class == EnumItemClass.Block || MatchConsumes(heldCode, Attributes["Framings"]) != null;

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

    // The deck box is appended after the block's own selection boxes (AddOpenPartBoxes).
    internal bool IsDeckHit(IBlockAccessor blockAccessor, BlockSelection blockSel, SidingWallEntity entity)
        => entity.Deck != null && blockSel.SelectionBoxIndex == base.GetSelectionBoxes(blockAccessor, blockSel.Position).Length;

    // The deck takes layers the way a floor does (SidingFloorBlock.OnBlockInteractStart): infill on any
    // face, then a finish on the top or the underside. Anything unclaimed falls through, so planks on a
    // bare deck's top still reach PlaceWallFrame.
    private bool LayerDeck(
        IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel, SidingWallEntity entity,
        ItemSlot slot, AssetLocation heldCode, bool isCreative)
    {
        if (entity.DeckInfill == null)
        {
            string? infillKey = MatchConsumes(heldCode, Attributes["Infills"]);
            if (infillKey == null) return base.OnBlockInteractStart(world, byPlayer, blockSel);

            var consumes = Attributes["Infills"][infillKey]["Consumes"];
            if (!TryAffordOrError(byPlayer, isCreative, slot.StackSize, consumes)) return true;

            entity.DeckInfill = infillKey;
            entity.LegacyDeck = false;
            entity.MarkDirty(true);
            // Rooms only recompute on a chunk-dirty event, and the deck's retention just changed.
            world.BlockAccessor.ExchangeBlock(Id, blockSel.Position);
            SidingFloorBlock.MarkNeighboursDirty(world, blockSel.Position);
            ConsumeHeld(slot, consumes, isCreative);
            return true;
        }

        string? finishKey = MatchConsumes(heldCode, Attributes["Finishes"]);
        if (finishKey == null) return base.OnBlockInteractStart(world, byPlayer, blockSel);

        bool heldPlaces = slot.Itemstack!.Class == EnumItemClass.Block || MatchConsumes(heldCode, Attributes["Framings"]) != null;
        if (IsTransparent(entity.DeckInfill, Attributes["Infills"]))
        {
            if (heldPlaces) return base.OnBlockInteractStart(world, byPlayer, blockSel);
            (byPlayer as IServerPlayer)?.SendIngameError("vssiding:glazed", Lang.Get("vssiding:build-glazed"));
            return true;
        }

        string? face = SidingFloorBlock.FinishFace(blockSel.Face);
        if (face == null)
        {
            if (heldPlaces) return base.OnBlockInteractStart(world, byPlayer, blockSel);
            (byPlayer as IServerPlayer)?.SendIngameError("vssiding:wrongface", Lang.Get("vssiding:build-floor-wrong-face"));
            return true;
        }

        string? style = SidingModePicker.FinishChoices(byPlayer).FirstOrDefault(s => SidingFloorEntity.HasFloorStyle(Attributes["Finishes"][finishKey], face, s));

        string? currentKey = face == "front" ? entity.DeckFront : entity.DeckBack;
        string? currentStyle = face == "front" ? entity.DeckFrontStyle : entity.DeckBackStyle;
        if (currentKey != null)
        {
            if (style != null && currentKey == finishKey && currentStyle != style)
            {
                SetDeckFinish(entity, face, finishKey, style);
                return true;
            }

            if (heldPlaces) return base.OnBlockInteractStart(world, byPlayer, blockSel);
            (byPlayer as IServerPlayer)?.SendIngameError("vssiding:alreadyfinished", Lang.Get("vssiding:build-already-finished"));
            return true;
        }

        var finishConsumes = Attributes["Finishes"][finishKey]["Consumes"];
        if (!TryAffordOrError(byPlayer, isCreative, slot.StackSize, finishConsumes)) return true;

        SetDeckFinish(entity, face, finishKey, style);
        ConsumeHeld(slot, finishConsumes, isCreative);
        return true;
    }

    private static void SetDeckFinish(SidingWallEntity entity, string face, string finishKey, string? style)
    {
        if (face == "front") { entity.DeckFront = finishKey; entity.DeckFrontStyle = style; }
        else { entity.DeckBack = finishKey; entity.DeckBackStyle = style; }
        entity.MarkDirty(true);
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

    // Only a finish that lists a style can be asked for it, so a picked board style on daub or
    // brick falls back to the entry's default rather than naming an element its shape hasn't got.
    internal static bool HasStyle(JsonObject finish, string style)
        => Array.IndexOf(finish["Styles"].AsArray<string>([]) ?? [], style) >= 0;

    internal void OnInfillChanged(IWorldAccessor world, SidingWallEntity entity, BlockPos pos, string? oldInfill)
    {
        entity.MarkDirty(true);
        MarkAbsorptionChanged(world.BlockAccessor, pos, entity.Framing, oldInfill);
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
    internal static bool TryAffordOrError(IPlayer byPlayer, bool isCreative, int stackSize, JsonObject consumes, int times = 1)
    {
        if (CanAfford(isCreative, stackSize, consumes, times)) return true;
        (byPlayer as IServerPlayer)?.SendIngameError("vssiding:cantafford", Lang.Get("vssiding:build-cant-afford"));
        return false;
    }

    internal static void ConsumeHeld(ItemSlot slot, JsonObject consumes, bool isCreative, int times = 1)
    {
        if (isCreative) return;
        slot.TakeOut(ConsumeQuantity(consumes) * times);
        slot.MarkDirty();
    }

    // A held stack too small to pay Consumes.quantity must not place/build - ItemSlot.TakeOut
    // silently takes whatever is available rather than failing, so the caller has to check first.
    // Creative players aren't charged at all.
    internal static bool CanAfford(bool isCreative, int stackSize, JsonObject consumes, int times = 1)
        => isCreative || stackSize >= ConsumeQuantity(consumes) * times;

    // Which plates a cell draws depends on the cells above and below it (decision 0008).
    public override void OnNeighbourBlockChange(IWorldAccessor world, BlockPos pos, BlockPos neibpos)
    {
        base.OnNeighbourBlockChange(world, pos, neibpos);
        if (neibpos.X != pos.X || neibpos.Z != pos.Z)
        {
            // Only glazing merges sideways, and only along its own run, so those are the only
            // horizontal neighbours that can change what this cell draws - an opaque wall never
            // joins one. Just this cell: merging is local, nothing propagates past the neighbour.
            if (neibpos.Y != pos.Y || ClaimsTwoFaces(Variant["layout"])) return;
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
        if (world.BlockAccessor.GetBlock(pos) is not SidingWallBlock block || ClaimsTwoFaces(block.Variant["layout"])) return;
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

    // A cornerout and a diagonal both claim the hugged side and the face counter-clockwise from it.
    internal static bool ClaimsTwoFaces(string layout) => layout is "cornerout" or "diagonal";

    // The faces this block's panels actually cover: the hugged side, plus a cornerout's or diagonal's second face.
    internal bool ClaimsFace(BlockFacing facing) => ClaimsFace(Variant["layout"], Variant["side"], facing.Code);

    internal static bool ClaimsFace(string layout, string side, string faceCode)
        => faceCode == side || (ClaimsTwoFaces(layout) && faceCode == CorneroutSecondFace[side]);

    public override int GetRetention(BlockPos pos, BlockFacing facing, EnumRetentionType type)
    {
        var entity = api.World.BlockAccessor.GetBlockEntity<SidingWallEntity>(pos);
        if (facing == BlockFacing.UP) return ComputeDeckRetention(entity?.Deck, entity?.DeckInfill, entity?.LegacyDeck ?? false, Attributes["Framings"], Attributes["Infills"]);
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
        if (blockFace == BlockFacing.UP) return ComputeDeckRetention(entity?.Deck, entity?.DeckInfill, entity?.LegacyDeck ?? false, Attributes["Framings"], Attributes["Infills"]) != 0;
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

    // The UP face seals like a floor's top, RoomRegistry.FindRoomForPosition asking every face: framing
    // and infill both, so a clay or stone deck cools. A deck saved before decks took layers sealed as
    // bare framing, and keeps doing so until infill is laid.
    internal static int ComputeDeckRetention(string? deckKey, string? deckInfill, bool legacyDeck, JsonObject framings, JsonObject infills)
        => legacyDeck && deckInfill == null
            ? (deckKey != null && framings[deckKey].Exists ? 1 : 0)
            : ComputeRetention(true, deckKey, deckInfill, framings, infills);

    // The deck changes the UP face's retention, and rooms only recompute on a chunk-dirty event,
    // so the block is exchanged for itself as OnInfillChanged does.
    internal bool DeckOccupied(IWorldAccessor world, BlockPos pos)
        => world.GetIntersectingEntities(pos, new[] { DeckBoxes[(Variant["layout"], Variant["side"])] }, e => e.IsInteractable) is { Length: > 0 };

    internal bool StepOccupied(IWorldAccessor world, BlockPos pos, string orientation)
    {
        var boxes = SidingWallEntity.StepElements(Variant["side"], orientation)
            .Select(element => StepBoxes[(Variant["side"], element)]).ToArray();
        return world.GetIntersectingEntities(pos, boxes, e => e.IsInteractable) is { Length: > 0 };
    }

    private void SetDeck(IWorldAccessor world, SidingWallEntity entity, BlockPos pos, string? deckKey)
    {
        entity.Deck = deckKey;
        entity.LegacyDeck = false;
        entity.MarkDirty(true);
        SidingFloorBlock.MarkNeighboursDirty(world, pos);
        world.BlockAccessor.ExchangeBlock(Id, pos);
    }

    // Looking at a built wall names its layers - otherwise a boarded infill is unreadable
    // short of breaking it, and the infill is what decides cellar vs warm room (decision 0015).
    // Takes a translate delegate (the entity passes Lang.GetIfExists) so this needs no loaded Lang.
    // System.Func is spelled out throughout: Vintagestory.API.Common declares a Func of its own.
    internal static string Describe(
        string? framing, string? infill, string? deck, JsonObject framings, JsonObject infills,
        string layout, string side, string? front, string? secondFront, string? back, JsonObject finishes,
        System.Func<string, string?> translate, string? stepName = null,
        string? deckInfill = null, string? deckFront = null, string? deckBack = null, bool full = true)
    {
        string? builtFraming = Installed(framing, framings);
        string? builtInfill = Installed(infill, infills);
        string? builtDeck = Installed(deck, framings);

        if (!full)
            return Translate(SealKey(builtFraming, builtInfill, framings, infills), translate) + "\n";

        var sb = new StringBuilder();
        sb.AppendLine();
        sb.AppendLine("  " + DescribeLayer(builtFraming, framings, "vssiding:tooltip-no-framing", translate));
        sb.AppendLine("  " + DescribeLayer(builtInfill, infills, "vssiding:tooltip-no-infill", translate));
        // Opt-in, so no line at all without one: "No deck" would be on nearly every wall.
        if (builtDeck != null)
        {
            sb.AppendLine("  " + string.Format(Translate("vssiding:tooltip-deck", translate), DescribeLayer(builtDeck, framings, "", translate)));
            sb.AppendLine("    " + DescribeLayer(Installed(deckInfill, infills), infills, "vssiding:tooltip-no-infill", translate));
            sb.AppendLine("    " + string.Format(Translate("vssiding:tooltip-top", translate),
                DescribeLayer(Installed(deckFront, finishes), finishes, "vssiding:tooltip-unfinished", translate)));
            sb.AppendLine("    " + string.Format(Translate("vssiding:tooltip-underside", translate),
                DescribeLayer(Installed(deckBack, finishes), finishes, "vssiding:tooltip-unfinished", translate)));
        }
        if (stepName != null)
            sb.AppendLine("  " + string.Format(Translate("vssiding:tooltip-step", translate), stepName));
        foreach (string line in DescribeFaces(layout, side, front, secondFront, back, finishes, translate))
            sb.AppendLine("  " + line);
        sb.AppendLine("  " + Translate(SealKey(builtFraming, builtInfill, framings, infills), translate));
        return sb.ToString();
    }

    // The line a cellar builder actually reads: whether the wall seals the room at all, and
    // if so, whether the infill makes it a cooling wall (decision 0015).
    internal static string SealKey(string? framing, string? infill, JsonObject framings, JsonObject infills)
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
        if (ClaimsTwoFaces(layout))
        {
            string secondSide = CorneroutSecondFace[side];
            directions.Add((secondSide, Installed(layout == "diagonal" ? front : secondFront, finishes)));
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
    internal static string? Installed(string? key, JsonObject materials) => key != null && materials[key].Exists ? key : null;

    // A gap prints the "no-x" line; a built layer resolves its DisplayName, falling back to a
    // title-cased material key when the entry has no DisplayName or the key has no translation.
    internal static string DescribeLayer(string? key, JsonObject materials, string missingLangKey, System.Func<string, string?> translate)
    {
        if (key == null) return Translate(missingLangKey, translate);

        string? displayName = materials[key]["DisplayName"].AsString(null!);
        return (displayName != null ? translate(displayName) : null) ?? TitleCase(key);
    }

    // Our own keys ship in en.json beside the code, so a miss means a broken install: show the
    // raw key rather than dressing it up. A material's DisplayName is the case worth dressing up,
    // since a game update can add a wood the lang file has never heard of.
    internal static string Translate(string langKey, System.Func<string, string?> translate) => translate(langKey) ?? langKey;

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

    // Emitting side AO keeps a sealed cell's light out of neighbouring faces' smooth-lighting corners.
    // That light is the open side's (decision 0018), which is daylight when the panels face in.
    public override bool DoEmitSideAo(IGeometryTester caller, BlockFacing facing)
        => IsSealed(caller.GetCurrentBlockEntityOnSide(facing.Opposite)) || base.DoEmitSideAo(caller, facing);

    public override bool DoEmitSideAoByFlag(IGeometryTester caller, Vec3iAndFacingFlags vec, int flags)
        => IsSealed(caller.GetCurrentBlockEntityOnSide(vec)) || base.DoEmitSideAoByFlag(caller, vec, flags);

    // The relight skips when old and new absorption match, so opening a wall needs the old infill's real value (decision 0034).
    internal void MarkAbsorptionChanged(IBlockAccessor accessor, BlockPos pos, string? framing, string? oldInfill)
        => accessor.MarkAbsorptionChanged(
            ComputeLightAbsorption(framing, oldInfill, Attributes["Framings"], Attributes["Infills"]),
            GetLightAbsorption(accessor, pos), pos);

    internal bool IsSealed(BlockEntity? be)
        => be is SidingWallEntity entity && ComputeLightAbsorption(entity.Framing, entity.Infill, Attributes["Framings"], Attributes["Infills"]) > 0;

    // The horizontal step from a wall cell to the cell its dead space opens onto: away from the panel, diagonally for a cornerout.
    internal static (int dx, int dz) OpenSide(string layout, string side)
    {
        var open = BlockFacing.FromCode(side).Opposite.Normali;
        if (!ClaimsTwoFaces(layout)) return (open.X, open.Z);
        var second = BlockFacing.FromCode(CorneroutSecondFace[side]).Opposite.Normali;
        return (open.X + second.X, open.Z + second.Z);
    }

    // The real wall at a cell, or the guest wall of a hosted block there (decision 0035).
    internal static (SidingWallBlock wall, SidingWallEntity entity)? WallAt(IBlockAccessor accessor, BlockPos pos)
    {
        Block block = accessor.GetBlock(pos);
        if (block is SidingWallBlock wall)
        {
            var entity = accessor.GetBlockEntity<SidingWallEntity>(pos);
            return entity == null ? null : (wall, entity);
        }

        if (!SidingModSystem.IsHostableId(block.BlockId)) return null;

        ICoreAPI? api = SidingModSystem.ApiRef(block);
        if (api == null) return null;

        SidingWallEntity? guest = GuestWalls.GuestAt(api, pos);
        return guest?.Block is SidingWallBlock guestWall ? (guestWall, guest) : null;
    }

    // A sealed wall's cell stores the sunlight flowing in from outside, which RoomRegistry would count as sky (decision 0015).
    internal static int RoomSunlight(IBlockAccessor accessor, BlockPos pos, EnumLightLevelType type)
    {
        var found = WallAt(accessor, pos);
        if (found == null) return accessor.GetLightLevel(pos, type);
        var (wall, entity) = found.Value;
        int absorption = ComputeLightAbsorption(entity.Framing, entity.Infill, wall.Attributes["Framings"], wall.Attributes["Infills"]);
        return absorption > 0 ? 0 : accessor.GetLightLevel(pos, type);
    }

    // Keyed by EnumBlockMaterial name (LayerSounds in wall.json), parsed once here rather than
    // AsObject<BlockSounds>() per hit - that's a full Newtonsoft parse and GetSounds runs every tick.
    private Dictionary<EnumBlockMaterial, BlockSounds>? layerSounds;

    // Keyed by EnumBlockMaterial name (LayerResistance in wall.json); multiplies Resistance.
    private Dictionary<EnumBlockMaterial, float>? layerResistance;

    public override void OnLoaded(ICoreAPI api)
    {
        base.OnLoaded(api);
        layerSounds = LayerTable(Attributes["LayerSounds"], entry => entry.AsObject(new BlockSounds()));
        layerResistance = LayerTable(Attributes["LayerResistance"], entry => entry.AsFloat(1f));
    }

    // LayerSounds and LayerResistance are keyed by EnumBlockMaterial name; unknown names are skipped.
    internal static Dictionary<EnumBlockMaterial, T> LayerTable<T>(JsonObject entries, System.Func<JsonObject, T> read)
    {
        var table = new Dictionary<EnumBlockMaterial, T>();
        if (!entries.Exists) return table;
        foreach (var keyToken in entries)
        {
            string key = keyToken.AsString()!;
            if (Enum.TryParse(key, true, out EnumBlockMaterial material)) table[material] = read(entries[key]);
        }
        return table;
    }

    // Which EnumBlockMaterial is under the cursor: resolves the clicked face to a finish layer
    // (or the infill, or the frame) the same way OnBlockBroken decides what to peel.
    internal EnumBlockMaterial HitLayerMaterial(IBlockAccessor accessor, BlockPos pos, BlockFacing? hitFace, BlockSelection? blockSel = null)
    {
        var entity = accessor.GetBlockEntity<SidingWallEntity>(pos);
        if (entity == null) return BlockMaterial;

        bool deckHit = blockSel != null && IsDeckHit(accessor, blockSel, entity);
        return LayerMaterialAt(PeelAt(entity, hitFace, deckHit), entity);
    }

    // The layer a hit peels. A hit on the deck's own box always peels the deck, in a floor's order
    // (DeckPeelLayer); any other hit takes the wall's order, and a deck it lands on is resolved the same way, faceless.
    internal string? PeelAt(SidingWallEntity entity, BlockFacing? hitFace, bool deckHit)
    {
        if (deckHit)
            return DeckPeelLayer(hitFace == null ? null : SidingFloorBlock.FinishFace(hitFace), entity.DeckInfill, entity.DeckFront, entity.DeckBack);

        string? face = hitFace == null ? null : ResolveFinishFace(Variant["layout"], Variant["side"], hitFace);
        string? layer = PeelLayer(face, entity.Infill, entity.Front, entity.SecondFront, entity.Back, entity.Deck, entity.Step);
        return layer == "deck" ? DeckPeelLayer(null, entity.DeckInfill, entity.DeckFront, entity.DeckBack) : layer;
    }

    private static string? KeyAt(string? layer, SidingWallEntity entity)
        => LayerKey(layer, entity.Infill, entity.Front, entity.SecondFront, entity.Back, entity.Deck, entity.Step, entity.DeckInfill, entity.DeckFront, entity.DeckBack);

    private EnumBlockMaterial LayerMaterialAt(string? layer, SidingWallEntity entity)
    {
        if (layer == "step" && entity.Step != null)
            return api?.World.GetBlock(new AssetLocation(entity.Step))?.BlockMaterial ?? BlockMaterial;
        return LayerMaterial(layer, KeyAt(layer, entity), entity.Framing, Attributes["Framings"], Attributes["Infills"], Attributes["Finishes"], BlockMaterial);
    }

    // The fallback is a parameter rather than read from Sounds here, so GetSounds can defer to
    // base.GetSounds while OnBlockBroken defers to Sounds.
    internal static BlockSounds ResolveLayerSounds(EnumBlockMaterial material, Dictionary<EnumBlockMaterial, BlockSounds>? layerSounds, BlockSounds fallback)
        => layerSounds != null && layerSounds.TryGetValue(material, out var sounds) ? sounds : fallback;

    public override BlockSounds GetSounds(IBlockAccessor blockAccessor, BlockSelection blockSel, ItemStack? stack = null)
    {
        // Vanilla's own GetSounds ignores its arguments, so a caller is free to pass no selection.
        if (blockSel?.Position == null) return base.GetSounds(blockAccessor, blockSel, stack);
        // A hit on a rug breaks the rug first (OnBlockBroken), so it sounds like one, as on a floor.
        if (blockSel.Face != null && blockAccessor.GetDecor(blockSel.Position, new DecorBits(blockSel.Face)) != null)
            return base.GetSounds(blockAccessor, blockSel, stack);

        var material = HitLayerMaterial(blockAccessor, blockSel.Position, blockSel.Face, blockSel);
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

    // Only a Wood topmost layer burns; any other material answers null so fire, lava and lightning skip it.
    internal static CombustibleProperties? ResolveLayerCombustible(EnumBlockMaterial material, CombustibleProperties? props)
        => material == EnumBlockMaterial.Wood ? props : null;

    // pos is null for an item in a slot, which keeps the block's own props.
    public override CombustibleProperties? GetCombustibleProperties(IWorldAccessor world, ItemStack? itemstack, BlockPos? pos)
    {
        if (pos == null) return base.GetCombustibleProperties(world, itemstack, pos);

        var entity = world.BlockAccessor.GetBlockEntity<SidingWallEntity>(pos);
        var material = entity == null ? BlockMaterial : LayerMaterialAt(BurnLayer(entity), entity);
        return ResolveLayerCombustible(material, base.GetCombustibleProperties(world, itemstack, pos));
    }

    // The layer a fire takes: the topmost, except that a deck whose own top layer does not burn is
    // stepped over for the wall's layers under it, so a bone deck does not fireproof a pelt (decision 0060).
    // A wall with no layer of its own keeps the deck's answer, so its frame does not burn out from under the deck.
    internal string? BurnLayer(SidingWallEntity entity)
    {
        string? layer = PeelAt(entity, null, false);
        if (layer?.StartsWith("deck") != true || LayerMaterialAt(layer, entity) == EnumBlockMaterial.Wood) return layer;
        return PeelLayer(null, entity.Infill, entity.Front, entity.SecondFront, entity.Back, null, entity.Step) ?? layer;
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
        bool deckHit = entity != null && hitFace != null && IsDeckHit(world.BlockAccessor, selection!, entity);

        // A rug on the deck comes off before any layer, as on a floor (decision 0051): vanilla only
        // breaks decor first in survival.
        if (deckHit && world.BlockAccessor.GetDecor(pos, new DecorBits(hitFace!)) != null)
        {
            world.BlockAccessor.BreakDecor(pos, hitFace);
            return;
        }

        string? layer = entity == null ? null : PeelAt(entity, hitFace, deckHit);
        if (entity == null || byPlayer == null || layer == null)
        {
            base.OnBlockBroken(world, pos, byPlayer, dropQuantityMultiplier);
            return;
        }

        if (world.Side == EnumAppSide.Server && byPlayer.WorldData.CurrentGameMode != EnumGameMode.Creative)
        {
            string? key = KeyAt(layer, entity);
            var drops = new List<BlockDropItemStack>();
            if (layer == "step")
            {
                var stepDrop = StepDrop(key);
                if (stepDrop != null) drops.Add(stepDrop);
            }
            else
            {
                AddDrops(drops, key, Attributes[layer switch { "infill" or "deckinfill" => "Infills", "deck" => "Framings", _ => "Finishes" }]);
            }
            foreach (var stack in ResolveDrops(world, drops, dropQuantityMultiplier)) world.SpawnItemEntity(stack, pos);

            if (Sounds != null)
            {
                var material = LayerMaterialAt(layer, entity);
                var breakSounds = ResolveLayerSounds(material, layerSounds, Sounds);
                world.PlaySoundAt(breakSounds.GetBreakSound(byPlayer), pos, 0.0, byPlayer);
            }
        }
        SpawnBlockBrokenParticles(pos, byPlayer);
        RemoveLayer(world, entity, pos, layer);
    }

    // A fire that burns out against a wall takes its topmost layer, not the whole block; a bare
    // wood frame has none left, so vanilla deletes it. Vanilla only rechecks fuel once a second, so a
    // non-wood layer added in that window, or a bare frame that is not wood, is left standing and the
    // fire just goes out.
    internal bool TryBurnLayer(IWorldAccessor world, BlockPos pos)
    {
        var entity = world.BlockAccessor.GetBlockEntity<SidingWallEntity>(pos);
        if (entity == null) return false;
        string? layer = BurnLayer(entity);
        bool burns = LayerMaterialAt(layer, entity) == EnumBlockMaterial.Wood;
        if (layer == null) return !burns;

        if (burns && world.Side == EnumAppSide.Server) RemoveLayer(world, entity, pos, layer);
        return true;
    }

    internal void RemoveLayer(IWorldAccessor world, SidingWallEntity entity, BlockPos pos, string layer)
    {
        switch (layer)
        {
            case "front": entity.Front = null; entity.FrontStyle = null; break;
            case "secondfront": entity.SecondFront = null; entity.SecondFrontStyle = null; break;
            case "back": entity.Back = null; entity.BackStyle = null; break;
            case "deckfront": entity.DeckFront = null; entity.DeckFrontStyle = null; break;
            case "deckback": entity.DeckBack = null; entity.DeckBackStyle = null; break;
            case "deckinfill":
                entity.DeckInfill = null;
                entity.MarkDirty(true);
                // Rooms only recompute on a chunk-dirty event, and the deck's retention just changed.
                world.BlockAccessor.ExchangeBlock(Id, pos);
                SidingFloorBlock.MarkNeighboursDirty(world, pos);
                // Decor only goes on a sealed top, so a rug left on the bare joists comes off with the infill.
                world.BlockAccessor.BreakDecor(pos, BlockFacing.UP);
                return;
            case "deck": SetDeck(world, entity, pos, null); return;
            case "step": entity.Step = null; entity.StepOrientation = null; entity.MarkDirty(true); return;
            default:
                string? oldInfill = entity.Infill;
                entity.Infill = null;
                OnInfillChanged(world, entity, pos, oldInfill);
                return;
        }
        entity.MarkDirty(true);
    }

    public override ItemStack[] GetDrops(IWorldAccessor world, BlockPos pos, IPlayer byPlayer, float dropQuantityMultiplier)
    {
        var entity = world.BlockAccessor.GetBlockEntity<SidingWallEntity>(pos);
        var drops = ComputeDrops(
            entity?.Framing, entity?.Infill, entity?.Front, entity?.SecondFront, entity?.Back, entity?.Deck,
            Attributes["Framings"], Attributes["Infills"], Attributes["Finishes"], entity?.Step,
            entity?.DeckInfill, entity?.DeckFront, entity?.DeckBack);

        // Nothing built yet - fall back to the base drops so HorizontalOrientable still
        // hands back the placed block.
        if (drops.Count == 0) return base.GetDrops(world, pos, byPlayer, dropQuantityMultiplier);

        return ResolveDrops(world, drops, dropQuantityMultiplier);
    }

    internal ItemStack[] ResolveDrops(IWorldAccessor world, List<BlockDropItemStack> drops, float dropQuantityMultiplier)
        => ResolveDrops(world, drops, dropQuantityMultiplier, Code);

    internal static ItemStack[] ResolveDrops(IWorldAccessor world, List<BlockDropItemStack> drops, float dropQuantityMultiplier, AssetLocation code)
    {
        var stacks = new List<ItemStack>();
        foreach (var drop in drops)
        {
            drop.Resolve(world, "vssiding:wall drops", code);
            var stack = drop.GetNextItemStack(dropQuantityMultiplier);
            if (stack != null) stacks.Add(stack);
        }
        return stacks.ToArray();
    }

    internal static List<BlockDropItemStack> ComputeDrops(
        string? framing, string? infill, string? front, string? secondFront, string? back, string? deck,
        JsonObject framings, JsonObject infills, JsonObject finishes, string? step = null,
        string? deckInfill = null, string? deckFront = null, string? deckBack = null)
    {
        var drops = new List<BlockDropItemStack>();
        AddDrops(drops, framing, framings);
        AddDrops(drops, infill, infills);
        AddDrops(drops, front, finishes);
        AddDrops(drops, secondFront, finishes);
        AddDrops(drops, back, finishes);
        AddDrops(drops, deck, framings);
        AddDrops(drops, deckInfill, infills);
        AddDrops(drops, deckFront, finishes);
        AddDrops(drops, deckBack, finishes);
        var stepDrop = StepDrop(step);
        if (stepDrop != null) drops.Add(stepDrop);
        return drops;
    }

    internal static void AddDrops(List<BlockDropItemStack> drops, string? key, JsonObject dictionary)
    {
        if (key == null) return;
        var entry = dictionary[key];
        if (!entry.Exists) return;

        foreach (var drop in entry["Drops"].AsObject(Array.Empty<BlockDropItemStack>()))
        {
            if (drop.Code != null) drops.Add(drop);
        }
    }

    private static BlockDropItemStack? StepDrop(string? step)
        => step == null ? null : new BlockDropItemStack { Type = EnumItemClass.Block, Code = new AssetLocation(step), Quantity = NatFloat.One };

    // Same layer names PeelLayer returns, resolved to the material key installed there.
    internal static string? LayerKey(string? layer, string? infill, string? front, string? secondFront, string? back, string? deck, string? step = null,
        string? deckInfill = null, string? deckFront = null, string? deckBack = null)
        => layer switch
        {
            "front" => front,
            "secondfront" => secondFront,
            "back" => back,
            "deck" => deck,
            "deckinfill" => deckInfill,
            "deckfront" => deckFront,
            "deckback" => deckBack,
            "step" => step,
            _ => infill,
        };

    // Infills and finishes look their material up in their own dictionary; a bare frame looks up the
    // framing and a deck its framing key, and an entry with no BlockMaterial (planks) falls through
    // to the caller's fallback, the block's own Wood.
    internal static EnumBlockMaterial LayerMaterial(string? layer, string? key, string? framing, JsonObject framings, JsonObject infills, JsonObject finishes, EnumBlockMaterial fallback)
    {
        if (layer == null) key = framing;
        if (key == null) return fallback;
        var dictionary = layer is null or "deck" ? framings : layer is "infill" or "deckinfill" ? infills : finishes;
        string? materialName = dictionary[key]["BlockMaterial"].AsString(null!);
        return Enum.TryParse(materialName, true, out EnumBlockMaterial material) ? material : fallback;
    }

    // Reverse build order: the hit face's finish, then any finish, then infill; null leaves only
    // the frame. The deck or step is outermost on the room side, so a back or end hit takes it first,
    // and otherwise it goes after the front finishes and before the back one.
    internal static string? PeelLayer(string? face, string? infill, string? front, string? secondFront, string? back, string? deck, string? step = null)
    {
        if ((deck != null || step != null) && face is null or "back") return deck != null ? "deck" : "step";
        string? hit = face switch { "front" => front, "secondfront" => secondFront, "back" => back, _ => null };
        if (hit != null) return face;
        if (front != null) return "front";
        if (secondFront != null) return "secondfront";
        if (deck != null) return "deck";
        if (step != null) return "step";
        if (back != null) return "back";
        return infill != null ? "infill" : null;
    }

    // A deck's own layers in a floor's order: the hit face's finish (front is the top, back the underside),
    // then any finish, then the infill, then the joists. Null face leaves the outermost layer.
    internal static string DeckPeelLayer(string? deckFace, string? deckInfill, string? deckFront, string? deckBack)
        => PeelLayer(deckFace, deckInfill, deckFront, null, deckBack, null) switch
        {
            "front" => "deckfront",
            "back" => "deckback",
            "infill" => "deckinfill",
            _ => "deck",
        };

    // An item that is both a framing and an infill, as sticks are, fills a bare frame rather than
    // decking or cornering it; a filled wall has no infill left to take, so there it frames.
    internal static string? MatchFraming(AssetLocation heldCode, bool filled, JsonObject framings, JsonObject infills)
        => !filled && MatchConsumes(heldCode, infills) != null ? null : MatchConsumes(heldCode, framings);

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

    // Which finish layer a build-flow click's clicked face targets - the hugged side is
    // "front", the opposite side is "back", an end/top/bottom face is neither. A cornerout's
    // second leg has its own hugged-side layer, "secondfront", but still shares "back" with
    // the first leg.
    internal static string? ResolveFinishFace(string layout, string side, BlockFacing clickedFace)
    {
        if (FinishFaceFor(side, clickedFace, "front") is string face) return face;
        if (!ClaimsTwoFaces(layout)) return null;
        return FinishFaceFor(CorneroutSecondFace[side], clickedFace, layout == "diagonal" ? "front" : "secondfront");
    }

    private static string? FinishFaceFor(string side, BlockFacing clickedFace, string frontLayer)
    {
        if (clickedFace.Code == side) return frontLayer;
        if (clickedFace == BlockFacing.FromCode(side).Opposite) return "back";
        return null;
    }
}
