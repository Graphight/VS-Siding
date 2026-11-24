using System;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Xunit;

namespace VSSiding.Tests;

// Fixtures are rows of docs/compat/vsroofing-roofs.md: Roofing 2.1.1's boxes in sixteenths, printed to a tenth of a voxel.
public class RoofCutTests
{
    private const string ThatchedOneByOne = "(0,0,8)-(16,8,16) (0,8,0)-(16,16,8)";

    private static Cuboidf[] Boxes(string row) => Regex.Matches(row, @"\(([\d.]+),([\d.]+),([\d.]+)\)-\(([\d.]+),([\d.]+),([\d.]+)\)")
        .Select(match => match.Groups.Values.Skip(1).Select(group => float.Parse(group.Value, CultureInfo.InvariantCulture) / 16).ToArray())
        .Select(v => new Cuboidf(v[0], v[1], v[2], v[3], v[4], v[5]))
        .ToArray();

    private static Cuboidf Voxels(double x1, double y1, double z1, double x2, double y2, double z2)
        => new((float)(x1 / 16), (float)(y1 / 16), (float)(z1 / 16), (float)(x2 / 16), (float)(y2 / 16), (float)(z2 / 16));

    private static HalfSpace[][] Cells(string row, string side) => RoofCut.CellsAt(Boxes(row), side)!;

    [Theory]
    [InlineData("north", "(0,0,8)-(2,8,16) (14,0,8)-(16,8,16) (0,8,0)-(2,16,8) (14,8,0)-(16,16,8)", 0, 16)] // pick 0, frame
    [InlineData("north", ThatchedOneByOne, 0, 16)] // pick 0, thatched
    [InlineData("north", "(0,0,8)-(16,8,16) (0,8,0)-(16,16,8) (0,0,0)-(16,16,16)", 0, 16)] // pick 0, filled selection
    [InlineData("north", "(0,0,8)-(16,2.7,16) (0,2.7,0)-(16,5.3,8)", 0, 5)] // pick 3 then 0
    [InlineData("north", "(0,5.3,8)-(16,8,16) (0,8,0)-(16,10.7,8)", 5, 11)] // pick 3 then 1
    [InlineData("north", "(0,0,13.3)-(16,8,16) (0,8,10.7)-(16,16,13.3)", 0, 48)] // pick 3 then 12
    [InlineData("north", "(0,8,0)-(16,16,8)", 0, 16)] // pick 4, eave
    [InlineData("north", "(0,2.7,0)-(16,5.3,8)", 0, 5)] // pick 7 then 0, eave
    [InlineData("east", "(0,0,0)-(8,8,2) (0,0,14)-(8,8,16) (8,8,0)-(16,16,2) (8,8,14)-(16,16,16)", 0, 16)] // pick 11, frame
    [InlineData("south", "(0,0,0)-(16,8,8) (0,8,8)-(16,16,16)", 0, 16)] // pick 12
    [InlineData("west", "(8,0,0)-(16,8,16) (0,8,0)-(8,16,16)", 0, 16)] // pick 13
    [InlineData("south", "(14,5.3,0)-(16,8,8) (0,5.3,0)-(2,8,8) (14,8,8)-(16,10.7,16) (0,8,8)-(2,10.7,16)", 5, 11)] // 3x1 gable, (0,0,-2)
    public void ThePlaneRunsThroughEveryStepsCorners(string side, string boxes, double downhill, double uphill)
    {
        var plane = RoofCut.PlaneFrom(Boxes(boxes), side)!.Value;
        var up = BlockFacing.FromCode(side).Normalf;

        // In whole voxels at the cell's downhill and uphill edges, mid-cell across the run.
        double At(float toward) => Math.Round(16 * plane.HeightAt(0.5 + toward * up.X / 2, 0.5 + toward * up.Z / 2));
        Assert.Equal((downhill, uphill), (At(-1), At(1)));
    }

