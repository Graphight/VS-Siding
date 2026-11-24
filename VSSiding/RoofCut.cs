using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace VSSiding;

// Nx x + Ny y + Nz z <= D, in block units: the side of a plane a roof-hosted wall keeps.
internal readonly record struct HalfSpace(double Nx, double Ny, double Nz, double D)
{
    // An upright half-space, the split between a valley's two cells, bounds where a cell stands rather than how high it reaches.
    internal double HeightAt(double x, double z) => Ny != 0 ? (D - Nx * x - Nz * z) / Ny
        : Nx * x + Nz * z <= D + RoofCut.Epsilon ? double.PositiveInfinity : double.NegativeInfinity;

    // The same half-space in the coordinates of the cell (dx, dy, dz) blocks from this one's.
    internal HalfSpace SeenFrom(int dx, int dy, int dz) => this with { D = D - Nx * dx - Ny * dy - Nz * dz };
}

// A mesh corner as a cut carries it, its colour packed as MeshData.Rgba lays out one vertex's bytes.
internal readonly record struct Corner(float X, float Y, float Z, float U, float V, int Rgba, int Flags)
{
    internal double Past(HalfSpace half) => half.Nx * X + half.Ny * Y + half.Nz * Z - half.D;

    internal Corner Toward(Corner other, double t, int flags) => new(
        (float)(X + (other.X - X) * t), (float)(Y + (other.Y - Y) * t), (float)(Z + (other.Z - Z) * t),
        (float)(U + (other.U - U) * t), (float)(V + (other.V - V) * t), LerpBytes(Rgba, other.Rgba, t), flags);

    private static int LerpBytes(int a, int b, double t)
    {
        int lerped = 0;
        for (int shift = 0; shift < 32; shift += 8)
        {
            int from = (a >> shift) & 0xFF, to = (b >> shift) & 0xFF;
            lerped |= (int)Math.Round(from + (to - from) * t) << shift;
        }

        return lerped;
    }
}

// What the chunk tesselator reads per quad rather than per vertex.
internal readonly record struct FaceData(int TextureId, byte XyzFace, byte Climate, byte Season, short RenderPass);

// A Roofing roof spans the cell of the wall it hosts, so the wall keeps only the part under the roof's slope. The slope comes
// from the roof's own boxes, a staircase whose step corners lie on it, and the part kept is a union of convex cells, each the
// half-spaces it lies inside.
internal static class RoofCut
{
    internal const double Epsilon = 1e-4;

    // A quarter voxel off the line still counts as on it: the probe prints a step's thirds of a block to a tenth of a voxel.
    private const double OnTheLine = 1.0 / 64;

    internal const int Strips = 8;

    internal static readonly IEqualityComparer<HalfSpace[][]> CellsComparer = new ByValue();

    // The one slope rising toward side that every box steps up, or null. A box spanning the run is no step of it (a ridge's slab,
    // a corner's).
    internal static HalfSpace? PlaneFrom(Cuboidf[]? boxes, string? side)
    {
        if (boxes == null || side == null || BlockFacing.FromCode(side) is not { IsHorizontal: true } facing) return null;
        return boxes.Any(box => Spans(box, facing)) ? null : Slope(boxes, facing);
    }

    // The slope through the lowest step's low corner and the highest step's high corner, rising toward facing; null unless every
    // step's corners lie on it.
    private static HalfSpace? Slope(IReadOnlyCollection<Cuboidf> steps, BlockFacing facing)
    {
        bool alongX = facing.Axis == EnumAxis.X;
        bool risesPositive = facing.Normali.X + facing.Normali.Z > 0;

        (double u, double y)? low = null, high = null;
        foreach (var box in steps)
        {
            var (boxLow, boxHigh) = Corners(box, alongX, risesPositive);
            if (low == null || boxLow.y < low.Value.y) low = boxLow;
            if (high == null || boxHigh.y > high.Value.y) high = boxHigh;
        }

        if (low is not { } l || high is not { } h || Math.Abs(h.u - l.u) < Epsilon) return null;
        double rise = (h.y - l.y) / (h.u - l.u), at = l.y - rise * l.u, scale = Math.Sqrt(1 + rise * rise);
        bool OnLine((double u, double y) corner) => Math.Abs(at + rise * corner.u - corner.y) / scale <= OnTheLine;

        foreach (var box in steps)
        {
            var (boxLow, boxHigh) = Corners(box, alongX, risesPositive);
            if (!OnLine(boxLow) || !OnLine(boxHigh)) return null;
        }

        return alongX ? new HalfSpace(-rise, 1, 0, at) : new HalfSpace(0, 1, -rise, at);
    }

