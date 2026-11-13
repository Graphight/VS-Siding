using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;

namespace VSSiding.Tests;

// Every uv in wall.json and cornerout.json derives from the element's box, per decision 0021.
// Each face reads two of the three axes - north/south (x, y), east/west (z, y), up/down (x, z) -
// and Flat pins that pair at 0. Positional instead maps v to the wall's height (16 - y), so painted
// courses land on the stepped lips and carry across stacked walls; up/down stay Flat either way.
// The weatherboard groups are Positional too, so a lap's grain is sampled where the lap sits and
// the sixteen laps of a block read as sixteen different boards (decision 0028).
// The opaque infill groups are Positional too, so the slivers filling a dropped plate continue the
// panel's texture across a stacked join (decision 0041).
// RunAxis names the axis a group runs along. A box cut out of a longer one then samples the texture
// at its own position on that axis, so a course split into segments keeps one continuous strip.
// Three cases need more: the flat board groups rotate their outward face 90 to stand plank grain upright,
// back-logs position-maps only its outward face, and infill-pane draws two of the six faces.
public enum UvRule
{
    Flat,
    Positional,
}

public record Element(
    string Name,
    (double X, double Y, double Z) From,
    (double X, double Y, double Z) To,
    string Slot,
    UvRule UvRule,
    string[]? Faces = null,
    string[]? PositionalOverrides = null,
    string[]? RotatedFaces = null,
    char? RunAxis = null,
    Element? ClippedFrom = null,
    double RotationY = 0,
    (double X, double Y, double Z)? RotationOrigin = null,
    (double X, double Y, double Z) Offset = default,
    Dictionary<string, FaceUv>? Explicit = null,
    Dictionary<string, string>? FaceSlots = null);

// A face's uv rect and rotation exactly as a vanilla shape writes them, for the step sets copied from vanilla's stairs.
public record FaceUv(double U0, double V0, double U1, double V1, int Rotation = 0);

public static class WallShapeGen
{
    // How far the mortar sits behind the unit faces. If it does not read at walking distance,
    // this is the number to turn.
    private const double MortarDepth = 0.25;

    private static (double X, double Y, double Z) Corner(char depthAxis, double depth, double run, double y)
        => depthAxis == 'x' ? (depth, y, run) : (run, y, depth);

    // Masonry does not lap, so neither cladding profile transfers; what it has instead is a grid,
    // and the painted grid already sits on the voxel grid, so the model can agree with the paint
    // exactly. One plane set back by MortarDepth carries the mortar and unit lips stand proud of
    // it on the block bound - a lip ends where the plane begins, so IsBuried culls its inward face.
    // `outer` is the face the wall shows and `inner` the depth it is cut back to, so a back-slot
    // group is the same table with the two swapped. Units are laid out against the wall's own
    // 0..16 grid and then clipped to the run, because a leg that starts at 1 still has to put its
    // joints where the texture paints them.
    private static IEnumerable<Element> RunningBond(
        string name, string slot, double coursePitch, double unitWidth,
        char depthAxis, double outer, double inner, double runLo, double runHi, bool flipBond = false)
    {
        char runAxis = depthAxis == 'x' ? 'z' : 'x';
        double lip = outer + Math.Sign(inner - outer) * MortarDepth;
        (double Lo, double Hi) MinMax(double a, double b) => (Math.Min(a, b), Math.Max(a, b));

        var (planeLo, planeHi) = MinMax(lip, inner);
        yield return new Element(name,
            Corner(depthAxis, planeLo, runLo, 0), Corner(depthAxis, planeHi, runHi, 16),
            slot, UvRule.Positional, RunAxis: runAxis);

        var (lipLo, lipHi) = MinMax(outer, lip);
        for (double y = 0; y < 16; y += coursePitch)
        {
            // The mortar line is the bottom voxel of a course, and the joints step half a unit
            // every other course - that is the running bond the texture draws. Which course gets
            // the offset is the texture's business, not the bond's: brick starts offset and ashlar
            // starts flush, so a shared flip would only move the error around.
            double offset = ((y / coursePitch) % 2 == 0) != flipBond ? unitWidth / 2 : 0;
            for (double u = offset - unitWidth; u < 16; u += unitWidth)
            {
                double lo = Math.Max(u, runLo), hi = Math.Min(u + unitWidth - 1, runHi);
                if (hi <= lo) continue;
                yield return new Element(name,
                    Corner(depthAxis, lipLo, lo, y + 1), Corner(depthAxis, lipHi, hi, y + coursePitch),
                    slot, UvRule.Positional, RunAxis: runAxis);
            }
        }
    }

    // Cobblestone and drystone paint no grid at all - row and column means across
    // stone/cobblestone/*.png and stone/drystone/*.png are flat, so there is nothing for modelled
    // joints to agree with. Decision 0022 hit this with irregular shake joints and the answer
    // holds: no modelled joints, vary depth only, and let the step carry the texture without
    // claiming to be a joint. Cells abut with no gaps, because a gap opens a line into the cavity.
    // Depths are literal, never randomised, or the golden test goes flaky.
    private static readonly double[][] RubbleDepths =
    [
        [0.2, 0, 0.3, 0.1],
        [0, 0.25, 0.1, 0.3],
        [0.3, 0.1, 0.2, 0],
        [0.15, 0.3, 0, 0.2],
    ];

    // The cell boundaries move row to row for the same reason the shakes' do - four identical
    // columns would read as one vertical seam.
    private static readonly double[][] RubbleCuts =
    [
        [0, 5, 9, 13, 16],
        [0, 4, 7, 12, 16],
        [0, 6, 10, 13, 16],
        [0, 3, 8, 12, 16],
    ];

    private static IEnumerable<Element> RubbleGrid(
        string name, string slot, char depthAxis, double outer, double inner, double runLo, double runHi)
    {
        char runAxis = depthAxis == 'x' ? 'z' : 'x';
        double dir = Math.Sign(inner - outer);
        for (int row = 0; row < RubbleDepths.Length; row++)
        {
            var cuts = RubbleCuts[row];
            for (int cell = 0; cell < cuts.Length - 1; cell++)
            {
                double lo = Math.Max(cuts[cell], runLo), hi = Math.Min(cuts[cell + 1], runHi);
                if (hi <= lo) continue;
                double face = outer + dir * RubbleDepths[row][cell];
                yield return new Element(name,
                    Corner(depthAxis, Math.Min(face, inner), lo, row * 4),
                    Corner(depthAxis, Math.Max(face, inner), hi, row * 4 + 4),
                    slot, UvRule.Positional, RunAxis: runAxis);
            }
        }
    }

    // A log face is a backing plane on the inner half, then four proud bands on the outer half with
    // a one-voxel reveal between, where the plane shows through. As with RunningBond, a back group is
    // the same call with outer and inner swapped, and a cornerout leg narrows runLo/runHi.
    private static IEnumerable<Element> Logs(
        string name, string slot, char depthAxis, double outer, double inner, string outwardFace,
        double runLo, double runHi)
    {
        (double Lo, double Hi) MinMax(double a, double b) => (Math.Min(a, b), Math.Max(a, b));
        double mid = (outer + inner) / 2;

        var (planeLo, planeHi) = MinMax(inner, mid);
        yield return new Element(name,
            Corner(depthAxis, planeLo, runLo, 0), Corner(depthAxis, planeHi, runHi, 16), slot, UvRule.Flat);

        var (bandLo, bandHi) = MinMax(mid, outer);
        for (int i = 0; i < 4; i++)
        {
            double y0 = 0.5 + i * 4, y1 = 3.5 + i * 4;
            yield return new Element(name,
                Corner(depthAxis, bandLo, runLo, y0), Corner(depthAxis, bandHi, runHi, y1),
                slot, UvRule.Flat, PositionalOverrides: [outwardFace]);
        }
    }

    // Mirror a relief table across the wall's depth (0..4 on x or z), so back-shakes reads the same
    // irregular joints as the front without a second hand-authored table.
    private static Element MirrorDepthX(Element e, string name, string slot) => e with
    {
        Name = name,
        Slot = slot,
        From = (4 - e.To.X, e.From.Y, e.From.Z),
        To = (4 - e.From.X, e.To.Y, e.To.Z),
    };

    private static Element MirrorDepthZ(Element e, string name, string slot) => e with
    {
        Name = name,
        Slot = slot,
        From = (e.From.X, e.From.Y, 4 - e.To.Z),
        To = (e.To.X, e.To.Y, 4 - e.From.Z),
    };

    // Crops a mirrored cornerout table to one leg's run, as RunningBond's runLo/runHi do.
    private static IEnumerable<Element> ClipRun(IEnumerable<Element> elements, char runAxis, double runLo, double runHi)
    {
        foreach (var e in elements)
        {
            double lo = runAxis == 'x' ? e.From.X : e.From.Z;
            double hi = runAxis == 'x' ? e.To.X : e.To.Z;
            double clo = Math.Max(lo, runLo), chi = Math.Min(hi, runHi);
            if (chi <= clo) continue;
            yield return e with
            {
                From = runAxis == 'x' ? (clo, e.From.Y, e.From.Z) : (e.From.X, e.From.Y, clo),
                To = runAxis == 'x' ? (chi, e.To.Y, e.To.Z) : (e.To.X, e.To.Y, chi),
            };
        }
    }

    private static readonly string[] AllFaces = ["north", "east", "south", "west", "up", "down"];

    // Weatherboard's lap (proud butt, recessed body) stays, but each course is cut into four
    // shakes along its run, and a shake keeps its depth through both bands - butt at d, body at
    // 0.5 + d - so it reads as one tile rather than a banded strip. Depths run 0 to 0.3 of a
    // voxel, recessed never proud, and the boundaries move course to course for the stagger.
    // The painted joints are irregular, so the shakes abut and the depth step alone carries the
    // joint; a gap would open a line straight into the wall.
    private static readonly Element[] FrontShakesElements =
    [
        new("front-shakes", (0, 0, 0), (1, 1, 5), "front", UvRule.Positional, RunAxis: 'z'),
        new("front-shakes", (0.2, 0, 5), (1, 1, 9), "front", UvRule.Positional, RunAxis: 'z'),
        new("front-shakes", (0.1, 0, 9), (1, 1, 13), "front", UvRule.Positional, RunAxis: 'z'),
        new("front-shakes", (0.3, 0, 13), (1, 1, 16), "front", UvRule.Positional, RunAxis: 'z'),
        new("front-shakes", (0.5, 1, 0), (1, 4, 5), "front", UvRule.Positional, RunAxis: 'z'),
        new("front-shakes", (0.7, 1, 5), (1, 4, 9), "front", UvRule.Positional, RunAxis: 'z'),
        new("front-shakes", (0.6, 1, 9), (1, 4, 13), "front", UvRule.Positional, RunAxis: 'z'),
        new("front-shakes", (0.8, 1, 13), (1, 4, 16), "front", UvRule.Positional, RunAxis: 'z'),
        new("front-shakes", (0.15, 4, 0), (1, 5, 4), "front", UvRule.Positional, RunAxis: 'z'),
        new("front-shakes", (0, 4, 4), (1, 5, 7), "front", UvRule.Positional, RunAxis: 'z'),
        new("front-shakes", (0.3, 4, 7), (1, 5, 12), "front", UvRule.Positional, RunAxis: 'z'),
        new("front-shakes", (0.1, 4, 12), (1, 5, 16), "front", UvRule.Positional, RunAxis: 'z'),
        new("front-shakes", (0.65, 5, 0), (1, 8, 4), "front", UvRule.Positional, RunAxis: 'z'),
        new("front-shakes", (0.5, 5, 4), (1, 8, 7), "front", UvRule.Positional, RunAxis: 'z'),
        new("front-shakes", (0.8, 5, 7), (1, 8, 12), "front", UvRule.Positional, RunAxis: 'z'),
        new("front-shakes", (0.6, 5, 12), (1, 8, 16), "front", UvRule.Positional, RunAxis: 'z'),
        new("front-shakes", (0.25, 8, 0), (1, 9, 6), "front", UvRule.Positional, RunAxis: 'z'),
        new("front-shakes", (0.1, 8, 6), (1, 9, 10), "front", UvRule.Positional, RunAxis: 'z'),
        new("front-shakes", (0, 8, 10), (1, 9, 13), "front", UvRule.Positional, RunAxis: 'z'),
        new("front-shakes", (0.2, 8, 13), (1, 9, 16), "front", UvRule.Positional, RunAxis: 'z'),
        new("front-shakes", (0.75, 9, 0), (1, 12, 6), "front", UvRule.Positional, RunAxis: 'z'),
        new("front-shakes", (0.6, 9, 6), (1, 12, 10), "front", UvRule.Positional, RunAxis: 'z'),
        new("front-shakes", (0.5, 9, 10), (1, 12, 13), "front", UvRule.Positional, RunAxis: 'z'),
        new("front-shakes", (0.7, 9, 13), (1, 12, 16), "front", UvRule.Positional, RunAxis: 'z'),
        new("front-shakes", (0.1, 12, 0), (1, 13, 3), "front", UvRule.Positional, RunAxis: 'z'),
        new("front-shakes", (0.3, 12, 3), (1, 13, 8), "front", UvRule.Positional, RunAxis: 'z'),
        new("front-shakes", (0.15, 12, 8), (1, 13, 12), "front", UvRule.Positional, RunAxis: 'z'),
        new("front-shakes", (0, 12, 12), (1, 13, 16), "front", UvRule.Positional, RunAxis: 'z'),
        new("front-shakes", (0.6, 13, 0), (1, 16, 3), "front", UvRule.Positional, RunAxis: 'z'),
        new("front-shakes", (0.8, 13, 3), (1, 16, 8), "front", UvRule.Positional, RunAxis: 'z'),
        new("front-shakes", (0.65, 13, 8), (1, 16, 12), "front", UvRule.Positional, RunAxis: 'z'),
        new("front-shakes", (0.5, 13, 12), (1, 16, 16), "front", UvRule.Positional, RunAxis: 'z'),
    ];

    private static readonly Element[] WallPoleElements =
    [
        // The rough frame: posts a quarter voxel inside the frame's thickness, cut into three lengths with
        // the middle one swelling inward, stubs standing out beside them, and plates that keep the plain
        // box and run over the post through cheeks in the quarter-voxel skins. A cheek instead of one
        // proud box keeps two same-facing faces from sharing the plane at a run's end. The plates' own
        // north and south faces sit behind the posts, so they are left out.
        new("poles-left", (1.25, 0, 0), (2.75, 5, 1), "framing", UvRule.Flat),
        new("poles-left", (1.25, 5, 0), (2.75, 11, 1.25), "framing", UvRule.Flat),
        new("poles-left", (1.25, 11, 0), (2.75, 16, 1), "framing", UvRule.Flat),
        new("poles-left", (1, 7, 1), (1.5, 8, 2.5), "framing", UvRule.Flat),
        new("poles-left", (2.5, 12.5, 1), (3, 13.5, 2), "framing", UvRule.Flat),
        new("poles-right", (1.25, 0, 15), (2.75, 3, 16), "framing", UvRule.Flat),
        new("poles-right", (1.25, 3, 14.75), (2.75, 9, 16), "framing", UvRule.Flat),
        new("poles-right", (1.25, 9, 15), (2.75, 16, 16), "framing", UvRule.Flat),
        new("poles-right", (2.5, 6.5, 13.5), (3, 7.5, 15), "framing", UvRule.Flat),
        new("poles-right", (1, 10.5, 14), (1.5, 11.5, 15), "framing", UvRule.Flat),
        new("poles-top", (1, 15, 1), (3, 16, 15), "framing", UvRule.Flat, Faces: ["west", "east", "up", "down"]),
        new("poles-top", (1, 15, 0), (1.25, 16, 1), "framing", UvRule.Flat),
        new("poles-top", (2.75, 15, 0), (3, 16, 1), "framing", UvRule.Flat),
        new("poles-top", (1, 15, 15), (1.25, 16, 16), "framing", UvRule.Flat),
        new("poles-top", (2.75, 15, 15), (3, 16, 16), "framing", UvRule.Flat),
        new("poles-top", (1, 13.5, 0), (1.25, 15, 1.25), "lashing", UvRule.Flat),
        new("poles-top", (2.75, 13.5, 0), (3, 15, 1.25), "lashing", UvRule.Flat),
        new("poles-top", (1, 13.5, 14.75), (1.25, 15, 16), "lashing", UvRule.Flat),
        new("poles-top", (2.75, 13.5, 14.75), (3, 15, 16), "lashing", UvRule.Flat),
        new("poles-bottom", (1, 0, 1), (3, 1, 15), "framing", UvRule.Flat, Faces: ["west", "east", "up", "down"]),
        new("poles-bottom", (1, 0, 0), (1.25, 1, 1), "framing", UvRule.Flat),
        new("poles-bottom", (2.75, 0, 0), (3, 1, 1), "framing", UvRule.Flat),
        new("poles-bottom", (1, 0, 15), (1.25, 1, 16), "framing", UvRule.Flat),
        new("poles-bottom", (2.75, 0, 15), (3, 1, 16), "framing", UvRule.Flat),
        new("poles-bottom", (1, 1, 0), (1.25, 2.5, 1.25), "lashing", UvRule.Flat),
        new("poles-bottom", (2.75, 1, 0), (3, 2.5, 1.25), "lashing", UvRule.Flat),
        new("poles-bottom", (1, 1, 14.75), (1.25, 2.5, 16), "lashing", UvRule.Flat),
        new("poles-bottom", (2.75, 1, 14.75), (3, 2.5, 16), "lashing", UvRule.Flat),
    ];

