using System.Collections.Generic;
using System.Reflection;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;
using Xunit;

namespace VSSiding.Tests;

public class GuestSealingPatchesTests
{
    // A hosted torch clicks the panel's inner face and vanilla asks the far cell, so
    // ClaimsFaceTowardPos has to answer for the QUERIED face rather than the guest's own claimed
    // face - the mirror image of SidingWallBlockClaimsFaceTests' table. A straight wall's panel
    // faces back at exactly the face opposite its side; a cornerout's panel faces back at the two
    // faces opposite its side and its second leg (SidingWallBlock's CorneroutSecondFace).
    private static readonly string[] HorizontalFaces = { "north", "east", "south", "west" };

    [Fact]
    public void ClaimsFaceTowardPosAnswersForTheQueriedFacesFarSide()
    {
        var expected = new Dictionary<(string layout, string side, string queriedFace), bool>
        {
            [("wall", "north", "north")] = false, [("wall", "north", "east")] = false,
            [("wall", "north", "south")] = true, [("wall", "north", "west")] = false,

            [("wall", "east", "north")] = false, [("wall", "east", "east")] = false,
            [("wall", "east", "south")] = false, [("wall", "east", "west")] = true,

            [("wall", "south", "north")] = true, [("wall", "south", "east")] = false,
            [("wall", "south", "south")] = false, [("wall", "south", "west")] = false,

            [("wall", "west", "north")] = false, [("wall", "west", "east")] = true,
            [("wall", "west", "south")] = false, [("wall", "west", "west")] = false,

            [("cornerout", "north", "north")] = false, [("cornerout", "north", "east")] = false,
            [("cornerout", "north", "south")] = true, [("cornerout", "north", "west")] = true,

            [("cornerout", "east", "north")] = true, [("cornerout", "east", "east")] = false,
            [("cornerout", "east", "south")] = false, [("cornerout", "east", "west")] = true,

            [("cornerout", "south", "north")] = true, [("cornerout", "south", "east")] = true,
            [("cornerout", "south", "south")] = false, [("cornerout", "south", "west")] = false,

            [("cornerout", "west", "north")] = false, [("cornerout", "west", "east")] = true,
            [("cornerout", "west", "south")] = true, [("cornerout", "west", "west")] = false,
        };

        var actual = new Dictionary<(string layout, string side, string queriedFace), bool>();
        foreach (string layout in new[] { "wall", "cornerout" })
        {
            foreach (string side in HorizontalFaces)
            {
                foreach (string queriedFace in HorizontalFaces)
                {
                    actual[(layout, side, queriedFace)] = GuestSealingPatches.ClaimsFaceTowardPos(layout, side, queriedFace);
                }
            }
        }

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void GetRetentionTargetsStillExistOnBlockAndAKnownVanillaOverride()
    {
        var signature = new[] { typeof(BlockPos), typeof(BlockFacing), typeof(EnumRetentionType) };
        Assert.NotNull(typeof(Block).GetMethod(nameof(Block.GetRetention),
            BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly, null, signature, null));

        // BlockFarmland declares its own override - proof the enumeration reaches a real vanilla
        // Block subclass, not just Block itself.
        Assert.NotNull(typeof(BlockFarmland).GetMethod(nameof(Block.GetRetention),
            BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly, null, signature, null));
    }

    [Fact]
    public void GetLiquidBarrierHeightOnSideTargetsStillExistOnBlockAndAKnownVanillaOverride()
    {
        var signature = new[] { typeof(BlockFacing), typeof(BlockPos) };
        Assert.NotNull(typeof(Block).GetMethod(nameof(Block.GetLiquidBarrierHeightOnSide),
            BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly, null, signature, null));

        Assert.NotNull(typeof(BlockMultiblock).GetMethod(nameof(Block.GetLiquidBarrierHeightOnSide),
            BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly, null, signature, null));
    }

    [Fact]
    public void CanAttachBlockAtTargetsStillExistOnBlockAndAKnownVanillaOverride()
    {
        var signature = new[] { typeof(IBlockAccessor), typeof(Block), typeof(BlockPos), typeof(BlockFacing), typeof(Cuboidi) };
        Assert.NotNull(typeof(Block).GetMethod(nameof(Block.CanAttachBlockAt),
            BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly, null, signature, null));

        Assert.NotNull(typeof(BlockGroundStorage).GetMethod(nameof(Block.CanAttachBlockAt),
            BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly, null, signature, null));
    }
}