    [Theory]
    [InlineData("north", "(0,0,0)-(16,8,16)")] // pick 8, a ridge's slab
    [InlineData("north", "(0,0,0)-(16,16,16)")] // pick 9 then 3, a ridge's full cube
    [InlineData("north", "(0,0,8)-(16,8,16) (0,0,0)-(8,8,16) (8,8,0)-(16,16,8) (8,8,0)-(16,16,8)")] // an outer corner
    [InlineData("north", "(0,0,8)-(8,8,16) (0,0,8)-(8,8,16) (0,8,0)-(16,16,8) (8,8,0)-(16,16,16)")] // an inner corner
    [InlineData("south", ThatchedOneByOne)] // pick 0 read as rising south
    [InlineData("north", "(0,0,8)-(16,8,16) (0,8,0)-(16,10.7,8)")] // steps of two slopes, not a probe row
    public void BoxesThatAreNoStaircaseNameNoPlane(string side, string boxes)
    {
        Assert.Null(RoofCut.PlaneFrom(Boxes(boxes), side));
    }

    // A wall claiming its west face, run along a slope rising north: a gable end.
    [Fact]
    public void AGableWallIsCutIntoStripsAsHighAsTheSlopeAtTheirMiddles()
    {
        var expected = Enumerable.Range(0, RoofCut.Strips).Select(k => Voxels(0, 0, 2 * k, 4, 15 - 2 * k, 2 * k + 2)).ToArray();

        Assert.Equal(expected, RoofCut.ClipBoxes([Voxels(0, 0, 0, 4, 16, 16)], Cells(ThatchedOneByOne, "north")));
    }

    // The same wall across a slope rising east: an eave.
    [Fact]
    public void AnEaveWallIsCutIntoStripsAcrossItsThickness()
    {
        var expected = Enumerable.Range(0, RoofCut.Strips).Select(k => Voxels(k / 2.0, 0, 0, (k + 1) / 2.0, 0.25 + k / 2.0, 16)).ToArray();

        Assert.Equal(expected, RoofCut.ClipBoxes([Voxels(0, 0, 0, 4, 16, 16)], Cells("(0,0,0)-(8,8,16) (8,8,0)-(16,16,16)", "east")));
    }

    [Fact]
    public void ABoxUnderTheSlopeIsKeptAndOneOverItDropped()
    {
        var sill = Voxels(0, 0, 0, 4, 1, 16);
        var plate = Voxels(0, 15, 0, 4, 16, 16);

        Assert.Same(sill, Assert.Single(RoofCut.ClipBoxes([sill, plate], Cells(ThatchedOneByOne, "north"))));
    }

    [Fact]
    public void ACutPanelIsCachedPerPanelArrayAndTheCutsValue()
    {
        Cuboidf[] panel = [Voxels(0, 0, 0, 4, 16, 16)];

        Assert.Same(GapShiftCollisionPatches.UnderRoof(panel, Cells(ThatchedOneByOne, "north")),
            GapShiftCollisionPatches.UnderRoof(panel, Cells(ThatchedOneByOne, "north")));
    }

    private static readonly TextureAtlasPosition Texture = new() { atlasTextureId = 7, x1 = 0, y1 = 0, x2 = 0.5f, y2 = 0.5f };

    // The atlas uvs of a whole 16-texel face of Texture, as the tesselator works them out.
    private static readonly (Vec2f origin, Vec2f span) WholeTexture = (new Vec2f(0, 0.5f), new Vec2f(0.5f, -0.5f));

    // A unit cube's six faces as the tesselator lays out a full-block element's.
    private static MeshData Cube()
    {
        var mesh = new MeshData(24, 36).WithColorMaps().WithRenderpasses();
        foreach (var facing in BlockFacing.ALLFACES)
        {
            ModelCubeUtilExt.AddFace(mesh, facing, new Vec3f(0.5f, 0.5f, 0.5f), new Vec3f(1, 1, 1), WholeTexture.origin, WholeTexture.span,
                Texture.atlasTextureId, -1, ModelCubeUtilExt.EnumShadeMode.On, [0, 0, 0, 0]);
        }

        return mesh;
    }

    // Quads, then indices, then each per-quad array's count, then the area the triangles cover, in square blocks.
    private static (int, int, int, int, int, int, double) Summary(MeshData mesh)
    {
        Vec3d At(int index) => new(mesh.xyz[index * 3], mesh.xyz[index * 3 + 1], mesh.xyz[index * 3 + 2]);
        double area = Enumerable.Range(0, mesh.IndicesCount / 3).Sum(triangle =>
        {
            Vec3d a = At(mesh.Indices[triangle * 3]), b = At(mesh.Indices[triangle * 3 + 1]), c = At(mesh.Indices[triangle * 3 + 2]);
            return (b - a).Cross(c - a).Length() / 2;
        });
        return (mesh.VerticesCount / 4, mesh.IndicesCount, mesh.TextureIndicesCount, mesh.XyzFacesCount, mesh.ColorMapIdsCount, mesh.RenderPassCount,
            Math.Round(area, 4));
    }

