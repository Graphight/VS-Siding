using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.MathTools;
using Xunit;

namespace VSSiding.Tests;

// The element table in WallShapeGen is a hand-transcribed reading of the uv derivation rules,
// checked against the committed shape files it is meant to replace. If it drifts from what the
// game actually loads, this is where that would show up.
// The golden test cannot check the generator's own arithmetic - `just shapes` reblesses that file
// from this same generator, so a wrong rule would pass against its own output. Every assertion
// below it is therefore written out by hand rather than recomputed (decision 0021).
public class WallShapeGenTests
{
    [Theory]
    [InlineData("wall")]
    [InlineData("cornerout")]
    [InlineData("diagonal")]
    [InlineData("floor")]
    public void GeneratedShapeMatchesTheCommittedOne(string layout)
    {
        var repoRoot = MaterialTextureOpacityTests.GetAssemblyMetadata("RepoRoot");
        var folder = layout == "floor" ? "floor" : "wall";
        var path = Path.Combine(repoRoot, "VSSiding", "assets", "vssiding", "shapes", "block", folder, layout + ".json");

        if (Environment.GetEnvironmentVariable("SIDING_REGEN") == "1")
        {
            File.WriteAllText(path, WallShapeGen.Generate(layout) + "\n");
            return;
        }

        var committed = JObject.Parse(File.ReadAllText(path));
        Assert.Equal(committed.ToString(), WallShapeGen.Generate(layout).ToString());
    }

    [Fact]
    public void ATurnedElementEmitsItsOffsetBoxAndItsRotationBeforeItsFaces()
    {
        var turned = new Element("panel", (0, 0, 0), (4, 16, 16), "infill", UvRule.Flat, ["north"], RotationY: 45, Offset: (6, 0, -16));

        var expected = new JObject
        {
            ["name"] = "panel",
            ["from"] = new JArray(6.0, 0.0, -16.0),
            ["to"] = new JArray(10.0, 16.0, 0.0),
            ["rotationOrigin"] = new JArray(8.0, 0.0, 8.0),
            ["rotationY"] = 45.0,
            ["faces"] = new JObject
            {
                ["north"] = new JObject { ["texture"] = "#infill", ["uv"] = new JArray(0.0, 0.0, 4.0, 16.0) },
            },
        };

        Assert.Equal(expected.ToString(), WallShapeGen.EmitElement(turned, [turned]).ToString());
    }

    // The turn is vanilla's translate(origin) * rotate(theta about +y) * translate(from - origin), so in plan
    // x' = x cos + z sin and z' = -x sin + z cos about the origin. Each group is then measured along the
    // diagonal: run from the cell's centre towards its north-east corner, and depth from the centre line
    // towards the room on the south-east. The corner is 8 root 2 = 11.31 along, and a square end 2 short
    // of it, at 9.31, touches both of the cell's faces, so the panel itself never leaves the cell. The
    // left pieces are the ones the north-east cell carries on from.
    [Fact]
    public void TheDiagonalRunsCornerToCornerWithItsFrontToTheNorthWest()
    {
        var elements = WallShapeGen.Generate("diagonal")["elements"]!.Where(e => e["rotationY"] != null).ToArray();

        static (double Run, double Depth) Along(JToken element, double x, double z)
        {
            double theta = (double)element["rotationY"]! * Math.PI / 180;
            double ox = (double)element["rotationOrigin"]![0]!, oz = (double)element["rotationOrigin"]![2]!;
            double tx = ox + (x - ox) * Math.Cos(theta) + (z - oz) * Math.Sin(theta), tz = oz - (x - ox) * Math.Sin(theta) + (z - oz) * Math.Cos(theta);
            return ((tx - tz) / Math.Sqrt(2), (tx + tz - 16) / Math.Sqrt(2));
        }

        (double, double, double, double) Extent(string name)
        {
            var corners = (
                from element in elements
                where (string)element["name"]! == name
                let box = Box(element)
                from x in new[] { box.Lo[0], box.Hi[0] }
                from z in new[] { box.Lo[2], box.Hi[2] }
                select Along(element, x, z)).ToArray();
            return (Math.Round(corners.Min(c => c.Run), 2), Math.Round(corners.Max(c => c.Run), 2),
                Math.Round(corners.Min(c => c.Depth), 2), Math.Round(corners.Max(c => c.Depth), 2));
        }

        var expected = new Dictionary<string, (double, double, double, double)>
        {
            ["front"] = (-9.31, 9.31, -2, -1),
            ["front-brick"] = (-9.31, 9.31, -2, -1),
            ["framing-top"] = (-9.31, 9.31, -1, 1),
            ["infill"] = (-9.31, 9.31, -0.5, 0.5),
            ["back"] = (-9.31, 9.31, 1, 2),
            ["front-left"] = (9.31, 11.31, -2, -1),
            ["framing-top-left"] = (9.31, 10.31, -1, 1),
            ["framing-join-left"] = (10.31, 11.31, -1, 1),
            ["back-right"] = (-11.31, -9.31, 1, 2),
            ["infill-right"] = (-10.31, -9.31, -0.5, 0.5),
            ["framing-join-right"] = (-11.31, -10.31, -1, 1),
        };

        Assert.Equal(expected, expected.Keys.ToDictionary(name => name, Extent));
    }

    // The fillers stand in the two corners the panel runs between, unturned, their diagonal on the
    // panel's square end.
    [Fact]
    public void TheDiagonalsFillersSitInItsTwoCorners()
    {
        var fillers = WallShapeGen.Generate("diagonal")["elements"]!
            .Where(e => (string)e["name"]! is "framing-left" or "framing-right")
            .Select(e => $"{e["name"]} {string.Join(",", e["from"]!)} {string.Join(",", e["to"]!)} {e["rotationY"]}");

        Assert.Equal(["framing-left 13.1716,0.01,0 16,15.99,2.8284 ", "framing-right 0,0.01,13.1716 2.8284,15.99,16 "], fillers);
    }

    // The depths in the shake table are a tuning knob, and a knob gets turned. A box that inverts
    // or runs past the framing renders as a hole rather than an error, so the bound is asserted
    // here instead of being re-checked by hand after every tune.
    [Theory]
    [InlineData("wall")]
    [InlineData("cornerout")]
    [InlineData("diagonal")]
    [InlineData("floor")]
    public void EveryGeneratedBoxIsNonDegenerateAndInsideTheBlock(string layout)
    {
        var offenders = new List<string>();
        foreach (var element in WallShapeGen.Generate(layout)["elements"]!)
        {
            var name = (string)element["name"]!;
            var lo = element["from"]!.Select(v => (double)v!).ToArray();
            var hi = element["to"]!.Select(v => (double)v!).ToArray();

            offenders.AddRange(
                from axis in Enumerable.Range(0, 3)
                where lo[axis] > hi[axis]
                select $"{layout} '{name}' inverts on axis {axis}: {lo[axis]} > {hi[axis]}");
            // A turned element's plan is measured after the turn (TheDiagonalRunsCornerToCornerWithItsFrontToTheNorthWest).
            offenders.AddRange(
                from v in element["rotationY"] == null ? lo.Concat(hi) : [lo[1], hi[1]]
                where v < 0 || v > 16
                select $"{layout} '{name}' leaves the block: {v}");
        }

        Assert.Equal([], offenders);
    }

    // A split segment samples the texture at its own position along the run, so u must equal the
    // box's own run coordinates. Restarting each segment at 0 is the bug this catches.
    [Theory]
    [InlineData("wall", "front-shakes", 2)]
    [InlineData("cornerout", "front-shakes", 2)]
    [InlineData("cornerout", "secondfront-shakes", 0)]
    public void EverySplitShakeSamplesTheTextureAtItsOwnPositionAlongTheRun(
        string layout, string group, int runAxis)
    {
        var boxes = WallShapeGen.Generate(layout)["elements"]!
            .Where(e => (string)e["name"]! == group)
            .ToArray();
        var face = runAxis == 2 ? "west" : "north";

        var expected = boxes
            .Select(e => ((double)e["from"]![runAxis]!, (double)e["to"]![runAxis]!))
            .ToArray();
        var actual = boxes
            .Select(e => e["faces"]![face]!["uv"]!)
            .Select(uv => ((double)uv[0]!, (double)uv[2]!))
            .ToArray();

        Assert.Equal(expected, actual);
    }

    // A box with no faces left is a box the tesselator draws nothing for: a hole in the wall, not
    // an error. Only the prune can cause one.
    [Theory]
    [InlineData("wall")]
    [InlineData("cornerout")]
    [InlineData("diagonal")]
    [InlineData("floor")]
    public void NoElementIsPrunedDownToNothing(string layout)
    {
        Assert.Equal([], WallShapeGen.Generate(layout)["elements"]!
            .Where(e => !((JObject)e["faces"]!).Properties().Any())
            .Select(e => (string)e["name"]!)
            .ToArray());
    }

