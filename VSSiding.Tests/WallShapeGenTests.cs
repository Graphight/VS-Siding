using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
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
    // The poles* groups are left out: the committed shape and the pole invariants pin them.
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
            "glazing-left 0.25,13,0 2,15,16",
            "glazing-right 14,13,0 15.75,15,16",
            "glazing-top 0,13.25,0.25 16,14.75,2",
            "glazing-bottom 0,13.25,14 16,14.75,15.75",
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
            .Where(e => !((string)e["name"]!).StartsWith("poles"))
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
            ["wall glazed"] = 26,
            ["wall glazed, merged all round"] = 2,
            ["cornerout bare frame"] = 42,
            ["cornerout pole frame"] = 158,
            ["cornerout second pole frame"] = 158,
            ["cornerout wattle"] = 54,
            ["cornerout wattle, mid-stack"] = 54,
            ["cornerout daub both faces"] = 77,
            ["cornerout weatherboard both faces"] = 227,
            ["cornerout shakes both faces"] = 400,
            ["cornerout glazed"] = 46,
            ["cornerout glazed, merged all round"] = 22,
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

        Assert.Equal(expected, actual);
    }

    private static readonly string[] Sides = ["west", "south", "east", "north"];

    private static double[] DeckBoxVoxels(string layout, string side)
    {
        var box = SidingWallBlock.AddOpenPartBoxes(Array.Empty<Cuboidf>(), layout, side, "oak", null).Single();
        return new[] { box.X1, box.Y1, box.Z1, box.X2, box.Y2, box.Z2 }.Select(v => Math.Round(v * 16.0, 3) + 0.0).ToArray();
    }

    // The deck is the floor's layers clipped to its area, a copy per side, each staying inside the box
    // SidingWallBlock gives that side's deck, and all of them on the deck's own texture codes, or on the
    // lashing's, which is one rope whatever the frame.
    [Theory]
    [InlineData("wall")]
    [InlineData("cornerout")]
    public void EverySidesDeckGroupsLieInsideItsDeckBox(string layout)
    {
        var elements = WallShapeGen.Generate(layout)["elements"]!.Cast<JObject>().ToArray();
        var expected = new Dictionary<string, string[]>();
        var actual = new Dictionary<string, string[]>();
        foreach (string side in Sides)
        {
            var box = DeckBoxVoxels(layout, side);
            var groups = elements.Where(e => ((string)e["name"]!).StartsWith($"deck-{side}-")).ToArray();
            Assert.NotEmpty(groups);

            expected[side] = [];
            actual[side] = groups
                .Where(e => !Inside(e, box))
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
            var b = DeckBoxVoxels(layout, side);
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
    // deck standing in it.
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
            var ledge = elements.Where(e => ((string)e["name"]!).StartsWith($"ledge-{side}-")).ToArray();
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
    public void EveryPieceOfAFloorOrDeckPoleGroupHangsOffAMember(string layout)
    {
        var frame = Frame("floor");
        double mid = (frame.Lo + frame.Hi) / 2;
        var groups = WallShapeGen.Generate(layout)["elements"]!
            .Where(e => layout == "floor" || ((string)e["name"]!).StartsWith("deck-") || ((string)e["name"]!).StartsWith("ledge-"))
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
        string[] facings = ["west", "east", "down", "up", "north", "south"];
        var faces = new List<(string Name, string Face, int Axis, double Plane, double[] Lo, double[] Hi)>();
        foreach (var element in WallShapeGen.Generate(layout)["elements"]!.Where(e => together.Contains((string)e["name"]!)))
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
                if (a.Face != b.Face || a.Plane != b.Plane) continue;
                if (Enumerable.Range(0, 3).Where(k => k != a.Axis).All(k => Math.Max(a.Lo[k], b.Lo[k]) < Math.Min(a.Hi[k], b.Hi[k])))
                    offenders.Add($"{a.Name} and {b.Name} both draw {a.Face} at {a.Plane}");
            }
        }

        Assert.Equal([], offenders);
    }
}
