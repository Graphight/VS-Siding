using System.Linq;
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
    private readonly int alternate;

    public SidingWallTexSource(ICoreClientAPI capi, SidingWallEntity entity, JsonObject framings, JsonObject infills, JsonObject finishes, int alternate)
    {
        this.capi = capi;
        this.entity = entity;
        this.framings = framings;
        this.infills = infills;
        this.finishes = finishes;
        this.alternate = alternate;
    }

    public Size2i AtlasSize => capi.BlockTextureAtlas.Size;

    public TextureAtlasPosition this[string textureCode]
    {
        get
        {
            if (textureCode == "step") return StepTexture();
            CompositeTexture? texture = ResolveTexture(
                textureCode, entity.Framing, entity.Infill, entity.Front, entity.SecondFront, entity.Back,
                framings, infills, finishes, entity.FrontStyle, entity.SecondFrontStyle, entity.BackStyle, entity.Deck,
                entity.DeckInfill, entity.DeckFront, entity.DeckBack);
            return AtlasPosition(capi, texture, alternate);
        }
    }

    // A "*" texture (planks-{wood}*) varies per cell as a vanilla plank block does: CubeTesselator takes
    // MurmurHash3 of the position mod the variant count. Kept mod 120, which every count from 1 to 6,
    // 8 and 10 (very aged planks) divides, so it can key the mesh cache and still pick vanilla's variant.
    internal static int Alternate(BlockPos pos) => GameMath.Mod(GameMath.MurmurHash3(pos.X, pos.Y, pos.Z), 120);

    internal static bool AnyVaries(JsonObject dictionary, params string?[] keys)
        => keys.Any(key => key != null && dictionary[key]["Texture"].AsString(null!)?.EndsWith('*') == true);

    // Variant 0 is the base, then the alternates, the order Bake lists them in.
    internal static CompositeTexture PickAlternate(CompositeTexture baked, int alternate)
    {
        if (baked.Alternates is not { Length: > 0 } alternates) return baked;
        int index = GameMath.Mod(alternate, alternates.Length + 1);
        return index == 0 ? baked : alternates[index - 1];
    }

    internal static TextureAtlasPosition AtlasPosition(ICoreClientAPI capi, CompositeTexture? texture, int alternate = 0)
    {
        texture ??= new CompositeTexture(new AssetLocation("game:block/wood/planks/oak1"));
        if (texture.Base.EndsWithWildCard)
        {
            texture.Bake(capi.Assets);
            texture = PickAlternate(texture, alternate);
        }
        var atlas = capi.BlockTextureAtlas;
        // The plain indexer only finds textures some other block/item already caused to
        // be packed into the atlas - most of our material textures aren't declared by
        // anything else, so they need GetOrInsertTexture to load and pack them on demand.
        return atlas.GetOrInsertTexture(texture, out _, out TextureAtlasPosition texPos)
            ? texPos
            : atlas.UnknownTexturePosition;
    }

    // From the stair block itself, so every BlockStairs works with no wall.json entry (decision 0046).
    private TextureAtlasPosition StepTexture()
    {
        Block? block = entity.Step == null ? null : capi.World.GetBlock(new AssetLocation(entity.Step));
        // Stairs with only their own keys, like vanilla stone path's normal1, have no "up" to ask for.
        var source = block == null ? null : capi.Tesselator.GetTextureSource(block, returnNullWhenMissing: true);
        TextureAtlasPosition? pos = source == null ? null : source["up"] ?? block!.Textures.Keys.Select(key => source[key]).FirstOrDefault(p => p != null);
        return pos ?? capi.BlockTextureAtlas.UnknownTexturePosition;
    }

    private const string LashingTexture = "game:item/resource/rope";

    // Unbuilt slots (null key), a key no longer present in its dictionary, or an entry with
    // no usable Texture resolve to null - callers only reach here for selectiveElements actually being tesselated, so
    // this is a defensive fallback, not the expected path.
    internal static CompositeTexture? ResolveTexture(
        string slotCode, string? framing, string? infill, string? front, string? secondFront, string? back,
        JsonObject framings, JsonObject infills, JsonObject finishes,
        string? frontStyle = null, string? secondFrontStyle = null, string? backStyle = null, string? deck = null,
        string? deckInfill = null, string? deckFront = null, string? deckBack = null)
    {
        if (slotCode == "lashing") return new CompositeTexture(new AssetLocation(LashingTexture));

        (string? key, JsonObject dictionary, string face, string? style) = slotCode switch
        {
            "framing" => (framing, framings, "framing", null),
            "deck" => (deck, framings, "framing", null),
            "deckinfill" => (deckInfill, infills, "infill", null),
            "deckfront" => (deckFront, finishes, "front", null),
            "deckback" => (deckBack, finishes, "back", null),
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
