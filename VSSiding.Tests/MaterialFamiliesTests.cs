using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using Vintagestory.API.Common;
using Xunit;

namespace VSSiding.Tests;

public class MaterialFamiliesTests
{
    private static readonly JObject WoodFamily = JObject.Parse("""
    {
        "{wood}": {
            "Match": { "type": "item", "code": "game:plank-*", "variant": "wood" },
            "Texture": "game:block/wood/planks/{wood}1",
            "Consumes": { "type": "item", "code": "game:plank-{wood}", "quantity": 2 },
            "Drops": [ { "type": "item", "code": "game:plank-{wood}", "quantity": { "avg": 2, "var": 0 } } ]
        }
    }
    """);

    private static (string, AssetLocation, IDictionary<string, string>) Candidate(string type, string code, string variant, string value)
        => (type, new AssetLocation(code), new Dictionary<string, string> { [variant] = value });

    private static JObject Entry(string wood) => JObject.Parse($$"""
    {
        "Texture": "game:block/wood/planks/{{wood}}1",
        "Consumes": { "type": "item", "code": "game:plank-{{wood}}", "quantity": 2 },
        "Drops": [ { "type": "item", "code": "game:plank-{{wood}}", "quantity": { "avg": 2, "var": 0 } } ]
    }
    """);

    private static void AssertJson(JObject expected, JObject actual) => Assert.Equal(expected.ToString(), actual.ToString());

    [Fact]
    public void ExpandsOneEntryPerMatchingItem()
    {
        var actual = MaterialFamilies.Expand(WoodFamily, new JObject(), new[]
        {
            Candidate("item", "game:plank-birch", "wood", "birch"),
            Candidate("item", "game:plank-pine", "wood", "pine"),
            Candidate("item", "game:stick", "wood", "oak"),
        });

        AssertJson(new JObject { ["birch"] = Entry("birch"), ["pine"] = Entry("pine") }, actual);
    }

    [Fact]
    public void AnyDomainMatchFillsTheMatchedDomain()
    {
        var families = JObject.Parse("""
        {
            "planks-{wood}": {
                "Match": { "type": "item", "code": "*:plank-*", "variant": "wood" },
                "Texture": "{domain}:block/wood/planks/{wood}1",
                "Consumes": { "type": "item", "code": "{domain}:plank-{wood}", "quantity": 2 }
            }
        }
        """);

        var actual = MaterialFamilies.Expand(families, new JObject(), new[]
        {
            Candidate("item", "game:plank-birch", "wood", "birch"),
            Candidate("item", "wildcrafttree:plank-ash", "wood", "ash"),
        });

        AssertJson(JObject.Parse("""
        {
            "planks-birch": {
                "Texture": "game:block/wood/planks/birch1",
                "Consumes": { "type": "item", "code": "game:plank-birch", "quantity": 2 }
            },
            "planks-ash": {
                "Texture": "wildcrafttree:block/wood/planks/ash1",
                "Consumes": { "type": "item", "code": "wildcrafttree:plank-ash", "quantity": 2 }
            }
        }
        """), actual);
    }

    [Fact]
    public void ExplicitKeyWins()
    {
        var explicitEntries = new JObject { ["birch"] = new JObject { ["Texture"] = "custom" } };

        var actual = MaterialFamilies.Expand(WoodFamily, explicitEntries, new[] { Candidate("item", "game:plank-birch", "wood", "birch") });

        AssertJson(explicitEntries, actual);
    }

    [Fact]
    public void ExplicitConsumesWinsUnderAnotherKey()
    {
        var explicitEntries = new JObject { ["planks"] = Entry("oak") };

        var actual = MaterialFamilies.Expand(WoodFamily, explicitEntries, new[]
        {
            Candidate("item", "game:plank-oak", "wood", "oak"),
            Candidate("item", "game:plank-birch", "wood", "birch"),
        });

        AssertJson(new JObject { ["planks"] = Entry("oak"), ["birch"] = Entry("birch") }, actual);
    }

    [Fact]
    public void NoCandidatesLeavesExplicitEntries()
    {
        var explicitEntries = new JObject { ["oak"] = Entry("oak") };

        var actual = MaterialFamilies.Expand(WoodFamily, explicitEntries, []);

        AssertJson(explicitEntries, actual);
    }

    [Fact]
    public void MalformedFamilyIsSkippedWithAWarning()
    {
        var families = (JObject)WoodFamily.DeepClone();
        families["broken-{wood}"] = JObject.Parse("""{ "Match": { "code": "game:plank-*" }, "Texture": "x" }""");
        families["nomatch-{wood}"] = JObject.Parse("""{ "Texture": "x" }""");
        var warnings = new List<string>();

        var actual = MaterialFamilies.Expand(families, new JObject(), new[] { Candidate("item", "game:plank-birch", "wood", "birch") }, warnings.Add);

        AssertJson(new JObject { ["birch"] = Entry("birch") }, actual);
        Assert.Equal(new[]
        {
            "material family 'broken-{wood}' needs Match.code and Match.variant; skipped",
            "material family 'nomatch-{wood}' needs Match.code and Match.variant; skipped",
        }, warnings);
    }

