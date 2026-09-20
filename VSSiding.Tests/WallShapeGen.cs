using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace VSSiding.Tests;

// Every uv in wall.json and cornerout.json derives from the element's box, per decision 0021:
// north/south use (dx, dy), east/west use (dz, dy), up/down use (dx, dz). Flat pins uv at 0;
// Positional maps v to the element's height (v0 = 16 - to.y, v1 = 16 - from.y) so painted courses
// land on the stepped lips and carry across stacked walls - up/down stay Flat either way.
// A handful of elements need one more bit than that: back-boards rotates its outward face 90 to
// stand plank grain upright, back-logs position-maps only its outward face while the rest of the
// log stays Flat, and infill-pane draws only two of the six faces.
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
    char? RunAxis = null);

public static class WallShapeGen
{
    private static readonly string[] AllFaces = ["north", "east", "south", "west", "up", "down"];

    private static readonly Element[] WallElements =
    [
        new("front", (0, 0, 0), (1, 16, 16), "front", UvRule.Flat, null, null, null),
        new("framing-top", (1, 15, 1), (3, 16, 15), "framing", UvRule.Flat, null, null, null),
        new("framing-bottom", (1, 0, 1), (3, 1, 15), "framing", UvRule.Flat, null, null, null),
        new("framing-left", (1, 0, 0), (3, 16, 1), "framing", UvRule.Flat, null, null, null),
        new("framing-right", (1, 0, 15), (3, 16, 16), "framing", UvRule.Flat, null, null, null),
        new("infill-top", (1.5, 15, 1), (2.5, 16, 15), "infill", UvRule.Flat, null, null, null),
        new("infill", (1.5, 1, 1), (2.5, 15, 15), "infill", UvRule.Flat, null, null, null),
        new("infill-bottom", (1.5, 0, 1), (2.5, 1, 15), "infill", UvRule.Flat, null, null, null),
        new("infill-pane", (2, 0, 0), (2, 16, 16), "infill", UvRule.Flat, ["west", "east"], null, null),
        new("glazing-left", (1, 0, 0.25), (3, 16, 2), "framing", UvRule.Flat, null, null, null),
        new("glazing-right", (1, 0, 14), (3, 16, 15.75), "framing", UvRule.Flat, null, null, null),
        new("glazing-top", (1.25, 14, 0), (2.75, 15.75, 16), "framing", UvRule.Flat, null, null, null),
        new("glazing-bottom", (1.25, 0.25, 0), (2.75, 2, 16), "framing", UvRule.Flat, null, null, null),
        new("back", (3, 0, 0), (4, 16, 16), "back", UvRule.Flat, null, null, null),
        new("back-boards", (3, 0, 0), (4, 16, 16), "back", UvRule.Flat, null, null, ["east"]),
        new("front-weatherboard", (0, 0, 0), (1, 1, 16), "front", UvRule.Flat, null, null, null),
        new("front-weatherboard", (0.5, 1, 0), (1, 4, 16), "front", UvRule.Flat, null, null, null),
        new("front-weatherboard", (0, 4, 0), (1, 5, 16), "front", UvRule.Flat, null, null, null),
        new("front-weatherboard", (0.5, 5, 0), (1, 8, 16), "front", UvRule.Flat, null, null, null),
        new("front-weatherboard", (0, 8, 0), (1, 9, 16), "front", UvRule.Flat, null, null, null),
        new("front-weatherboard", (0.5, 9, 0), (1, 12, 16), "front", UvRule.Flat, null, null, null),
        new("front-weatherboard", (0, 12, 0), (1, 13, 16), "front", UvRule.Flat, null, null, null),
        new("front-weatherboard", (0.5, 13, 0), (1, 16, 16), "front", UvRule.Flat, null, null, null),
        // Weatherboard's lap (proud butt, recessed body) stays, but each course is cut into four
        // shakes along its run, and a shake keeps its own depth through both bands - butt at d,
        // body at 0.5 + d - so it reads as one tile from butt to head rather than a banded strip.
        // Depths run 0 to 0.3 of a voxel and are recessed, never proud, so wall thickness is
        // unchanged. Shake boundaries move course to course, which is the stagger.
        // The painted texture's vertical joints are irregular and land differently in every course,
        // so modelled joints can never agree with them; the shakes abut with no gaps, and the depth
        // step alone carries the joint. Gaps would open a line straight into the wall.
        new("front-shakes", (0, 0, 0), (1, 1, 5), "front", UvRule.Positional, null, null, null, 'z'),
        new("front-shakes", (0.2, 0, 5), (1, 1, 9), "front", UvRule.Positional, null, null, null, 'z'),
        new("front-shakes", (0.1, 0, 9), (1, 1, 13), "front", UvRule.Positional, null, null, null, 'z'),
        new("front-shakes", (0.3, 0, 13), (1, 1, 16), "front", UvRule.Positional, null, null, null, 'z'),
        new("front-shakes", (0.5, 1, 0), (1, 4, 5), "front", UvRule.Positional, null, null, null, 'z'),
        new("front-shakes", (0.7, 1, 5), (1, 4, 9), "front", UvRule.Positional, null, null, null, 'z'),
        new("front-shakes", (0.6, 1, 9), (1, 4, 13), "front", UvRule.Positional, null, null, null, 'z'),
        new("front-shakes", (0.8, 1, 13), (1, 4, 16), "front", UvRule.Positional, null, null, null, 'z'),
        new("front-shakes", (0.15, 4, 0), (1, 5, 4), "front", UvRule.Positional, null, null, null, 'z'),
        new("front-shakes", (0, 4, 4), (1, 5, 7), "front", UvRule.Positional, null, null, null, 'z'),
        new("front-shakes", (0.3, 4, 7), (1, 5, 12), "front", UvRule.Positional, null, null, null, 'z'),
        new("front-shakes", (0.1, 4, 12), (1, 5, 16), "front", UvRule.Positional, null, null, null, 'z'),
        new("front-shakes", (0.65, 5, 0), (1, 8, 4), "front", UvRule.Positional, null, null, null, 'z'),
        new("front-shakes", (0.5, 5, 4), (1, 8, 7), "front", UvRule.Positional, null, null, null, 'z'),
        new("front-shakes", (0.8, 5, 7), (1, 8, 12), "front", UvRule.Positional, null, null, null, 'z'),
        new("front-shakes", (0.6, 5, 12), (1, 8, 16), "front", UvRule.Positional, null, null, null, 'z'),
        new("front-shakes", (0.25, 8, 0), (1, 9, 6), "front", UvRule.Positional, null, null, null, 'z'),
        new("front-shakes", (0.1, 8, 6), (1, 9, 10), "front", UvRule.Positional, null, null, null, 'z'),
        new("front-shakes", (0, 8, 10), (1, 9, 13), "front", UvRule.Positional, null, null, null, 'z'),
        new("front-shakes", (0.2, 8, 13), (1, 9, 16), "front", UvRule.Positional, null, null, null, 'z'),
        new("front-shakes", (0.75, 9, 0), (1, 12, 6), "front", UvRule.Positional, null, null, null, 'z'),
        new("front-shakes", (0.6, 9, 6), (1, 12, 10), "front", UvRule.Positional, null, null, null, 'z'),
        new("front-shakes", (0.5, 9, 10), (1, 12, 13), "front", UvRule.Positional, null, null, null, 'z'),
        new("front-shakes", (0.7, 9, 13), (1, 12, 16), "front", UvRule.Positional, null, null, null, 'z'),
        new("front-shakes", (0.1, 12, 0), (1, 13, 3), "front", UvRule.Positional, null, null, null, 'z'),
        new("front-shakes", (0.3, 12, 3), (1, 13, 8), "front", UvRule.Positional, null, null, null, 'z'),
        new("front-shakes", (0.15, 12, 8), (1, 13, 12), "front", UvRule.Positional, null, null, null, 'z'),
        new("front-shakes", (0, 12, 12), (1, 13, 16), "front", UvRule.Positional, null, null, null, 'z'),
        new("front-shakes", (0.6, 13, 0), (1, 16, 3), "front", UvRule.Positional, null, null, null, 'z'),
        new("front-shakes", (0.8, 13, 3), (1, 16, 8), "front", UvRule.Positional, null, null, null, 'z'),
        new("front-shakes", (0.65, 13, 8), (1, 16, 12), "front", UvRule.Positional, null, null, null, 'z'),
        new("front-shakes", (0.5, 13, 12), (1, 16, 16), "front", UvRule.Positional, null, null, null, 'z'),
        new("back-logs", (3, 0, 0), (3.5, 16, 16), "back", UvRule.Flat, null, null, null),
        new("back-logs", (3.5, 0.5, 0), (4, 3.5, 16), "back", UvRule.Flat, null, ["east"], null),
        new("back-logs", (3.5, 4.5, 0), (4, 7.5, 16), "back", UvRule.Flat, null, ["east"], null),
        new("back-logs", (3.5, 8.5, 0), (4, 11.5, 16), "back", UvRule.Flat, null, ["east"], null),
        new("back-logs", (3.5, 12.5, 0), (4, 15.5, 16), "back", UvRule.Flat, null, ["east"], null),
    ];

