using System.Collections.Generic;
using Vintagestory.API.MathTools;
using System;
using System.Linq;
using Vintagestory.API.Common;
using Xunit;

namespace VSSiding.Tests;

public class SidingWallBlockCollisionTests
{
    // Cuboidf.Equals compares floats exactly, but RotatedCopy goes through trig, so a
    // tolerant comparer is needed for the rotated cases.
    private class ApproximatelyEqual : IEqualityComparer<Cuboidf>
    {
        public bool Equals(Cuboidf? x, Cuboidf? y)
        {
            if (x == null || y == null) return x == y;
            return Close(x.X1, y.X1) && Close(x.Y1, y.Y1) && Close(x.Z1, y.Z1)
                && Close(x.X2, y.X2) && Close(x.Y2, y.Y2) && Close(x.Z2, y.Z2);
        }

        public int GetHashCode(Cuboidf obj) => 0;

        private static bool Close(float a, float b) => System.Math.Abs(a - b) < 0.0001f;
    }

    private static readonly ApproximatelyEqual Comparer = new();

    private static readonly Cuboidf[] FullBoxes = { new(0, 0, 0, 1, 1, 1) };

    [Fact]
    public void RotationDirectionMatchesTheJsonSlabConvention()
    {
        // wall.json's static slab, x 0..0.25, must land on z 0.75..1 when rotated for "south".
        var slab = new Cuboidf(0, 0, 0, 0.25f, 1, 1);
        var rotated = slab.RotatedCopy(0, SidingWallEntity.RotationYDeg("south"), 0, new Vec3d(0.5, 0.5, 0.5));

        Assert.Equal(new Cuboidf(0, 0, 0.75f, 1, 1, 1), rotated, Comparer);
    }

    [Fact]
    public void WallWestPostBoxesAreUnrotated()
    {
        var expected = new[]
        {
            new Cuboidf(1f / 16, 0, 0, 3f / 16, 1, 1f / 16),
            new Cuboidf(1f / 16, 0, 15f / 16, 3f / 16, 1, 1),
        };

        Assert.Equal(expected, SidingWallBlock.ComputeCollisionBoxes("wall", "west", "oak", null, (true, false, false, false), FullBoxes), Comparer);
    }

    [Fact]
    public void WallSouthPostBoxesAreRotated()
    {
        var expected = new[]
        {
            new Cuboidf(0, 0, 13f / 16, 1f / 16, 1, 15f / 16),
            new Cuboidf(15f / 16, 0, 13f / 16, 1, 1, 15f / 16),
        };

        Assert.Equal(expected, SidingWallBlock.ComputeCollisionBoxes("wall", "south", "oak", null, (true, false, false, false), FullBoxes), Comparer);
    }

    [Fact]
    public void CorneroutWestPostBoxesAreUnrotated()
    {
        var expected = new[]
        {
            new Cuboidf(1f / 16, 0, 1f / 16, 3f / 16, 1, 3f / 16),
            new Cuboidf(1f / 16, 0, 15f / 16, 3f / 16, 1, 1),
            new Cuboidf(15f / 16, 0, 1f / 16, 1, 1, 3f / 16),
        };

        Assert.Equal(expected, SidingWallBlock.ComputeCollisionBoxes("cornerout", "west", "oak", null, (true, false, false, false), FullBoxes), Comparer);
    }

    [Fact]
    public void CorneroutSouthPostBoxesAreRotated()
    {
        var expected = new[]
        {
            new Cuboidf(1f / 16, 0, 13f / 16, 3f / 16, 1, 15f / 16),
            new Cuboidf(15f / 16, 0, 13f / 16, 1, 1, 15f / 16),
            new Cuboidf(1f / 16, 0, 0, 3f / 16, 1, 1f / 16),
        };

        Assert.Equal(expected, SidingWallBlock.ComputeCollisionBoxes("cornerout", "south", "oak", null, (true, false, false, false), FullBoxes), Comparer);
    }

    [Fact]
    public void UnjoinedWallWestCollidesOnItsTopPlateButNotItsBottomPlate()
    {
        var expected = new[]
        {
            new Cuboidf(1f / 16, 0, 0, 3f / 16, 1, 1f / 16),
            new Cuboidf(1f / 16, 0, 15f / 16, 3f / 16, 1, 1),
            new Cuboidf(1f / 16, 15f / 16, 1f / 16, 3f / 16, 1, 15f / 16),
        };

        Assert.Equal(expected, SidingWallBlock.ComputeCollisionBoxes("wall", "west", "oak", null, (false, false, false, false), FullBoxes), Comparer);
    }

