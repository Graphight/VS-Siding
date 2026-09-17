using System.Collections.Generic;
using Vintagestory.API.MathTools;
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
        // The static collision slab from wall.json, x 0..0.25 z 0..1, rotated for "south"
        // must land on z 0.75..1 - the reference check from the framing-only-collision plan.
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

        Assert.Equal(expected, SidingWallBlock.ComputeCollisionBoxes("wall", "west", "oak", null, FullBoxes), Comparer);
    }

    [Fact]
    public void WallSouthPostBoxesAreRotated()
    {
        var expected = new[]
        {
            new Cuboidf(0, 0, 13f / 16, 1f / 16, 1, 15f / 16),
            new Cuboidf(15f / 16, 0, 13f / 16, 1, 1, 15f / 16),
        };

        Assert.Equal(expected, SidingWallBlock.ComputeCollisionBoxes("wall", "south", "oak", null, FullBoxes), Comparer);
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

        Assert.Equal(expected, SidingWallBlock.ComputeCollisionBoxes("cornerout", "west", "oak", null, FullBoxes), Comparer);
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

        Assert.Equal(expected, SidingWallBlock.ComputeCollisionBoxes("cornerout", "south", "oak", null, FullBoxes), Comparer);
    }

    [Fact]
    public void FilledFrameReturnsFullBoxesUnchanged()
    {
        Assert.Same(FullBoxes, SidingWallBlock.ComputeCollisionBoxes("wall", "west", "oak", "wattle", FullBoxes));
    }

    [Fact]
    public void MissingEntityFallsBackToFullBoxes()
    {
        Assert.Same(FullBoxes, SidingWallBlock.ComputeCollisionBoxes("wall", "west", null, null, FullBoxes));
    }
}
