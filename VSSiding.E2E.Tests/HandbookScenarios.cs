using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Atlas.Api;
using Atlas.XUnit;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Xunit;

namespace VSSiding.E2E.Tests;

// An unknown itemstack code draws block 0 with no warning, so the codes are resolved here instead.
[AtlasWorld(StrictBootDiagnostics = true)]
public class HandbookScenarios : AtlasScenarioBase
{
    private static readonly string[] PageKeys =
    {
        "gamemechanicinfo-siding-text",
        "gamemechanicinfo-siding-walls-text",
        "gamemechanicinfo-siding-floors-text",
        "gamemechanicinfo-siding-modes-text",
        "gamemechanicinfo-siding-corners-text",
        "gamemechanicinfo-siding-rooms-text",
        "gamemechanicinfo-siding-furniture-text",
        "gamemechanicinfo-siding-stoneage-text"
    };

    [AtlasScenario(FreshWorld = true)]
    public Task ItemstackTags_Should_NameRealItemsAndBlocks()
    {
        List<(string Type, string Code)> codes = PageKeys
            .Where(key => Lang.HasTranslation("vssiding:" + key))
            .Select(key => Lang.Get("vssiding:" + key))
            .SelectMany(text => Regex.Matches(text, "<itemstack([^>]*)>([^<]*)</itemstack>").Select(m => (Attributes: m.Groups[1].Value, Codes: m.Groups[2].Value)))
            .SelectMany(tag => tag.Codes.Split('|').Select(code => (tag.Attributes.Contains("type=\"item\"") ? "item" : "block", code)))
            .ToList();

        List<string> unresolved = codes
            .Where(c => !Resolves(c.Type, c.Code))
            .Select(c => $"{c.Type} {c.Code}")
            .ToList();

        Assert.NotEmpty(codes);
        Assert.Equal(new List<string>(), unresolved);
        return Task.CompletedTask;
    }

    private bool Resolves(string type, string code)
    {
        AssetLocation location = new(code.Contains(':') ? code : "game:" + code);
        return type == "item"
            ? World.Api.World.GetItem(location) != null
            : World.Api.World.GetBlock(location)?.BlockId > 0;
    }
}