    private static readonly Element[] CornerOutElements =
    [
        new("front", (0, 0, 0), (1, 16, 16), "front", UvRule.Flat, null, null, null),
        new("framing-top", (1, 15, 3), (3, 16, 15), "framing", UvRule.Flat, null, null, null),
        new("framing-bottom", (1, 0, 3), (3, 1, 15), "framing", UvRule.Flat, null, null, null),
        new("framing", (1, 0, 1), (3, 16, 3), "framing", UvRule.Flat, null, null, null),
        new("infill-top", (1.5, 15, 3), (2.5, 16, 15), "infill", UvRule.Flat, null, null, null),
        new("infill", (1.5, 1, 3), (2.5, 15, 15), "infill", UvRule.Flat, null, null, null),
        new("infill-bottom", (1.5, 0, 3), (2.5, 1, 15), "infill", UvRule.Flat, null, null, null),
        new("framing", (1, 0, 15), (3, 16, 16), "framing", UvRule.Flat, null, null, null),
        new("back", (3, 0, 4), (4, 16, 16), "back", UvRule.Flat, null, null, null),
        new("secondfront", (1, 0, 0), (16, 16, 1), "secondfront", UvRule.Flat, null, null, null),
        new("framing-top", (3, 15, 1), (15, 16, 3), "framing", UvRule.Flat, null, null, null),
        new("framing-bottom", (3, 0, 1), (15, 1, 3), "framing", UvRule.Flat, null, null, null),
        new("infill-top", (3, 15, 1.5), (15, 16, 2.5), "infill", UvRule.Flat, null, null, null),
        new("infill", (3, 1, 1.5), (15, 15, 2.5), "infill", UvRule.Flat, null, null, null),
        new("infill-bottom", (3, 0, 1.5), (15, 1, 2.5), "infill", UvRule.Flat, null, null, null),
        new("infill-pane", (2, 0, 3), (2, 16, 16), "infill", UvRule.Flat, ["west", "east"], null, null),
        new("infill-pane", (3, 0, 2), (16, 16, 2), "infill", UvRule.Flat, ["north", "south"], null, null),
        new("framing", (15, 0, 1), (16, 16, 3), "framing", UvRule.Flat, null, null, null),
        new("back", (3, 0, 3), (16, 16, 4), "back", UvRule.Flat, null, null, null),
        new("front-weatherboard", (0, 0, 0), (1, 1, 16), "front", UvRule.Flat, null, null, null),
        new("front-weatherboard", (0.5, 1, 0.5), (1, 4, 16), "front", UvRule.Flat, null, null, null),
        new("secondfront-weatherboard", (1, 0, 0), (16, 1, 1), "secondfront", UvRule.Flat, null, null, null),
        new("secondfront-weatherboard", (1, 1, 0.5), (16, 4, 1), "secondfront", UvRule.Flat, null, null, null),
        new("front-weatherboard", (0, 4, 0), (1, 5, 16), "front", UvRule.Flat, null, null, null),
        new("front-weatherboard", (0.5, 5, 0.5), (1, 8, 16), "front", UvRule.Flat, null, null, null),
        new("secondfront-weatherboard", (1, 4, 0), (16, 5, 1), "secondfront", UvRule.Flat, null, null, null),
        new("secondfront-weatherboard", (1, 5, 0.5), (16, 8, 1), "secondfront", UvRule.Flat, null, null, null),
        new("front-weatherboard", (0, 8, 0), (1, 9, 16), "front", UvRule.Flat, null, null, null),
        new("front-weatherboard", (0.5, 9, 0.5), (1, 12, 16), "front", UvRule.Flat, null, null, null),
        new("secondfront-weatherboard", (1, 8, 0), (16, 9, 1), "secondfront", UvRule.Flat, null, null, null),
        new("secondfront-weatherboard", (1, 9, 0.5), (16, 12, 1), "secondfront", UvRule.Flat, null, null, null),
        new("front-weatherboard", (0, 12, 0), (1, 13, 16), "front", UvRule.Flat, null, null, null),
        new("front-weatherboard", (0.5, 13, 0.5), (1, 16, 16), "front", UvRule.Flat, null, null, null),
        new("secondfront-weatherboard", (1, 12, 0), (16, 13, 1), "secondfront", UvRule.Flat, null, null, null),
        new("secondfront-weatherboard", (1, 13, 0.5), (16, 16, 1), "secondfront", UvRule.Flat, null, null, null),
        new("back-boards", (3, 0, 4), (4, 16, 16), "back", UvRule.Flat, null, null, ["east"]),
        new("back-boards", (3, 0, 3), (16, 16, 4), "back", UvRule.Flat, null, null, ["south"]),
        // Same tiling as wall's front-shakes (see the comment there), on both legs. The front leg
        // runs along z with depth along x; the second leg runs along x with depth along z, so its
        // shakes recess from z 0 - the outward face - and its body sits at 0.5 + d the same way.
        // Each leg's RunAxis is what keeps the texture running across its own split courses.
        new("front-shakes", (0, 0, 0), (1, 1, 5), "front", UvRule.Positional, null, null, null, 'z'),
        new("front-shakes", (0.2, 0, 5), (1, 1, 9), "front", UvRule.Positional, null, null, null, 'z'),
        new("front-shakes", (0.1, 0, 9), (1, 1, 13), "front", UvRule.Positional, null, null, null, 'z'),
        new("front-shakes", (0.3, 0, 13), (1, 1, 16), "front", UvRule.Positional, null, null, null, 'z'),
        new("front-shakes", (0.5, 1, 0.5), (1, 4, 5), "front", UvRule.Positional, null, null, null, 'z'),
        new("front-shakes", (0.7, 1, 5), (1, 4, 9), "front", UvRule.Positional, null, null, null, 'z'),
        new("front-shakes", (0.6, 1, 9), (1, 4, 13), "front", UvRule.Positional, null, null, null, 'z'),
        new("front-shakes", (0.8, 1, 13), (1, 4, 16), "front", UvRule.Positional, null, null, null, 'z'),
        new("secondfront-shakes", (1, 0, 0), (5, 1, 1), "secondfront", UvRule.Positional, null, null, null, 'x'),
        new("secondfront-shakes", (5, 0, 0.2), (9, 1, 1), "secondfront", UvRule.Positional, null, null, null, 'x'),
        new("secondfront-shakes", (9, 0, 0.1), (13, 1, 1), "secondfront", UvRule.Positional, null, null, null, 'x'),
        new("secondfront-shakes", (13, 0, 0.3), (16, 1, 1), "secondfront", UvRule.Positional, null, null, null, 'x'),
        new("secondfront-shakes", (1, 1, 0.5), (5, 4, 1), "secondfront", UvRule.Positional, null, null, null, 'x'),
        new("secondfront-shakes", (5, 1, 0.7), (9, 4, 1), "secondfront", UvRule.Positional, null, null, null, 'x'),
        new("secondfront-shakes", (9, 1, 0.6), (13, 4, 1), "secondfront", UvRule.Positional, null, null, null, 'x'),
        new("secondfront-shakes", (13, 1, 0.8), (16, 4, 1), "secondfront", UvRule.Positional, null, null, null, 'x'),
        new("front-shakes", (0.15, 4, 0), (1, 5, 4), "front", UvRule.Positional, null, null, null, 'z'),
        new("front-shakes", (0, 4, 4), (1, 5, 7), "front", UvRule.Positional, null, null, null, 'z'),
        new("front-shakes", (0.3, 4, 7), (1, 5, 12), "front", UvRule.Positional, null, null, null, 'z'),
        new("front-shakes", (0.1, 4, 12), (1, 5, 16), "front", UvRule.Positional, null, null, null, 'z'),
        new("front-shakes", (0.65, 5, 0.5), (1, 8, 4), "front", UvRule.Positional, null, null, null, 'z'),
        new("front-shakes", (0.5, 5, 4), (1, 8, 7), "front", UvRule.Positional, null, null, null, 'z'),
        new("front-shakes", (0.8, 5, 7), (1, 8, 12), "front", UvRule.Positional, null, null, null, 'z'),
        new("front-shakes", (0.6, 5, 12), (1, 8, 16), "front", UvRule.Positional, null, null, null, 'z'),
        new("secondfront-shakes", (1, 4, 0.15), (4, 5, 1), "secondfront", UvRule.Positional, null, null, null, 'x'),
        new("secondfront-shakes", (4, 4, 0), (7, 5, 1), "secondfront", UvRule.Positional, null, null, null, 'x'),
        new("secondfront-shakes", (7, 4, 0.3), (12, 5, 1), "secondfront", UvRule.Positional, null, null, null, 'x'),
        new("secondfront-shakes", (12, 4, 0.1), (16, 5, 1), "secondfront", UvRule.Positional, null, null, null, 'x'),
        new("secondfront-shakes", (1, 5, 0.65), (4, 8, 1), "secondfront", UvRule.Positional, null, null, null, 'x'),
        new("secondfront-shakes", (4, 5, 0.5), (7, 8, 1), "secondfront", UvRule.Positional, null, null, null, 'x'),
        new("secondfront-shakes", (7, 5, 0.8), (12, 8, 1), "secondfront", UvRule.Positional, null, null, null, 'x'),
        new("secondfront-shakes", (12, 5, 0.6), (16, 8, 1), "secondfront", UvRule.Positional, null, null, null, 'x'),
        new("front-shakes", (0.25, 8, 0), (1, 9, 6), "front", UvRule.Positional, null, null, null, 'z'),
        new("front-shakes", (0.1, 8, 6), (1, 9, 10), "front", UvRule.Positional, null, null, null, 'z'),
        new("front-shakes", (0, 8, 10), (1, 9, 13), "front", UvRule.Positional, null, null, null, 'z'),
        new("front-shakes", (0.2, 8, 13), (1, 9, 16), "front", UvRule.Positional, null, null, null, 'z'),
        new("front-shakes", (0.75, 9, 0.5), (1, 12, 6), "front", UvRule.Positional, null, null, null, 'z'),
        new("front-shakes", (0.6, 9, 6), (1, 12, 10), "front", UvRule.Positional, null, null, null, 'z'),
        new("front-shakes", (0.5, 9, 10), (1, 12, 13), "front", UvRule.Positional, null, null, null, 'z'),
        new("front-shakes", (0.7, 9, 13), (1, 12, 16), "front", UvRule.Positional, null, null, null, 'z'),
        new("secondfront-shakes", (1, 8, 0.25), (6, 9, 1), "secondfront", UvRule.Positional, null, null, null, 'x'),
        new("secondfront-shakes", (6, 8, 0.1), (10, 9, 1), "secondfront", UvRule.Positional, null, null, null, 'x'),
        new("secondfront-shakes", (10, 8, 0), (13, 9, 1), "secondfront", UvRule.Positional, null, null, null, 'x'),
        new("secondfront-shakes", (13, 8, 0.2), (16, 9, 1), "secondfront", UvRule.Positional, null, null, null, 'x'),
        new("secondfront-shakes", (1, 9, 0.75), (6, 12, 1), "secondfront", UvRule.Positional, null, null, null, 'x'),
        new("secondfront-shakes", (6, 9, 0.6), (10, 12, 1), "secondfront", UvRule.Positional, null, null, null, 'x'),
        new("secondfront-shakes", (10, 9, 0.5), (13, 12, 1), "secondfront", UvRule.Positional, null, null, null, 'x'),
        new("secondfront-shakes", (13, 9, 0.7), (16, 12, 1), "secondfront", UvRule.Positional, null, null, null, 'x'),
        new("front-shakes", (0.1, 12, 0), (1, 13, 3), "front", UvRule.Positional, null, null, null, 'z'),
        new("front-shakes", (0.3, 12, 3), (1, 13, 8), "front", UvRule.Positional, null, null, null, 'z'),
        new("front-shakes", (0.15, 12, 8), (1, 13, 12), "front", UvRule.Positional, null, null, null, 'z'),
        new("front-shakes", (0, 12, 12), (1, 13, 16), "front", UvRule.Positional, null, null, null, 'z'),
        new("front-shakes", (0.6, 13, 0.5), (1, 16, 3), "front", UvRule.Positional, null, null, null, 'z'),
        new("front-shakes", (0.8, 13, 3), (1, 16, 8), "front", UvRule.Positional, null, null, null, 'z'),
        new("front-shakes", (0.65, 13, 8), (1, 16, 12), "front", UvRule.Positional, null, null, null, 'z'),
        new("front-shakes", (0.5, 13, 12), (1, 16, 16), "front", UvRule.Positional, null, null, null, 'z'),
        new("secondfront-shakes", (1, 12, 0.1), (3, 13, 1), "secondfront", UvRule.Positional, null, null, null, 'x'),
        new("secondfront-shakes", (3, 12, 0.3), (8, 13, 1), "secondfront", UvRule.Positional, null, null, null, 'x'),
        new("secondfront-shakes", (8, 12, 0.15), (12, 13, 1), "secondfront", UvRule.Positional, null, null, null, 'x'),
        new("secondfront-shakes", (12, 12, 0), (16, 13, 1), "secondfront", UvRule.Positional, null, null, null, 'x'),
        new("secondfront-shakes", (1, 13, 0.6), (3, 16, 1), "secondfront", UvRule.Positional, null, null, null, 'x'),
        new("secondfront-shakes", (3, 13, 0.8), (8, 16, 1), "secondfront", UvRule.Positional, null, null, null, 'x'),
        new("secondfront-shakes", (8, 13, 0.65), (12, 16, 1), "secondfront", UvRule.Positional, null, null, null, 'x'),
        new("secondfront-shakes", (12, 13, 0.5), (16, 16, 1), "secondfront", UvRule.Positional, null, null, null, 'x'),
        new("back-logs", (3, 0, 4), (3.5, 16, 16), "back", UvRule.Flat, null, null, null),
        new("back-logs", (3.5, 0.5, 4), (4, 3.5, 16), "back", UvRule.Flat, null, ["east"], null),
        new("back-logs", (3.5, 4.5, 4), (4, 7.5, 16), "back", UvRule.Flat, null, ["east"], null),
        new("back-logs", (3.5, 8.5, 4), (4, 11.5, 16), "back", UvRule.Flat, null, ["east"], null),
        new("back-logs", (3.5, 12.5, 4), (4, 15.5, 16), "back", UvRule.Flat, null, ["east"], null),
        new("back-logs", (3, 0, 3), (16, 16, 3.5), "back", UvRule.Flat, null, null, null),
        new("back-logs", (3, 0.5, 3.5), (16, 3.5, 4), "back", UvRule.Flat, null, ["south"], null),
        new("back-logs", (3, 4.5, 3.5), (16, 7.5, 4), "back", UvRule.Flat, null, ["south"], null),
        new("back-logs", (3, 8.5, 3.5), (16, 11.5, 4), "back", UvRule.Flat, null, ["south"], null),
        new("back-logs", (3, 12.5, 3.5), (16, 15.5, 4), "back", UvRule.Flat, null, ["south"], null),
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
            ["elements"] = new JArray(elements.Select(EmitElement)),
        };
    }

    private static JObject EmitElement(Element element)
    {
        var faces = new JObject();
        foreach (var face in element.Faces ?? AllFaces) faces[face] = EmitFace(element, face);

        return new JObject
        {
            ["name"] = element.Name,
            ["from"] = new JArray(element.From.X, element.From.Y, element.From.Z),
            ["to"] = new JArray(element.To.X, element.To.Y, element.To.Z),
            ["faces"] = faces,
        };
    }

    private static JObject EmitFace(Element element, string face)
    {
        var (fx, fy, fz) = element.From;
        var (tx, ty, tz) = element.To;
        double dx = tx - fx, dy = ty - fy, dz = tz - fz;

        var (sizeA, sizeB, uAxis, vAxis) = face switch
        {
            "north" or "south" => (dx, dy, 'x', 'y'),
            "east" or "west" => (dz, dy, 'z', 'y'),
            _ => (dx, dz, 'x', 'z'),
        };

        bool positional = vAxis == 'y' &&
            (element.UvRule == UvRule.Positional || (element.PositionalOverrides?.Contains(face) ?? false));

        double Coord(char axis, bool end) => axis switch
        {
            'x' => end ? tx : fx,
            'y' => end ? ty : fy,
            _ => end ? tz : fz,
        };

        // A split course has to keep sampling the texture where the box sits, or every segment
        // restarts the same left-hand strip and the painted grain repeats across the run.
        var (u0, u1) = element.RunAxis == uAxis
            ? (Coord(uAxis, false), Coord(uAxis, true))
            : (0.0, sizeA);
        var (v0, v1) = positional ? (16 - ty, 16 - fy)
            : element.RunAxis == vAxis ? (Coord(vAxis, false), Coord(vAxis, true))
            : (0.0, sizeB);

        var uv = new JArray(u0, v0, u1, v1);

        var result = new JObject { ["texture"] = "#" + element.Slot, ["uv"] = uv };
        if (element.RotatedFaces?.Contains(face) ?? false) result["rotation"] = 90;
        return result;
    }
}
