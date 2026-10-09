using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Util;

namespace VSSiding;

// Expands family templates (e.g. FramingFamilies."{wood}") into one material entry per
// matching item or block - see decision 0010.
public static class MaterialFamilies
{
    internal static JObject Expand(
        JObject families, JObject explicitEntries,
        IEnumerable<(string type, AssetLocation code, IDictionary<string, string> variants)> candidates,
        Action<string>? warn = null)
    {
        var result = (JObject)explicitEntries.DeepClone();
        var explicitMaterials = new JsonObject(explicitEntries);

        foreach (var family in families.Properties())
        {
            // Family entries can come from other mods' patches, so a malformed one costs that material, not the load.
            if (family.Value is not JObject template || template["Match"] is not JObject match
                || match["code"] is not JValue { Type: JTokenType.String } codeToken
                || VariantNames(match["variant"]) is not { } variantNames)
            {
                warn?.Invoke($"material family '{family.Name}' needs Match.code and Match.variant; skipped");
                continue;
            }
            string type = (string?)match["type"] ?? "item";
            var codePattern = new AssetLocation((string)codeToken!);

            foreach (var (candidateType, code, variants) in candidates)
            {
                if (candidateType != type || !WildcardUtil.Match(codePattern, code)) continue;
                if (!variantNames.All(variants.ContainsKey)) continue;

                string key = Fill(family.Name, variantNames, variants);
                if (result.ContainsKey(key)) continue;
                if (SidingWallBlock.MatchConsumes(code, explicitMaterials) != null) continue;

                var entry = (JObject)template.DeepClone();
                entry.Remove("Match");
                foreach (var str in entry.Descendants().OfType<JValue>().Where(v => v.Type == JTokenType.String).ToList())
                {
                    str.Value = Fill((string)str.Value!, variantNames, variants).Replace("{domain}", code.Domain);
                }
                result[key] = entry;
            }
        }

        return result;
    }

    private static List<string>? VariantNames(JToken? token) => token switch
    {
        JValue { Type: JTokenType.String } name => [(string)name!],
        JArray { Count: > 0 } names when names.All(n => n is JValue { Type: JTokenType.String }) => names.Select(n => (string)n!).ToList(),
        _ => null,
    };

    private static string Fill(string text, List<string> variantNames, IDictionary<string, string> variants)
        => variantNames.Aggregate(text, (current, name) => current.Replace("{" + name + "}", variants[name]));

    // Lays a block's own attributes over the shared config/materials.json: each shared dictionary
    // keeps its entries, and an entry of the same key in the block's file replaces it whole.
    internal static JObject MergeShared(JObject shared, JObject attributes)
    {
        var result = (JObject)attributes.DeepClone();
        foreach (var dictionary in shared.Properties())
        {
            var merged = (JObject)dictionary.Value.DeepClone();
            if (attributes[dictionary.Name] is JObject own)
            {
                foreach (var entry in own.Properties()) merged[entry.Name] = entry.Value.DeepClone();
            }
            result[dictionary.Name] = merged;
        }
        return result;
    }
}
