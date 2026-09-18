using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json.Linq;
using SkiaSharp;
using Vintagestory.API.Common;
using Xunit;

namespace VSSiding.Tests;

public class MaterialTextureOpacityTests
{
    private static readonly string[] AssetDomainFolders = ["survival", "game", "creative"];

    private static string GetAssemblyMetadata(string key)
    {
        var value = typeof(MaterialTextureOpacityTests).Assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(a => a.Key == key)?.Value;
        Assert.False(string.IsNullOrEmpty(value), $"AssemblyMetadata '{key}' was not set at build time");
        return value!;
    }

    private static IEnumerable<string> CollectTextureCodes(JObject wallJson, string dictName, string familiesName,
        List<(string, AssetLocation, IDictionary<string, string>)> candidates)
    {
        var attributes = (JObject)wallJson["attributes"]!;
        var dict = (JObject)attributes[dictName]!;
        if (attributes[familiesName] is JObject families) dict = MaterialFamilies.Expand(families, dict, candidates);
        foreach (var entry in dict.Properties())
            yield return (string)entry.Value["Texture"]!;
    }

    // Vanilla's plank items: plank.json's own states plus every wood in worldproperties/block/wood.json.
    private static List<(string, AssetLocation, IDictionary<string, string>)> PlankCandidates(string vintageStoryPath)
    {
        var survival = Path.Combine(vintageStoryPath, "assets", "survival");
        var plank = JToken.Parse(File.ReadAllText(Path.Combine(survival, "itemtypes", "resource", "plank.json")));
        var wood = JToken.Parse(File.ReadAllText(Path.Combine(survival, "worldproperties", "block", "wood.json")));
        var woods = plank["variantgroups"]![0]!["states"]!.Select(t => (string)t!)
            .Concat(wood["variants"]!.Select(v => (string)v["Code"]!));
        return woods.Select(w => ("item", new AssetLocation("game", "plank-" + w), (IDictionary<string, string>)new Dictionary<string, string> { ["wood"] = w }))
            .ToList();
    }

    private static string ResolveTextureFile(string vintageStoryPath, string textureCode)
    {
        var parts = textureCode.Split(':', 2);
        var relativePath = parts[1] + ".png";
        foreach (var domainFolder in AssetDomainFolders)
        {
            var candidate = Path.Combine(vintageStoryPath, "assets", domainFolder, "textures", relativePath);
            if (File.Exists(candidate))
                return candidate;
        }
        throw new FileNotFoundException($"Could not find texture file for '{textureCode}' under any of: {string.Join(", ", AssetDomainFolders)}");
    }

    [Fact]
    public void NoMaterialTextureHasPartialAlpha()
    {
        var vintageStoryPath = GetAssemblyMetadata("VintageStoryPath");
        var repoRoot = GetAssemblyMetadata("RepoRoot");
        var wallJsonPath = Path.Combine(repoRoot, "VSSiding", "assets", "vssiding", "blocktypes", "wall.json");
        var wallJson = (JObject)JToken.Parse(File.ReadAllText(wallJsonPath));

        var candidates = PlankCandidates(vintageStoryPath);
        var textureCodes = CollectTextureCodes(wallJson, "Framings", "FramingFamilies", candidates)
            .Concat(CollectTextureCodes(wallJson, "Infills", "InfillFamilies", candidates))
            .Concat(CollectTextureCodes(wallJson, "Finishes", "FinishFamilies", candidates))
            .Distinct();

        var offenders = new List<string>();
        foreach (var textureCode in textureCodes)
        {
            var file = ResolveTextureFile(vintageStoryPath, textureCode);
            using var bitmap = SKBitmap.Decode(file);
            int partial = bitmap.Pixels.Count(p => p.Alpha != 0 && p.Alpha != 255);
            if (partial > 0)
                offenders.Add($"{textureCode} ({partial} pixels with partial alpha)");
        }

        Assert.Equal(Array.Empty<string>(), offenders);
    }
}
