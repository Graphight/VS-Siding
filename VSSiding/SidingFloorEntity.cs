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

    private (string?, string?, string?, string?) MeshState => (Framing, Infill, Front, Back);

    public override bool OnTesselation(ITerrainMeshPool mesher, ITesselatorAPI tesselator)
    {
        if (Api is not ICoreClientAPI capi) return false;

        var joins = SidingFloorBlock.Joins(Api.World.BlockAccessor, Pos);
        string[] selectiveElements = SelectiveElements(Framing, Infill, Front, Back, Block.Attributes["Finishes"], joins);
        if (selectiveElements.Length == 0) return false;

        string cacheKey = $"vssiding-floor-mesh-{Framing}-{Infill}-{Front}-{Back}-{joins.above}-{joins.below}";
        MeshData mesh = ObjectCacheUtil.GetOrCreate(capi, cacheKey, () =>
        {
            Shape shape = Shape.TryGet(capi, new AssetLocation("vssiding", "shapes/block/floor/floor.json"));
            var texSource = new TexSource(capi, this);
            tesselator.TesselateShape("vssiding-floor", shape, out MeshData modeldata, texSource,
                new Vec3f(0, 0, 0), 0, 0, 0, null, selectiveElements);
            return modeldata;
        });

        mesher.AddMeshData(mesh);
        return true;
    }

    // The wall's own selection, with every finish drawn as its plain slab: styled finishes on a
    // floor are thin-floor-finishes' job, and the floor shape has only the plain front and back.
    internal static string[] SelectiveElements(
        string? framing, string? infill, string? front, string? back, JsonObject finishes,
        (bool above, bool below, bool left, bool right) joins)
        => SidingWallEntity.SelectiveElements("wall", framing, infill, front, null, back, finishes, joins, glazed: false)
            .Select(name => name.StartsWith("front-") ? "front" : name.StartsWith("back-") ? "back" : name)
            .ToArray();

    private class TexSource(ICoreClientAPI capi, SidingFloorEntity entity) : ITexPositionSource
    {
        public Size2i AtlasSize => capi.BlockTextureAtlas.Size;

        public TextureAtlasPosition this[string textureCode] => SidingWallTexSource.AtlasPosition(capi, SidingWallTexSource.ResolveTexture(
            textureCode, entity.Framing, entity.Infill, entity.Front, null, entity.Back,
            entity.Block.Attributes["Framings"], entity.Block.Attributes["Infills"], entity.Block.Attributes["Finishes"]));
    }
}