    // The floor's whole layout, written out: a 4/16 panel at y 12..16, three joists running north-south.
    // The poles* groups are left out: the committed shape and the pole invariants pin them. So are the rim
    // pieces, which FloorCutsItsFinishesToTheGlazingFramesFootprints pins.
    [Fact]
    public void FloorLaysTheWallsLayersFlatAtTheTopOfTheCell()
    {
        string[] expected =
        [
            "front 0,15,0 16,16,16",
            "framing-left 2,13,0 3,15,16",
            "framing-left 7.5,13,0 8.5,15,16",
            "framing-right 13,13,0 14,15,16",
            "framing-top 0,13,0 2,15,1",
            "framing-top 3,13,0 7.5,15,1",
            "framing-top 8.5,13,0 13,15,1",
            "framing-top 14,13,0 16,15,1",
            "framing-bottom 0,13,15 2,15,16",
            "framing-bottom 3,13,15 7.5,15,16",
            "framing-bottom 8.5,13,15 13,15,16",
            "framing-bottom 14,13,15 16,15,16",
            "infill-top 0,13.5,0 16,14.5,1",
            "infill 0,13.5,1 16,14.5,15",
            "infill-bottom 0,13.5,15 16,14.5,16",
            "back 0,12,0 16,13,16",
            "infill-pane 0,14,0 16,14,16",
            "glazing-left 0,13,0 2,15,16",
            "glazing-right 14,13,0 16,15,16",
            "glazing-top 2,13,0 14,15,2",
            "glazing-bottom 2,13,14 14,15,16",
            "glazing-top-left 0,13,0 2,15,2",
            "glazing-top-right 14,13,0 16,15,2",
            "glazing-bottom-left 0,13,14 2,15,16",
            "glazing-bottom-right 14,13,14 16,15,16",
            "front-boards 0,15,0 16,16,16",
            "front-hboards 0,15,0 16,16,16",
            "back-boards 0,12,0 16,13,16",
            "back-hboards 0,12,0 16,13,16",
            "back-lath-hboards 0,12.5,0 16,13,16",
            .. Enumerable.Range(0, 8).Select(i => $"back-lath-hboards 0,12,{2 * i + 0.5} 16,12.5,{2 * i + 1.5}"),
            "back-lath-boards 0,12.5,0 16,13,16",
            .. Enumerable.Range(0, 8).Select(i => $"back-lath-boards {2 * i + 0.5},12,0 {2 * i + 1.5},12.5,16"),
        ];
        Assert.Equal(expected, WallShapeGen.Generate("floor")["elements"]!
            .Where(e => !((string)e["name"]!).StartsWith("poles") && !((string)e["name"]!).Contains("-rim"))
            .Select(e => $"{e["name"]} {string.Join(",", e["from"]!.Select(v => (double)v!))} {string.Join(",", e["to"]!.Select(v => (double)v!))}")
            .ToArray());
    }

    // A glazed floor's finish is its group cut to each of the frame's eight footprints on x and z, keeping
    // its own y: the top boards at y 15 to 16, and a batten of the daub's lath, which the north rail
    // reaches and the south rail does not.
    [Fact]
    public void FloorCutsItsFinishesToTheGlazingFramesFootprints()
    {
        string[] expected =
        [
            "front-boards-rim-left 0,15,0 2,16,16",
            "back-lath-hboards-rim-left 0,12,0.5 2,12.5,1.5",
            "front-boards-rim-right 14,15,0 16,16,16",
            "back-lath-hboards-rim-right 14,12,0.5 16,12.5,1.5",
            "front-boards-rim-top 2,15,0 14,16,2",
            "back-lath-hboards-rim-top 2,12,0.5 14,12.5,1.5",
            "front-boards-rim-bottom 2,15,14 14,16,16",
            "front-boards-rim-top-left 0,15,0 2,16,2",
            "back-lath-hboards-rim-top-left 0,12,0.5 2,12.5,1.5",
            "front-boards-rim-top-right 14,15,0 16,16,2",
            "back-lath-hboards-rim-top-right 14,12,0.5 16,12.5,1.5",
            "front-boards-rim-bottom-left 0,15,14 2,16,16",
            "front-boards-rim-bottom-right 14,15,14 16,16,16",
        ];
        Assert.Equal(expected, WallShapeGen.Generate("floor")["elements"]!
            .Where(e => ((string)e["name"]!).StartsWith("front-boards-rim")
                || (((string)e["name"]!).StartsWith("back-lath-hboards-rim") && (double)e["from"]![2]! == 0.5))
            .Select(e => $"{e["name"]} {string.Join(",", e["from"]!.Select(v => (double)v!))} {string.Join(",", e["to"]!.Select(v => (double)v!))}")
            .ToArray());
    }

    // The bottom course of shakes carries every case of the prune in four boxes. They abut along
    // z at 5, 9 and 13, at depths 0, 0.2, 0.1 and 0.3 - a depth is an x offset from the outward
    // face, so the smaller number is the one standing proud.
    [Fact]
    public void AShakeDropsOnlyTheFaceItsDeeperNeighbourCoversEntirely()
    {
        string[][] expected =
        [
            // Proudest of the four, so its neighbour leaves a strip of it showing.
            ["north", "east", "south", "west", "up", "down"],
            // Recessed behind both neighbours, so it loses both.
            ["east", "west", "up", "down"],
            // Proud of the boxes at z 5 and z 13 alike.
            ["north", "east", "south", "west", "up", "down"],
            // Recessed behind z 9; past z 16 is the cell edge, with no neighbour in this mesh.
            ["east", "south", "west", "up", "down"],
        ];

        Assert.Equal(expected, WallShapeGen.Generate("wall")["elements"]!
            .Where(e => (string)e["name"]! == "front-shakes")
            .Take(4)
            .Select(e => ((JObject)e["faces"]!).Properties().Select(p => p.Name).ToArray())
            .ToArray());
    }

    // What the prune is actually for: the quads one built cell hands the tesselator.
    [Fact]
    public void EachBuiltCellHandsTheTesselatorThisManyQuads()
    {
        var expected = new Dictionary<string, int>
        {
            ["wall bare frame"] = 24,
            ["wall pole frame"] = 144,
            ["wall second pole frame"] = 144,
            ["floor bare frame"] = 66,
            ["floor pole frame"] = 240,
            ["floor second pole frame"] = 240,
            ["wall wattle"] = 30,
            ["wall wattle, mid-stack"] = 30,
            ["wall daub both faces"] = 42,
            ["wall weatherboard both faces"] = 117,
            ["wall shakes both faces"] = 205,
            ["wall glazed"] = 22,
            ["wall glazed, merged left"] = 28,
            // Glazed with daub, a rim in place of each slab: a piece per part the frame draws, six faces each.
            // Unjoined, 22 + 2 faces * 4 parts * 6 = 70. Merged left, 28 + 2 * 5 * 6 = 88.
            ["wall glazed, daub both faces"] = 70,
            ["wall glazed, daub both faces, merged left"] = 88,
            ["wall glazed, daub both faces, merged all round"] = 2,
            ["wall glazed, merged all round"] = 2,
            // A floor's bezel is the same eight pieces at six faces each, and the pane two faces. Glazed, 4 * 6 + 2 = 26;
            // merged left, 5 * 6 + 2 = 32. Daub rims the top and the underside, a piece per part: 26 + 2 * 4 * 6 = 74,
            // and merged left 32 + 2 * 5 * 6 = 92.
            ["floor glazed"] = 26,
            ["floor glazed, merged left"] = 32,
            ["floor glazed, daub both faces"] = 74,
            ["floor glazed, daub both faces, merged left"] = 92,
            ["floor glazed, daub both faces, merged all round"] = 2,
            ["cornerout bare frame"] = 42,
            ["cornerout pole frame"] = 158,
            ["cornerout second pole frame"] = 158,
            ["cornerout wattle"] = 54,
            ["cornerout wattle, mid-stack"] = 54,
            ["cornerout daub both faces"] = 77,
            ["cornerout weatherboard both faces"] = 227,
            ["cornerout shakes both faces"] = 400,
            ["cornerout glazed"] = 43,
            // A corner's left end post gives way to two fillers: 43 - 6 + 2 * 6 = 49.
            ["cornerout glazed, merged left"] = 49,
            // A corner's rims are cut round each leg's opening: the front rims leg 1 and the second front leg 2,
            // each as the corner post's piece, its end post's, and one over the opening and one under it,
            // 4 boxes * 6 = 24 apiece. Back rims both legs: the post's piece, both end posts' and two over
            // and two under, 7 * 6 = 42. Unjoined, 43 + 24 + 24 + 42 = 133. Merged left, the left end post's
            // piece becomes two fillers' on the second front and the back: 49 + 24 + 30 + 48 = 151.
            // Merged all round only the corner post's pieces are left: 15 + 6 + 6 + 6 = 33.
            ["cornerout glazed, daub both faces"] = 133,
            ["cornerout glazed, daub both faces, merged left"] = 151,
            ["cornerout glazed, daub both faces, merged all round"] = 33,
            ["cornerout glazed, merged all round"] = 15,
            ["wall shakes front, daub elsewhere"] = 185,
            ["cornerout shakes front, daub elsewhere"] = 220,
            ["wall weatherboard front, shakes back"] = 137,
            ["cornerout weatherboard front, shakes back"] = 332,
            ["wall brick both faces"] = 142,
            ["wall ashlar both faces"] = 72,
            ["wall rubble both faces"] = 198,
            ["cornerout brick both faces"] = 256,
            ["cornerout ashlar both faces"] = 136,
            ["cornerout rubble both faces"] = 371,
        };

        var finishes = SidingWallEntityTests.Dict("""
        {
            "daub": {},
            "planks": { "Elements": { "front": "front-weatherboard", "back": "back-boards" } },
            "shakes": { "Elements": { "front": "front-shakes", "back": "back-logs" } },
            "brick": { "Elements": { "front": "front-brick", "back": "back-brick" } },
            "ashlar": { "Elements": { "front": "front-ashlar", "back": "back-ashlar" } },
            "rubble": { "Elements": { "front": "front-rubble", "back": "back-rubble" } }
        }
        """);

        // Front, second front and back are picked independently (decisions 0007 and 0009), so the
        // last two rows give each face a different finish. The same-name prune is per element
        // group, and those groups only stay independent if a mixed cell still names all three.
        (string State, string? Infill, string? Front, string? SecondFront, string? Back,
            (bool, bool, bool, bool) Joins, bool Glazed)[] states =
        [
            ("bare frame", null, null, null, null, (false, false, false, false), false),
            ("wattle", "wattle", null, null, null, (false, false, false, false), false),
            ("wattle, mid-stack", "wattle", null, null, null, (true, true, false, false), false),
            ("daub both faces", "wattle", "daub", "daub", "daub", (false, false, false, false), false),
            ("weatherboard both faces", "wattle", "planks", "planks", "planks", (false, false, false, false), false),
            ("shakes both faces", "wattle", "shakes", "shakes", "shakes", (false, false, false, false), false),
            ("glazed", "glass", null, null, null, (false, false, false, false), true),
            ("glazed, merged left", "glass", null, null, null, (false, false, true, false), true),
            ("glazed, daub both faces", "glass", "daub", "daub", "daub", (false, false, false, false), true),
            ("glazed, daub both faces, merged left", "glass", "daub", "daub", "daub", (false, false, true, false), true),
            ("glazed, daub both faces, merged all round", "glass", "daub", "daub", "daub", (true, true, true, true), true),
            ("glazed, merged all round", "glass", null, null, null, (true, true, true, true), true),
            ("shakes front, daub elsewhere", "wattle", "shakes", "daub", "daub", (false, false, false, false), false),
            ("weatherboard front, shakes back", "wattle", "planks", "shakes", "shakes", (false, false, false, false), false),
            ("brick both faces", "wattle", "brick", "brick", "brick", (false, false, false, false), false),
            ("ashlar both faces", "wattle", "ashlar", "ashlar", "ashlar", (false, false, false, false), false),
            ("rubble both faces", "wattle", "rubble", "rubble", "rubble", (false, false, false, false), false),
        ];

        static Dictionary<string, int> Quads(string layout) => WallShapeGen.Generate(layout)["elements"]!
            .GroupBy(e => (string)e["name"]!)
            .ToDictionary(g => g.Key, g => g.Sum(e => ((JObject)e["faces"]!).Properties().Count()));

        var actual = new Dictionary<string, int>();
        foreach (var layout in new[] { "wall", "cornerout" })
        {
            var quads = Quads(layout);

            foreach (var (state, infill, front, secondFront, back, joins, glazed) in states)
            {
                var names = SidingWallEntity.SelectiveElements(
                    layout, "oak", infill, front, layout == "cornerout" ? secondFront : null, back,
                    finishes, joins, glazed);
                actual[$"{layout} {state}"] = names.Sum(n => quads[n]);
            }

            foreach (var (state, frame) in new[] { ("pole frame", "poles"), ("second pole frame", "poles2") })
            {
                actual[$"{layout} {state}"] = SidingWallEntity.SelectiveElements(
                    layout, "sticks", null, null, null, null, finishes, (false, false, false, false), false, frame: frame)
                    .Sum(n => quads[n]);
            }
        }

        var floorQuads = Quads("floor");
        foreach (var (state, framing, frame) in new[] { ("bare frame", "oak", "framing"), ("pole frame", "sticks", "poles"), ("second pole frame", "sticks", "poles2") })
        {
            actual[$"floor {state}"] = SidingFloorEntity.SelectiveElements(
                framing, null, null, null, finishes, (false, false, false, false), frame: frame)
                .Sum(n => floorQuads[n]);
        }

        foreach (var (state, finish, joins) in new (string, string?, (bool, bool, bool, bool))[]
        {
            ("glazed", null, (false, false, false, false)),
            ("glazed, merged left", null, (false, false, true, false)),
            ("glazed, daub both faces", "daub", (false, false, false, false)),
            ("glazed, daub both faces, merged left", "daub", (false, false, true, false)),
            ("glazed, daub both faces, merged all round", "daub", (true, true, true, true)),
        })
        {
            actual[$"floor {state}"] = SidingFloorEntity.SelectiveElements(
                "oak", "glass", finish, finish, finishes, joins, glazed: true)
                .Sum(n => floorQuads[n]);
        }

        Assert.Equal(expected, actual);
    }

