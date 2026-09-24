using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using Vintagestory.API.MathTools;
using Vintagestory.Client.NoObf;
using Xunit;

namespace VSSiding.Tests;

public class SealedCellLightTests
{
    // The extended array's own index math, decoded back from BuildExtendedChunkData's own
    // (y*34+z)*34+x scheme (SealedCellLightPostfix's wall-scan loop decodes it the same way).
    [Fact]
    public void TryExtendedIndexMapsAGuestInsideTheOneCellBorder()
    {
        bool found = SidingModSystem.TryExtendedIndex(new BlockPos(31, 5, 0), 1, 0, 0, out int extIndex);
        Assert.True(found);
        Assert.Equal(MapUtil.Index3d(0, 6, 1, 34, 34), extIndex);
    }

    [Fact]
    public void TryExtendedIndexRejectsAGuestOutsideTheOneCellBorder()
    {
        bool found = SidingModSystem.TryExtendedIndex(new BlockPos(0, 5, 0), 3, 0, 0, out _);
        Assert.False(found);
    }

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
    public void OccludeScalesEveryByteIncludingSunlight()
        => Assert.Equal(unchecked((int)0x7F_44_22_00), SidingModSystem.Occlude(unchecked((int)0xFF_88_44_00), 0.5f));

    [Fact]
    public void PatchTargetsStillExist()
    {
        Assert.NotNull(AccessTools.Method(typeof(ChunkTesselator), "BuildExtendedChunkData"));
        Assert.NotNull(AccessTools.Field(typeof(ChunkTesselator), "game"));
        Assert.NotNull(AccessTools.Field(typeof(ChunkTesselator), "currentChunkBlocksExt"));
        Assert.NotNull(AccessTools.Field(typeof(ChunkTesselator), "currentChunkRgbsExt"));
        Assert.NotNull(AccessTools.Method(typeof(TCTCache), "CalcBlockFaceLight"));
        Assert.NotNull(AccessTools.Field(typeof(TCTCache), "CurrentLightRGBByCorner"));
        Assert.NotNull(AccessTools.Field(typeof(TCTCache), "occ"));
        Assert.NotNull(AccessTools.Field(typeof(TCTCache), "aoAndSmoothShadows"));
        Assert.NotNull(AccessTools.Field(typeof(TCTCache), "block"));
    }

    // Harmony binds the patches' parameters by name, so a vanilla rename would drop the fix at
    // runtime with only a log line to show for it.
    [Fact]
    public void PatchedParameterNamesStillMatch()
    {
        var actual = AccessTools.Method(typeof(ChunkTesselator), "BuildExtendedChunkData")
            .GetParameters().Select(p => p.Name).ToArray();
        Assert.Equal(new[] { "curChunk", "chunkX", "chunkY", "chunkZ", "atMapEdge", "skipChunkCenter" }, actual);

        var faceLight = AccessTools.Method(typeof(TCTCache), "CalcBlockFaceLight")
            .GetParameters().Select(p => p.Name).ToArray();
        Assert.Equal(new[] { "tileSide", "extNeibIndex3d" }, faceLight);
    }
}
