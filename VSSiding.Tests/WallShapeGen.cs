using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace VSSiding.Tests;

// Every uv in wall.json and cornerout.json derives from the element's box, per decision 0021.
// Each face reads two of the three axes - north/south (x, y), east/west (z, y), up/down (x, z) -
// and Flat pins that pair at 0. Positional instead maps v to the wall's height (16 - y), so painted
// courses land on the stepped lips and carry across stacked walls; up/down stay Flat either way.
// RunAxis names the axis a group runs along. A box cut out of a longer one then samples the texture
// at its own position on that axis, so a course split into segments keeps one continuous strip.
// Three cases need more: back-boards rotates its outward face 90 to stand plank grain upright,
// back-logs position-maps only its outward face, and infill-pane draws two of the six faces.
public enum UvRule
{
    Flat,
    Positional,
    Course,
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
    char? RunAxis = null);

public static class WallShapeGen
{
    // Both cladding textures draw a course every 4 voxels of a 16-voxel face.
    private const double CoursePitch = 4;

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

    private static readonly string[] AllFaces = ["north", "east", "south", "west", "up", "down"];

    private static readonly Element[] WallElements =
    [
        new("front", (0, 0, 0), (1, 16, 16), "front", UvRule.Flat),
        new("framing-top", (1, 15, 1), (3, 16, 15), "framing", UvRule.Flat),
        new("framing-bottom", (1, 0, 1), (3, 1, 15), "framing", UvRule.Flat),
        new("framing-left", (1, 0, 0), (3, 16, 1), "framing", UvRule.Flat),
        new("framing-right", (1, 0, 15), (3, 16, 16), "framing", UvRule.Flat),
        new("infill-top", (1.5, 15, 1), (2.5, 16, 15), "infill", UvRule.Flat),
        new("infill", (1.5, 1, 1), (2.5, 15, 15), "infill", UvRule.Flat),
        new("infill-bottom", (1.5, 0, 1), (2.5, 1, 15), "infill", UvRule.Flat),
        new("infill-pane", (2, 0, 0), (2, 16, 16), "infill", UvRule.Flat, Faces: ["west", "east"]),
        new("glazing-left", (1, 0, 0.25), (3, 16, 2), "framing", UvRule.Flat),
        new("glazing-right", (1, 0, 14), (3, 16, 15.75), "framing", UvRule.Flat),
        new("glazing-top", (1.25, 14, 0), (2.75, 15.75, 16), "framing", UvRule.Flat),
        new("glazing-bottom", (1.25, 0.25, 0), (2.75, 2, 16), "framing", UvRule.Flat),
        new("back", (3, 0, 0), (4, 16, 16), "back", UvRule.Flat),
        new("back-boards", (3, 0, 0), (4, 16, 16), "back", UvRule.Flat, RotatedFaces: ["east"]),
        new("front-weatherboard", (0, 0, 0), (1, 1, 16), "front", UvRule.Course),
        new("front-weatherboard", (0.25, 1, 0), (1, 2, 16), "front", UvRule.Course),
        new("front-weatherboard", (0.5, 2, 0), (1, 3, 16), "front", UvRule.Course),
        new("front-weatherboard", (0.75, 3, 0), (1, 4, 16), "front", UvRule.Course),
        new("front-weatherboard", (0, 4, 0), (1, 5, 16), "front", UvRule.Course),
        new("front-weatherboard", (0.25, 5, 0), (1, 6, 16), "front", UvRule.Course),
        new("front-weatherboard", (0.5, 6, 0), (1, 7, 16), "front", UvRule.Course),
        new("front-weatherboard", (0.75, 7, 0), (1, 8, 16), "front", UvRule.Course),
        new("front-weatherboard", (0, 8, 0), (1, 9, 16), "front", UvRule.Course),
        new("front-weatherboard", (0.25, 9, 0), (1, 10, 16), "front", UvRule.Course),
        new("front-weatherboard", (0.5, 10, 0), (1, 11, 16), "front", UvRule.Course),
        new("front-weatherboard", (0.75, 11, 0), (1, 12, 16), "front", UvRule.Course),
        new("front-weatherboard", (0, 12, 0), (1, 13, 16), "front", UvRule.Course),
        new("front-weatherboard", (0.25, 13, 0), (1, 14, 16), "front", UvRule.Course),
        new("front-weatherboard", (0.5, 14, 0), (1, 15, 16), "front", UvRule.Course),
        new("front-weatherboard", (0.75, 15, 0), (1, 16, 16), "front", UvRule.Course),
        // Weatherboard's lap (proud butt, recessed body) stays, but each course is cut into four
        // shakes along its run, and a shake keeps its depth through both bands - butt at d, body at
        // 0.5 + d - so it reads as one tile rather than a banded strip. Depths run 0 to 0.3 of a
        // voxel, recessed never proud, and the boundaries move course to course for the stagger.
        // The painted joints are irregular, so the shakes abut and the depth step alone carries the
        // joint; a gap would open a line straight into the wall.
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
        new("back-logs", (3, 0, 0), (3.5, 16, 16), "back", UvRule.Flat),
        new("back-logs", (3.5, 0.5, 0), (4, 3.5, 16), "back", UvRule.Flat, PositionalOverrides: ["east"]),
        new("back-logs", (3.5, 4.5, 0), (4, 7.5, 16), "back", UvRule.Flat, PositionalOverrides: ["east"]),
        new("back-logs", (3.5, 8.5, 0), (4, 11.5, 16), "back", UvRule.Flat, PositionalOverrides: ["east"]),
        new("back-logs", (3.5, 12.5, 0), (4, 15.5, 16), "back", UvRule.Flat, PositionalOverrides: ["east"]),
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

    private static readonly Element[] CornerOutElements =
    [
        new("front", (0, 0, 0), (1, 16, 16), "front", UvRule.Flat),
        new("framing-top", (1, 15, 3), (3, 16, 15), "framing", UvRule.Flat),
        new("framing-bottom", (1, 0, 3), (3, 1, 15), "framing", UvRule.Flat),
        new("framing", (1, 0, 1), (3, 16, 3), "framing", UvRule.Flat),
        new("infill-top", (1.5, 15, 3), (2.5, 16, 15), "infill", UvRule.Flat),
        new("infill", (1.5, 1, 3), (2.5, 15, 15), "infill", UvRule.Flat),
        new("infill-bottom", (1.5, 0, 3), (2.5, 1, 15), "infill", UvRule.Flat),
        new("framing", (1, 0, 15), (3, 16, 16), "framing", UvRule.Flat),
        new("back", (3, 0, 4), (4, 16, 16), "back", UvRule.Flat),
        new("secondfront", (1, 0, 0), (16, 16, 1), "secondfront", UvRule.Flat),
        new("framing-top", (3, 15, 1), (15, 16, 3), "framing", UvRule.Flat),
        new("framing-bottom", (3, 0, 1), (15, 1, 3), "framing", UvRule.Flat),
        new("infill-top", (3, 15, 1.5), (15, 16, 2.5), "infill", UvRule.Flat),
        new("infill", (3, 1, 1.5), (15, 15, 2.5), "infill", UvRule.Flat),
        new("infill-bottom", (3, 0, 1.5), (15, 1, 2.5), "infill", UvRule.Flat),
        new("infill-pane", (2, 0, 3), (2, 16, 16), "infill", UvRule.Flat, Faces: ["west", "east"]),
        new("infill-pane", (3, 0, 2), (16, 16, 2), "infill", UvRule.Flat, Faces: ["north", "south"]),
        new("framing", (15, 0, 1), (16, 16, 3), "framing", UvRule.Flat),
        new("back", (3, 0, 3), (16, 16, 4), "back", UvRule.Flat),
        new("front-weatherboard", (0, 0, 0), (1, 1, 16), "front", UvRule.Course),
        new("front-weatherboard", (0.25, 1, 0.5), (1, 2, 16), "front", UvRule.Course),
        new("front-weatherboard", (0.5, 2, 0.5), (1, 3, 16), "front", UvRule.Course),
        new("front-weatherboard", (0.75, 3, 0.5), (1, 4, 16), "front", UvRule.Course),
        new("secondfront-weatherboard", (1, 0, 0), (16, 1, 1), "secondfront", UvRule.Course),
        new("secondfront-weatherboard", (1, 1, 0.25), (16, 2, 1), "secondfront", UvRule.Course),
        new("secondfront-weatherboard", (1, 2, 0.5), (16, 3, 1), "secondfront", UvRule.Course),
        new("secondfront-weatherboard", (1, 3, 0.75), (16, 4, 1), "secondfront", UvRule.Course),
        new("front-weatherboard", (0, 4, 0), (1, 5, 16), "front", UvRule.Course),
        new("front-weatherboard", (0.25, 5, 0.5), (1, 6, 16), "front", UvRule.Course),
        new("front-weatherboard", (0.5, 6, 0.5), (1, 7, 16), "front", UvRule.Course),
        new("front-weatherboard", (0.75, 7, 0.5), (1, 8, 16), "front", UvRule.Course),
        new("secondfront-weatherboard", (1, 4, 0), (16, 5, 1), "secondfront", UvRule.Course),
        new("secondfront-weatherboard", (1, 5, 0.25), (16, 6, 1), "secondfront", UvRule.Course),
        new("secondfront-weatherboard", (1, 6, 0.5), (16, 7, 1), "secondfront", UvRule.Course),
        new("secondfront-weatherboard", (1, 7, 0.75), (16, 8, 1), "secondfront", UvRule.Course),
        new("front-weatherboard", (0, 8, 0), (1, 9, 16), "front", UvRule.Course),
        new("front-weatherboard", (0.25, 9, 0.5), (1, 10, 16), "front", UvRule.Course),
        new("front-weatherboard", (0.5, 10, 0.5), (1, 11, 16), "front", UvRule.Course),
        new("front-weatherboard", (0.75, 11, 0.5), (1, 12, 16), "front", UvRule.Course),
        new("secondfront-weatherboard", (1, 8, 0), (16, 9, 1), "secondfront", UvRule.Course),
        new("secondfront-weatherboard", (1, 9, 0.25), (16, 10, 1), "secondfront", UvRule.Course),
        new("secondfront-weatherboard", (1, 10, 0.5), (16, 11, 1), "secondfront", UvRule.Course),
        new("secondfront-weatherboard", (1, 11, 0.75), (16, 12, 1), "secondfront", UvRule.Course),
        new("front-weatherboard", (0, 12, 0), (1, 13, 16), "front", UvRule.Course),
        new("front-weatherboard", (0.25, 13, 0.5), (1, 14, 16), "front", UvRule.Course),
        new("front-weatherboard", (0.5, 14, 0.5), (1, 15, 16), "front", UvRule.Course),
        new("front-weatherboard", (0.75, 15, 0.5), (1, 16, 16), "front", UvRule.Course),
        new("secondfront-weatherboard", (1, 12, 0), (16, 13, 1), "secondfront", UvRule.Course),
        new("secondfront-weatherboard", (1, 13, 0.25), (16, 14, 1), "secondfront", UvRule.Course),
        new("secondfront-weatherboard", (1, 14, 0.5), (16, 15, 1), "secondfront", UvRule.Course),
        new("secondfront-weatherboard", (1, 15, 0.75), (16, 16, 1), "secondfront", UvRule.Course),
        new("back-boards", (3, 0, 4), (4, 16, 16), "back", UvRule.Flat, RotatedFaces: ["east"]),
        new("back-boards", (3, 0, 3), (16, 16, 4), "back", UvRule.Flat, null, null, ["south"]),
        // Same tiling as wall's front-shakes (see the comment there), on both legs. The front leg
        // runs along z with depth along x; the second leg runs along x with depth along z, so its
        // shakes recess from z 0 - the outward face - and its body sits at 0.5 + d the same way.
        // Each leg's RunAxis is what keeps the texture running across its own split courses.
        new("front-shakes", (0, 0, 0), (1, 1, 5), "front", UvRule.Positional, RunAxis: 'z'),
        new("front-shakes", (0.2, 0, 5), (1, 1, 9), "front", UvRule.Positional, RunAxis: 'z'),
        new("front-shakes", (0.1, 0, 9), (1, 1, 13), "front", UvRule.Positional, RunAxis: 'z'),
        new("front-shakes", (0.3, 0, 13), (1, 1, 16), "front", UvRule.Positional, RunAxis: 'z'),
        new("front-shakes", (0.5, 1, 0.5), (1, 4, 5), "front", UvRule.Positional, RunAxis: 'z'),
        new("front-shakes", (0.7, 1, 5), (1, 4, 9), "front", UvRule.Positional, RunAxis: 'z'),
        new("front-shakes", (0.6, 1, 9), (1, 4, 13), "front", UvRule.Positional, RunAxis: 'z'),
        new("front-shakes", (0.8, 1, 13), (1, 4, 16), "front", UvRule.Positional, RunAxis: 'z'),
        new("secondfront-shakes", (1, 0, 0), (5, 1, 1), "secondfront", UvRule.Positional, RunAxis: 'x'),
        new("secondfront-shakes", (5, 0, 0.2), (9, 1, 1), "secondfront", UvRule.Positional, RunAxis: 'x'),
        new("secondfront-shakes", (9, 0, 0.1), (13, 1, 1), "secondfront", UvRule.Positional, RunAxis: 'x'),
        new("secondfront-shakes", (13, 0, 0.3), (16, 1, 1), "secondfront", UvRule.Positional, RunAxis: 'x'),
        new("secondfront-shakes", (1, 1, 0.5), (5, 4, 1), "secondfront", UvRule.Positional, RunAxis: 'x'),
        new("secondfront-shakes", (5, 1, 0.7), (9, 4, 1), "secondfront", UvRule.Positional, RunAxis: 'x'),
        new("secondfront-shakes", (9, 1, 0.6), (13, 4, 1), "secondfront", UvRule.Positional, RunAxis: 'x'),
        new("secondfront-shakes", (13, 1, 0.8), (16, 4, 1), "secondfront", UvRule.Positional, RunAxis: 'x'),
        new("front-shakes", (0.15, 4, 0), (1, 5, 4), "front", UvRule.Positional, RunAxis: 'z'),
        new("front-shakes", (0, 4, 4), (1, 5, 7), "front", UvRule.Positional, RunAxis: 'z'),
        new("front-shakes", (0.3, 4, 7), (1, 5, 12), "front", UvRule.Positional, RunAxis: 'z'),
        new("front-shakes", (0.1, 4, 12), (1, 5, 16), "front", UvRule.Positional, RunAxis: 'z'),
        new("front-shakes", (0.65, 5, 0.5), (1, 8, 4), "front", UvRule.Positional, RunAxis: 'z'),
        new("front-shakes", (0.5, 5, 4), (1, 8, 7), "front", UvRule.Positional, RunAxis: 'z'),
        new("front-shakes", (0.8, 5, 7), (1, 8, 12), "front", UvRule.Positional, RunAxis: 'z'),
        new("front-shakes", (0.6, 5, 12), (1, 8, 16), "front", UvRule.Positional, RunAxis: 'z'),
        new("secondfront-shakes", (1, 4, 0.15), (4, 5, 1), "secondfront", UvRule.Positional, RunAxis: 'x'),
        new("secondfront-shakes", (4, 4, 0), (7, 5, 1), "secondfront", UvRule.Positional, RunAxis: 'x'),
        new("secondfront-shakes", (7, 4, 0.3), (12, 5, 1), "secondfront", UvRule.Positional, RunAxis: 'x'),
        new("secondfront-shakes", (12, 4, 0.1), (16, 5, 1), "secondfront", UvRule.Positional, RunAxis: 'x'),
        new("secondfront-shakes", (1, 5, 0.65), (4, 8, 1), "secondfront", UvRule.Positional, RunAxis: 'x'),
        new("secondfront-shakes", (4, 5, 0.5), (7, 8, 1), "secondfront", UvRule.Positional, RunAxis: 'x'),
        new("secondfront-shakes", (7, 5, 0.8), (12, 8, 1), "secondfront", UvRule.Positional, RunAxis: 'x'),
        new("secondfront-shakes", (12, 5, 0.6), (16, 8, 1), "secondfront", UvRule.Positional, RunAxis: 'x'),
        new("front-shakes", (0.25, 8, 0), (1, 9, 6), "front", UvRule.Positional, RunAxis: 'z'),
        new("front-shakes", (0.1, 8, 6), (1, 9, 10), "front", UvRule.Positional, RunAxis: 'z'),
        new("front-shakes", (0, 8, 10), (1, 9, 13), "front", UvRule.Positional, RunAxis: 'z'),
        new("front-shakes", (0.2, 8, 13), (1, 9, 16), "front", UvRule.Positional, RunAxis: 'z'),
        new("front-shakes", (0.75, 9, 0.5), (1, 12, 6), "front", UvRule.Positional, RunAxis: 'z'),
        new("front-shakes", (0.6, 9, 6), (1, 12, 10), "front", UvRule.Positional, RunAxis: 'z'),
        new("front-shakes", (0.5, 9, 10), (1, 12, 13), "front", UvRule.Positional, RunAxis: 'z'),
        new("front-shakes", (0.7, 9, 13), (1, 12, 16), "front", UvRule.Positional, RunAxis: 'z'),
        new("secondfront-shakes", (1, 8, 0.25), (6, 9, 1), "secondfront", UvRule.Positional, RunAxis: 'x'),
        new("secondfront-shakes", (6, 8, 0.1), (10, 9, 1), "secondfront", UvRule.Positional, RunAxis: 'x'),
        new("secondfront-shakes", (10, 8, 0), (13, 9, 1), "secondfront", UvRule.Positional, RunAxis: 'x'),
        new("secondfront-shakes", (13, 8, 0.2), (16, 9, 1), "secondfront", UvRule.Positional, RunAxis: 'x'),
        new("secondfront-shakes", (1, 9, 0.75), (6, 12, 1), "secondfront", UvRule.Positional, RunAxis: 'x'),
        new("secondfront-shakes", (6, 9, 0.6), (10, 12, 1), "secondfront", UvRule.Positional, RunAxis: 'x'),
        new("secondfront-shakes", (10, 9, 0.5), (13, 12, 1), "secondfront", UvRule.Positional, RunAxis: 'x'),
        new("secondfront-shakes", (13, 9, 0.7), (16, 12, 1), "secondfront", UvRule.Positional, RunAxis: 'x'),
        new("front-shakes", (0.1, 12, 0), (1, 13, 3), "front", UvRule.Positional, RunAxis: 'z'),
        new("front-shakes", (0.3, 12, 3), (1, 13, 8), "front", UvRule.Positional, RunAxis: 'z'),
        new("front-shakes", (0.15, 12, 8), (1, 13, 12), "front", UvRule.Positional, RunAxis: 'z'),
        new("front-shakes", (0, 12, 12), (1, 13, 16), "front", UvRule.Positional, RunAxis: 'z'),
        new("front-shakes", (0.6, 13, 0.5), (1, 16, 3), "front", UvRule.Positional, RunAxis: 'z'),
        new("front-shakes", (0.8, 13, 3), (1, 16, 8), "front", UvRule.Positional, RunAxis: 'z'),
        new("front-shakes", (0.65, 13, 8), (1, 16, 12), "front", UvRule.Positional, RunAxis: 'z'),
        new("front-shakes", (0.5, 13, 12), (1, 16, 16), "front", UvRule.Positional, RunAxis: 'z'),
        new("secondfront-shakes", (1, 12, 0.1), (3, 13, 1), "secondfront", UvRule.Positional, RunAxis: 'x'),
        new("secondfront-shakes", (3, 12, 0.3), (8, 13, 1), "secondfront", UvRule.Positional, RunAxis: 'x'),
        new("secondfront-shakes", (8, 12, 0.15), (12, 13, 1), "secondfront", UvRule.Positional, RunAxis: 'x'),
        new("secondfront-shakes", (12, 12, 0), (16, 13, 1), "secondfront", UvRule.Positional, RunAxis: 'x'),
        new("secondfront-shakes", (1, 13, 0.6), (3, 16, 1), "secondfront", UvRule.Positional, RunAxis: 'x'),
        new("secondfront-shakes", (3, 13, 0.8), (8, 16, 1), "secondfront", UvRule.Positional, RunAxis: 'x'),
        new("secondfront-shakes", (8, 13, 0.65), (12, 16, 1), "secondfront", UvRule.Positional, RunAxis: 'x'),
        new("secondfront-shakes", (12, 13, 0.5), (16, 16, 1), "secondfront", UvRule.Positional, RunAxis: 'x'),
        new("back-logs", (3, 0, 4), (3.5, 16, 16), "back", UvRule.Flat),
        new("back-logs", (3.5, 0.5, 4), (4, 3.5, 16), "back", UvRule.Flat, PositionalOverrides: ["east"]),
        new("back-logs", (3.5, 4.5, 4), (4, 7.5, 16), "back", UvRule.Flat, PositionalOverrides: ["east"]),
        new("back-logs", (3.5, 8.5, 4), (4, 11.5, 16), "back", UvRule.Flat, PositionalOverrides: ["east"]),
        new("back-logs", (3.5, 12.5, 4), (4, 15.5, 16), "back", UvRule.Flat, PositionalOverrides: ["east"]),
        new("back-logs", (3, 0, 3), (16, 16, 3.5), "back", UvRule.Flat),
        new("back-logs", (3, 0.5, 3.5), (16, 3.5, 4), "back", UvRule.Flat, PositionalOverrides: ["south"]),
        new("back-logs", (3, 4.5, 3.5), (16, 7.5, 4), "back", UvRule.Flat, PositionalOverrides: ["south"]),
        new("back-logs", (3, 8.5, 3.5), (16, 11.5, 4), "back", UvRule.Flat, PositionalOverrides: ["south"]),
        new("back-logs", (3, 12.5, 3.5), (16, 15.5, 4), "back", UvRule.Flat, PositionalOverrides: ["south"]),
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
    ];

    private static readonly (string Slot, string Texture)[] CornerOutTextures =
    [
        ("framing", "game:block/wood/planks/aged/aged1"),
        ("infill", "game:block/wood/planks/aged/aged1"),
        ("front", "game:block/wood/planks/aged/aged1"),
        ("secondfront", "game:block/wood/planks/aged/aged1"),
        ("back", "game:block/wood/planks/aged/aged1"),
    ];

    public static JObject Generate(string layout) => layout switch
    {
        "wall" => Emit(WallElements, WallTextures),
        "cornerout" => Emit(CornerOutElements, CornerOutTextures),
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

        // A board tapered into steps samples down its own course, so the grain runs once from butt
        // to head instead of each step restarting the same slice.
        double courseTop = Math.Floor(fy / CoursePitch) * CoursePitch + CoursePitch;
        bool course = vAxis == 'y' && element.UvRule == UvRule.Course;

        var (u0, u1) = Span(uAxis);
        var (v0, v1) = course ? (courseTop - ty, courseTop - fy)
            : positional ? (16 - ty, 16 - fy)
            : Span(vAxis);

        var result = new JObject
        {
            ["texture"] = "#" + element.Slot,
            ["uv"] = new JArray(u0, v0, u1, v1),
        };
        if (element.RotatedFaces?.Contains(face) ?? false) result["rotation"] = 90;
        return result;
    }
}