    private static readonly string[] Sides = ["west", "south", "east", "north"];

    internal static double[][] DeckBoxesVoxels(string layout, string side)
        => SidingWallBlock.AddOpenPartBoxes(Array.Empty<Cuboidf>(), layout, side, "oak", null)
            .Select(box => new[] { box.X1, box.Y1, box.Z1, box.X2, box.Y2, box.Z2 }.Select(v => Math.Round(v * 16.0, 3) + 0.0).ToArray())
            .ToArray();

    // The deck is the floor's layers clipped to its area, a copy per side, each staying inside the box
    // SidingWallBlock gives that side's deck, and all of them on the deck's own texture codes, or on the
    // lashing's, which is one rope whatever the frame.
    [Theory]
    [InlineData("wall")]
    [InlineData("cornerout")]
    [InlineData("diagonal")]
    public void EverySidesDeckGroupsLieInsideItsDeckBox(string layout)
    {
        var elements = WallShapeGen.Generate(layout)["elements"]!.Cast<JObject>().ToArray();
        var expected = new Dictionary<string, string[]>();
        var actual = new Dictionary<string, string[]>();
        foreach (string side in Sides)
        {
            var boxes = DeckBoxesVoxels(layout, side);
            var groups = elements.Where(e => ((string)e["name"]!).StartsWith($"deck-{side}-")).ToArray();
            Assert.NotEmpty(groups);

            expected[side] = [];
            actual[side] = groups
                .Where(e => !boxes.Any(box => Inside(e, box)))
                .Select(e => (string)e["name"]!)
                .ToArray();
            Assert.All(groups.SelectMany(g => ((JObject)g["faces"]!).Properties()),
                p => Assert.Contains((string)p.Value["texture"]!, new[] { "#deck", "#deckinfill", "#deckfront", "#deckback", "#lashing" }));
        }

        Assert.Equal(expected, actual);
    }

    private static bool Inside(JObject element, double[] box)
    {
        var lo = element["from"]!.Select(v => (double)v!).ToArray();
        var hi = element["to"]!.Select(v => (double)v!).ToArray();
        return Enumerable.Range(0, 3).All(i => lo[i] >= box[i] && hi[i] <= box[i + 3]);
    }

    // A clipped face samples the texels the floor's whole face has there: a flat face reads its uv
    // the floor's way, and a rotated one runs u along z and v against x (up) or with x (down).
    [Theory]
    [InlineData("wall")]
    [InlineData("cornerout")]
    public void ClippedDeckFacesKeepTheFloorsUv(string layout)
    {
        var elements = WallShapeGen.Generate(layout)["elements"]!.Cast<JObject>().ToArray();
        var expected = new Dictionary<string, string>();
        var actual = new Dictionary<string, string>();
        foreach (string side in Sides)
        {
            var b = DeckBoxesVoxels(layout, side).Single();
            double x0 = b[0], z0 = b[2], x1 = b[3], z1 = b[5];
            var cases = new (string Name, string Face, double[] Uv)[]
            {
                ("front-hboards", "up", [x0, z0, x1, z1]),
                ("front-boards", "up", [z0, 16 - x1, z1, 16 - x0]),
                ("back-hboards", "down", [16 - x1, z0, 16 - x0, z1]),
                ("back-boards", "down", [z0, x0, z1, x1]),
            };
            foreach (var (name, face, uv) in cases)
            {
                string key = $"{side} {name} {face}";
                expected[key] = new JArray(uv).ToString();
                actual[key] = elements.Single(e => (string)e["name"]! == $"deck-{side}-{name}")["faces"]![face]!["uv"]!.ToString();
            }
        }

        Assert.Equal(expected, actual);
    }

    // A rim piece samples the texels the uncut finish has at the same spot, so a lap or a board carries on
    // from the cell beside it. Written out by hand for a flat, a rotated and a positional group, on the
    // wall and on both of the corner's legs, and on the floor and one deck copy of it.
    [Fact]
    public void RimPiecesKeepTheUncutFinishesUv()
    {
        var cases = new (string Layout, string Name, double[] From, string Face, double[] Uv)[]
        {
            ("wall", "front-rim-top", [0, 14, 2], "west", [2, 0, 14, 2]),
            ("wall", "front-rim-top", [0, 14, 2], "up", [0, 2, 1, 14]),
            ("wall", "front-rim-bottom-right", [0, 0, 14], "west", [14, 14, 16, 16]),
            ("wall", "front-boards-rim-bottom", [0, 0, 2], "west", [0, 2, 2, 14]),
            ("wall", "front-weatherboard-rim-top-right", [0.75, 15, 14], "west", [14, 0, 16, 1]),
            ("wall", "front-weatherboard-rim-top-right", [0.5, 14, 14], "west", [14, 1, 16, 2]),
            ("wall", "back-rim-left", [3, 0, 0], "east", [14, 0, 16, 16]),
            ("wall", "back-boards-rim-top", [3, 14, 2], "east", [14, 2, 16, 14]),
            ("cornerout", "front-rim-top", [0, 14, 4], "west", [4, 0, 14, 2]),
            ("cornerout", "front-rim-top", [0, 14, 4], "up", [0, 4, 1, 14]),
            ("cornerout", "front-boards-rim-bottom", [0, 0, 4], "west", [0, 4, 2, 14]),
            ("cornerout", "front-weatherboard-rim-top", [0.75, 15, 4], "west", [3.5, 0, 13.5, 1]),
            ("cornerout", "front-weatherboard-rim-top", [0.5, 14, 4], "west", [3.5, 1, 13.5, 2]),
            ("cornerout", "secondfront-rim-top", [4, 14, 0], "north", [2, 0, 12, 2]),
            ("cornerout", "secondfront-rim-left", [14, 0, 0], "north", [0, 0, 2, 16]),
            ("cornerout", "back-rim-right", [3, 0, 14], "east", [0, 0, 2, 16]),
            ("cornerout", "front-rim-top-right", [0, 14, 14], "west", [14, 0, 16, 2]),
            ("cornerout", "secondfront-rim-bottom-left", [14, 0, 0], "north", [0, 14, 2, 16]),
            ("cornerout", "back-rim-top", [4, 14, 3], "south", [1, 0, 11, 2]),
            ("floor", "front-rim-left", [0, 15, 0], "up", [0, 0, 2, 16]),
            ("floor", "front-rim-top", [2, 15, 0], "up", [2, 0, 14, 2]),
            ("floor", "front-rim-bottom-right", [14, 15, 14], "up", [14, 14, 16, 16]),
            ("floor", "front-boards-rim-top", [2, 15, 0], "up", [0, 2, 2, 14]),
            ("floor", "front-boards-rim-right", [14, 15, 0], "up", [0, 0, 16, 2]),
            ("floor", "back-hboards-rim-left", [0, 12, 0], "down", [14, 0, 16, 16]),
            ("floor", "back-boards-rim-top", [2, 12, 0], "down", [0, 2, 2, 14]),
            // A deck copy is a cut of a cut, so it reads the same spot of the uncut finish: the top rail
            // from x 4, where the west deck begins, and the boards' rail with u along z and v against x.
            ("wall", "deck-west-front-rim-top", [4, 15, 0], "up", [4, 0, 14, 2]),
            ("wall", "deck-west-front-boards-rim-top", [4, 15, 0], "up", [0, 2, 2, 12]),
        };

        var shapes = cases.Select(c => c.Layout).Distinct()
            .ToDictionary(layout => layout, layout => WallShapeGen.Generate(layout)["elements"]!.Cast<JObject>().ToArray());
        var expected = new Dictionary<string, string>();
        var actual = new Dictionary<string, string>();
        foreach (var (layout, name, from, face, uv) in cases)
        {
            string key = $"{layout} {name} {string.Join(",", from)} {face}";
            expected[key] = new JArray(uv).ToString();
            actual[key] = shapes[layout].Single(e => (string)e["name"]! == name && Box(e).Lo.SequenceEqual(from))["faces"]![face]!["uv"]!.ToString();
        }

        Assert.Equal(expected, actual);
    }