    [Fact]
    public void BlockMatchesOnlyBlocks()
    {
        var families = JObject.Parse("""
        { "cobble-{rock}": { "Match": { "type": "block", "code": "game:cobblestone-*", "variant": "rock" }, "Texture": "game:block/stone/cobblestone/{rock}1" } }
        """);

        var actual = MaterialFamilies.Expand(families, new JObject(), new[]
        {
            Candidate("block", "game:cobblestone-granite", "rock", "granite"),
            Candidate("item", "game:cobblestone-basalt", "rock", "basalt"),
        });

        AssertJson(JObject.Parse("""{ "cobble-granite": { "Texture": "game:block/stone/cobblestone/granite1" } }"""), actual);
    }

    [Fact]
    public void ListedVariantsFillEveryPlaceholderAndALackingCandidateIsLeftOut()
    {
        var families = JObject.Parse("""
        {
            "stained-{stain}-{wood}": {
                "Match": { "type": "block", "code": "woodstain:stainedplanks-*-*-ns", "variant": ["stain", "wood"] },
                "Texture": "{domain}:block/{stain}/{wood}1",
                "Consumes": { "type": "block", "code": "{domain}:stainedplanks-{stain}-{wood}-*", "quantity": 1 }
            }
        }
        """);

        var actual = MaterialFamilies.Expand(families, new JObject(), new (string, AssetLocation, IDictionary<string, string>)[]
        {
            ("block", new AssetLocation("woodstain:stainedplanks-red-oak-ns"), new Dictionary<string, string> { ["stain"] = "red", ["wood"] = "oak" }),
            ("block", new AssetLocation("woodstain:stainedplanks-blue-pine-ns"), new Dictionary<string, string> { ["stain"] = "blue" }),
        });

        AssertJson(JObject.Parse("""
        {
            "stained-red-oak": {
                "Texture": "woodstain:block/red/oak1",
                "Consumes": { "type": "block", "code": "woodstain:stainedplanks-red-oak-*", "quantity": 1 }
            }
        }
        """), actual);
    }

    [Fact]
    public void AnEmptyOrNonStringVariantListIsSkippedWithAWarning()
    {
        var families = JObject.Parse("""
        {
            "empty-{wood}": { "Match": { "code": "game:plank-*", "variant": [] }, "Texture": "x" },
            "number-{wood}": { "Match": { "code": "game:plank-*", "variant": ["wood", 3] }, "Texture": "x" }
        }
        """);
        var warnings = new List<string>();

        var actual = MaterialFamilies.Expand(families, new JObject(), new[] { Candidate("item", "game:plank-birch", "wood", "birch") }, warnings.Add);

        AssertJson(new JObject(), actual);
        Assert.Equal(new[]
        {
            "material family 'empty-{wood}' needs Match.code and Match.variant; skipped",
            "material family 'number-{wood}' needs Match.code and Match.variant; skipped",
        }, warnings);
    }

    [Fact]
    public void RegexMatchExcludesVariants()
    {
        var families = JObject.Parse("""
        { "drystone-{rock}": { "Match": { "type": "item", "code": "game:@stone-(?!travertine$|meteorite-iron$).*", "variant": "rock" }, "Texture": "game:block/stone/drystone/{rock}1" } }
        """);

        var actual = MaterialFamilies.Expand(families, new JObject(), new[]
        {
            Candidate("item", "game:stone-granite", "rock", "granite"),
            Candidate("item", "game:stone-travertine", "rock", "travertine"),
            Candidate("item", "game:stone-meteorite-iron", "rock", "meteorite-iron"),
        });

        AssertJson(JObject.Parse("""{ "drystone-granite": { "Texture": "game:block/stone/drystone/granite1" } }"""), actual);
    }

    [Fact]
    public void CompositeTextureExpandsInsideOverlays()
    {
        var families = JObject.Parse("""
        {
            "brick-{type}": {
                "Match": { "type": "item", "code": "game:burnedbrick-*", "variant": "type" },
                "Texture": { "base": "game:block/clay/brick/four/running/cream1", "overlays": [ "game:block/clay/brick/four/running/{type}1" ] }
            }
        }
        """);

        var actual = MaterialFamilies.Expand(families, new JObject(), new[]
        {
            Candidate("item", "game:burnedbrick-fire", "type", "fire"),
        });

        AssertJson(JObject.Parse("""
        { "brick-fire": { "Texture": { "base": "game:block/clay/brick/four/running/cream1", "overlays": [ "game:block/clay/brick/four/running/fire1" ] } } }
        """), actual);
    }

