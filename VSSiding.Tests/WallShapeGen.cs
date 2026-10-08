using System;
using System.Collections.Generic;
using System.Linq;
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
    Element? ClippedFrom = null);

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

    private static readonly Element[] WallElements =
    [
        new("front", (0, 0, 0), (1, 16, 16), "front", UvRule.Flat),
        new("framing-top", (1, 15, 1), (3, 16, 15), "framing", UvRule.Flat),
        new("framing-bottom", (1, 0, 1), (3, 1, 15), "framing", UvRule.Flat),
        new("framing-left", (1, 0, 0), (3, 16, 1), "framing", UvRule.Flat),
        new("framing-right", (1, 0, 15), (3, 16, 16), "framing", UvRule.Flat),
        .. WallPoleElements,
        .. WallPole2Elements,
        new("infill-top", (1.5, 15, 1), (2.5, 16, 15), "infill", UvRule.Positional),
        new("infill", (1.5, 1, 1), (2.5, 15, 15), "infill", UvRule.Positional),
        new("infill-bottom", (1.5, 0, 1), (2.5, 1, 15), "infill", UvRule.Positional),
        new("infill-pane", (2, 0, 0), (2, 16, 16), "infill", UvRule.Flat, Faces: ["west", "east"]),
        new("glazing-left", (1, 0, 0.25), (3, 16, 2), "framing", UvRule.Flat),
        new("glazing-right", (1, 0, 14), (3, 16, 15.75), "framing", UvRule.Flat),
        new("glazing-top", (1.25, 14, 0), (2.75, 15.75, 16), "framing", UvRule.Flat),
        new("glazing-bottom", (1.25, 0.25, 0), (2.75, 2, 16), "framing", UvRule.Flat),
        new("back", (3, 0, 0), (4, 16, 16), "back", UvRule.Flat),
        // A step draws two of these: a full-length half plus the opposite-height half on the side it rises toward.
        new("step-lower", (4, 0, 0), (16, 8, 16), "step", UvRule.Flat),
        new("step-upper", (4, 8, 0), (16, 16, 16), "step", UvRule.Flat),
        new("step-lower-north", (4, 0, 0), (16, 8, 8), "step", UvRule.Flat),
        new("step-lower-south", (4, 0, 8), (16, 8, 16), "step", UvRule.Flat),
        new("step-upper-north", (4, 8, 0), (16, 16, 8), "step", UvRule.Flat),
        new("step-upper-south", (4, 8, 8), (16, 16, 16), "step", UvRule.Flat),
        new("back-boards", (3, 0, 0), (4, 16, 16), "back", UvRule.Flat, RotatedFaces: ["east"]),
        new("front-boards", (0, 0, 0), (1, 16, 16), "front", UvRule.Flat, RotatedFaces: ["west"]),
        new("back-hboards", (3, 0, 0), (4, 16, 16), "back", UvRule.Flat),
        new("front-hboards", (0, 0, 0), (1, 16, 16), "front", UvRule.Flat),
        new("back-bark", (3, 0, 0), (4, 16, 16), "back", UvRule.Flat),
        new("front-bark", (0, 0, 0), (1, 16, 16), "front", UvRule.Flat),
        new("back-hbark", (3, 0, 0), (4, 16, 16), "back", UvRule.Flat),
        new("front-hbark", (0, 0, 0), (1, 16, 16), "front", UvRule.Flat),
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
    ];

    private static readonly Element[] CornerOutElements =
    [
        new("front", (0, 0, 0), (1, 16, 16), "front", UvRule.Flat),
        new("framing-top", (1, 15, 3), (3, 16, 15), "framing", UvRule.Flat),
        new("framing-bottom", (1, 0, 3), (3, 1, 15), "framing", UvRule.Flat),
        new("framing", (1, 0, 1), (3, 16, 3), "framing", UvRule.Flat),
        new("infill-top", (1.5, 15, 3), (2.5, 16, 15), "infill", UvRule.Positional),
        new("infill", (1.5, 1, 3), (2.5, 15, 15), "infill", UvRule.Positional),
        new("infill-bottom", (1.5, 0, 3), (2.5, 1, 15), "infill", UvRule.Positional),
        new("framing", (1, 0, 15), (3, 16, 16), "framing", UvRule.Flat),
        new("back", (3, 0, 4), (4, 16, 16), "back", UvRule.Flat),
        new("secondfront", (1, 0, 0), (16, 16, 1), "secondfront", UvRule.Flat),
        new("framing-top", (3, 15, 1), (15, 16, 3), "framing", UvRule.Flat),
        new("framing-bottom", (3, 0, 1), (15, 1, 3), "framing", UvRule.Flat),
        new("infill-top", (3, 15, 1.5), (15, 16, 2.5), "infill", UvRule.Positional),
        new("infill", (3, 1, 1.5), (15, 15, 2.5), "infill", UvRule.Positional),
        new("infill-bottom", (3, 0, 1.5), (15, 1, 2.5), "infill", UvRule.Positional),
        new("infill-pane", (2, 0, 3), (2, 16, 16), "infill", UvRule.Flat, Faces: ["west", "east"]),
        new("infill-pane", (3, 0, 2), (16, 16, 2), "infill", UvRule.Flat, Faces: ["north", "south"]),
        new("framing", (15, 0, 1), (16, 16, 3), "framing", UvRule.Flat),
        .. CornerPoleElements,
        .. CornerPole2Elements,
        new("back", (3, 0, 3), (16, 16, 4), "back", UvRule.Flat),
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
        new("back-boards", (3, 0, 3), (16, 16, 4), "back", UvRule.Flat, null, null, ["south"]),
        new("front-boards", (0, 0, 0), (1, 16, 16), "front", UvRule.Flat, RotatedFaces: ["west"]),
        new("secondfront-boards", (1, 0, 0), (16, 16, 1), "secondfront", UvRule.Flat, null, null, ["north"]),
        new("back-hboards", (3, 0, 4), (4, 16, 16), "back", UvRule.Flat),
        new("back-hboards", (3, 0, 3), (16, 16, 4), "back", UvRule.Flat),
        new("front-hboards", (0, 0, 0), (1, 16, 16), "front", UvRule.Flat),
        new("secondfront-hboards", (1, 0, 0), (16, 16, 1), "secondfront", UvRule.Flat),
        new("back-bark", (3, 0, 4), (4, 16, 16), "back", UvRule.Flat),
        new("back-bark", (3, 0, 3), (16, 16, 4), "back", UvRule.Flat),
        new("front-bark", (0, 0, 0), (1, 16, 16), "front", UvRule.Flat),
        new("secondfront-bark", (1, 0, 0), (16, 16, 1), "secondfront", UvRule.Flat),
        new("back-hbark", (3, 0, 4), (4, 16, 16), "back", UvRule.Flat),
        new("back-hbark", (3, 0, 3), (16, 16, 4), "back", UvRule.Flat),
        new("front-hbark", (0, 0, 0), (1, 16, 16), "front", UvRule.Flat),
        new("secondfront-hbark", (1, 0, 0), (16, 16, 1), "secondfront", UvRule.Flat),
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
        // A back group covers both legs, as back-brick does below.
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
        // A back group covers both legs, the way back-logs does.
        .. RunningBond("back-brick", "back", 4, 8, 'x', 4, 3, 4, 16),
        .. RunningBond("back-brick", "back", 4, 8, 'z', 4, 3, 3, 16),
        .. RunningBond("back-ashlar", "back", 8, 16, 'x', 4, 3, 4, 16, flipBond: true),
        .. RunningBond("back-ashlar", "back", 8, 16, 'z', 4, 3, 3, 16, flipBond: true),
        .. RubbleGrid("front-rubble", "front", 'x', 0, 1, 0, 16),
        .. RubbleGrid("secondfront-rubble", "secondfront", 'z', 0, 1, 1, 16),
        .. RubbleGrid("back-rubble", "back", 'x', 4, 3, 4, 16),
        .. RubbleGrid("back-rubble", "back", 'z', 4, 3, 3, 16),
    ];

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
        ("back", "game:block/wood/planks/aged/aged1"),
        ("deck", "game:block/wood/planks/aged/aged1"),
        ("deckinfill", "game:block/wood/planks/aged/aged1"),
        ("deckfront", "game:block/wood/planks/aged/aged1"),
        ("deckback", "game:block/wood/planks/aged/aged1"),
        ("lashing", "game:item/resource/rope"),
    ];

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
        new("infill-top", (0, 13.5, 0), (16, 14.5, 1), "infill", UvRule.Flat, RunAxis: 'z'),
        new("infill", (0, 13.5, 1), (16, 14.5, 15), "infill", UvRule.Flat, RunAxis: 'z'),
        new("infill-bottom", (0, 13.5, 15), (16, 14.5, 16), "infill", UvRule.Flat, RunAxis: 'z'),
        new("back", (0, 12, 0), (16, 13, 16), "back", UvRule.Flat),
        new("infill-pane", (0, 14, 0), (16, 14, 16), "infill", UvRule.Flat, Faces: ["up", "down"]),
        // The wall's glazing bezel laid flat, with no joists: stiles on the west and east edges,
        // rails on the north (top) and south (bottom), each dropped where the next floor is glazed too.
        new("glazing-left", (0.25, 13, 0), (2, 15, 16), "framing", UvRule.Flat),
        new("glazing-right", (14, 13, 0), (15.75, 15, 16), "framing", UvRule.Flat),
        new("glazing-top", (0, 13.25, 0.25), (16, 14.75, 2), "framing", UvRule.Flat),
        new("glazing-bottom", (0, 13.25, 14), (16, 14.75, 15.75), "framing", UvRule.Flat),
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
    private static ((double X, double Y, double Z) From, (double X, double Y, double Z) To)[] Ledge(Element[] elements)
        => elements.Where(e => e.Name == "back").Select(e => ((e.From.X, 12.0, e.From.Z), (e.To.X, 16.0, e.To.Z))).ToArray();

    private static IEnumerable<Element> DeckGroups(
        string prefix, params ((double X, double Y, double Z) From, (double X, double Y, double Z) To)[] boxes)
    {
        foreach (var (side, rotationYDeg) in DeckSides)
        foreach (var (from, to) in boxes)
        {
            var region = DeckRegion(from, to, rotationYDeg);
            foreach (var source in FloorElements)
            {
                var lo = (X: Math.Max(source.From.X, region.X), Y: Math.Max(source.From.Y, region.Y), Z: Math.Max(source.From.Z, region.Z));
                var hi = (X: Math.Min(source.To.X, region.X2), Y: Math.Min(source.To.Y, region.Y2), Z: Math.Min(source.To.Z, region.Z2));
                bool Empty(double l, double h, double sourceLo, double sourceHi) => l > h || (l == h && sourceLo != sourceHi);
                if (Empty(lo.X, hi.X, source.From.X, source.To.X)
                    || Empty(lo.Y, hi.Y, source.From.Y, source.To.Y)
                    || Empty(lo.Z, hi.Z, source.From.Z, source.To.Z)) continue;
                // Only a rim runs across an edge. Any other pole piece an edge cuts is a sliver of a joist,
                // or a stub, cheek or lashing whose joist is on the far side.
                if (source.Name.StartsWith("poles") && source.RunAxis != 'x' && (lo.X, hi.X) != (source.From.X, source.To.X)) continue;

                yield return source with
                {
                    Name = $"{prefix}-{side}-{source.Name}",
                    From = lo,
                    To = hi,
                    Slot = DeckSlots[source.Slot],
                    ClippedFrom = source,
                };
            }
        }
    }

    public static JObject Generate(string layout) => layout switch
    {
        "wall" => Emit([.. WallElements, .. DeckGroups("deck", ((4, 12, 0), (16, 16, 16))), .. DeckGroups("ledge", Ledge(WallElements))], WallTextures),
        "floor" => Emit(FloorElements, FloorTextures),
        "cornerout" => Emit(
            [.. CornerOutElements, .. DeckGroups("deck", ((4, 12, 4), (16, 16, 16))), .. DeckGroups("ledge", Ledge(CornerOutElements))], CornerOutTextures),
        _ => throw new KeyNotFoundException(layout),
    };

    private static JObject Emit(Element[] elements, (string Slot, string Texture)[] textures)
    {
        var textureObject = new JObject();
        foreach (var (slot, texture) in textures) textureObject[slot] = texture;

        return new JObject
        {
            ["textureWidth"] = 16,
            ["textureHeight"] = 16,
            ["textures"] = textureObject,
            ["elements"] = new JArray(elements.Select(e => EmitElement(e, elements))),
        };
    }

    private static JObject EmitElement(Element element, Element[] elements)
    {
        var sameName = elements.Where(e => e.Name == element.Name).ToArray();

        var faces = new JObject();
        foreach (var face in element.Faces ?? AllFaces)
        {
            if (IsBuried(element, face, sameName)) continue;
            faces[face] = EmitFace(element, face);
        }

        return new JObject
        {
            ["name"] = element.Name,
            ["from"] = new JArray(element.From.X, element.From.Y, element.From.Z),
            ["to"] = new JArray(element.To.X, element.To.Y, element.To.Z),
            ["faces"] = faces,
        };
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

    private static JObject EmitFace(Element element, string face)
    {
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

        var (u0, u1) = Span(uAxis);
        var (v0, v1) = positional ? (16 - ty, 16 - fy) : Span(vAxis);

        if (element.ClippedFrom is { } source)
            (u0, v0, u1, v1) = ClippedUv(source, element, face);

        var result = new JObject
        {
            ["texture"] = "#" + element.Slot,
            ["uv"] = new JArray(u0, v0, u1, v1),
        };
        // A face rotation turns the texture the opposite way to a texture's own rotation, so 270 on a side
        // face matches vanilla's upright planks (rotation 90), seam for seam. Floors keep 90.
        if (element.RotatedFaces?.Contains(face) ?? false) result["rotation"] = face is "up" or "down" ? 90 : 270;
        return result;
    }

    // How the game lays a face's uv rect over the face, read from ModelCubeUtilExt.AddFace: which axis
    // u and v run along, and whether they grow with it. A rotation of 90 turns the rect a quarter, so
    // u runs along z and v along x on the two faces that carry one.
    private static (char UAxis, int USign, char VAxis, int VSign) UvLayout(string face, bool rotated) => (face, rotated) switch
    {
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
    // source would have drawn there.
    private static (double, double, double, double) ClippedUv(Element source, Element clipped, string face)
    {
        var sourceUv = EmitFace(source, face)["uv"]!.Select(v => (double)v!).ToArray();
        var (uAxis, uSign, vAxis, vSign) = UvLayout(face, clipped.RotatedFaces?.Contains(face) ?? false);

        (double, double) Sub(double s0, double s1, char axis, int sign)
        {
            double a = Axis(clipped.From, axis) - Axis(source.From, axis);
            double b = Axis(clipped.To, axis) - Axis(source.From, axis);
            return sign > 0 ? (s0 + a, s0 + b) : (s1 - b, s1 - a);
        }

        var (u0, u1) = Sub(sourceUv[0], sourceUv[2], uAxis, uSign);
        var (v0, v1) = Sub(sourceUv[1], sourceUv[3], vAxis, vSign);
        return (u0, v0, u1, v1);
    }
}