    private static Shape OneElementShape(params (string name, double[] from, double[] to)[] elements) => new()
    {
        Elements = elements.Select(element =>
        {
            var shaped = new ShapeElement { Name = element.name, From = element.from, To = element.to };
            shaped.FacesResolved![BlockFacing.UP.Index] = new ShapeElementFace { Texture = "framing", Uv = [0, 0, 16, 16] };
            return shaped;
        }).ToArray(),
    };

    private sealed class OneTexture : ITexPositionSource
    {
        public TextureAtlasPosition this[string textureCode] => Texture;

        public Size2i AtlasSize => new(16, 16);
    }

    // Under y = 1 - z: the north and bottom faces whole, the east and west cut to triangles, the south and top to edges.
    [Fact]
    public void ACubeKeepsOnlyItsFacesPartsUnderTheSlope()
    {
        Assert.Equal((4, 24, 4, 4, 4, 4, 3.0), Summary(RoofCut.ClipMesh(Cube(), Cells(ThatchedOneByOne, "north"))));
    }

    // Each corner of a quad with the quad's lighting face and atlas page.
    private static (float x, float y, float z, float u, float v, byte face, int texture)[] Corners(MeshData mesh, int quad)
        => [.. Enumerable.Range(quad * 4, 4).Select(i => (mesh.xyz[i * 3], mesh.xyz[i * 3 + 1], mesh.xyz[i * 3 + 2], mesh.Uv[i * 2], mesh.Uv[i * 2 + 1],
            mesh.XyzFaces[quad], mesh.TextureIds[mesh.TextureIndices[quad]]))];

    // The north face is the cube's first quad and the first one kept.
    [Fact]
    public void AFaceWhollyUnderTheSlopeKeepsItsCornersAndQuadData()
    {
        var cube = Cube();

        Assert.Equal(Corners(cube, 0), Corners(RoofCut.ClipMesh(cube, Cells(ThatchedOneByOne, "north")), 0));
    }

    // The cap is the element's up face, corners and uvs, raised onto y = 1 - z: a slope one block wide and root two long.
    [Fact]
    public void ACutElementIsClosedByItsUpFaceRaisedOntoTheSlope()
    {
        var expected = Corners(Cube(), BlockFacing.UP.Index).Select(corner => corner with { y = 1 - corner.z });

        var caps = RoofCut.Caps(OneElementShape(("framing", [0, 0, 0], [16, 16, 16])), ["framing"], new Vec3f(), new OneTexture(),
            Cells(ThatchedOneByOne, "north"));

        Assert.Equal((1, 6, 1, 1, 1, 1, Math.Round(Math.Sqrt(2), 4)), Summary(caps));
        Assert.Equal(expected, Corners(caps, 0));
    }

    [Fact]
    public void AHalfHeightElementIsCappedOnlyWhereTheSlopeRunsThroughIt()
    {
        var caps = RoofCut.Caps(OneElementShape(("framing", [0, 0, 0], [16, 8, 16])), ["framing"], new Vec3f(), new OneTexture(),
            Cells(ThatchedOneByOne, "north"));

        Assert.Equal((1, 6, 1, 1, 1, 1, Math.Round(Math.Sqrt(2) / 2, 4)), Summary(caps));
    }

    // A sill under the slope, a plate over it, and a cube the selection leaves out.
    [Fact]
    public void NoCapGoesOnAnElementTheSlopeMissesOrTheSelectionLeavesOut()
    {
        var shape = OneElementShape(("sill", [0, 0, 0], [16, 8, 8]), ("plate", [0, 15, 8], [16, 16, 16]), ("infill", [0, 0, 0], [16, 16, 16]));

        Assert.Equal((0, 0, 0, 0, 0, 0, 0.0),
            Summary(RoofCut.Caps(shape, ["sill", "plate"], new Vec3f(), new OneTexture(), Cells(ThatchedOneByOne, "north"))));
    }
}
