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

    // A ridge's cut, a Gables row, with the roofs a step down and over to its north and south as the cells beside it.
    private static HalfSpace[][]? UnderRidge(string ridge, (string boxes, string side)? north, (string boxes, string side)? south)
        => RoofCut.CellsAt(Boxes(ridge), "north", facing => (facing == BlockFacing.NORTH ? north : facing == BlockFacing.SOUTH ? south : null) is { } roof
            ? (Boxes(roof.boxes), roof.side)
            : (null, null));

    private const string OneByOneRisingSouth = "(14,0,0)-(16,8,8) (0,0,0)-(2,8,8) (14,8,8)-(16,16,16) (0,8,8)-(2,16,16)";
    private const string OneByOneRisingNorth = "(0,0,8)-(2,8,16) (14,0,8)-(16,8,16) (0,8,0)-(2,16,8) (14,8,0)-(16,16,8)";

    // In whole voxels mid-cell across the run, at the ridge cell's north edge, a quarter in, the middle, three quarters in and
    // its south edge, not held to the cell.
    [Theory]
    [InlineData("(0,0,0)-(16,2.7,16)", "(14,10.7,0)-(16,13.3,8) (0,10.7,0)-(2,13.3,8) (14,13.3,8)-(16,16,16) (0,13.3,8)-(2,16,16)",
        "(0,10.7,8)-(2,13.3,16) (14,10.7,8)-(16,13.3,16) (0,13.3,0)-(2,16,8) (14,13.3,0)-(16,16,8)", 0, 1, 3, 1, 0)] // 3x1
    [InlineData("(0,0,0)-(16,8,16)", OneByOneRisingSouth, OneByOneRisingNorth, 0, 4, 8, 4, 0)] // 1x1
    [InlineData("(0,0,0)-(16,16,16)", "(14,0,10.7)-(16,8,13.3) (0,0,10.7)-(2,8,13.3) (14,8,13.3)-(16,16,16) (0,8,13.3)-(2,16,16)",
        "(0,0,2.7)-(2,8,5.3) (14,0,2.7)-(16,8,5.3) (0,8,0)-(2,16,2.7) (14,8,0)-(16,16,2.7)", 0, 12, 24, 12, 0)] // 1x3
    public void AWallUnderARidgeIsCutToThePeakOfTheSlopesEitherSide(string ridge, string north, string south,
        double edge, double quarter, double middle, double threeQuarters, double farEdge)
    {
        var cells = UnderRidge(ridge, (north, "south"), (south, "north"))!;

        Assert.Equal(new[] { edge, quarter, middle, threeQuarters, farEdge },
            new[] { 0, 0.25, 0.5, 0.75, 1 }.Select(z => Math.Round(16 * RoofCut.Height(cells, 0.5, z))));
    }

    // A gable end under the 1x1 ridge: 8 strips rising to the peak and falling again.
    [Fact]
    public void AGableWallUnderARidgeIsCutIntoStripsUpToItsPeak()
    {
        var expected = Enumerable.Range(0, RoofCut.Strips).Select(k => Voxels(0, 0, 2 * k, 4, k < 4 ? 2 * k + 1 : 15 - 2 * k, 2 * k + 2)).ToArray();

        Assert.Equal(expected, RoofCut.ClipBoxes([Voxels(0, 0, 0, 4, 16, 16)],
            UnderRidge("(0,0,0)-(16,8,16)", (OneByOneRisingSouth, "south"), (OneByOneRisingNorth, "north"))!));
    }

    // To the north a roof rising north, away from the ridge; to the south one rising toward it, y = 1 - z in the ridge's cell.
    [Fact]
    public void ARidgeIsCutOnlyBySlopesRisingTowardIt()
    {
        Assert.Equal(new HalfSpace[][] { [new HalfSpace(0, 1, 1, 1)] },
            UnderRidge("(0,0,0)-(16,8,16)", (ThatchedOneByOne, "north"), (OneByOneRisingNorth, "north"))!, RoofCut.CellsComparer);
    }

    [Fact]
    public void ARidgeWithNoSlopeBesideItIsNotCut()
    {
        Assert.Null(UnderRidge("(0,0,0)-(16,8,16)", null, null));
    }

    // The Corners rows' (0,0,0) cells, a roof rising north, y = 1 - z, met by one rising east, y = x.
    private const string OuterCorner = "(0,0,8)-(16,8,16) (0,0,0)-(8,8,16) (8,8,0)-(16,16,8) (8,8,0)-(16,16,8)";
    private const string InnerCorner = "(0,0,8)-(8,8,16) (0,0,8)-(8,8,16) (0,8,0)-(16,16,8) (8,8,0)-(16,16,16)";

    // A hip: the one cell under both slopes.
    [Fact]
    public void AnOuterCornerKeepsWhatLiesUnderBothSlopes()
    {
        Assert.Equal(new HalfSpace[][] { [new(0, 1, 1, 1), new(-1, 1, 0, 0)] }, Cells(OuterCorner, "north"), RoofCut.CellsComparer);
    }

    // A valley: under either slope, as two cells split upright along x + z = 1, where the slopes cross.
    [Fact]
    public void AnInnerCornerKeepsWhatLiesUnderEitherSlopeSplitWhereTheyCross()
    {
        Assert.Equal(new HalfSpace[][] { [new(0, 1, 1, 1), new(1, 0, 1, 1)], [new(-1, 1, 0, 0), new(-1, 0, -1, -1)] },
            Cells(InnerCorner, "north"), RoofCut.CellsComparer);
    }

    // A corner wall claiming its west and north faces, cornerout-west's two boxes. Under the hip both legs are stripped along x,
    // the west one rising x and the north one x up to 1 - z mid-leg; under the valley both along z, the west one falling 1 - z
    // down to x mid-leg and the north one 1 - z.
    [Fact]
    public void ACornerWallUnderAnOuterCornerIsCutToTheLowerSlope()
    {
        var expected = Enumerable.Range(0, RoofCut.Strips).Select(k => Voxels(k / 2.0, 0, 0, (k + 1) / 2.0, 0.25 + k / 2.0, 16))
            .Concat(Enumerable.Range(0, RoofCut.Strips).Select(k => Voxels(4 + 1.5 * k, 0, 0, 5.5 + 1.5 * k, Math.Min(14, 4.75 + 1.5 * k), 4)))
            .ToArray();

        Assert.Equal(expected, RoofCut.ClipBoxes([Voxels(0, 0, 0, 4, 16, 16), Voxels(4, 0, 0, 16, 16, 4)], Cells(OuterCorner, "north")));
    }

    [Fact]
    public void ACornerWallUnderAnInnerCornerIsCutToTheHigherSlope()
    {
        var expected = Enumerable.Range(0, RoofCut.Strips).Select(k => Voxels(0, 0, 2 * k, 4, Math.Max(15 - 2 * k, 2), 2 * k + 2))
            .Concat(Enumerable.Range(0, RoofCut.Strips).Select(k => Voxels(4, 0, k / 2.0, 16, 15.75 - k / 2.0, (k + 1) / 2.0)))
            .ToArray();

        Assert.Equal(expected, RoofCut.ClipBoxes([Voxels(0, 0, 0, 4, 16, 16), Voxels(4, 0, 0, 16, 16, 4)], Cells(InnerCorner, "north")));
    }

    // Each half of a valley's cube is capped on its own slope, a right triangle of half the slope's area, and nothing on the split.
    [Fact]
    public void AValleyCapsEachSlopeOnItsOwnSideOfTheSplit()
    {
        var caps = RoofCut.Caps(OneElementShape(("framing", [0, 0, 0], [16, 16, 16])), ["framing"], new Vec3f(), new OneTexture(),
            Cells(InnerCorner, "north"));

        Assert.Equal((2, 12, 2, 2, 2, 2, Math.Round(Math.Sqrt(2), 4)), Summary(caps));
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

    // In whole voxels, the lowest the cut reaches along the north, east, south and west faces.
    [Theory]
    [InlineData("north", ThatchedOneByOne, 16, 0, 0, 0)] // pick 0
    [InlineData("north", "(0,5.3,8)-(16,8,16) (0,8,0)-(16,10.7,8)", 11, 5, 5, 5)] // pick 3 then 1
    [InlineData("north", "(0,0,13.3)-(16,8,16) (0,8,10.7)-(16,16,13.3)", 16, 0, 0, 0)] // pick 3 then 12, held to the cell
    [InlineData("east", "(0,0,0)-(8,8,16) (8,8,0)-(16,16,16)", 0, 16, 0, 0)] // pick 11
    [InlineData("south", "(0,0,0)-(16,8,8) (0,8,8)-(16,16,16)", 0, 0, 16, 0)] // pick 12
    [InlineData("west", "(8,0,0)-(16,8,16) (0,8,0)-(8,16,16)", 0, 0, 0, 16)] // pick 13
    [InlineData("north", OuterCorner, 0, 0, 0, 0)]
    [InlineData("north", InnerCorner, 16, 16, 0, 0)]
    public void AFaceDamsAsHighAsTheCutsLowestPointAlongIt(string side, string boxes, double north, double east, double south, double west)
    {
        var cells = Cells(boxes, side);
        double Height(BlockFacing face) => Math.Round(16 * RoofCut.FaceHeight(cells, face));

        Assert.Equal((north, east, south, west), (Height(BlockFacing.NORTH), Height(BlockFacing.EAST), Height(BlockFacing.SOUTH), Height(BlockFacing.WEST)));
    }

    // On the east face under y = 1 - z, an area 7..8 voxels along it meets the cut 7 voxels up at its downhill end; no area
    // asks for the whole face.
    [Fact]
    public void AnAttachmentHoldsOnlyWhereItsAreaLiesWhollyUnderTheCut()
    {
        var cells = Cells(ThatchedOneByOne, "north");

        Assert.Equal((true, false, true, false), (
            RoofCut.Holds(cells, BlockFacing.EAST, new Cuboidi(15, 0, 7, 15, 6, 8)),
            RoofCut.Holds(cells, BlockFacing.EAST, new Cuboidi(15, 0, 7, 15, 7, 8)),
            RoofCut.Holds(cells, BlockFacing.NORTH, null),
            RoofCut.Holds(cells, BlockFacing.EAST, null)));
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
