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

    // RunAxis is the generator's other piece of arithmetic, and the golden test cannot check it
    // either - that file is regenerated from this same generator. The rule is that a split segment
    // samples the texture at its own position along the run, so u must equal the box's own run
    // coordinates. Restarting each segment at 0 is the bug this catches.
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

    // UvRule.Course is the only arithmetic the generator carries, and the golden test cannot check
    // it: that file is regenerated from this same generator, so a wrong courseTop would be blessed
    // by `just shapes` and still pass. These are the spans decision 0023 fixes, written out rather
    // than recomputed, so the rule is checked against something other than its own output.
    [Fact]
    public void EachWeatherboardStepSamplesTheTextureOnceDownItsCourse()
    {
        (double V0, double V1)[] perCourse = [(3, 4), (2, 3), (1, 2), (0, 1)];
        var expected = Enumerable.Range(0, 4).SelectMany(_ => perCourse).ToArray();

        var actual = WallShapeGen.Generate("wall")["elements"]!
            .Where(e => (string)e["name"]! == "front-weatherboard")
            .Select(e => e["faces"]!["west"]!["uv"]!)
            .Select(uv => ((double)uv[1]!, (double)uv[3]!))
            .ToArray();

        Assert.Equal(expected, actual);
    }
}
