using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
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
    // Null means "the finish entry's own Elements default" (decision 0027).
    public string? FrontStyle;
    public string? SecondFrontStyle;
    public string? BackStyle;
    // A Framings key, like Framing.
    public string? Deck;
    // The deck's own layers, laid like a floor's; null is unbuilt.
    public string? DeckInfill;
    public string? DeckFront;
    public string? DeckBack;
    public string? DeckFrontStyle;
    public string? DeckBackStyle;
    // A deck saved before decks took layers sealed as bare framing, and keeps doing so until infill is laid.
    public bool LegacyDeck;
    // The held stair's full block code, e.g. "game:plankstairs-oak-up-north-free".
    public string? Step;
    // Vanilla's own vertical-horizontal naming, e.g. "up-north".
    public string? StepOrientation;

    internal bool OpenPartFilled => Deck != null || Step != null;

    public override void ToTreeAttributes(ITreeAttribute tree)
    {
        base.ToTreeAttributes(tree);
        tree.SetString("framing", Framing);
        tree.SetString("infill", Infill);
        tree.SetString("front", Front);
        tree.SetString("secondfront", SecondFront);
        tree.SetString("back", Back);
        tree.SetString("frontstyle", FrontStyle);
        tree.SetString("secondfrontstyle", SecondFrontStyle);
        tree.SetString("backstyle", BackStyle);
        tree.SetString("deck", Deck);
        tree.SetString("deckinfill", DeckInfill);
        tree.SetString("deckfront", DeckFront);
        tree.SetString("deckback", DeckBack);
        tree.SetString("deckfrontstyle", DeckFrontStyle);
        tree.SetString("deckbackstyle", DeckBackStyle);
        tree.SetBool("legacydeck", LegacyDeck);
        tree.SetString("step", Step);
        tree.SetString("steporientation", StepOrientation);
    }

    public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldAccessForResolve)
    {
        base.FromTreeAttributes(tree, worldAccessForResolve);
        string? oldInfill = Infill;
        var oldMesh = MeshState;
        Framing = NullIfEmpty(tree.GetString("framing", null));
        Infill = NullIfEmpty(tree.GetString("infill", null));
        // Other clients learn of new infill only through this sync, so they relight here; Api is null on chunk load.
        if (Api?.Side == EnumAppSide.Client && Infill != oldInfill)
        {
            ((SidingWallBlock)Block).MarkAbsorptionChanged(worldAccessForResolve.BlockAccessor, Pos, Framing, oldInfill);
        }
        Front = NullIfEmpty(tree.GetString("front", null));
        SecondFront = NullIfEmpty(tree.GetString("secondfront", null));
        Back = NullIfEmpty(tree.GetString("back", null));
        FrontStyle = NullIfEmpty(tree.GetString("frontstyle", null));
        SecondFrontStyle = NullIfEmpty(tree.GetString("secondfrontstyle", null));
        BackStyle = NullIfEmpty(tree.GetString("backstyle", null));
        Deck = NullIfEmpty(tree.GetString("deck", null));
        DeckInfill = NullIfEmpty(tree.GetString("deckinfill", null));
        DeckFront = NullIfEmpty(tree.GetString("deckfront", null));
        DeckBack = NullIfEmpty(tree.GetString("deckback", null));
        DeckFrontStyle = NullIfEmpty(tree.GetString("deckfrontstyle", null));
        DeckBackStyle = NullIfEmpty(tree.GetString("deckbackstyle", null));
        LegacyDeck = ReadLegacyDeck(tree, Deck);
        Step = NullIfEmpty(tree.GetString("step", null));
        StepOrientation = NullIfEmpty(tree.GetString("steporientation", null));

        // A chunk can be meshed before its block entities arrive, and nothing else redraws it
        // afterwards: OnTesselation reads null state, returns false, and the cell keeps the
        // block's default solid shape - flat slabs, no cladding relief - until something
        // unrelated disturbs it. Redrawing on the sync that brought the state is that something.
        if (Api?.Side == EnumAppSide.Client && MeshState != oldMesh)
        {
            worldAccessForResolve.BlockAccessor.MarkBlockDirty(Pos);
        }
    }

    // A tree that carries the flag was saved by a version that knows the layers, so it is authoritative.
    // The flag is keyed rather than deckinfill, which a null string never stores.
    internal static bool ReadLegacyDeck(ITreeAttribute tree, string? deck)
        => tree.HasAttribute("legacydeck") ? tree.GetBool("legacydeck") : deck != null;

    // Block.GetPlacedBlockInfo already calls this inside a try/catch and appends the
    // blockdesc- line after it, so overriding here is the one hook rather than two.
    public override void GetBlockInfo(IPlayer forPlayer, StringBuilder dsc)
    {
        base.GetBlockInfo(forPlayer, dsc);
        string? stepName = null;
        if (Step != null && Api.World.GetBlock(new AssetLocation(Step)) is { } stepBlock)
            stepName = stepBlock.GetHeldItemName(new ItemStack(stepBlock));
        dsc.Append(SidingWallBlock.Describe(
            Framing, Infill, Deck, Block.Attributes["Framings"], Block.Attributes["Infills"],
            Block.Variant["layout"], Block.Variant["side"], Front, SecondFront, Back, Block.Attributes["Finishes"],
            key => Lang.GetIfExists(key), stepName, DeckInfill, DeckFront, DeckBack));
    }

    // Everything OnTesselation reads off this entity, which is exactly what CacheKey covers.
    private ((string?, string?, string?, string?, string?, string?, string?, string?, string?, string?, string?), (string?, string?, string?, string?, string?)) MeshState
        => ((Framing, Infill, Front, SecondFront, Back, FrontStyle, SecondFrontStyle, BackStyle, Deck, Step, StepOrientation),
            (DeckInfill, DeckFront, DeckBack, DeckFrontStyle, DeckBackStyle));

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

        var joins = ((SidingWallBlock)Block).NeighbourJoins(Api.World.BlockAccessor, Pos, Infill);

        // An empty selectiveElements array matches zero shape elements, not "no filter", and any
        // built cell names at least one - glazing merged on every side still draws its pane. So
        // empty means nothing is built, and the block's default JSON shape stands in.
        bool glazed = SidingWallBlock.IsTransparent(Infill, Block.Attributes["Infills"]);
        string side = Block.Variant["side"];
        string[]? step = Step != null && layout == "wall" ? StepElements(side, StepOrientation) : null;
        string[] selectiveElements = SelectiveElements(layout, Framing, Infill, Front, SecondFront, Back, Block.Attributes["Finishes"], joins, glazed, Styles, step);

        // The deck is laid like a floor, in world directions, so its groups are tesselated unrotated.
        bool deckGlazed = SidingWallBlock.IsTransparent(DeckInfill, Block.Attributes["Infills"]);
        var deckJoins = SidingFloorBlock.Joins(Api.World.BlockAccessor, Pos, deckGlazed);
        string[] deckElements = DeckElements(side, Deck, DeckInfill, DeckFront, DeckBack, Block.Attributes["Finishes"], deckJoins, (DeckFrontStyle, DeckBackStyle), deckGlazed);
        if (selectiveElements.Length == 0 && deckElements.Length == 0) return false;

        string cacheKey = CacheKey(layout, side, Framing, Infill, Front, SecondFront, Back, joins, Styles, Deck, Step, StepOrientation,
            DeckInfill, DeckFront, DeckBack, (DeckFrontStyle, DeckBackStyle), deckJoins);

        MeshData[] meshes = ObjectCacheUtil.GetOrCreate(capi, cacheKey, () =>
        {
            Shape shape = Shape.TryGet(capi, new AssetLocation("vssiding", $"shapes/block/wall/{layout}.json"));
            var texSource = new SidingWallTexSource(
                capi, this, Block.Attributes["Framings"], Block.Attributes["Infills"], Block.Attributes["Finishes"]);
            var rotation = new Vec3f(0, RotationYDeg(side), 0);
            return Meshes(tesselator, shape, texSource, rotation, selectiveElements, glazed)
                .Concat(Meshes(tesselator, shape, texSource, new Vec3f(0, 0, 0), deckElements, deckGlazed))
                .ToArray();
        });

        foreach (MeshData mesh in meshes) mesher.AddMeshData(mesh);
        return true;
    }

    private static IEnumerable<MeshData> Meshes(
        ITesselatorAPI tesselator, Shape shape, ITexPositionSource texSource, Vec3f rotation, string[] elements, bool glazed)
    {
        if (elements.Length == 0) return [];
        if (!glazed) return [Tesselate(tesselator, shape, texSource, rotation, elements)];

        // Glazing has to reach the transparent pool while its frame stays opaque, so the two
        // halves are tesselated and handed over separately. They are deliberately NOT merged
        // into one mesh: MeshData.AddMeshData offsets the incoming indices by the target's
        // last index value plus one, which is only the target's vertex count when that last
        // index is also its highest. Any trailing vertex without an index shifts the glass
        // indices back into the frame's vertices and smears a pane across the room.
        MeshData glass = Tesselate(tesselator, shape, texSource, rotation, Array.FindAll(elements, IsInfillElement));
        SetRenderPass(glass, EnumChunkRenderPass.Transparent);
        MeshData frame = Tesselate(tesselator, shape, texSource, rotation, Array.FindAll(elements, name => !IsInfillElement(name)));
        return [frame, glass];
    }

    private static MeshData Tesselate(
        ITesselatorAPI tesselator, Shape shape, ITexPositionSource texSource, Vec3f rotation, string[] selectiveElements)
    {
        tesselator.TesselateShape(
            "vssiding-wall", shape, out MeshData modeldata, texSource, rotation, 0, 0, 0, null, selectiveElements);
        return modeldata;
    }

    internal static bool IsInfillElement(string name)
        => name.StartsWith("infill") || (name.StartsWith("deck-") && name[(name.IndexOf('-', 5) + 1)..].StartsWith("infill"));

    // TesselateShape already writes one entry per quad - ShapeElement.RenderPass, which defaults
    // to -1 and counts as opaque - so the frame half needs nothing and this only overwrites.
    // The per-quad count has to stay exact either way: AddMeshData appends the two lists in step
    // with the two vertex lists.
    internal static void SetRenderPass(MeshData mesh, EnumChunkRenderPass pass)
    {
        for (int quad = 0; quad < mesh.RenderPassCount; quad++) mesh.RenderPassesAndExtraBits[quad] = (short)pass;
    }

    private (string? front, string? secondFront, string? back) Styles => (FrontStyle, SecondFrontStyle, BackStyle);

    internal static string CacheKey(
        string layout, string side, string? framing, string? infill, string? front, string? secondFront, string? back,
        (bool above, bool below, bool left, bool right) joins,
        (string? front, string? secondFront, string? back) styles = default, string? deck = null, string? step = null, string? stepOrientation = null,
        string? deckInfill = null, string? deckFront = null, string? deckBack = null, (string? front, string? back) deckStyles = default,
        (bool above, bool below, bool left, bool right) deckJoins = default)
        => $"vssiding-wall-mesh-{layout}-{side}-{framing}-{infill}-{front}-{secondFront}-{back}-{joins.above}-{joins.below}-{joins.left}-{joins.right}-{styles.front}-{styles.secondFront}-{styles.back}-{deck}-{step}-{stepOrientation}"
            + $"-{deckInfill}-{deckFront}-{deckBack}-{deckStyles.front}-{deckStyles.back}-{deckJoins.above}-{deckJoins.below}-{deckJoins.left}-{deckJoins.right}";

    // The floor's groups for the deck, which the wall shapes carry per side as deck-{side}-{name}.
    internal static string[] DeckElements(
        string side, string? deck, string? infill, string? front, string? back, JsonObject finishes,
        (bool above, bool below, bool left, bool right) joins, (string? front, string? back) styles, bool glazed)
        => deck == null
            ? []
            : SidingFloorEntity.SelectiveElements(deck, infill, front, back, finishes, joins, styles, glazed).Select(name => $"deck-{side}-{name}").ToArray();

    // Unbuilt parts (null key) are left out so a frame-only wall shows just its frame.
    // A finish can name its own element per face (decision 0007) instead of the plain slab.
    // A join between stacked cells has no plates, so the infill extends across it (decision 0008).
    internal static string[] SelectiveElements(
        string layout, string? framing, string? infill, string? front, string? secondFront, string? back, JsonObject finishes,
        (bool above, bool below, bool left, bool right) joins, bool glazed,
        (string? front, string? secondFront, string? back) styles = default, string[]? step = null)
    {
        var names = new List<string>();
        if (front != null) names.Add(FinishElement(finishes, front, "front", styles.front));
        if (secondFront != null) names.Add("second" + FinishElement(finishes, secondFront, "front", styles.secondFront));
        if (framing != null)
        {
            // A cornerout's three posts are structural and always drawn; a wall's two drop
            // individually wherever glazing merges sideways.
            bool corner = layout == "cornerout";
            // Glazing gets its own frame: a bezel whose members each span the full cell edge, so
            // the frame still reaches the pane where the member beside it has been dropped. A
            // plain wall's plates stop short at its posts, which is right while the posts are
            // always there and leaves a notch at every cell edge once they aren't.
            string member = glazed && !corner ? "glazing" : "framing";
            if (corner) names.Add("framing");
            else
            {
                if (!joins.left) names.Add(member + "-left");
                if (!joins.right) names.Add(member + "-right");
            }
            if (!joins.above) names.Add(member + "-top");
            if (!joins.below) names.Add(member + "-bottom");
        }
        if (infill != null)
        {
            // Glazing is one flat pane spanning the whole cell rather than a slab plus fillers.
            // Three stacked boxes share a face at each seam, and two coincident transparent quads
            // blend twice over - a bright line exactly where a cross-beam would be. One pane also
            // meets the pane above it edge on, so a glazed stack has no seams at all.
            if (glazed) names.Add("infill-pane");
            else
            {
                names.Add("infill");
                if (joins.above) names.Add("infill-top");
                if (joins.below) names.Add("infill-bottom");
            }
        }
        if (back != null) names.Add(FinishElement(finishes, back, "back", styles.back));
        if (step != null) names.AddRange(step);
        return names.ToArray();
    }

    // A style names its element outright; without one the entry's Elements default stands.
    private static string FinishElement(JsonObject finishes, string key, string face, string? style)
        => style != null ? $"{face}-{style}" : finishes[key]["Elements"][face].AsString(face);

    // Same four angles as collisionSelectionBoxesbytype's rotateYByType in wall.json.
    internal static float RotationYDeg(string side) => side switch
    {
        "west" => 0,
        "south" => 90,
        "east" => 180,
        "north" => 270,
        _ => 0,
    };

    // The unrotated "north" half (wall.json's -north elements, z 0..8) lands on this world
    // facing once RotationYDeg(side) turns it; the "south" half always lands on the opposite one.
    private static readonly Dictionary<string, Dictionary<string, string>> StepHalfByFacing = new()
    {
        ["west"] = new() { ["north"] = "north", ["south"] = "south" },
        ["south"] = new() { ["west"] = "north", ["east"] = "south" },
        ["east"] = new() { ["south"] = "north", ["north"] = "south" },
        ["north"] = new() { ["east"] = "north", ["west"] = "south" },
    };

    // "up-F" rises to a full lower half plus an upper half on the F side; "down-F" is the
    // mirror. F has to run along the wall - the wall's own normal axis (e.g. east/west for
    // side west) isn't a direction a step against it can rise toward.
    internal static string[] StepElements(string side, string? orientation)
    {
        if (orientation == null) return [];
        string[] parts = orientation.Split('-');
        if (parts.Length != 2) return [];
        (string vertical, string facing) = (parts[0], parts[1]);
        if (!StepHalfByFacing[side].TryGetValue(facing, out string? half)) return [];
        return vertical == "up" ? new[] { "step-lower", $"step-upper-{half}" } : new[] { "step-upper", $"step-lower-{half}" };
    }
}
