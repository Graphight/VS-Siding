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

    // Only "wall" gets the layered mesh. "cornerout" has no four-layer shape yet
    // (decision layered-wall-mesh's own open question), so it keeps rendering via the
    // block's default JSON shape - returning false here leaves that path untouched.
    public override bool OnTesselation(ITerrainMeshPool mesher, ITesselatorAPI tesselator)
    {
        if (Block.Variant["layout"] != "wall") return false;
        if (Api is not ICoreClientAPI capi) return false;

        // An empty selectiveElements array matches zero shape elements, not "no filter" -
        // an unbuilt wall (true of every wall today, since nothing sets these keys yet)
        // must fall back to the block's default JSON shape instead of tesselating nothing.
        string[] selectiveElements = SelectiveElements(Framing, Infill, Front, Back);
        if (selectiveElements.Length == 0) return false;

        string side = Block.Variant["side"];
        string cacheKey = CacheKey(side, Framing, Infill, Front, Back);

        MeshData mesh = ObjectCacheUtil.GetOrCreate(capi, cacheKey, () =>
        {
            Shape shape = Shape.TryGet(capi, new AssetLocation("vssiding", "shapes/block/wall/wall.json"));
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

    internal static string CacheKey(string side, string? framing, string? infill, string? front, string? back)
        => $"vssiding-wall-mesh-{side}-{framing}-{infill}-{front}-{back}";

    // Unbuilt parts (null key) are left out so a frame-only wall shows just its frame.
    internal static string[] SelectiveElements(string? framing, string? infill, string? front, string? back)
    {
        var names = new List<string>();
        if (front != null) names.Add("front");
        if (framing != null) names.Add("framing");
        if (infill != null) names.Add("infill");
        if (back != null) names.Add("back");
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
