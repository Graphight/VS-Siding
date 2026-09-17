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
    public string? Back;

    public override void ToTreeAttributes(ITreeAttribute tree)
    {
        base.ToTreeAttributes(tree);
        tree.SetString("framing", Framing);
        tree.SetString("infill", Infill);
        tree.SetString("front", Front);
        tree.SetString("back", Back);
    }

    public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldAccessForResolve)
    {
        base.FromTreeAttributes(tree, worldAccessForResolve);
        Framing = NullIfEmpty(tree.GetString("framing", null));
        Infill = NullIfEmpty(tree.GetString("infill", null));
        Front = NullIfEmpty(tree.GetString("front", null));
        Back = NullIfEmpty(tree.GetString("back", null));
    }

    // A ToBytes/FromBytes round trip (chunk save/reload, client sync) turns a null
    // SetString value into "" - normalize back to null so "unbuilt" survives a reload.
    internal static string? NullIfEmpty(string? value) => string.IsNullOrEmpty(value) ? null : value;

    // "wall" and "cornerout" each have their own layered shape file (same four element
    // names - front/framing/infill/back - so selectiveElements works identically on
    // either). A cornerout wraps both claimed faces (decision 0002's CorneroutSecondFace)
    // with the SAME Framing/Infill/Front/Back state - one physical L-shaped frame, not two
    // independent builds - so no per-leg entity fields are needed, just per-leg geometry.
    public override bool OnTesselation(ITerrainMeshPool mesher, ITesselatorAPI tesselator)
    {
        string layout = Block.Variant["layout"];
        if (layout != "wall" && layout != "cornerout") return false;
        if (Api is not ICoreClientAPI capi) return false;

        // An empty selectiveElements array matches zero shape elements, not "no filter" -
        // an unbuilt wall (true of every wall today, since nothing sets these keys yet)
        // must fall back to the block's default JSON shape instead of tesselating nothing.
        bool continuesAbove = ContinuesFrame(Pos.UpCopy());
        bool continuesBelow = ContinuesFrame(Pos.DownCopy());

        string[] selectiveElements = SelectiveElements(Framing, Infill, Front, Back, Block.Attributes["Finishes"], continuesAbove, continuesBelow);
        if (selectiveElements.Length == 0) return false;

        string side = Block.Variant["side"];
        string cacheKey = CacheKey(layout, side, Framing, Infill, Front, Back, continuesAbove, continuesBelow);

        MeshData mesh = ObjectCacheUtil.GetOrCreate(capi, cacheKey, () =>
        {
            Shape shape = Shape.TryGet(capi, new AssetLocation("vssiding", $"shapes/block/wall/{layout}.json"));
            var texSource = new SidingWallTexSource(
                capi, this, Block.Attributes["Framings"], Block.Attributes["Infills"], Block.Attributes["Finishes"]);
            tesselator.TesselateShape(
                "vssiding-wall", shape, out MeshData modeldata, texSource,
                new Vec3f(0, RotationYDeg(side), 0), 0, 0, 0, null,
                selectiveElements);
            return modeldata;
        });

        mesher.AddMeshData(mesh);
        return true;
    }

    // Any framing counts - mixed woods still count as one continuous stack (decision 0008).
    private bool ContinuesFrame(BlockPos neighbourPos)
    {
        if (Api.World.BlockAccessor.GetBlock(neighbourPos) is not SidingWallBlock neighbourBlock) return false;
        if (neighbourBlock.Variant["layout"] != Block.Variant["layout"]) return false;
        if (neighbourBlock.Variant["side"] != Block.Variant["side"]) return false;
        return Api.World.BlockAccessor.GetBlockEntity<SidingWallEntity>(neighbourPos)?.Framing != null;
    }

    internal static string CacheKey(
        string layout, string side, string? framing, string? infill, string? front, string? back,
        bool continuesAbove, bool continuesBelow)
        => $"vssiding-wall-mesh-{layout}-{side}-{framing}-{infill}-{front}-{back}-{continuesAbove}-{continuesBelow}";

    // Unbuilt parts (null key) are left out so a frame-only wall shows just its frame.
    // A finish can name its own element per face (decision 0007) instead of the plain slab.
    // A stack draws one pair of plates for the whole run (decision 0008): a join has no
    // plates, so the infill extends across it instead.
    internal static string[] SelectiveElements(
        string? framing, string? infill, string? front, string? back, JsonObject finishes,
        bool continuesAbove, bool continuesBelow)
    {
        var names = new List<string>();
        if (front != null) names.Add(finishes[front]["Elements"]["front"].AsString("front"));
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
