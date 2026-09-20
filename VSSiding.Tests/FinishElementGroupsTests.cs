using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using Vintagestory.API.Datastructures;
using Xunit;

namespace VSSiding.Tests;

// A finish's named element group has to exist in the shape it draws from, and has to be in every
// ignoreElements list of that shape, or it renders on the block's default mesh with no error.
public class FinishElementGroupsTests
{
    [Fact]
    public void EveryFinishElementGroupExistsAndIsIgnoredByTheDefaultShape()
    {
        var repoRoot = MaterialTextureOpacityTests.GetAssemblyMetadata("RepoRoot");
        var assets = Path.Combine(repoRoot, "VSSiding", "assets", "vssiding");
        var wallJson = JObject.Parse(File.ReadAllText(Path.Combine(assets, "blocktypes", "wall.json")));
        var attributes = (JObject)wallJson["attributes"]!;

        var entries = ((JObject)attributes["Finishes"]!).Properties().Concat(((JObject)attributes["FinishFamilies"]!).Properties());
        var fronts = entries.Select(e => (string?)e.Value["Elements"]?["front"]).OfType<string>().Distinct().ToList();
        var backs = entries.Select(e => (string?)e.Value["Elements"]?["back"]).OfType<string>().Distinct().ToList();
        var groupsByShape = new Dictionary<string, List<string>>
        {
            ["block/wall/wall"] = fronts.Concat(backs).ToList(),
            ["block/wall/cornerout"] = fronts.Concat(fronts.Select(f => "second" + f)).Concat(backs).ToList(),
        };

        var offenders = new List<string>();
        foreach (var (shape, groups) in groupsByShape)
        {
            var shapeJson = JObject.Parse(File.ReadAllText(Path.Combine(assets, "shapes", shape + ".json")));
            var names = shapeJson["elements"]!.Select(e => (string)e["name"]!).ToHashSet();
            offenders.AddRange(groups.Where(g => !names.Contains(g)).Select(g => $"{shape} has no element '{g}'"));

            foreach (var variant in ((JObject)wallJson["shapebytype"]!).Properties().Where(p => (string)p.Value["base"]! == shape))
            {
                var ignored = variant.Value["ignoreElements"]?.Select(t => (string)t!).ToHashSet() ?? [];
                offenders.AddRange(groups.Where(g => !ignored.Contains(g)).Select(g => $"{variant.Name} doesn't ignore '{g}'"));
            }
        }

        Assert.Equal([], offenders);
    }

    // Every element name SelectiveElements can produce has to exist in the shape it draws from,
    // for every layout and every join state. A name the shape lacks draws nothing and says
    // nothing - which is how glazing silently kept its seam-prone filler stack.
    [Theory]
    [InlineData("wall")]
    [InlineData("cornerout")]
    [InlineData("window")]
    public void EveryElementSelectiveElementsCanAskForExistsInItsShape(string layout)
    {
        var repoRoot = MaterialTextureOpacityTests.GetAssemblyMetadata("RepoRoot");
        var shapeJson = JObject.Parse(File.ReadAllText(
            Path.Combine(repoRoot, "VSSiding", "assets", "vssiding", "shapes", "block", "wall", layout + ".json")));
        var names = shapeJson["elements"]!.Select(e => (string)e["name"]!).ToHashSet();

        var asked =
            from above in new[] { false, true }
            from below in new[] { false, true }
            from left in new[] { false, true }
            from right in new[] { false, true }
            from transparent in new[] { false, true }
            from name in SidingWallEntity.SelectiveElements(
                layout, "oak", "glass", null, null, null, new JsonObject(new JObject()),
                (above, below, left, right), transparent)
            select name;

        Assert.Equal([], asked.Distinct().Where(name => !names.Contains(name)).ToArray());
    }

    // The glazing pane must not ride along on the block's default JSON shape.
    [Fact]
    public void EveryWallAndCorneroutVariantIgnoresTheGlazingPane()
    {
        var repoRoot = MaterialTextureOpacityTests.GetAssemblyMetadata("RepoRoot");
        var wallJson = JObject.Parse(File.ReadAllText(
            Path.Combine(repoRoot, "VSSiding", "assets", "vssiding", "blocktypes", "wall.json")));

        var offenders = ((JObject)wallJson["shapebytype"]!).Properties()
            .Where(v => (string)v.Value["base"]! != "block/wall/window")
            .Where(v => v.Value["ignoreElements"]?.Select(t => (string)t!).Contains("infill-pane") != true)
            .Select(v => v.Name)
            .ToArray();

        Assert.Equal([], offenders);
    }
}