    private static readonly Element[] WallPole2Elements =
    [
        // Second variant: cuts, stubs and their sides move, the plates are the first one's renamed.
        new("poles2-left", (1.25, 0, 0), (2.75, 4, 1), "framing", UvRule.Flat),
        new("poles2-left", (1.25, 4, 0), (2.75, 12, 1.25), "framing", UvRule.Flat),
        new("poles2-left", (1.25, 12, 0), (2.75, 16, 1), "framing", UvRule.Flat),
        new("poles2-left", (2.5, 8, 1), (3, 9, 2.5), "framing", UvRule.Flat),
        new("poles2-left", (1, 6, 1), (1.5, 7, 2), "framing", UvRule.Flat),
        new("poles2-right", (1.25, 0, 15), (2.75, 6, 16), "framing", UvRule.Flat),
        new("poles2-right", (1.25, 6, 14.75), (2.75, 13, 16), "framing", UvRule.Flat),
        new("poles2-right", (1.25, 13, 15), (2.75, 16, 16), "framing", UvRule.Flat),
        new("poles2-right", (1, 3, 13.5), (1.5, 4, 15), "framing", UvRule.Flat),
        new("poles2-right", (2.5, 7.5, 13.5), (3, 8.5, 14.75), "framing", UvRule.Flat),
        .. WallPoleElements.Where(e => e.Name is "poles-top" or "poles-bottom").Select(e => e with { Name = e.Name.Replace("poles", "poles2") }),
    ];

    // A bamboo pole standing in for a plain member's box: a core a quarter voxel inside it across `depth`, a ridge down
    // each of its two depth faces so the seam reads, and a thin ring all round it at each of `rings` along `run`. Across, the
    // ring stands a quarter voxel proud of the member's box, into the next cell where the member lies on the cell's side, so
    // the core reaches the edge and meets the next cell's member with no gap. A ring never reaches through the cell's top or
    // bottom, where a deck's cap or the next wall up sits flush. On depth everything stays inside the box, so a finish still
    // covers it, and a ring stops a hair inside, off the planes of a plain member, tee or finish the next cell draws where
    // the ring reaches. The ridge stands at 0.1, off the ring's plane; the ring tables keep off the lashings, the stems and
    // each other, which the plane and ring tests check.
    private static IEnumerable<Element> BambooPole(
        string name, (double X, double Y, double Z) from, (double X, double Y, double Z) to, char depth, char run, double[] rings,
        string[]? coreFaces = null, double ridgeInset = 0.25)
    {
        double[] lo = [from.X, from.Y, from.Z], hi = [to.X, to.Y, to.Z];
        int d = depth - 'x', r = run - 'x', o = 3 - d - r;
        Element Box(double depthLo, double depthHi, double runLo, double runHi, double acrossLo, double acrossHi, string[]? faces = null)
        {
            double[] a = new double[3], b = new double[3];
            (a[d], b[d], a[r], b[r], a[o], b[o]) = (depthLo, depthHi, runLo, runHi, acrossLo, acrossHi);
            return new Element(name, (a[0], a[1], a[2]), (b[0], b[1], b[2]), "framing", UvRule.Flat, Faces: faces);
        }

        yield return Box(lo[d] + 0.25, hi[d] - 0.25, lo[r], hi[r], lo[o], hi[o], coreFaces);
        foreach (var (near, far) in new[] { (lo[d] + 0.1, lo[d] + 0.25), (hi[d] - 0.25, hi[d] - 0.1) })
            yield return Box(near, far, lo[r], hi[r], lo[o] + ridgeInset, hi[o] - ridgeInset);
        // A hair short of the cell's top or bottom, so the ring never shares the plane of the core reaching it.
        bool vertical = o == 1;
        double ringLo = vertical && lo[o] == 0 ? 0.01 : lo[o] - 0.25, ringHi = vertical && hi[o] == 16 ? 15.99 : hi[o] + 0.25;
        foreach (double ring in rings)
            yield return Box(lo[d] + 0.01, hi[d] - 0.01, ring, ring + 0.5, ringLo, ringHi);
    }

    // Where each variant rings its members: the uprights' heights, clear of the plates and lashings, the plates' places
    // along the run, which start past a corner's post at 3, and the post's heights. Even cells take `bamboo`, odd `bamboo2`.
    // An upright's ring reaches into the next cell, where that cell's upright may ring the same height, so the four upright
    // tables keep half a voxel apart.
    private record BambooRings(string Prefix, double[] Left, double[] Right, double[] Top, double[] Bottom, double[] Post);

    private static readonly BambooRings[] BambooVariants =
    [
        new("bamboo", [5.25, 11.25], [4.25, 10.75], [3.25, 7.75, 12.25], [3.75, 6.75, 11.25], [4.25, 10.25]),
        new("bamboo2", [6.25, 12.25], [7.25, 12.75], [3.75, 8.25, 13.25], [3.75, 9.25, 13.75], [6.75, 12.25]),
    ];

    private static readonly string[] BambooPlateFaces = ["west", "east", "up", "down"];

    // The wall's frame in bamboo: posts left and right, plates top and bottom, and the rough frame's lashings where a plate
    // crosses an upright.
    private static IEnumerable<Element> BambooWall(BambooRings v) =>
    [
        .. BambooPole($"{v.Prefix}-left", (1, 0, 0), (3, 16, 1), 'x', 'y', v.Left),
        .. BambooPole($"{v.Prefix}-right", (1, 0, 15), (3, 16, 16), 'x', 'y', v.Right),
        .. BambooPole($"{v.Prefix}-top", (1, 15, 1), (3, 16, 15), 'x', 'z', v.Top, BambooPlateFaces),
        .. BambooPole($"{v.Prefix}-bottom", (1, 0, 1), (3, 1, 15), 'x', 'z', v.Bottom, BambooPlateFaces),
        .. WallPoleElements.Where(e => e.Slot == "lashing").Select(e => e with { Name = e.Name.Replace("poles", v.Prefix) }),
    ];

    private static readonly Element[] WallBambooElements = [.. BambooVariants.SelectMany(BambooWall)];

    // Bamboo poles standing side by side, two voxels wide on the odd voxels from z 1, in the wall's frame: a core in the
    // infill's depth, a ridge down each face so the seams read, and two nodes a pole at heights staggered pole to pole.
    // Ridges and nodes sit at 1.3, 1.15, 2.7 and 2.85, off the planes of the pole frames' stubs, lashings and joists. The
    // poles are cut to z runLo..runHi and y y0..y1, which is how a join's sliver and a corner's leg take their part.
    private static IEnumerable<Element> Stems(string name, double runLo, double runHi, double y0, double y1)
    {
        for (int k = (int)Math.Floor((runLo - 1) / 2); 1 + 2 * k < runHi; k++)
        {
            double z = 1 + 2 * k;
            var (low, high) = StemNodes[((k % 8) + 8) % 8];
            (double X1, double Y1, double Z1, double X2, double Y2, double Z2)[] boxes =
            [
                (1.5, 0, z, 2.5, 16, z + 2),
                (1.3, 0, z + 0.4, 1.5, 16, z + 1.6),
                (2.5, 0, z + 0.4, 2.7, 16, z + 1.6),
                .. new[] { low, high }.SelectMany(n => new[] { (1.15, n, z + 0.2, 1.5, n + 0.5, z + 1.8), (2.5, n, z + 0.2, 2.85, n + 0.5, z + 1.8) }),
            ];
            foreach (var b in boxes)
            {
                double lo = Math.Max(b.Z1, runLo), hi = Math.Min(b.Z2, runHi), bottom = Math.Max(b.Y1, y0), top = Math.Min(b.Y2, y1);
                if (hi > lo && top > bottom)
                    yield return new Element(name, (b.X1, bottom, lo), (b.X2, top, hi), "infill", UvRule.Flat, RunAxis: 'z');
            }
        }
    }

    // The nodes of pole k mod 8, a quarter voxel off the grid: no plane of a node meets a frame's or a floor joist's.
    private static readonly (double Low, double High)[] StemNodes =
        [(3.75, 9.25), (6.25, 11.75), (2.25, 7.75), (9.75, 13.25), (3.75, 11.25), (7.75, 13.25), (2.25, 9.75), (6.25, 11.25)];

    // A wall's stems at the infill's three heights, the join slivers above and below the panel.
    private static IEnumerable<Element> StemBands(double runLo, double runHi)
        => [.. Stems("stems-top", runLo, runHi, 15, 16), .. Stems("stems", runLo, runHi, 1, 15), .. Stems("stems-bottom", runLo, runHi, 0, 1)];

    // A corner's second leg runs along x with its depth on z.
    private static Element SwapXZ(Element e)
        => e with { From = (e.From.Z, e.From.Y, e.From.X), To = (e.To.Z, e.To.Y, e.To.X), RunAxis = 'x' };

    // A floor lays the wall's stems flat: the wall's depth is the floor's height and its height the floor's x, so the
    // poles lie east-west across the joists; the top and underside turn the texture to run along them.
    private static Element OnFloor(Element e)
        => e with { From = (e.From.Y, e.From.X + 12, e.From.Z), To = (e.To.Y, e.To.X + 12, e.To.Z), RotatedFaces = ["up", "down"] };

    // Bark is the flat slab horizontal boards are, and its two styles differ in texture alone
    // (decision 0063), so their groups are the hboards rows under other names.
    private static IEnumerable<Element> WithBark(params Element[] hboards)
        => hboards.Concat(new[] { "bark", "hbark" }.SelectMany(style => hboards.Select(e => e with { Name = e.Name.Replace("hboards", style) })));

    private static FaceUv F(double u0, double v0, double u1, double v1, int rotation = 0) => new(u0, v0, u1, v1, rotation);

    // The wall step as the vanilla stair it continues, in the stair's own frame (vanilla rotateY 0: the upper
    // tread toward z 0), so a step and a stair share every seam. The wall's panel takes x 0..4 ("left") or
    // x 12..16 ("right"), and the step is vanilla's box clipped to the rest. Boxes and uvs are transcribed from
    // block/basic/stairs/{planks-free-up,planks-free-down,normal,flipped,stonepath-stairs-free}.json; a face
    // vanilla disables is left out. The stone path's two rows differ from its file, each noted on the row.
    // A corner takes the same clip plus one on z: "front" keeps z 4..16 (the other leg's panel at z 0..4, the end
    // the stair rises toward), "back" keeps z 0..12 (that panel at z 12..16).
    private static IEnumerable<Element> StepSetElements(bool corner = false)
    {
        var sources = new (string Family, string Vertical, string Part, (double, double, double) From, (double, double, double) To, Dictionary<string, FaceUv> Faces)[]
        {
            ("planks", "up", "base", (0, 0, 0), (16, 8, 16), new()
            {
                ["north"] = F(0, 8, 16, 16), ["east"] = F(0, 2.5, 16, 10.5), ["south"] = F(0, 2.5, 16, 10.5),
                ["west"] = F(0, 8, 16, 16), ["up"] = F(0, 0, 16, 16), ["down"] = F(0, 0, 16, 16),
            }),
            ("planks", "up", "step", (0, 8, 0), (16, 16, 8), new()
            {
                ["north"] = F(0, 0, 16, 8), ["east"] = F(0, 0, 8, 8), ["south"] = F(0, 0, 16, 8),
                ["west"] = F(0, 0, 8, 8), ["up"] = F(0, 0, 16, 8, 180),
            }),
            ("planks", "down", "base", (0, 8, 0), (16, 16, 16), new()
            {
                ["north"] = F(0, 5.5, 16, 13.5), ["east"] = F(0, 0, 16, 8), ["south"] = F(0, 5.5, 16, 13.5),
                ["west"] = F(0, 5.5, 16, 13.5), ["up"] = F(0, 0, 16, 16), ["down"] = F(0, 0, 16, 16),
            }),
            ("planks", "down", "step", (0, 0, 0), (16, 8, 8), new()
            {
                ["north"] = F(0, 3, 16, 11), ["east"] = F(2.5, 3, 10.5, 11), ["south"] = F(0, 0, 16, 8),
                ["west"] = F(0, 3, 8, 11), ["down"] = F(0, 0, 16, 8),
            }),
            ("basic", "up", "base", (0, 0, 0), (16, 8, 16), new()
            {
                ["north"] = F(0, 0, 16, 8), ["east"] = F(0, 0, 16, 8), ["south"] = F(0, 0, 16, 8),
                ["west"] = F(0, 0, 16, 8), ["up"] = F(0, 0, 16, 16, 180), ["down"] = F(0, 0, 16, 16),
            }),
            ("basic", "up", "step", (0, 8, 0), (16, 16, 8), new()
            {
                ["north"] = F(0, 0, 16, 8), ["east"] = F(0, 0, 8, 8), ["south"] = F(0, 0, 16, 8),
                ["west"] = F(0, 0, 8, 8), ["up"] = F(0, 0, 16, 8, 180),
            }),
            ("basic", "down", "base", (0, 8, 0), (16, 16, 16), new()
            {
                ["north"] = F(0, 0, 16, 8), ["east"] = F(0, 0, 16, 8), ["south"] = F(0, 0, 16, 8),
                ["west"] = F(0, 0, 16, 8), ["up"] = F(0, 0, 16, 16, 180), ["down"] = F(0, 0, 16, 16),
            }),
            ("basic", "down", "step", (0, 0, 0), (16, 8, 8), new()
            {
                ["north"] = F(0, 0, 16, 8), ["east"] = F(0, 0, 8, 8), ["south"] = F(0, 0, 16, 8),
                ["west"] = F(0, 0, 8, 8), ["down"] = F(0, 0, 16, 8),
            }),
            // Vanilla's base runs y -1..7 with a 0.01 overlap; it is cut to the cell at y 0, so each side's uv loses its bottom texel row.
            ("stonepath", "up", "base", (0, 0, 0), (16, 7, 16), new()
            {
                ["north"] = F(0, 7.5, 16, 14.5), ["east"] = F(0, 8, 16, 15), ["south"] = F(0, 8, 16, 15),
                ["west"] = F(0, 8, 16, 15), ["up"] = F(0, 0, 16, 16), ["down"] = F(0, 0, 16, 16),
            }),
            // The tread is Cube3, a child of the base: y 8..16 drawn from the base's y -1, so y 7..15 in the cell.
            ("stonepath", "up", "step", (0, 7, 0), (16, 15, 8), new()
            {
                ["north"] = F(0, 0, 16, 8), ["east"] = F(8, 0, 16, 8), ["south"] = F(0, 0, 16, 8),
                ["west"] = F(0, 0, 8, 8), ["up"] = F(0, 0, 16, 8), ["down"] = F(0, 0, 16, 8),
            }),
        };

        (string? End, double Lo, double Hi)[] ends = corner ? [("front", 4, 16), ("back", 0, 12)] : [(null, 0, 16)];
        foreach (var (family, vertical, part, from, to, faces) in sources)
        {
            var source = new Element("", from, to, "step", UvRule.Flat, Faces: [.. faces.Keys], Explicit: faces);
            foreach (var (hand, xLo, xHi) in new[] { ("left", 4.0, 16.0), ("right", 0.0, 12.0) })
            {
                foreach (var (end, zLo, zHi) in ends)
                {
                    yield return source with
                    {
                        Name = $"step-{family}-{vertical}-{hand}{(end == null ? "" : "-" + end)}-{part}",
                        From = (xLo, from.Item2, Math.Max(from.Item3, zLo)),
                        To = (xHi, to.Item2, Math.Min(to.Item3, zHi)),
                        ClippedFrom = source,
                    };
                }
            }
        }
    }

