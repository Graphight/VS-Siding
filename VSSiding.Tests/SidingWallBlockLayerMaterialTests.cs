using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Xunit;

namespace VSSiding.Tests;

public class SidingWallBlockLayerMaterialTests
{
    private static JsonObject Dict(string json) => new(JToken.Parse(json));

    private static readonly JsonObject Infills = Dict("""{ "clay": { "BlockMaterial": "Soil" } }""");

    private static readonly JsonObject Finishes = Dict("""{ "ashlar-granite": { "BlockMaterial": "Stone" } }""");

    [Fact]
    public void FrontLayerKeyIsFront()
    {
        Assert.Equal("ashlar-granite", SidingWallBlock.LayerKey("front", "clay", "ashlar-granite", "shakes-oak", "planks", "oak"));
    }

    [Fact]
    public void SecondFrontLayerKeyIsSecondFront()
    {
        Assert.Equal("shakes-oak", SidingWallBlock.LayerKey("secondfront", "clay", "ashlar-granite", "shakes-oak", "planks", "oak"));
    }

    [Fact]
    public void BackLayerKeyIsBack()
    {
        Assert.Equal("planks", SidingWallBlock.LayerKey("back", "clay", "ashlar-granite", "shakes-oak", "planks", "oak"));
    }

    [Fact]
    public void InfillLayerKeyIsInfill()
    {
        Assert.Equal("clay", SidingWallBlock.LayerKey("infill", "clay", "ashlar-granite", "shakes-oak", "planks", "oak"));
    }

    [Fact]
    public void DeckLayerKeyIsDeck()
    {
        Assert.Equal("oak", SidingWallBlock.LayerKey("deck", "clay", "ashlar-granite", "shakes-oak", "planks", "oak"));
    }

    [Fact]
    public void DeckLayerKeysResolveToTheirOwnMaterials()
    {
        var keys = new[] { "deckinfill", "deckfront", "deckback" }
            .Select(layer => SidingWallBlock.LayerKey(layer, "clay", null, null, null, "oak", null, "wattle", "planks", "daub"))
            .ToArray();
        Assert.Equal(new[] { "wattle", "planks", "daub" }, keys);
    }

    [Fact]
    public void StepLayerKeyIsStep()
    {
        Assert.Equal("game:plankstairs-oak-up-north-free", SidingWallBlock.LayerKey("step", "clay", "ashlar-granite", "shakes-oak", "planks", null, "game:plankstairs-oak-up-north-free"));
    }

    [Fact]
    public void NullLayerKeyFallsBackToInfill()
    {
        Assert.Equal("clay", SidingWallBlock.LayerKey(null, "clay", "ashlar-granite", "shakes-oak", "planks", "oak"));
    }

    [Fact]
    public void InfillLayerMaterialReadsInfillsDictionary()
    {
        Assert.Equal(EnumBlockMaterial.Soil, SidingWallBlock.LayerMaterial("infill", "clay", Infills, Finishes, EnumBlockMaterial.Wood));
    }

    [Fact]
    public void FrontLayerMaterialReadsFinishesDictionary()
    {
        Assert.Equal(EnumBlockMaterial.Stone, SidingWallBlock.LayerMaterial("front", "ashlar-granite", Infills, Finishes, EnumBlockMaterial.Wood));
    }

    [Fact]
    public void MissingKeyFallsBack()
    {
        Assert.Equal(EnumBlockMaterial.Wood, SidingWallBlock.LayerMaterial("front", null, Infills, Finishes, EnumBlockMaterial.Wood));
    }

    [Fact]
    public void UnknownKeyFallsBack()
    {
        Assert.Equal(EnumBlockMaterial.Wood, SidingWallBlock.LayerMaterial("front", "planks", Infills, Finishes, EnumBlockMaterial.Wood));
    }

    [Fact]
    public void DeckLayerFallsBackLikeTheFrame()
    {
        Assert.Equal(EnumBlockMaterial.Wood, SidingWallBlock.LayerMaterial("deck", "oak", Infills, Finishes, EnumBlockMaterial.Wood));
    }

    [Fact]
    public void DeckLayersReadTheirOwnDictionaries()
    {
        var materials = new[] { "deckinfill", "deckfront", "deckback" }
            .Select(layer => SidingWallBlock.LayerMaterial(layer, layer == "deckinfill" ? "clay" : "ashlar-granite", Infills, Finishes, EnumBlockMaterial.Wood))
            .ToArray();
        Assert.Equal(new[] { EnumBlockMaterial.Soil, EnumBlockMaterial.Stone, EnumBlockMaterial.Stone }, materials);
    }

    // A material missing from LayerSounds or LayerResistance quietly falls back to the block's
    // plank sounds and base resistance, so every one a layer can report needs both keys.
    [Fact]
    public void EveryLayerMaterialHasSoundsAndResistance()
    {
        var repoRoot = MaterialTextureOpacityTests.GetAssemblyMetadata("RepoRoot");
        var wallJson = (JObject)JToken.Parse(File.ReadAllText(
            Path.Combine(repoRoot, "VSSiding", "assets", "vssiding", "blocktypes", "wall.json")));
        var attributes = MaterialTextureOpacityTests.BlockAttributes("wall.json");
        var sounds = (JObject)attributes["LayerSounds"]!;
        var resistance = (JObject)attributes["LayerResistance"]!;

        var materials = new[] { "Infills", "InfillFamilies", "Finishes", "FinishFamilies" }
            .SelectMany(dict => ((JObject)attributes[dict]!).Properties())
            .Select(entry => (string)entry.Value["BlockMaterial"]!)
            .Append((string)wallJson["blockmaterial"]!)
            .Distinct();

        var missing = materials
            .Where(m => !Enum.TryParse<EnumBlockMaterial>(m, true, out _) || sounds[m] == null || resistance[m] == null)
            .ToList();

        Assert.Equal(Array.Empty<string>(), missing);
    }
}
