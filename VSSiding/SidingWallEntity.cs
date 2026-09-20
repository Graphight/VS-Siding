using System;
using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

namespace VSSiding;

public class SidingWallEntity : BlockEntity
{
    public string? Framing;
    public string? Infill;
    public string? Front;
    public string? SecondFront;
    public string? Back;

    public override void ToTreeAttributes(ITreeAttribute tree)
    {
        base.ToTreeAttributes(tree);
        tree.SetString("framing", Framing);
        tree.SetString("infill", Infill);
        tree.SetString("front", Front);
        tree.SetString("secondfront", SecondFront);
        tree.SetString("back", Back);
    }

    public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldAccessForResolve)
    {
        base.FromTreeAttributes(tree, worldAccessForResolve);
        string? oldInfill = Infill;
        Framing = NullIfEmpty(tree.GetString("framing", null));
        Infill = NullIfEmpty(tree.GetString("infill", null));
        // Other clients learn of new infill only through this sync, so they relight here; Api is null on chunk load.
        if (Api?.Side == EnumAppSide.Client && Infill != oldInfill)
        {
            worldAccessForResolve.BlockAccessor.MarkAbsorptionChanged(0, Block.GetLightAbsorption(worldAccessForResolve.BlockAccessor, Pos), Pos);
        }
        Front = NullIfEmpty(tree.GetString("front", null));
        SecondFront = NullIfEmpty(tree.GetString("secondfront", null));
        Back = NullIfEmpty(tree.GetString("back", null));
    }

    // A ToBytes/FromBytes round trip (chunk save/reload, client sync) turns a null
    // SetString value into "" - normalize back to null so "unbuilt" survives a reload.
    internal static string? NullIfEmpty(string? value) => string.IsNullOrEmpty(value) ? null : value;

    // "wall" and "cornerout" each have their own layered shape file (same four element
    // names - front/framing/infill/back - so selectiveElements works identically on
    // either). A cornerout wraps both claimed faces (decision 0002's CorneroutSecondFace)
    // with a shared Framing/Infill/Back but its own SecondFront (decision 0009) - the
    // second leg's front faces a different room, so it finishes independently.
    public override bool OnTesselation(ITerrainMeshPool mesher, ITesselatorAPI tesselator)
    {
        string layout = Block.Variant["layout"];
        if (layout != "wall" && layout != "cornerout") return false;
        if (Api is not ICoreClientAPI capi) return false;

        var (continuesAbove, continuesBelow) = ((SidingWallBlock)Block).StackJoins(Api.World.BlockAccessor, Pos, Infill);

        // An empty selectiveElements array matches zero shape elements, not "no filter" -
        // an unbuilt wall (true of every wall today, since nothing sets these keys yet)
        // must fall back to the block's default JSON shape instead of tesselating nothing.
        string[] selectiveElements = SelectiveElements(Framing, Infill, Front, SecondFront, Back, Block.Attributes["Finishes"], continuesAbove, continuesBelow);
        if (selectiveElements.Length == 0) return false;

        string side = Block.Variant["side"];
        string cacheKey = CacheKey(layout, side, Framing, Infill, Front, SecondFront, Back, continuesAbove, continuesBelow);

        MeshData mesh = ObjectCacheUtil.GetOrCreate(capi, cacheKey, () =>
        {
            Shape shape = Shape.TryGet(capi, new AssetLocation("vssiding", $"shapes/block/wall/{layout}.json"));
            var texSource = new SidingWallTexSource(
                capi, this, Block.Attributes["Framings"], Block.Attributes["Infills"], Block.Attributes["Finishes"]);
            var rotation = new Vec3f(0, RotationYDeg(side), 0);

            if (!SidingWallBlock.IsTransparent(Infill, Block.Attributes["Infills"]))
                return Tesselate(tesselator, shape, texSource, rotation, selectiveElements);

            // Glazing has to reach the transparent pool while its frame stays opaque, so the two
            // are tesselated apart and the glass half restamped before they're merged - one mesh
            // carrying a pass per quad, the same way vanilla's chiselled blocks mix materials.
            MeshData glass = Tesselate(tesselator, shape, texSource, rotation, Array.FindAll(selectiveElements, IsInfillElement));
            SetRenderPass(glass, EnumChunkRenderPass.Transparent);
            MeshData frame = Tesselate(tesselator, shape, texSource, rotation, Array.FindAll(selectiveElements, name => !IsInfillElement(name)));
            frame.AddMeshData(glass);
            return frame;
        });

        mesher.AddMeshData(mesh);
        return true;
    }

    private static MeshData Tesselate(
        ITesselatorAPI tesselator, Shape shape, ITexPositionSource texSource, Vec3f rotation, string[] selectiveElements)
    {
        tesselator.TesselateShape(
            "vssiding-wall", shape, out MeshData modeldata, texSource, rotation, 0, 0, 0, null, selectiveElements);
        return modeldata;
    }

    internal static bool IsInfillElement(string name) => name.StartsWith("infill");

    // TesselateShape already writes one entry per quad - ShapeElement.RenderPass, which defaults
    // to -1 and counts as opaque - so the frame half needs nothing and this only overwrites.
    // The per-quad count has to stay exact either way: AddMeshData appends the two lists in step
    // with the two vertex lists.
    private static void SetRenderPass(MeshData mesh, EnumChunkRenderPass pass)
    {
        for (int quad = 0; quad < mesh.RenderPassCount; quad++) mesh.RenderPassesAndExtraBits[quad] = (short)pass;
    }

    internal static string CacheKey(
        string layout, string side, string? framing, string? infill, string? front, string? secondFront, string? back,
        bool continuesAbove, bool continuesBelow)
        => $"vssiding-wall-mesh-{layout}-{side}-{framing}-{infill}-{front}-{secondFront}-{back}-{continuesAbove}-{continuesBelow}";

    // Unbuilt parts (null key) are left out so a frame-only wall shows just its frame.
    // A finish can name its own element per face (decision 0007) instead of the plain slab.
    // A join between stacked cells has no plates, so the infill extends across it (decision 0008).
    internal static string[] SelectiveElements(
        string? framing, string? infill, string? front, string? secondFront, string? back, JsonObject finishes,
        bool continuesAbove, bool continuesBelow)
    {
        var names = new List<string>();
        if (front != null) names.Add(finishes[front]["Elements"]["front"].AsString("front"));
        if (secondFront != null) names.Add("second" + finishes[secondFront]["Elements"]["front"].AsString("front"));
        if (framing != null)
        {
            names.Add("framing");
            if (!continuesAbove) names.Add("framing-top");
            if (!continuesBelow) names.Add("framing-bottom");
        }
        if (infill != null)
        {
            names.Add("infill");
            if (continuesAbove) names.Add("infill-top");
            if (continuesBelow) names.Add("infill-bottom");
        }
        if (back != null) names.Add(finishes[back]["Elements"]["back"].AsString("back"));
        return names.ToArray();
    }

    // Same four angles as collisionSelectionBoxesbytype's rotateYByType in wall.json.
    internal static float RotationYDeg(string side) => side switch
    {
        "west" => 0,
        "south" => 90,
        "east" => 180,
        "north" => 270,
        _ => 0,
    };
}
