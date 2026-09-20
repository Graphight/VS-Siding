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
    string[]? RotatedFaces = null);

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
        // Weatherboard's lap (flush butt, recessed body) stays; the butt is now split into a few
        // boxes per course with a literal depth jitter, so the exposed edge reads as individual
        // split shingles instead of one ruled line. The painted texture's vertical joints are
        // irregular and land differently in every course, so modelled joints can never agree with
        // them - splitting sideways with gaps would just add a second, disagreeing set of lines.
        // The outward face stays flush at x 0 on every segment; only the inward cut (to.x) jitters,
        // so the wall's overall thickness is unchanged and the step is hidden under the course above.
        new("front-shakes", (0.0, 0, 0), (1, 1, 6), "front", UvRule.Positional, null, null, null),
        new("front-shakes", (0.1, 0, 6), (1, 1, 11), "front", UvRule.Positional, null, null, null),
        new("front-shakes", (0.05, 0, 11), (1, 1, 16), "front", UvRule.Positional, null, null, null),
        new("front-shakes", (0.5, 1, 0), (1, 4, 16), "front", UvRule.Positional, null, null, null),
        new("front-shakes", (0.1, 4, 0), (1, 5, 5), "front", UvRule.Positional, null, null, null),
        new("front-shakes", (0.0, 4, 5), (1, 5, 10), "front", UvRule.Positional, null, null, null),
        new("front-shakes", (0.15, 4, 10), (1, 5, 16), "front", UvRule.Positional, null, null, null),
        new("front-shakes", (0.5, 5, 0), (1, 8, 16), "front", UvRule.Positional, null, null, null),
        new("front-shakes", (0.05, 8, 0), (1, 9, 7), "front", UvRule.Positional, null, null, null),
        new("front-shakes", (0.15, 8, 7), (1, 9, 12), "front", UvRule.Positional, null, null, null),
        new("front-shakes", (0.0, 8, 12), (1, 9, 16), "front", UvRule.Positional, null, null, null),
        new("front-shakes", (0.5, 9, 0), (1, 12, 16), "front", UvRule.Positional, null, null, null),
        new("front-shakes", (0.15, 12, 0), (1, 13, 4), "front", UvRule.Positional, null, null, null),
        new("front-shakes", (0.05, 12, 4), (1, 13, 9), "front", UvRule.Positional, null, null, null),
        new("front-shakes", (0.0, 12, 9), (1, 13, 16), "front", UvRule.Positional, null, null, null),
        new("front-shakes", (0.5, 13, 0), (1, 16, 16), "front", UvRule.Positional, null, null, null),
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
        // Same taper-not-split reasoning as wall's front-shakes (see the comment there). The front
        // leg's butt keeps the wall's z split points and jitter depths - its lip runs the full z
        // 0..16 exactly as the lap it replaces did. The second leg's run is along x and its depth
        // is along z, so its butt stays flush at z 0 and jitters its inward cut (to.z) instead.
        new("front-shakes", (0.0, 0, 0), (1, 1, 6), "front", UvRule.Positional, null, null, null),
        new("front-shakes", (0.1, 0, 6), (1, 1, 11), "front", UvRule.Positional, null, null, null),
        new("front-shakes", (0.05, 0, 11), (1, 1, 16), "front", UvRule.Positional, null, null, null),
        new("front-shakes", (0.5, 1, 0.5), (1, 4, 16), "front", UvRule.Positional, null, null, null),
        new("secondfront-shakes", (1, 0, 0.0), (7, 1, 1), "secondfront", UvRule.Positional, null, null, null),
        new("secondfront-shakes", (7, 0, 0.1), (12, 1, 1), "secondfront", UvRule.Positional, null, null, null),
        new("secondfront-shakes", (12, 0, 0.05), (16, 1, 1), "secondfront", UvRule.Positional, null, null, null),
        new("secondfront-shakes", (1, 1, 0.5), (16, 4, 1), "secondfront", UvRule.Positional, null, null, null),
        new("front-shakes", (0.1, 4, 0), (1, 5, 5), "front", UvRule.Positional, null, null, null),
        new("front-shakes", (0.0, 4, 5), (1, 5, 10), "front", UvRule.Positional, null, null, null),
        new("front-shakes", (0.15, 4, 10), (1, 5, 16), "front", UvRule.Positional, null, null, null),
        new("front-shakes", (0.5, 5, 0.5), (1, 8, 16), "front", UvRule.Positional, null, null, null),
        new("secondfront-shakes", (1, 4, 0.1), (6, 5, 1), "secondfront", UvRule.Positional, null, null, null),
        new("secondfront-shakes", (6, 4, 0.0), (11, 5, 1), "secondfront", UvRule.Positional, null, null, null),
        new("secondfront-shakes", (11, 4, 0.15), (16, 5, 1), "secondfront", UvRule.Positional, null, null, null),
        new("secondfront-shakes", (1, 5, 0.5), (16, 8, 1), "secondfront", UvRule.Positional, null, null, null),
        new("front-shakes", (0.05, 8, 0), (1, 9, 7), "front", UvRule.Positional, null, null, null),
        new("front-shakes", (0.15, 8, 7), (1, 9, 12), "front", UvRule.Positional, null, null, null),
        new("front-shakes", (0.0, 8, 12), (1, 9, 16), "front", UvRule.Positional, null, null, null),
        new("front-shakes", (0.5, 9, 0.5), (1, 12, 16), "front", UvRule.Positional, null, null, null),
        new("secondfront-shakes", (1, 8, 0.05), (8, 9, 1), "secondfront", UvRule.Positional, null, null, null),
        new("secondfront-shakes", (8, 8, 0.15), (13, 9, 1), "secondfront", UvRule.Positional, null, null, null),
        new("secondfront-shakes", (13, 8, 0.0), (16, 9, 1), "secondfront", UvRule.Positional, null, null, null),
        new("secondfront-shakes", (1, 9, 0.5), (16, 12, 1), "secondfront", UvRule.Positional, null, null, null),
        new("front-shakes", (0.15, 12, 0), (1, 13, 4), "front", UvRule.Positional, null, null, null),
        new("front-shakes", (0.05, 12, 4), (1, 13, 9), "front", UvRule.Positional, null, null, null),
        new("front-shakes", (0.0, 12, 9), (1, 13, 16), "front", UvRule.Positional, null, null, null),
        new("front-shakes", (0.5, 13, 0.5), (1, 16, 16), "front", UvRule.Positional, null, null, null),
        new("secondfront-shakes", (1, 12, 0.15), (5, 13, 1), "secondfront", UvRule.Positional, null, null, null),
        new("secondfront-shakes", (5, 12, 0.05), (10, 13, 1), "secondfront", UvRule.Positional, null, null, null),
        new("secondfront-shakes", (10, 12, 0.0), (16, 13, 1), "secondfront", UvRule.Positional, null, null, null),
        new("secondfront-shakes", (1, 13, 0.5), (16, 16, 1), "secondfront", UvRule.Positional, null, null, null),
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

        var (sizeA, sizeB) = face switch
        {
            "north" or "south" => (dx, dy),
            "east" or "west" => (dz, dy),
            _ => (dx, dz),
        };

        bool positional = face is "north" or "south" or "east" or "west" &&
            (element.UvRule == UvRule.Positional || (element.PositionalOverrides?.Contains(face) ?? false));
        var uv = positional
            ? new JArray(0.0, 16 - ty, sizeA, 16 - fy)
            : new JArray(0.0, 0.0, sizeA, sizeB);

        var result = new JObject { ["texture"] = "#" + element.Slot, ["uv"] = uv };
        if (element.RotatedFaces?.Contains(face) ?? false) result["rotation"] = 90;
        return result;
    }
}