    private static readonly string[] FaceOrder = ["north", "east", "south", "west", "up", "down"];

    // The uv the game gives each corner of a face, from its own vertex tables, the way ShapeTesselator
    // reads a face's rect and rotation: the rect's origin is (u0, v1), and a rotation steps the corners round.
    private static (double[] Xyz, double[] Uv)[] FaceCorners(JObject element, string face)
    {
        var (lo, hi) = Box(element);
        var uv = element["faces"]![face]!["uv"]!.Select(v => (double)v!).ToArray();
        int rotation = (int)((double?)element["faces"]![face]!["rotation"] ?? 0) / 90;
        int index = Array.IndexOf(FaceOrder, face);
        return Enumerable.Range(0, 4).Select(corner =>
        {
            int at = index * 8 + 2 * ((rotation + corner) % 4);
            var xyz = Enumerable.Range(0, 3).Select(a => CubeMeshUtil.CubeVertices[index * 12 + corner * 3 + a] > 0 ? hi[a] : lo[a]).ToArray();
            return (xyz, new[] { uv[0] + (uv[2] - uv[0]) * CubeMeshUtil.CubeUvCoords[at], uv[3] + (uv[1] - uv[3]) * CubeMeshUtil.CubeUvCoords[at + 1] });
        }).ToArray();
    }

    // A diagonal's finish rim cuts a piece that reaches on to the corner, so its source keeps that piece's
    // trailing -left or -right, which a wall's or corner's rim part is its own.
    private static JObject UncutSource(JObject[] elements, JObject rim, string layout)
    {
        string name = (string)rim["name"]!;
        var (lo, hi) = Box(rim);
        string source = name[..name.IndexOf("-rim")];
        if (layout == "diagonal" && name.Split('-') is ["front" or "back", .., var end and ("left" or "right")]) source += "-" + end;
        return elements.Single(e => (string)e["name"]! == source
            && Box(e) is var (sLo, sHi) && Enumerable.Range(0, 3).All(a => sLo[a] <= lo[a] && sHi[a] >= hi[a]));
    }

    // A rim piece samples what the uncut finish samples at the same spot, at every corner of every face
    // both draw, worked out from the game's tables and not from the generator's own reading of them.
    [Theory]
    [InlineData("wall")]
    [InlineData("cornerout")]
    [InlineData("floor")]
    [InlineData("diagonal")]
    public void EveryRimPieceSamplesWhatTheUncutFinishSamplesThere(string layout)
    {
        var elements = WallShapeGen.Generate(layout)["elements"]!.Cast<JObject>().ToArray();
        var rims = elements.Where(e => ((string)e["name"]!).Contains("-rim")).ToArray();
        Assert.NotEmpty(rims);

        var offenders = new List<string>();
        foreach (var rim in rims)
        {
            var source = UncutSource(elements, rim, layout);
            foreach (string face in ((JObject)rim["faces"]!).Properties().Select(p => p.Name).Where(((JObject)source["faces"]!).ContainsKey))
            {
                var uncut = FaceCorners(source, face);
                foreach (var (xyz, uv) in FaceCorners(rim, face))
                {
                    // An uncut face's uv is a plane over it, set by its first corner and that corner's two neighbours.
                    double[] there = [.. uncut[0].Uv];
                    foreach (var next in new[] { uncut[1], uncut[3] })
                    {
                        int axis = Enumerable.Range(0, 3).Single(a => next.Xyz[a] != uncut[0].Xyz[a]);
                        double along = (xyz[axis] - uncut[0].Xyz[axis]) / (next.Xyz[axis] - uncut[0].Xyz[axis]);
                        there = [there[0] + along * (next.Uv[0] - uncut[0].Uv[0]), there[1] + along * (next.Uv[1] - uncut[0].Uv[1])];
                    }

                    if (Math.Abs(uv[0] - there[0]) > 0.001 || Math.Abs(uv[1] - there[1]) > 0.001)
                        offenders.Add($"{rim["name"]} {face} at {string.Join(",", xyz)}: {string.Join(",", uv)}, the uncut finish has {string.Join(",", there)}");
                }
            }
        }

        Assert.Equal([], offenders);
    }

    // Every cut leaves an end that looks onto the glass or the next piece, so a rim piece draws a face at
    // each plane it was cut at, whatever its source group draws.
    [Theory]
    [InlineData("wall")]
    [InlineData("cornerout")]
    [InlineData("floor")]
    [InlineData("diagonal")]
    public void EveryRimPieceDrawsAFaceOnEachPlaneItWasCutAt(string layout)
    {
        var elements = WallShapeGen.Generate(layout)["elements"]!.Cast<JObject>().ToArray();
        var rims = elements.Where(e => ((string)e["name"]!).Contains("-rim")).ToArray();
        Assert.NotEmpty(rims);

        string[] lowFaces = ["west", "down", "north"], highFaces = ["east", "up", "south"];
        var offenders = new List<string>();
        foreach (var rim in rims)
        {
            string name = (string)rim["name"]!;
            var (lo, hi) = Box(rim);
            var (sourceLo, sourceHi) = Box(UncutSource(elements, rim, layout));
            var cuts = Enumerable.Range(0, 3).SelectMany(a => new[] { (lowFaces[a], lo[a] > sourceLo[a]), (highFaces[a], hi[a] < sourceHi[a]) });
            offenders.AddRange(cuts.Where(c => c.Item2 && !((JObject)rim["faces"]!).ContainsKey(c.Item1)).Select(c => $"{name} at {string.Join(",", lo)} has no {c.Item1} face"));
        }

        Assert.Equal([], offenders);
    }

    // The faces a group limits itself to are the ones its neighbours leave showing, and a cut shows new ones.
    [Fact]
    public void ARimPieceAddsTheFacesItsCutsExposeToAGroupThatLimitsItsFaces()
    {
        Element[] elements =
        [
            new("glazing-top", (1, 14, 2), (3, 16, 14), "framing", UvRule.Flat),
            new("front-limited", (0, 0, 0), (1, 16, 16), "front", UvRule.Flat, Faces: ["west", "up"]),
        ];

        Assert.Equal(["west", "up", "north", "south", "down"], WallShapeGen.RimPieces(elements).Single().Faces!);
    }

    // On a floor the face is the top, so the cuts are on x and z and the group keeps its own y.
    [Fact]
    public void AFloorRimPieceAddsTheFacesItsCutsOnXAndZExpose()
    {
        Element[] elements =
        [
            new("glazing-top-left", (0, 13, 0), (2, 15, 2), "framing", UvRule.Flat),
            new("front-limited", (0, 15, 0), (16, 16, 16), "front", UvRule.Flat, Faces: ["up"]),
        ];

        var rim = WallShapeGen.RimPieces(elements, 'x', 'z').Single();
        Assert.Equal(
            ("front-limited-rim-top-left", (0.0, 15.0, 0.0), (2.0, 16.0, 2.0), "up,east,south"),
            (rim.Name, rim.From, rim.To, string.Join(",", rim.Faces!)));
    }

    [Fact]
    public void DiagonalDeckTopsSitBelowTheFillersCaps()
    {
        var elements = WallShapeGen.Generate("diagonal")["elements"]!.Cast<JObject>()
            .Where(e => ((string)e["name"]!).StartsWith("deck-")).ToArray();

        Assert.NotEmpty(elements);
        Assert.Equal([], elements.Where(e => (double)e["to"]![1]! > 15.98).Select(e => (string)e["name"]!));
    }

    // A corner filler is unturned, so a deck box that ends on one of its outer planes and overlaps it
    // there draws a second face in that plane. The fillers turn with the wall's side and the deck groups
    // are cut per side, so each side's are compared where they stand.
    [Fact]
    public void NoDiagonalDeckBoxEndsOnAFillersPlane()
    {
        var elements = WallShapeGen.Generate("diagonal")["elements"]!.ToArray();
        var offenders = new List<string>();
        foreach (string side in Sides)
        {
            var fillers = elements.Where(e => (string)e["name"]! is "framing-left" or "framing-right").Select(Box)
                .Select(b => WallShapeGen.DeckRegion((b.Lo[0], b.Lo[1], b.Lo[2]), (b.Hi[0], b.Hi[1], b.Hi[2]), (int)SidingWallEntity.RotationYDeg(side)))
                .Select(r => (Lo: new[] { r.X, r.Y, r.Z }, Hi: new[] { r.X2, r.Y2, r.Z2 }))
                .ToArray();
            foreach (var deck in elements.Where(e => ((string)e["name"]!).StartsWith($"deck-{side}-")))
            {
                var (lo, hi) = Box(deck);
                foreach (var filler in fillers)
                {
                    for (int axis = 0; axis < 3; axis++)
                    {
                        bool shares = Math.Abs(lo[axis] - filler.Lo[axis]) < 1e-6 || Math.Abs(hi[axis] - filler.Hi[axis]) < 1e-6;
                        if (shares && Enumerable.Range(0, 3).Where(k => k != axis).All(k => Math.Max(lo[k], filler.Lo[k]) < Math.Min(hi[k], filler.Hi[k])))
                            offenders.Add($"{deck["name"]} {string.Join(",", lo)} to {string.Join(",", hi)}");
                    }
                }
            }
        }

        Assert.Equal([], offenders);
    }

    [Theory]
    [InlineData("step-lower", 4, 0, 0, 16, 8, 16)]
    [InlineData("step-upper", 4, 8, 0, 16, 16, 16)]
    [InlineData("step-lower-north", 4, 0, 0, 16, 8, 8)]
    [InlineData("step-lower-south", 4, 0, 8, 16, 8, 16)]
    [InlineData("step-upper-north", 4, 8, 0, 16, 16, 8)]
    [InlineData("step-upper-south", 4, 8, 8, 16, 16, 16)]
    public void StepElementsFillTheOpenPartInLowerUpperHalves(
        string name, double fx, double fy, double fz, double tx, double ty, double tz)
    {
        var step = WallShapeGen.Generate("wall")["elements"]!
            .Single(e => (string)e["name"]! == name);

        Assert.Equal(new JArray(fx, fy, fz).ToString(), step["from"]!.ToString());
        Assert.Equal(new JArray(tx, ty, tz).ToString(), step["to"]!.ToString());
        Assert.All(((JObject)step["faces"]!).Properties(), p => Assert.Equal("#step", p.Value["texture"]!.ToString()));
    }

