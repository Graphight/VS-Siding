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

    // The outside torch: breaking a hosted chest leaves air for a tick before the wall comes back,
    // and the server asks that air whether the torch on the wall's far side can stay. Only the
    // attach check (restorePending) answers for it; the hot paths keep reading the Hostable table.
    [Fact]
    public void MayHoldGuestCoversTheAirABrokenHostLeaves()
    {
        var air = new Block { BlockId = 0, Replaceable = 9999 };
        var tallgrass = new Block { BlockId = 1, Replaceable = 6000 };
        var chest = new Block { BlockId = 2 };
        var stone = new Block { BlockId = 3 };
        bool[] hostable = { false, false, true, false };

        var expected = new Dictionary<(string, bool), bool>
        {
            [("air", false)] = false, [("air", true)] = true,
            [("tallgrass", false)] = false, [("tallgrass", true)] = true,
            [("chest", false)] = true, [("chest", true)] = true,
            [("stone", false)] = false, [("stone", true)] = false,
        };

        var blocks = new Dictionary<string, Block> { ["air"] = air, ["tallgrass"] = tallgrass, ["chest"] = chest, ["stone"] = stone };
        var actual = new Dictionary<(string, bool), bool>();
        foreach (var (name, block) in blocks)
        {
            foreach (bool restorePending in new[] { false, true })
                actual[(name, restorePending)] = GuestSealingPatches.MayHoldGuest(block, hostable, restorePending);
        }

        Assert.Equal(expected, actual);
    }
}
