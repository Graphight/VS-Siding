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
    [InlineData("diagonal")]
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

    private static readonly string[] PlankFinishes = ["planks", "planks-aged", "planks-veryaged", "planks-{wood}"];

    private static List<JProperty> FinishEntries()
    {
        var attributes = MaterialTextureOpacityTests.BlockAttributes("wall.json");
        return ((JObject)attributes["Finishes"]!).Properties().Concat(((JObject)attributes["FinishFamilies"]!).Properties()).ToList();
    }

    // A finish missing one of its row's styles falls back to its default look when that style is
    // picked, with no error.
    [Fact]
    public void EveryStyledFinishOffersItsWholePickerRow()
    {
        var entries = FinishEntries();
        string[] Row(string key) => SidingModePicker.Rows.Single(r => r.Key == key).Options;
        var finishesByRow = new (string Row, string[] Finishes)[] { ("vssidingBoards", PlankFinishes), ("vssidingLogs", ["shakes-{wood}"]) };

        Assert.Equal(
            finishesByRow.SelectMany(row => row.Finishes.Select(name => $"{name}: {string.Join(", ", Row(row.Row))}")),
            finishesByRow.SelectMany(row => entries
                .Where(e => e.Value["Styles"]?.Any(t => Row(row.Row).Contains((string)t!)) ?? false)
                .Select(e => $"{e.Name}: {string.Join(", ", e.Value["Styles"]!.Select(t => (string)t!))}")));
    }

    // A style a wall takes and a floor face refuses falls back to that face's default with no error,
    // which in play reads as the picker being ignored. Weatherboard is the one style refused: laid
    // flat it is a run of ridges (decision 0051).
    [Fact]
    public void AFloorFaceRefusesNoStyleButWeatherboard()
    {
        var refused =
            from entry in FinishEntries()
            from style in entry.Value["Styles"]?.Select(t => (string)t!) ?? []
            from face in new[] { "front", "back" }
            where !SidingFloorEntity.HasFloorStyle(new JsonObject(entry.Value), face, style)
            select $"{entry.Name} {face} {style}";

        Assert.Equal(PlankFinishes.SelectMany(name => new[] { $"{name} front weatherboard", $"{name} back weatherboard" }), refused);
    }

    // A style with no StyleTextures entry draws the finish's own Texture. The log finish's bark styles
    // share one flat shape, so a missing entry would draw one of them as shakes with nothing to say so.
    [Fact]
    public void EveryLogStyleDrawsItsOwnTexture()
    {
        var families = new JsonObject(MaterialTextureOpacityTests.BlockAttributes("wall.json")["FinishFamilies"]!);
        var none = new JsonObject(new JObject());
        var textures = SidingModePicker.Rows.Single(r => r.Key == "vssidingLogs").Options
            .Select(style => SidingWallTexSource.ResolveTexture(
                "front", null, null, "shakes-{wood}", null, null, none, none, families, frontStyle: style)!.Base.ToString())
            .ToList();

        Assert.Equal(textures.Distinct(), textures);
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
            // A diagonal names what it draws instead, its shape having two end pieces for every group.
            var selected = variant.Value["selectiveElements"]?.Select(t => (string)t!).ToHashSet();
            offenders.AddRange(glazingOnly.Where(n => !ignored.Contains(n) && selected?.Contains(n) != false).Select(n => $"{variant.Name} draws '{n}'"));
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