    [Fact]
    public void CornerOutHasNoStepElements()
    {
        var names = WallShapeGen.Generate("cornerout")["elements"]!.Select(e => (string)e["name"]!);

        Assert.DoesNotContain(names, n => n.StartsWith("step"));
    }

    // A masonry unit has to sit where the texture paints one, and the golden file cannot say so -
    // it is rewritten from this same generator. So the joints are written out by hand, read off
    // the textures: clay/brick/four/running/cream1 puts two joints per 4-voxel course, half a unit
    // apart course to course; stone/brick/{rock}1 puts one per 8-voxel course, at the opposite
    // phase. Get the unit width or the phase wrong and a modelled joint lands mid-stone, which
    // renders as a groove down the middle of a brick rather than as an error.
    [Theory]
    [InlineData("wall")]
    [InlineData("cornerout")]
    public void EveryMasonryUnitSitsBetweenThePaintedJoints(string layout)
    {
        (double Y0, double Y1, double Z0, double Z1)[] brick =
        [
            (1, 4, 0, 3), (1, 4, 4, 11), (1, 4, 12, 16),
            (5, 8, 0, 7), (5, 8, 8, 15),
            (9, 12, 0, 3), (9, 12, 4, 11), (9, 12, 12, 16),
            (13, 16, 0, 7), (13, 16, 8, 15),
        ];
        (double Y0, double Y1, double Z0, double Z1)[] ashlar =
        [
            (1, 8, 0, 15),
            (9, 16, 0, 7), (9, 16, 8, 16),
        ];

        Assert.Equal(brick, Lips(layout, "front-brick"));
        Assert.Equal(ashlar, Lips(layout, "front-ashlar"));
    }

    // The unit lips, not the mortar plane behind them: the plane is the one box spanning the face.
    private static (double Y0, double Y1, double Z0, double Z1)[] Lips(string layout, string group)
        => WallShapeGen.Generate(layout)["elements"]!
            .Where(e => (string)e["name"]! == group && (double)e["from"]![0]! == 0)
            .Select(e => ((double)e["from"]![1]!, (double)e["to"]![1]!,
                          (double)e["from"]![2]!, (double)e["to"]![2]!))
            .ToArray();

    // Decision 0028: each lap samples the texture where it sits on the wall, so the sixteen laps
    // of a block are sixteen different slices rather than the top course repeated four times.
    // A rule that went back to course-relative v would show the same four pixel rows per board.
    [Theory]
    [InlineData("front-weatherboard", "west")]
    [InlineData("back-weatherboard", "east")]
    public void EveryWeatherboardLapSamplesItsOwnSliceOfTheTexture(string group, string outward)
    {
        var expected = Enumerable.Range(0, 16).Select(y => (15.0 - y, 16.0 - y)).ToArray();

        var actual = WallShapeGen.Generate("wall")["elements"]!
            .Where(e => (string)e["name"]! == group)
            .Select(e => e["faces"]![outward]!["uv"]!)
            .Select(uv => ((double)uv[1]!, (double)uv[3]!))
            .ToArray();

        Assert.Equal(expected, actual);
    }

    // Decision 0041: the three infill boxes are adjoining slices of one 0..16 texture. A rule that
    // went back to box-relative v would restart each at 0 and show a seam at a stacked join.
    [Theory]
    [InlineData("wall")]
    [InlineData("cornerout")]
    [InlineData("diagonal")]
    public void EveryInfillBoxSamplesItsOwnSliceOfTheTexture(string layout)
    {
        var slices = new Dictionary<string, (double, double)>
        {
            ["infill-top"] = (0, 1),
            ["infill"] = (1, 15),
            ["infill-bottom"] = (15, 16),
        };
        string[] sides = ["north", "south", "east", "west"];

        var actual = WallShapeGen.Generate(layout)["elements"]!
            .Where(e => slices.ContainsKey((string)e["name"]!))
            .SelectMany(e => sides.Select(s => e["faces"]![s]).Where(f => f != null)
                .Select(f => (Name: (string)e["name"]!, V: ((double)f!["uv"]![1]!, (double)f["uv"]![3]!))))
            .ToArray();
        var expected = actual.Select(f => (f.Name, slices[f.Name])).ToArray();

        Assert.NotEmpty(actual);
        Assert.Equal(expected, actual);
    }

    // Each group of one pole variant beside the plain group it stands in for.
    private static (string Pole, string Plain)[] PolesOf(string layout, string prefix)
        => (layout != "cornerout" ? new[] { "-left", "-right", "-top", "-bottom" } : ["", "-top", "-bottom"])
            .Select(suffix => (prefix + suffix, "framing" + suffix)).ToArray();

    private static (double[] Lo, double[] Hi) Box(JToken element)
        => (element["from"]!.Select(v => (double)v!).ToArray(), element["to"]!.Select(v => (double)v!).ToArray());

    // A wall's frame is thick on x; a corner's second leg is thick on z; a floor's is thick on y, at the top of the cell.
    private static (int[] Axes, double Lo, double Hi) Frame(string layout) => layout switch
    {
        "wall" => ([0], 1, 3),
        "cornerout" => ([0, 2], 1, 3),
        _ => ([1], 13, 15),
    };

    private static bool Thick(double[] lo, double[] hi, int axis, (int[] Axes, double Lo, double Hi) frame)
        => lo[axis] >= frame.Lo && hi[axis] <= frame.Hi;

    // A pole that leaves the frame's thickness shows through a finish. A corner's frame is an L, so a
    // box inside one leg's thickness also has to start clear of the slab on the other leg's outer face.
    [Theory]
    [InlineData("wall", "poles")]
    [InlineData("wall", "poles2")]
    [InlineData("cornerout", "poles")]
    [InlineData("cornerout", "poles2")]
    [InlineData("floor", "poles")]
    [InlineData("floor", "poles2")]
    public void EveryPoleBoxLiesInsideTheFramesThickness(string layout, string prefix)
    {
        string[] groups = PolesOf(layout, prefix).Select(p => p.Pole).ToArray();
        var frame = Frame(layout);
        var offenders = new List<string>();
        foreach (var element in WallShapeGen.Generate(layout)["elements"]!.Where(e => groups.Contains((string)e["name"]!)))
        {
            var (lo, hi) = Box(element);
            if (!frame.Axes.Any(depth => Thick(lo, hi, depth, frame) && (layout != "cornerout" || lo[2 - depth] >= 1)))
                offenders.Add($"{element["name"]} spans {string.Join(",", lo)} to {string.Join(",", hi)}");
        }

        Assert.Equal([], offenders);
    }

    // A pole group stands in for a plain member, so where the member's mid-plane had material, the
    // group's has too; otherwise a glance along the wall finds a gap the plain frame did not have.
    [Theory]
    [InlineData("wall", "poles")]
    [InlineData("wall", "poles2")]
    [InlineData("cornerout", "poles")]
    [InlineData("cornerout", "poles2")]
    [InlineData("floor", "poles")]
    [InlineData("floor", "poles2")]
    public void EveryPoleGroupCoversTheMidPlaneOfThePlainMemberItReplaces(string layout, string prefix)
    {
        var elements = WallShapeGen.Generate(layout)["elements"]!.ToArray();
        var frame = Frame(layout);
        double mid = (frame.Lo + frame.Hi) / 2;
        var offenders = new List<string>();
        foreach (var (group, plain) in PolesOf(layout, prefix))
        {
            var poles = elements.Where(e => (string)e["name"]! == group).Select(Box).ToArray();
            foreach (var (lo, hi) in elements.Where(e => (string)e["name"]! == plain).Select(Box))
            {
                foreach (int depth in frame.Axes.Where(axis => Thick(lo, hi, axis, frame)))
                {
                    int[] across = [.. Enumerable.Range(0, 3).Where(axis => axis != depth)];
                    var cover = poles.Where(b => b.Lo[depth] < mid && b.Hi[depth] > mid).ToArray();
                    // The pole edges inside the member cut it into rectangles, each covered whole or open whole.
                    double[] Cuts(int axis) => cover.SelectMany(b => new[] { b.Lo[axis], b.Hi[axis] })
                        .Where(c => c > lo[axis] && c < hi[axis]).Append(lo[axis]).Append(hi[axis]).Distinct().Order().ToArray();
                    var (firsts, seconds) = (Cuts(across[0]), Cuts(across[1]));
                    for (int i = 0; i < firsts.Length - 1; i++)
                    {
                        for (int j = 0; j < seconds.Length - 1; j++)
                        {
                            double u = (firsts[i] + firsts[i + 1]) / 2, v = (seconds[j] + seconds[j + 1]) / 2;
                            if (!cover.Any(b => b.Lo[across[0]] < u && u < b.Hi[across[0]] && b.Lo[across[1]] < v && v < b.Hi[across[1]]))
                                offenders.Add($"{group} leaves {"xyz"[across[0]]} {firsts[i]}..{firsts[i + 1]}, {"xyz"[across[1]]} {seconds[j]}..{seconds[j + 1]} of {plain} open");
                        }
                    }
                }
            }
        }

        Assert.Equal([], offenders);
    }