    private static readonly Element[] WallElements =
    [
        new("front", (0, 0, 0), (1, 16, 16), "front", UvRule.Flat),
        new("framing-top", (1, 15, 1), (3, 16, 15), "framing", UvRule.Flat),
        new("framing-bottom", (1, 0, 1), (3, 1, 15), "framing", UvRule.Flat),
        new("framing-left", (1, 0, 0), (3, 16, 1), "framing", UvRule.Flat),
        new("framing-right", (1, 0, 15), (3, 16, 16), "framing", UvRule.Flat),
        .. WallPoleElements,
        .. WallPole2Elements,
        .. WallBambooElements,
        new("infill-top", (1.5, 15, 1), (2.5, 16, 15), "infill", UvRule.Positional),
        new("infill", (1.5, 1, 1), (2.5, 15, 15), "infill", UvRule.Positional),
        new("infill-bottom", (1.5, 0, 1), (2.5, 1, 15), "infill", UvRule.Positional),
        .. StemBands(1, 15),
        new("infill-pane", (2, 0, 0), (2, 16, 16), "infill", UvRule.Flat, Faces: ["west", "east"]),
        // A bare front facing another wall's front is closed in the infill (decision 0083).
        new("infill-front", (0, 0, 0), (1, 16, 16), "infill", UvRule.Positional),
        // A glazed cell's frame: stiles, the rails between them, and a filler in each corner for a rail
        // whose stile has merged away. A rail's ends always stand against another piece, so those
        // faces are left out; a filler keeps all six, since at an L's inside corner it stands alone.
        new("glazing-left", (1, 0, 0), (3, 16, 2), "framing", UvRule.Flat),
        new("glazing-right", (1, 0, 14), (3, 16, 16), "framing", UvRule.Flat),
        new("glazing-top", (1, 14, 2), (3, 16, 14), "framing", UvRule.Flat, Faces: ["west", "east", "up", "down"]),
        new("glazing-bottom", (1, 0, 2), (3, 2, 14), "framing", UvRule.Flat, Faces: ["west", "east", "up", "down"]),
        new("glazing-top-left", (1, 14, 0), (3, 16, 2), "framing", UvRule.Flat),
        new("glazing-top-right", (1, 14, 14), (3, 16, 16), "framing", UvRule.Flat),
        new("glazing-bottom-left", (1, 0, 0), (3, 2, 2), "framing", UvRule.Flat),
        new("glazing-bottom-right", (1, 0, 14), (3, 2, 16), "framing", UvRule.Flat),
        new("back", (3, 0, 0), (4, 16, 16), "back", UvRule.Flat),
        .. StepSetElements(),
        new("back-boards", (3, 0, 0), (4, 16, 16), "back", UvRule.Flat, RotatedFaces: ["east"]),
        new("front-boards", (0, 0, 0), (1, 16, 16), "front", UvRule.Flat, RotatedFaces: ["west"]),
        .. WithBark(
            new("back-hboards", (3, 0, 0), (4, 16, 16), "back", UvRule.Flat),
            new("front-hboards", (0, 0, 0), (1, 16, 16), "front", UvRule.Flat)),
        new("front-weatherboard", (0, 0, 0), (1, 1, 16), "front", UvRule.Positional),
        new("front-weatherboard", (0.25, 1, 0), (1, 2, 16), "front", UvRule.Positional),
        new("front-weatherboard", (0.5, 2, 0), (1, 3, 16), "front", UvRule.Positional),
        new("front-weatherboard", (0.75, 3, 0), (1, 4, 16), "front", UvRule.Positional),
        new("front-weatherboard", (0, 4, 0), (1, 5, 16), "front", UvRule.Positional),
        new("front-weatherboard", (0.25, 5, 0), (1, 6, 16), "front", UvRule.Positional),
        new("front-weatherboard", (0.5, 6, 0), (1, 7, 16), "front", UvRule.Positional),
        new("front-weatherboard", (0.75, 7, 0), (1, 8, 16), "front", UvRule.Positional),
        new("front-weatherboard", (0, 8, 0), (1, 9, 16), "front", UvRule.Positional),
        new("front-weatherboard", (0.25, 9, 0), (1, 10, 16), "front", UvRule.Positional),
        new("front-weatherboard", (0.5, 10, 0), (1, 11, 16), "front", UvRule.Positional),
        new("front-weatherboard", (0.75, 11, 0), (1, 12, 16), "front", UvRule.Positional),
        new("front-weatherboard", (0, 12, 0), (1, 13, 16), "front", UvRule.Positional),
        new("front-weatherboard", (0.25, 13, 0), (1, 14, 16), "front", UvRule.Positional),
        new("front-weatherboard", (0.5, 14, 0), (1, 15, 16), "front", UvRule.Positional),
        new("front-weatherboard", (0.75, 15, 0), (1, 16, 16), "front", UvRule.Positional),
        new("back-weatherboard", (3, 0, 0), (4, 1, 16), "back", UvRule.Positional),
        new("back-weatherboard", (3, 1, 0), (3.75, 2, 16), "back", UvRule.Positional),
        new("back-weatherboard", (3, 2, 0), (3.5, 3, 16), "back", UvRule.Positional),
        new("back-weatherboard", (3, 3, 0), (3.25, 4, 16), "back", UvRule.Positional),
        new("back-weatherboard", (3, 4, 0), (4, 5, 16), "back", UvRule.Positional),
        new("back-weatherboard", (3, 5, 0), (3.75, 6, 16), "back", UvRule.Positional),
        new("back-weatherboard", (3, 6, 0), (3.5, 7, 16), "back", UvRule.Positional),
        new("back-weatherboard", (3, 7, 0), (3.25, 8, 16), "back", UvRule.Positional),
        new("back-weatherboard", (3, 8, 0), (4, 9, 16), "back", UvRule.Positional),
        new("back-weatherboard", (3, 9, 0), (3.75, 10, 16), "back", UvRule.Positional),
        new("back-weatherboard", (3, 10, 0), (3.5, 11, 16), "back", UvRule.Positional),
        new("back-weatherboard", (3, 11, 0), (3.25, 12, 16), "back", UvRule.Positional),
        new("back-weatherboard", (3, 12, 0), (4, 13, 16), "back", UvRule.Positional),
        new("back-weatherboard", (3, 13, 0), (3.75, 14, 16), "back", UvRule.Positional),
        new("back-weatherboard", (3, 14, 0), (3.5, 15, 16), "back", UvRule.Positional),
        new("back-weatherboard", (3, 15, 0), (3.25, 16, 16), "back", UvRule.Positional),
        .. FrontShakesElements,
        .. Logs("back-logs", "back", 'x', 4, 3, "east", 0, 16),
        .. Logs("front-logs", "front", 'x', 0, 1, "west", 0, 16),
        .. FrontShakesElements.Select(e => MirrorDepthX(e, "back-shakes", "back")),
        // clay/brick/four/running/cream1.png is 32px over a 16-voxel face: mortar rows at px 6-7,
        // 14-15, 22-23 and 30-31 - a course every 4 voxels with one voxel of mortar at its foot -
        // and two joints per course over 8-voxel units, with the bottom course offset.
        .. RunningBond("front-brick", "front", 4, 8, 'x', 0, 1, 0, 16),
        // stone/brick/{rock}1.png draws one vertical joint per course, not two: a single trough
        // at px 16 on the upper course and px 31 on the lower, flat everywhere else, across
        // andesite, granite, basalt, limestone and sandstone alike. So its units are 16 voxels
        // wide on an 8-voxel course, and the offset course is the upper one - the opposite phase
        // to brick.
        .. RunningBond("front-ashlar", "front", 8, 16, 'x', 0, 1, 0, 16, flipBond: true),
        // The room side is the same table with outer and inner swapped, so the units stand proud
        // toward x 4 instead of x 0. Without it a brick partition is flat on the face you live
        // beside while the elevation it backs onto has relief.
        .. RunningBond("back-brick", "back", 4, 8, 'x', 4, 3, 0, 16),
        .. RunningBond("back-ashlar", "back", 8, 16, 'x', 4, 3, 0, 16, flipBond: true),
        .. RubbleGrid("front-rubble", "front", 'x', 0, 1, 0, 16),
        .. RubbleGrid("back-rubble", "back", 'x', 4, 3, 0, 16),
    ];

    // A finish slab cut into the four slots a tile fills, each its own group `<slab>-tile<n>` in its own texture slot
    // `<slab slot>tile<n>`, n = row * 2 + col. Row 0 is the lower half on rowAxis and col 0 the lower on colAxis, cut at 8
    // in the cell whatever part of it the slab covers. ClippedFrom keeps each piece sampling its own part of the uv the
    // slab would have across the whole cell, and a face that lies against the next tile is left out. A squeezed face, a
    // post's 4 wide, samples a whole tile across the slab's own width instead of the part of one the grid gives it.
    internal static IEnumerable<Element> TilePieces(Element slab, char rowAxis, char colAxis, string? squeezedFace = null)
    {
        var cell = squeezedFace == null
            ? slab with { From = WithAxis(WithAxis(slab.From, rowAxis, 0), colAxis, 0), To = WithAxis(WithAxis(slab.To, rowAxis, 16), colAxis, 16) }
            : slab with { From = WithAxis(slab.From, rowAxis, 0), To = WithAxis(slab.To, rowAxis, 16), Explicit = Only(squeezedFace, F(0, 0, 8, 16)) };
        for (int row = 0; row < 2; row++)
        for (int col = 0; col < 2; col++)
        {
            var from = slab.From;
            var to = slab.To;
            var faces = (slab.Faces ?? AllFaces).AsEnumerable();
            bool empty = false;
            foreach (var (axis, high) in new[] { (rowAxis, row == 1), (colAxis, col == 1) })
            {
                double lo = Axis(slab.From, axis), hi = Axis(slab.To, axis);
                if (high) lo = Math.Max(lo, 8); else hi = Math.Min(hi, 8);
                if (hi <= lo) { empty = true; break; }
                var (loFace, hiFace) = axis switch { 'x' => ("west", "east"), 'y' => ("down", "up"), _ => ("north", "south") };
                if (high && lo > Axis(slab.From, axis)) faces = faces.Where(f => f != loFace);
                if (!high && hi < Axis(slab.To, axis)) faces = faces.Where(f => f != hiFace);
                from = WithAxis(from, axis, lo);
                to = WithAxis(to, axis, hi);
            }
            if (empty) continue;

            yield return slab with
            {
                Name = $"{slab.Name}-tile{row * 2 + col}",
                Slot = $"{slab.Slot}tile{row * 2 + col}",
                From = from,
                To = to,
                Faces = [.. faces],
                ClippedFrom = cell,
            };
        }
    }

    // The outside corner's four plain faces cut into tiles: the front and the leg 1 back along z, the second front and
    // the leg 2 back along x, each with the second front's `-corner` copies over the front's column.
    private static IEnumerable<Element> CornerTiles(Element[] elements)
    {
        var tiles = elements.Where(e => e.Name is "front" or "back" or "secondfront" or "secondback")
            .SelectMany(e => TilePieces(e, 'y', e.Name is "front" or "back" ? 'z' : 'x')).ToArray();
        return SecondFrontCorners(tiles);
    }

    private static Element[] WallTiles(Element[] elements)
        => [.. elements.Where(e => e.Name is "front" or "back").SelectMany(e => TilePieces(e, 'y', 'z'))];

    // What a glazed cell shows of a finish: each front and back group again, cut to the footprint of each
    // glazing piece on the face and named for it, so the border round the glass is the finish's own pieces
    // and its laps and joints carry on from the cells beside it. The finish keeps its depth, ClippedFrom
    // keeps its texture, and a cut end looks onto the glass or the next piece, so it draws its face even
    // where the group limits its faces. The two axes are the face's: z and y on a wall, x and z on a floor.
    internal static IEnumerable<Element> RimPieces(IEnumerable<Element> elements, char firstAxis = 'z', char secondAxis = 'y')
    {
        Element[] all = [.. elements];
        return from part in all
               where part.Name.StartsWith("glazing-")
               from e in all
               where e.Name.StartsWith("front") || e.Name.StartsWith("back")
               from rim in RimPiece(e, part.Name["glazing".Length..],
                   (firstAxis, Axis(part.From, firstAxis), Axis(part.To, firstAxis)),
                   (secondAxis, Axis(part.From, secondAxis), Axis(part.To, secondAxis)))
               select rim;
    }

    // A corner's rims, cut round each leg's opening, which is read off the leg's own rails: `G-rim` is the
    // corner post's side of it and `G-rim-right` (leg 1) or `G-rim-left` (leg 2) the end post's, both full
    // height, `G-rim-top` and `G-rim-bottom` the parts over and under it, and `G-rim-top-right` and the like
    // the parts over and under the end post, which its fillers take once it drops. A front or back group belongs to
    // leg 1, a second front or second back group to leg 2. A T's piece lies in a front or second front slot only, so only
    // those groups give way to it.
    internal static IEnumerable<Element> CornerRimPieces(IEnumerable<Element> elements)
    {
        Element[] all = [.. elements];
        double none = double.NegativeInfinity, any = double.PositiveInfinity;
        static double Length(Element e, char axis) => Axis(e.To, axis) - Axis(e.From, axis);
        foreach (char run in "zx")
        {
            char across = run == 'z' ? 'x' : 'z';
            Element OnLeg(string name) => all.Single(e => e.Name == name && Length(e, run) > Length(e, across));
            Element top = OnLeg("glazing-top"), bottom = OnLeg("glazing-bottom");
            var opening = (Lo: Axis(top.From, run), Hi: Axis(top.To, run));
            string end = run == 'z' ? "-right" : "-left";
            var beyond = (opening.Hi, any);
            (string Part, (double Lo, double Hi) Run, (double Lo, double Hi) Y)[] parts =
            [
                ("", (none, opening.Lo), (none, any)),
                (end, beyond, (none, any)),
                ("-top", opening, (top.From.Y, any)),
                ("-bottom", opening, (none, bottom.To.Y)),
                ("-top" + end, beyond, (top.From.Y, any)),
                ("-bottom" + end, beyond, (none, bottom.To.Y)),
            ];

            string front = run == 'z' ? "front" : "secondfront", back = run == 'z' ? "back" : "secondback";
            foreach (var e in all.Where(e => e.Name.StartsWith(front) || e.Name.StartsWith(back)))
            foreach (var (part, range, y) in parts)
            foreach (var rim in RimPiece(e, part, (run, range.Lo, range.Hi), ('y', y.Lo, y.Hi)))
                yield return rim;

            // `G-rim-tee-right` or `G-rim-tee-left`, the corner post's side cut either side of the post's piece in this leg's slot.
            Element tee = all.Single(e => e.Name == "glazing-tee" + end);
            foreach (var e in all.Where(e => e.Name.StartsWith(front)))
            foreach (var (lo, hi) in new[] { (none, Axis(tee.From, run)), (Axis(tee.To, run), opening.Lo) })
            foreach (var rim in RimPiece(e, "-tee" + end, (run, lo, hi)))
                yield return rim;
        }
    }

    // The part of a finish box inside a range on each of the face's two axes, named for the rim piece it is.
    private static IEnumerable<Element> RimPiece(Element e, string part, params (char Axis, double Lo, double Hi)[] ranges)
    {
        var from = e.From;
        var to = e.To;
        var cutFaces = new List<string>();
        foreach (var (axis, rangeLo, rangeHi) in ranges)
        {
            double sourceLo = Axis(e.From, axis), sourceHi = Axis(e.To, axis);
            double lo = Math.Max(sourceLo, rangeLo), hi = Math.Min(sourceHi, rangeHi);
            if (hi <= lo) yield break;

            var (loFace, hiFace) = axis switch { 'x' => ("west", "east"), 'y' => ("down", "up"), _ => ("north", "south") };
            if (lo > sourceLo) cutFaces.Add(loFace);
            if (hi < sourceHi) cutFaces.Add(hiFace);
            from = WithAxis(from, axis, lo);
            to = WithAxis(to, axis, hi);
        }

        yield return e with
        {
            Name = $"{e.Name}-rim{part}",
            From = from,
            To = to,
            Faces = e.Faces?.Union(cutFaces).ToArray(),
            ClippedFrom = e,
        };
    }

    private static (double X, double Y, double Z) WithAxis((double X, double Y, double Z) corner, char axis, double value)
        => axis == 'x' ? corner with { X = value } : axis == 'y' ? corner with { Y = value } : corner with { Z = value };

    // Wall's front-shakes, except each body course's first shake starts at z 0.5 rather than 0.
    private static readonly Element[] CornerFrontShakesLeg1 =
    [
        new("front-shakes", (0, 0, 0), (1, 1, 5), "front", UvRule.Positional, RunAxis: 'z'),
        new("front-shakes", (0.2, 0, 5), (1, 1, 9), "front", UvRule.Positional, RunAxis: 'z'),
        new("front-shakes", (0.1, 0, 9), (1, 1, 13), "front", UvRule.Positional, RunAxis: 'z'),
        new("front-shakes", (0.3, 0, 13), (1, 1, 16), "front", UvRule.Positional, RunAxis: 'z'),
        new("front-shakes", (0.5, 1, 0.5), (1, 4, 5), "front", UvRule.Positional, RunAxis: 'z'),
        new("front-shakes", (0.7, 1, 5), (1, 4, 9), "front", UvRule.Positional, RunAxis: 'z'),
        new("front-shakes", (0.6, 1, 9), (1, 4, 13), "front", UvRule.Positional, RunAxis: 'z'),
        new("front-shakes", (0.8, 1, 13), (1, 4, 16), "front", UvRule.Positional, RunAxis: 'z'),
        new("front-shakes", (0.15, 4, 0), (1, 5, 4), "front", UvRule.Positional, RunAxis: 'z'),
        new("front-shakes", (0, 4, 4), (1, 5, 7), "front", UvRule.Positional, RunAxis: 'z'),
        new("front-shakes", (0.3, 4, 7), (1, 5, 12), "front", UvRule.Positional, RunAxis: 'z'),
        new("front-shakes", (0.1, 4, 12), (1, 5, 16), "front", UvRule.Positional, RunAxis: 'z'),
        new("front-shakes", (0.65, 5, 0.5), (1, 8, 4), "front", UvRule.Positional, RunAxis: 'z'),
        new("front-shakes", (0.5, 5, 4), (1, 8, 7), "front", UvRule.Positional, RunAxis: 'z'),
        new("front-shakes", (0.8, 5, 7), (1, 8, 12), "front", UvRule.Positional, RunAxis: 'z'),
        new("front-shakes", (0.6, 5, 12), (1, 8, 16), "front", UvRule.Positional, RunAxis: 'z'),
        new("front-shakes", (0.25, 8, 0), (1, 9, 6), "front", UvRule.Positional, RunAxis: 'z'),
        new("front-shakes", (0.1, 8, 6), (1, 9, 10), "front", UvRule.Positional, RunAxis: 'z'),
        new("front-shakes", (0, 8, 10), (1, 9, 13), "front", UvRule.Positional, RunAxis: 'z'),
        new("front-shakes", (0.2, 8, 13), (1, 9, 16), "front", UvRule.Positional, RunAxis: 'z'),
        new("front-shakes", (0.75, 9, 0.5), (1, 12, 6), "front", UvRule.Positional, RunAxis: 'z'),
        new("front-shakes", (0.6, 9, 6), (1, 12, 10), "front", UvRule.Positional, RunAxis: 'z'),
        new("front-shakes", (0.5, 9, 10), (1, 12, 13), "front", UvRule.Positional, RunAxis: 'z'),
        new("front-shakes", (0.7, 9, 13), (1, 12, 16), "front", UvRule.Positional, RunAxis: 'z'),
        new("front-shakes", (0.1, 12, 0), (1, 13, 3), "front", UvRule.Positional, RunAxis: 'z'),
        new("front-shakes", (0.3, 12, 3), (1, 13, 8), "front", UvRule.Positional, RunAxis: 'z'),
        new("front-shakes", (0.15, 12, 8), (1, 13, 12), "front", UvRule.Positional, RunAxis: 'z'),
        new("front-shakes", (0, 12, 12), (1, 13, 16), "front", UvRule.Positional, RunAxis: 'z'),
        new("front-shakes", (0.6, 13, 0.5), (1, 16, 3), "front", UvRule.Positional, RunAxis: 'z'),
        new("front-shakes", (0.8, 13, 3), (1, 16, 8), "front", UvRule.Positional, RunAxis: 'z'),
        new("front-shakes", (0.65, 13, 8), (1, 16, 12), "front", UvRule.Positional, RunAxis: 'z'),
        new("front-shakes", (0.5, 13, 12), (1, 16, 16), "front", UvRule.Positional, RunAxis: 'z'),
    ];

