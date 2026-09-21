using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;
using Xunit;

namespace VSSiding.Tests;

public class MaterialDisplayNameTests
{
    // Every expanded Framings/Infills/Finishes entry's DisplayName must resolve to a key
    // present in en.json - otherwise the tooltip (decision 0030) falls back to a title-cased
    // material key instead of the intended English name.
    [Fact]
    public void EveryMaterialDisplayNameResolvesInLangFile()
    {
        var vintageStoryPath = MaterialTextureOpacityTests.GetAssemblyMetadata("VintageStoryPath");
        var repoRoot = MaterialTextureOpacityTests.GetAssemblyMetadata("RepoRoot");
        var wallJsonPath = Path.Combine(repoRoot, "VSSiding", "assets", "vssiding", "blocktypes", "wall.json");
        var wallJson = (JObject)JToken.Parse(File.ReadAllText(wallJsonPath));
        var langJsonPath = Path.Combine(repoRoot, "VSSiding", "assets", "vssiding", "lang", "en.json");
        var lang = (JObject)JToken.Parse(File.ReadAllText(langJsonPath));

        var candidates = MaterialTextureOpacityTests.AllCandidates(vintageStoryPath);

        var missing = new List<string>();
        foreach (var (dictName, familiesName) in new[] { ("Framings", "FramingFamilies"), ("Infills", "InfillFamilies"), ("Finishes", "FinishFamilies") })
        {
            var attributes = (JObject)wallJson["attributes"]!;
            var dict = (JObject)attributes[dictName]!;
            if (attributes[familiesName] is JObject families) dict = MaterialFamilies.Expand(families, dict, candidates);
            foreach (var entry in dict.Properties())
            {
                string? displayName = (string?)entry.Value["DisplayName"];
                if (displayName == null)
                {
                    missing.Add($"{dictName}.{entry.Name}: no DisplayName");
                    continue;
                }
                string key = displayName.StartsWith("vssiding:") ? displayName["vssiding:".Length..] : displayName;
                if (lang[key] == null)
                    missing.Add($"{dictName}.{entry.Name}: {key}");
            }
        }

        Assert.Equal(Array.Empty<string>(), missing);
    }

    // The tooltip's own lang keys are literals in the source, not entries in wall.json, so the
    // DisplayName sweep above never sees them - a typo in one ships a raw key to a player. Read
    // them back out of the source rather than restating the list here, where the same typo would
    // just be copied across and pass.
    [Fact]
    public void EveryTooltipLangKeyInTheSourceResolves()
    {
        var repoRoot = MaterialTextureOpacityTests.GetAssemblyMetadata("RepoRoot");
        var lang = (JObject)JToken.Parse(File.ReadAllText(
            Path.Combine(repoRoot, "VSSiding", "assets", "vssiding", "lang", "en.json")));

        var keys = Directory.EnumerateFiles(Path.Combine(repoRoot, "VSSiding"), "*.cs", SearchOption.AllDirectories)
            .SelectMany(file => Regex.Matches(File.ReadAllText(file), @"vssiding:(tooltip-[a-z-]+)"))
            .Select(match => match.Groups[1].Value)
            .Distinct()
            .OrderBy(key => key)
            .ToList();

        Assert.NotEmpty(keys);
        Assert.Equal(Array.Empty<string>(), keys.Where(key => lang[key] == null).ToArray());
    }
}
