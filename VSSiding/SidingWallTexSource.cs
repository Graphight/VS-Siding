using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace VSSiding;

// Answers the tesselator's "where is texture #framing/#infill/#front/#back?" by looking
// up the entity's chosen material key in the block's material dictionaries.
public class SidingWallTexSource : ITexPositionSource
{
    private readonly ICoreClientAPI capi;
    private readonly SidingWallEntity entity;
    private readonly JsonObject framings;
    private readonly JsonObject infills;
    private readonly JsonObject finishes;

    public SidingWallTexSource(ICoreClientAPI capi, SidingWallEntity entity, JsonObject framings, JsonObject infills, JsonObject finishes)
    {
        this.capi = capi;
        this.entity = entity;
        this.framings = framings;
        this.infills = infills;
        this.finishes = finishes;
    }

    public Size2i AtlasSize => capi.BlockTextureAtlas.Size;

    public TextureAtlasPosition this[string textureCode]
    {
        get
        {
            CompositeTexture texture = ResolveTexture(
                textureCode, entity.Framing, entity.Infill, entity.Front, entity.SecondFront, entity.Back,
                framings, infills, finishes) ?? new CompositeTexture(new AssetLocation("game:block/wood/planks/oak1"));
            var atlas = capi.BlockTextureAtlas;
            // RuntimeBake loads and packs the base plus any overlays into the atlas on demand
            // (most of our material textures aren't declared by anything else), and keys the
            // atlas entry by the baked name, so identical composites across walls share one slot.
            texture.RuntimeBake(capi, atlas);
            return texture.Baked != null && atlas.Positions.Length > texture.Baked.TextureSubId
                ? atlas.Positions[texture.Baked.TextureSubId]
                : atlas.UnknownTexturePosition;
        }
    }

    // Unbuilt slots (null key) or a key no longer present in its dictionary resolve to
    // null - callers only reach here for selectiveElements actually being tesselated, so
    // this is a defensive fallback, not the expected path.
    internal static CompositeTexture? ResolveTexture(
        string slotCode, string? framing, string? infill, string? front, string? secondFront, string? back,
        JsonObject framings, JsonObject infills, JsonObject finishes)
    {
        (string? key, JsonObject dictionary) = slotCode switch
        {
            "framing" => (framing, framings),
            "infill" => (infill, infills),
            "front" => (front, finishes),
            "secondfront" => (secondFront, finishes),
            "back" => (back, finishes),
            _ => (null, finishes),
        };
        if (key == null) return null;

        var entry = dictionary[key];
        if (!entry.Exists) return null;

        var texture = entry["Texture"];
        return texture.Token?.Type == Newtonsoft.Json.Linq.JTokenType.Object
            ? texture.AsObject<CompositeTexture>()
            : new CompositeTexture(new AssetLocation(texture.AsString(null!)));
    }
}