    // The ledge is the deck's reach into the slot a wall's room-side finish takes. Every ledge box has to
    // lie where that side's back slab would, at the deck's height, or laying the finish leaves a piece of
    // deck standing in it. That holds for every copy of it, the ones behind a glazed cell's rim too.
    [Theory]
    [InlineData("wall")]
    [InlineData("cornerout")]
    public void EveryLedgeBoxLiesInTheSlotOfTheRoomSideFinish(string layout)
    {
        var elements = WallShapeGen.Generate(layout)["elements"]!.ToArray();
        var backs = elements.Where(e => (string)e["name"]! == "back").Select(Box).ToArray();
        var offenders = new List<string>();
        foreach (string side in Sides)
        {
            var slots = backs.Select(b => WallShapeGen.DeckRegion((b.Lo[0], 12, b.Lo[2]), (b.Hi[0], 16, b.Hi[2]), (int)SidingWallEntity.RotationYDeg(side))).ToArray();
            var ledge = elements.Where(e => ((string)e["name"]!).Split('-') is [var copy, var of, ..] && copy.StartsWith("ledge") && of == side).ToArray();
            Assert.NotEmpty(ledge);
            foreach (var element in ledge)
            {
                var (lo, hi) = Box(element);
                if (!slots.Any(s => lo[0] >= s.X && lo[1] >= s.Y && lo[2] >= s.Z && hi[0] <= s.X2 && hi[1] <= s.Y2 && hi[2] <= s.Z2))
                    offenders.Add($"{element["name"]} {string.Join(",", lo)} to {string.Join(",", hi)}");
            }
        }

        Assert.Equal([], offenders);
    }

    // A stub, a cheek or a lashing never crosses the frame's mid-plane, so it has to be joined, through
    // boxes of its own group, to a joist or a rim that does. A deck is the floor clipped to its side, and
    // the clip once took a joist and left its stubs; a group's name survives that when it holds two joists.
    [Theory]
    [InlineData("floor")]
    [InlineData("wall")]
    [InlineData("cornerout")]
    [InlineData("diagonal")]
    public void EveryPieceOfAFloorOrDeckPoleGroupHangsOffAMember(string layout)
    {
        var frame = Frame("floor");
        double mid = (frame.Lo + frame.Hi) / 2;
        var groups = WallShapeGen.Generate(layout)["elements"]!
            .Where(e => layout == "floor" || ((string)e["name"]!).StartsWith("deck-") || ((string)e["name"]!).StartsWith("ledge"))
            .Where(e => ((string)e["name"]!).Contains("poles"))
            .GroupBy(e => (string)e["name"]!, Box);
        Assert.NotEmpty(groups);

        var offenders = new List<string>();
        foreach (var group in groups)
        {
            var joined = group.Where(b => b.Lo[1] < mid && mid < b.Hi[1]).ToList();
            var loose = group.Except(joined).ToList();
            while (loose.FirstOrDefault(b => joined.Any(j => Enumerable.Range(0, 3).All(k => b.Lo[k] <= j.Hi[k] && j.Lo[k] <= b.Hi[k]))) is { Lo: not null } next)
            {
                joined.Add(next);
                loose.Remove(next);
            }
            offenders.AddRange(loose.Select(b => $"{group.Key} {string.Join(",", b.Lo)} to {string.Join(",", b.Hi)}"));
        }

        Assert.Equal([], offenders);
    }

    // Two faces that look the same way from one plane and overlap fight over the pixels. Only groups
    // a built cell draws together can meet: one pole variant, the infill, and the plain finishes.
    // The infill's top and bottom slivers fill a dropped plate, so they never meet a pole plate.
    [Theory]
    [InlineData("wall", "poles")]
    [InlineData("wall", "poles2")]
    [InlineData("cornerout", "poles")]
    [InlineData("cornerout", "poles2")]
    [InlineData("floor", "poles")]
    [InlineData("floor", "poles2")]
    public void NoTwoEmittedFacesOfGroupsDrawnTogetherShareAPlaneAndOverlap(string layout, string prefix)
    {
        string[] together = [.. PolesOf(layout, prefix).Select(p => p.Pole), "infill", "infill-pane", "front", "secondfront", "back"];
        Assert.Equal([], SharedPlaneOffenders(layout, together));
    }

    // A glazed wall's or floor's frame is stiles and rails that meet edge to edge. Where a stile has merged away
    // its rails end on fillers instead, so a filler is never drawn with the stile on its side. A glazed corner's
    // end posts and fillers are a wall's, and its corner post is drawn with every set. A cell merged all round draws only the fillers of its inside
    // corners. A glazed cell's finish is drawn as a rim piece for each frame part, so those meet the same way.
    [Theory]
    [InlineData("wall")]
    [InlineData("floor")]
    [InlineData("cornerout")]
    public void NoTwoFacesOfAGlazedCellsFramePiecesShareAPlaneAndOverlap(string layout)
    {
        string[][] uprights =
        [
            ["glazing-left", "glazing-right"],
            ["glazing-bottom-left", "glazing-top-left", "glazing-right"],
            ["glazing-bottom-right", "glazing-top-right", "glazing-left"],
            ["glazing-bottom-left", "glazing-top-left", "glazing-bottom-right", "glazing-top-right"],
        ];
        if (layout == "cornerout") uprights = [.. uprights.Select(set => (string[])["glazing", .. set])];

        string[][] frames = [.. uprights.Select(set => (string[])["glazing-top", "glazing-bottom", .. set])];
        frames = [.. frames, uprights[^1]];

        Assert.Equal([], frames.SelectMany(frame =>
        {
            string[] groups = layout == "cornerout" ? ["front", "secondfront", "back"] : ["front", "back"];
            string[] rims = [.. frame.SelectMany(part => groups.Select(group => group + "-rim" + part["glazing".Length..]))];
            return SharedPlaneOffenders(layout, [.. frame, "infill-pane", .. rims]);
        }).Distinct());
    }

    // At a T a corner's post fills the finish slot the run crosses, glazed or with a plain or rough frame, in every set of joins:
    // a glazed face's rim gives way to the post's piece, and an opaque face's finish slab takes the slot in its place.
    [Fact]
    public void NoFaceOfACornerPostsPieceAtATSharesAPlaneWithAnotherAndOverlaps()
    {
        bool[] either = [false, true];
        Assert.Equal([], (
            from glazed in either
            from frame in glazed ? ["framing"] : new[] { "framing", "poles", "poles2" }
            from above in either
            from below in either
            from left in either
            from right in either
            from teeLeft in either
            from teeRight in either
            where teeLeft || teeRight
            from finished in either
            let names = Built("cornerout", finished, (above, below, left, right), tees: (teeLeft, teeRight), glazed: glazed, frame: frame)
            from offender in SharedPlaneOffenders("cornerout", names, (a, b) => a.Contains("-tee") || b.Contains("-tee"))
            select $"{(glazed ? "glazed" : frame)} {offender}").Distinct());
    }

    // A glazed diagonal's rails, pane and rims are turned together and meet as a wall's do, in every set of joins, with the glass
    // running on through each end the run carries on from or not, and with an inside corner there or not. Its corner fillers are
    // the unturned pieces, which a box read without the turn cannot be compared with.
    [Fact]
    public void NoTwoFacesOfAGlazedDiagonalsTurnedPiecesShareAPlaneAndOverlap()
    {
        static bool Filler(string name) => name is "framing-left" or "framing-right";
        bool[] either = [false, true];
        string[][] innerCorners = [[], ["-top-left", "-top-right", "-bottom-left", "-bottom-right"]];
        Assert.Equal([], (
            from above in either from below in either from left in either from right in either
            from glassLeft in either.Where(glass => !glass || left) from glassRight in either.Where(glass => !glass || right)
            from corners in innerCorners
            from finished in either
            let names = Built("diagonal", finished, (above, below, left, right), corners, (glassLeft, glassRight))
            select SharedPlaneOffenders("diagonal", names, between: (a, b) => Filler(a) == Filler(b))
        ).SelectMany(offenders => offenders).Distinct().ToList());
    }

    // Behind a decked glazed cell's rim the deck's ledge stands where the rim's top would, between the stiles
    // or out to the edge where one has merged away. Read off the names the wall itself asks for, on the side
    // whose deck copy lies the way the unturned shape does. Only a deck piece against a piece of the wall
    // counts: a floor's infill runs through its joists, so any cut of one has the two end on one plane.
    [Theory]
    [InlineData("wall", false, false)]
    [InlineData("wall", true, false)]
    [InlineData("wall", false, true)]
    [InlineData("wall", true, true)]
    [InlineData("cornerout", false, false)]
    [InlineData("cornerout", true, false)]
    [InlineData("cornerout", false, true)]
    [InlineData("cornerout", true, true)]
    public void ADeckedGlazedCellsLedgeSharesNoPlaneWithItsRimOrFrame(string layout, bool left, bool right)
    {
        var finishes = new Vintagestory.API.Datastructures.JsonObject(JObject.Parse("""{ "daub": {} }"""));

        Assert.Equal([], new[] { false, true }.SelectMany(above =>
        {
            var joins = (above, false, left, right);
            string[] ledges = SidingWallEntity.Ledges(layout, roomSideBare: false, glazed: true, joins);
            return SharedPlaneOffenders(layout,
            [
                .. SidingWallEntity.SelectiveElements(
                    layout, "oak", "glass", "daub", layout == "cornerout" ? "daub" : null, "daub", finishes, joins, glazed: true, decked: true),
                .. SidingWallEntity.DeckElements("west", "oak", "wattle", "daub", "daub", finishes, default, default, false, ledges: ledges),
            ], between: (a, b) => a.Contains("-west-") != b.Contains("-west-"));
        }).Distinct());
    }

    private static List<string> SharedPlaneOffenders(string layout, string[] together, Func<string, string, bool>? between = null)
    {
        string[] facings = ["west", "east", "down", "up", "north", "south"];
        var faces = new List<(string Name, string Face, int Axis, double Plane, double[] Lo, double[] Hi)>();
        foreach (var element in Generated.GetOrAdd(layout, WallShapeGen.Generate)["elements"]!.Where(e => together.Contains((string)e["name"]!)))
        {
            var (lo, hi) = Box(element);
            foreach (var face in ((JObject)element["faces"]!).Properties())
            {
                int facing = Array.IndexOf(facings, face.Name);
                faces.Add(((string)element["name"]!, face.Name, facing / 2, (facing % 2 == 0 ? lo : hi)[facing / 2], lo, hi));
            }
        }

        var offenders = new List<string>();
        for (int i = 0; i < faces.Count; i++)
        {
            for (int j = i + 1; j < faces.Count; j++)
            {
                var (a, b) = (faces[i], faces[j]);
                if (a.Face != b.Face || a.Plane != b.Plane || between?.Invoke(a.Name, b.Name) == false) continue;
                if (Enumerable.Range(0, 3).Where(k => k != a.Axis).All(k => Math.Max(a.Lo[k], b.Lo[k]) < Math.Min(a.Hi[k], b.Hi[k])))
                    offenders.Add($"{a.Name} and {b.Name} both draw {a.Face} at {a.Plane}");
            }
        }

        return offenders;
    }

