using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using Vintagestory.API.Datastructures;
using Xunit;

namespace VSSiding.Tests;

// A finish's named element group has to exist in the shape it draws from, and has to be out of every
// selectiveElements list of that shape, or it renders on the block's default mesh with no error.
public class FinishElementGroupsTests
{
    [Fact]
    public void EveryFinishElementGroupExistsAndIsLeftOutOfTheDefaultShape()
    {
        var repoRoot = MaterialTextureOpacityTests.GetAssemblyMetadata("RepoRoot");
        var assets = Path.Combine(repoRoot, "VSSiding", "assets", "vssiding");
        var wallJson = JObject.Parse(File.ReadAllText(Path.Combine(assets, "blocktypes", "wall.json")));
        var attributes = MaterialTextureOpacityTests.BlockAttributes("wall.json");

        var entries = ((JObject)attributes["Finishes"]!).Properties().Concat(((JObject)attributes["FinishFamilies"]!).Properties());
        // A Styles entry names {face}-{style} outright (decision 0027), so those groups have to
        // exist and be left out exactly like the Elements defaults do.
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
                var selected = variant.Value["selectiveElements"]!.Select(t => (string)t!).ToHashSet();
                offenders.AddRange(groups.Where(selected.Contains).Select(g => $"{variant.Name} draws '{g}'"));
            }
        }

        Assert.Equal([], offenders);
    }

    // A glazed wall or corner draws a finish group's rim parts in place of the group, for every finish, style and join.
    // Each has to exist in the generated shape, and none may be named among what the block's default shape draws.
    [Theory]
    [InlineData("wall")]
    [InlineData("cornerout")]
    [InlineData("diagonal")]
    public void EveryRimElementAGlazedCellCanAskForExistsAndIsLeftOutOfTheDefaultShape(string layout)
    {
        var assets = Path.Combine(MaterialTextureOpacityTests.GetAssemblyMetadata("RepoRoot"), "VSSiding", "assets", "vssiding");
        var wallJson = JObject.Parse(File.ReadAllText(Path.Combine(assets, "blocktypes", "wall.json")));
        var rims = RimsEveryGlazedCellAsksFor((finishes, key, style, joins) => SidingWallEntity.SelectiveElements(
            layout, "oak", "glass", key, layout == "cornerout" ? key : null, key, finishes, joins, glazed: true,
            styles: (style, layout == "cornerout" ? style : null, style)));
        var names = WallShapeGen.Generate(layout)["elements"]!.Select(e => (string)e["name"]!).ToHashSet();
        var offenders = rims.Where(r => !names.Contains(r)).Select(r => $"the {layout} shape has no element '{r}'").ToList();
        foreach (var variant in ((JObject)wallJson["shapebytype"]!).Properties().Where(p => (string)p.Value["base"]! == $"block/wall/{layout}"))
        {
            var selected = variant.Value["selectiveElements"]!.Select(t => (string)t!).ToHashSet();
            offenders.AddRange(rims.Where(selected.Contains).Select(r => $"{variant.Name} draws '{r}'"));
        }

        Assert.Equal([], offenders);
    }

    // A glazed floor draws a finish group's rim parts in place of the group, for every finish, style and join.
    // Each has to exist in the generated floor shape, and none may be named among what the block's default shape draws.
    [Fact]
    public void EveryRimElementAGlazedFloorCanAskForExistsAndIsLeftOutOfTheDefaultShape()
    {
        var assets = Path.Combine(MaterialTextureOpacityTests.GetAssemblyMetadata("RepoRoot"), "VSSiding", "assets", "vssiding");
        var floorJson = JObject.Parse(File.ReadAllText(Path.Combine(assets, "blocktypes", "floor.json")));
        var rims = RimsEveryGlazedCellAsksFor((finishes, key, style, joins) => SidingFloorEntity.SelectiveElements(
            "oak", "glass", key, key, finishes, joins, (style, style), glazed: true));

        var names = WallShapeGen.Generate("floor")["elements"]!.Select(e => (string)e["name"]!).ToHashSet();
        var selected = floorJson["shape"]!["selectiveElements"]!.Select(t => (string)t!).ToHashSet();
        var offenders = rims.Where(r => !names.Contains(r)).Select(r => $"the floor shape has no element '{r}'")
            .Concat(rims.Where(selected.Contains).Select(r => $"floor draws '{r}'")).ToList();

        Assert.Equal([], offenders);
    }

    // A glazed deck asks for the floor's rim parts under its side's prefix, for its deck and its ledge. Each has
    // to exist in the generated wall shape, unless the box that copy is cut to misses every piece of that name
    // on the floor: the clip then drops the whole name, and nothing of it is left to draw. None may be named
    // among what the block's default shape draws.
    [Theory]
    [InlineData("wall")]
    [InlineData("cornerout")]
    [InlineData("diagonal")]
    public void EveryRimElementAGlazedDeckCanAskForExistsOrIsCutAwayEntirelyAndIsLeftOutOfTheDefaultShape(string layout)
    {
        var assets = Path.Combine(MaterialTextureOpacityTests.GetAssemblyMetadata("RepoRoot"), "VSSiding", "assets", "vssiding");
        var wallJson = JObject.Parse(File.ReadAllText(Path.Combine(assets, "blocktypes", "wall.json")));
        var shape = WallShapeGen.Generate(layout)["elements"]!.ToArray();
        var names = shape.Select(e => (string)e["name"]!).ToHashSet();
        var floorPieces = WallShapeGen.Generate("floor")["elements"]!.ToLookup(e => (string)e["name"]!);

        // The boxes each copy is cut to, in the order of the sides' turns: the deck box, and for a ledge the
        // room side's slot over the deck's height, the whole of it or the part under a top piece of its rim.
        (double[] Lo, double[] Hi)[] Boxes(string copy, string side) => copy == "deck"
            ? [.. WallShapeGenTests.DeckBoxesVoxels(layout, side).Select(b => (b[..3], b[3..]))]
            : [.. shape.Where(e => (string)e["name"]! == LedgeSlots[copy]).Select(e =>
            {
                var r = WallShapeGen.DeckRegion(((double)e["from"]![0]!, 12, (double)e["from"]![2]!), ((double)e["to"]![0]!, 16, (double)e["to"]![2]!),
                    (int)SidingWallEntity.RotationYDeg(side));
                return (new[] { r.X, r.Y, r.Z }, new[] { r.X2, r.Y2, r.Z2 });
            })];

        bool Reaches(JToken piece, (double[] Lo, double[] Hi)[] boxes) => boxes.Any(box => Enumerable.Range(0, 3).All(a =>
            Math.Max((double)piece["from"]![a]!, box.Lo[a]) < Math.Min((double)piece["to"]![a]!, box.Hi[a])));

        var asked = (
            from side in new[] { "west", "south", "east", "north" }
            from name in RimsEveryGlazedCellAsksFor((finishes, key, style, joins) => SidingWallEntity.DeckElements(
                side, "oak", "glass", key, key, finishes, joins, (style, style), glazed: true, ledges: layout == "diagonal" ? [] : Ledges))
            select (Side: side, Name: name)).ToList();

        var offenders = new List<string>();
        foreach (var (side, name) in asked)
        {
            string[] parts = name.Split('-', 3);
            var floorName = parts[2];
            if (!names.Contains(name) && floorPieces[floorName].Any(piece => Reaches(piece, Boxes(parts[0], side))))
                offenders.Add($"the {layout} shape has no element '{name}'");
        }

        foreach (var variant in ((JObject)wallJson["shapebytype"]!).Properties().Where(p => (string)p.Value["base"]! == $"block/wall/{layout}"))
        {
            var selected = variant.Value["selectiveElements"]!.Select(t => (string)t!).ToHashSet();
            offenders.AddRange(asked.Select(a => a.Name).Distinct().Where(selected.Contains).Select(r => $"{variant.Name} draws '{r}'"));
        }

        Assert.Equal([], offenders.Distinct());
    }

    // A framing's Elements is a prefix that FrameElements turns into one name per variant. Read from
    // the real entries, so a prefix the shapes lack, or one variant of it, is caught here and not in play.
    [Fact]
    public void EveryFramingElementGroupExistsAndIsLeftOutOfTheDefaultShape()
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
                var selected = variant.Value["selectiveElements"]!.Select(t => (string)t!).ToHashSet();
                offenders.AddRange(groups.Where(selected.Contains).Select(g => $"{variant.Name} draws '{g}'"));
            }
        }

        var floorShape = JObject.Parse(File.ReadAllText(Path.Combine(assets, "shapes", "block", "floor", "floor.json")));
        var floorNames = floorShape["elements"]!.Select(e => (string)e["name"]!).ToHashSet();
        var floorJson = JObject.Parse(File.ReadAllText(Path.Combine(assets, "blocktypes", "floor.json")));
        var floorSelected = floorJson["shape"]!["selectiveElements"]!.Select(t => (string)t!).ToHashSet();
        var floorGroups = frames.SelectMany(frame => SidingFloorEntity.SelectiveElements(
            "sticks", null, null, null, new JsonObject(new JObject()), (false, false, false, false), frame: frame)).ToList();
        offenders.AddRange(floorGroups.Where(g => !floorNames.Contains(g)).Select(g => $"floor shape has no element '{g}'"));
        offenders.AddRange(floorGroups.Where(g => floorSelected.Contains(g)).Select(g => $"floor draws '{g}'"));

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
                        side, "sticks", null, null, null, new JsonObject(new JObject()), (false, false, false, false), default, false, frame, ledges: Ledges)
                    .Where(names.Contains).Select(name => name.Replace(frame, "framing")).ToArray();
                offenders.AddRange(frames.Where(frame => !Kept(frame).SequenceEqual(Kept("framing")))
                    .Select(frame => $"block/wall/{layout} keeps [{string.Join(", ", Kept(frame))}] of the {side} deck's {frame} groups, not [{string.Join(", ", Kept("framing"))}]"));
            }
        }

        Assert.Equal([], offenders);
    }

    // Each ledge copy of a deck, and the wall piece whose footprint it is cut to.
    private static readonly Dictionary<string, string> LedgeSlots = new()
    {
        ["ledge"] = "back",
        ["ledgetop"] = "back-rim-top",
        ["ledgetopleft"] = "back-rim-top-left",
        ["ledgetopright"] = "back-rim-top-right",
    };

    private static readonly string[] Ledges = [.. LedgeSlots.Keys];

    // Every prefix a real framing entry can draw with, both variants of it.
    private static List<string> RoughFrames(JObject attributes)
        => new[] { "Framings", "FramingFamilies" }.Select(name => attributes[name]).OfType<JObject>()
            .SelectMany(entries => entries.Properties().SelectMany(entry => new[] { 0, 1 }
                .Select(alternate => SidingWallEntity.FrameElements(new JsonObject(entries), entry.Name, alternate))))
            .Where(frame => frame != "framing").Distinct().ToList();

    [Fact]
    public void EveryFloorElementGroupExistsAndIsLeftOutOfTheFloorBlock()
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
        var selected = floorJson["shape"]!["selectiveElements"]!.Select(t => (string)t!).ToHashSet();

        var offenders = groups.Where(g => !names.Contains(g)).Select(g => $"floor shape has no element '{g}'")
            .Concat(groups.Where(selected.Contains).Select(g => $"floor draws '{g}'"));

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

    // A glazed diagonal whose glass runs on draws rail and pane pieces through the post and no post-front rim; each must exist in the shape.
    [Fact]
    public void EveryElementAGlazedDiagonalWhoseGlassRunsOnAsksForExists()
    {
        var finishes = new JsonObject(JObject.Parse("""{ "daub": {} }"""));
        bool[] either = [false, true];
        var names = WallShapeGen.Generate("diagonal")["elements"]!.Select(e => (string)e["name"]!).ToHashSet();
        var asked = (
            from above in either from below in either from left in either from right in either
            from glassLeft in either.Where(glass => !glass || left) from glassRight in either.Where(glass => !glass || right)
            from name in SidingWallEntity.SelectiveElements(
                "diagonal", "oak", "glass", "daub", null, "daub", finishes, (above, below, left, right), glazed: true, glassRunsOn: (glassLeft, glassRight))
            select name).Distinct();

        Assert.Equal([], asked.Where(name => !names.Contains(name)).ToArray());
    }

    private static readonly string[] PlankFinishes = ["planks", "planks-aged", "planks-veryaged", "planks-{wood}", "stained-{stain}-{wood}", "dyed-{color}-{wood}"];

    // The rim names glazed cells ask for, over every finish, each of its styles and none, and every set of joins.
    private static List<string> RimsEveryGlazedCellAsksFor(
        Func<JsonObject, string, string?, (bool above, bool below, bool left, bool right), IEnumerable<string>> selectiveElements)
    {
        var entries = FinishEntries();
        var finishes = new JsonObject(new JObject(entries.Select(e => new JProperty(e.Name, e.Value))));
        bool[] either = [false, true];
        var rims = (
            from entry in entries
            from style in new string?[] { null }.Concat(entry.Value["Styles"]?.Select(t => (string)t!) ?? [])
            from above in either
            from below in either
            from left in either
            from right in either
            from name in selectiveElements(finishes, entry.Name, style, (above, below, left, right))
            where name.Contains("-rim")
            select name).Distinct().ToList();
        Assert.NotEmpty(rims);
        return rims;
    }

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
    // them may be named among what the block's default JSON shape draws.
    [Fact]
    public void EveryVariantLeavesOutTheGlazingOnlyElements()
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
            var selected = variant.Value["selectiveElements"]!.Select(t => (string)t!).ToHashSet();
            offenders.AddRange(glazingOnly.Where(selected.Contains).Select(n => $"{variant.Name} draws '{n}'"));
        }

        var floorJson = JObject.Parse(File.ReadAllText(Path.Combine(assets, "blocktypes", "floor.json")));
        var floorSelected = floorJson["shape"]!["selectiveElements"]!.Select(t => (string)t!).ToHashSet();
        var floorShape = JObject.Parse(File.ReadAllText(Path.Combine(assets, "shapes", "block", "floor", "floor.json")));
        offenders.AddRange(floorShape["elements"]!.Select(e => (string)e["name"]!)
            .Where(n => n == "infill-pane" || n.StartsWith("glazing")).Distinct()
            .Where(floorSelected.Contains).Select(n => $"floor draws '{n}'"));

        Assert.Equal([], offenders);
    }
}
