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
}