    // The second leg runs along x with depth along z, so its shakes recess from z 0 - the outward
    // face - and its body sits at 0.5 + d the same way; its own RunAxis keeps the texture running
    // across its split courses.
    private static readonly Element[] CornerSecondFrontShakesLeg2 =
    [
        new("secondfront-shakes", (1, 0, 0), (5, 1, 1), "secondfront", UvRule.Positional, RunAxis: 'x'),
        new("secondfront-shakes", (5, 0, 0.2), (9, 1, 1), "secondfront", UvRule.Positional, RunAxis: 'x'),
        new("secondfront-shakes", (9, 0, 0.1), (13, 1, 1), "secondfront", UvRule.Positional, RunAxis: 'x'),
        new("secondfront-shakes", (13, 0, 0.3), (16, 1, 1), "secondfront", UvRule.Positional, RunAxis: 'x'),
        new("secondfront-shakes", (1, 1, 0.5), (5, 4, 1), "secondfront", UvRule.Positional, RunAxis: 'x'),
        new("secondfront-shakes", (5, 1, 0.7), (9, 4, 1), "secondfront", UvRule.Positional, RunAxis: 'x'),
        new("secondfront-shakes", (9, 1, 0.6), (13, 4, 1), "secondfront", UvRule.Positional, RunAxis: 'x'),
        new("secondfront-shakes", (13, 1, 0.8), (16, 4, 1), "secondfront", UvRule.Positional, RunAxis: 'x'),
        new("secondfront-shakes", (1, 4, 0.15), (4, 5, 1), "secondfront", UvRule.Positional, RunAxis: 'x'),
        new("secondfront-shakes", (4, 4, 0), (7, 5, 1), "secondfront", UvRule.Positional, RunAxis: 'x'),
        new("secondfront-shakes", (7, 4, 0.3), (12, 5, 1), "secondfront", UvRule.Positional, RunAxis: 'x'),
        new("secondfront-shakes", (12, 4, 0.1), (16, 5, 1), "secondfront", UvRule.Positional, RunAxis: 'x'),
        new("secondfront-shakes", (1, 5, 0.65), (4, 8, 1), "secondfront", UvRule.Positional, RunAxis: 'x'),
        new("secondfront-shakes", (4, 5, 0.5), (7, 8, 1), "secondfront", UvRule.Positional, RunAxis: 'x'),
        new("secondfront-shakes", (7, 5, 0.8), (12, 8, 1), "secondfront", UvRule.Positional, RunAxis: 'x'),
        new("secondfront-shakes", (12, 5, 0.6), (16, 8, 1), "secondfront", UvRule.Positional, RunAxis: 'x'),
        new("secondfront-shakes", (1, 8, 0.25), (6, 9, 1), "secondfront", UvRule.Positional, RunAxis: 'x'),
        new("secondfront-shakes", (6, 8, 0.1), (10, 9, 1), "secondfront", UvRule.Positional, RunAxis: 'x'),
        new("secondfront-shakes", (10, 8, 0), (13, 9, 1), "secondfront", UvRule.Positional, RunAxis: 'x'),
        new("secondfront-shakes", (13, 8, 0.2), (16, 9, 1), "secondfront", UvRule.Positional, RunAxis: 'x'),
        new("secondfront-shakes", (1, 9, 0.75), (6, 12, 1), "secondfront", UvRule.Positional, RunAxis: 'x'),
        new("secondfront-shakes", (6, 9, 0.6), (10, 12, 1), "secondfront", UvRule.Positional, RunAxis: 'x'),
        new("secondfront-shakes", (10, 9, 0.5), (13, 12, 1), "secondfront", UvRule.Positional, RunAxis: 'x'),
        new("secondfront-shakes", (13, 9, 0.7), (16, 12, 1), "secondfront", UvRule.Positional, RunAxis: 'x'),
        new("secondfront-shakes", (1, 12, 0.1), (3, 13, 1), "secondfront", UvRule.Positional, RunAxis: 'x'),
        new("secondfront-shakes", (3, 12, 0.3), (8, 13, 1), "secondfront", UvRule.Positional, RunAxis: 'x'),
        new("secondfront-shakes", (8, 12, 0.15), (12, 13, 1), "secondfront", UvRule.Positional, RunAxis: 'x'),
        new("secondfront-shakes", (12, 12, 0), (16, 13, 1), "secondfront", UvRule.Positional, RunAxis: 'x'),
        new("secondfront-shakes", (1, 13, 0.6), (3, 16, 1), "secondfront", UvRule.Positional, RunAxis: 'x'),
        new("secondfront-shakes", (3, 13, 0.8), (8, 16, 1), "secondfront", UvRule.Positional, RunAxis: 'x'),
        new("secondfront-shakes", (8, 13, 0.65), (12, 16, 1), "secondfront", UvRule.Positional, RunAxis: 'x'),
        new("secondfront-shakes", (12, 13, 0.5), (16, 16, 1), "secondfront", UvRule.Positional, RunAxis: 'x'),
    ];

    private static readonly Element[] CornerPoleElements =
    [
        // The wall's rough frame (see WallPoleElements), turned around the corner: the corner post keeps the
        // plain box, each end post recesses on its own leg's depth axis, and the plates gain cheeks at
        // the end posts only. Nothing stands out beside the corner post.
        new("poles", (1, 0, 1), (3, 16, 3), "framing", UvRule.Flat),
        new("poles", (1.25, 0, 15), (2.75, 4, 16), "framing", UvRule.Flat),
        new("poles", (1.25, 4, 14.75), (2.75, 10, 16), "framing", UvRule.Flat),
        new("poles", (1.25, 10, 15), (2.75, 16, 16), "framing", UvRule.Flat),
        new("poles", (1, 7, 13.5), (1.5, 8, 15), "framing", UvRule.Flat),
        new("poles", (2.5, 12.5, 14), (3, 13.5, 15), "framing", UvRule.Flat),
        new("poles", (15, 0, 1.25), (16, 6, 2.75), "framing", UvRule.Flat),
        new("poles", (14.75, 6, 1.25), (16, 12, 2.75), "framing", UvRule.Flat),
        new("poles", (15, 12, 1.25), (16, 16, 2.75), "framing", UvRule.Flat),
        new("poles", (13.5, 8, 1), (15, 9, 1.5), "framing", UvRule.Flat),
        new("poles", (14, 9.5, 2.5), (15, 10.5, 3), "framing", UvRule.Flat),
        new("poles-top", (1, 15, 3), (3, 16, 15), "framing", UvRule.Flat, Faces: ["west", "east", "up", "down"]),
        new("poles-top", (1, 15, 15), (1.25, 16, 16), "framing", UvRule.Flat),
        new("poles-top", (2.75, 15, 15), (3, 16, 16), "framing", UvRule.Flat),
        new("poles-top", (3, 15, 1), (15, 16, 3), "framing", UvRule.Flat, Faces: ["north", "south", "up", "down"]),
        new("poles-top", (15, 15, 1), (16, 16, 1.25), "framing", UvRule.Flat),
        new("poles-top", (15, 15, 2.75), (16, 16, 3), "framing", UvRule.Flat),
        new("poles-top", (1, 13.5, 14.75), (1.25, 15, 16), "lashing", UvRule.Flat),
        new("poles-top", (2.75, 13.5, 14.75), (3, 15, 16), "lashing", UvRule.Flat),
        new("poles-top", (14.75, 13.5, 1), (16, 15, 1.25), "lashing", UvRule.Flat),
        new("poles-top", (14.75, 13.5, 2.75), (16, 15, 3), "lashing", UvRule.Flat),
        new("poles-bottom", (1, 0, 3), (3, 1, 15), "framing", UvRule.Flat, Faces: ["west", "east", "up", "down"]),
        new("poles-bottom", (1, 0, 15), (1.25, 1, 16), "framing", UvRule.Flat),
        new("poles-bottom", (2.75, 0, 15), (3, 1, 16), "framing", UvRule.Flat),
        new("poles-bottom", (3, 0, 1), (15, 1, 3), "framing", UvRule.Flat, Faces: ["north", "south", "up", "down"]),
        new("poles-bottom", (15, 0, 1), (16, 1, 1.25), "framing", UvRule.Flat),
        new("poles-bottom", (15, 0, 2.75), (16, 1, 3), "framing", UvRule.Flat),
        new("poles-bottom", (1, 1, 14.75), (1.25, 2.5, 16), "lashing", UvRule.Flat),
        new("poles-bottom", (2.75, 1, 14.75), (3, 2.5, 16), "lashing", UvRule.Flat),
        new("poles-bottom", (14.75, 1, 1), (16, 2.5, 1.25), "lashing", UvRule.Flat),
        new("poles-bottom", (14.75, 1, 2.75), (16, 2.5, 3), "lashing", UvRule.Flat),
        .. CornerTees("poles"),
    ];

    private static readonly Element[] CornerPole2Elements =
    [
        // Second variant, as the wall's.
        new("poles2", (1, 0, 1), (3, 16, 3), "framing", UvRule.Flat),
        new("poles2", (1.25, 0, 15), (2.75, 7, 16), "framing", UvRule.Flat),
        new("poles2", (1.25, 7, 14.75), (2.75, 13, 16), "framing", UvRule.Flat),
        new("poles2", (1.25, 13, 15), (2.75, 16, 16), "framing", UvRule.Flat),
        new("poles2", (2.5, 3, 12.5), (3, 4, 15), "framing", UvRule.Flat),
        new("poles2", (1, 9, 13), (1.5, 10, 15), "framing", UvRule.Flat),
        new("poles2", (15, 0, 1.25), (16, 5, 2.75), "framing", UvRule.Flat),
        new("poles2", (14.75, 5, 1.25), (16, 11, 2.75), "framing", UvRule.Flat),
        new("poles2", (15, 11, 1.25), (16, 16, 2.75), "framing", UvRule.Flat),
        new("poles2", (13, 7, 2.5), (15, 8, 3), "framing", UvRule.Flat),
        new("poles2", (13.5, 8.5, 1), (15, 9.5, 1.5), "framing", UvRule.Flat),
        .. CornerPoleElements.Where(e => e.Name is "poles-top" or "poles-bottom").Select(e => e with { Name = e.Name.Replace("poles", "poles2") }),
        .. CornerTees("poles2"),
    ];

    // A corner's bamboo post: a pole a quarter voxel back from both fronts, as every member's core stands behind its front,
    // notched at the outer corner so it reads round, and full to the room sides, where the legs' plates end on it. Its rings
    // fill the 2 by 2 and reach a quarter voxel into each leg's bay, as an upright's do. A ring box against the pole or the
    // infill there lies flush on one pole box, so that face is buried rather than drawn on the infill's plane.
    private static IEnumerable<Element> BambooPost(string name, double[] rings)
    {
        Element Box(double x1, double y1, double z1, double x2, double y2, double z2) => new(name, (x1, y1, z1), (x2, y2, z2), "framing", UvRule.Flat);
        yield return Box(1.5, 0, 1.25, 3, 16, 3);
        yield return Box(1.25, 0, 1.5, 1.5, 16, 3);
        foreach (double y in rings)
        {
            yield return Box(1, y, 1, 1.25, y + 0.5, 3.25);
            yield return Box(1.25, y, 1, 3.25, y + 0.5, 1.25);
            yield return Box(1.25, y, 1.25, 1.5, y + 0.5, 1.5);
            yield return Box(3, y, 1.25, 3.25, y + 0.5, 3);
            yield return Box(1.25, y, 3, 1.5, y + 0.5, 3.25);
            yield return Box(1.5, y, 3, 3, y + 0.5, 3.25);
        }
    }

    // The square lo..hi on x and z over y1..y2, its four vertical edges notched `notch` deep: a core and a wing on each side.
    private static Element[] NotchedSquare(string name, double lo, double hi, double notch, double y1, double y2)
    {
        Element Box(double x1, double z1, double x2, double z2) => new(name, (x1, y1, z1), (x2, y2, z2), "framing", UvRule.Flat);
        double a = lo + notch, b = hi - notch;
        return [Box(a, a, b, b), Box(lo, a, a, b), Box(b, a, hi, b), Box(a, lo, b, a), Box(a, b, b, hi)];
    }

    // The corner's frame in bamboo: the post and the two end posts, a plate along each leg, the rough frame's lashings, and
    // the tees. A leg's plates start at the post's wings, which fill the first quarter voxel of its depth. Leg 1's end post
    // takes the wall's right upright's rings and leg 2's its left's.
    private static IEnumerable<Element> BambooCorner(BambooRings v)
    {
        string[] leg2 = ["north", "south", "up", "down"];
        return
        [
            .. BambooPost(v.Prefix, v.Post),
            .. BambooPole(v.Prefix, (1, 0, 15), (3, 16, 16), 'x', 'y', v.Right),
            .. BambooPole(v.Prefix, (15, 0, 1), (16, 16, 3), 'z', 'y', v.Left),
            .. BambooPole($"{v.Prefix}-top", (1, 15, 3), (3, 16, 15), 'x', 'z', v.Top, BambooPlateFaces),
            .. BambooPole($"{v.Prefix}-top", (3, 15, 1), (15, 16, 3), 'z', 'x', v.Top, leg2),
            .. BambooPole($"{v.Prefix}-bottom", (1, 0, 3), (3, 1, 15), 'x', 'z', v.Bottom, BambooPlateFaces),
            .. BambooPole($"{v.Prefix}-bottom", (3, 0, 1), (15, 1, 3), 'z', 'x', v.Bottom, leg2),
            .. CornerPoleElements.Where(e => e.Slot == "lashing").Select(e => e with { Name = e.Name.Replace("poles", v.Prefix) }),
            // The post stands back from its fronts, so a tee draws the face it puts against the post, and between the post's
            // rings fills the step behind it, which would be a slot through the T from one leg's outside to the other's. The
            // left tee's step takes the corner square; with the right tee alone that square opens onto leg 2's front only.
            .. CornerTees(v.Prefix).Select(e => e with { Faces = null }),
            .. from gap in v.Post.Select(ring => ring + 0.5).Prepend(0.0).Zip(v.Post.Append(16.0))
               from step in new Element[]
               {
                   new($"{v.Prefix}-tee-left", (1, gap.First, 1), (3, gap.Second, 1.25), "framing", UvRule.Flat),
                   new($"{v.Prefix}-tee-right", (1, gap.First, 1.25), (1.25, gap.Second, 3), "framing", UvRule.Flat),
               }
               select step,
        ];
    }

    private static readonly Element[] CornerBambooElements = [.. BambooVariants.SelectMany(BambooCorner)];

    // Every frame's corner post stands a finish slot in from the cell's edge on each face, so at a T it fills the slot
    // the run crosses: the left leg's, where leg 1's line runs on past it, or the right leg's, where leg 2's does.
    private static Element[] CornerTees(string group) =>
    [
        new($"{group}-tee-left", (1, 0, 0), (3, 16, 1), "framing", UvRule.Flat, Faces: ["north", "west", "east", "up", "down"]),
        new($"{group}-tee-right", (0, 0, 1), (1, 16, 3), "framing", UvRule.Flat, Faces: ["west", "north", "south", "up", "down"]),
    ];

    // A corner's outer side samples the whole texture across its two legs: the sliver on the front leg's end takes the
    // column the second leg leaves (15..16). Its room side keeps one texel per voxel on the grid, so tiling finishes meet
    // the walls each leg runs into, and the inner corner's column is a box of its own (decision 0086).
    private static Dictionary<string, FaceUv> Only(string face, FaceUv uv) => new() { [face] = uv };

    // The `-whole` copy of a corner's room-side group squeezes the whole texture onto each leg's 12 voxels, for a framed
    // pattern such as plaster, whose border a grid uv would cut off (decision 0086).
    private static Element[] WholeCornerBack(string name, bool turned)
    {
        int rotation = turned ? 270 : 0;
        return
        [
            new($"{name}-whole", (3, 0, 4), (4, 16, 16), "back", UvRule.Flat, RotatedFaces: turned ? ["east"] : null, Explicit: Only("east", F(0, 0, 16, 16, rotation))),
            new($"{name}-whole", (3, 0, 3), (4, 16, 4), "back", UvRule.Flat, RotatedFaces: turned ? ["south"] : null, RunAxis: 'x'),
            new($"{name}-whole", (4, 0, 3), (16, 16, 4), "back", UvRule.Flat, RotatedFaces: turned ? ["south"] : null, Explicit: Only("south", F(0, 0, 16, 16, rotation))),
        ];
    }

