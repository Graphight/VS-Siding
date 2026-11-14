using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using Newtonsoft.Json.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;
using Vintagestory.GameContent;

[assembly: InternalsVisibleTo("VSSiding.Tests")]

namespace VSSiding;

internal enum DeckHalf { Inner, Outer }

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
    // A diagonal's are the two ends of its staircase in blocktypes/wall.json, then the eleven
    // steps between them at plate height.
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
            Enumerable.Range(1, 11)
                .Select(k => new Cuboidf(k / 16f, 15f / 16, (12f - k) / 16, (k + 4f) / 16, 1, (16f - k) / 16))
                .ToArray()),
        // An inside corner's post: a bare face draws in the framing, so the frame fills its 4/16 footprint, with no plate.
        ["cornerin"] = ([new Cuboidf(0, 0, 0, 4f / 16, 1, 4f / 16)], []),
    };

    // Unrotated ("west") deck boxes per layout, matching the area WallShapeGen clips the floor's layers to.
    // A diagonal's are eight strips stepping across the half of the cell on the room side of its panel.
    // A post's are two strips covering the cell around its 4/16 corner.
    private static readonly Dictionary<string, Cuboidf[]> UnrotatedDeckBoxes = new()
    {
        ["cornerin"] = new[] { new Cuboidf(4f / 16, 12f / 16, 0, 1, 1, 1), new Cuboidf(0, 12f / 16, 4f / 16, 4f / 16, 1, 1) },
        ["wall"] = new[] { new Cuboidf(4f / 16, 12f / 16, 0, 1, 1, 1) },
        ["cornerout"] = new[] { new Cuboidf(4f / 16, 12f / 16, 4f / 16, 1, 1, 1) },
        ["diagonal"] = Enumerable.Range(0, 8)
            .Select(k => new Cuboidf((15f - 2 * k) / 16, 12f / 16, 2f * k / 16, 1, 1, (2f * k + 2) / 16))
            .ToArray(),
    };

    // Unrotated ("west") step boxes per element, for the collision only; the mesh draws a step from the stair's own shapes.
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

    private static readonly Dictionary<(string layout, string side), Cuboidf[]> DeckBoxes = BuildDeckBoxes();

    // A diagonal's outer deck is its inner strips turned 180 degrees about the cell's centre (decision 0096).
    private static readonly Dictionary<string, Cuboidf[]> OuterDeckBoxes = CorneroutSecondFace.Keys.ToDictionary(
        side => side, side => DeckBoxes[("diagonal", side)].Select(box => box.RotatedCopy(0, 180, 0, new Vec3d(0.5, 0.5, 0.5))).ToArray());

    private static readonly Dictionary<(string side, string element), Cuboidf> StepBoxes = BuildStepBoxes();

    private static readonly Dictionary<(string side, string orientation), Cuboidf[]> CornerStepBoxes = BuildCornerStepBoxes();

    // A corner's step is cut in the stair's frame, as its mesh is (SidingWallEntity.StepFrame): the stair-north
    // step clipped to x 4..16 (left) or 0..12 (right) and z 4..16 (front) or 0..12 (back), then turned by the facing.
    private static Dictionary<(string side, string orientation), Cuboidf[]> BuildCornerStepBoxes()
    {
        var origin = new Vec3d(0.5, 0.5, 0.5);
        var boxes = new Dictionary<(string side, string orientation), Cuboidf[]>();
        foreach (string side in CorneroutSecondFace.Keys)
        {
            foreach (string vertical in new[] { "up", "down" })
            {
                foreach (string facing in SidingWallEntity.StairRotationByFacing.Keys)
                {
                    string orientation = $"{vertical}-{facing}";
                    if (SidingWallEntity.StepFrame("cornerout", side, orientation) is not { } frame) continue;
                    (float x1, float x2) = frame.Hand == "left" ? (4f / 16, 1f) : (0f, 12f / 16);
                    (float z1, float z2) = frame.End == "front" ? (4f / 16, 1f) : (0f, 12f / 16);
                    float halfZ2 = Math.Min(z2, 8f / 16);
                    (float baseY1, float baseY2, float stepY1, float stepY2) = vertical == "up" ? (0f, 8f / 16, 8f / 16, 1f) : (8f / 16, 1f, 0f, 8f / 16);
                    boxes[(side, orientation)] =
                    [
                        new Cuboidf(x1, baseY1, z1, x2, baseY2, z2).RotatedCopy(0, frame.Rotation, 0, origin),
                        new Cuboidf(x1, stepY1, z1, x2, stepY2, halfZ2).RotatedCopy(0, frame.Rotation, 0, origin),
                    ];
                }
            }
        }
        return boxes;
    }

    private static Dictionary<(string layout, string side), Cuboidf[]> BuildDeckBoxes()
    {
        var origin = new Vec3d(0.5, 0.5, 0.5);
        var boxes = new Dictionary<(string layout, string side), Cuboidf[]>();
        foreach (var (layout, layoutBoxes) in UnrotatedDeckBoxes)
        {
            foreach (string side in CorneroutSecondFace.Keys)
            {
                float rotationYDeg = SidingWallEntity.RotationYDeg(side);
                boxes[(layout, side)] = layoutBoxes.Select(box => box.RotatedCopy(0, rotationYDeg, 0, origin)).ToArray();
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
    // panel's, except a diagonal's deck, whose strips run on under its plate to the middle of the frame.
    // A step applies to a wall and a cornerout; a diagonal never has one.
    internal static Cuboidf[] AddOpenPartBoxes(Cuboidf[] boxes, string layout, string side, string? deck, string? stepOrientation, string? outerDeck = null)
    {
        if (deck == null && outerDeck == null && stepOrientation == null) return boxes;
        // No lambda here: one capturing these arguments would be built on every call, the plain wall's included.
        var cache = OpenPartCache.GetValue(boxes, static _ => new());
        var key = (layout, side, deck != null, outerDeck != null, stepOrientation);
        return cache.TryGetValue(key, out var cached) ? cached : cache.GetOrAdd(key, BuildOpenPartBoxes(boxes, layout, side, deck != null, outerDeck != null, stepOrientation));
    }

    private static Cuboidf[] BuildOpenPartBoxes(Cuboidf[] boxes, string layout, string side, bool deck, bool outerDeck, string? stepOrientation)
    {
        if (deck) boxes = boxes.Concat(DeckBoxes[(layout, side)]).ToArray();
        if (outerDeck) boxes = boxes.Concat(OuterDeckBoxes[side]).ToArray();
        var step = OpenStepBoxes(layout, side, stepOrientation);
        return step.Length == 0 ? boxes : boxes.Concat(step).ToArray();
    }

    // Per base array, one result per deck, outer deck and step: this runs every physics tick.
    private static readonly ConditionalWeakTable<Cuboidf[], ConcurrentDictionary<(string layout, string side, bool deck, bool outerDeck, string? step), Cuboidf[]>> OpenPartCache = new();

    private static Cuboidf[] OpenStepBoxes(string layout, string side, string? stepOrientation)
    {
        if (stepOrientation == null) return [];
        if (layout == "wall") return SidingWallEntity.StepElements(side, stepOrientation).Select(element => StepBoxes[(side, element)]).ToArray();
        return layout == "cornerout" && CornerStepBoxes.TryGetValue((side, stepOrientation), out var boxes) ? boxes : [];
    }

    // wall.json's collisionSelectionBoxesbytype makes the selection box the panel alone, so without
    // this the deck can be stood on but not aimed at, and a break from below lands on whatever
    // wall lies beyond it.
    public override Cuboidf[] GetSelectionBoxes(IBlockAccessor blockAccessor, BlockPos pos)
    {
        var boxes = base.GetSelectionBoxes(blockAccessor, pos);
        var entity = blockAccessor.GetBlockEntity<SidingWallEntity>(pos);
        var own = entity == null ? boxes : AddOpenPartBoxes(
            boxes, Variant["layout"], Variant["side"], entity.Deck, entity.Step == null ? null : entity.StepOrientation, entity.OuterDeck);
        return SidingFloorBlock.WithHangerBelow(blockAccessor, pos, own);
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

        return AddOpenPartBoxes(boxes, Variant["layout"], Variant["side"], entity.Deck, entity.Step == null ? null : entity.StepOrientation, entity.OuterDeck);
    }

    // Which neighbours this cell shares a member with, i.e. draws no plate or post against. For a
    // diagonal, left and right say which ends the next cell carries the run on from (decision 0066).
    internal (bool above, bool below, bool left, bool right) NeighbourJoins(
        IBlockAccessor blockAccessor, BlockPos pos, string? infill)
    {
        var runsOn = (left: false, right: false);
        if (Variant["layout"] == "diagonal")
        {
            var (leftStep, rightStep) = DiagonalRunNeighbours(Variant["side"]);
            runsOn = (ContinuesDiagonal(blockAccessor, pos.AddCopy(leftStep)), ContinuesDiagonal(blockAccessor, pos.AddCopy(rightStep)));
        }

        // Glazing merges with the glazing around it in every direction, with no member between,
        // so a run of it reads as one sheet however large. Opaque fill keeps decision 0008's
        // alternating cross-beam. That is why this asks the infill and not the layout: merging
        // belongs to glass, not to a shape. A cornerout merges at its two leg ends and its corner
        // post stays either way (decision 0080).
        if (IsTransparent(infill, Attributes["Infills"]))
        {
            bool above = ContinuesGlazing(blockAccessor, pos.UpCopy(), infill);
            bool below = ContinuesGlazing(blockAccessor, pos.DownCopy(), infill);
            if (Variant["layout"] == "diagonal") return (above, below, runsOn.left, runsOn.right);
            var (leftEnd, rightEnd) = LeftRightEnds(Variant["layout"], Variant["side"]);
            return (above, below, EndContinuesGlazing(blockAccessor, pos, leftEnd, infill), EndContinuesGlazing(blockAccessor, pos, rightEnd, infill));
        }

        int cellsBelow = 0;
        for (BlockPos p = pos.DownCopy(); ContinuesFrame(blockAccessor, p, infill); p.Down()) cellsBelow++;
        return (JoinsAbove(ContinuesFrame(blockAccessor, pos.UpCopy(), infill), cellsBelow), cellsBelow > 0, runsOn.left, runsOn.right);
    }

    // Which ends of a diagonal the glass carries on through: the run goes on there and the diagonal it
    // goes on into is glazed in the same stack, so no post stands between them.
    internal (bool left, bool right) GlassRunsOn(
        IBlockAccessor blockAccessor, BlockPos pos, string? infill, (bool above, bool below, bool left, bool right) joins)
    {
        if (Variant["layout"] != "diagonal" || !IsTransparent(infill, Attributes["Infills"])) return default;
        var (leftStep, rightStep) = DiagonalRunNeighbours(Variant["side"]);
        return (joins.left && GlazedDiagonalAt(blockAccessor, pos.AddCopy(leftStep), infill),
            joins.right && GlazedDiagonalAt(blockAccessor, pos.AddCopy(rightStep), infill));
    }

    // Which of a corner's post ends another cell's end meets, a T, whatever either is filled with: the left end
    // of its front's line and the right end of its second face's. The post stays, and fills the slot the run crosses.
    internal (bool left, bool right) Tees(IBlockAccessor blockAccessor, BlockPos pos)
    {
        if (Variant["layout"] != "cornerout") return default;
        var ends = Ends("cornerout", Variant["side"]);
        bool Met(End end) => WallAt(blockAccessor, pos.AddCopy(end.Points)) is var (wall, _) && JoinsAtEnd(end, wall.Variant["layout"], wall.Variant["side"]);
        return (Met(ends[2]), Met(ends[3]));
    }

    private bool GlazedDiagonalAt(IBlockAccessor blockAccessor, BlockPos at, string? infill)
        => ContinuesDiagonal(blockAccessor, at) && ContinuesGlazing(infill, WallAt(blockAccessor, at)?.entity, Attributes["Infills"]);

    // The corners a cell merged on both sides still has to frame, as the fillers' suffixes. Where the
    // cell diagonally across carries no glazing on, the sheet is an L and its frame turns an inside
    // corner in this cell; four merged cells round a point leave it clear.
    internal static string[] InnerCorners((bool above, bool below, bool left, bool right) joins, System.Func<string, string, bool> glazedAcross)
        => (from rail in new[] { (Joined: joins.above, Part: "-top"), (Joined: joins.below, Part: "-bottom") }
            from stile in new[] { (Joined: joins.left, Part: "-left"), (Joined: joins.right, Part: "-right") }
            where rail.Joined && stile.Joined && !glazedAcross(rail.Part, stile.Part)
            select rail.Part + stile.Part).ToArray();

    // A diagonal's sheet turns an inside corner at an end its glass runs on through, where the next cell's stack stops short of its own.
    internal string[] InnerCorners(IBlockAccessor blockAccessor, BlockPos pos, string? infill, (bool above, bool below, bool left, bool right) joins)
    {
        if (!IsTransparent(infill, Attributes["Infills"])) return [];
        if (Variant["layout"] == "diagonal")
        {
            var (leftStep, rightStep) = DiagonalRunNeighbours(Variant["side"]);
            var glassOn = GlassRunsOn(blockAccessor, pos, infill, joins);
            return InnerCorners((joins.above, joins.below, glassOn.left, glassOn.right), (rail, stile) =>
                GlazedDiagonalAt(blockAccessor, pos.AddCopy(stile == "-left" ? leftStep : rightStep).Add(0, rail == "-top" ? 1 : -1, 0), infill));
        }
        if (Variant["layout"] is not ("wall" or "cornerout")) return [];
        var (leftEnd, rightEnd) = LeftRightEnds(Variant["layout"], Variant["side"]);
        return InnerCorners(joins, (rail, stile) =>
            EndContinuesGlazing(blockAccessor, pos.AddCopy(0, rail == "-top" ? 1 : -1, 0), stile == "-left" ? leftEnd : rightEnd, infill));
    }

    // Where a cell's frame stops along a run: the face the end lies on and the way the run carries on from it.
    internal readonly record struct End(BlockFacing Face, BlockFacing Points);

    // Each layout's, left then right. A wall's lie on `side`; a cornerout's and a diagonal's left lies on the
    // second face and right on `side`, and the cornerout adds the two ends of its corner post (decision 0066).
    internal static End[] Ends(string layout, string side)
    {
        BlockFacing face = BlockFacing.FromCode(side);
        var (left, right) = RunNeighbours(side);
        return layout switch
        {
            "wall" => [new(face, left), new(face, right)],
            "cornerout" => [new(left, face.Opposite), new(face, right), new(face, left), new(left, face)],
            "diagonal" => [new(left, face.Opposite), new(face, right)],
            _ => [],
        };
    }

    // The ends a cell's left and right joins answer for: a corner's leg 2 is its left and leg 1 its right.
    internal static (End left, End right) LeftRightEnds(string layout, string side)
    {
        var ends = Ends(layout, side);
        return (ends[0], ends[1]);
    }

    // Two ends on the same face pointing at each other are the one join seen from both cells.
    internal static bool EndsMeet(End a, End b) => a.Face == b.Face && a.Points == b.Points.Opposite;

    private bool EndContinuesGlazing(IBlockAccessor blockAccessor, BlockPos pos, End end, string? infill)
    {
        BlockPos neighbourPos = pos.AddCopy(end.Points);
        if (WallAt(blockAccessor, neighbourPos) is not var (wall, neighbour)) return false;
        return JoinsAtEnd(end, wall.Variant["layout"], wall.Variant["side"])
            && ContinuesGlazing(infill, neighbour, Attributes["Infills"]);
    }

    // Whether a cell of `layout` and `side` has an end meeting `end`: another wall on its line, a corner
    // whose leg end or corner post end lies against it, or a diagonal whose end meets it (decision 0080).
    // Whether glass runs on through the join is EndContinuesGlazing's question, not this one's.
    // shortcut: this compares the two ends alone, so a straight wall standing where a diagonal run passes its
    // corner, a clashing build, may drop its stile against a diagonal whose run goes on into the next diagonal
    // there instead; upgrade if players build it.
    internal static bool JoinsAtEnd(End end, string layout, string side)
        => Ends(layout, side).Any(other => EndsMeet(end, other));

    // A diagonal on the same line, whichever way it faces.
    private bool ContinuesDiagonal(IBlockAccessor blockAccessor, BlockPos neighbourPos)
        => blockAccessor.GetBlock(neighbourPos) is SidingWallBlock other && other.Variant["layout"] == "diagonal"
            && (other.Variant["side"] == Variant["side"] || other.Variant["side"] == BlockFacing.FromCode(Variant["side"]).Opposite.Code);

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

    // The two cells a diagonal's run carries on into: a step along the run and a step out through the
    // hugged side at the left end, and the reverse at the right.
    internal static (Vec3i left, Vec3i right) DiagonalRunNeighbours(string side)
    {
        var (left, right) = RunNeighbours(side);
        Vec3i face = BlockFacing.FromCode(side).Normali;
        return (new Vec3i(left.Normali.X - face.X, 0, left.Normali.Z - face.Z), new Vec3i(right.Normali.X + face.X, 0, right.Normali.Z + face.Z));
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

    // A cornerout on the run's right end has the wall's old face as its second face, so the finish
    // on it moves to the second slot; one on the left end keeps that face as its front.
    internal static bool FrontMovesToSecondFace(string wallSide, string corneroutSide) => corneroutSide != wallSide;

    // The block a bare frame becomes when the saw picks another layout (decision 0026): a wall turns
    // into a cornerout or a diagonal at the end clicked, a cornerout into a diagonal on the same
    // side, and nothing turns back. Null when the pick offers no upgrade.
    internal static string? ResolveFramingUpgrade(string layout, string side, string picked, Vec3d hitPosition) => (layout, picked) switch
    {
        ("wall", "cornerout" or "diagonal") => $"wall-{picked}-{ResolveCornerUpgrade(side, hitPosition)}",
        ("cornerout", "diagonal") => $"wall-diagonal-{side}",
        _ => null,
    };

    // The leg of a corner a step continues: the one whose panel faces along the clicked face's axis, and for the
    // underside the one nearer the hit. A wall has the one leg.
    internal static string ResolveStepLeg(string layout, string side, BlockFacing clickedFace, Vec3d hit)
    {
        if (layout != "cornerout") return side;
        string second = CorneroutSecondFace[side];
        if (clickedFace.IsHorizontal) return clickedFace.Axis == BlockFacing.FromCode(side).Axis ? side : second;
        double Distance(string leg) => leg switch { "west" => hit.X, "east" => 1 - hit.X, "north" => hit.Z, _ => 1 - hit.Z };
        return Distance(side) <= Distance(second) ? side : second;
    }

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
    // Not a diagonal's: the open part of its cell is a triangle (decision 0066).
    public override bool IsReplacableBy(Block block)
        => (SidingModSystem.IsHostableId(block.BlockId) && Variant["layout"] != "diagonal") || base.IsReplacableBy(block);

    // The placement failure code keeping a block out of the wall's cell: a step takes nothing, a deck
    // takes only a block that fits under it (decision 0094).
    internal static string? OpenPartRefusal(SidingWallEntity? entity, bool underDeck = false)
        => entity?.Deck != null && !underDeck ? "vssiding-decked" : entity?.Step != null ? "vssiding-stepped" : null;

    // Right-click on the wall's open side with a hostable block and no saw places that block in the
    // wall's own cell, where vanilla would put it in the cell in front. The client only reports the
    // interaction handled - TryPlaceBlock must run once, on the server, or the client would place
    // the held block into the front cell itself before the server ever gets to swap the wall out.
    private bool TryHost(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
    {
        ItemSlot slot = byPlayer.InventoryManager.ActiveHotbarSlot;
        Block? heldBlock = slot.Itemstack?.Block;
        if (heldBlock == null || !SidingModSystem.IsHostableId(heldBlock.BlockId) || Variant["layout"] == "diagonal") return false;
        if (!HostsClick(Variant["layout"], Variant["side"], blockSel.Face)) return false;

        // Furniture would sit where the deck or step is. Swallowed rather than returning false,
        // which hands the click to vanilla and hosts anyway. Other ways in drop it
        // (HostChangePrefix).
        var hostEntity = world.BlockAccessor.GetBlockEntity<SidingWallEntity>(blockSel.Position);
        bool underDeck = SidingModSystem.IsUnderDeckId(SidingModSystem.PlacedVariant(world.BlockAccessor, heldBlock, blockSel.Face).BlockId);
        if (OpenPartRefusal(hostEntity, underDeck) is { } refusal)
        {
            (byPlayer as IServerPlayer)?.SendIngameError(refusal, Lang.Get("placefailure-" + refusal));
            return true;
        }

        if (world.Side == EnumAppSide.Client) return true;

        PlaceHeld(world, byPlayer, slot, heldBlock, blockSel);
        return true;
    }

    // A click on the outer face, where a thin floor fills the cell beyond, would place into the floor's cell, which takes
    // nothing. A block that hangs on a wall face and fits under a deck goes into the cell below instead, on the wall
    // there, and the floor raises it to its underside (decision 0094). It places on the server only, as TryHost does.
    private bool TryRaise(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
    {
        ItemSlot slot = byPlayer.InventoryManager.ActiveHotbarSlot;
        Block? heldBlock = slot.Itemstack?.Block;
        if (heldBlock == null || !blockSel.Face.IsHorizontal || HostsClick(Variant["layout"], Variant["side"], blockSel.Face)) return false;

        var accessor = world.BlockAccessor;
        var floorPos = blockSel.Position.AddCopy(blockSel.Face);
        if (accessor.GetBlockEntity<SidingFloorEntity>(floorPos) is not { } floor) return false;
        Block placed = SidingModSystem.PlacedVariant(accessor, heldBlock, blockSel.Face);
        if (!SidingModSystem.IsUnderDeckId(placed.BlockId) || !SidingModSystem.HangsOnSide(placed)) return false;

        var target = floorPos.DownCopy();
        var wallBelow = blockSel.Position.DownCopy();
        if (accessor.GetBlock(target) is SidingWallBlock || !accessor.GetBlock(target).IsReplacableBy(heldBlock)
            || !accessor.GetBlock(wallBelow).CanAttachBlockAt(accessor, placed, wallBelow, blockSel.Face)) return false;

        // The server sends a tick's block changes before its block entities, so the clicking client sets the flag itself,
        // or it would draw the block low for a frame. The server sends the floor back either way, correcting a refusal.
        floor.RaisedBelow = true;
        if (world.Side == EnumAppSide.Client) return true;

        var below = blockSel.Clone();
        below.Position = target;
        floor.RaisedBelow = PlaceHeld(world, byPlayer, slot, heldBlock, below) && SidingModSystem.IsUnderDeckId(accessor.GetBlock(target).BlockId);
        floor.MarkDirty(true);
        return true;
    }

    private static bool PlaceHeld(IWorldAccessor world, IPlayer byPlayer, ItemSlot slot, Block heldBlock, BlockSelection blockSel)
    {
        string failureCode = "";
        if (!heldBlock.TryPlaceBlock(world, byPlayer, slot.Itemstack!, blockSel, ref failureCode))
        {
            (byPlayer as IServerPlayer)?.SendIngameError(failureCode, Lang.Get("placefailure-" + failureCode));
            return false;
        }

        bool isCreative = byPlayer.WorldData.CurrentGameMode == EnumGameMode.Creative;
        if (!isCreative) slot.TakeOut(1);
        slot.MarkDirty();
        if (heldBlock.Sounds != null) world.PlaySoundAt(heldBlock.Sounds.Place, blockSel.Position, -0.5, byPlayer);
        return true;
    }

    // Every layer a click adds needs build rights in a land claim, as placing a block does (decision 0098).
    internal static bool ClaimRefuses(IWorldAccessor world, IPlayer byPlayer, BlockPos pos)
        => !world.Claims.TryAccess(byPlayer, pos, EnumBlockAccessFlags.BuildOrBreak);

    // True keeps a held click one interaction, so vanilla's repeat does not press the next layer on (decision 0075).
    public override bool OnBlockInteractStep(float secondsUsed, IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel) => true;

    // A saw in the off hand layers infill onto a framed wall, then finishes onto a filled
    // one - which face was clicked picks Front vs Back. Returns true for every handled
    // branch (including the wrong-face error) so vanilla's "place block against" fallthrough
    // doesn't also fire. Plain right-click, not shift - see decision 0006.
    public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
    {
        if (TryRaise(world, byPlayer, blockSel)) return true;
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
        // and sticks still fill the bare wall or deck face they hit (MatchFraming, FramesOnFrom).
        DeckHalf? hitHalf = entity == null ? null : HitDeckHalf(world.BlockAccessor, blockSel, entity);
        bool deckHit = hitHalf != null;
        bool hitFilled = deckHit ? entity!.DeckLayers(hitHalf!.Value).Infill != null : entity != null && FramesAsFilled(entity);
        if (SidingModePicker.Layout(byPlayer) == "floor" && MatchFraming(heldCode, hitFilled, Attributes["Framings"], Attributes["Infills"]) != null
            && !(deckHit && hitFilled && SidingFloorBlock.FinishFace(blockSel.Face) != null))
            return base.OnBlockInteractStart(world, byPlayer, blockSel);

        if (entity == null || entity.Framing == null) return base.OnBlockInteractStart(world, byPlayer, blockSel);

        bool isCreative = byPlayer.WorldData.CurrentGameMode == EnumGameMode.Creative;

        // A stair on a side face fills the open part beside the stair in the room (decision 0046).
        if (slot.Itemstack!.Block is BlockStairs && blockSel.Face != BlockFacing.UP)
        {
            if (ClaimRefuses(world, byPlayer, blockSel.Position)) return true;

            if (Variant["layout"] is "diagonal" or "cornerin")
            {
                string noStep = Variant["layout"] == "diagonal" ? "diagonal" : "post";
                (byPlayer as IServerPlayer)?.SendIngameError($"vssiding:step{noStep}", Lang.Get($"vssiding:build-step-{noStep}"));
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

            string leg = ResolveStepLeg(Variant["layout"], Variant["side"], blockSel.Face, blockSel.HitPosition);
            BlockPos neighbourPos = blockSel.Position.AddCopy(BlockFacing.FromCode(leg).Opposite);
            // A stair with no vertical group, like vanilla's stone path, only comes upright.
            string? neighbourOrientation = world.BlockAccessor.GetBlock(neighbourPos) is BlockStairs neighbourBlock
                && neighbourBlock.Variant["horizontalorientation"] is { } neighbourFacing
                ? $"{neighbourBlock.Variant["verticalorientation"] ?? "up"}-{neighbourFacing}"
                : null;

            string orientation = ResolveStepOrientation(
                leg, neighbourOrientation, byPlayer.Entity.SidedPos.GetViewVector(), blockSel.Face, blockSel.HitPosition.Y,
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

        // A post takes no infill; it goes straight to the finish path at a quarter of the cost.
        bool post = Variant["layout"] == "cornerin";

        if (hitHalf is { } half)
            return LayerDeck(world, byPlayer, blockSel, entity, slot, heldCode, isCreative, half);

        // With the deck lit, planks on a side face add a deck in place. Ahead of finishing, since
        // planks are a finish too. The top face still stacks the next course (PlaceWallFrame).
        if (blockSel.Face != BlockFacing.UP && SidingModePicker.Deck(byPlayer)
            && TryAddDeck(world, byPlayer, blockSel.Position, entity, slot, heldCode, isCreative, HalfFacing(blockSel.Face)))
            return true;

        if (!post && entity.Infill == null)
        {
            // A bare frame clicked in corner or diagonal mode becomes one in place, for a T-junction
            // found once a partition reaches it (decision 0026). Nothing is charged: a fresh
            // cornerout or diagonal frame costs the same as a fresh wall frame. Everything this
            // doesn't claim falls through to the infill match below, then to PlaceWallFrame.
            if (TryUpgradeLayout(world, byPlayer, blockSel, entity, heldCode)) return true;

            string? infillKey = MatchConsumes(heldCode, Attributes["Infills"]);
            if (infillKey == null) return base.OnBlockInteractStart(world, byPlayer, blockSel);
            if (FramesOnFrom(heldCode, ResolveFinishFace(Variant["layout"], Variant["side"], blockSel.Face) != null, Attributes["Framings"]))
                return base.OnBlockInteractStart(world, byPlayer, blockSel);

            if (ClaimRefuses(world, byPlayer, blockSel.Position)) return true;

            // The full slab, not the open frame's collision: infill would seal in anyone standing in it.
            var occupants = world.GetIntersectingEntities(blockSel.Position, base.GetCollisionBoxes(world.BlockAccessor, blockSel.Position), e => e.IsInteractable);
            if (occupants is { Length: > 0 })
            {
                (byPlayer as IServerPlayer)?.SendIngameError("vssiding:occupied", Lang.Get("vssiding:build-occupied"));
                return true;
            }

            var consumes = Attributes["Infills"][infillKey]["Consumes"];
            if (!TryAffordOrError(byPlayer, isCreative, slot.StackSize, consumes)) return true;

            int oldAbsorption = AbsorptionOf(entity);
            entity.Infill = infillKey;
            OnInfillChanged(world, entity, blockSel.Position, oldAbsorption);
            ConsumeHeld(slot, consumes, isCreative);
            return true;
        }

        if (MatchConsumes(heldCode, Attributes["Finishes"], null, true) is { } tileKey)
            return LayTile(world, byPlayer, blockSel, entity, slot, tileKey, isCreative);

        string? finishKey = MatchConsumes(heldCode, Attributes["Finishes"], slot.Itemstack!.Attributes);
        if (finishKey == null) return base.OnBlockInteractStart(world, byPlayer, blockSel);

        // The picker's chosen style is used only if this finish lists it; otherwise the entry's
        // default applies.
        string? style = SidingModePicker.FinishChoices(byPlayer).FirstOrDefault(s => HasStyle(Attributes["Finishes"][finishKey], s));

        // Planks that can't finish this face still extend the wall via PlaceWallFrame, and held blocks still place.
        bool heldPlaces = slot.Itemstack!.Class == EnumItemClass.Block || MatchConsumes(heldCode, Attributes["Framings"]) != null;

        string side = Variant["side"];
        string? face = ResolveFinishFace(Variant["layout"], side, blockSel.Face)
            ?? HiddenBareFrontAtEnd(world.BlockAccessor, blockSel.Position, blockSel.Face, entity);
        if (face == null)
        {
            if (heldPlaces) return base.OnBlockInteractStart(world, byPlayer, blockSel);
            (byPlayer as IServerPlayer)?.SendIngameError("vssiding:wrongface", Lang.Get("vssiding:build-wrong-face"));
            return true;
        }

        if (ClaimRefuses(world, byPlayer, blockSel.Position)) return true;

        string? currentKey = FinishOn(entity, face);
        string? currentStyle = StyleOn(entity, face);
        if (currentKey != null)
        {
            // Restyling the same material is free: only the profile changes, so there is nothing
            // to charge for and nothing to drop.
            if (style != null && currentKey == finishKey && currentStyle != style)
            {
                SetFinish(world, entity, blockSel.Position, face, finishKey, style);
                return true;
            }

            // A finish clicked on a finished face goes on the face behind it while that one is bare (decision 0075),
            // except a framing material that would turn this wall into a corner (PlaceWallFrame), and on a post, whose
            // every face is clicked on its own and whose backs an inside corner hides.
            bool turnsToCorner = Variant["layout"] == "wall" && SidingModePicker.Layout(byPlayer) == "cornerout"
                && MatchFraming(heldCode, true, Attributes["Framings"], Attributes["Infills"]) != null;
            string? farFace = turnsToCorner || post ? null : ResolveFinishFace(Variant["layout"], side, blockSel.Face.Opposite);
            if (farFace == null || FinishOn(entity, farFace) != null)
            {
                if (heldPlaces) return base.OnBlockInteractStart(world, byPlayer, blockSel);
                (byPlayer as IServerPlayer)?.SendIngameError("vssiding:alreadyfinished", Lang.Get("vssiding:build-already-finished"));
                return true;
            }

            face = farFace;
        }

        var finishConsumes = Attributes["Finishes"][finishKey]["Consumes"];
        int parts = LayerParts(Variant["layout"], face);
        if (parts > 1) finishConsumes = ShareConsumes(finishConsumes, parts);
        if (!TryAffordOrError(byPlayer, isCreative, slot.StackSize, finishConsumes)) return true;

        SetFinish(world, entity, blockSel.Position, face, finishKey, MirroredStyle(Variant["layout"], face, entity, Attributes["Finishes"], finishKey) ?? style);
        ConsumeHeld(slot, finishConsumes, isCreative);
        return true;
    }

    // A tile goes into the slot under the crosshair, one tile a slot (decision 0095). A face with another finish sends it to the
    // bare face behind, as a finish does (decision 0075); a full slot is the already-finished answer, with no jump.
    private bool LayTile(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel, SidingWallEntity entity, ItemSlot slot, string tileKey, bool isCreative)
    {
        if (ClaimRefuses(world, byPlayer, blockSel.Position)) return true;

        string layout = Variant["layout"];
        string? refusal = layout == "diagonal" ? "diagonal" : IsTransparent(entity.Infill, Attributes["Infills"]) ? "glazed" : null;
        if (refusal != null)
        {
            (byPlayer as IServerPlayer)?.SendIngameError($"vssiding:tile{refusal}", Lang.Get($"vssiding:build-tile-{refusal}"));
            return true;
        }

        string side = Variant["side"];
        string? face = ResolveFinishFace(layout, side, blockSel.Face) ?? HiddenBareFrontAtEnd(world.BlockAccessor, blockSel.Position, blockSel.Face, entity);
        if (face == null)
        {
            (byPlayer as IServerPlayer)?.SendIngameError("vssiding:wrongface", Lang.Get("vssiding:build-wrong-face"));
            return true;
        }

        string? currentKey = FinishOn(entity, face);
        if (currentKey != null && currentKey != SidingWallEntity.TilesKey)
        {
            string? farFace = layout == "cornerin" ? null : ResolveFinishFace(layout, side, blockSel.Face.Opposite);
            face = farFace != null && FinishOn(entity, farFace) == null ? farFace : face;
            currentKey = FinishOn(entity, face);
        }

        int tileSlot = TileSlot(layout, side, face, blockSel.HitPosition);
        string? style = StyleOn(entity, face);
        bool fill = byPlayer.Entity.Controls.ShiftKey;
        if (currentKey != null && (currentKey != SidingWallEntity.TilesKey || TileSlotFull(style, tileSlot, fill)))
        {
            (byPlayer as IServerPlayer)?.SendIngameError("vssiding:alreadyfinished", Lang.Get("vssiding:build-already-finished"));
            return true;
        }

        LayTiles(byPlayer, slot, isCreative, Attributes["Finishes"], tileKey, style, tileSlot,
            laid => SetFinish(world, entity, blockSel.Position, face, SidingWallEntity.TilesKey, laid));
        return true;
    }

    // Lays the tiles a click takes and charges for them. A cut tile costs a whole one, so a tile ignores the layer's share (LayerParts).
    internal static void LayTiles(
        IPlayer byPlayer, ItemSlot slot, bool isCreative, JsonObject finishes, string tileKey, string? style, int tileSlot, System.Action<string> setStyle)
    {
        var consumes = finishes[tileKey]["Consumes"];
        var (laidStyle, count) = TilesToLay(style, tileSlot, tileKey, byPlayer.Entity.Controls.ShiftKey, slot.StackSize, isCreative);
        if (!TryAffordOrError(byPlayer, isCreative, slot.StackSize, consumes, count)) return;

        setStyle(laidStyle);
        ConsumeHeld(slot, consumes, isCreative, count);
    }

    // A sneak-click fills every empty slot on the face, so it is refused only once none is left (decision 0095).
    internal static bool TileSlotFull(string? style, int slot, bool fill)
        => fill ? SidingWallEntity.TileSlots(style).All(kind => kind != "") : SidingWallEntity.TileSlots(style)[slot] != "";

    // The style a tile click leaves and how many tiles it takes: the one slot, or every empty one in slot order, as many as are held.
    internal static (string Style, int Count) TilesToLay(string? style, int slot, string tileKey, bool fill, int held, bool isCreative)
    {
        if (!fill) return (SidingWallEntity.WithTile(style, slot, tileKey), 1);

        string[] slots = SidingWallEntity.TileSlots(style);
        int count = 0;
        for (int i = 0; i < slots.Length && (isCreative || count < held); i++)
        {
            if (slots[i] != "") continue;
            slots[i] = tileKey;
            count++;
        }
        return (string.Join(",", slots), count);
    }

    // Which of a face's four tile slots a hit lies in: row 0 the lower half, col 0 the lower coordinate along the face in
    // the shape's unrotated frame (west side), found by turning the hit back through the side's rotation. A leg's front and
    // back run along z and a corner's second leg along x; a post's faces are 4 wide, so show the column 0 slots only.
    internal static int TileSlot(string layout, string side, string face, Vec3d hit)
    {
        int row = hit.Y >= 0.5 ? 1 : 0;
        if (layout == "cornerin") return row * 2;

        double x = hit.X - 0.5, z = hit.Z - 0.5;
        (double xu, double zu) = side switch { "south" => (-z, x), "east" => (-x, -z), "north" => (z, -x), _ => (x, z) };
        return row * 2 + ((face is "front" or "back" ? zu : xu) >= 0 ? 1 : 0);
    }

    // A floor's or a deck's slots are the world grid, whatever the wall's side: row 0 the lower z, col 0 the lower x.
    internal static int FloorTileSlot(Vec3d hit) => (hit.Z >= 0.5 ? 2 : 0) + (hit.X >= 0.5 ? 1 : 0);

    // The face and slot a tile laid on a floor or a deck goes in, or null once a refusal has been sent: the clicked face while
    // it takes tiles, else the bare face behind it (decision 0075); a full slot is the already-finished answer.
    internal static (string Face, int Slot)? FlatTilePlacement(
        IPlayer byPlayer, BlockSelection blockSel, string? infill, JsonObject infills, System.Func<string, string?> keyOn, System.Func<string, string?> styleOn)
    {
        (string Code, string Lang)? error = null;
        string? face = SidingFloorBlock.FinishFace(blockSel.Face);
        if (IsTransparent(infill, infills)) error = ("tileglazed", "build-tile-glazed");
        else if (face == null) error = ("wrongface", "build-floor-wrong-face");
        else if (keyOn(face) != null && keyOn(face) != SidingWallEntity.TilesKey)
        {
            string? farFace = SidingFloorBlock.FinishFace(blockSel.Face.Opposite);
            if (farFace != null && keyOn(farFace) == null) face = farFace;
        }

        int slot = FloorTileSlot(blockSel.HitPosition);
        if (error == null && keyOn(face!) != null && (keyOn(face!) != SidingWallEntity.TilesKey || TileSlotFull(styleOn(face!), slot, byPlayer.Entity.Controls.ShiftKey)))
            error = ("alreadyfinished", "build-already-finished");

        if (error is not { } refusal) return (face!, slot);
        (byPlayer as IServerPlayer)?.SendIngameError($"vssiding:{refusal.Code}", Lang.Get($"vssiding:{refusal.Lang}"));
        return null;
    }

    // The slot a break takes: the one under the crosshair, else the first tile in the list, so infill never shows from behind a tile.
    internal static int TileToBreak(string? style, int hitSlot)
    {
        string[] slots = SidingWallEntity.TileSlots(style);
        return slots[hitSlot] != "" ? hitSlot : Array.FindIndex(slots, kind => kind != "");
    }

    // The slot a break takes when the layer it peels is a tiled face, else -1.
    private int TileBreakSlot(SidingWallEntity entity, string layer, Vec3d? hit)
    {
        if (BareLayer(layer) is not ("front" or "secondfront" or "back" or "secondback" or "deckfront" or "deckback") || KeyAt(layer, entity) != SidingWallEntity.TilesKey) return -1;
        int hitSlot = hit == null ? 0 : layer.Contains("deck") ? FloorTileSlot(hit) : TileSlot(Variant["layout"], Variant["side"], layer, hit);
        return TileToBreak(StyleOn(entity, layer), hitSlot);
    }

    private void RemoveTile(IWorldAccessor world, SidingWallEntity entity, BlockPos pos, string layer, int tileSlot)
    {
        string style = SidingWallEntity.WithTile(StyleOn(entity, layer), tileSlot, "");
        if (style.Trim(',').Length == 0) RemoveLayer(world, entity, pos, layer);
        else if (layer.Contains("deck"))
            SetDeckFinish(entity, BareLayer(layer)!["deck".Length..], SidingWallEntity.TilesKey, style, HalfOf(layer));
        else SetFinish(world, entity, pos, layer, SidingWallEntity.TilesKey, style);
    }

    // True when the click was claimed, by the upgrade or by a refusal. A filled wall only turns into a
    // cornerout, and only from PlaceWallFrame, which sees the clicks that could not board the face.
    internal bool TryUpgradeLayout(
        IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel, SidingWallEntity entity, AssetLocation heldCode)
    {
        string layout = Variant["layout"];
        string picked = SidingModePicker.Layout(byPlayer);
        bool filled = entity.Infill != null;
        if (filled && (layout, picked) != ("wall", "cornerout")) return false;

        string? upgrade = ResolveFramingUpgrade(layout, Variant["side"], picked, blockSel.HitPosition);
        if (upgrade == null
            || MatchFraming(heldCode, filled, Attributes["Framings"], Attributes["Infills"]) == null
            || ResolveFinishFace(layout, Variant["side"], blockSel.Face) == null)
            return false;

        if (ClaimRefuses(world, byPlayer, blockSel.Position)) return true;

        if (entity.Step != null)
        {
            (byPlayer as IServerPlayer)?.SendIngameError("vssiding:stepped", Lang.Get("vssiding:build-stepped"));
            return true;
        }

        if (world.GetBlock(new AssetLocation("vssiding", FirstCodePart() + upgrade["wall".Length..])) is not SidingWallBlock target) return false;

        if (filled && world.GetIntersectingEntities(blockSel.Position, target.GetCollisionBoxes(world.BlockAccessor, blockSel.Position), e => e.IsInteractable) is { Length: > 0 })
        {
            (byPlayer as IServerPlayer)?.SendIngameError("vssiding:occupied", Lang.Get("vssiding:build-occupied"));
            return true;
        }

        int oldAbsorption = AbsorptionOf(entity);

        // Keeps the block entity, and the engine repoints its Block at the new
        // type, so Framing survives and OnTesselation reads the new layout.
        world.BlockAccessor.ExchangeBlock(target.Id, blockSel.Position);
        if (filled)
        {
            if (FrontMovesToSecondFace(Variant["side"], target.Variant["side"]))
            {
                entity.SecondFront = entity.Front;
                entity.SecondFrontStyle = SidingWallEntity.TurnTiles(entity.Front, entity.FrontStyle);
                entity.Front = null;
                entity.FrontStyle = null;
                // The old wall's room side goes with it, moved rather than copied, since the upgrade is free (decision 0090).
                (entity.SecondBack, entity.SecondBackStyle, entity.Back, entity.BackStyle) = (entity.Back, SidingWallEntity.TurnTiles(entity.Back, entity.BackStyle), null, null);
            }

            // The new leg changes retention, light and the liquid barrier. On the target, since
            // OnInfillChanged exchanges the block for its own Id.
            target.OnInfillChanged(world, entity, blockSel.Position, oldAbsorption);
            return true;
        }

        entity.MarkDirty(true);
        // Plates key off the cells above and below sharing this one's layout
        // (decision 0008), which the swap just changed.
        MarkNeighboursDirty(world, blockSel.Position);
        return true;
    }

    // The deck's boxes are appended after the block's own selection boxes (AddOpenPartBoxes).
    internal bool IsDeckHit(IBlockAccessor blockAccessor, BlockSelection blockSel, SidingWallEntity entity)
        => HitDeckHalf(blockAccessor, blockSel, entity) != null;

    // Which half of the deck the selection is on: the inner's boxes come first, the outer's follow.
    internal DeckHalf? HitDeckHalf(IBlockAccessor blockAccessor, BlockSelection blockSel, SidingWallEntity entity)
    {
        int index = blockSel.SelectionBoxIndex - base.GetSelectionBoxes(blockAccessor, blockSel.Position).Length;
        int inner = entity.Deck == null ? 0 : DeckBoxes[(Variant["layout"], Variant["side"])].Length;
        if (index >= 0 && index < inner) return DeckHalf.Inner;
        return entity.OuterDeck != null && index >= inner && index < inner + OuterDeckBoxes[Variant["side"]].Length
            ? DeckHalf.Outer : null;
    }

    // The deck takes layers the way a floor does (SidingFloorBlock.OnBlockInteractStart): infill on any
    // face, a frame-and-fill item's on the top or underside only, then a finish on the top or the underside. Anything unclaimed falls through, so planks on a
    // bare deck's top still reach PlaceWallFrame.
    private bool LayerDeck(
        IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel, SidingWallEntity entity,
        ItemSlot slot, AssetLocation heldCode, bool isCreative, DeckHalf half)
    {
        bool outer = half == DeckHalf.Outer;
        var (_, deckInfill, deckFront, deckBack, deckFrontStyle, deckBackStyle) = entity.DeckLayers(half);

        if (deckInfill == null)
        {
            string? infillKey = MatchConsumes(heldCode, Attributes["Infills"]);
            if (infillKey == null) return base.OnBlockInteractStart(world, byPlayer, blockSel);
            if (FramesOnFrom(heldCode, SidingFloorBlock.FinishFace(blockSel.Face) != null, Attributes["Framings"]))
                return base.OnBlockInteractStart(world, byPlayer, blockSel);

            if (ClaimRefuses(world, byPlayer, blockSel.Position)) return true;

            var consumes = ShareConsumes(Attributes["Infills"][infillKey]["Consumes"], LayerParts(Variant["layout"], "deckinfill"));
            if (!TryAffordOrError(byPlayer, isCreative, slot.StackSize, consumes)) return true;

            if (outer) entity.OuterDeckInfill = infillKey;
            else { entity.DeckInfill = infillKey; entity.LegacyDeck = false; }
            entity.MarkDirty(true);
            // Rooms only recompute on a chunk-dirty event, and the deck's retention just changed.
            world.BlockAccessor.ExchangeBlock(VentId(world, entity), blockSel.Position);
            SidingFloorBlock.MarkNeighboursDirty(world, blockSel.Position);
            ConsumeHeld(slot, consumes, isCreative);
            return true;
        }

        if (MatchConsumes(heldCode, Attributes["Finishes"], null, true) is { } tileKey)
        {
            if (ClaimRefuses(world, byPlayer, blockSel.Position)) return true;
            string? DeckStyle(string on) => on == "front" ? deckFrontStyle : deckBackStyle;
            var placed = FlatTilePlacement(byPlayer, blockSel, deckInfill, Attributes["Infills"], on => on == "front" ? deckFront : deckBack, DeckStyle);
            if (placed is { } laid)
                LayTiles(byPlayer, slot, isCreative, Attributes["Finishes"], tileKey, DeckStyle(laid.Face), laid.Slot,
                    style => SetDeckFinish(entity, laid.Face, SidingWallEntity.TilesKey, style, half));
            return true;
        }

        string? finishKey = MatchConsumes(heldCode, Attributes["Finishes"], slot.Itemstack!.Attributes);
        if (finishKey == null) return base.OnBlockInteractStart(world, byPlayer, blockSel);

        bool heldPlaces = slot.Itemstack!.Class == EnumItemClass.Block || MatchConsumes(heldCode, Attributes["Framings"]) != null;

        string? face = SidingFloorBlock.FinishFace(blockSel.Face);
        if (face == null)
        {
            if (heldPlaces) return base.OnBlockInteractStart(world, byPlayer, blockSel);
            (byPlayer as IServerPlayer)?.SendIngameError("vssiding:wrongface", Lang.Get("vssiding:build-floor-wrong-face"));
            return true;
        }

        if (ClaimRefuses(world, byPlayer, blockSel.Position)) return true;

        string? StyleFor(string on) => SidingModePicker.FinishChoices(byPlayer).FirstOrDefault(s => SidingFloorEntity.HasFloorStyle(Attributes["Finishes"][finishKey], on, s));
        string? style = StyleFor(face);

        string? currentKey = face == "front" ? deckFront : deckBack;
        string? currentStyle = face == "front" ? deckFrontStyle : deckBackStyle;
        if (currentKey != null)
        {
            if (style != null && currentKey == finishKey && currentStyle != style)
            {
                SetDeckFinish(entity, face, finishKey, style, half);
                return true;
            }

            // The far face takes the finish as a floor's does (decision 0075). A click on the deck is the deck's, so
            // planks take it too, whatever is picked; the wall's own top still stacks the next course.
            string? farFace = SidingFloorBlock.FinishFace(blockSel.Face.Opposite);
            if (farFace == null || (farFace == "front" ? deckFront : deckBack) != null)
            {
                if (heldPlaces) return base.OnBlockInteractStart(world, byPlayer, blockSel);
                (byPlayer as IServerPlayer)?.SendIngameError("vssiding:alreadyfinished", Lang.Get("vssiding:build-already-finished"));
                return true;
            }

            face = farFace;
            style = StyleFor(face);
        }

        var finishConsumes = ShareConsumes(Attributes["Finishes"][finishKey]["Consumes"], LayerParts(Variant["layout"], "deckfront"));
        if (!TryAffordOrError(byPlayer, isCreative, slot.StackSize, finishConsumes)) return true;

        SetDeckFinish(entity, face, finishKey, style, half);
        ConsumeHeld(slot, finishConsumes, isCreative);
        return true;
    }

    private static void SetDeckFinish(SidingWallEntity entity, string face, string finishKey, string? style, DeckHalf half)
    {
        if (half == DeckHalf.Outer)
        {
            if (face == "front") { entity.OuterDeckFront = finishKey; entity.OuterDeckFrontStyle = style; }
            else { entity.OuterDeckBack = finishKey; entity.OuterDeckBackStyle = style; }
        }
        else if (face == "front") { entity.DeckFront = finishKey; entity.DeckFrontStyle = style; }
        else { entity.DeckBack = finishKey; entity.DeckBackStyle = style; }
        entity.MarkDirty(true);
    }

    private void SetFinish(IWorldAccessor world, SidingWallEntity entity, BlockPos pos, string face, string finishKey, string? style)
    {
        int oldAbsorption = AbsorptionOf(entity);
        switch (face)
        {
            case "front": entity.Front = finishKey; entity.FrontStyle = style; break;
            case "secondfront": entity.SecondFront = finishKey; entity.SecondFrontStyle = style; break;
            case "secondback": entity.SecondBack = finishKey; entity.SecondBackStyle = style; break;
            default: entity.Back = finishKey; entity.BackStyle = style; break;
        }
        OnFinishChanged(world, entity, pos, oldAbsorption);
    }

    // Only a finish that lists a style can be asked for it, so a picked board style on daub or
    // brick falls back to the entry's default rather than naming an element its shape hasn't got.
    internal static bool HasStyle(JsonObject finish, string style)
        => Array.IndexOf(finish["Styles"].AsArray<string>([]) ?? [], style) >= 0;

    // A trapdoor infill, in the wall or either deck, puts the cell on the vent block, which Real Smoke tags porous (decision 0100).
    internal bool HasVent(SidingWallEntity entity)
        => IsVent(entity.Infill, Attributes["Infills"]) || IsVent(entity.DeckInfill, Attributes["Infills"]) || IsVent(entity.OuterDeckInfill, Attributes["Infills"]);

    internal static bool IsVent(string? infill, JsonObject infills) => infill != null && infills[infill]["Vent"].AsBool();

    internal int VentId(IWorldAccessor world, SidingWallEntity entity) => VentId(world, this, HasVent(entity));

    // The block with this one's code and variants, on the vent or the plain first part.
    internal static int VentId(IWorldAccessor world, Block block, bool vent)
    {
        string first = block.FirstCodePart();
        string plain = first.EndsWith("vent") ? first[..^4] : first;
        return world.GetBlock(new AssetLocation(block.Code.Domain, (vent ? plain + "vent" : plain) + block.Code.Path[first.Length..]))?.Id ?? block.Id;
    }

    internal void OnInfillChanged(IWorldAccessor world, SidingWallEntity entity, BlockPos pos, int oldAbsorption)
    {
        entity.MarkDirty(true);
        MarkAbsorptionChanged(world.BlockAccessor, pos, oldAbsorption);
        // Infill changes retention, but rooms only recompute on a chunk-dirty event; exchanging the block for itself fires one.
        world.BlockAccessor.ExchangeBlock(VentId(world, entity), pos);
        // It changes the liquid barrier too, and the block itself never changed, so the water
        // beside it has no idea. Without this a wall only starts damming once something else
        // nearby happens to make the neighbours recalculate - and only stops damming then too.
        world.BlockAccessor.TriggerNeighbourBlockUpdate(pos);
        MarkNeighboursDirty(world, pos);
    }

    // A finish changes neither retention nor the liquid barrier, only the light the wall absorbs.
    private void OnFinishChanged(IWorldAccessor world, SidingWallEntity entity, BlockPos pos, int oldAbsorption)
    {
        entity.MarkDirty(true);
        if (AbsorptionOf(entity) == oldAbsorption) return;
        MarkAbsorptionChanged(world.BlockAccessor, pos, oldAbsorption);
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
            // Only glazing merges sideways, and only at its ends, so those are the only
            // horizontal neighbours that can change what this cell draws - an opaque wall never
            // joins one - bar a corner's post ends, where it fills a T's slot whatever its fill (Tees).
            // Just this cell: merging is local, and the one thing drawn by a cell further
            // off, an inside corner (InnerCorners), is redrawn where a layer changes (MarkNeighboursDirty).
            if (neibpos.Y != pos.Y || Variant["layout"] == "diagonal") return;
            var entity = world.BlockAccessor.GetBlockEntity<SidingWallEntity>(pos);
            if (entity == null) return;
            // The wall across a front face decides whether a bare front draws closed.
            var fronts = FrontDirections(Variant["layout"], Variant["side"]);
            if (fronts.Any(direction => neibpos.Equals(pos.AddCopy(BlockFacing.FromCode(direction))))) entity.MarkDirty(true);
            var ends = Ends(Variant["layout"], Variant["side"]);
            if (!IsTransparent(entity.Infill, Attributes["Infills"])) ends = Variant["layout"] == "cornerout" ? ends[2..] : [];
            if (ends.Any(end => neibpos.Equals(pos.AddCopy(end.Points)))) entity.MarkDirty(true);
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
        // merged with it - by which point this cell is no longer glazed. No walk either way. The
        // cells diagonally across are told too: each frames its inside corner by this one (InnerCorners).
        if (world.BlockAccessor.GetBlock(pos) is not SidingWallBlock block) return;
        if (block.Variant["layout"] == "diagonal") MarkDiagonalRunDirty(world, pos, block.Variant["side"]);
        BlockFacing[] sideways = [.. Ends(block.Variant["layout"], block.Variant["side"]).Select(end => end.Points)];
        for (int dy = -1; dy <= 1; dy++)
        {
            foreach (var facing in sideways) world.BlockAccessor.GetBlockEntity<SidingWallEntity>(pos.AddCopy(facing).Add(0, dy, 0))?.MarkDirty(true);
        }
    }

    // A diagonal draws its end by whether the next cell carries the run on, and its inside corner there by
    // the cells above and below that one (InnerCorners). None of them is a face neighbour, so vanilla's
    // neighbour update reaches none of them.
    private static void MarkDiagonalRunDirty(IWorldAccessor world, BlockPos pos, string side)
    {
        var (left, right) = DiagonalRunNeighbours(side);
        for (int dy = -1; dy <= 1; dy++)
        {
            world.BlockAccessor.GetBlockEntity<SidingWallEntity>(pos.AddCopy(left).Add(0, dy, 0))?.MarkDirty(true);
            world.BlockAccessor.GetBlockEntity<SidingWallEntity>(pos.AddCopy(right).Add(0, dy, 0))?.MarkDirty(true);
        }
    }

    public override void OnBlockRemoved(IWorldAccessor world, BlockPos pos)
    {
        base.OnBlockRemoved(world, pos);
        if (Variant["layout"] == "diagonal") MarkDiagonalRunDirty(world, pos, Variant["side"]);
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

    // The directions a wall's or cornerout's front slots face: a diagonal's panel is not flush with its faces and a post has none.
    internal static string[] FrontDirections(string layout, string side) => layout switch
    {
        "wall" => [side],
        "cornerout" => [side, CorneroutSecondFace[side]],
        _ => [],
    };

    // Whether the wall across `direction` has a front slot facing back, so the two fronts lie against each other (decision 0083).
    internal static bool FrontsFaceEachOther(string layout, string side, string direction, string otherLayout, string otherSide)
        => FrontDirections(layout, side).Contains(direction) && FrontDirections(otherLayout, otherSide).Contains(Opposite(direction));

    // "front" for a click on a plain wall's end while its front is bare and another wall's front hides it (decision 0083).
    // A cornerout's leg ends share their faces with its back, which ResolveFinishFace answers first; its hidden front is
    // reached through the back's far face instead (decision 0075).
    internal string? HiddenBareFrontAtEnd(IBlockAccessor blockAccessor, BlockPos pos, BlockFacing clickedFace, SidingWallEntity entity)
    {
        string side = Variant["side"];
        if (Variant["layout"] != "wall" || !clickedFace.IsHorizontal || ResolveFinishFace("wall", side, clickedFace) != null || entity.Front != null) return null;
        return WallAt(blockAccessor, pos.AddCopy(BlockFacing.FromCode(side))) is var (wall, _)
            && FrontsFaceEachOther("wall", side, side, wall.Variant["layout"], wall.Variant["side"]) ? "front" : null;
    }

    // The faces this block's panels actually cover: the hugged side, plus a cornerout's or diagonal's second face.
    // An inside corner's post covers none.
    internal bool ClaimsFace(BlockFacing facing) => ClaimsFace(Variant["layout"], Variant["side"], facing.Code);

    internal static bool ClaimsFace(string layout, string side, string faceCode)
        => layout != "cornerin" && (faceCode == side || (ClaimsTwoFaces(layout) && faceCode == CorneroutSecondFace[side]));

    public override int GetRetention(BlockPos pos, BlockFacing facing, EnumRetentionType type)
        => RetentionOf(api.World.BlockAccessor.GetBlockEntity<SidingWallEntity>(pos), facing);

    internal int RetentionOf(SidingWallEntity? entity, BlockFacing facing)
    {
        if (facing == BlockFacing.UP) return DeckRetentionOf(entity);
        return ClaimsFace(facing) ? TraitsOf(Attributes, entity?.Framing, entity?.Infill).Retention : 0;
    }

    // Vanilla derives this from SideSolid, which is false on every face (decision 0002) so a thin
    // wall doesn't cull its neighbours - leaving every siding wall with a barrier of 0 and water
    // pouring through the fluid layer. A wall that seals air seals water too, so this asks exactly
    // what GetRetention asks. Glazing counts: it retains, so it dams, light notwithstanding.
    public override float GetLiquidBarrierHeightOnSide(BlockFacing face, BlockPos pos)
    {
        var entity = api.World.BlockAccessor.GetBlockEntity<SidingWallEntity>(pos);
        return ComputeLiquidBarrier(ClaimsFace(face), entity?.Framing, entity?.Infill, Attributes);
    }

    // Real Smoke's fire venting and vanilla's water edges ask this; a face that dams liquid is solid to them (decision 0100).
    // A vent Real Smoke tags porous is open to a fire's smoke on either face, so it is not solid to it.
    public override bool SideIsSolid(BlockPos pos, int faceIndex)
        => !Attributes.IsTrue("vssidingVentsSmoke") && GetLiquidBarrierHeightOnSide(BlockFacing.ALLFACES[faceIndex], pos) >= 1f;

    // Full height or nothing: a wall either dams its face or it doesn't. A cooling infill retains
    // negatively but still dams, so this asks whether retention is non-zero, not what sign it has.
    internal static float ComputeLiquidBarrier(bool claimed, string? framingKey, string? infillKey, JsonObject attributes)
        => claimed && TraitsOf(attributes, framingKey, infillKey).Retention != 0 ? 1f : 0f;

    // Another SideSolid consumer (decision 0020): with sidesolid off, nothing could be hung on any
    // siding wall. attachmentArea is ignored - a sealed face is solid across its whole 16x16.
    public override bool CanAttachBlockAt(IBlockAccessor blockAccessor, Block block, BlockPos pos, BlockFacing blockFace, Cuboidi? attachmentArea = null)
        => CanAttachOf(blockAccessor.GetBlockEntity<SidingWallEntity>(pos), blockFace);

    // A deck's underside takes hangers (decision 0094); a diagonal's deck covers half the cell, so it has none.
    internal bool CanAttachOf(SidingWallEntity? entity, BlockFacing blockFace)
    {
        if (blockFace == BlockFacing.UP) return DeckRetentionOf(entity) != 0;
        if (blockFace == BlockFacing.DOWN && entity?.Deck != null) return Variant["layout"] != "diagonal";
        return ClaimsFace(blockFace) && TraitsOf(Attributes, entity?.Framing, entity?.Infill).Retention != 0;
    }

    internal readonly record struct SealTraits(int Retention, bool Transparent, bool Porous);

    private static readonly ConditionalWeakTable<JsonObject, ConcurrentDictionary<(string?, string?), SealTraits>> TraitsCache = new();

    // What a claimed face of this framing and infill does, read once per block's Attributes object (the one AssetsFinalize sets).
    internal static SealTraits TraitsOf(JsonObject attributes, string? framing, string? infill)
        => TraitsCache.GetValue(attributes, static _ => new()).GetOrAdd((framing, infill), static (key, attributes) =>
        {
            JsonObject framings = attributes["Framings"], infills = attributes["Infills"];
            return new SealTraits(ComputeRetention(true, key.Item1, key.Item2, framings, infills), IsTransparent(key.Item2, infills), IsPorous(key.Item2, infills));
        }, attributes);

    private int DeckRetentionOf(SidingWallEntity? entity)
        => entity is { LegacyDeck: true, DeckInfill: null }
            ? ComputeDeckRetention(entity.Deck, null, true, Attributes["Framings"], Attributes["Infills"])
            : TraitsOf(Attributes, entity?.Deck, entity?.DeckInfill).Retention;

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
    internal bool DeckOccupied(IWorldAccessor world, BlockPos pos, DeckHalf half = DeckHalf.Inner)
        => world.GetIntersectingEntities(pos, half == DeckHalf.Outer ? OuterDeckBoxes[Variant["side"]] : DeckBoxes[(Variant["layout"], Variant["side"])], e => e.IsInteractable) is { Length: > 0 };

    // Whether the held framing could give this wall a deck now: used where a refusal must stay quiet (a floor run's end).
    internal bool CanTakeDeck(IWorldAccessor world, BlockPos pos, SidingWallEntity entity, AssetLocation heldCode, DeckHalf half)
        => entity.Framing != null && entity.DeckLayers(half).Deck == null
            && entity.Step == null && !DeckOccupied(world, pos, half)
            && MatchFraming(heldCode, FramesAsFilled(entity), Attributes["Framings"], Attributes["Infills"]) != null;

    // Adds a deck from the held framing, shared by a plank click on a side face and a floor laid up to the cell.
    // False when the wall has a deck or the held item is no framing for it; true once handled, refused or done.
    internal bool TryAddDeck(IWorldAccessor world, IPlayer byPlayer, BlockPos pos, SidingWallEntity entity, ItemSlot slot, AssetLocation heldCode, bool isCreative, DeckHalf half)
    {
        if (entity.DeckLayers(half).Deck != null || MatchFraming(heldCode, FramesAsFilled(entity), Attributes["Framings"], Attributes["Infills"]) is not { } deckKey) return false;

        if (ClaimRefuses(world, byPlayer, pos)) return true;

        if (entity.Step != null)
        {
            (byPlayer as IServerPlayer)?.SendIngameError("vssiding:stepped", Lang.Get("vssiding:build-stepped"));
            return true;
        }

        if (DeckOccupied(world, pos, half))
        {
            (byPlayer as IServerPlayer)?.SendIngameError("vssiding:occupied", Lang.Get("vssiding:build-occupied"));
            return true;
        }

        var deckConsumes = ShareConsumes(Attributes["Framings"][deckKey]["Consumes"], LayerParts(Variant["layout"], "deck"));
        if (!TryAffordOrError(byPlayer, isCreative, slot.StackSize, deckConsumes)) return true;

        SetDeck(world, entity, pos, deckKey, half);
        ConsumeHeld(slot, deckConsumes, isCreative);
        return true;
    }

    internal bool StepOccupied(IWorldAccessor world, BlockPos pos, string orientation)
    {
        var boxes = OpenStepBoxes(Variant["layout"], Variant["side"], orientation);
        return world.GetIntersectingEntities(pos, boxes, e => e.IsInteractable) is { Length: > 0 };
    }

    // A diagonal's claimed faces look out at the outer triangle; every other face, and every other layout, is the inner deck.
    internal DeckHalf HalfFacing(BlockFacing face) => HalfFacing(Variant["layout"], Variant["side"], face.Code);

    internal static DeckHalf HalfFacing(string layout, string side, string faceCode)
        => layout == "diagonal" && ClaimsFace(layout, side, faceCode) ? DeckHalf.Outer : DeckHalf.Inner;

    private void SetDeck(IWorldAccessor world, SidingWallEntity entity, BlockPos pos, string? deckKey, DeckHalf half = DeckHalf.Inner)
    {
        if (half == DeckHalf.Outer) entity.OuterDeck = deckKey;
        else { entity.Deck = deckKey; entity.LegacyDeck = false; }
        entity.MarkDirty(true);
        SidingFloorBlock.MarkNeighboursDirty(world, pos);
        world.BlockAccessor.ExchangeBlock(Id, pos);
    }

    // Looking at a built wall names its layers - otherwise a boarded infill is unreadable
    // short of breaking it, and the infill is what decides cellar vs warm room (decision 0015).
    // Takes a translate delegate (the entities pass LangEntry) so this needs no loaded Lang.
    // System.Func is spelled out throughout: Vintagestory.API.Common declares a Func of its own.
    internal static string Describe(
        string? framing, string? infill, string? deck, JsonObject framings, JsonObject infills,
        string layout, string side, string? front, string? secondFront, string? back, JsonObject finishes,
        System.Func<string, string?> translate, string? stepName = null,
        string? deckInfill = null, string? deckFront = null, string? deckBack = null, bool full = true,
        System.Func<string, bool>? buried = null, string? secondBack = null,
        string? frontStyle = null, string? secondFrontStyle = null, string? backStyle = null, string? secondBackStyle = null,
        string? deckFrontStyle = null, string? deckBackStyle = null)
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
                DescribeLayer(Installed(deckFront, finishes), finishes, "vssiding:tooltip-unfinished", translate, deckFrontStyle)));
            sb.AppendLine("    " + string.Format(Translate("vssiding:tooltip-underside", translate),
                DescribeLayer(Installed(deckBack, finishes), finishes, "vssiding:tooltip-unfinished", translate, deckBackStyle)));
        }
        if (stepName != null)
            sb.AppendLine("  " + string.Format(Translate("vssiding:tooltip-step", translate), stepName));
        foreach (string line in DescribeFaces(layout, side, front, secondFront, back, finishes, translate, buried, secondBack,
            frontStyle, secondFrontStyle, backStyle, secondBackStyle))
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
    // four finishable faces without inventing words for them. Directions carrying the same finish
    // share a line, grouped by first appearance, so a corner whose two backs match names them on
    // one line even with a SecondFront direction between them (decision 0090). A diagonal's two claimed
    // faces are one front, so its four directions come out as two lines. An unfinished front
    // flush against a solid block (buried) cannot be seen, so it is left out (decision 0075).
    private static IEnumerable<string> DescribeFaces(
        string layout, string side, string? front, string? secondFront, string? back, JsonObject finishes,
        System.Func<string, string?> translate, System.Func<string, bool>? buried = null, string? secondBack = null,
        string? frontStyle = null, string? secondFrontStyle = null, string? backStyle = null, string? secondBackStyle = null)
    {
        // A post's back faces look at the corner it stands in, so a bare one against a wall there is left out.
        if (layout == "cornerin")
        {
            var faces = PostFaces(side).Select(Opposite).Zip(new[] { (key: front, style: frontStyle), (key: secondFront, style: secondFrontStyle) })
                .Select(face => (face.First, face.Second.key, face.Second.style, back: false))
                .Concat(PostFaces(side).Zip(new[] { (key: back, style: backStyle), (key: secondBack, style: secondBackStyle) })
                    .Select(face => (face.First, face.Second.key, face.Second.style, back: true)));
            foreach (var (direction, key, style, isBack) in faces)
            {
                if (isBack && key == null && buried?.Invoke(direction) == true) continue;
                yield return $"{Translate($"game:facing-{direction}", translate)}: {DescribeLayer(Installed(key, finishes), finishes, "vssiding:tooltip-unfinished", translate, style)}";
            }
            yield break;
        }

        string? builtFront = Installed(front, finishes), builtBack = Installed(back, finishes);
        var directions = new List<(string direction, string? finish, string? style)> { (side, builtFront, frontStyle), (Opposite(side), builtBack, backStyle) };
        var fronts = new List<string> { side };
        if (ClaimsTwoFaces(layout))
        {
            string secondSide = CorneroutSecondFace[side];
            directions.Add((secondSide, layout == "diagonal" ? builtFront : Installed(secondFront, finishes), layout == "diagonal" ? frontStyle : secondFrontStyle));
            directions.Add((Opposite(secondSide), layout == "diagonal" ? builtBack : Installed(secondBack, finishes), layout == "diagonal" ? backStyle : secondBackStyle));
            fronts.Add(secondSide);
        }

        if (buried != null)
            directions.RemoveAll(d => d.finish == null && fronts.Contains(d.direction) && buried(d.direction));

        foreach (var group in directions.GroupBy(d => (d.finish, style: d.finish == SidingWallEntity.TilesKey ? d.style : null)))
        {
            string labels = string.Join(", ", group.Select(d => Translate($"game:facing-{d.direction}", translate)));
            yield return $"{labels}: {DescribeLayer(group.Key.finish, finishes, "vssiding:tooltip-unfinished", translate, group.Key.style)}";
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
    // A tiled face names each kind it holds with a count, in slot order (decision 0095).
    internal static string DescribeLayer(string? key, JsonObject materials, string missingLangKey, System.Func<string, string?> translate, string? style = null)
    {
        if (key == null) return Translate(missingLangKey, translate);
        if (key == SidingWallEntity.TilesKey)
        {
            var counts = SidingWallEntity.TileSlots(style).Where(kind => kind != "").GroupBy(kind => kind).ToList();
            if (counts.Count > 0)
                return string.Join(", ", counts.Select(kind => $"{kind.Count()} {DescribeLayer(kind.Key, materials, missingLangKey, translate)}"));
        }

        string? displayName = materials[key]["DisplayName"].AsString(null!);
        return (displayName != null ? translate(displayName) : null) ?? TitleCase(key);
    }

    // Our own keys ship in en.json beside the code, so a miss means a broken install: show the
    // raw key rather than dressing it up. A material's DisplayName is the case worth dressing up,
    // since a game update can add a wood the lang file has never heard of.
    internal static string Translate(string langKey, System.Func<string, string?> translate) => translate(langKey) ?? langKey;

    // The entities' translate delegate: the entry as written, or null where lang has none. Not Lang.GetIfExists,
    // which formats the entry with no arguments and logs an exception for every line that carries a {0}.
    internal static string? LangEntry(string key)
        => Lang.HasTranslation(key, findWildcarded: false, logErrors: false) ? Lang.GetUnformatted(key) : null;

    private static string TitleCase(string key)
        => string.Join(' ', key.Split('-').Select(word => word.Length == 0 ? word : char.ToUpperInvariant(word[0]) + word[1..]));

    public override int GetLightAbsorption(IBlockAccessor blockAccessor, BlockPos pos)
        => GetLightAbsorption(blockAccessor.GetChunkAtBlockPos(pos), pos);

    // lightAbsorption is 0 in wall.json so an open frame lets light through; a sealed wall
    // is opaque, or sunlight through it warms the room and cancels a cellar.
    public override int GetLightAbsorption(IWorldChunk chunk, BlockPos pos)
    {
        var entity = chunk?.GetLocalBlockEntityAtBlockPos(pos) as SidingWallEntity;
        return AbsorptionOf(entity);
    }

    internal int AbsorptionOf(SidingWallEntity? entity)
        => ComputeLightAbsorption(entity?.Framing, entity?.Infill, Attributes,
            IsFinished(Variant["layout"], entity?.Front, entity?.SecondFront, entity?.Back, entity?.SecondBack));

    // A wall's back or front covers its whole side; a cornerout's two outer faces and its two inner faces are
    // separate layers, so it takes both of either pair (decisions 0074, 0090).
    internal static bool IsFinished(string layout, string? front, string? secondFront, string? back, string? secondBack)
        => layout == "cornerout"
            ? (back != null && secondBack != null) || (front != null && secondFront != null)
            : back != null || front != null;

    // Sealing a room and blocking light are separate questions: glazing does the first and must
    // not do the second. An infill marked Transparent absorbs nothing, which also takes its cell
    // out of the room skylight patch (decision 0015), side AO (0016) and both 0018 patches, since
    // all of them ask this. An infill marked Porous passes light until a face is finished; the
    // default finished = true is what RoomSunlight and the thin floor rely on to keep their answers.
    internal static int ComputeLightAbsorption(string? framingKey, string? infillKey, JsonObject attributes, bool finished = true)
    {
        var traits = TraitsOf(attributes, framingKey, infillKey);
        return traits.Retention != 0 && !traits.Transparent && (finished || !traits.Porous) ? 99 : 0;
    }

    internal static bool IsPorous(string? infillKey, JsonObject infills)
        => infillKey != null && infills[infillKey]["Porous"].AsBool(false);

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

    // The relight skips when old and new absorption match, so the caller reads the old value before it edits the wall (decision 0034).
    internal void MarkAbsorptionChanged(IBlockAccessor accessor, BlockPos pos, int oldAbsorption)
        => accessor.MarkAbsorptionChanged(oldAbsorption, GetLightAbsorption(accessor, pos), pos);

    internal bool IsSealed(BlockEntity? be)
        => be is SidingWallEntity entity && AbsorptionOf(entity) > 0;

    // The horizontal step from a wall cell to the cell its dead space opens onto: away from the panel, diagonally for a cornerout.
    // A diagonal takes the same step, which is wrong for the triangle outside its panel (decision 0066).
    internal static (int dx, int dz) OpenSide(string layout, string side)
    {
        var open = BlockFacing.FromCode(side).Opposite.Normali;
        if (!ClaimsTwoFaces(layout)) return (open.X, open.Z);
        var second = BlockFacing.FromCode(CorneroutSecondFace[side]).Opposite.Normali;
        return (open.X + second.X, open.Z + second.Z);
    }

    // Nothing stands on the cell: its above holds air or a block that gives way to a placement, and no wall or guest wall.
    internal static bool FreeAbove(IBlockAccessor accessor, BlockPos pos)
    {
        BlockPos above = pos.UpCopy();
        Block block = accessor.GetBlock(above);
        return (block.BlockId == 0 || block.Replaceable >= 6000) && WallAt(accessor, above) == null;
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
        int absorption = ComputeLightAbsorption(entity.Framing, entity.Infill, wall.Attributes);
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

        DeckHalf? half = blockSel == null ? null : HitDeckHalf(accessor, blockSel, entity);
        return LayerMaterialAt(PeelAt(entity, hitFace, half), entity);
    }

    // The layer a hit peels. A hit on the deck's own box always peels the deck, in a floor's order
    // (DeckPeelLayer); any other hit takes the wall's order, and a deck it lands on is resolved the same way, faceless.
    internal string? PeelAt(SidingWallEntity entity, BlockFacing? hitFace, DeckHalf? deckHit)
    {
        string? deckFace = hitFace == null ? null : SidingFloorBlock.FinishFace(hitFace);
        if (deckHit is { } half)
            return half == DeckHalf.Outer ? OuterPeelLayer(entity, deckFace)
                : DeckPeelLayer(deckFace, entity.DeckInfill, entity.DeckFront, entity.DeckBack);

        string? face = hitFace == null ? null : ResolveFinishFace(Variant["layout"], Variant["side"], hitFace);
        // An outer face hit takes the outer deck as a room-side hit takes the inner; a faceless one takes it once the inner is gone (decision 0096).
        if (entity.OuterDeck != null && ((hitFace != null && HalfFacing(hitFace) == DeckHalf.Outer) || (face == null && entity.Deck == null)))
            return OuterPeelLayer(entity, null);
        string? layer = PeelLayer(face, entity.Infill, entity.Front, entity.SecondFront, entity.Back, entity.Deck, entity.Step, entity.SecondBack);
        if (layer == null && entity.OuterDeck != null) return OuterPeelLayer(entity, null);
        return layer == "deck" ? DeckPeelLayer(null, entity.DeckInfill, entity.DeckFront, entity.DeckBack) : layer;
    }

    private static string OuterPeelLayer(SidingWallEntity entity, string? deckFace)
        => "outer" + DeckPeelLayer(deckFace, entity.OuterDeckInfill, entity.OuterDeckFront, entity.OuterDeckBack);

    private static string? KeyAt(string? layer, SidingWallEntity entity)
    {
        var deck = entity.DeckLayers(HalfOf(layer));
        return LayerKey(BareLayer(layer), entity.Infill, entity.Front, entity.SecondFront, entity.Back, deck.Deck, entity.Step, deck.Infill, deck.Front, deck.Back, entity.SecondBack);
    }

    internal static string? BareLayer(string? layer) => layer?.StartsWith("outer") == true ? layer["outer".Length..] : layer;

    internal static DeckHalf HalfOf(string? layer) => layer?.StartsWith("outer") == true ? DeckHalf.Outer : DeckHalf.Inner;

    private EnumBlockMaterial LayerMaterialAt(string? layer, SidingWallEntity entity)
    {
        if (layer == "step" && entity.Step != null)
            return api?.World.GetBlock(new AssetLocation(entity.Step))?.BlockMaterial ?? BlockMaterial;
        return LayerMaterial(BareLayer(layer), KeyAt(layer, entity), entity.Framing, Attributes["Framings"], Attributes["Infills"], Attributes["Finishes"], BlockMaterial);
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
        string? layer = PeelAt(entity, null, null);
        if (layer?.Contains("deck") != true || LayerMaterialAt(layer, entity) == EnumBlockMaterial.Wood) return layer;
        // An inner deck that does not burn leaves the outer deck's top layer next in line.
        string? outer = entity.OuterDeck == null ? null : OuterPeelLayer(entity, null);
        if (outer != null && outer != layer && LayerMaterialAt(outer, entity) == EnumBlockMaterial.Wood) return outer;
        return PeelLayer(null, entity.Infill, entity.Front, entity.SecondFront, entity.Back, null, entity.Step, entity.SecondBack) ?? layer;
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
        DeckHalf? hitHalf = entity != null && hitFace != null ? HitDeckHalf(world.BlockAccessor, selection!, entity) : null;
        bool deckHit = hitHalf != null;

        // A rug on the deck comes off before any layer, as on a floor (decision 0051): vanilla only
        // breaks decor first in survival.
        if (deckHit && world.BlockAccessor.GetDecor(pos, new DecorBits(hitFace!)) != null)
        {
            world.BlockAccessor.BreakDecor(pos, hitFace);
            return;
        }

        string? layer = entity == null ? null : PeelAt(entity, hitFace, hitHalf);
        if (entity == null || byPlayer == null || layer == null)
        {
            base.OnBlockBroken(world, pos, byPlayer, dropQuantityMultiplier);
            return;
        }

        int tileSlot = TileBreakSlot(entity, layer, selection?.Position.Equals(pos) == true ? selection.HitPosition : null);
        if (world.Side == EnumAppSide.Server && byPlayer.WorldData.CurrentGameMode != EnumGameMode.Creative)
        {
            string? key = KeyAt(layer, entity);
            var drops = new List<BlockDropItemStack>();
            if (tileSlot >= 0)
            {
                AddDrops(drops, SidingWallEntity.TileSlots(StyleOn(entity, layer))[tileSlot], Attributes["Finishes"]);
            }
            else if (layer == "step")
            {
                var stepDrop = StepDrop(key);
                if (stepDrop != null) drops.Add(stepDrop);
            }
            else
            {
                AddDrops(drops, key, Attributes[BareLayer(layer) switch { "infill" or "deckinfill" => "Infills", "deck" => "Framings", _ => "Finishes" }],
                    LayerParts(Variant["layout"], layer));
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
        if (tileSlot >= 0) RemoveTile(world, entity, pos, layer, tileSlot);
        else RemoveLayer(world, entity, pos, layer);
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
        int oldAbsorption = AbsorptionOf(entity);
        switch (layer)
        {
            case "front": entity.Front = null; entity.FrontStyle = null; break;
            case "secondfront": entity.SecondFront = null; entity.SecondFrontStyle = null; break;
            case "back": entity.Back = null; entity.BackStyle = null; break;
            case "secondback": entity.SecondBack = null; entity.SecondBackStyle = null; break;
            case "deckfront": entity.DeckFront = null; entity.DeckFrontStyle = null; break;
            case "deckback": entity.DeckBack = null; entity.DeckBackStyle = null; break;
            case "deckinfill":
                entity.DeckInfill = null;
                entity.MarkDirty(true);
                // Rooms only recompute on a chunk-dirty event, and the deck's retention just changed.
                world.BlockAccessor.ExchangeBlock(VentId(world, entity), pos);
                SidingFloorBlock.MarkNeighboursDirty(world, pos);
                // Decor only goes on a sealed top, so a rug left on the bare joists comes off with the infill.
                world.BlockAccessor.BreakDecor(pos, BlockFacing.UP);
                return;
            case "outerdeckfront": entity.OuterDeckFront = null; entity.OuterDeckFrontStyle = null; break;
            case "outerdeckback": entity.OuterDeckBack = null; entity.OuterDeckBackStyle = null; break;
            case "outerdeckinfill":
                entity.OuterDeckInfill = null;
                entity.MarkDirty(true);
                world.BlockAccessor.ExchangeBlock(VentId(world, entity), pos);
                SidingFloorBlock.MarkNeighboursDirty(world, pos);
                return;
            case "deck": SetDeck(world, entity, pos, null); return;
            case "outerdeck": SetDeck(world, entity, pos, null, DeckHalf.Outer); return;
            case "step": entity.Step = null; entity.StepOrientation = null; entity.MarkDirty(true); return;
            default:
                entity.Infill = null;
                OnInfillChanged(world, entity, pos, oldAbsorption);
                return;
        }
        OnFinishChanged(world, entity, pos, oldAbsorption);
    }

    public override ItemStack[] GetDrops(IWorldAccessor world, BlockPos pos, IPlayer byPlayer, float dropQuantityMultiplier)
    {
        var drops = DropsOf(world.BlockAccessor.GetBlockEntity<SidingWallEntity>(pos));

        // Nothing built yet - fall back to the base drops so HorizontalOrientable still
        // hands back the placed block.
        if (drops.Count == 0) return base.GetDrops(world, pos, byPlayer, dropQuantityMultiplier);

        return ResolveDrops(world, drops, dropQuantityMultiplier);
    }

    internal List<BlockDropItemStack> DropsOf(SidingWallEntity? entity) => ComputeDrops(
        entity?.Framing, entity?.Infill, entity?.Front, entity?.SecondFront, entity?.Back, entity?.Deck,
        Attributes["Framings"], Attributes["Infills"], Attributes["Finishes"], entity?.Step,
        entity?.DeckInfill, entity?.DeckFront, entity?.DeckBack, Variant["layout"], entity?.SecondBack, TileLists(entity),
        entity?.OuterDeck, entity?.OuterDeckInfill, entity?.OuterDeckFront, entity?.OuterDeckBack);

    // The style of every face whose finish is tiles, for the tiles a break with no player drops (decision 0095).
    private static string?[] TileLists(SidingWallEntity? entity) => entity == null ? [] :
        TileStyles((entity.Front, entity.FrontStyle), (entity.SecondFront, entity.SecondFrontStyle), (entity.Back, entity.BackStyle),
            (entity.SecondBack, entity.SecondBackStyle), (entity.DeckFront, entity.DeckFrontStyle), (entity.DeckBack, entity.DeckBackStyle),
            (entity.OuterDeckFront, entity.OuterDeckFrontStyle), (entity.OuterDeckBack, entity.OuterDeckBackStyle));

    internal static string?[] TileStyles(params (string? Key, string? Style)[] faces)
        => [.. faces.Where(face => face.Key == SidingWallEntity.TilesKey).Select(face => face.Style)];

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
        string? deckInfill = null, string? deckFront = null, string? deckBack = null, string? layout = null, string? secondBack = null, string?[]? tileLists = null,
        string? outerDeck = null, string? outerDeckInfill = null, string? outerDeckFront = null, string? outerDeckBack = null)
    {
        var drops = new List<BlockDropItemStack>();
        foreach (string[] kinds in (tileLists ?? []).Select(SidingWallEntity.TileSlots))
            foreach (string kind in kinds) AddDrops(drops, kind == "" ? null : kind, finishes);
        AddDrops(drops, framing, framings, LayerParts(layout, "framing"));
        AddDrops(drops, infill, infills);
        AddDrops(drops, front, finishes, LayerParts(layout, "front"));
        AddDrops(drops, secondFront, finishes, LayerParts(layout, "secondfront"));
        AddDrops(drops, back, finishes, LayerParts(layout, "back"));
        AddDrops(drops, secondBack, finishes, LayerParts(layout, "secondback"));
        AddDrops(drops, deck, framings, LayerParts(layout, "deck"));
        AddDrops(drops, deckInfill, infills, LayerParts(layout, "deckinfill"));
        AddDrops(drops, deckFront, finishes, LayerParts(layout, "deckfront"));
        AddDrops(drops, deckBack, finishes, LayerParts(layout, "deckback"));
        AddDrops(drops, outerDeck, framings, LayerParts(layout, "outerdeck"));
        AddDrops(drops, outerDeckInfill, infills, LayerParts(layout, "outerdeckinfill"));
        AddDrops(drops, outerDeckFront, finishes, LayerParts(layout, "outerdeckfront"));
        AddDrops(drops, outerDeckBack, finishes, LayerParts(layout, "outerdeckback"));
        var stepDrop = StepDrop(step);
        if (stepDrop != null) drops.Add(stepDrop);
        return drops;
    }

    internal static void AddDrops(List<BlockDropItemStack> drops, string? key, JsonObject dictionary, int parts = 1)
    {
        if (key == null) return;
        var entry = dictionary[key];
        if (!entry.Exists) return;

        foreach (var drop in entry["Drops"].AsObject(Array.Empty<BlockDropItemStack>()))
        {
            if (drop.Code == null) continue;
            if (parts > 1) drop.Quantity = NatFloat.createUniform(Share((int)drop.Quantity.avg, parts), 0);
            drops.Add(drop);
        }
    }

    private static BlockDropItemStack? StepDrop(string? step)
        => step == null ? null : new BlockDropItemStack { Type = EnumItemClass.Block, Code = new AssetLocation(step), Quantity = NatFloat.One };

    // Same layer names PeelLayer returns, resolved to the material key installed there.
    internal static string? LayerKey(string? layer, string? infill, string? front, string? secondFront, string? back, string? deck, string? step = null,
        string? deckInfill = null, string? deckFront = null, string? deckBack = null, string? secondBack = null)
        => layer switch
        {
            "front" => front,
            "secondfront" => secondFront,
            "back" => back,
            "secondback" => secondBack,
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
    // and otherwise it goes after the front finishes and before the backs.
    internal static string? PeelLayer(string? face, string? infill, string? front, string? secondFront, string? back, string? deck, string? step = null, string? secondBack = null)
    {
        if ((deck != null || step != null) && face is null or "back" or "secondback") return deck != null ? "deck" : "step";
        string? hit = face switch { "front" => front, "secondfront" => secondFront, "back" => back, "secondback" => secondBack, _ => null };
        if (hit != null) return face;
        if (front != null) return "front";
        if (secondFront != null) return "secondfront";
        if (deck != null) return "deck";
        if (step != null) return "step";
        if (back != null) return "back";
        if (secondBack != null) return "secondback";
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

    // A post takes no infill, so an item that is both a framing and an infill frames it as it would a filled wall.
    private bool FramesAsFilled(SidingWallEntity entity) => entity.Infill != null || Variant["layout"] == "cornerin";

    // An item that is both a framing and an infill, as sticks are, fills a bare frame rather than
    // decking or cornering it; a filled wall has no infill left to take, so there it frames.
    internal static string? MatchFraming(AssetLocation heldCode, bool filled, JsonObject framings, JsonObject infills)
        => !filled && MatchConsumes(heldCode, infills) != null ? null : MatchConsumes(heldCode, framings);

    // A frame-and-fill item fills a bare cell only from a face that takes a finish; any other face frames on (decision 0089).
    internal static bool FramesOnFrom(AssetLocation heldCode, bool finishFace, JsonObject framings)
        => !finishFace && MatchConsumes(heldCode, framings) != null;

    // The lang key for the line telling which faces fill a bare frame and which frame on, for an item that does both (decision 0089).
    internal static string? FillHintKey(string layout, AssetLocation? held, JsonObject framings, JsonObject infills)
        => held == null || MatchConsumes(held, framings) == null || MatchConsumes(held, infills) == null ? null : layout switch
        {
            "wall" => "vssiding:tooltip-fill-hint-wall",
            "cornerout" or "diagonal" => "vssiding:tooltip-fill-hint-corner",
            "floor" => "vssiding:tooltip-fill-hint-floor",
            _ => null
        };

    internal static void AppendFillHint(StringBuilder dsc, IPlayer forPlayer, string layout, JsonObject framings, JsonObject infills)
    {
        if (!HasBuildSignal(forPlayer)) return;
        string? key = FillHintKey(layout, forPlayer.InventoryManager.ActiveHotbarSlot?.Itemstack?.Collectible.Code, framings, infills);
        if (key != null) dsc.AppendLine("  " + Translate(key, LangEntry));
    }

    // Finds the material dictionary entry whose Consumes.code matches the held item, so a
    // build-flow behavior can turn "the player right-clicked with plank-oak" into "oak". An entry
    // with Consumes.attributes also needs the held stack to carry them, which is what tells one
    // dyed plank from another under a single block code (decision 0068).
    internal static string? MatchConsumes(AssetLocation heldCode, JsonObject materials, ITreeAttribute? heldAttributes = null, bool tile = false)
    {
        if (!materials.Exists) return null;

        foreach (var keyToken in materials)
        {
            string key = keyToken.AsString()!;
            if (materials[key]["Tile"].AsBool() != tile) continue;
            var consumes = materials[key]["Consumes"];
            if (!consumes.Exists) continue;

            string? code = consumes["code"].AsString(null!);
            if (code == null) continue;

            if (!WildcardUtil.Match(new AssetLocation(code), heldCode)) continue;

            var wanted = consumes["attributes"];
            if (wanted.Exists && (heldAttributes == null || wanted.ToAttribute() is not ITreeAttribute tree || !Carries(heldAttributes, tree))) continue;

            return key;
        }

        return null;
    }

    // Every wanted key is in the held tree with an equal value, at every depth. Vanilla's
    // TreeAttribute.IsSubSetOf turns the test round one level down, held inside wanted, and casts the
    // held value to a tree unchecked; a dyed plank's types sit exactly one level down.
    internal static bool Carries(ITreeAttribute held, ITreeAttribute wanted)
    {
        foreach (var (key, wantedValue) in wanted)
        {
            IAttribute? heldValue = held[key];
            if (wantedValue is ITreeAttribute wantedTree)
            {
                if (heldValue is not ITreeAttribute heldTree || !Carries(heldTree, wantedTree)) return false;
            }
            else if (heldValue == null || !wantedValue.Equals(null!, heldValue)) return false;
        }
        return true;
    }

    internal static int ConsumeQuantity(JsonObject consumes) => consumes["quantity"].AsInt(1);

    // A post is a quarter of a wall's cross-section and a corner's inner face about half a wall's run, so each
    // costs and refunds that share of the full amount, rounded up (decisions 0082, 0090).
    internal static int Share(int quantity, int parts) => Math.Max(1, (quantity + parts - 1) / parts);

    internal static int PostQuarter(int quantity) => Share(quantity, 4);

    // How many parts of a wall's amount a layer costs and refunds one of; a deck is laid in full, but a diagonal's
    // covers one triangle of its cell, so half (decision 0096).
    internal static int LayerParts(string? layout, string? layer) => (layout, layer) switch
    {
        ("diagonal", not null) when layer.Contains("deck") => 2,
        (_, not null) when layer.Contains("deck") => 1,
        ("cornerin", _) => 4,
        ("cornerout", "back" or "secondback") => 2,
        _ => 1,
    };

    internal static JsonObject ShareConsumes(JsonObject consumes, int parts)
        => new(new JObject { ["quantity"] = Share(ConsumeQuantity(consumes), parts) });

    // Which finish layer a build-flow click's clicked face targets - the hugged side is
    // "front", the opposite side is "back", an end/top/bottom face is neither. A cornerout's
    // second leg has its own layers on both sides, "secondfront" and "secondback" (decision 0090).
    // A diagonal's second claimed face is the same "front": the panel has one outer face.
    internal static string? ResolveFinishFace(string layout, string side, BlockFacing clickedFace)
    {
        // A post's two fronts look away from the corner it fills, and its two backs into it (decision 0082).
        if (layout == "cornerin")
        {
            if (clickedFace == BlockFacing.FromCode(side).Opposite) return "front";
            if (clickedFace == BlockFacing.FromCode(CorneroutSecondFace[side]).Opposite) return "secondfront";
            if (clickedFace == BlockFacing.FromCode(side)) return "back";
            if (clickedFace == BlockFacing.FromCode(CorneroutSecondFace[side])) return "secondback";
            return null;
        }
        if (FinishFaceFor(side, clickedFace, "front") is string face) return face;
        if (!ClaimsTwoFaces(layout)) return null;
        bool diagonal = layout == "diagonal";
        return FinishFaceFor(CorneroutSecondFace[side], clickedFace, diagonal ? "front" : "secondfront", diagonal ? "back" : "secondback");
    }

    // A click hosts furniture on a wall's open side, which a corner's two backs face; a post's open side is its cell, which either front faces.
    internal static bool HostsClick(string layout, string side, BlockFacing clickedFace)
        => ResolveFinishFace(layout, side, clickedFace) is { } face && (layout == "cornerin" ? face is "front" or "secondfront" : face is "back" or "secondback");

    internal static string? FinishOn(SidingWallEntity entity, string face)
        => face switch { "front" => entity.Front, "secondfront" => entity.SecondFront, "secondback" => entity.SecondBack, _ => entity.Back };

    internal static string? StyleOn(SidingWallEntity entity, string face)
        => face switch
        {
            "front" => entity.FrontStyle, "secondfront" => entity.SecondFrontStyle, "secondback" => entity.SecondBackStyle,
            "deckfront" => entity.DeckFrontStyle, "deckback" => entity.DeckBackStyle,
            "outerdeckfront" => entity.OuterDeckFrontStyle, "outerdeckback" => entity.OuterDeckBackStyle, _ => entity.BackStyle
        };

    // A finish that mirrors on corners, laid beside itself across a corner's edge, takes the turn its partner face has not, whatever
    // is picked (decision 0090): boards draws the pattern a quarter turn, and hboards or no style draws it as it lies (decision 0086).
    internal static string? MirroredStyle(string layout, string face, SidingWallEntity entity, JsonObject finishes, string finishKey)
    {
        string partner = face.StartsWith("second") ? face["second".Length..] : "second" + face;
        if (layout != "cornerout" || !finishes[finishKey]["MirrorsOnCorners"].AsBool() || FinishOn(entity, partner) != finishKey) return null;
        return StyleOn(entity, partner) == "boards" ? "hboards" : "boards";
    }

    // The two faces a post stands against; it claims none, but a hosted block shifts away from both.
    internal static string[] PostFaces(string side) => [side, CorneroutSecondFace[side]];

    private static string? FinishFaceFor(string side, BlockFacing clickedFace, string frontLayer, string backLayer = "back")
    {
        if (clickedFace.Code == side) return frontLayer;
        if (clickedFace == BlockFacing.FromCode(side).Opposite) return backLayer;
        return null;
    }
}
