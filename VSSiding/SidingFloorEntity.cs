using System;
using System.Linq;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

namespace VSSiding;

// A floor's layers, named as the wall's are so the wall's helpers take them unchanged:
// Front is the top and Back the underside.
public class SidingFloorEntity : BlockEntity
{
    public string? Framing;
    public string? Infill;
    public string? Front;
    public string? Back;
    public string? FrontStyle;
    public string? BackStyle;

    public override void ToTreeAttributes(ITreeAttribute tree)
    {
        base.ToTreeAttributes(tree);
        tree.SetString("framing", Framing);
        tree.SetString("infill", Infill);
        tree.SetString("front", Front);
        tree.SetString("back", Back);
        tree.SetString("frontstyle", FrontStyle);
        tree.SetString("backstyle", BackStyle);
    }

    public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldAccessForResolve)
    {
        base.FromTreeAttributes(tree, worldAccessForResolve);
        string? oldInfill = Infill;
        var oldMesh = MeshState;
        Framing = SidingWallEntity.NullIfEmpty(tree.GetString("framing", null));
        Infill = SidingWallEntity.NullIfEmpty(tree.GetString("infill", null));
        // Other clients learn of new infill only through this sync, so they relight here; Api is null on chunk load.
        if (Api?.Side == EnumAppSide.Client && Infill != oldInfill)
        {
            ((SidingFloorBlock)Block).MarkAbsorptionChanged(worldAccessForResolve.BlockAccessor, Pos, Framing, oldInfill);
        }
        Front = SidingWallEntity.NullIfEmpty(tree.GetString("front", null));
        Back = SidingWallEntity.NullIfEmpty(tree.GetString("back", null));
        FrontStyle = SidingWallEntity.NullIfEmpty(tree.GetString("frontstyle", null));
        BackStyle = SidingWallEntity.NullIfEmpty(tree.GetString("backstyle", null));

        // As on the wall: a chunk meshed before its block entities arrive keeps the default shape until redrawn.
        if (Api?.Side == EnumAppSide.Client && MeshState != oldMesh)
        {
            worldAccessForResolve.BlockAccessor.MarkBlockDirty(Pos);
        }
    }

    // Block.GetPlacedBlockInfo calls this and appends the blockdesc- line after it, as on the wall.
    public override void GetBlockInfo(IPlayer forPlayer, StringBuilder dsc)
    {
        base.GetBlockInfo(forPlayer, dsc);
        dsc.Append(SidingFloorBlock.Describe(Framing, Infill, Front, Back, Block.Attributes, key => Lang.GetIfExists(key)));
    }

    private (string?, string?, string?, string?, string?, string?) MeshState => (Framing, Infill, Front, Back, FrontStyle, BackStyle);

    public override bool OnTesselation(ITerrainMeshPool mesher, ITesselatorAPI tesselator)
    {
        if (Api is not ICoreClientAPI capi) return false;

        bool glazed = SidingWallBlock.IsTransparent(Infill, Block.Attributes["Infills"]);
        var joins = SidingFloorBlock.Joins(Api.World.BlockAccessor, Pos, glazed);
        string[] selectiveElements = SelectiveElements(Framing, Infill, Front, Back, Block.Attributes["Finishes"], joins, (FrontStyle, BackStyle), glazed);
        if (selectiveElements.Length == 0) return false;

        string cacheKey = $"vssiding-floor-mesh-{Framing}-{Infill}-{Front}-{Back}-{FrontStyle}-{BackStyle}-{joins.above}-{joins.below}";
        MeshData[] meshes = ObjectCacheUtil.GetOrCreate(capi, cacheKey, () =>
        {
            Shape shape = Shape.TryGet(capi, new AssetLocation("vssiding", "shapes/block/floor/floor.json"));
            var texSource = new TexSource(capi, this);
            if (!glazed) return new[] { Tesselate(tesselator, shape, texSource, selectiveElements) };

            // Two meshes, never merged: see SidingWallEntity.OnTesselation.
            MeshData glass = Tesselate(tesselator, shape, texSource, Array.FindAll(selectiveElements, SidingWallEntity.IsInfillElement));
            SidingWallEntity.SetRenderPass(glass, EnumChunkRenderPass.Transparent);
            MeshData frame = Tesselate(tesselator, shape, texSource, Array.FindAll(selectiveElements, name => !SidingWallEntity.IsInfillElement(name)));
            return new[] { frame, glass };
        });

        foreach (MeshData mesh in meshes) mesher.AddMeshData(mesh);
        return true;
    }

    private static MeshData Tesselate(ITesselatorAPI tesselator, Shape shape, ITexPositionSource texSource, string[] selectiveElements)
    {
        tesselator.TesselateShape("vssiding-floor", shape, out MeshData modeldata, texSource,
            new Vec3f(0, 0, 0), 0, 0, 0, null, selectiveElements);
        return modeldata;
    }

    // The wall's framing and infill names, with each face's element read from the finish's own
    // FloorElements by its style, or the plain slab where it has none.
    internal static string[] SelectiveElements(
        string? framing, string? infill, string? front, string? back, JsonObject finishes,
        (bool above, bool below, bool left, bool right) joins, (string? front, string? back) styles = default, bool glazed = false)
    {
        var names = SidingWallEntity.SelectiveElements("wall", framing, infill, null, null, null, finishes, joins, glazed).ToList();
        if (front != null) names.Insert(0, FloorElement(finishes, front, "front", styles.front));
        if (back != null) names.Add(FloorElement(finishes, back, "back", styles.back));
        return names.ToArray();
    }

    // Every floor's joists run north-south (decision 0050), so a face with no style picked lays its
    // boards across them.
    private const string DefaultStyle = "hboards";

    // FloorElements maps each face's styles to the element drawing them; a face it leaves out is the plain slab.
    private static string FloorElement(JsonObject finishes, string key, string face, string? style)
    {
        var looks = finishes[key]["FloorElements"][face];
        return looks.Exists ? looks[style ?? DefaultStyle].AsString(null!) ?? looks[DefaultStyle].AsString(face) : face;
    }

    internal static bool HasFloorStyle(JsonObject finish, string face, string style)
        => finish["FloorElements"][face][style].Exists;

    private class TexSource(ICoreClientAPI capi, SidingFloorEntity entity) : ITexPositionSource
    {
        public Size2i AtlasSize => capi.BlockTextureAtlas.Size;

        public TextureAtlasPosition this[string textureCode] => SidingWallTexSource.AtlasPosition(capi, SidingWallTexSource.ResolveTexture(
            textureCode, entity.Framing, entity.Infill, entity.Front, null, entity.Back,
            entity.Block.Attributes["Framings"], entity.Block.Attributes["Infills"], entity.Block.Attributes["Finishes"]));
    }
}