    // A face a built cell draws, turned and moved to world voxels (16 to a block) with its cell's offset.
    private record WorldFace(int Cell, string Name, BlockFacing Facing, double Plane, double[] Lo, double[] Hi);

    // Where an unrotated horizontal facing points once a cell's shape is turned for `side`.
    internal static BlockFacing TurnFacing(BlockFacing unrotated, string side)
    {
        Vec3i n = unrotated.Normali;
        var slab = new Cuboidf(0.5f + n.X * 0.4f - 0.05f, 0, 0.5f + n.Z * 0.4f - 0.05f, 0.5f + n.X * 0.4f + 0.05f, 1, 0.5f + n.Z * 0.4f + 0.05f);
        Cuboidf box = slab.RotatedCopy(0, SidingWallEntity.RotationYDeg(side), 0, new Vec3d(0.5, 0.5, 0.5));
        int dx = (int)Math.Round(((box.X1 + box.X2) / 2 - 0.5f) * 2.5), dz = (int)Math.Round(((box.Z1 + box.Z2) / 2 - 0.5f) * 2.5);
        return BlockFacing.HORIZONTALS.First(f => f.Normali.X == dx && f.Normali.Z == dz);
    }

    // Every meeting reads the same few shapes, and the diagonal's takes seconds to generate.
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, JObject> Generated = new();

    // `zShift` moves the cell along z by any amount, as the next diagonal along a run lies in the shape's own frame.
    private static List<WorldFace> WorldFaces(int cell, string layout, string side, string[] names, BlockFacing? offset, double zShift = 0)
    {
        var faces = new List<WorldFace>();
        foreach (var element in Generated.GetOrAdd(layout, WallShapeGen.Generate)["elements"]!.Where(e => names.Contains((string)e["name"]!)))
        {
            var (lo, hi) = Box(element);
            var turned = new Cuboidf((float)lo[0] / 16, (float)lo[1] / 16, (float)lo[2] / 16, (float)hi[0] / 16, (float)hi[1] / 16, (float)hi[2] / 16)
                .RotatedCopy(0, SidingWallEntity.RotationYDeg(side), 0, new Vec3d(0.5, 0.5, 0.5));
            double dx = (offset?.Normali.X ?? 0) * 16, dz = (offset?.Normali.Z ?? 0) * 16 + zShift;
            double[] wlo = [Math.Round(turned.X1 * 16 + dx, 3), Math.Round(turned.Y1 * 16, 3), Math.Round(turned.Z1 * 16 + dz, 3)];
            double[] whi = [Math.Round(turned.X2 * 16 + dx, 3), Math.Round(turned.Y2 * 16, 3), Math.Round(turned.Z2 * 16 + dz, 3)];
            foreach (var face in ((JObject)element["faces"]!).Properties())
            {
                var facing = BlockFacing.FromCode(face.Name);
                if (facing.IsHorizontal) facing = TurnFacing(facing, side);
                double[] at = facing == BlockFacing.EAST || facing == BlockFacing.SOUTH || facing == BlockFacing.UP ? whi : wlo;
                faces.Add(new(cell, (string)element["name"]!, facing, at[AxisIndex(facing)], wlo, whi));
            }
        }

        return faces;
    }

    private static int AxisIndex(BlockFacing facing) => facing.Axis == EnumAxis.X ? 0 : facing.Axis == EnumAxis.Y ? 1 : 2;

    private static string[] Built(string layout, bool finished, (bool above, bool below, bool left, bool right) joins,
        string[]? innerCorners = null, (bool left, bool right) glassRunsOn = default, (bool left, bool right) tees = default,
        bool glazed = true, string frame = "framing")
    {
        var finishes = new Vintagestory.API.Datastructures.JsonObject(JObject.Parse("""{ "daub": {} }"""));
        string? daub = finished ? "daub" : null;
        return SidingWallEntity.SelectiveElements(layout, "oak", glazed ? "glass" : "wattle", daub, layout == "cornerout" ? daub : null, daub, finishes, joins,
            glazed: glazed, frame: frame, innerCorners: innerCorners, glassRunsOn: glassRunsOn, tees: tees);
    }

    // Each pair of faces, one from each cell, that point the same way on the same plane and overlap there.
    private static IEnumerable<string> CrossCellOverlaps(List<WorldFace> a, List<WorldFace> b)
        => from p in a
           from q in b
           where p.Facing == q.Facing && p.Plane == q.Plane
               && Enumerable.Range(0, 3).Where(k => k != AxisIndex(p.Facing)).All(k => Math.Max(p.Lo[k], q.Lo[k]) < Math.Min(p.Hi[k], q.Hi[k]))
           select $"{p.Name} and {q.Name} both draw {p.Facing.Code} at {p.Plane}";

    // The names of `own`'s faces toward `toward` on `plane` with a point, sampled at half voxels, that no face of `covering` meets pointing back.
    private static List<string> Uncovered(IEnumerable<WorldFace> own, IEnumerable<WorldFace> covering, BlockFacing toward, double plane)
    {
        int[] axes = [.. Enumerable.Range(0, 3).Where(k => k != AxisIndex(toward))];
        bool Covers(WorldFace q, double u, double v) => q.Facing == toward.Opposite && q.Plane == plane
            && q.Lo[axes[0]] <= u && u <= q.Hi[axes[0]] && q.Lo[axes[1]] <= v && v <= q.Hi[axes[1]];

        return (from p in own
                where p.Facing == toward && p.Plane == plane
                from u in Samples(p.Lo[axes[0]], p.Hi[axes[0]])
                from v in Samples(p.Lo[axes[1]], p.Hi[axes[1]])
                where !covering.Any(q => Covers(q, u, v))
                select p.Name).Distinct().OrderBy(name => name).ToList();
    }

    private static (bool above, bool below, bool left, bool right) JoinsToward(
        string layout, string side, BlockFacing towards, string otherLayout, string otherSide)
    {
        if (layout == "diagonal") return default;
        var (left, right) = SidingWallBlock.LeftRightEnds(layout, side);
        bool Meets(SidingWallBlock.End end) => end.Points == towards && SidingWallBlock.JoinsAtEnd(end, otherLayout, otherSide);
        return (false, false, Meets(left), Meets(right));
    }

    // A corner's post ends that the other cell's end meets, a T (SidingWallBlock.Tees).
    private static (bool left, bool right) TeesToward(string layout, string side, BlockFacing towards, string otherLayout, string otherSide)
    {
        if (layout != "cornerout") return default;
        var ends = SidingWallBlock.Ends(layout, side);
        bool Meets(SidingWallBlock.End end) => end.Points == towards && SidingWallBlock.JoinsAtEnd(end, otherLayout, otherSide);
        return (Meets(ends[2]), Meets(ends[3]));
    }

    // A at the origin and B one block off along `towards`, each dropping its end post where the other's end meets it. Only a
    // diagonal's two fillers are axis-aligned, so they stand for it: its other parts are turned 45 degrees and meet the other
    // cell only through them.
    private static (List<WorldFace> A, List<WorldFace> B) Meeting(
        (string Layout, string Side) a, (string Layout, string Side) b, BlockFacing towards, bool finished)
    {
        List<WorldFace> Faces(int cell, (string Layout, string Side) own, (string Layout, string Side) other, BlockFacing facing, BlockFacing? offset)
        {
            var joins = JoinsToward(own.Layout, own.Side, facing, other.Layout, other.Side);
            string[] names = own.Layout == "diagonal"
                ? Built("diagonal", false, default).Where(name => name is "framing-left" or "framing-right").ToArray()
                : Built(own.Layout, finished, joins, tees: TeesToward(own.Layout, own.Side, facing, other.Layout, other.Side));
            return WorldFaces(cell, own.Layout, own.Side, names, offset);
        }

        return (Faces(0, a, b, towards, null), Faces(1, b, a, towards.Opposite, towards));
    }

    // A glazed diagonal-west and the glazed diagonal on its left, each with its glass running on to the other, as the faces of their
    // turned pieces in the shape's own frame: a cell along the run is the same frame moved 2 x 8 root 2 along z, the left's at the
    // negative end. The left cell is a diagonal-west, which carries the run on from its right end, or a diagonal-east, turned end for
    // end, which carries it on from its left. The unturned corner fillers are left out, as are the deck's strips. Each cell is merged
    // up and down as given, and frames the inside corner its sheet turns where it is merged and the other is not.
    private static (List<WorldFace> A, List<WorldFace> B) GlassRunningOn(
        (bool Above, bool Below) cellA, string sideB, (bool Above, bool Below) cellB, bool finished)
    {
        string[] Names((bool Above, bool Below) own, (bool Above, bool Below) other, bool left)
        {
            var joins = (own.Above, own.Below, left, !left);
            string[] corners = SidingWallBlock.InnerCorners(joins, (rail, _) => rail == "-top" ? other.Above : other.Below);
            return [.. Built("diagonal", finished, joins, corners, (left, !left)).Where(name => name is not ("framing-left" or "framing-right"))];
        }

        return (WorldFaces(0, "diagonal", "west", Names(cellA, cellB, left: true), null),
            WorldFaces(1, "diagonal", sideB, Names(cellB, cellA, left: sideB == "east"), null, -2 * WallShapeGen.DiagonalCorner));
    }

    public static IEnumerable<object[]> StackedAndFinished()
    {
        bool[] either = [false, true];
        return from sideB in new[] { "west", "east" }
               from aAbove in either from aBelow in either from bAbove in either from bBelow in either from finished in either
               select new object[] { aAbove, aBelow, sideB, bAbove, bBelow, finished };
    }

    [Theory]
    [MemberData(nameof(StackedAndFinished))]
    public void TwoGlazedDiagonalsWhoseGlassRunsOnDrawNoFaceOnTheSamePlaneAndOverlap(
        bool aAbove, bool aBelow, string sideB, bool bAbove, bool bBelow, bool finished)
    {
        var (a, b) = GlassRunningOn((aAbove, aBelow), sideB, (bAbove, bBelow), finished);

        Assert.Equal([], CrossCellOverlaps(a, b).Distinct().ToList());
    }