    private static readonly Element[] CornerOutElements = SecondFrontCorners(SecondBackOnLegTwo(
    [
        new("front", (0, 0, 0), (1, 16, 16), "front", UvRule.Flat, Explicit: Only("north", F(15, 0, 16, 16))),
        new("framing-top", (1, 15, 3), (3, 16, 15), "framing", UvRule.Flat),
        new("framing-bottom", (1, 0, 3), (3, 1, 15), "framing", UvRule.Flat),
        new("framing", (1, 0, 1), (3, 16, 3), "framing", UvRule.Flat),
        new("infill-top", (1.5, 15, 3), (2.5, 16, 15), "infill", UvRule.Positional),
        new("infill", (1.5, 1, 3), (2.5, 15, 15), "infill", UvRule.Positional),
        new("infill-bottom", (1.5, 0, 3), (2.5, 1, 15), "infill", UvRule.Positional),
        .. StemBands(3, 15),
        new("framing", (1, 0, 15), (3, 16, 16), "framing", UvRule.Flat),
        new("back", (3, 0, 4), (4, 16, 16), "back", UvRule.Flat),
        new("secondfront", (1, 0, 0), (16, 16, 1), "secondfront", UvRule.Flat),
        new("infill-front", (0, 0, 0), (1, 16, 16), "infill", UvRule.Positional),
        new("infill-secondfront", (1, 0, 0), (16, 16, 1), "infill", UvRule.Positional),
        .. StepSetElements(corner: true),
        new("framing-top", (3, 15, 1), (15, 16, 3), "framing", UvRule.Flat),
        new("framing-bottom", (3, 0, 1), (15, 1, 3), "framing", UvRule.Flat),
        new("infill-top", (3, 15, 1.5), (15, 16, 2.5), "infill", UvRule.Positional),
        new("infill", (3, 1, 1.5), (15, 15, 2.5), "infill", UvRule.Positional),
        new("infill-bottom", (3, 0, 1.5), (15, 1, 2.5), "infill", UvRule.Positional),
        .. StemBands(3, 15).Select(SwapXZ),
        new("infill-pane", (2, 0, 3), (2, 16, 16), "infill", UvRule.Flat, Faces: ["west", "east"]),
        new("infill-pane", (3, 0, 2), (16, 16, 2), "infill", UvRule.Flat, Faces: ["north", "south"]),
        new("framing", (15, 0, 1), (16, 16, 3), "framing", UvRule.Flat),
        .. CornerTees("framing"),
        // A glazed corner's frame: a corner post that never drops, an end post per leg named for the side of
        // a wall it lies on (leg 1's far end is its right, leg 2's its left), a rail per leg between them,
        // and a filler per leg in the rail's corner where the end post has merged away. The corner post is two boxes.
        new("glazing", (1, 0, 1), (3, 16, 4), "framing", UvRule.Flat),
        new("glazing", (3, 0, 1), (4, 16, 3), "framing", UvRule.Flat),
        .. CornerTees("glazing"),
        new("glazing-right", (1, 0, 14), (3, 16, 16), "framing", UvRule.Flat),
        new("glazing-left", (14, 0, 1), (16, 16, 3), "framing", UvRule.Flat),
        new("glazing-top-right", (1, 14, 14), (3, 16, 16), "framing", UvRule.Flat),
        new("glazing-bottom-right", (1, 0, 14), (3, 2, 16), "framing", UvRule.Flat),
        new("glazing-top-left", (14, 14, 1), (16, 16, 3), "framing", UvRule.Flat),
        new("glazing-bottom-left", (14, 0, 1), (16, 2, 3), "framing", UvRule.Flat),
        new("glazing-top", (1, 14, 4), (3, 16, 14), "framing", UvRule.Flat, Faces: ["west", "east", "up", "down"]),
        new("glazing-top", (4, 14, 1), (14, 16, 3), "framing", UvRule.Flat, Faces: ["north", "south", "up", "down"]),
        new("glazing-bottom", (1, 0, 4), (3, 2, 14), "framing", UvRule.Flat, Faces: ["west", "east", "up", "down"]),
        new("glazing-bottom", (4, 0, 1), (14, 2, 3), "framing", UvRule.Flat, Faces: ["north", "south", "up", "down"]),
        .. CornerPoleElements,
        .. CornerPole2Elements,
        .. CornerBambooElements,
        new("back", (3, 0, 3), (4, 16, 4), "back", UvRule.Flat, RunAxis: 'x'),
        new("back", (4, 0, 3), (16, 16, 4), "back", UvRule.Flat, RunAxis: 'x'),
        new("front-weatherboard", (0, 0, 0), (1, 1, 16), "front", UvRule.Positional),
        new("front-weatherboard", (0.25, 1, 0.5), (1, 2, 16), "front", UvRule.Positional),
        new("front-weatherboard", (0.5, 2, 0.5), (1, 3, 16), "front", UvRule.Positional),
        new("front-weatherboard", (0.75, 3, 0.5), (1, 4, 16), "front", UvRule.Positional),
        new("secondfront-weatherboard", (1, 0, 0), (16, 1, 1), "secondfront", UvRule.Positional),
        new("secondfront-weatherboard", (1, 1, 0.25), (16, 2, 1), "secondfront", UvRule.Positional),
        new("secondfront-weatherboard", (1, 2, 0.5), (16, 3, 1), "secondfront", UvRule.Positional),
        new("secondfront-weatherboard", (1, 3, 0.75), (16, 4, 1), "secondfront", UvRule.Positional),
        new("front-weatherboard", (0, 4, 0), (1, 5, 16), "front", UvRule.Positional),
        new("front-weatherboard", (0.25, 5, 0.5), (1, 6, 16), "front", UvRule.Positional),
        new("front-weatherboard", (0.5, 6, 0.5), (1, 7, 16), "front", UvRule.Positional),
        new("front-weatherboard", (0.75, 7, 0.5), (1, 8, 16), "front", UvRule.Positional),
        new("secondfront-weatherboard", (1, 4, 0), (16, 5, 1), "secondfront", UvRule.Positional),
        new("secondfront-weatherboard", (1, 5, 0.25), (16, 6, 1), "secondfront", UvRule.Positional),
        new("secondfront-weatherboard", (1, 6, 0.5), (16, 7, 1), "secondfront", UvRule.Positional),
        new("secondfront-weatherboard", (1, 7, 0.75), (16, 8, 1), "secondfront", UvRule.Positional),
        new("front-weatherboard", (0, 8, 0), (1, 9, 16), "front", UvRule.Positional),
        new("front-weatherboard", (0.25, 9, 0.5), (1, 10, 16), "front", UvRule.Positional),
        new("front-weatherboard", (0.5, 10, 0.5), (1, 11, 16), "front", UvRule.Positional),
        new("front-weatherboard", (0.75, 11, 0.5), (1, 12, 16), "front", UvRule.Positional),
        new("secondfront-weatherboard", (1, 8, 0), (16, 9, 1), "secondfront", UvRule.Positional),
        new("secondfront-weatherboard", (1, 9, 0.25), (16, 10, 1), "secondfront", UvRule.Positional),
        new("secondfront-weatherboard", (1, 10, 0.5), (16, 11, 1), "secondfront", UvRule.Positional),
        new("secondfront-weatherboard", (1, 11, 0.75), (16, 12, 1), "secondfront", UvRule.Positional),
        new("front-weatherboard", (0, 12, 0), (1, 13, 16), "front", UvRule.Positional),
        new("front-weatherboard", (0.25, 13, 0.5), (1, 14, 16), "front", UvRule.Positional),
        new("front-weatherboard", (0.5, 14, 0.5), (1, 15, 16), "front", UvRule.Positional),
        new("front-weatherboard", (0.75, 15, 0.5), (1, 16, 16), "front", UvRule.Positional),
        new("secondfront-weatherboard", (1, 12, 0), (16, 13, 1), "secondfront", UvRule.Positional),
        new("secondfront-weatherboard", (1, 13, 0.25), (16, 14, 1), "secondfront", UvRule.Positional),
        new("secondfront-weatherboard", (1, 14, 0.5), (16, 15, 1), "secondfront", UvRule.Positional),
        new("secondfront-weatherboard", (1, 15, 0.75), (16, 16, 1), "secondfront", UvRule.Positional),
        new("back-boards", (3, 0, 4), (4, 16, 16), "back", UvRule.Flat, RotatedFaces: ["east"]),
        new("back-boards", (3, 0, 3), (4, 16, 4), "back", UvRule.Flat, RotatedFaces: ["south"], RunAxis: 'x'),
        new("back-boards", (4, 0, 3), (16, 16, 4), "back", UvRule.Flat, RotatedFaces: ["south"], RunAxis: 'x'),
        .. WholeCornerBack("back", turned: false),
        .. WholeCornerBack("back-boards", turned: true),
        .. WholeCornerBack("back-hboards", turned: false),
        new("front-boards", (0, 0, 0), (1, 16, 16), "front", UvRule.Flat, RotatedFaces: ["west", "north"], Explicit: Only("north", F(0, 15, 16, 16, 270))),
        new("secondfront-boards", (1, 0, 0), (16, 16, 1), "secondfront", UvRule.Flat, null, null, ["north"]),
        .. WithBark(
            new("back-hboards", (3, 0, 4), (4, 16, 16), "back", UvRule.Flat),
            new("back-hboards", (3, 0, 3), (4, 16, 4), "back", UvRule.Flat, RunAxis: 'x'),
            new("back-hboards", (4, 0, 3), (16, 16, 4), "back", UvRule.Flat, RunAxis: 'x'),
            new("front-hboards", (0, 0, 0), (1, 16, 16), "front", UvRule.Flat, Explicit: Only("north", F(15, 0, 16, 16))),
            new("secondfront-hboards", (1, 0, 0), (16, 16, 1), "secondfront", UvRule.Flat)),
        new("back-weatherboard", (3, 0, 4), (4, 1, 16), "back", UvRule.Positional),
        new("back-weatherboard", (3, 0, 3), (16, 1, 4), "back", UvRule.Positional),
        new("back-weatherboard", (3, 1, 4), (3.75, 2, 16), "back", UvRule.Positional),
        new("back-weatherboard", (3, 1, 3), (16, 2, 3.75), "back", UvRule.Positional),
        new("back-weatherboard", (3, 2, 4), (3.5, 3, 16), "back", UvRule.Positional),
        new("back-weatherboard", (3, 2, 3), (16, 3, 3.5), "back", UvRule.Positional),
        new("back-weatherboard", (3, 3, 4), (3.25, 4, 16), "back", UvRule.Positional),
        new("back-weatherboard", (3, 3, 3), (16, 4, 3.25), "back", UvRule.Positional),
        new("back-weatherboard", (3, 4, 4), (4, 5, 16), "back", UvRule.Positional),
        new("back-weatherboard", (3, 4, 3), (16, 5, 4), "back", UvRule.Positional),
        new("back-weatherboard", (3, 5, 4), (3.75, 6, 16), "back", UvRule.Positional),
        new("back-weatherboard", (3, 5, 3), (16, 6, 3.75), "back", UvRule.Positional),
        new("back-weatherboard", (3, 6, 4), (3.5, 7, 16), "back", UvRule.Positional),
        new("back-weatherboard", (3, 6, 3), (16, 7, 3.5), "back", UvRule.Positional),
        new("back-weatherboard", (3, 7, 4), (3.25, 8, 16), "back", UvRule.Positional),
        new("back-weatherboard", (3, 7, 3), (16, 8, 3.25), "back", UvRule.Positional),
        new("back-weatherboard", (3, 8, 4), (4, 9, 16), "back", UvRule.Positional),
        new("back-weatherboard", (3, 8, 3), (16, 9, 4), "back", UvRule.Positional),
        new("back-weatherboard", (3, 9, 4), (3.75, 10, 16), "back", UvRule.Positional),
        new("back-weatherboard", (3, 9, 3), (16, 10, 3.75), "back", UvRule.Positional),
        new("back-weatherboard", (3, 10, 4), (3.5, 11, 16), "back", UvRule.Positional),
        new("back-weatherboard", (3, 10, 3), (16, 11, 3.5), "back", UvRule.Positional),
        new("back-weatherboard", (3, 11, 4), (3.25, 12, 16), "back", UvRule.Positional),
        new("back-weatherboard", (3, 11, 3), (16, 12, 3.25), "back", UvRule.Positional),
        new("back-weatherboard", (3, 12, 4), (4, 13, 16), "back", UvRule.Positional),
        new("back-weatherboard", (3, 12, 3), (16, 13, 4), "back", UvRule.Positional),
        new("back-weatherboard", (3, 13, 4), (3.75, 14, 16), "back", UvRule.Positional),
        new("back-weatherboard", (3, 13, 3), (16, 14, 3.75), "back", UvRule.Positional),
        new("back-weatherboard", (3, 14, 4), (3.5, 15, 16), "back", UvRule.Positional),
        new("back-weatherboard", (3, 14, 3), (16, 15, 3.5), "back", UvRule.Positional),
        new("back-weatherboard", (3, 15, 4), (3.25, 16, 16), "back", UvRule.Positional),
        new("back-weatherboard", (3, 15, 3), (16, 16, 3.25), "back", UvRule.Positional),
        // Same tiling as wall's front-shakes (see FrontShakesElements' comment), on both legs. The
        // front leg runs along z with depth along x; the second leg runs along x with depth along
        // z, so its shakes recess from z 0 - the outward face - and its body sits at 0.5 + d the
        // same way. Each leg's RunAxis is what keeps the texture running across its own split
        // courses.
        .. CornerFrontShakesLeg1,
        .. CornerSecondFrontShakesLeg2,
        // A back group is built across both legs, as back-brick is below; SecondBackOnLegTwo splits leg 2 off.
        .. Logs("back-logs", "back", 'x', 4, 3, "east", 4, 16),
        .. Logs("front-logs", "front", 'x', 0, 1, "west", 0, 16),
        .. Logs("back-logs", "back", 'z', 4, 3, "south", 3, 16),
        .. Logs("secondfront-logs", "secondfront", 'z', 0, 1, "north", 1, 16),
        .. ClipRun(CornerFrontShakesLeg1.Select(e => MirrorDepthX(e, "back-shakes", "back")), 'z', 4, 16),
        .. ClipRun(CornerSecondFrontShakesLeg2.Select(e => MirrorDepthZ(e, "back-shakes", "back")), 'x', 3, 16),
        .. RunningBond("front-brick", "front", 4, 8, 'x', 0, 1, 0, 16),
        .. RunningBond("secondfront-brick", "secondfront", 4, 8, 'z', 0, 1, 1, 16),
        .. RunningBond("front-ashlar", "front", 8, 16, 'x', 0, 1, 0, 16, flipBond: true),
        .. RunningBond("secondfront-ashlar", "secondfront", 8, 16, 'z', 0, 1, 1, 16, flipBond: true),
        // A back group is built across both legs, the way back-logs is; SecondBackOnLegTwo splits leg 2 off.
        .. RunningBond("back-brick", "back", 4, 8, 'x', 4, 3, 4, 16),
        .. RunningBond("back-brick", "back", 4, 8, 'z', 4, 3, 3, 16),
        .. RunningBond("back-ashlar", "back", 8, 16, 'x', 4, 3, 4, 16, flipBond: true),
        .. RunningBond("back-ashlar", "back", 8, 16, 'z', 4, 3, 3, 16, flipBond: true),
        .. RubbleGrid("front-rubble", "front", 'x', 0, 1, 0, 16),
        .. RubbleGrid("secondfront-rubble", "secondfront", 'z', 0, 1, 1, 16),
        .. RubbleGrid("back-rubble", "back", 'x', 4, 3, 4, 16),
        .. RubbleGrid("back-rubble", "back", 'z', 4, 3, 3, 16),
    ]));

    // A corner's room side is two faces, leg 1 and leg 2, and the inner corner's column goes with leg 2 (decision 0090).
    // The shapes are built as one back and split here, so every style keeps its two legs' look and only the name differs.
    private static Element[] SecondBackOnLegTwo(Element[] elements)
        => [.. elements.Select(e => e.Slot == "back" && e.From.Z < 4 ? e with { Name = "second" + e.Name, Slot = "secondback" } : e)];

    // The outside corner's column is the front's, and `G-corner` carries each second front group G on over it for a bare
    // front (decision 0090): its boxes against the column, moved into it. A group running along x samples its own place on
    // the run already; the rest keep their slab's texture going, through the column the front's sliver samples.
    private static Element[] SecondFrontCorners(Element[] elements)
        => [.. elements, .. elements.Where(e => (e.Slot == "secondfront" || e.Slot.StartsWith("secondfronttile")) && e.From.X == 1).Select(e => e with
        {
            Name = e.Name + "-corner",
            From = e.From with { X = 0 },
            To = e.To with { X = 1 },
            Faces = [.. (e.Faces ?? AllFaces).Where(face => face != "east")],
            ClippedFrom = e.RunAxis == 'x' ? null : e with { From = e.From with { X = 0 } },
        })];

    // The column's north side lies in the second face's plane, so the front draws it in `frontedge`, which shows the second
    // face's finish, or the front's while that face is bare (decision 0090). Run after the rims are cut, so a cut face
    // looking north onto the glass keeps the front's.
    private static Element[] FrontEdgeOnSecondFace(Element[] elements)
        => [.. elements.Select(e => e.From.Z < 1 && Regex.IsMatch(e.Slot, @"^front(tile\d)?$")
            ? e with { FaceSlots = new() { ["north"] = "frontedge" + e.Slot["front".Length..] } } : e)];

    private static readonly (string Slot, string Texture)[] WallTextures =
    [
        ("framing", "game:block/wood/planks/oak1"),
        ("infill", "game:block/wood/planks/oak1"),
        ("front", "game:block/wood/planks/oak1"),
        ("back", "game:block/wood/planks/oak1"),
        ("deck", "game:block/wood/planks/oak1"),
        ("deckinfill", "game:block/wood/planks/oak1"),
        ("deckfront", "game:block/wood/planks/oak1"),
        ("deckback", "game:block/wood/planks/oak1"),
        ("step", "game:block/wood/planks/oak1"),
        ("lashing", "game:item/resource/rope"),
    ];

    private static readonly (string Slot, string Texture)[] CornerOutTextures =
    [
        ("framing", "game:block/wood/planks/aged/aged1"),
        ("infill", "game:block/wood/planks/aged/aged1"),
        ("front", "game:block/wood/planks/aged/aged1"),
        ("secondfront", "game:block/wood/planks/aged/aged1"),
        ("frontedge", "game:block/wood/planks/aged/aged1"),
        ("back", "game:block/wood/planks/aged/aged1"),
        ("secondback", "game:block/wood/planks/aged/aged1"),
        ("deck", "game:block/wood/planks/aged/aged1"),
        ("deckinfill", "game:block/wood/planks/aged/aged1"),
        ("deckfront", "game:block/wood/planks/aged/aged1"),
        ("deckback", "game:block/wood/planks/aged/aged1"),
        ("step", "game:block/wood/planks/aged/aged1"),
        ("lashing", "game:item/resource/rope"),
    ];

