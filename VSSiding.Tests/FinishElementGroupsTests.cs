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
        var attributes = MaterialTextureOpacityTests.BlockAttributes("wall.json");

        var entries = ((JObject)attributes["Finishes"]!).Properties().Concat(((JObject)attributes["FinishFamilies"]!).Properties());
        // A Styles entry names {face}-{style} outright (decision 0027), so those groups have to
        // exist and be ignored exactly like the Elements defaults do.
        var styles = entries.SelectMany(e => e.Value["Styles"]?.Select(t => (string)t!) ?? []).Distinct().ToList();
        var fronts = entries.Select(e => (string?)e.Value["Elements"]?["front"]).OfType<string>()
            .Concat(styles.Select(style => "front-" + style)).Distinct().ToList();
        var backs = entries.Select(e => (string?)e.Value["Elements"]?["back"]).OfType<string>()
            .Concat(styles.Select(style => "back-" + style)).Distinct().ToList();
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

    [Fact]
    public void EveryFloorElementGroupExistsAndIsIgnoredByTheFloorBlock()
    {
        var repoRoot = MaterialTextureOpacityTests.GetAssemblyMetadata("RepoRoot");
        var assets = Path.Combine(repoRoot, "VSSiding", "assets", "vssiding");
        var floorJson = JObject.Parse(File.ReadAllText(Path.Combine(assets, "blocktypes", "floor.json")));
        var shapeJson = JObject.Parse(File.ReadAllText(Path.Combine(assets, "shapes", "block", "floor", "floor.json")));
        var attributes = MaterialTextureOpacityTests.BlockAttributes("wall.json");

        var entries = ((JObject)attributes["Finishes"]!).Properties().Concat(((JObject)attributes["FinishFamilies"]!).Properties());
        var groups = entries.SelectMany(e => new[] { "front", "back" }.Select(face => (string?)e.Value["FloorElements"]?[face]))
            .OfType<string>().Where(g => g is not ("front" or "back"))
            .Concat(entries.SelectMany(e => e.Value["FloorStyles"]?.Select(t => (string)t!) ?? [])
                .SelectMany(style => new[] { "front-" + style, "back-" + style }))
            .Distinct().ToList();
        var names = shapeJson["elements"]!.Select(e => (string)e["name"]!).ToHashSet();
        var ignored = floorJson["shape"]!["ignoreElements"]?.Select(t => (string)t!).ToHashSet() ?? [];

        var offenders = groups.Where(g => !names.Contains(g)).Select(g => $"floor shape has no element '{g}'")
            .Concat(groups.Where(g => !ignored.Contains(g)).Select(g => $"floor doesn't ignore '{g}'"));

        Assert.Equal([], offenders.ToArray());
    }

    [Fact]
    public void EveryElementSelectiveElementsCanAskForExistsInTheFloorShape()
    {
        var repoRoot = MaterialTextureOpacityTests.GetAssemblyMetadata("RepoRoot");
        var shapeJson = JObject.Parse(File.ReadAllText(
            Path.Combine(repoRoot, "VSSiding", "assets", "vssiding", "shapes", "block", "floor", "floor.json")));
        var names = shapeJson["elements"]!.Select(e => (string)e["name"]!).ToHashSet();
        var attributes = MaterialTextureOpacityTests.BlockAttributes("wall.json");
        var finishes = new JsonObject((JObject)attributes["Finishes"]!);

        var asked =
            from above in new[] { false, true }
            from below in new[] { false, true }
            from left in new[] { false, true }
            from right in new[] { false, true }
            from finish in finishes.Token.Children<JProperty>().Select(p => p.Name)
            from name in SidingFloorEntity.SelectiveElements("oak", "wattle", finish, finish, finishes, (above, below, left, right))
            select name;

        Assert.Equal([], asked.Distinct().Where(name => !names.Contains(name)).ToArray());
    }

    // Every element name SelectiveElements can produce has to exist in the shape it draws from,
    // for every layout and every join state. A name the shape lacks draws nothing and says
    // nothing - which is how glazing silently kept its seam-prone filler stack.
    [Theory]
    [InlineData("wall")]
    [InlineData("cornerout")]
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
                (above, below, left, right), glazed: transparent)
            select name;

        Assert.Equal([], asked.Distinct().Where(name => !names.Contains(name)).ToArray());
    }

    // A plank finish missing one of the boards row's styles falls back to its default look when
    // that style is picked, with no error.
    [Fact]
    public void EveryPlankFinishOffersTheWholeBoardsRow()
    {
        var repoRoot = MaterialTextureOpacityTests.GetAssemblyMetadata("RepoRoot");
        var attributes = MaterialTextureOpacityTests.BlockAttributes("wall.json");
        var entries = ((JObject)attributes["Finishes"]!).Properties().Concat(((JObject)attributes["FinishFamilies"]!).Properties());
        var boards = SidingModePicker.Rows.Single(r => r.Key == "vssidingBoards").Options;

        Assert.Equal(
            new[] { "planks", "planks-veryaged", "planks-{wood}" }.Select(name => $"{name}: {string.Join(", ", boards)}"),
            entries.Select(e => (e.Name, Styles: (e.Value["Styles"] ?? new JArray()).Concat(e.Value["FloorStyles"] ?? new JArray()).Select(t => (string)t!).ToList()))
                .Where(e => e.Styles.Any(boards.Contains))
                .Select(e => $"{e.Name}: {string.Join(", ", e.Styles.Distinct())}"));
    }

    // Glazing's own elements - the pane and its bezel - exist only for a glazed cell, so none of
    // them may ride along on the block's default JSON shape.
    [Fact]
    public void EveryVariantIgnoresTheGlazingOnlyElements()
    {
        var repoRoot = MaterialTextureOpacityTests.GetAssemblyMetadata("RepoRoot");
        var assets = Path.Combine(repoRoot, "VSSiding", "assets", "vssiding");
        var wallJson = JObject.Parse(File.ReadAllText(Path.Combine(assets, "blocktypes", "wall.json")));

        var offenders = new List<string>();
        foreach (var variant in ((JObject)wallJson["shapebytype"]!).Properties())
        {
            var shapeJson = JObject.Parse(File.ReadAllText(
                Path.Combine(assets, "shapes", (string)variant.Value["base"]! + ".json")));
            var glazingOnly = shapeJson["elements"]!.Select(e => (string)e["name"]!)
                .Where(n => n == "infill-pane" || n.StartsWith("glazing")).Distinct();
            var ignored = variant.Value["ignoreElements"]?.Select(t => (string)t!).ToHashSet() ?? [];
            offenders.AddRange(glazingOnly.Where(n => !ignored.Contains(n)).Select(n => $"{variant.Name} draws '{n}'"));
        }

        Assert.Equal([], offenders);
    }
}