    [Fact]
    public void MergeSharedKeepsSharedEntriesAndLetsTheBlockReplaceOne()
    {
        var shared = JObject.Parse("""
        {
            "Framings": { "oak": { "Texture": "shared-oak" }, "pine": { "Texture": "shared-pine" } },
            "Infills": { "wattle": { "Texture": "shared-wattle" } }
        }
        """);
        var attributes = JObject.Parse("""
        {
            "canStep": false,
            "Framings": { "oak": { "DisplayName": "own-oak" }, "birch": { "Texture": "own-birch" } }
        }
        """);

        var expected = JObject.Parse("""
        {
            "canStep": false,
            "Framings": { "oak": { "DisplayName": "own-oak" }, "pine": { "Texture": "shared-pine" }, "birch": { "Texture": "own-birch" } },
            "Infills": { "wattle": { "Texture": "shared-wattle" } }
        }
        """);
        Assert.True(JToken.DeepEquals(expected, MaterialFamilies.MergeShared(shared, attributes)),
            MaterialFamilies.MergeShared(shared, attributes).ToString());
    }

    // What every plank finish shares, whichever mod the plank comes from.
    private const string PlankLook = """
        "Elements": { "front": "front-weatherboard", "back": "back-boards" },
        "FloorElements": {
            "front": { "hboards": "front-hboards", "boards": "front-boards" },
            "back": { "hboards": "back-hboards", "boards": "back-boards" }
        },
        "Styles": [ "weatherboard", "boards", "hboards" ],
        "BlockMaterial": "Wood"
        """;

    private static (string, AssetLocation, IDictionary<string, string>) StainCandidate(string stain, string wood, string orientation, string block = "stainedplanks")
        => ("block", new AssetLocation($"woodstain:{block}-{stain}-{wood}-{orientation}"),
            new Dictionary<string, string> { ["stain"] = stain, ["wood"] = wood, ["orientation"] = orientation });

    [Fact]
    public void ShippedWoodStainTemplateExpandsOneEntryPerStainAndWood()
    {
        var families = (JObject)MaterialTextureOpacityTests.BlockAttributes("wall.json")["FinishFamilies"]!;

        var actual = MaterialFamilies.Expand(families, new JObject(), new[]
        {
            StainCandidate("red", "oak", "ns"),
            StainCandidate("blue", "agedebony", "ns"),
            StainCandidate("green", "cedar", "ns"),
            StainCandidate("pink", "birch", "ud"),
            StainCandidate("black", "pine", "ns", "stainedplankslab"),
        });

        Assert.Equal(new[] { "stained-red-oak", "stained-blue-agedebony", "stained-green-cedar" },
            actual.Properties().Select(p => p.Name));
        AssertJson(JObject.Parse($$"""
        {
            "TextureBlock": "woodstain:stainedplanks-red-oak-ns",
            {{PlankLook}},
            "Consumes": { "type": "block", "code": "woodstain:stainedplanks-red-oak-*", "quantity": 1 },
            "Drops": [ { "type": "block", "code": "woodstain:stainedplanks-red-oak-ns", "quantity": { "avg": 1, "var": 0 } } ]
        }
        """), (JObject)actual["stained-red-oak"]!);
    }

    private static (string, AssetLocation, IDictionary<string, string>) DyeCandidate(string color, string wood)
        => ("block", new AssetLocation($"dyedwood:chiselmaterial-{color}-{wood}"),
            new Dictionary<string, string> { ["color"] = color, ["wood"] = wood });

    [Fact]
    public void ShippedDyedWoodTemplateExpandsUnderItsOwnPrefix()
    {
        var families = (JObject)MaterialTextureOpacityTests.BlockAttributes("wall.json")["FinishFamilies"]!;

        var actual = MaterialFamilies.Expand(families, new JObject(), new[]
        {
            DyeCandidate("red", "oak"),
            DyeCandidate("blue", "rottenebony"),
            StainCandidate("red", "oak", "ns"),
        });

        Assert.Equal(new[] { "stained-red-oak", "dyed-red-oak", "dyed-blue-rottenebony" },
            actual.Properties().Select(p => p.Name));
        AssertJson(JObject.Parse($$"""
        {
            "Texture": "dyedwood:block/wood/planks/redoak1",
            {{PlankLook}},
            "Consumes": { "type": "block", "code": "dyedwood:planks", "quantity": 1, "attributes": { "types": { "color": "red", "wood": "oak" } } },
            "Drops": [ { "type": "block", "code": "dyedwood:planks", "quantity": { "avg": 1, "var": 0 }, "attributes": { "types": { "color": "red", "wood": "oak" } } } ]
        }
        """), (JObject)actual["dyed-red-oak"]!);
    }
}
