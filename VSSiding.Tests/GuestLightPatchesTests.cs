using System.Reflection;
using HarmonyLib;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.Client.NoObf;
using Vintagestory.GameContent;
using Xunit;

namespace VSSiding.Tests;

public class GuestLightPatchesTests
{
    [Fact]
    public void GetLightAbsorptionAccessorTargetsStillExistOnBlockAndAKnownVanillaOverride()
    {
        var signature = new[] { typeof(IBlockAccessor), typeof(BlockPos) };
        Assert.NotNull(typeof(Block).GetMethod(nameof(Block.GetLightAbsorption),
            BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly, null, signature, null));

        Assert.NotNull(typeof(BlockMicroBlock).GetMethod(nameof(Block.GetLightAbsorption),
            BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly, null, signature, null));
    }

    [Fact]
    public void GetLightAbsorptionChunkTargetsStillExistOnBlockAndAKnownVanillaOverride()
    {
        var signature = new[] { typeof(IWorldChunk), typeof(BlockPos) };
        Assert.NotNull(typeof(Block).GetMethod(nameof(Block.GetLightAbsorption),
            BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly, null, signature, null));

        Assert.NotNull(typeof(BlockMicroBlock).GetMethod(nameof(Block.GetLightAbsorption),
            BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly, null, signature, null));
    }

    [Fact]
    public void DoEmitSideAoTargetsStillExistOnBlockAndAKnownVanillaOverride()
    {
        var signature = new[] { typeof(IGeometryTester), typeof(BlockFacing) };
        Assert.NotNull(typeof(Block).GetMethod(nameof(Block.DoEmitSideAo),
            BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly, null, signature, null));

        Assert.NotNull(typeof(BlockMicroBlock).GetMethod(nameof(Block.DoEmitSideAo),
            BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly, null, signature, null));
    }

    [Fact]
    public void DoEmitSideAoByFlagTargetsStillExistOnBlockAndAKnownVanillaOverride()
    {
        var signature = new[] { typeof(IGeometryTester), typeof(Vec3iAndFacingFlags), typeof(int) };
        Assert.NotNull(typeof(Block).GetMethod(nameof(Block.DoEmitSideAoByFlag),
            BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly, null, signature, null));

        Assert.NotNull(typeof(BlockMicroBlock).GetMethod(nameof(Block.DoEmitSideAoByFlag),
            BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly, null, signature, null));
    }

    // The side AO postfixes read the caller's position off TCTCache directly (SidingWallBlock's own
    // overrides go through IGeometryTester.GetCurrentBlockEntityOnSide instead, which has no field
    // to pin), and the guest cell enumeration in SealedCellLightPostfix reads WorldMap.GetChunk.
    [Fact]
    public void TCTCachePositionFieldsAndWorldMapGetChunkStillExist()
    {
        Assert.NotNull(AccessTools.Field(typeof(TCTCache), "posX"));
        Assert.NotNull(AccessTools.Field(typeof(TCTCache), "posY"));
        Assert.NotNull(AccessTools.Field(typeof(TCTCache), "posZ"));
        Assert.NotNull(AccessTools.Field(typeof(TCTCache), "dimension"));
        Assert.NotNull(AccessTools.Field(typeof(ClientMain), "WorldMap"));
        Assert.NotNull(AccessTools.Method(typeof(ClientWorldMap), "GetChunk",
            new[] { typeof(int), typeof(int), typeof(int) }));
    }
}
