using System.Collections.Generic;
using HarmonyLib;
using Vintagestory.Client.NoObf;
using Xunit;

namespace VSSiding.Tests;

public class SealedCellLightTests
{
    [Fact]
    public void OpenSideFacesAwayFromThePanel()
    {
        var actual = new Dictionary<(string, string), (int, int)>();
        foreach (var layout in new[] { "wall", "cornerout" })
        foreach (var side in new[] { "north", "east", "south", "west" })
            actual[(layout, side)] = SidingWallBlock.OpenSide(layout, side);

        var expected = new Dictionary<(string, string), (int, int)>
        {
            [("wall", "north")] = (0, 1),
            [("wall", "east")] = (-1, 0),
            [("wall", "south")] = (0, -1),
            [("wall", "west")] = (1, 0),
            [("cornerout", "north")] = (-1, 1),
            [("cornerout", "east")] = (-1, -1),
            [("cornerout", "south")] = (1, -1),
            [("cornerout", "west")] = (1, 1),
        };
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void PatchTargetsStillExist()
    {
        Assert.NotNull(AccessTools.Method(typeof(ChunkTesselator), "BuildExtendedChunkData"));
        Assert.NotNull(AccessTools.Field(typeof(ChunkTesselator), "game"));
        Assert.NotNull(AccessTools.Field(typeof(ChunkTesselator), "currentChunkBlocksExt"));
        Assert.NotNull(AccessTools.Field(typeof(ChunkTesselator), "currentChunkRgbsExt"));
        Assert.NotNull(AccessTools.Method(typeof(TCTCache), "CalcBlockFaceLight"));
        Assert.NotNull(AccessTools.Field(typeof(TCTCache), "CurrentLightRGBByCorner"));
    }
}
