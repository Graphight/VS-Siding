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

    // The depths in the shake table are a tuning knob, and a knob gets turned. A box that inverts
    // or runs past the framing renders as a hole rather than an error, so the bound is asserted
    // here instead of being re-checked by hand after every tune.
    [Theory]
    [InlineData("wall")]
    [InlineData("cornerout")]
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
            offenders.AddRange(
                from v in lo.Concat(hi)
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
    [InlineData("floor")]
    public void NoElementIsPrunedDownToNothing(string layout)
    {
        Assert.Equal([], WallShapeGen.Generate(layout)["elements"]!
            .Where(e => !((JObject)e["faces"]!).Properties().Any())
            .Select(e => (string)e["name"]!)
            .ToArray());
    }

    // The floor's whole layout, written out: a 4/16 panel at y 12..16, three joists running north-south.
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

        var actual = new Dictionary<string, int>();
        foreach (var layout in new[] { "wall", "cornerout" })
        {
            var quads = WallShapeGen.Generate(layout)["elements"]!
                .GroupBy(e => (string)e["name"]!)
                .ToDictionary(g => g.Key, g => g.Sum(e => ((JObject)e["faces"]!).Properties().Count()));

            foreach (var (state, infill, front, secondFront, back, joins, glazed) in states)
            {
                var names = SidingWallEntity.SelectiveElements(
                    layout, "oak", infill, front, layout == "cornerout" ? secondFront : null, back,
                    finishes, joins, glazed);
                actual[$"{layout} {state}"] = names.Sum(n => quads[n]);
            }

            actual[$"{layout} pole frame"] = SidingWallEntity.SelectiveElements(
                layout, "sticks", null, null, null, null, finishes, (false, false, false, false), false, frame: "poles")
                .Sum(n => quads[n]);
            actual[$"{layout} second pole frame"] = SidingWallEntity.SelectiveElements(
                layout, "sticks", null, null, null, null, finishes, (false, false, false, false), false, frame: "poles2")
                .Sum(n => quads[n]);
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
    // SidingWallBlock gives that side's deck, and all of them on the deck's own texture codes.
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
                p => Assert.Contains((string)p.Value["texture"]!, new[] { "#deck", "#deckinfill", "#deckfront", "#deckback" }));
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

    // Each pole group beside the plain group it stands in for, for both variants.
    private static readonly (string Pole, string Plain)[] WallPoles =
        [("poles-left", "framing-left"), ("poles-right", "framing-right"), ("poles-top", "framing-top"), ("poles-bottom", "framing-bottom"),
         ("poles2-left", "framing-left"), ("poles2-right", "framing-right"), ("poles2-top", "framing-top"), ("poles2-bottom", "framing-bottom")];
    private static readonly (string Pole, string Plain)[] CornerPoles =
        [("poles", "framing"), ("poles-top", "framing-top"), ("poles-bottom", "framing-bottom"),
         ("poles2", "framing"), ("poles2-top", "framing-top"), ("poles2-bottom", "framing-bottom")];

    private static (string Pole, string Plain)[] PolesOf(string layout) => layout == "wall" ? WallPoles : CornerPoles;

    // The groups one built cell draws, so two variants are never drawn together.
    private static IEnumerable<string[]> VariantsOf(string layout)
        => PolesOf(layout).Select(p => p.Pole).GroupBy(n => n.StartsWith("poles2") ? 2 : 1).Select(g => g.ToArray());

    private static (double[] Lo, double[] Hi) Box(JToken element)
        => (element["from"]!.Select(v => (double)v!).ToArray(), element["to"]!.Select(v => (double)v!).ToArray());

    private static bool Thick(double[] lo, double[] hi, int axis) => lo[axis] >= 1 && hi[axis] <= 3;

    // A pole that leaves the frame's thickness shows through a finish on either face.
    [Theory]
    [InlineData("wall")]
    [InlineData("cornerout")]
    public void EveryPoleBoxLiesInsideTheFramesThickness(string layout)
    {
        string[] groups = PolesOf(layout).Select(p => p.Pole).ToArray();
        var offenders = new List<string>();
        foreach (var element in WallShapeGen.Generate(layout)["elements"]!.Where(e => groups.Contains((string)e["name"]!)))
        {
            var (lo, hi) = Box(element);
            bool inside = Thick(lo, hi, 0) || layout == "cornerout" && Thick(lo, hi, 2);
            if (!inside) offenders.Add($"'{element["name"]}' spans x {lo[0]}..{hi[0]}, z {lo[2]}..{hi[2]}");
        }

        Assert.Equal([], offenders);
    }

    // A pole group stands in for a plain member, so where the member's mid-plane had material, the
    // group's has too; otherwise a glance along the wall finds a gap the plain frame did not have.
    [Theory]
    [InlineData("wall")]
    [InlineData("cornerout")]
    public void EveryPoleGroupCoversTheMidPlaneOfThePlainMemberItReplaces(string layout)
    {
        var elements = WallShapeGen.Generate(layout)["elements"]!.ToArray();
        var offenders = new List<string>();
        foreach (var (group, plain) in PolesOf(layout))
        {
            foreach (var plainBox in elements.Where(e => (string)e["name"]! == plain).Select(Box))
            {
                foreach (int axis in new[] { 0, 2 }.Where(a => Thick(plainBox.Lo, plainBox.Hi, a) && (layout == "cornerout" || a == 0)))
                {
                    int[] across = Enumerable.Range(0, 3).Where(k => k != axis).ToArray();
                    var cover = elements.Where(e => (string)e["name"]! == group).Select(Box).Where(b => b.Lo[axis] < 2 && b.Hi[axis] > 2).ToArray();
                    var us = cover.SelectMany(b => new[] { b.Lo[across[0]], b.Hi[across[0]] }).Concat([plainBox.Lo[across[0]], plainBox.Hi[across[0]]]).Distinct().Order().ToArray();
                    var vs = cover.SelectMany(b => new[] { b.Lo[across[1]], b.Hi[across[1]] }).Concat([plainBox.Lo[across[1]], plainBox.Hi[across[1]]]).Distinct().Order().ToArray();
                    for (int i = 0; i < us.Length - 1; i++)
                    {
                        for (int j = 0; j < vs.Length - 1; j++)
                        {
                            double u = (us[i] + us[i + 1]) / 2, v = (vs[j] + vs[j + 1]) / 2;
                            bool inPlain = u > plainBox.Lo[across[0]] && u < plainBox.Hi[across[0]] && v > plainBox.Lo[across[1]] && v < plainBox.Hi[across[1]];
                            if (inPlain && !cover.Any(b => u > b.Lo[across[0]] && u < b.Hi[across[0]] && v > b.Lo[across[1]] && v < b.Hi[across[1]]))
                                offenders.Add($"{group} leaves axis {across[0]} {us[i]}..{us[i + 1]}, axis {across[1]} {vs[j]}..{vs[j + 1]} of a '{plain}' box open at axis {axis} = 2");
                        }
                    }
                }
            }
        }

        Assert.Equal([], offenders);
    }

    // Two faces that look the same way from one plane and overlap fight over the pixels. Only groups
    // a built cell draws together can meet: one pole frame, the infill, and the plain finishes.
    // The infill's top and bottom slivers fill a dropped plate, so they never meet a pole plate.
    [Theory]
    [InlineData("wall")]
    [InlineData("cornerout")]
    public void NoTwoEmittedFacesOfGroupsDrawnTogetherShareAPlaneAndOverlap(string layout)
    {
        var offenders = new List<string>();
        foreach (var variant in VariantsOf(layout)) offenders.AddRange(SamePlaneOverlaps(layout, variant));

        Assert.Equal([], offenders);
    }

    private static List<string> SamePlaneOverlaps(string layout, string[] poles)
    {
        string[] together =
        [
            .. poles, "infill", "infill-pane", "front", "back",
            .. layout == "cornerout" ? new[] { "secondfront" } : [],
        ];
        string[] facings = ["west", "east", "down", "up", "north", "south"];
        int[] axes = [0, 0, 1, 1, 2, 2];
        var faces = new List<(string Name, string Face, double Plane, double[] Lo, double[] Hi)>();
        foreach (var element in WallShapeGen.Generate(layout)["elements"]!.Where(e => together.Contains((string)e["name"]!)))
        {
            var (lo, hi) = Box(element);
            foreach (var face in ((JObject)element["faces"]!).Properties())
            {
                int facing = Array.IndexOf(facings, face.Name);
                faces.Add(((string)element["name"]!, face.Name, facing % 2 == 0 ? lo[axes[facing]] : hi[axes[facing]], lo, hi));
            }
        }

        var offenders = new List<string>();
        for (int i = 0; i < faces.Count; i++)
        {
            for (int j = i + 1; j < faces.Count; j++)
            {
                var (a, b) = (faces[i], faces[j]);
                int axis = axes[Array.IndexOf(facings, a.Face)];
                if (a.Face != b.Face || a.Plane != b.Plane) continue;
                if (Enumerable.Range(0, 3).Where(k => k != axis).All(k => Math.Max(a.Lo[k], b.Lo[k]) < Math.Min(a.Hi[k], b.Hi[k])))
                    offenders.Add($"{a.Name} and {b.Name} both draw {a.Face} at {a.Plane}");
            }
        }

        return offenders;
    }
}
