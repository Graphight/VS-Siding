using Newtonsoft.Json.Linq;
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
                framings, infills, finishes, entity.FrontStyle, entity.SecondFrontStyle, entity.BackStyle)
                ?? new CompositeTexture(new AssetLocation("game:block/wood/planks/oak1"));
            var atlas = capi.BlockTextureAtlas;
            // The plain indexer only finds textures some other block/item already caused to
            // be packed into the atlas - most of our material textures aren't declared by
            // anything else, so they need GetOrInsertTexture to load and pack them on demand.
            return atlas.GetOrInsertTexture(texture, out _, out TextureAtlasPosition texPos)
                ? texPos
                : atlas.UnknownTexturePosition;
        }
    }

    // Unbuilt slots (null key), a key no longer present in its dictionary, or an entry with
    // no usable Texture resolve to null - callers only reach here for selectiveElements actually being tesselated, so
    // this is a defensive fallback, not the expected path.
    internal static CompositeTexture? ResolveTexture(
        string slotCode, string? framing, string? infill, string? front, string? secondFront, string? back,
        JsonObject framings, JsonObject infills, JsonObject finishes,
        string? frontStyle = null, string? secondFrontStyle = null, string? backStyle = null)
    {
        (string? key, JsonObject dictionary, string face, string? style) = slotCode switch
        {
            "framing" => (framing, framings, "framing", null),
            "infill" => (infill, infills, "infill", null),
            // secondfront reads the front face's Elements default (decision 0027's naming), so its
            // style falls back the same way.
            "front" => (front, finishes, "front", frontStyle),
            "secondfront" => (secondFront, finishes, "front", secondFrontStyle),
            "back" => (back, finishes, "back", backStyle),
            _ => (null, finishes, slotCode, null),
        };
        if (key == null) return null;

        var entry = dictionary[key];
        if (!entry.Exists) return null;

        string? resolvedStyle = style ?? StyleSuffix(entry, face);
        var texture = resolvedStyle != null && entry["StyleTextures"][resolvedStyle].Exists
            ? entry["StyleTextures"][resolvedStyle]
            : entry["Texture"];
        if (texture.Token?.Type == JTokenType.Object)
            return texture.AsObject<CompositeTexture>() is { Base: not null } composite ? composite : null;
        string? path = texture.AsString(null!);
        return path == null ? null : new CompositeTexture(new AssetLocation(path));
    }

    // Mirrors SidingWallEntity.FinishElement's fallback: without an explicit style, the style is
    // whatever follows "{face}-" in the entry's own Elements default, or none if that default
    // names no style (e.g. a plain "front"/"back" with no per-style geometry).
    private static string? StyleSuffix(JsonObject entry, string face)
    {
        string elementName = entry["Elements"][face].AsString(face);
        string prefix = face + "-";
        return elementName.StartsWith(prefix) ? elementName[prefix.Length..] : null;
    }
}
