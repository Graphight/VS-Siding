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

    // The window layout takes no finish, so it has no finish groups to ignore - what matters
    // instead is that every element SelectiveElements can name for a window actually exists.
    [Fact]
    public void WindowShapeHasEveryElementSelectiveElementsAsksFor()
    {
        var repoRoot = MaterialTextureOpacityTests.GetAssemblyMetadata("RepoRoot");
        var shapeJson = JObject.Parse(File.ReadAllText(
            Path.Combine(repoRoot, "VSSiding", "assets", "vssiding", "shapes", "block", "wall", "window.json")));
        var names = shapeJson["elements"]!.Select(e => (string)e["name"]!).ToHashSet();

        var asked = new[] { (false, false), (true, false), (false, true), (true, true) }
            .SelectMany(j => SidingWallEntity.SelectiveElements(
                "window", "oak", "glass", null, null, null, new JsonObject(new JObject()), j.Item1, j.Item2))
            .Distinct();

        Assert.Equal([], asked.Where(name => !names.Contains(name)).ToArray());
    }
}