    // A corner piece's boxes are two slopes' steps, one rising toward side and one across it, each step kept where its slope is
    // the lower (an outer corner, a hip) or the higher (an inner corner, a valley), so a step spanning one slope's run is the
    // other's. Only a valley's boxes reach the higher plane everywhere. A hip keeps the one cell under both planes, a valley two,
    // split upright where the planes cross, so the split takes no cap.
    private static HalfSpace[][]? CornerFrom(Cuboidf[]? boxes, string? side)
    {
        if (boxes == null || side == null || BlockFacing.FromCode(side) is not { IsHorizontal: true } facing) return null;
        if (boxes.Any(box => Spans(box, facing) && Spans(box, facing.GetCW()))
            || Slope(boxes.Where(box => !Spans(box, facing)).ToArray(), facing) is not { } rising) return null;

        foreach (var across in new[] { facing.GetCW(), facing.GetCCW() })
        {
            if (Slope(boxes.Where(box => !Spans(box, across)).ToArray(), across) is not { } other) continue;
            if (Reaches(boxes, (x, z) => Math.Max(rising.HeightAt(x, z), other.HeightAt(x, z))))
                return [[rising, Over(rising, other)], [other, Over(other, rising)]];
            if (Reaches(boxes, (x, z) => Math.Min(rising.HeightAt(x, z), other.HeightAt(x, z)))) return [[rising, other]];
        }

        return null;
    }

    // Whether the boxes' tops reach the height over every strip's middle both ways across the cell.
    private static bool Reaches(Cuboidf[] steps, System.Func<double, double, double> height)
    {
        for (int i = 0; i < Strips; i++)
        {
            for (int j = 0; j < Strips; j++)
            {
                double x = (i + 0.5) / Strips, z = (j + 0.5) / Strips;
                if (!steps.Any(box => box.X1 < x && x < box.X2 && box.Z1 < z && z < box.Z2 && box.Y2 >= height(x, z) - OnTheLine)) return false;
            }
        }

        return true;
    }

    // Where a's plane is at or over b's, both planes as Slope makes them.
    private static HalfSpace Over(HalfSpace a, HalfSpace b) => new(a.Nx - b.Nx, 0, a.Nz - b.Nz, a.D - b.D);

    // The convex cells a wall in the roof's cell keeps, or null where the roof's boxes name no slope Siding reads. A ridge is a
    // flat slab one level over the slopes either side, and the cell under it holds no roof, so under a ridge the wall keeps the
    // one cell under every slope a step down and over that rises toward it, slopeBelow answering that roof's boxes and side.
    internal static HalfSpace[][]? CellsAt(Cuboidf[]? boxes, string? side, System.Func<BlockFacing, (Cuboidf[]? boxes, string? side)>? slopeBelow = null)
    {
        if (PlaneFrom(boxes, side) is { } plane) return [[plane]];
        if (CornerFrom(boxes, side) is { } corner) return corner;
        if (slopeBelow == null || boxes is not { Length: > 0 } || !boxes.All(IsSlab)) return null;

        var planes = new List<HalfSpace>();
        foreach (var facing in BlockFacing.HORIZONTALS)
        {
            var (belowBoxes, belowSide) = slopeBelow(facing);
            if (belowSide == facing.Opposite.Code && PlaneFrom(belowBoxes, belowSide) is { } below)
                planes.Add(below.SeenFrom(-facing.Normali.X, 1, -facing.Normali.Z));
        }

        return planes.Count > 0 ? [[.. planes]] : null;
    }

