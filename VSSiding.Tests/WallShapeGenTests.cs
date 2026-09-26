using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
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
    public void GeneratedShapeMatchesTheCommittedOne(string layout)
    {
        var repoRoot = MaterialTextureOpacityTests.GetAssemblyMetadata("RepoRoot");
        var path = Path.Combine(repoRoot, "VSSiding", "assets", "vssiding", "shapes", "block", "wall", layout + ".json");

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
    public void NoElementIsPrunedDownToNothing(string layout)
    {
        Assert.Equal([], WallShapeGen.Generate(layout)["elements"]!
            .Where(e => !((JObject)e["faces"]!).Properties().Any())
            .Select(e => (string)e["name"]!)
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
            ["wall wattle"] = 30,
            ["wall wattle, mid-stack"] = 30,
            ["wall daub both faces"] = 42,
            ["wall weatherboard both faces"] = 117,
            ["wall shakes both faces"] = 205,
            ["wall glazed"] = 26,
            ["wall glazed, merged all round"] = 2,
            ["cornerout bare frame"] = 42,
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
        }

        Assert.Equal(expected, actual);
    }

    // The deck fills the wall's open 12/16 flush with the top of the cell, textured like framing
    // but through its own "deck" texture code so it reads the Deck layer, not Framing.
    [Theory]
    [InlineData("wall", 4, 12, 0, 16, 16, 16)]
    [InlineData("cornerout", 4, 12, 4, 16, 16, 16)]
    public void DeckFillsTheOpenPartFlushWithTheTopOfTheCell(
        string layout, double fx, double fy, double fz, double tx, double ty, double tz)
    {
        var deck = WallShapeGen.Generate(layout)["elements"]!
            .Single(e => (string)e["name"]! == "deck");

        Assert.Equal(new JArray(fx, fy, fz).ToString(), deck["from"]!.ToString());
        Assert.Equal(new JArray(tx, ty, tz).ToString(), deck["to"]!.ToString());
        Assert.All(((JObject)deck["faces"]!).Properties(), p => Assert.Equal("#deck", p.Value["texture"]!.ToString()));
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
}