    // The diagonal (decision 0066): the straight wall's own boxes, moved onto the cell centre and turned
    // 45 degrees so the front faces north-west. Its run is longer than a face's 16, so the frame and the
    // infill are redrawn as two halves, and a finish repeats along the run as it does from one straight
    // wall to the next, uv and all, which is what Offset is for. A square end 2 short of the corner
    // touches both of the cell's faces there, and a filler in the corner behind it makes the mitre to a
    // straight wall. Where the next cell carries the run on, the -left and -right groups take every
    // piece to the corner instead, and half a post stands there; their square ends reach into the two
    // cells beside the corner, and with the next cell's they make one band.
    internal const double DiagonalCorner = 11.3137;
    private const double DiagonalEnd = 9.3137;
    private const double DiagonalFiller = 2.8284;

    // A glazed diagonal has no stiles of its own, since its corner fillers are its stiles, so its glazing
    // rails run end to end between them.
    private static readonly string[] DiagonalFrame =
        ["framing-top", "framing-bottom", "infill-top", "infill", "infill-bottom", "infill-pane", "glazing-top", "glazing-bottom"];

    // Where two glazed diagonals' glass runs on, the pieces named for a join fill the half post's place
    // beyond the rails' and the pane's -left and -right pieces.
    private static readonly string[] DiagonalJoined = ["glazing-top", "glazing-bottom", "infill-pane"];

    // A finish on a glazed diagonal is a border on the rails, cut to their heights.
    private static readonly (string Part, double Lo, double Hi)[] DiagonalRails =
    [
        .. new[] { "top", "bottom" }.Select(rail => WallElements.Single(e => e.Name == "glazing-" + rail))
            .Select(e => (e.Name["glazing".Length..], (double)e.From.Y, (double)e.To.Y)),
    ];

    private static Element OnDiagonal(Element e, string suffix = "", double shift = 0)
        => e with { Name = e.Name + suffix, Offset = (6, 0, shift), RotationY = -45 };

    private static Element DiagonalRun(Element e, double lo, double hi, string suffix = "")
        => OnDiagonal(e with { From = (e.From.X, e.From.Y, 8 + lo), To = (e.To.X, e.To.Y, 8 + hi) }, suffix);

    // A join's rail piece draws its end toward the cell, where it frames an inside corner with no rail beside it.
    // The pane is a plane and has no end to draw.
    private static Element InnerEnd(Element e, string face) => e.Name == "infill-pane" ? e : e with { Faces = [.. e.Faces!, face] };

    private static IEnumerable<Element> DiagonalFinish(double runLo, double runHi, string suffix = "")
        => from shift in new[] { -16, 0, 16 }
           from e in WallElements
           where e.Name.StartsWith("front") || e.Name.StartsWith("back")
           let lo = Math.Max(e.From.Z, 8 + runLo - shift)
           let hi = Math.Min(e.To.Z, 8 + runHi - shift)
           where hi > lo
           select OnDiagonal(e with { From = (e.From.X, e.From.Y, lo), To = (e.To.X, e.To.Y, hi), ClippedFrom = e }, suffix, shift);

    // The stems over a run, laid on the cell's 0..16 and repeated along it as DiagonalFinish lays a finish, so the poles keep
    // their spacing and nodes through the run's ends instead of stretching as DiagonalRun stretches a slab.
    private static IEnumerable<Element> DiagonalStems(double runLo, double runHi, string suffix = "")
        => from shift in new[] { -16, 0, 16 }
           from e in StemBands(0, 16)
           let lo = Math.Max(e.From.Z, 8 + runLo - shift)
           let hi = Math.Min(e.To.Z, 8 + runHi - shift)
           where hi > lo
           select OnDiagonal(e with { From = (e.From.X, e.From.Y, lo), To = (e.To.X, e.To.Y, hi) }, suffix, shift);

    // A finish's pieces over a run cut to each rail's height, or with `post`, where the half post stays, one piece over the
    // post's whole height: `G-rim-join-left` and `-right`.
    private static IEnumerable<Element> DiagonalRims(double runLo, double runHi, string suffix = "", bool post = false)
        => from shift in new[] { -16, 0, 16 }
           from e in WallElements
           where e.Name.StartsWith("front") || e.Name.StartsWith("back")
           from rail in post ? [("-join", double.NegativeInfinity, double.PositiveInfinity)] : DiagonalRails
           from rim in RimPiece(e, rail.Part, ('z', 8 + runLo - shift, 8 + runHi - shift), ('y', rail.Lo, rail.Hi))
           select OnDiagonal(rim, suffix, shift);

    // The plates of a bamboo frame over a run, laid on the cell's 0..16 and repeated along it as DiagonalStems repeats the
    // stems, so the rings keep their spacing through the run's ends.
    private static IEnumerable<Element> DiagonalBambooPlates(BambooRings v, double runLo, double runHi, string suffix = "")
        => from shift in new[] { -16, 0, 16 }
           from e in BambooPole($"{v.Prefix}-top", (1, 15, 0), (3, 16, 16), 'x', 'z', v.Top, BambooPlateFaces)
               .Concat(BambooPole($"{v.Prefix}-bottom", (1, 0, 0), (3, 1, 16), 'x', 'z', v.Bottom, BambooPlateFaces))
           let lo = Math.Max(e.From.Z, 8 + runLo - shift)
           let hi = Math.Min(e.To.Z, 8 + runHi - shift)
           where hi > lo
           select OnDiagonal(e with { From = (e.From.X, e.From.Y, lo), To = (e.To.X, e.To.Y, hi) }, suffix, shift);

    // A diagonal's frame in bamboo: the plates over the run and its two ends, and the corner fillers and half posts as ringed
    // uprights, each filler's depth on the axis its mirror across the run puts it.
    private static IEnumerable<Element> DiagonalBamboo(BambooRings v) =>
    [
        .. DiagonalBambooPlates(v, -DiagonalEnd, DiagonalEnd),
        .. DiagonalBambooPlates(v, 1 - DiagonalCorner, -DiagonalEnd, "-left"),
        .. DiagonalBambooPlates(v, DiagonalEnd, DiagonalCorner - 1, "-right"),
        .. BambooPole($"{v.Prefix}-left", (16 - DiagonalFiller, 0.01, 0), (16, 15.99, DiagonalFiller), 'x', 'y', v.Left),
        .. BambooPole($"{v.Prefix}-right", (0, 0.01, 16 - DiagonalFiller), (DiagonalFiller, 15.99, 16), 'z', 'y', v.Right),
        .. BambooPole($"{v.Prefix}-join-left", (1, 0, 8 - DiagonalCorner), (3, 16, 9 - DiagonalCorner), 'x', 'y', v.Left).Select(e => OnDiagonal(e)),
        .. BambooPole($"{v.Prefix}-join-right", (1, 0, 7 + DiagonalCorner), (3, 16, 8 + DiagonalCorner), 'x', 'y', v.Right).Select(e => OnDiagonal(e)),
    ];

    private static readonly Element[] DiagonalElements =
    [
        .. BambooVariants.SelectMany(DiagonalBamboo),
        // A hair short of the cell's height, so a cap never shares a plane with the plate lying over it.
        new("framing-left", (16 - DiagonalFiller, 0.01, 0), (16, 15.99, DiagonalFiller), "framing", UvRule.Flat),
        new("framing-right", (0, 0.01, 16 - DiagonalFiller), (DiagonalFiller, 15.99, 16), "framing", UvRule.Flat),
        OnDiagonal(new("framing-join-left", (1, 0, 8 - DiagonalCorner), (3, 16, 9 - DiagonalCorner), "framing", UvRule.Flat)),
        OnDiagonal(new("framing-join-right", (1, 0, 7 + DiagonalCorner), (3, 16, 8 + DiagonalCorner), "framing", UvRule.Flat)),
        .. WallElements.Where(e => DiagonalFrame.Contains(e.Name)).SelectMany(e => new[]
        {
            DiagonalRun(e, -DiagonalEnd, 0),
            DiagonalRun(e, 0, DiagonalEnd),
            DiagonalRun(e, 1 - DiagonalCorner, -DiagonalEnd, "-left"),
            DiagonalRun(e, DiagonalEnd, DiagonalCorner - 1, "-right"),
        }),
        .. DiagonalStems(-DiagonalEnd, DiagonalEnd),
        .. DiagonalStems(1 - DiagonalCorner, -DiagonalEnd, "-left"),
        .. DiagonalStems(DiagonalEnd, DiagonalCorner - 1, "-right"),
        .. DiagonalFinish(-DiagonalEnd, DiagonalEnd),
        .. DiagonalFinish(-DiagonalCorner, -DiagonalEnd, "-left"),
        .. DiagonalFinish(DiagonalEnd, DiagonalCorner, "-right"),
        .. WallElements.Where(e => DiagonalJoined.Contains(e.Name)).SelectMany(e => new[]
        {
            DiagonalRun(InnerEnd(e, "south"), -DiagonalCorner, 1 - DiagonalCorner, "-join-left"),
            DiagonalRun(InnerEnd(e, "north"), DiagonalCorner - 1, DiagonalCorner, "-join-right"),
        }),
        .. DiagonalRims(-DiagonalEnd, DiagonalEnd),
        .. DiagonalRims(1 - DiagonalCorner, -DiagonalEnd, "-left"),
        .. DiagonalRims(DiagonalEnd, DiagonalCorner - 1, "-right"),
        .. DiagonalRims(-DiagonalCorner, 1 - DiagonalCorner, "-join-left"),
        .. DiagonalRims(DiagonalCorner - 1, DiagonalCorner, "-join-right"),
        .. DiagonalRims(-DiagonalCorner, 1 - DiagonalCorner, "-left", post: true),
        .. DiagonalRims(DiagonalCorner - 1, DiagonalCorner, "-right", post: true),
    ];

    private static readonly (string Slot, string Texture)[] DiagonalTextures =
        [.. WallTextures.Where(t => t.Slot is "front" or "framing" or "infill" or "back" or "deck" or "deckinfill" or "deckfront" or "deckback" or "lashing")
            .SelectMany(t => t.Slot.StartsWith("deck") ? [t, ("outer" + t.Slot, t.Texture)] : new[] { t })];

    private const double DeckHair = 0.02;

    // The outer triangle's strips are the inner's turned 180 degrees about the cell's centre (decision 0096).
    private static ((double X, double Y, double Z) From, (double X, double Y, double Z) To)[] OuterDiagonalDeckStrips()
        => [.. DiagonalDeckStrips().Select(Turned)];

    private static ((double X, double Y, double Z) From, (double X, double Y, double Z) To) Turned(
        ((double X, double Y, double Z) From, (double X, double Y, double Z) To) box)
        => ((16 - box.To.X, box.From.Y, 16 - box.To.Z), (16 - box.From.X, box.To.Y, 16 - box.From.Z));

    // Each half's top finish over its side of the panel's top, for a diagonal decked on both halves (decision 0096): a row
    // per strip, from where the two halves meet to 4 voxels on, past the panel's face at 2√2 from the line, so it lies on the deck.
    private static IEnumerable<Element> DiagonalCaps()
    {
        var inner = SidingWallBlock.AddOpenPartBoxes([], "diagonal", "west", "oak", null)
            .Select(b => ((b.X1 * 16.0, 15.0, b.Z1 * 16.0), (Math.Min(16, b.X1 * 16.0 + 4), 16.0, b.Z2 * 16.0))).ToArray();
        return CapGroups("cap", inner).Concat(CapGroups("outercap", [.. inner.Select(Turned)]));
    }

    // The outer deck's edge on each face the diagonal claims is the building's face, so a board in the deck's framing covers its
    // layers there (decision 0096), from the corner to the panel, a hair outside the cell so it shares no plane with them.
    private static IEnumerable<Element> OuterDeckFascia() =>
        from deckSide in DeckSides
        from box in new[] { ((-DeckHair, 12.0, 0.0), (0.0, 16.0, 16 - DiagonalFiller)), ((0.0, 12.0, -DeckHair), (16 - DiagonalFiller, 16.0, 0.0)) }
        let r = DeckRegion(box.Item1, box.Item2, deckSide.RotationYDeg)
        let face = r.X2 <= 0 ? "west" : r.X >= 16 ? "east" : r.Z2 <= 0 ? "north" : "south"
        select new Element($"outerdeck-{deckSide.Side}-fascia", (r.X, r.Y, r.Z), (r.X2, r.Y2, r.Z2), "outerdeck", UvRule.Positional,
            Faces: [face], RunAxis: face is "west" or "east" ? 'z' : 'x');

    // The strips of the block's own west diagonal deck, a hair off every plane the panel's unturned
    // pieces share with them: the top, under the plate and the fillers' caps, and the cell's own faces
    // in the two corners the fillers stand in. A strip that runs on past a corner is cut a voxel clear
    // of the filler, so the rest of it still meets the floor beside it.
    private static ((double X, double Y, double Z) From, (double X, double Y, double Z) To)[] DiagonalDeckStrips()
    {
        double corner = Math.Ceiling(DiagonalFiller);
        static double[] Cut(double lo, double hi, double at) => lo < at && at < hi ? [lo, at, hi] : [lo, hi];
        static double Voxels(float v) => Math.Round(v * 16.0, 3);
        static double Inset(double v) => Math.Clamp(v, DeckHair, 16 - DeckHair);

        var strips = new List<((double X, double Y, double Z) From, (double X, double Y, double Z) To)>();
        foreach (var box in SidingWallBlock.AddOpenPartBoxes([], "diagonal", "west", "oak", null))
        {
            double[] xs = Cut(Voxels(box.X1), Voxels(box.X2), corner);
            double[] zs = Cut(Voxels(box.Z1), Voxels(box.Z2), corner);
            for (int i = 0; i + 1 < xs.Length; i++)
            for (int j = 0; j + 1 < zs.Length; j++)
            {
                var (x, x2, z, z2) = (xs[i], xs[i + 1], zs[j], zs[j + 1]);
                if ((x >= 16 - corner && z2 <= corner) || (x2 <= corner && z >= 16 - corner))
                    (x, x2, z, z2) = (Inset(x), Inset(x2), Inset(z), Inset(z2));
                strips.Add(((x, Voxels(box.Y1), z), (x2, 16 - DeckHair, z2)));
            }
        }
        return [.. strips];
    }

    // The wall's rough frame laid flat: joists recessed a quarter voxel in y and cut in three along z with
    // the middle length swelling in x, stubs in the half voxel over the infill, and rims that keep the plain
    // boxes and run over the joist ends through cheeks in the two skins, lashed beside them. A cheek instead
    // of one proud box keeps two same-facing faces from sharing the plane at a rim's end.
    private static readonly Element[] FloorPoleElements =
    [
        new("poles-left", (2, 13.25, 0), (3, 14.75, 5), "framing", UvRule.Flat),
        new("poles-left", (2, 13.25, 5), (3.25, 14.75, 11), "framing", UvRule.Flat),
        new("poles-left", (2, 13.25, 11), (3, 14.75, 16), "framing", UvRule.Flat),
        new("poles-left", (7.5, 13.25, 0), (8.5, 14.75, 4), "framing", UvRule.Flat),
        new("poles-left", (7.25, 13.25, 4), (8.5, 14.75, 9), "framing", UvRule.Flat),
        new("poles-left", (7.5, 13.25, 9), (8.5, 14.75, 16), "framing", UvRule.Flat),
        new("poles-left", (0.5, 13, 7), (2, 13.5, 8), "framing", UvRule.Flat),
        new("poles-left", (3.25, 14.5, 9), (5, 15, 10), "framing", UvRule.Flat),
        new("poles-left", (8.5, 13, 5), (10.5, 13.5, 6), "framing", UvRule.Flat),
        new("poles-left", (5.75, 14.5, 11), (7.5, 15, 12), "framing", UvRule.Flat),
        new("poles-right", (13, 13.25, 0), (14, 14.75, 6), "framing", UvRule.Flat),
        new("poles-right", (13, 13.25, 6), (14.25, 14.75, 12), "framing", UvRule.Flat),
        new("poles-right", (13, 13.25, 12), (14, 14.75, 16), "framing", UvRule.Flat),
        new("poles-right", (11, 14.5, 8), (13, 15, 9), "framing", UvRule.Flat),
        new("poles-right", (14.25, 13, 10), (15.5, 13.5, 11), "framing", UvRule.Flat),
        .. PoleRim("poles-top", 0, 1, 1, 2.5),
        .. PoleRim("poles-bottom", 15, 16, 13.5, 15),
    ];

    private static readonly Element[] FloorPole2Elements =
    [
        // Second variant: cuts, stubs and their faces move, the swelling flips sides, the rims are the first one's renamed.
        new("poles2-left", (2, 13.25, 0), (3, 14.75, 4), "framing", UvRule.Flat),
        new("poles2-left", (1.75, 13.25, 4), (3, 14.75, 10), "framing", UvRule.Flat),
        new("poles2-left", (2, 13.25, 10), (3, 14.75, 16), "framing", UvRule.Flat),
        new("poles2-left", (7.5, 13.25, 0), (8.5, 14.75, 6), "framing", UvRule.Flat),
        new("poles2-left", (7.5, 13.25, 6), (8.75, 14.75, 12), "framing", UvRule.Flat),
        new("poles2-left", (7.5, 13.25, 12), (8.5, 14.75, 16), "framing", UvRule.Flat),
        new("poles2-left", (3, 13, 6), (4.75, 13.5, 7), "framing", UvRule.Flat),
        new("poles2-left", (0.25, 14.5, 8), (1.75, 15, 9), "framing", UvRule.Flat),
        new("poles2-left", (5.5, 13, 8), (7.5, 13.5, 9), "framing", UvRule.Flat),
        new("poles2-left", (8.5, 14.5, 4), (10.5, 15, 5), "framing", UvRule.Flat),
        new("poles2-right", (13, 13.25, 0), (14, 14.75, 5), "framing", UvRule.Flat),
        new("poles2-right", (12.75, 13.25, 5), (14, 14.75, 9), "framing", UvRule.Flat),
        new("poles2-right", (13, 13.25, 9), (14, 14.75, 16), "framing", UvRule.Flat),
        new("poles2-right", (10.75, 14.5, 6), (12.75, 15, 7), "framing", UvRule.Flat),
        new("poles2-right", (14, 13, 11), (15.25, 13.5, 12), "framing", UvRule.Flat),
        .. FloorPoleElements.Where(e => e.Name is "poles-top" or "poles-bottom").Select(e => e with { Name = e.Name.Replace("poles", "poles2") }),
    ];

