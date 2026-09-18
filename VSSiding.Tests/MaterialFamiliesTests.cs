using System.Collections.Generic;
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
}
