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
        IEnumerable<(string type, AssetLocation code, IDictionary<string, string> variants)> candidates)
    {
        var result = (JObject)explicitEntries.DeepClone();
        var explicitMaterials = new JsonObject(explicitEntries);

        foreach (var family in families.Properties())
        {
            var match = family.Value["Match"]!;
            string type = (string?)match["type"] ?? "item";
            var codePattern = new AssetLocation((string)match["code"]!);
            string variant = (string)match["variant"]!;
            string placeholder = "{" + variant + "}";

            foreach (var (candidateType, code, variants) in candidates)
            {
                if (candidateType != type || !WildcardUtil.Match(codePattern, code)) continue;
                if (!variants.TryGetValue(variant, out string? value)) continue;

                string key = family.Name.Replace(placeholder, value);
                if (result.ContainsKey(key)) continue;
                if (SidingWallBlock.MatchConsumes(code, explicitMaterials) != null) continue;

                var entry = (JObject)family.Value.DeepClone();
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