    // The floor's frame in bamboo: ringed joists in the plain joists' boxes and ringed rim lengths between them, with the rough
    // frame's lashings where a rim meets a joist. Rings keep to z 2.75..13.25 on a joist, clear of those lashings,
    // and sit off the planes of the floor's stems. A rim's rings reach into the next cell, where its rim on the other edge
    // may stand, so every north rim's table keeps half a voxel from every south rim's.
    private static IEnumerable<Element> BambooFloor(string prefix, double[][] joists, double[][] topRims, double[][] bottomRims)
    {
        (double Lo, double Hi)[] rimX = [(0, 2), (3, 7.5), (8.5, 13), (14, 16)];
        IEnumerable<Element> Joists(string name, double[] rings, (double Lo, double Hi) x) => BambooPole(name, (x.Lo, 13, 0), (x.Hi, 15, 16), 'y', 'z', rings, ridgeInset: 0.1);
        IEnumerable<Element> Rims(string name, double z1, double z2, double[][] rings) => rimX
            .Select((x, i) => BambooPole(name, (x.Lo, 13, z1), (x.Hi, 15, z2), 'y', 'x', rings[i]).Select(e => e with { RunAxis = 'x' }))
            .SelectMany(e => e)
            .Concat(FloorPoleElements.Where(e => e.Name == "poles" + name[prefix.Length..] && e.Slot == "lashing").Select(e => e with { Name = name }));
        return
        [
            .. Joists($"{prefix}-left", joists[0], (2, 3)),
            .. Joists($"{prefix}-left", joists[1], (7.5, 8.5)),
            .. Joists($"{prefix}-right", joists[2], (13, 14)),
            .. Rims($"{prefix}-top", 0, 1, topRims),
            .. Rims($"{prefix}-bottom", 15, 16, bottomRims),
        ];
    }

    private static readonly Element[] FloorBambooElements =
    [
        .. BambooFloor("bamboo", [[3.25, 8.25, 12.25], [4.75, 10.25], [3.75, 9.75]], [[0.25], [4.75], [10.5], [14.75]], [[0.75], [3.75], [11.5], [14.25]]),
        .. BambooFloor("bamboo2", [[4.75, 10.75], [3.25, 8.75, 12.75], [5.25, 11.25]], [[1.25], [6.5], [9.5], [15.25]], [[0.75], [5.5], [12.25], [14.25]]),
    ];

    // The wall's layers laid flat: top finish, joists with infill between, underside, filling y 12..16
    // so the top sits where a plank block's would. Every floor's joists run north-south (along z) at
    // the same x, three to a cell and never more than 4.5 voxels apart, so they tile across any run
    // and read as a floor a player could not fall through. The rims across the north and south ends
    // are cut between the joists and drop where the next floor carries the joists on, as a stacked
    // wall drops its plates. The names are the wall's own, so SelectiveElements picks from either
    // table unchanged; framing-left holds two of the three joists since no floor ever drops one.
    private static readonly Element[] FloorElements =
    [
        new("front", (0, 15, 0), (16, 16, 16), "front", UvRule.Flat),
        new("framing-left", (2, 13, 0), (3, 15, 16), "framing", UvRule.Flat),
        new("framing-left", (7.5, 13, 0), (8.5, 15, 16), "framing", UvRule.Flat),
        new("framing-right", (13, 13, 0), (14, 15, 16), "framing", UvRule.Flat),
        .. Rim("framing-top", 0, 1),
        .. Rim("framing-bottom", 15, 16),
        .. FloorPoleElements,
        .. FloorPole2Elements,
        .. FloorBambooElements,
        new("infill-top", (0, 13.5, 0), (16, 14.5, 1), "infill", UvRule.Flat, RunAxis: 'z'),
        new("infill", (0, 13.5, 1), (16, 14.5, 15), "infill", UvRule.Flat, RunAxis: 'z'),
        new("infill-bottom", (0, 13.5, 15), (16, 14.5, 16), "infill", UvRule.Flat, RunAxis: 'z'),
        // Joined floors make one pole of their two halves over the seam.
        .. new (string Name, double Lo, double Hi)[] { ("stems-top", 0, 1), ("stems", 1, 15), ("stems-bottom", 15, 16) }
            .SelectMany(band => Stems(band.Name, band.Lo, band.Hi, 0, 16).Select(OnFloor)),
        new("back", (0, 12, 0), (16, 13, 16), "back", UvRule.Flat),
        new("infill-pane", (0, 14, 0), (16, 14, 16), "infill", UvRule.Flat, Faces: ["up", "down"]),
        // The wall's glazing frame laid flat, with no joists: stiles on the west and east edges, rails
        // between them on the north (top) and south (bottom), each dropped where the next floor is glazed
        // too, and a filler in each corner for a rail whose stile has gone. Every piece keeps all six
        // faces, unlike the wall's: a deck cuts these boxes, and a cut end has nothing standing against it.
        new("glazing-left", (0, 13, 0), (2, 15, 16), "framing", UvRule.Flat),
        new("glazing-right", (14, 13, 0), (16, 15, 16), "framing", UvRule.Flat),
        new("glazing-top", (2, 13, 0), (14, 15, 2), "framing", UvRule.Flat),
        new("glazing-bottom", (2, 13, 14), (14, 15, 16), "framing", UvRule.Flat),
        new("glazing-top-left", (0, 13, 0), (2, 15, 2), "framing", UvRule.Flat),
        new("glazing-top-right", (14, 13, 0), (16, 15, 2), "framing", UvRule.Flat),
        new("glazing-bottom-left", (0, 13, 14), (2, 15, 16), "framing", UvRule.Flat),
        new("glazing-bottom-right", (14, 13, 14), (16, 15, 16), "framing", UvRule.Flat),
        new("front-boards", (0, 15, 0), (16, 16, 16), "front", UvRule.Flat, RotatedFaces: ["up"]),
        new("front-hboards", (0, 15, 0), (16, 16, 16), "front", UvRule.Flat),
        new("back-boards", (0, 12, 0), (16, 13, 16), "back", UvRule.Flat, RotatedFaces: ["down"]),
        new("back-hboards", (0, 12, 0), (16, 13, 16), "back", UvRule.Flat),
        // The daub slab sits on battens two voxels apart, so the lath shows on the underside and
        // tiles across any run. The battens run the way the same style's floorboards would.
        new("back-lath-hboards", (0, 12.5, 0), (16, 13, 16), "back", UvRule.Flat),
        .. Enumerable.Range(0, 8).Select(i => new Element("back-lath-hboards", (0, 12, 2 * i + 0.5), (16, 12.5, 2 * i + 1.5), "framing", UvRule.Flat, RunAxis: 'x')),
        new("back-lath-boards", (0, 12.5, 0), (16, 13, 16), "back", UvRule.Flat),
        .. Enumerable.Range(0, 8).Select(i => new Element("back-lath-boards", (2 * i + 0.5, 12, 0), (2 * i + 1.5, 12.5, 16), "framing", UvRule.Flat, RunAxis: 'z')),
    ];

    // The floor's elements and its glazed finishes' rims, which a deck cuts from like any other piece.
    private static readonly Element[] FloorShape =
    [
        .. FloorElements, .. RimPieces(FloorElements, 'x', 'z'),
        .. FloorElements.Where(e => e.Name is "front" or "back").SelectMany(e => TilePieces(e, 'z', 'x')),
    ];

    private static IEnumerable<Element> Rim(string name, double z1, double z2) =>
        new[] { (0.0, 2.0), (3.0, 7.5), (8.5, 13.0), (14.0, 16.0) }
            .Select(x => new Element(name, (x.Item1, 13, z1), (x.Item2, 15, z2), "framing", UvRule.Flat, RunAxis: 'x'));

    // A pole rim: the plain boxes, a cheek in each skin across each joist, and lashing cheeks beside the rim.
    private static IEnumerable<Element> PoleRim(string name, double z1, double z2, double lashZ1, double lashZ2)
    {
        foreach (var (x1, x2) in new[] { (2.0, 3.0), (7.5, 8.5), (13.0, 14.0) })
        {
            foreach (var (y1, y2) in new[] { (13.0, 13.25), (14.75, 15.0) })
            {
                yield return new Element(name, (x1, y1, z1), (x2, y2, z2), "framing", UvRule.Flat);
                yield return new Element(name, (x1 - 0.25, y1, lashZ1), (x2 + 0.25, y2, lashZ2), "lashing", UvRule.Flat);
            }
        }
        foreach (var box in Rim(name, z1, z2)) yield return box;
    }

    private static readonly (string Slot, string Texture)[] FloorTextures =
    [
        ("framing", "game:block/wood/planks/oak1"),
        ("infill", "game:block/wood/planks/oak1"),
        ("front", "game:block/wood/planks/oak1"),
        ("back", "game:block/wood/planks/oak1"),
        ("lashing", "game:item/resource/rope"),
    ];

    private static readonly (string Side, int RotationYDeg)[] DeckSides = [("west", 0), ("south", 90), ("east", 180), ("north", 270)];

    private static readonly Dictionary<string, string> DeckSlots = new()
    {
        ["framing"] = "deck",
        ["infill"] = "deckinfill",
        ["front"] = "deckfront",
        ["back"] = "deckback",
        ["lashing"] = "lashing",
    };

    static WallShapeGen()
    {
        for (int n = 0; n < 4; n++)
        {
            DeckSlots[$"fronttile{n}"] = $"deckfronttile{n}";
            DeckSlots[$"backtile{n}"] = $"deckbacktile{n}";
        }
    }

    // The deck is the floor's layers clipped to the deck's area, one copy per side. The wall mesh is
    // tesselated turned by the side, but every floor's joists run north-south, so each copy is cut from
    // the floor to the area the side's deck box covers in world space and is tesselated unturned.
    // The box is turned about the cell centre the way SidingWallBlock.BuildDeckBoxes turns it.
    // A clipped face keeps the floor's uv where it sits, so the grain carries on from the floor beside.
    public static (double X, double Y, double Z, double X2, double Y2, double Z2) DeckRegion(
        (double X, double Y, double Z) from, (double X, double Y, double Z) to, int rotationYDeg)
    {
        double cos = Math.Round(Math.Cos(rotationYDeg * Math.PI / 180));
        double sin = Math.Round(Math.Sin(rotationYDeg * Math.PI / 180));
        var xs = new List<double>();
        var zs = new List<double>();
        foreach (double x in new[] { from.X, to.X })
        {
            foreach (double z in new[] { from.Z, to.Z })
            {
                xs.Add(8 + (x - 8) * cos + (z - 8) * sin);
                zs.Add(8 - (x - 8) * sin + (z - 8) * cos);
            }
        }
        return (xs.Min(), from.Y, zs.Min(), xs.Max(), to.Y, zs.Max());
    }

    // The deck fills the cell's open part. The ledge is the same copy for the slot between the deck and
    // the frame, where a wall's room-side finish goes; the deck reaches into it until that finish is laid.
    private static ((double X, double Y, double Z) From, (double X, double Y, double Z) To)[] Ledge(Element[] elements, string back = "back")
        => elements.Where(e => e.Name == back).Select(e => ((e.From.X, 12.0, e.From.Z), (e.To.X, 16.0, e.To.Z))).ToArray();

    // A glazed wall's room-side finish is a rim, and the top of it lies in the deck's height. The ledge takes
    // that part's place across the opening: a copy under each top piece of the rim, carried down to the
    // deck's underside, so it meets the rim's stiles edge to edge and overlaps none of it.
    private static IEnumerable<Element> RimLedges(Element[] elements, string back = "back", string prefix = "ledge")
        => new[] { "top", "top-left", "top-right" }.SelectMany(part => DeckGroups(
            prefix + part.Replace("-", ""),
            [.. elements.Where(e => e.Name == $"{back}-rim-{part}").Select(e => ((e.From.X, 12.0, e.From.Z), e.To))]));

    private static IEnumerable<Element> DeckGroups(
        string prefix, params ((double X, double Y, double Z) From, (double X, double Y, double Z) To)[] boxes)
    {
        foreach (var (side, rotationYDeg) in DeckSides)
        {
            var regions = boxes.Select(box => DeckRegion(box.From, box.To, rotationYDeg)).ToArray();
            foreach (var region in regions)
            foreach (var source in FloorShape)
            {
                var lo = (X: Math.Max(source.From.X, region.X), Y: Math.Max(source.From.Y, region.Y), Z: Math.Max(source.From.Z, region.Z));
                var hi = (X: Math.Min(source.To.X, region.X2), Y: Math.Min(source.To.Y, region.Y2), Z: Math.Min(source.To.Z, region.Z2));
                bool Empty(double l, double h, double sourceLo, double sourceHi) => l > h || (l == h && sourceLo != sourceHi);
                if (Empty(lo.X, hi.X, source.From.X, source.To.X)
                    || Empty(lo.Y, hi.Y, source.From.Y, source.To.Y)
                    || Empty(lo.Z, hi.Z, source.From.Z, source.To.Z)) continue;

                // Only a rim runs across an edge. Any other pole piece an edge cuts is a sliver of a joist,
                // or a stub, cheek or lashing whose joist is on the far side, so a box draws its part of one
                // only along the lengths where the side's boxes together cover the piece's whole width.
                var lengths = (source.Name.StartsWith("poles") || source.Name.StartsWith("bamboo")) && source.RunAxis != 'x'
                    ? CoveredLengths(source, regions, lo.Z, hi.Z)
                    : [(lo.Z, hi.Z)];
                foreach (var (z, z2) in lengths)
                {
                    yield return source with
                    {
                        Name = $"{prefix}-{side}-{source.Name}",
                        From = lo with { Z = z },
                        To = hi with { Z = z2 },
                        Slot = prefix.StartsWith("outer") && source.Slot != "lashing" ? "outer" + DeckSlots[source.Slot] : DeckSlots[source.Slot],
                        ClippedFrom = source,
                    };
                }
            }
        }
    }

    // An inside corner's post, 4 by 4 in the cell's west-north corner: a 2 by 2 framing core and a 1/16 layer on each face,
    // drawn in its finish or, bare, in the framing (`framing-<slot>`): front looks east, secondfront south, back west and
    // secondback north. Each layer owns the corner at one end of its run and meets the next layer round in a mitre at the
    // other, where its `-mitre` piece draws its outward face alone, so every face shows its own finish to the edge.
    private static readonly Element[] CornerInElements =
    [
        new("framing", (1, 0, 1), (3, 16, 3), "framing", UvRule.Flat),
        .. PostLayer("front", 'x', 4, "east", ownsHigh: true),
        .. PostLayer("secondfront", 'z', 4, "south", ownsHigh: false),
        .. PostLayer("back", 'x', 0, "west", ownsHigh: false),
        .. PostLayer("secondback", 'z', 0, "north", ownsHigh: true),
    ];

    // A bare post in bamboo: rings filling the 4 by 4, notched a half voxel down each vertical edge so they read round, and
    // between them the same pole a quarter voxel in all round, as a wall's pole stands inside its rings. The rings meet the
    // walls behind and a floor round the post edge to edge, and one closes the post's top, so a floor laid onto it leaves no
    // groove to see down. All of it is cut into a core and a slice in each layer's owned body (see PostLayer), so a
    // finished face drops its slice alone.
    private static IEnumerable<Element> BambooFatPost(BambooRings v)
    {
        (string Name, double X1, double Z1, double X2, double Z2)[] bodies =
        [
            (v.Prefix, 1, 1, 3, 3), ($"{v.Prefix}-front", 3, 1, 4, 4), ($"{v.Prefix}-secondfront", 0, 3, 3, 4),
            ($"{v.Prefix}-back", 0, 0, 1, 3), ($"{v.Prefix}-secondback", 1, 0, 4, 1),
        ];
        return from e in NotchedSquare(v.Prefix, 0.25, 3.75, 0.5, 0, 16).Concat(v.Post.Append(15.5).SelectMany(ring => FatPostRing(v.Prefix, ring, ring + 0.5)))
               from body in bodies
               let lo = (X: Math.Max(e.From.X, body.X1), Z: Math.Max(e.From.Z, body.Z1))
               let hi = (X: Math.Min(e.To.X, body.X2), Z: Math.Min(e.To.Z, body.Z2))
               where hi.X > lo.X && hi.Z > lo.Z
               select e with { Name = body.Name, From = e.From with { X = lo.X, Z = lo.Z }, To = e.To with { X = hi.X, Z = hi.Z }, ClippedFrom = e };
    }

    // The part of a fat post's ring standing out of the pole, so no ring box lies over the pole's own faces once both are cut
    // into the slices: a strip along each flat, and an L in each notch, the north-west one mirrored to the other three.
    private static IEnumerable<Element> FatPostRing(string name, double y1, double y2)
    {
        (double X1, double Z1, double X2, double Z2)[] strips = [(0.5, 0, 3.5, 0.25), (0.5, 3.75, 3.5, 4), (0, 0.5, 0.25, 3.5), (3.75, 0.5, 4, 3.5)];
        (double X1, double Z1, double X2, double Z2)[] notch = [(0.25, 0.5, 0.75, 0.75), (0.5, 0.25, 0.75, 0.5)];
        var corners = from n in notch
                      from mirror in new[] { (X: false, Z: false), (X: true, Z: false), (X: false, Z: true), (X: true, Z: true) }
                      select (mirror.X ? 4 - n.X2 : n.X1, mirror.Z ? 4 - n.Z2 : n.Z1, mirror.X ? 4 - n.X1 : n.X2, mirror.Z ? 4 - n.Z1 : n.Z2);
        return strips.Concat(corners).Select(b => new Element(name, (b.Item1, y1, b.Item2), (b.Item3, y2, b.Item4), "framing", UvRule.Flat));
    }

