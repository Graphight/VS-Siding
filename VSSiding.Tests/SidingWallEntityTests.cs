using Vintagestory.API.Datastructures;
using Xunit;

namespace VSSiding.Tests;

public class SidingWallEntityTests
{
    [Fact]
    public void UnsetKeySurvivesByteRoundTripAsNullNotEmptyString()
    {
        var tree = new TreeAttribute();
        tree.SetString("framing", null);

        var reloaded = new TreeAttribute();
        reloaded.FromBytes(tree.ToBytes());

        Assert.Null(SidingWallEntity.NullIfEmpty(reloaded.GetString("framing", null)));
    }

    [Fact]
    public void SetKeySurvivesByteRoundTrip()
    {
        var tree = new TreeAttribute();
        tree.SetString("framing", "oak");

        var reloaded = new TreeAttribute();
        reloaded.FromBytes(tree.ToBytes());

        Assert.Equal("oak", SidingWallEntity.NullIfEmpty(reloaded.GetString("framing", null)));
    }

    [Fact]
    public void SelectiveElementsSkipsUnbuiltParts()
    {
        Assert.Equal(new string[0], SidingWallEntity.SelectiveElements(null, null, null, null));
        Assert.Equal(new[] { "front", "framing", "infill", "back" },
            SidingWallEntity.SelectiveElements("oak", "wattle", "daub", "brick"));
        Assert.Equal(new[] { "framing" }, SidingWallEntity.SelectiveElements("oak", null, null, null));
    }

    [Theory]
    [InlineData("west", 0)]
    [InlineData("south", 90)]
    [InlineData("east", 180)]
    [InlineData("north", 270)]
    public void RotationYDegMatchesCollisionBoxRotation(string side, float expectedDegrees)
    {
        Assert.Equal(expectedDegrees, SidingWallEntity.RotationYDeg(side));
    }

    [Fact]
    public void CacheKeyDiffersByLayoutForTheSameMaterials()
    {
        string wallKey = SidingWallEntity.CacheKey("wall", "west", "oak", "wattle", "daub", "brick");
        string cornerKey = SidingWallEntity.CacheKey("cornerout", "west", "oak", "wattle", "daub", "brick");

        Assert.NotEqual(wallKey, cornerKey);
    }
}
