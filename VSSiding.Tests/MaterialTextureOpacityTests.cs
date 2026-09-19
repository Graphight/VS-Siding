using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json.Linq;
using SkiaSharp;
using Vintagestory.API.Common;
using Vintagestory.API.Util;
using Xunit;

namespace VSSiding.Tests;

public class MaterialTextureOpacityTests
{
    private static readonly string[] AssetDomainFolders = ["survival", "game", "creative"];

    internal static string GetAssemblyMetadata(string key)
    {
        var value = typeof(MaterialTextureOpacityTests).Assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(a => a.Key == key)?.Value;
        Assert.False(string.IsNullOrEmpty(value), $"AssemblyMetadata '{key}' was not set at build time");
        return value!;
    }

    // Every texture a material draws, and whether it must be opaque: a composite's overlays
    // draw over an opaque base, so only the base is held to that.
    private static IEnumerable<(string Code, bool MustBeOpaque)> CollectTextureCodes(JObject wallJson, string dictName, string familiesName,
        List<(string, AssetLocation, IDictionary<string, string>)> candidates)
    {
        var attributes = (JObject)wallJson["attributes"]!;
        var dict = (JObject)attributes[dictName]!;
        if (attributes[familiesName] is JObject families) dict = MaterialFamilies.Expand(families, dict, candidates);
        foreach (var entry in dict.Properties())
        {
            if (entry.Value["Texture"] is not JObject composite)
            {
                yield return ((string)entry.Value["Texture"]!, true);
                continue;
            }
            yield return ((string)composite["base"]!, true);
            foreach (var overlay in composite["overlays"] ?? new JArray())
                yield return ((string)overlay!, false);
            foreach (var overlay in composite["blendedOverlays"] ?? new JArray())
                yield return ((string)overlay["base"]!, false);
        }
    }

    // A vanilla item or block type's variants: one variant group's own states plus its
    // loadFromProperties list, minus skipVariants. Codes are "{code}-{value}" unless codeFormat
    // fills in the other groups, as for log-placed-{wood}-ud.
    private static IEnumerable<(string, AssetLocation, IDictionary<string, string>)> Candidates(
        string vintageStoryPath, string type, string relativePath, int groupIndex = 0, string codeFormat = "{0}-{1}")
    {
        var survival = Path.Combine(vintageStoryPath, "assets", "survival");
        var json = JToken.Parse(File.ReadAllText(Path.Combine(survival, relativePath)));
        var group = json["variantgroups"]![groupIndex]!;
        string code = (string)json["code"]!, variant = (string)group["code"]!;
        var values = group["states"]?.Select(t => (string)t!) ?? [];
        if (group["loadFromProperties"] is JValue props)
        {
            var properties = JToken.Parse(File.ReadAllText(Path.Combine(survival, "worldproperties", (string)props! + ".json")));
            values = values.Concat(properties["variants"]!.Select(v => (string)v["Code"]!));
        }
        var skip = json["skipVariants"]?.Select(t => new AssetLocation("game", (string)t!)).ToList() ?? [];
        return values.Distinct()
            .Select(v => (code: new AssetLocation("game", string.Format(codeFormat, code, v)), value: v))
            .Where(c => !skip.Any(s => WildcardUtil.Match(s, c.code)))
            .Select(c => (type, c.code, (IDictionary<string, string>)new Dictionary<string, string> { [variant] = c.value }));
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

        var candidates = Candidates(vintageStoryPath, "item", "itemtypes/resource/plank.json")
            .Concat(Candidates(vintageStoryPath, "block", "blocktypes/stone/cobble/cobblestone.json"))
            .Concat(Candidates(vintageStoryPath, "block", "blocktypes/stone/polished/polishedrock.json"))
            .Concat(Candidates(vintageStoryPath, "item", "itemtypes/resource/stonebrick.json"))
            .Concat(Candidates(vintageStoryPath, "item", "itemtypes/resource/stone.json"))
            .Concat(Candidates(vintageStoryPath, "item", "itemtypes/resource/burnedbrick.json"))
            .Concat(Candidates(vintageStoryPath, "item", "itemtypes/resource/clay.json"))
            .Concat(Candidates(vintageStoryPath, "item", "itemtypes/resource/daub.json"))
            .Concat(Candidates(vintageStoryPath, "block", "blocktypes/wood/woodtyped/log.json", 1, "{0}-placed-{1}-ud"))
            .ToList();
        var textureCodes = CollectTextureCodes(wallJson, "Framings", "FramingFamilies", candidates)
            .Concat(CollectTextureCodes(wallJson, "Infills", "InfillFamilies", candidates))
            .Concat(CollectTextureCodes(wallJson, "Finishes", "FinishFamilies", candidates))
            .Distinct();

        var offenders = new List<string>();
        foreach (var (textureCode, mustBeOpaque) in textureCodes)
        {
            var file = ResolveTextureFile(vintageStoryPath, textureCode);
            if (!mustBeOpaque) continue;
            using var bitmap = SKBitmap.Decode(file);
            int partial = bitmap.Pixels.Count(p => p.Alpha != 0 && p.Alpha != 255);
            if (partial > 0)
                offenders.Add($"{textureCode} ({partial} pixels with partial alpha)");
        }

        Assert.Equal(Array.Empty<string>(), offenders);
    }
}