    private static IEnumerable<Element> PostLayer(string slot, char depthAxis, double outer, string outwardFace, bool ownsHigh)
    {
        char runAxis = depthAxis == 'x' ? 'z' : 'x';
        string ownedEnd = (runAxis, ownsHigh) switch { ('z', true) => "south", ('z', false) => "north", ('x', true) => "east", _ => "west" };
        (double Lo, double Hi) body = ownsHigh ? (1, 4) : (0, 3), mitre = ownsHigh ? (0, 1) : (3, 4);
        Element[] full =
        [
            PostSlab(slot, slot, depthAxis, outer),
            PostSlab($"framing-{slot}", "framing", depthAxis, outer),
            .. PostFinishes(slot, slot, depthAxis, outer, outwardFace),
            .. TilePieces(PostSlab(slot, slot, depthAxis, outer), 'y', runAxis, outwardFace),
        ];
        foreach (var e in full)
        {
            if (Cut(e, runAxis, body) is { } main)
            {
                bool atEnd = ownsHigh ? Axis(main.To, runAxis) == 4 : Axis(main.From, runAxis) == 0;
                yield return atEnd ? main with { Faces = [.. (e.Faces ?? AllFaces).Where(face => face != ownedEnd)] } : main;
            }
            if (Cut(e, runAxis, mitre) is { } piece)
                yield return piece with { Name = e.Name + "-mitre", Faces = [.. (e.Faces ?? AllFaces).Where(face => face == outwardFace)] };
        }
    }

    // The part of a post layer's element along one stretch of its run, sampling the texture where it sits.
    private static Element? Cut(Element e, char runAxis, (double Lo, double Hi) run)
    {
        double lo = Math.Max(Axis(e.From, runAxis), run.Lo), hi = Math.Min(Axis(e.To, runAxis), run.Hi);
        if (hi <= lo) return null;
        return e with
        {
            From = runAxis == 'x' ? e.From with { X = lo } : e.From with { Z = lo },
            To = runAxis == 'x' ? e.To with { X = hi } : e.To with { Z = hi },
            ClippedFrom = e,
        };
    }

    private static Element PostSlab(string name, string slot, char depthAxis, double outer) => new(name,
        Corner(depthAxis, outer == 4 ? 3 : 0, 0, 0), Corner(depthAxis, outer == 4 ? 4 : 1, 4, 16), slot, UvRule.Flat);

    // Every style a wall's face takes, on one of the post's layers: outer 4 on the east or south, 0 on the west or north,
    // and 1/16 deep, as a cornerout's back is.
    private static IEnumerable<Element> PostFinishes(string group, string slot, char depthAxis, double outer, string outwardFace)
    {
        char runAxis = depthAxis == 'x' ? 'z' : 'x';
        double inner = outer == 4 ? 3 : 1, dir = Math.Sign(inner - outer);
        Element Slab(string name, string[]? rotated = null) => PostSlab(name, slot, depthAxis, outer) with { RotatedFaces = rotated };

        var weatherboard = Enumerable.Range(0, 16).Select(course => new Element($"{group}-weatherboard",
            Corner(depthAxis, Math.Min(inner, outer + dir * 0.25 * (course % 4)), 0, course),
            Corner(depthAxis, Math.Max(inner, outer + dir * 0.25 * (course % 4)), 4, course + 1), slot, UvRule.Positional));

        // The wall's shakes table, a front at depth 0..1, turned onto the layer's run and mirrored for an outer of 4.
        var shakes = FrontShakesElements.Select(e =>
        {
            var turned = depthAxis == 'x' ? e : e with { From = (e.From.Z, e.From.Y, e.From.X), To = (e.To.Z, e.To.Y, e.To.X), RunAxis = 'x' };
            if (outer == 0) return turned with { Name = $"{group}-shakes", Slot = slot };
            return depthAxis == 'x' ? MirrorDepthX(turned, $"{group}-shakes", slot) : MirrorDepthZ(turned, $"{group}-shakes", slot);
        });

        return
        [
            .. WithBark(Slab($"{group}-hboards")),
            Slab($"{group}-boards", [outwardFace]),
            .. weatherboard,
            .. ClipRun(shakes, runAxis, 0, 4),
            .. Logs($"{group}-logs", slot, depthAxis, outer, inner, outwardFace, 0, 4),
            .. RunningBond($"{group}-brick", slot, 4, 8, depthAxis, outer, inner, 0, 4),
            .. RunningBond($"{group}-ashlar", slot, 8, 16, depthAxis, outer, inner, 0, 4, flipBond: true),
            .. RubbleGrid($"{group}-rubble", slot, depthAxis, outer, inner, 0, 4),
        ];
    }

    private static readonly (string Slot, string Texture)[] CornerInTextures =
    [
        ("framing", "game:block/wood/planks/aged/aged1"),
        ("front", "game:block/wood/planks/aged/aged1"),
        ("secondfront", "game:block/wood/planks/aged/aged1"),
        ("back", "game:block/wood/planks/aged/aged1"),
        ("secondback", "game:block/wood/planks/aged/aged1"),
        ("deck", "game:block/wood/planks/aged/aged1"),
        ("deckinfill", "game:block/wood/planks/aged/aged1"),
        ("deckfront", "game:block/wood/planks/aged/aged1"),
        ("deckback", "game:block/wood/planks/aged/aged1"),
        ("lashing", "game:item/resource/rope"),
    ];

    // The strips of the block's own west post deck, covering the cell around the post.
    private static ((double X, double Y, double Z) From, (double X, double Y, double Z) To)[] PostDeckStrips()
        => [.. SidingWallBlock.AddOpenPartBoxes([], "cornerin", "west", "oak", null)
            .Select(box => ((box.X1 * 16.0, box.Y1 * 16.0, box.Z1 * 16.0), (box.X2 * 16.0, box.Y2 * 16.0, box.Z2 * 16.0)))];

    // The lengths between two z over which the boxes, taken together, cover a piece from one side of its
    // width to the other. Two boxes that meet inside the width cover it between them.
    private static IEnumerable<(double Z, double Z2)> CoveredLengths(
        Element source, (double X, double Y, double Z, double X2, double Y2, double Z2)[] regions, double from, double to)
    {
        double[] cuts = [.. regions.SelectMany(r => new[] { r.Z, r.Z2 }).Where(z => z > from && z < to).Append(from).Append(to).Distinct().Order()];
        double? start = null;
        for (int i = 0; i + 1 < cuts.Length; i++)
        {
            double reach = source.From.X;
            foreach (var r in regions.Where(r => r.Z <= cuts[i] && r.Z2 >= cuts[i + 1]).OrderBy(r => r.X))
                if (r.X <= reach) reach = Math.Max(reach, r.X2);

            bool covered = reach >= source.To.X;
            if (covered) start ??= cuts[i];
            if (start != null && (!covered || i + 2 == cuts.Length))
            {
                yield return (start.Value, covered ? cuts[i + 1] : cuts[i]);
                start = null;
            }
        }
    }

    // How far the cap sits above the wall's own top, so its face lies over the slabs' and the frame's, which end at the same height.
    private const double CapRise = 0.05;

    // The deck's top finish laid over the wall's own 4/16 top, for when nothing stands on it: each finish group cut to the
    // wall's footprint, its top face alone, so the floor reads as one surface across the wall.
    private static IEnumerable<Element> CapGroups(string prefix, params ((double X, double Y, double Z) From, (double X, double Y, double Z) To)[] footprint)
        => DeckGroups(prefix, footprint.Select(box => (box.From with { Y = 15 }, box.To with { Y = 16 })).ToArray())
            .Where(e => e.Slot.Contains("deckfront") && !e.Name.Contains("-rim"))
            .Select(e => e with { Faces = ["up"], Offset = (0, CapRise, 0) });

    // A wall's or corner's own pieces, then its deck's: the deck in its box, and each of its ledges.
    private static Element[] WithDeck(Element[] elements, ((double X, double Y, double Z) From, (double X, double Y, double Z) To) deck)
        => [.. elements, .. DeckGroups("deck", deck), .. DeckGroups("ledge", Ledge(elements)), .. RimLedges(elements),
            .. DeckGroups("ledgesecond", Ledge(elements, "secondback")), .. RimLedges(elements, "secondback", "ledgesecond")];

    public static JObject Generate(string layout) => layout switch
    {
        "wall" => Emit([.. WithDeck([.. WallElements, .. RimPieces(WallElements), .. WallTiles(WallElements)], ((4, 12, 0), (16, 16, 16))), .. CapGroups("cap", ((0, 15, 0), (4, 16, 16)))], WallTextures),
        "floor" => Emit(FloorShape, FloorTextures),
        "cornerout" => Emit(
            [.. WithDeck(FrontEdgeOnSecondFace([.. CornerOutElements, .. CornerRimPieces(CornerOutElements), .. CornerTiles(CornerOutElements)]), ((4, 12, 4), (16, 16, 16))),
             .. CapGroups("cap", ((0, 15, 0), (4, 16, 16)), ((4, 15, 0), (16, 16, 4)))], CornerOutTextures),
        "diagonal" => Rounded(Emit([.. DiagonalElements, .. DeckGroups("deck", DiagonalDeckStrips()), .. DeckGroups("outerdeck", OuterDiagonalDeckStrips()),
            .. DiagonalCaps(), .. OuterDeckFascia()], DiagonalTextures)),
        "cornerin" => Emit([.. CornerInElements, .. BambooVariants.SelectMany(BambooFatPost), .. DeckGroups("deck", PostDeckStrips()), .. CapGroups("cap", ((0, 15, 0), (4, 16, 4)))], CornerInTextures),
        _ => throw new KeyNotFoundException(layout),
    };

    // The diagonal's lengths are irrational, and a sum of two of them carries float noise into the file.
    private static JObject Rounded(JObject shape)
    {
        foreach (var number in shape.Descendants().OfType<JValue>().Where(v => v.Type == JTokenType.Float).ToArray())
            number.Value = Math.Round((double)number.Value!, 4);
        return shape;
    }

    private static JObject Emit(Element[] elements, (string Slot, string Texture)[] textures)
    {
        var textureObject = new JObject();
        foreach (var (slot, texture) in textures) textureObject[slot] = texture;
        foreach (var slot in elements.SelectMany(e => e.FaceSlots?.Values ?? Enumerable.Empty<string>()).Concat(elements.Select(e => e.Slot)).Distinct().Where(slot => Regex.IsMatch(slot, @"tile\d$")).Order())
            textureObject[slot] = textureObject[Regex.Replace(slot, @"tile\d$", "")]!;

        return new JObject
        {
            ["textureWidth"] = 16,
            ["textureHeight"] = 16,
            ["textures"] = textureObject,
            ["elements"] = new JArray(elements.Select(e => EmitElement(e, elements))),
        };
    }

    internal static JObject EmitElement(Element element, Element[] elements)
    {
        var sameName = elements.Where(e => e.Name == element.Name).ToArray();

        var faces = new JObject();
        foreach (var face in element.Faces ?? AllFaces)
        {
            if (IsBuried(element, face, sameName)) continue;
            faces[face] = EmitFace(element, face);
        }

        var emitted = new JObject
        {
            ["name"] = element.Name,
            ["from"] = new JArray(element.From.X + element.Offset.X, element.From.Y + element.Offset.Y, element.From.Z + element.Offset.Z),
            ["to"] = new JArray(element.To.X + element.Offset.X, element.To.Y + element.Offset.Y, element.To.Z + element.Offset.Z),
        };
        if (element.RotationY != 0)
        {
            var origin = element.RotationOrigin ?? (8, 0, 8);
            emitted["rotationOrigin"] = new JArray(origin.X, origin.Y, origin.Z);
            emitted["rotationY"] = element.RotationY;
        }
        emitted["faces"] = faces;
        return emitted;
    }

    private static double Axis((double X, double Y, double Z) corner, char axis)
        => axis == 'x' ? corner.X : axis == 'y' ? corner.Y : corner.Z;

    // One name is one selectiveElements group, so boxes sharing a name are drawn together or not
    // at all - a face flat against one of them is never seen, whatever the cell is built from.
    // Cover has to be total: abutting shakes meet at a shared plane but sit at different depths,
    // and the recessed one leaves a strip of its neighbour's face showing.
    private static bool IsBuried(Element element, string face, Element[] sameName)
    {
        var (axis, high) = face switch
        {
            "north" => ('z', false),
            "south" => ('z', true),
            "west" => ('x', false),
            "east" => ('x', true),
            "down" => ('y', false),
            _ => ('y', true),
        };
        double plane = Axis(high ? element.To : element.From, axis);
        char[] across = axis == 'x' ? ['y', 'z'] : axis == 'y' ? ['x', 'z'] : ['x', 'y'];

        return sameName.Any(other => other != element
            && Axis(high ? other.From : other.To, axis) == plane
            && across.All(a => Axis(other.From, a) <= Axis(element.From, a)
                && Axis(other.To, a) >= Axis(element.To, a)));
    }

    private static string SlotOf(Element element, string face) => "#" + (element.FaceSlots?.GetValueOrDefault(face) ?? element.Slot);

    private static JObject EmitFace(Element element, string face)
    {
        if (element.Explicit is { } explicitFaces && explicitFaces.TryGetValue(face, out var vanilla))
        {
            var (e0, e1, e2, e3) = element.ClippedFrom is { } from ? ClippedUv(from, element, face) : (vanilla.U0, vanilla.V0, vanilla.U1, vanilla.V1);
            var explicitFace = new JObject { ["texture"] = SlotOf(element, face), ["uv"] = new JArray(e0, e1, e2, e3) };
            if (vanilla.Rotation != 0) explicitFace["rotation"] = vanilla.Rotation;
            return explicitFace;
        }

        var (fx, fy, fz) = element.From;
        var (tx, ty, tz) = element.To;

        var (uAxis, vAxis) = face switch
        {
            "north" or "south" => ('x', 'y'),
            "east" or "west" => ('z', 'y'),
            _ => ('x', 'z'),
        };

        double Lo(char axis) => axis == 'x' ? fx : axis == 'y' ? fy : fz;
        double Hi(char axis) => axis == 'x' ? tx : axis == 'y' ? ty : tz;

        bool positional = vAxis == 'y' &&
            (element.UvRule == UvRule.Positional || (element.PositionalOverrides?.Contains(face) ?? false));

        // A box cut out of a longer one has to keep sampling the texture where it sits, or every
        // segment restarts the same strip and the painted grain repeats across the run.
        (double, double) Span(char axis) =>
            element.RunAxis == axis ? (Lo(axis), Hi(axis)) : (0.0, Hi(axis) - Lo(axis));

        // A turned face lays its rect a quarter round (UvLayout), so u spans the axis v would have, and v the axis u would.
        bool rotated = element.RotatedFaces?.Contains(face) ?? false;
        var (u0, u1) = Span(rotated ? vAxis : uAxis);
        var (v0, v1) = positional ? (16 - ty, 16 - fy) : Span(rotated ? uAxis : vAxis);

        if (element.ClippedFrom is { } source)
            (u0, v0, u1, v1) = ClippedUv(source, element, face);

        var result = new JObject
        {
            ["texture"] = SlotOf(element, face),
            ["uv"] = new JArray(u0, v0, u1, v1),
        };
        // A face rotation turns the texture the opposite way to a texture's own rotation, so 270 on a side
        // face matches vanilla's upright planks (rotation 90), seam for seam. Floors keep 90.
        if (rotated) result["rotation"] = face is "up" or "down" ? 90 : 270;
        return result;
    }

    // How the game lays a face's uv rect over the face, read from ModelCubeUtilExt.AddFace: which axis
    // u and v run along, and whether they grow with it. A rotation of 90 turns the rect a quarter, so
    // u runs along z and v along x on the two faces that carry one. A side face's 270 is three of those
    // turns, which stands u up the wall and lays v along the run.
    private static (char UAxis, int USign, char VAxis, int VSign) UvLayout(string face, bool rotated) => (face, rotated) switch
    {
        ("east", true) => ('y', 1, 'z', -1),
        ("west", true) => ('y', 1, 'z', 1),
        ("north", true) => ('y', 1, 'x', -1),
        ("south", true) => ('y', 1, 'x', 1),
        ("north", _) => ('x', -1, 'y', -1),
        ("east", _) => ('z', -1, 'y', -1),
        ("south", _) => ('x', 1, 'y', -1),
        ("west", _) => ('z', 1, 'y', -1),
        ("up", false) => ('x', 1, 'z', 1),
        ("up", true) => ('z', 1, 'x', -1),
        ("down", false) => ('x', -1, 'z', 1),
        ("down", true) => ('z', 1, 'x', 1),
        _ => throw new ArgumentException(face),
    };

    // The part of the source face's uv rect the clipped box covers, so the clip samples the texels the
    // source would have drawn there, in proportion to the source's rect.
    private static (double, double, double, double) ClippedUv(Element source, Element clipped, string face)
    {
        var sourceUv = EmitFace(source, face)["uv"]!.Select(v => (double)v!).ToArray();
        var (uAxis, uSign, vAxis, vSign) = UvLayout(face, clipped.RotatedFaces?.Contains(face) ?? false);
        // A vanilla 180 turns the rect half way round, so both axes run against the face's own.
        if (source.Explicit?.GetValueOrDefault(face)?.Rotation == 180) (uSign, vSign) = (-uSign, -vSign);

        (double, double) Sub(double s0, double s1, char axis, int sign)
        {
            double perVoxel = (s1 - s0) / (Axis(source.To, axis) - Axis(source.From, axis));
            double a = (Axis(clipped.From, axis) - Axis(source.From, axis)) * perVoxel;
            double b = (Axis(clipped.To, axis) - Axis(source.From, axis)) * perVoxel;
            return sign > 0 ? (s0 + a, s0 + b) : (s1 - b, s1 - a);
        }

        var (u0, u1) = Sub(sourceUv[0], sourceUv[2], uAxis, uSign);
        var (v0, v1) = Sub(sourceUv[1], sourceUv[3], vAxis, vSign);
        return (u0, v0, u1, v1);
    }
}
