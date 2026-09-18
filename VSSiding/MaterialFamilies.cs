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
                || match["variant"] is not JValue { Type: JTokenType.String } variantToken)
            {
                warn?.Invoke($"material family '{family.Name}' needs Match.code and Match.variant; skipped");
                continue;
            }
            string type = (string?)match["type"] ?? "item";
            var codePattern = new AssetLocation((string)codeToken!);
            string variant = (string)variantToken!;
            string placeholder = "{" + variant + "}";

            foreach (var (candidateType, code, variants) in candidates)
            {
                if (candidateType != type || !WildcardUtil.Match(codePattern, code)) continue;
                if (!variants.TryGetValue(variant, out string? value)) continue;

                string key = family.Name.Replace(placeholder, value);
                if (result.ContainsKey(key)) continue;
                if (SidingWallBlock.MatchConsumes(code, explicitMaterials) != null) continue;

                var entry = (JObject)template.DeepClone();
                entry.Remove("Match");
                foreach (var str in entry.Descendants().OfType<JValue>().Where(v => v.Type == JTokenType.String).ToList())
                {
                    str.Value = ((string)str.Value!).Replace(placeholder, value);
                }
                result[key] = entry;
            }
        }

        return result;
    }
}
