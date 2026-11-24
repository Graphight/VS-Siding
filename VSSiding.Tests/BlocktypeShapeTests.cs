using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using Xunit;

namespace VSSiding.Tests;

// A shape base with no domain resolves to vssiding:, so a vanilla shape needs its game: prefix (#190).
public class BlocktypeShapeTests
{
    [Fact]
    public void EveryDomainlessShapeBaseIsOneOfOurShapes()
    {
        var assets = Path.Combine(MaterialTextureOpacityTests.GetAssemblyMetadata("RepoRoot"), "VSSiding", "assets", "vssiding");
        var missing = Directory.GetFiles(Path.Combine(assets, "blocktypes"), "*.json")
            .SelectMany(file => JObject.Parse(File.ReadAllText(file))
                .Descendants().OfType<JProperty>()
                .Where(p => p.Name.StartsWith("shape", StringComparison.OrdinalIgnoreCase))
                .Select(p => p.Value).OfType<JContainer>()
                .SelectMany(c => c.Descendants().OfType<JProperty>())
                .Where(p => p.Name.Equals("base", StringComparison.OrdinalIgnoreCase) && p.Value.Type == JTokenType.String)
                .Select(p => (string)p.Value!)
                .Where(b => !b.Contains(':') && !File.Exists(Path.Combine(assets, "shapes", b + ".json")))
                .Select(b => $"{Path.GetFileName(file)}: {b}"))
            .ToList();

        Assert.Empty(missing);
    }
}
