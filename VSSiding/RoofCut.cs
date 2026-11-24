using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.MathTools;

namespace VSSiding;

// Nx x + Ny y + Nz z <= D, in block units: the side of a plane a roof-hosted wall keeps.
internal readonly record struct HalfSpace(double Nx, double Ny, double Nz, double D)
{
    internal double HeightAt(double x, double z) => (D - Nx * x - Nz * z) / Ny;
}

// A Roofing roof spans the cell of the wall it hosts, so the wall keeps only the part under the roof's slope. The slope comes
// from the roof's own boxes, a staircase whose step corners lie on it, and the part kept is a union of convex cells, each the
// half-spaces it lies inside.
internal static class RoofCut
{
    private const double Epsilon = 1e-4;

    // A quarter voxel off the line still counts as on it: the probe prints a step's thirds of a block to a tenth of a voxel.
    private const double OnTheLine = 1.0 / 64;

    internal const int Strips = 8;

    internal static readonly IEqualityComparer<HalfSpace[][]> CellsComparer = new ByValue();

    // The slope through the lowest step's low corner and the highest step's high corner, rising toward side; null unless every
    // step's corners lie on it. A box spanning the run is no step (a ridge's slab, a corner's), and the full cube Roofing's base
    // fill adds to the selection boxes is skipped.
    internal static HalfSpace? PlaneFrom(Cuboidf[]? boxes, string? side)
    {
        if (boxes == null || side == null || BlockFacing.FromCode(side) is not { IsHorizontal: true } facing) return null;
        bool alongX = facing.Axis == EnumAxis.X;
        bool risesPositive = facing.Normali.X + facing.Normali.Z > 0;

        (double u, double y)? low = null, high = null;
        foreach (var box in boxes)
        {
            if (IsFullCube(box)) continue;
            var (u1, u2) = alongX ? (box.X1, box.X2) : (box.Z1, box.Z2);
            if (u1 <= Epsilon && u2 >= 1 - Epsilon) return null;
            var (boxLow, boxHigh) = Corners(box, alongX, risesPositive);
            if (low == null || boxLow.y < low.Value.y) low = boxLow;
            if (high == null || boxHigh.y > high.Value.y) high = boxHigh;
        }

        if (low is not { } l || high is not { } h || Math.Abs(h.u - l.u) < Epsilon) return null;
        double rise = (h.y - l.y) / (h.u - l.u), at = l.y - rise * l.u, scale = Math.Sqrt(1 + rise * rise);
        bool OnLine((double u, double y) corner) => Math.Abs(at + rise * corner.u - corner.y) / scale <= OnTheLine;

        foreach (var box in boxes)
        {
            if (IsFullCube(box)) continue;
            var (boxLow, boxHigh) = Corners(box, alongX, risesPositive);
            if (!OnLine(boxLow) || !OnLine(boxHigh)) return null;
        }

        return alongX ? new HalfSpace(-rise, 1, 0, at) : new HalfSpace(0, 1, -rise, at);
    }

    // The convex cells a wall in the roof's cell keeps, or null where the roof's boxes name no slope Siding reads.
    internal static HalfSpace[][]? CellsAt(Cuboidf[]? boxes, string? side)
        => PlaneFrom(boxes, side) is { } plane ? [[plane]] : null;

    // Each box cut into Strips strips along the slope's run, each as high as the cut at its middle, so its cut end lies either
    // side of the slope by half a strip's rise, as decision 0067's deck strips straddle the panel's line. A box wholly under the
    // cut is kept as it is, and a strip wholly over it is dropped.
    internal static Cuboidf[] ClipBoxes(Cuboidf[] boxes, HalfSpace[][] cells)
    {
        bool alongX = cells.Any(cell => cell.Any(half => Math.Abs(half.Nx) > Epsilon));
        var clipped = new List<Cuboidf>();
        var tops = new float[Strips];
        foreach (var box in boxes)
        {
            double lo = alongX ? box.X1 : box.Z1, hi = alongX ? box.X2 : box.Z2;
            double across = alongX ? (box.Z1 + box.Z2) / 2.0 : (box.X1 + box.X2) / 2.0;
            for (int k = 0; k < Strips; k++)
            {
                double middle = lo + (hi - lo) * (k + 0.5) / Strips;
                tops[k] = (float)Math.Min(box.Y2, alongX ? Height(cells, middle, across) : Height(cells, across, middle));
            }

            if (tops.All(top => top >= box.Y2))
            {
                clipped.Add(box);
                continue;
            }

            for (int k = 0; k < Strips; k++)
            {
                if (tops[k] <= box.Y1 + Epsilon) continue;
                float u1 = (float)(lo + (hi - lo) * k / Strips), u2 = (float)(lo + (hi - lo) * (k + 1) / Strips);
                clipped.Add(alongX
                    ? new Cuboidf(u1, box.Y1, box.Z1, u2, tops[k], box.Z2)
                    : new Cuboidf(box.X1, box.Y1, u1, box.X2, tops[k], u2));
            }
        }

        return clipped.ToArray();
    }

    // The cut's height over (x, z): the highest of its cells, each as high as the lowest of its planes.
    internal static double Height(HalfSpace[][] cells, double x, double z)
        => cells.Max(cell => cell.Min(half => half.HeightAt(x, z)));

    private static bool IsFullCube(Cuboidf box)
        => box.X1 <= Epsilon && box.Y1 <= Epsilon && box.Z1 <= Epsilon && box.X2 >= 1 - Epsilon && box.Y2 >= 1 - Epsilon && box.Z2 >= 1 - Epsilon;

    // A step's foot on its downhill edge and its top on its uphill edge, as (along the run, height).
    private static ((double u, double y) low, (double u, double y) high) Corners(Cuboidf box, bool alongX, bool risesPositive)
    {
        var (u1, u2) = alongX ? (box.X1, box.X2) : (box.Z1, box.Z2);
        return risesPositive ? ((u1, box.Y1), (u2, box.Y2)) : ((u2, box.Y1), (u1, box.Y2));
    }

    // Two cuts built from the same boxes are equal arrays, not the same one.
    private sealed class ByValue : IEqualityComparer<HalfSpace[][]>
    {
        public bool Equals(HalfSpace[][]? a, HalfSpace[][]? b)
        {
            if (a == null || b == null || a.Length != b.Length) return a == b;
            for (int i = 0; i < a.Length; i++)
            {
                if (!a[i].AsSpan().SequenceEqual(b[i])) return false;
            }

            return true;
        }

        public int GetHashCode(HalfSpace[][] cells)
        {
            var hash = new HashCode();
            foreach (var cell in cells)
            {
                foreach (var half in cell) hash.Add(half);
            }

            return hash.ToHashCode();
        }
    }
}