    // Each box cut into Strips strips along the way the cut falls further across it (the slope's run, or under a corner either),
    // each as high as the cut at its middle, so its cut end lies either side of the slope by half a strip's rise, as decision
    // 0067's deck strips straddle the panel's line. A box wholly under the cut is kept as it is, and a strip wholly over it is
    // dropped. shortcut: a strip is read at the box's middle across it, so under a corner a thick box's top is off by the cut's
    // fall across it; cut such a box both ways if that shows in game.
    internal static Cuboidf[] ClipBoxes(Cuboidf[] boxes, HalfSpace[][] cells)
    {
        var clipped = new List<Cuboidf>();
        foreach (var box in boxes)
        {
            float[] byX = Tops(box, cells, alongX: true), byZ = Tops(box, cells, alongX: false);
            bool alongX = byX.Max() - byX.Min() > byZ.Max() - byZ.Min();
            float[] tops = alongX ? byX : byZ;
            double lo = alongX ? box.X1 : box.Z1, hi = alongX ? box.X2 : box.Z2;

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

    // The box's top in each of Strips strips along one way, read at their middles and at the box's middle the other way.
    private static float[] Tops(Cuboidf box, HalfSpace[][] cells, bool alongX)
    {
        double lo = alongX ? box.X1 : box.Z1, hi = alongX ? box.X2 : box.Z2;
        double across = alongX ? (box.Z1 + box.Z2) / 2.0 : (box.X1 + box.X2) / 2.0;
        var tops = new float[Strips];
        for (int k = 0; k < Strips; k++)
        {
            double middle = lo + (hi - lo) * (k + 0.5) / Strips;
            tops[k] = (float)Math.Min(box.Y2, alongX ? Height(cells, middle, across) : Height(cells, across, middle));
        }

        return tops;
    }

    // The cut's height over (x, z): the highest of its cells, each as high as the lowest of its planes.
    internal static double Height(HalfSpace[][] cells, double x, double z)
        => cells.Max(cell => cell.Min(half => half.HeightAt(x, z)));

    // The lowest the cut reaches along a side face's edge of the cell, between from and to along the face, held to the cell:
    // as high as the face dams liquid. shortcut: reads the span's ends only. A cell's cut, the lowest of its planes, is lowest at
    // an end; a valley's, the higher of two, is too while one of the two is level along the face, as on every side face of a
    // Roofing valley; read the planes' crossings too if a cut ever dips mid-span.
    internal static double FaceHeight(HalfSpace[][] cells, BlockFacing face, double from = 0, double to = 1)
    {
        double edge = face.Normali.X + face.Normali.Z > 0 ? 1 : 0;
        double At(double along) => face.Axis == EnumAxis.Z ? Height(cells, along, edge) : Height(cells, edge, along);
        double height = Math.Min(At(from), At(to));
        return height >= 1 - Epsilon ? 1 : Math.Max(0, height);
    }

    // Whether an attachment area on a side face, in inclusive voxels as vanilla passes one (the face's own axis unused), lies
    // under the cut. No area is the whole face.
    internal static bool Holds(HalfSpace[][] cells, BlockFacing face, Cuboidi? area)
    {
        if (area == null) return FaceHeight(cells, face) >= 1;
        var (from, to) = face.Axis == EnumAxis.Z ? (area.X1, area.X2) : (area.Z1, area.Z2);
        return FaceHeight(cells, face, from / 16.0, (to + 1) / 16.0) >= Math.Min(1, (area.Y2 + 1) / 16.0) - Epsilon;
    }

    // Every mesh built under one roof is cached under the cut's value as well as the wall's state.
    internal static string Key(HalfSpace[][] cells)
        => string.Join("/", cells.Select(cell => string.Join(";", cell.Select(half
            => string.Create(CultureInfo.InvariantCulture, $"{half.Nx:R},{half.Ny:R},{half.Nz:R},{half.D:R}")))));

    // A new mesh of each quad's part inside the cells. The chunk tesselator reads a mesh four corners and six indices per quad,
    // so a face's cut part, a convex polygon, goes back in as quads fanned from its first corner, the last repeating a corner
    // where the polygon's count is odd.
    internal static MeshData ClipMesh(MeshData mesh, HalfSpace[][] cells)
    {
        var clipped = NewMesh(mesh.VerticesCount);
        var quad = new Corner[4];
        for (int face = 0; face < mesh.VerticesCount / 4; face++)
        {
            for (int i = 0; i < 4; i++) quad[i] = CornerAt(mesh, face * 4 + i);
            var data = new FaceData(mesh.TextureIds[mesh.TextureIndices[face]], mesh.XyzFaces[face],
                mesh.ClimateColorMapIds[face], mesh.SeasonColorMapIds[face], mesh.RenderPassesAndExtraBits[face]);
            foreach (var cell in cells)
            {
                // The cells only touch, so a face wholly in one has nothing in another.
                if (quad.All(corner => cell.All(half => corner.Past(half) <= Epsilon)))
                {
                    Emit(clipped, quad, data);
                    break;
                }

                Emit(clipped, cell.Aggregate<HalfSpace, IReadOnlyList<Corner>>(quad, Clip), data);
            }
        }

        return clipped;
    }

    // Each selected element's footprint raised onto every roof plane of a cell, where that plane runs through the element's
    // box: the closed sloped top of the part ClipMesh keeps. It takes the element's up face's texture and uvs, or its first
    // face's where another element sits on it, as the tesselator would lay them out, turned with the mesh. A turned element,
    // a diagonal's, gets none: a diagonal hosts nothing.
    internal static MeshData Caps(Shape shape, string[] selectiveElements, Vec3f rotation, ITexPositionSource texSource, HalfSpace[][] cells)
    {
        var caps = NewMesh(16);
        var selected = selectiveElements.ToHashSet();
        foreach (var element in shape.Elements)
        {
            if (!selected.Contains(element.Name ?? "") || element.From is not { } from || element.To is not { } to
                || element.RotationX != 0 || element.RotationY != 0 || element.RotationZ != 0) continue;
            if (element.FacesResolved is not { } faces || (faces[BlockFacing.UP.Index] ?? faces.FirstOrDefault(face => face != null)) is not { } top
                || texSource[top.Texture] is not { } texture) continue;

            Corner[] footprint = Footprint(shape, from, to, element, top, texture, rotation);
            var slab = new[] { new HalfSpace(0, 1, 0, to[1] / 16), new HalfSpace(0, -1, 0, -from[1] / 16) };
            var data = new FaceData(texture.atlasTextureId, BlockFacing.UP.MeshDataIndex, 0, 0,
                (short)(element.RenderPass + (element.DisableRandomDrawOffset ? 1024 : 0)));
            foreach (var cell in cells)
            {
                foreach (var plane in cell.Where(half => half.Ny > Epsilon))
                {
                    double length = Math.Sqrt(plane.Nx * plane.Nx + plane.Ny * plane.Ny + plane.Nz * plane.Nz);
                    int normal = VertexFlags.PackNormal(plane.Nx / length, plane.Ny / length, plane.Nz / length);
                    IReadOnlyList<Corner> raised = Array.ConvertAll(footprint, corner
                        => corner with { Y = (float)plane.HeightAt(corner.X, corner.Z), Flags = (corner.Flags & VertexFlags.ClearNormalBitMask) | normal });
                    Emit(caps, cell.Where(half => half != plane).Concat(slab).Aggregate(raised, Clip), data);
                }
            }
        }

        return caps;
    }

    // The face as ShapeTesselator.TesselateShapeElement lays out an up face, uvs and all, then turned as TesselateShape turns
    // the whole mesh.
    private static Corner[] Footprint(
        Shape shape, double[] from, double[] to, ShapeElement element, ShapeElementFace top, TextureAtlasPosition texture, Vec3f rotation)
    {
        int[] texels = shape.TextureSizes.TryGetValue(top.Texture, out var sized) ? sized : [shape.TextureWidth, shape.TextureHeight];
        float perU = (texture.x2 - texture.x1) / texels[0], perV = (texture.y2 - texture.y1) / texels[1];
        float[] uv = top.Uv ?? [0, 0, (float)(to[0] - from[0]), (float)(to[2] - from[2])];
        var origin = new Vec2f(texture.x1 + uv[0] * perU, texture.y1 + uv[3] * perV);
        var span = new Vec2f((uv[2] - uv[0]) * perU, (uv[1] - uv[3]) * perV);
        span.X -= Math.Max(0, origin.X + span.X - texture.x2);
        span.Y -= Math.Max(0, origin.Y + span.Y - texture.y2);
        int light = ((element.ZOffset & 7) << 8) | top.Glow;

        var quad = new MeshData(4, 6);
        ModelCubeUtilExt.AddFace(quad, BlockFacing.UP,
            new Vec3f((float)(from[0] + to[0]) / 32, (float)(from[1] + to[1]) / 32, (float)(from[2] + to[2]) / 32),
            new Vec3f((float)(to[0] - from[0]) / 16, (float)(to[1] - from[1]) / 16, (float)(to[2] - from[2]) / 16),
            origin, span, texture.atlasTextureId, element.Color, ModelCubeUtilExt.EnumShadeMode.On, [light, light, light, light],
            uvRotation: (int)(top.Rotation / 90) % 4);
        if (rotation.X != 0 || rotation.Y != 0 || rotation.Z != 0)
            quad.Rotate(new Vec3f(0.5f, 0.5f, 0.5f), rotation.X * GameMath.DEG2RAD, rotation.Y * GameMath.DEG2RAD, rotation.Z * GameMath.DEG2RAD);
        return [.. Enumerable.Range(0, 4).Select(i => CornerAt(quad, i))];
    }

    private static MeshData NewMesh(int vertices)
        => new MeshData(Math.Max(vertices, 4), Math.Max(vertices, 4) * 6 / 4).WithColorMaps().WithRenderpasses().WithXyzFaces();

    private static Corner CornerAt(MeshData mesh, int vertex) => new(
        mesh.xyz[vertex * 3], mesh.xyz[vertex * 3 + 1], mesh.xyz[vertex * 3 + 2], mesh.Uv[vertex * 2], mesh.Uv[vertex * 2 + 1],
        BitConverter.ToInt32(mesh.Rgba, vertex * 4), mesh.Flags?[vertex] ?? 0);

    // Sutherland-Hodgman: the polygon's part on the kept side, a crossing corner taking the flags of the corner kept. A corner
    // within Epsilon of the plane is kept and is its edges' crossing, so only an edge from one side clear to the other adds one.
    private static IReadOnlyList<Corner> Clip(IReadOnlyList<Corner> polygon, HalfSpace half)
    {
        var kept = new List<Corner>(polygon.Count + 1);
        for (int i = 0; i < polygon.Count; i++)
        {
            Corner a = polygon[i], b = polygon[(i + 1) % polygon.Count];
            double pastA = a.Past(half), pastB = b.Past(half);
            if (pastA <= Epsilon) kept.Add(a);
            if (Math.Min(pastA, pastB) < -Epsilon && Math.Max(pastA, pastB) > Epsilon)
                kept.Add(a.Toward(b, pastA / (pastA - pastB), pastA < 0 ? a.Flags : b.Flags));
        }

        return kept;
    }

    // A polygon cut down to an edge or a point draws nothing.
    private const double Sliver = 1e-6;

    private static void Emit(MeshData mesh, IReadOnlyList<Corner> polygon, FaceData face)
    {
        if (polygon.Count < 3 || Area(polygon) < Sliver) return;
        for (int i = 1; i < polygon.Count - 1; i += 2)
        {
            int first = mesh.VerticesCount;
            foreach (var corner in new[] { polygon[0], polygon[i], polygon[i + 1], polygon[Math.Min(i + 2, polygon.Count - 1)] })
                mesh.AddWithFlagsVertex(corner.X, corner.Y, corner.Z, corner.U, corner.V, corner.Rgba, corner.Flags);
            mesh.AddQuadIndices(first);
            mesh.AddTextureId(face.TextureId);
            mesh.AddXyzFace(face.XyzFace);
            mesh.AddColorMapIndex(face.Climate, face.Season);
            mesh.AddRenderPass(face.RenderPass);
        }
    }

    // Newell's method: half the length of the summed edge cross products.
    private static double Area(IReadOnlyList<Corner> polygon)
    {
        double x = 0, y = 0, z = 0;
        for (int i = 0; i < polygon.Count; i++)
        {
            Corner a = polygon[i], b = polygon[(i + 1) % polygon.Count];
            x += (a.Y - b.Y) * (a.Z + b.Z);
            y += (a.Z - b.Z) * (a.X + b.X);
            z += (a.X - b.X) * (a.Y + b.Y);
        }

        return Math.Sqrt(x * x + y * y + z * z) / 2;
    }

    // Whether the box runs the cell's whole width along facing's axis.
    private static bool Spans(Cuboidf box, BlockFacing facing)
    {
        var (u1, u2) = facing.Axis == EnumAxis.X ? (box.X1, box.X2) : (box.Z1, box.Z2);
        return u1 <= Epsilon && u2 >= 1 - Epsilon;
    }

    // A box over the cell's whole floor, from its bottom up.
    private static bool IsSlab(Cuboidf box)
        => box.X1 <= Epsilon && box.Y1 <= Epsilon && box.Z1 <= Epsilon && box.X2 >= 1 - Epsilon && box.Z2 >= 1 - Epsilon;

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