    // Where the two cells meet, each face one draws on the boundary is met by the other's facing back, so no gap shows where the post has gone.
    // A rail draws no face across its run, so a piece that ends along the run without one, anywhere short of the two cells' outer ends, has
    // to be carried on by another piece of either cell: a stack that stops short of the other's leaves no rail end open at the inside corner.
    [Theory]
    [MemberData(nameof(StackedAndFinished))]
    public void TwoGlazedDiagonalsWhoseGlassRunsOnMeetFlushAcrossTheirBoundary(
        bool aAbove, bool aBelow, string sideB, bool bAbove, bool bBelow, bool finished)
    {
        var (a, b) = GlassRunningOn((aAbove, aBelow), sideB, (bAbove, bBelow), finished);
        double plane = Math.Round(8 - WallShapeGen.DiagonalCorner, 3);

        // A piece's faces share the one box array, so they group by it.
        var pieces = a.Concat(b).GroupBy(face => (face.Cell, face.Lo)).ToList();
        static double End(WorldFace box, BlockFacing facing) => facing == BlockFacing.NORTH ? box.Lo[2] : box.Hi[2];
        double outerLo = b.Min(face => face.Lo[2]), outerHi = a.Max(face => face.Hi[2]);
        var open = from piece in pieces
                   let box = piece.First()
                   from facing in new[] { BlockFacing.NORTH, BlockFacing.SOUTH }
                   let at = End(box, facing)
                   where outerLo < at && at < outerHi && !piece.Any(face => face.Facing == facing)
                   from y in Samples(box.Lo[1], box.Hi[1])
                   from x in Samples(box.Lo[0], box.Hi[0])
                   where !pieces.Select(other => other.First()).Any(q => End(q, facing.Opposite) == at
                       && q.Lo[1] <= y && y <= q.Hi[1] && q.Lo[0] <= x && x <= q.Hi[0])
                   select $"{box.Name} {facing.Code} at {at}";

        Assert.Equal([], Uncovered(a, b, BlockFacing.NORTH, plane).Concat(Uncovered(b, a, BlockFacing.SOUTH, plane)).Concat(open.Distinct()).ToList());
        if (finished && !(aAbove && aBelow && bAbove && bBelow)) Assert.Contains(a, face => face.Facing == BlockFacing.NORTH && face.Plane == plane);
    }

    // Where a glazed diagonal's run carries on but its half post stays, the finish covers the post's front and back from the cell's bottom
    // to its top however the cell is stacked, as a wall's stile rim does, so no bare framing shows at the seam with the cell above or below.
    [Fact]
    public void AKeptDiagonalJoinPostIsRimmedOverItsWholeFrontAndBack()
    {
        bool[] either = [false, true];
        var actual = from above in either
                     from below in either
                     let names = Built("diagonal", true, (above, below, true, false)).Where(name => name is not ("framing-left" or "framing-right"))
                     let faces = WorldFaces(0, "diagonal", "west", [.. names], null)
                     let post = faces.Where(face => face.Name == "framing-join-left").ToList()
                     from toward in new[] { BlockFacing.WEST, BlockFacing.EAST }
                     from name in Uncovered(post, faces, toward, post.Single(face => face.Facing == toward).Plane)
                     select $"{name} {toward.Code} (above {above}, below {below})";

        Assert.Equal([], actual.ToList());
    }

    // Every ordered pair of cells bar two diagonals, the second one block off along the first's end, whose ends meet there,
    // where at least one of the two drops its end post.
    public static IEnumerable<object[]> Meetings()
    {
        string[] layouts = ["wall", "cornerout", "diagonal"];
        string[] sides = ["west", "south", "east", "north"];
        static bool Drops(string layout, string side, BlockFacing towards, string otherLayout, string otherSide)
        {
            var joins = JoinsToward(layout, side, towards, otherLayout, otherSide);
            return joins.left || joins.right;
        }

        return from layoutA in layouts
               from sideA in sides
               from layoutB in layouts
               where layoutA != "diagonal" || layoutB != "diagonal"
               from sideB in sides
               from end in SidingWallBlock.Ends(layoutA, sideA)
               where SidingWallBlock.JoinsAtEnd(end, layoutB, sideB)
                   && (Drops(layoutA, sideA, end.Points, layoutB, sideB) || Drops(layoutB, sideB, end.Points.Opposite, layoutA, sideA))
               select new object[] { layoutA, sideA, layoutB, sideB, end.Points.Code };
    }

    [Theory]
    [MemberData(nameof(Meetings))]
    public void TwoGlazedCellsWhoseEndsMeetDrawNoFaceOnTheSamePlaneAndOverlap(string layoutA, string sideA, string layoutB, string sideB, string towards)
    {
        Assert.Equal([], new[] { false, true }.SelectMany(finished =>
        {
            var (a, b) = Meeting((layoutA, sideA), (layoutB, sideB), BlockFacing.FromCode(towards), finished);
            return CrossCellOverlaps(a, b).Select(overlap => $"{overlap} (finished {finished})");
        }).Distinct().ToList());
    }

    // The faces a cell that has dropped its end post draws on the boundary, pointing into its neighbour, are all covered by
    // the neighbour's pointing back, so no gap shows where the post has gone. Sampled at half voxels. A neighbour's corner post
    // starts a finish slot in, which it fills at a T; against a diagonal the room side's finish rims the 1.17 mitre ledge and
    // the 0.01 hairs at the filler's top and bottom (0066).
    // A neighbour that keeps its own post or filler is the jamb, which the dropped end's cell need not be covered by in turn.
    [Theory]
    [MemberData(nameof(Meetings))]
    public void EveryFaceACellDrawsOnItsDroppedEndIsCoveredByTheCellBeyondBarTheListedSlotsLedgesAndHairs(
        string layoutA, string sideA, string layoutB, string sideB, string towards)
    {
        var toward = BlockFacing.FromCode(towards);
        int plane = toward == BlockFacing.NORTH || toward == BlockFacing.WEST ? 0 : 16;

        string[] Expected(bool finished, string dropped, string otherLayout)
            => otherLayout == "diagonal" && finished ? [$"back-rim-bottom-{dropped}", $"back-rim-top-{dropped}"] : [];

        var actual = new List<string>();
        var expected = new List<string>();
        foreach (bool finished in new[] { false, true })
        {
            var (a, b) = Meeting((layoutA, sideA), (layoutB, sideB), toward, finished);
            foreach (var (own, other, faces, beyond, facing) in new[]
            {
                ((Layout: layoutA, Side: sideA), (Layout: layoutB, Side: sideB), a, b, toward),
                ((Layout: layoutB, Side: sideB), (Layout: layoutA, Side: sideA), b, a, toward.Opposite),
            })
            {
                var joins = JoinsToward(own.Layout, own.Side, facing, other.Layout, other.Side);
                if (!joins.left && !joins.right) continue;
                string cell = $"{own.Layout}-{own.Side} {finished}";
                actual.AddRange(Uncovered(faces, beyond, facing, plane).Select(name => $"{cell} {name}"));
                expected.AddRange(Expected(finished, joins.left ? "left" : "right", other.Layout).Select(name => $"{cell} {name}"));
            }
        }

        Assert.Equal(expected, actual);
    }

    // Every T at a corner's post bar glass to glass, which Meetings has: the cell whose end meets the post's, and whether the
    // corner and that cell are glazed. Neither drops a member, since glass merges only into glass.
    public static IEnumerable<object[]> TeesBarGlassToGlass()
    {
        string[] sides = ["west", "south", "east", "north"];
        bool[] either = [false, true];
        return from side in sides
               from post in SidingWallBlock.Ends("cornerout", side)[2..]
               from layout in new[] { "wall", "cornerout", "diagonal" }
               from otherSide in sides
               where SidingWallBlock.JoinsAtEnd(post, layout, otherSide)
               from cornerGlazed in either
               from otherGlazed in either
               where !(cornerGlazed && otherGlazed)
               select new object[] { side, post.Points.Code, layout, otherSide, cornerGlazed, otherGlazed };
    }

    // The corner's post fills the slot beside it, so every face a wall or a corner's leg draws on the boundary, pointing at the
    // corner, is covered by the corner's pointing back, and no two faces across it share a plane, finished and bare. A diagonal,
    // standing as its two fillers as in Meeting, keeps a filler that is its own jamb, and a corner whose post meets this one's has
    // its leg face to face with this one's, so neither leaves a gap for the post to fill and only their overlaps are checked.
    [Theory]
    [MemberData(nameof(TeesBarGlassToGlass))]
    public void TheCellAtACornerPostsTMeetsItFlushWithNoSharedPlane(string side, string towards, string layout, string otherSide, bool cornerGlazed, bool otherGlazed)
    {
        var toward = BlockFacing.FromCode(towards);
        int plane = toward == BlockFacing.NORTH || toward == BlockFacing.WEST ? 0 : 16;
        var tees = TeesToward("cornerout", side, toward, layout, otherSide);
        var post = SidingWallBlock.Ends("cornerout", side)[2..].Single(end => end.Points == toward);
        var (otherLeft, otherRight) = SidingWallBlock.LeftRightEnds(layout, otherSide);
        bool flush = layout != "diagonal" && (SidingWallBlock.EndsMeet(post, otherLeft) || SidingWallBlock.EndsMeet(post, otherRight));
        var actual = new List<string>();
        foreach (bool finished in new[] { false, true })
        {
            string[] otherNames = layout == "diagonal"
                ? [.. Built("diagonal", false, default).Where(name => name is "framing-left" or "framing-right")]
                : Built(layout, finished, default, glazed: otherGlazed);
            var corner = WorldFaces(0, "cornerout", side, Built("cornerout", finished, default, tees: tees, glazed: cornerGlazed), null);
            var other = WorldFaces(1, layout, otherSide, otherNames, toward);
            actual.AddRange(CrossCellOverlaps(corner, other).Select(overlap => $"{finished} {overlap}"));
            if (flush) actual.AddRange(Uncovered(other, corner, toward.Opposite, plane).Select(name => $"{finished} {name}"));
        }

        Assert.Equal([], actual);
    }

    private static IEnumerable<double> Samples(double lo, double hi)
    {
        for (double v = lo + 0.25; v < hi; v += 0.5) yield return v;
    }
}