    [Fact]
    public void CrossBeamOnARotatedWallCollides()
    {
        var expected = new[]
        {
            new Cuboidf(0, 0, 13f / 16, 1f / 16, 1, 15f / 16),
            new Cuboidf(15f / 16, 0, 13f / 16, 1, 1, 15f / 16),
            new Cuboidf(1f / 16, 15f / 16, 13f / 16, 15f / 16, 1, 15f / 16),
        };

        Assert.Equal(expected, SidingWallBlock.ComputeCollisionBoxes("wall", "south", "oak", null, (false, false, false, false), FullBoxes), Comparer);
    }

    [Theory]
    [InlineData(null, "oak", null, true)]
    [InlineData("wattle", "pine", "wattle", true)]
    [InlineData(null, "oak", "wattle", false)]
    [InlineData("wattle", "oak", null, false)]
    [InlineData(null, null, null, false)]
    public void OnlyFramedNeighboursWithTheSameInfillStateShareAStack(string? infill, string? neighbourFraming, string? neighbourInfill, bool expected)
    {
        var neighbour = new SidingWallEntity { Framing = neighbourFraming, Infill = neighbourInfill };

        Assert.Equal(expected, SidingWallBlock.SharesStack(infill, neighbour));
    }

    [Fact]
    public void FilledFrameReturnsFullBoxesUnchanged()
    {
        Assert.Same(FullBoxes, SidingWallBlock.ComputeCollisionBoxes("wall", "west", "oak", "wattle", (false, false, false, false), FullBoxes));
    }

    [Fact]
    public void MissingEntityFallsBackToFullBoxes()
    {
        Assert.Same(FullBoxes, SidingWallBlock.ComputeCollisionBoxes("wall", "west", null, null, (false, false, false, false), FullBoxes));
    }
    // RunNeighbours reuses cornerout's table on the claim that the face counter-clockwise from
    // `side` is where the unrotated shape's z = 0 end lands once rotated. That is the whole basis
    // for which neighbour a window merges with, so it gets checked against the rotation itself
    // rather than left to a comment - get it backwards and windows merge with the wrong cell.
    [Fact]
    public void WindowRunLeftIsWhereTheUnrotatedZeroZEndPoints()
    {
        var origin = new Vec3d(0.5, 0.5, 0.5);
        var zeroZEnd = new Cuboidf(0, 0, 0, 1, 1, 1f / 16);

        var rotated = new[] { "west", "south", "east", "north" }.ToDictionary(side => side, side =>
        {
            Cuboidf box = zeroZEnd.RotatedCopy(0, SidingWallEntity.RotationYDeg(side), 0, origin);
            int dx = Math.Sign((box.X1 + box.X2) / 2 - 0.5f), dz = Math.Sign((box.Z1 + box.Z2) / 2 - 0.5f);
            return BlockFacing.HORIZONTALS.First(f => f.Normali.X == dx && f.Normali.Z == dz).Code;
        });

        Assert.Equal(
            rotated,
            new[] { "west", "south", "east", "north" }.ToDictionary(side => side, side => SidingWallBlock.RunNeighbours(side).left.Code));
    }

    // Unrotated, a window's posts sit at the two z ends and its head rail spans between them.
    // Merging drops the post on the merged side only, so a run of unglazed windows is walk-
    // through where the posts were - the collision has to follow what's actually drawn.
    [Fact]
    public void MergedWindowDropsOnlyThePostOnTheMergedSide()
    {
        // "west" is the unrotated case, so these come back exactly as the table lists them.
        string leftPost = Describe(new Cuboidf(1f / 16, 0, 0, 3f / 16, 1, 1f / 16));
        string rightPost = Describe(new Cuboidf(1f / 16, 0, 15f / 16, 3f / 16, 1, 1));
        string headRail = Describe(new Cuboidf(1.25f / 16, 12f / 16, 0, 2.75f / 16, 1, 1));

        Assert.Equal(
            new Dictionary<(bool left, bool right), string[]>
            {
                [(false, false)] = [leftPost, rightPost, headRail],
                [(true, false)] = [rightPost, headRail],
                [(false, true)] = [leftPost, headRail],
                [(true, true)] = [headRail],
            },
            new[] { (false, false), (true, false), (false, true), (true, true) }
                .ToDictionary(m => m, m => SidingWallBlock.ComputeCollisionBoxes(
                    "window", "west", "oak", null, (false, false, m.Item1, m.Item2), FullBoxes)
                    .Select(Describe).ToArray()));
    }

    // A wall never merges sideways, so those flags must not take its posts away.
    [Fact]
    public void WallPostsSurviveTheHorizontalMergeFlags()
    {
        Assert.Equal(
            SidingWallBlock.ComputeCollisionBoxes("wall", "west", "oak", null, (false, false, false, false), FullBoxes),
            SidingWallBlock.ComputeCollisionBoxes("wall", "west", "oak", null, (false, false, true, true), FullBoxes),
            Comparer);
    }

    private static string Describe(Cuboidf b) =>
        $"{b.X1:F4},{b.Y1:F4},{b.Z1:F4}..{b.X2:F4},{b.Y2:F4},{b.Z2:F4}";

}
