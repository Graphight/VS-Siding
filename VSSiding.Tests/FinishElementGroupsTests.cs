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

    // A framing's Elements is a prefix that FrameElements turns into one name per variant. Read from
    // the real entries, so a prefix the shapes lack, or one variant of it, is caught here and not in play.
    [Fact]
    public void EveryFramingElementGroupExistsAndIsIgnoredByTheDefaultShape()
    {
        var repoRoot = MaterialTextureOpacityTests.GetAssemblyMetadata("RepoRoot");
        var assets = Path.Combine(repoRoot, "VSSiding", "assets", "vssiding");
        var wallJson = JObject.Parse(File.ReadAllText(Path.Combine(assets, "blocktypes", "wall.json")));
        var attributes = MaterialTextureOpacityTests.BlockAttributes("wall.json");

        var frames = RoughFrames(attributes);
        Assert.NotEmpty(frames);

        var offenders = new List<string>();
        foreach (var layout in new[] { "wall", "cornerout" })
        {
            string shape = "block/wall/" + layout;
            var groups = frames.SelectMany(frame => SidingWallEntity.SelectiveElements(
                layout, "sticks", null, null, null, null, new JsonObject(new JObject()), (false, false, false, false), glazed: false, frame: frame)).ToList();
            var shapeJson = JObject.Parse(File.ReadAllText(Path.Combine(assets, "shapes", shape + ".json")));
            var names = shapeJson["elements"]!.Select(e => (string)e["name"]!).ToHashSet();
            offenders.AddRange(groups.Where(g => !names.Contains(g)).Select(g => $"{shape} has no element '{g}'"));

            foreach (var variant in ((JObject)wallJson["shapebytype"]!).Properties().Where(p => (string)p.Value["base"]! == shape))
            {
                var ignored = variant.Value["ignoreElements"]?.Select(t => (string)t!).ToHashSet() ?? [];
                offenders.AddRange(groups.Where(g => !ignored.Contains(g)).Select(g => $"{variant.Name} doesn't ignore '{g}'"));
            }
        }

        var floorShape = JObject.Parse(File.ReadAllText(Path.Combine(assets, "shapes", "block", "floor", "floor.json")));
        var floorNames = floorShape["elements"]!.Select(e => (string)e["name"]!).ToHashSet();
        var floorJson = JObject.Parse(File.ReadAllText(Path.Combine(assets, "blocktypes", "floor.json")));
        var floorIgnored = floorJson["shape"]!["ignoreElements"]?.Select(t => (string)t!).ToHashSet() ?? [];
        var floorGroups = frames.SelectMany(frame => SidingFloorEntity.SelectiveElements(
            "sticks", null, null, null, new JsonObject(new JObject()), (false, false, false, false), frame: frame)).ToList();
        offenders.AddRange(floorGroups.Where(g => !floorNames.Contains(g)).Select(g => $"floor shape has no element '{g}'"));
        offenders.AddRange(floorGroups.Where(g => !floorIgnored.Contains(g)).Select(g => $"floor doesn't ignore '{g}'"));

        Assert.Equal([], offenders);
    }

    // A deck, and its ledge, is the floor clipped to its side, and the clip drops some members whole, such
    // as the rim against the wall. A pole deck has to keep exactly the members the plain deck keeps, or it
    // draws a piece of a member with the rest of it clipped away.
    [Fact]
    public void APoleDeckKeepsTheMembersThePlainDeckKeeps()
    {
        var assets = Path.Combine(MaterialTextureOpacityTests.GetAssemblyMetadata("RepoRoot"), "VSSiding", "assets", "vssiding");
        var frames = RoughFrames(MaterialTextureOpacityTests.BlockAttributes("wall.json"));

        var offenders = new List<string>();
        foreach (var layout in new[] { "wall", "cornerout" })
        {
            var names = JObject.Parse(File.ReadAllText(Path.Combine(assets, "shapes", "block", "wall", layout + ".json")))["elements"]!
                .Select(e => (string)e["name"]!).ToHashSet();
            foreach (var side in new[] { "north", "east", "south", "west" })
            {
                string[] Kept(string frame) => SidingWallEntity.DeckElements(
                        side, "sticks", null, null, null, new JsonObject(new JObject()), (false, false, false, false), default, false, frame, ledge: true)
                    .Where(names.Contains).Select(name => name.Replace(frame, "framing")).ToArray();
                offenders.AddRange(frames.Where(frame => !Kept(frame).SequenceEqual(Kept("framing")))
                    .Select(frame => $"block/wall/{layout} keeps [{string.Join(", ", Kept(frame))}] of the {side} deck's {frame} groups, not [{string.Join(", ", Kept("framing"))}]"));
            }
        }

        Assert.Equal([], offenders);
    }

    // Every prefix a real framing entry can draw with, both variants of it.
    private static List<string> RoughFrames(JObject attributes)
        => new[] { "Framings", "FramingFamilies" }.Select(name => attributes[name]).OfType<JObject>()
            .SelectMany(entries => entries.Properties().SelectMany(entry => new[] { 0, 1 }
                .Select(alternate => SidingWallEntity.FrameElements(new JsonObject(entries), entry.Name, alternate))))
            .Where(frame => frame != "framing").Distinct().ToList();

    [Fact]
    public void EveryFloorElementGroupExistsAndIsIgnoredByTheFloorBlock()
    {
        var repoRoot = MaterialTextureOpacityTests.GetAssemblyMetadata("RepoRoot");
        var assets = Path.Combine(repoRoot, "VSSiding", "assets", "vssiding");
        var floorJson = JObject.Parse(File.ReadAllText(Path.Combine(assets, "blocktypes", "floor.json")));
        var shapeJson = JObject.Parse(File.ReadAllText(Path.Combine(assets, "shapes", "block", "floor", "floor.json")));
        var attributes = MaterialTextureOpacityTests.BlockAttributes("wall.json");

        var entries = ((JObject)attributes["Finishes"]!).Properties().Concat(((JObject)attributes["FinishFamilies"]!).Properties());
        var groups = entries.SelectMany(e => new[] { "front", "back" }
                .SelectMany(face => e.Value["FloorElements"]?[face]?.Values().Select(t => (string)t!) ?? []))
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
            from glazed in new[] { false, true }
            from name in SidingFloorEntity.SelectiveElements(
                "oak", glazed ? "glass" : "wattle", finish, finish, finishes, (above, below, left, right), glazed: glazed)
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
            new[] { "planks", "planks-aged", "planks-veryaged", "planks-{wood}" }.Select(name => $"{name}: {string.Join(", ", boards)}"),
            entries.Where(e => e.Value["Styles"]?.Any(t => boards.Contains((string)t!)) ?? false)
                .Select(e => $"{e.Name}: {string.Join(", ", e.Value["Styles"]!.Select(t => (string)t!))}"));
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

        var floorJson = JObject.Parse(File.ReadAllText(Path.Combine(assets, "blocktypes", "floor.json")));
        var floorIgnored = floorJson["shape"]!["ignoreElements"]?.Select(t => (string)t!).ToHashSet() ?? [];
        var floorShape = JObject.Parse(File.ReadAllText(Path.Combine(assets, "shapes", "block", "floor", "floor.json")));
        offenders.AddRange(floorShape["elements"]!.Select(e => (string)e["name"]!)
            .Where(n => n == "infill-pane" || n.StartsWith("glazing")).Distinct()
            .Where(n => !floorIgnored.Contains(n)).Select(n => $"floor draws '{n}'"));

        Assert.Equal([], offenders);
    }
}
