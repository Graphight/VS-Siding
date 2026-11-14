using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using Newtonsoft.Json.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
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
        foreach (var layout in new[] { "wall", "cornerout", "diagonal" })
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
            [("diagonal", "north")] = (-1, 1),
            [("diagonal", "east")] = (-1, -1),
            [("diagonal", "south")] = (1, -1),
            [("diagonal", "west")] = (1, 1),
        };
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void OccludeScalesEveryByteIncludingSunlight()
        => Assert.Equal(unchecked((int)0x7F_44_22_00), SidingModSystem.Occlude(unchecked((int)0xFF_88_44_00), 0.5f));

    [Fact]
    public void SealedFlagsAreThePanelsFacesPlusTheTopUnderADeck()
    {
        int N = BlockFacing.NORTH.Flag, E = BlockFacing.EAST.Flag, U = BlockFacing.UP.Flag;
        var actual = new Dictionary<string, int>
        {
            ["bare frame"] = Wall("wall", "north").SealedFlags(new SidingWallEntity { Framing = "oak" }),
            ["sealed wall"] = Wall("wall", "north").SealedFlags(Sealed()),
            ["sealed corner"] = Wall("cornerout", "north").SealedFlags(Sealed()),
            ["sealed wall under a deck"] = Wall("wall", "north").SealedFlags(new SidingWallEntity { Framing = "oak", Infill = "clay", Deck = "oak" }),
            ["sealed diagonal under its outer deck"] = Wall("diagonal", "north").SealedFlags(new SidingWallEntity { Framing = "oak", Infill = "clay", OuterDeck = "oak" }),
        };

        var expected = new Dictionary<string, int>
        {
            ["bare frame"] = 0,
            ["sealed wall"] = N,
            ["sealed corner"] = N | E,
            ["sealed wall under a deck"] = N | U,
            ["sealed diagonal under its outer deck"] = N | E | U,
        };
        Assert.Equal(expected, actual);
    }

    // Vanilla asks a ring cell with flags naming its face toward the drawn face's front cell plus a tangent,
    // and the front cell with its face pointing back at the drawn face, so a wall emits only where its panel touches.
    [Fact]
    public void ASealedWallEmitsSideAoOnlyWhereItsPanelTouchesTheFace()
    {
        var northOfAnUpFace = CubeFaceVertices.blockFaceVerticesCentered[BlockFacing.UP.Index][1];
        var tester = new FixedEntityTester(Sealed());
        (bool, bool) RingHalves(SidingWallBlock wall)
            => (wall.DoEmitSideAoByFlag(tester, northOfAnUpFace, northOfAnUpFace.OppositeFlagsUpperOrLeft),
                wall.DoEmitSideAoByFlag(tester, northOfAnUpFace, northOfAnUpFace.OppositeFlagsLowerOrRight));

        var actual = new Dictionary<string, object>
        {
            ["ring cell, panel toward the face"] = RingHalves(Wall("wall", "south")),
            ["ring cell, panel away from the face"] = RingHalves(Wall("wall", "north")),
            ["front cell of an east face, panel toward it"] = Wall("wall", "west").DoEmitSideAo(tester, BlockFacing.WEST),
            ["front cell of an east face, panel away from it"] = Wall("wall", "east").DoEmitSideAo(tester, BlockFacing.WEST),
        };

        var expected = new Dictionary<string, object>
        {
            ["ring cell, panel toward the face"] = (true, true),
            ["ring cell, panel away from the face"] = (false, false),
            ["front cell of an east face, panel toward it"] = true,
            ["front cell of an east face, panel away from it"] = false,
        };
        Assert.Equal(expected, actual);
    }

    private static SidingWallBlock Wall(string layout, string side)
    {
        var wall = new SidingWallBlock { Attributes = new JsonObject(JToken.Parse("""{ "Framings": { "oak": {} }, "Infills": { "clay": { "BlockMaterial": "Soil" } } }""")) };
        wall.VariantStrict["layout"] = layout;
        wall.VariantStrict["side"] = side;
        return wall;
    }

    private static SidingWallEntity Sealed() => new() { Framing = "oak", Infill = "clay" };

    private sealed class FixedEntityTester(BlockEntity entity) : IGeometryTester
    {
        public BlockEntity GetCurrentBlockEntityOnSide(BlockFacing side) => entity;
        public BlockEntity GetCurrentBlockEntityOnSide(Vec3iAndFacingFlags vec) => entity;
    }

    [Fact]
    public void CornerFacesAreTheTangentDirectionsTowardEachRingCell()
    {
        int N = BlockFacing.NORTH.Flag, E = BlockFacing.EAST.Flag, S = BlockFacing.SOUTH.Flag, W = BlockFacing.WEST.Flag,
            U = BlockFacing.UP.Flag, D = BlockFacing.DOWN.Flag;
        var expected = new[]
        {
            new[] { E | U, W | U, E | D, W | D },
            new[] { U | S, U | N, D | S, D | N },
            new[] { W | U, E | U, W | D, E | D },
            new[] { U | N, U | S, D | N, D | S },
            new[] { E | S, W | S, E | N, W | N },
            new[] { E | N, W | N, E | S, W | S },
        };
        var actual = Enumerable.Range(0, 6).Select(side => Enumerable.Range(0, 4).Select(k => SidingModSystem.CornerFaces(side, k)).ToArray()).ToArray();
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
