using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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
    // A post's fourth face, the one at CorneroutSecondFace[side] (decision 0082).
    public string? SecondBack;
    // Null means "the finish entry's own Elements default" (decision 0027).
    public string? FrontStyle;
    public string? SecondFrontStyle;
    public string? BackStyle;
    public string? SecondBackStyle;
    // A Framings key, like Framing.
    public string? Deck;
    // The deck's own layers, laid like a floor's; null is unbuilt.
    public string? DeckInfill;
    public string? DeckFront;
    public string? DeckBack;
    public string? DeckFrontStyle;
    public string? DeckBackStyle;
    // A diagonal's deck on its outer triangle, with layers of its own (decision 0096).
    public string? OuterDeck;
    public string? OuterDeckInfill;
    public string? OuterDeckFront;
    public string? OuterDeckBack;
    public string? OuterDeckFrontStyle;
    public string? OuterDeckBackStyle;
    // A deck saved before decks took layers sealed as bare framing, and keeps doing so until infill is laid.
    public bool LegacyDeck;
    // The held stair's full block code, e.g. "game:plankstairs-oak-up-north-free".
    public string? Step;
    // Vanilla's own vertical-horizontal naming, e.g. "up-north".
    public string? StepOrientation;

    internal bool OpenPartFilled => Deck != null || OuterDeck != null || Step != null;

    internal (string? Deck, string? Infill, string? Front, string? Back, string? FrontStyle, string? BackStyle) DeckLayers(DeckHalf half)
        => half == DeckHalf.Outer
            ? (OuterDeck, OuterDeckInfill, OuterDeckFront, OuterDeckBack, OuterDeckFrontStyle, OuterDeckBackStyle)
            : (Deck, DeckInfill, DeckFront, DeckBack, DeckFrontStyle, DeckBackStyle);

    public override void ToTreeAttributes(ITreeAttribute tree)
    {
        base.ToTreeAttributes(tree);
        tree.SetString("framing", Framing);
        tree.SetString("infill", Infill);
        tree.SetString("front", Front);
        tree.SetString("secondfront", SecondFront);
        tree.SetString("back", Back);
        tree.SetString("secondback", SecondBack);
        tree.SetString("frontstyle", FrontStyle);
        tree.SetString("secondfrontstyle", SecondFrontStyle);
        tree.SetString("backstyle", BackStyle);
        tree.SetString("secondbackstyle", SecondBackStyle);
        tree.SetBool("splitback", true);
        tree.SetString("deck", Deck);
        tree.SetString("deckinfill", DeckInfill);
        tree.SetString("deckfront", DeckFront);
        tree.SetString("deckback", DeckBack);
        tree.SetString("deckfrontstyle", DeckFrontStyle);
        tree.SetString("deckbackstyle", DeckBackStyle);
        tree.SetString("outerdeck", OuterDeck);
        tree.SetString("outerdeckinfill", OuterDeckInfill);
        tree.SetString("outerdeckfront", OuterDeckFront);
        tree.SetString("outerdeckback", OuterDeckBack);
        tree.SetString("outerdeckfrontstyle", OuterDeckFrontStyle);
        tree.SetString("outerdeckbackstyle", OuterDeckBackStyle);
        tree.SetBool("legacydeck", LegacyDeck);
        tree.SetString("step", Step);
        tree.SetString("steporientation", StepOrientation);
    }

    // A client builds a restored wall twice before the server's data arrives: once predicting the
    // restore, once from the server's own SetBlock. Both start empty, and an empty wall draws as its
    // default shape, a full plank cube (#58); the guest record still at the cell holds the layers.
    public override void Initialize(ICoreAPI api)
    {
        base.Initialize(api);
        if (api.Side != EnumAppSide.Client || Framing != null || GuestWalls.GuestAt(api, Pos) is not { } guest) return;
        var tree = new TreeAttribute();
        guest.ToTreeAttributes(tree);
        FromTreeAttributes(tree, api.World);
    }

    public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldAccessForResolve)
    {
        base.FromTreeAttributes(tree, worldAccessForResolve);
        // Other clients learn of new layers only through this sync, so they relight here; Api is null on chunk load.
        int? oldAbsorption = Api?.Side == EnumAppSide.Client ? (Block as SidingWallBlock)?.AbsorptionOf(this) : null;
        var oldMesh = MeshState;
        Framing = NullIfEmpty(tree.GetString("framing", null));
        Infill = NullIfEmpty(tree.GetString("infill", null));
        Front = NullIfEmpty(tree.GetString("front", null));
        SecondFront = NullIfEmpty(tree.GetString("secondfront", null));
        Back = NullIfEmpty(tree.GetString("back", null));
        SecondBack = NullIfEmpty(tree.GetString("secondback", null));
        FrontStyle = NullIfEmpty(tree.GetString("frontstyle", null));
        SecondFrontStyle = NullIfEmpty(tree.GetString("secondfrontstyle", null));
        BackStyle = NullIfEmpty(tree.GetString("backstyle", null));
        SecondBackStyle = NullIfEmpty(tree.GetString("secondbackstyle", null));
        if (SharesLegacyBack(tree, Block?.Variant["layout"])) (SecondBack, SecondBackStyle) = (Back, BackStyle);
        if (oldAbsorption is int old && Block is SidingWallBlock wall && wall.AbsorptionOf(this) != old)
            wall.MarkAbsorptionChanged(worldAccessForResolve.BlockAccessor, Pos, old);
        Deck = NullIfEmpty(tree.GetString("deck", null));
        DeckInfill = NullIfEmpty(tree.GetString("deckinfill", null));
        DeckFront = NullIfEmpty(tree.GetString("deckfront", null));
        DeckBack = NullIfEmpty(tree.GetString("deckback", null));
        DeckFrontStyle = NullIfEmpty(tree.GetString("deckfrontstyle", null));
        DeckBackStyle = NullIfEmpty(tree.GetString("deckbackstyle", null));
        OuterDeck = NullIfEmpty(tree.GetString("outerdeck", null));
        OuterDeckInfill = NullIfEmpty(tree.GetString("outerdeckinfill", null));
        OuterDeckFront = NullIfEmpty(tree.GetString("outerdeckfront", null));
        OuterDeckBack = NullIfEmpty(tree.GetString("outerdeckback", null));
        OuterDeckFrontStyle = NullIfEmpty(tree.GetString("outerdeckfrontstyle", null));
        OuterDeckBackStyle = NullIfEmpty(tree.GetString("outerdeckbackstyle", null));
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

    // A corner saved before its inner faces split laid one back across both legs (decision 0090). Every load path
    // sets Block before this runs (CreateBehaviors, GuestWalls.Decode); a plain wall or post never gets the copy,
    // since ComputeDrops refunds SecondBack on every layout.
    internal static bool SharesLegacyBack(ITreeAttribute tree, string? layout)
        => layout == "cornerout" && !tree.HasAttribute("splitback");

    // Block.GetPlacedBlockInfo calls this inside a try/catch and appends the blockdesc- line
    // after it; SidingWallBlock's override takes that line back off without the build signal.
    public override void GetBlockInfo(IPlayer forPlayer, StringBuilder dsc)
    {
        base.GetBlockInfo(forPlayer, dsc);
        string? stepName = null;
        if (Step != null && Api.World.GetBlock(new AssetLocation(Step)) is { } stepBlock)
            stepName = stepBlock.GetHeldItemName(new ItemStack(stepBlock));
        bool outer = forPlayer?.CurrentBlockSelection is { } sel && sel.Position.Equals(Pos) && Block is SidingWallBlock wall
            && wall.HitDeckHalf(Api.World.BlockAccessor, sel, this) == DeckHalf.Outer;
        var (deck, deckInfill, deckFront, deckBack, deckFrontStyle, deckBackStyle) = DeckLayers(outer ? DeckHalf.Outer : DeckHalf.Inner);
        dsc.Append(SidingWallBlock.Describe(
            Framing, Infill, deck, Block.Attributes["Framings"], Block.Attributes["Infills"],
            Block.Variant["layout"], Block.Variant["side"], Front, SecondFront, Back, Block.Attributes["Finishes"],
            SidingWallBlock.LangEntry, stepName, deckInfill, deckFront, deckBack,
            SidingWallBlock.HasBuildSignal(forPlayer), Api != null && Pos != null ? Buried : null, SecondBack,
            FrontStyle, SecondFrontStyle, BackStyle, SecondBackStyle, deckFrontStyle, deckBackStyle));
        // A bare deck answers a click on the top, so its hint wins over the wall's.
        if (deck != null && deckInfill == null)
            SidingWallBlock.AppendFillHint(dsc, forPlayer, "floor", Block.Attributes["Framings"], Block.Attributes["Infills"]);
        else if (Infill == null)
            SidingWallBlock.AppendFillHint(dsc, forPlayer, Block.Variant["layout"], Block.Attributes["Framings"], Block.Attributes["Infills"]);
    }

    private bool Buried(string direction)
    {
        BlockFacing facing = BlockFacing.FromCode(direction);
        return Api.World.BlockAccessor.GetBlock(Pos.AddCopy(facing)).SideOpaque[facing.Opposite.Index] || FrontClosed(direction)
            || (Block.Variant["layout"] == "cornerin" && SidingWallBlock.WallAt(Api.World.BlockAccessor, Pos.AddCopy(facing)) != null);
    }

    // A bare front facing another wall's front is closed in this wall's own infill, glazing and a bare frame excepted (decision 0083).
    private bool FrontClosed(string direction)
        => Infill != null && !SidingWallBlock.IsTransparent(Infill, Block.Attributes["Infills"])
            && SidingWallBlock.WallAt(Api.World.BlockAccessor, Pos.AddCopy(BlockFacing.FromCode(direction))) is var (wall, _)
            && SidingWallBlock.FrontsFaceEachOther(Block.Variant["layout"], Block.Variant["side"], direction, wall.Variant["layout"], wall.Variant["side"]);

    // Everything OnTesselation reads off this entity, which is exactly what CacheKey covers.
    private ((string?, string?, string?, string?, string?, string?, string?, string?, string?, string?, string?), (string?, string?, string?, string?, string?, string?, string?), string) MeshState
        => ((Framing, Infill, Front, SecondFront, Back, FrontStyle, SecondFrontStyle, BackStyle, Deck, Step, StepOrientation),
            (DeckInfill, DeckFront, DeckBack, DeckFrontStyle, DeckBackStyle, SecondBack, SecondBackStyle), OuterDeckState);

    private string OuterDeckState => $"{OuterDeck}-{OuterDeckInfill}-{OuterDeckFront}-{OuterDeckBack}-{OuterDeckFrontStyle}-{OuterDeckBackStyle}";

    // A ToBytes/FromBytes round trip (chunk save/reload, client sync) turns a null
    // SetString value into "" - normalize back to null so "unbuilt" survives a reload.
    internal static string? NullIfEmpty(string? value) => string.IsNullOrEmpty(value) ? null : value;

    // "wall", "cornerout" and "diagonal" each have their own layered shape file (same four element
    // names - front/framing/infill/back - so selectiveElements works identically on
    // any). A cornerout wraps both claimed faces (decision 0002's CorneroutSecondFace)
    // with a shared Framing/Infill but its own SecondFront (decision 0009) and SecondBack
    // (decision 0090) - each leg's faces finish independently.
    public override bool OnTesselation(ITerrainMeshPool mesher, ITesselatorAPI tesselator)
    {
        string layout = Block.Variant["layout"];
        if (layout is not ("wall" or "cornerout" or "diagonal" or "cornerin")) return false;
        if (Api is not ICoreClientAPI capi) return false;

        var joins = ((SidingWallBlock)Block).NeighbourJoins(Api.World.BlockAccessor, Pos, Infill);

        // An empty selectiveElements array matches zero shape elements, not "no filter", and any
        // built cell names at least one - glazing merged on every side still draws its pane. So
        // empty means nothing is built, and the block's default JSON shape stands in.
        bool glazed = SidingWallBlock.IsTransparent(Infill, Block.Attributes["Infills"]);
        string side = Block.Variant["side"];
        bool stepped = Step != null && layout is "wall" or "cornerout";
        Block? stair = stepped ? Api.World.GetBlock(new AssetLocation(Step)) : null;
        (string[] stepElements, float stepRotation) = stepped
            ? StepMesh(layout, side, StepOrientation, StepFamily(stair?.Shape?.Base?.Path))
            : ([], 0);
        int alternate = CellAlternate(Block.Attributes, SidingWallTexSource.Alternate(Pos), Framing, [Deck, OuterDeck],
            [Infill, DeckInfill, OuterDeckInfill], [Front, SecondFront, Back, SecondBack, DeckFront, DeckBack, OuterDeckFront, OuterDeckBack],
            SidingWallTexSource.Varies(SidingWallTexSource.StairTexture(stair)),
            [FrontStyle, SecondFrontStyle, BackStyle, SecondBackStyle, DeckFrontStyle, DeckBackStyle, OuterDeckFrontStyle, OuterDeckBackStyle]);
        string[] innerCorners = ((SidingWallBlock)Block).InnerCorners(Api.World.BlockAccessor, Pos, Infill, joins);
        var glassRunsOn = ((SidingWallBlock)Block).GlassRunsOn(Api.World.BlockAccessor, Pos, Infill, joins);
        var tees = ((SidingWallBlock)Block).Tees(Api.World.BlockAccessor, Pos);
        string[] frontDirections = SidingWallBlock.FrontDirections(layout, side);
        var closed = (front: frontDirections.Length > 0 && FrontClosed(frontDirections[0]), second: frontDirections.Length > 1 && FrontClosed(frontDirections[1]));
        string[] selectiveElements = SelectiveElements(layout, Framing, Infill, Front, SecondFront, Back, Block.Attributes["Finishes"], joins, glazed, Styles,
            frame: FrameElements(Block.Attributes["Framings"], Framing, alternate), decked: Deck != null, innerCorners: innerCorners, glassRunsOn: glassRunsOn, tees: tees, closed: closed,
            secondBack: SecondBack, secondBackStyle: SecondBackStyle, fill: InfillElements(Block.Attributes["Infills"], Infill));

        // The deck is laid like a floor, in world directions, so its groups are tesselated unrotated.
        bool deckGlazed = SidingWallBlock.IsTransparent(DeckInfill, Block.Attributes["Infills"]);
        var deckJoins = SidingFloorBlock.Joins(Api.World.BlockAccessor, Pos, deckGlazed);
        string[] deckInnerCorners = SidingFloorBlock.InnerCorners(Api.World.BlockAccessor, Pos, deckGlazed, deckJoins);
        bool capped = Capped(layout, DeckFront, SidingWallBlock.FreeAbove(Api.World.BlockAccessor, Pos));
        string[] deckElements = DeckElements(side, Deck, DeckInfill, DeckFront, DeckBack, Block.Attributes["Finishes"], deckJoins, (DeckFrontStyle, DeckBackStyle), deckGlazed,
            FrameElements(Block.Attributes["Framings"], Deck, alternate), Ledges(layout, Back == null, glazed, joins, SecondBack == null), deckInnerCorners, capped,
            InfillElements(Block.Attributes["Infills"], DeckInfill));
        bool outerGlazed = SidingWallBlock.IsTransparent(OuterDeckInfill, Block.Attributes["Infills"]);
        var allJoins = SidingFloorBlock.Joins(Api.World.BlockAccessor, Pos, outerGlazed);
        var outerJoins = (allJoins.above && SidingWallBlock.ClaimsFace(layout, side, "north"), allJoins.below && SidingWallBlock.ClaimsFace(layout, side, "south"),
            allJoins.left && SidingWallBlock.ClaimsFace(layout, side, "west"), allJoins.right && SidingWallBlock.ClaimsFace(layout, side, "east"));
        string[] outerElements = DeckElements(side, OuterDeck, OuterDeckInfill, OuterDeckFront, OuterDeckBack, Block.Attributes["Finishes"], outerJoins, (OuterDeckFrontStyle, OuterDeckBackStyle),
            outerGlazed, FrameElements(Block.Attributes["Framings"], OuterDeck, alternate), fill: InfillElements(Block.Attributes["Infills"], OuterDeckInfill), group: "outerdeck");
        if (selectiveElements.Length == 0 && deckElements.Length == 0 && outerElements.Length == 0 && stepElements.Length == 0) return false;

        string cacheKey = CacheKey(layout, side, Framing, Infill, Front, SecondFront, Back, joins, Styles, Deck, Step, StepOrientation,
            DeckInfill, DeckFront, DeckBack, (DeckFrontStyle, DeckBackStyle), deckJoins, alternate, innerCorners, deckInnerCorners, glassRunsOn, tees, closed, capped,
            SecondBack, SecondBackStyle, OuterDeckState + outerJoins);

        MeshData[] meshes = ObjectCacheUtil.GetOrCreate(capi, cacheKey, () =>
        {
            Shape shape = ParsedShape(capi, $"wall/{layout}");
            var texSource = new SidingWallTexSource(
                capi, this, Block.Attributes["Framings"], Block.Attributes["Infills"], Block.Attributes["Finishes"], alternate);
            var rotation = new Vec3f(0, RotationYDeg(side), 0);
            return Meshes(tesselator, shape, texSource, rotation, selectiveElements, glazed)
                .Concat(Meshes(tesselator, shape, texSource, new Vec3f(0, 0, 0), deckElements, deckGlazed))
                .Concat(Meshes(tesselator, shape, texSource, new Vec3f(0, 0, 0), outerElements, outerGlazed))
                .Concat(Meshes(tesselator, shape, texSource, new Vec3f(0, stepRotation, 0), stepElements, false))
                .ToArray();
        });

        foreach (MeshData mesh in meshes) mesher.AddMeshData(mesh);
        return true;
    }

    // Shape.TryGet parses the whole file on every call, and a mesh is built once per distinct cell, so
    // each shape is parsed once and kept.
    internal static Shape ParsedShape(ICoreClientAPI capi, string path)
        => ObjectCacheUtil.GetOrCreate(capi, "vssiding-shape-" + path, () => Shape.TryGet(capi, new AssetLocation("vssiding", $"shapes/block/{path}.json")));

    // The position hash has three readers: the texture source picks each wildcard texture's variant by it,
    // FrameElements picks the rough frame's variant by its parity, and the mesh cache keys on it. So a
    // cell keeps the whole hash while any layer's texture varies, the parity alone for a rough wall or
    // deck frame, and nothing otherwise, which is what lets a long plain wall share one mesh.
    internal static int CellAlternate(
        JsonObject attributes, int hash, string? framing, string?[] decks, string?[] infills, string?[] finishes, bool stepVaries = false,
        string?[]? styles = null)
    {
        // A tiled face keeps its tile kinds in its style, so those count as finishes too.
        string?[] kinds = [.. finishes, .. finishes.Zip(styles ?? []).Where(face => face.First == TilesKey).SelectMany(face => TileSlots(face.Second)).Where(kind => kind != "")];
        string?[] frames = [framing, .. decks];
        if (stepVaries || SidingWallTexSource.AnyVaries(attributes["Framings"], frames)
            || SidingWallTexSource.AnyVaries(attributes["Infills"], infills)
            || SidingWallTexSource.AnyVaries(attributes["Finishes"], kinds)) return hash;
        return frames.Any(frame => FrameElements(attributes["Framings"], frame) != "framing") ? hash % 2 : 0;
    }

    // The shape-group prefix a framing entry draws its frame with; most draw the plain "framing" groups.
    // A rough frame has a second variant, named with a trailing 2, that odd cells take.
    internal static string FrameElements(JsonObject framings, string? framing, int alternate = 0)
    {
        string frame = framing == null ? "framing" : framings[framing]["Elements"].AsString("framing");
        return frame != "framing" && alternate % 2 == 1 ? frame + "2" : frame;
    }

    // The shape-group prefix an infill entry draws with, as a framing's Elements names its frame's.
    internal static string InfillElements(JsonObject infills, string? infill)
        => infill == null ? "infill" : infills[infill]["Elements"].AsString("infill");

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
    {
        if (name.StartsWith("deck-") || name.StartsWith("ledge")) name = name[(name.IndexOf('-', name.IndexOf('-') + 1) + 1)..];
        return name.StartsWith("infill");
    }

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
        (bool above, bool below, bool left, bool right) deckJoins = default, int alternate = 0,
        string[]? innerCorners = null, string[]? deckInnerCorners = null, (bool left, bool right) glassRunsOn = default, (bool left, bool right) tees = default,
        (bool front, bool second) closed = default, bool capped = false, string? secondBack = null, string? secondBackStyle = null, string? outerDeckState = null)
        => $"vssiding-wall-mesh-{layout}-{side}-{framing}-{infill}-{front}-{secondFront}-{back}-{joins.above}-{joins.below}-{joins.left}-{joins.right}-{styles.front}-{styles.secondFront}-{styles.back}-{deck}-{step}-{stepOrientation}"
            + $"-{deckInfill}-{deckFront}-{deckBack}-{deckStyles.front}-{deckStyles.back}-{deckJoins.above}-{deckJoins.below}-{deckJoins.left}-{deckJoins.right}-{alternate}"
            + $"-{string.Concat(innerCorners ?? [])}-{string.Concat(deckInnerCorners ?? [])}-{glassRunsOn.left}-{glassRunsOn.right}-{tees.left}-{tees.right}-{closed.front}-{closed.second}-{capped}-{secondBack}-{secondBackStyle}-{outerDeckState}";

    // The floor's groups for the deck, which the wall shapes carry per side as deck-{side}-{name}. The
    // deck stops a voxel short of the frame, at the slot the wall's room-side finish takes, so the shapes
    // carry the same groups again for that slot, one copy for each name Ledges gives.
    internal static string[] DeckElements(
        string side, string? deck, string? infill, string? front, string? back, JsonObject finishes,
        (bool above, bool below, bool left, bool right) joins, (string? front, string? back) styles, bool glazed,
        string frame = "framing", string[]? ledges = null, string[]? innerCorners = null, bool capped = false, string fill = "infill", string group = "deck")
    {
        if (deck == null) return [];
        string[] names = SidingFloorEntity.SelectiveElements(deck, infill, front, back, finishes, joins, styles, glazed, frame, innerCorners, fill);
        string[] copies = [group, .. ledges ?? []];
        // The top finish is the first name, and a glazed deck's is a rim round the glass, which has no top to lay over the wall.
        string[] top = front == TilesKey ? [.. TileGroups("front", styles.front)] : [names[0]];
        string[] cap = capped && front != null && !glazed ? [.. top.Select(name => $"cap-{side}-{name}")] : [];
        return [.. copies.SelectMany(copy => names.Select(name => $"{copy}-{side}-{name}")), .. cap];
    }

    // A decked wall or corner with nothing standing on it shows the deck's top finish over its own 4/16 top, so the floor
    // reads as one surface (decision 0085). A diagonal's deck is strips round its run, which the cap does not follow.
    internal static bool Capped(string layout, string? deckFront, bool freeAbove) => layout != "diagonal" && deckFront != null && freeAbove;

    // The copies of the deck that reach into the slot the wall's room-side finish takes. While that side
    // is bare the whole slot is open, and the deck draws its ledge. A finish fills the slot, but a glazed
    // wall's is only a rim, which leaves the slot open across the glass, so there the ledge carries on in
    // the place of the rim's top: between the stiles, and out to the cell's edge where a stile has merged
    // away. A diagonal's deck draws none (0067). A corner's second inner leg has ledge copies of its own, which
    // follow its own face (decision 0090).
    internal static string[] Ledges(
        string layout, bool roomSideBare, bool glazed, (bool above, bool below, bool left, bool right) joins, bool secondRoomSideBare = false)
    {
        if (layout is "diagonal" or "cornerin") return [];
        List<string> Leg(string prefix, bool bare)
        {
            if (bare) return [prefix];
            if (!glazed) return [];
            var copies = new List<string> { prefix + "top" };
            if (joins.left) copies.Add(prefix + "topleft");
            if (joins.right) copies.Add(prefix + "topright");
            return copies;
        }
        return [.. Leg("ledge", roomSideBare), .. layout == "cornerout" ? Leg("ledgesecond", secondRoomSideBare) : []];
    }

    // The suffixes of the frame pieces a cell draws, which a glazed face's rims follow part for part.
    // A wall's two posts drop one by one wherever glazing merges sideways; a glazed cornerout's corner
    // post is always drawn and its two end posts are named like a wall's stiles. A plain wall's plates
    // stop short at its posts, which leaves a notch at the cell's edge once a post drops, so glazing
    // gets its own frame: stiles and the rails between them, overlapping nothing, and a filler in the
    // corner where a stile has merged away. An opaque cornerout has no end posts of its own, and a
    // glazed diagonal's rails run between its corner fillers, which are its stiles. A filler also stands
    // alone in each of `innerCorners`, where rail and stile have both merged away and the sheet turns.
    internal static List<string> FrameParts(
        string layout, (bool above, bool below, bool left, bool right) joins, bool glazed, string[]? innerCorners = null)
    {
        if (layout == "cornerin") return [""];
        bool fillers = glazed && layout != "diagonal";
        var parts = new List<string>();
        if (layout == "cornerout") parts.Add("");
        if (layout == "wall" || fillers)
        {
            if (!joins.left) parts.Add("-left");
            if (!joins.right) parts.Add("-right");
        }
        foreach (var (joined, rail) in new[] { (joins.above, "-top"), (joins.below, "-bottom") })
        {
            if (joined) continue;
            parts.Add(rail);
            if (fillers && joins.left) parts.Add(rail + "-left");
            if (fillers && joins.right) parts.Add(rail + "-right");
        }
        if (fillers && innerCorners != null) parts.AddRange(innerCorners);

        return parts;
    }

    internal static IEnumerable<string> RimNames(string group, IEnumerable<string> parts) => parts.Select(part => $"{group}-rim{part}");

    // Unbuilt parts (null key) are left out so a frame-only wall shows just its frame.
    // A finish can name its own element per face (decision 0007) instead of the plain slab.
    // A join between stacked cells has no plates, so the infill extends across it (decision 0008).
    internal static string[] SelectiveElements(
        string layout, string? framing, string? infill, string? front, string? secondFront, string? back, JsonObject finishes,
        (bool above, bool below, bool left, bool right) joins, bool glazed,
        (string? front, string? secondFront, string? back) styles = default, string frame = "framing", bool decked = false,
        string[]? innerCorners = null, (bool left, bool right) glassRunsOn = default, (bool left, bool right) tees = default,
        (bool front, bool second) closed = default, string? secondBack = null, string? secondBackStyle = null, string fill = "infill")
    {
        if (layout == "cornerin") return PostElements(framing, finishes, [(front, styles.front), (secondFront, styles.secondFront), (back, styles.back), (secondBack, secondBackStyle)], frame);
        var names = new List<string>();
        // A diagonal's shape has bamboo's ringed groups but no rough frame's, so any other framing draws the plain ones
        // (decisions 0066, 0092).
        bool diagonal = layout == "diagonal";
        if (diagonal && !frame.StartsWith("bamboo")) frame = "framing";
        string member = glazed ? "glazing" : frame;
        var parts = FrameParts(layout, joins, glazed, innerCorners);
        // A corner's post stands a finish slot in from the cell's edge, so at a T it fills the slot the run crosses: its
        // left leg's at its left end, its right leg's at its right (decision 0080). A glazed face's rim gives way to it, but
        // an opaque finish is one slab that fills the slot itself, so an opaque post fills it only while that face is bare.
        bool cornerout = layout == "cornerout";
        if (cornerout && tees.left && (glazed || secondFront == null)) parts.Add("-tee-left");
        if (cornerout && tees.right && (glazed || front == null)) parts.Add("-tee-right");

        // A glazed cell's finish is a rim round the glass: its group cut to each part the frame draws that the
        // group keeps.
        var rims = new List<(string group, System.Func<string, bool> keeps)>();
        void AddFinish(string group, System.Func<string, bool>? keeps = null)
        {
            keeps ??= _ => true;
            if (glazed)
            {
                rims.Add((group, keeps));
                names.AddRange(RimNames(group, parts.Where(keeps)));
            }
            else names.Add(group);
        }

        // A corner's front finish rims leg 1, whose far end is its right, and its second front leg 2, whose is its left,
        // and its corner post rim gives way to the rims either side of a T's piece in its slot.
        bool OnLeg(string part, string otherLeg, bool tee) => !part.EndsWith(otherLeg) && !(part == "" && tee);
        // A tiled face draws the groups of the slots it has filled instead, none under glazing, which takes no tiles.
        bool Tiles(string? key, string group, string? style, string suffix = "")
        {
            if (key != TilesKey) return false;
            if (!glazed) names.AddRange(TileGroups(group, style).Select(name => name + suffix));
            return true;
        }
        if (front != null && !Tiles(front, "front", styles.front))
            AddFinish(FinishElement(finishes, front, "front", styles.front), cornerout ? part => OnLeg(part, "-left", tees.right) : null);
        if (secondFront != null)
        {
            string group = "second" + FinishElement(finishes, secondFront, "front", styles.secondFront);
            if (!Tiles(secondFront, "secondfront", styles.secondFront))
                AddFinish(group, cornerout ? part => OnLeg(part, "-right", tees.left) : null);
            // A bare front leaves the outside corner's column open, and the second front carries on over it, where no facing
            // front's infill fills it (decisions 0083, 0090). Its glazed rim is the corner post's piece alone.
            if (cornerout && front == null && !(closed.front && infill != null && !glazed))
            {
                if (!Tiles(secondFront, "secondfront", styles.secondFront, "-corner"))
                    AddFinish(group + "-corner", part => part is "" or "-tee-left" && OnLeg(part, "-right", tees.left));
            }
        }
        if (framing != null) names.AddRange(parts.Select(part => member + part));
        if (infill != null)
        {
            // A bare front facing another wall's front draws in the infill, so the pair reads as one wall (decision 0083).
            if (!glazed && front == null && closed.front) names.Add("infill-front");
            if (!glazed && secondFront == null && closed.second) names.Add("infill-secondfront");
            // Glazing is one flat pane spanning the whole cell rather than a slab plus fillers.
            // Three stacked boxes share a face at each seam, and two coincident transparent quads
            // blend twice over - a bright line exactly where a cross-beam would be. One pane also
            // meets the pane above it edge on, so a glazed stack has no seams at all.
            if (glazed) names.Add("infill-pane");
            else
            {
                names.Add(fill);
                if (joins.above) names.Add($"{fill}-top");
                if (joins.below) names.Add($"{fill}-bottom");
            }
        }
        // The top of the room side's rim lies in a deck's height, where the deck's ledge takes its place (Ledges), and a T's
        // piece lies in a front slot, clear of it. A framed pattern takes a corner's `-whole` copy, which fits its border onto
        // each 12-voxel leg (decision 0086). A corner's back rims leg 1 and its second back leg 2, the corner post's
        // piece included (decision 0090).
        bool Keeps(string part) => !part.StartsWith("-tee") && !(decked && part.StartsWith("-top"));
        string Whole(string key) => cornerout && finishes[key]["WholeOnCorners"].AsBool() ? "-whole" : "";
        if (back != null && !Tiles(back, "back", styles.back))
            AddFinish(FinishElement(finishes, back, "back", styles.back) + Whole(back), cornerout ? part => Keeps(part) && part != "" && !part.EndsWith("-left") : Keeps);
        if (cornerout && secondBack != null && !Tiles(secondBack, "secondback", secondBackStyle))
            AddFinish("second" + FinishElement(finishes, secondBack, "back", secondBackStyle) + Whole(secondBack), part => Keeps(part) && !part.EndsWith("-right"));
        // A diagonal's end is the filler in its corner, or, where the next cell carries the run on,
        // each group's own piece out to the corner and half a post there.
        if (diagonal)
        {
            string[] panel = names.ToArray();
            // A glazed diagonal's rails are the glazing's, so its ends stay the plain frame's whatever the framing.
            string post = glazed ? "framing" : frame;
            foreach (var (runsOn, glassOn, end) in new[] { (joins.left, glassRunsOn.left, "left"), (joins.right, glassRunsOn.right, "right") })
            {
                if (runsOn) names.AddRange(panel.Select(name => $"{name}-{end}"));
                if (glassOn)
                {
                    // Where the glass runs on the post is gone: the rails and the pane carry on through it and the
                    // rims follow the rails only. A rail merged away still crosses it at an inside corner, as a wall's filler does.
                    string[] rails = [.. new[] { "-top", "-bottom" }.Where(rail => parts.Contains(rail) || (innerCorners ?? []).Contains($"{rail}-{end}"))];
                    foreach (var (group, keeps) in rims) names.AddRange(rails.Where(keeps).Select(rail => $"{group}-rim{rail}-join-{end}"));
                    if (framing != null) names.AddRange(rails.Select(rail => $"glazing{rail}-join-{end}"));
                    if (infill != null) names.Add($"infill-pane-join-{end}");
                    continue;
                }
                // A glazed diagonal's rims also cover the half post's front, full height as a wall's stile rim does.
                if (runsOn) names.AddRange(rims.Select(rim => $"{rim.group}-rim-join-{end}"));
                if (framing != null) names.Add(runsOn ? $"{post}-join-{end}" : $"{post}-{end}");
            }
        }
        return names.ToArray();
    }

    // A post's core, and on each face, front, secondfront, back and secondback, its finish's layer or, bare, the framing's,
    // with the mitre piece each layer meets the next with (decision 0082). Every face takes the front's groups.
    // A bamboo post is one round pole, so a bare face has no mitre, and a finished face's mitre lies over the previous
    // face's body, where it would float over the pole while that face is bare.
    internal static string[] PostElements(string? framing, JsonObject finishes, (string? key, string? style)[] faces, string frame = "framing")
    {
        if (framing == null) return [];
        string[] slots = ["front", "secondfront", "back", "secondback"];
        bool bamboo = frame.StartsWith("bamboo");
        string member = bamboo ? frame : "framing";
        var names = new List<string> { member };
        for (int i = 0; i < slots.Length; i++)
        {
            var (key, style) = faces[i];
            bool mitre = !bamboo || (key != null && faces[(i + 3) % 4].key != null);
            if (key == TilesKey)
            {
                foreach (string tile in TileGroups(slots[i], style)) names.AddRange(mitre ? [tile, tile + "-mitre"] : [tile]);
                continue;
            }
            string group = key == null ? $"{member}-{slots[i]}" : slots[i] + FinishElement(finishes, key, "front", style)["front".Length..];
            names.Add(group);
            if (mitre) names.Add(group + "-mitre");
        }
        return [.. names];
    }

    // A tiled face's finish key, and its four slots, kept in the face's style as comma-separated tile kinds with an
    // empty entry for an empty slot. Slot n is row n / 2 (0 the lower half), column n % 2 (decision 0095).
    internal const string TilesKey = "tiles";

    internal static string[] TileSlots(string? style)
    {
        string[] kinds = style?.Split(',') ?? [];
        return [.. Enumerable.Range(0, 4).Select(slot => slot < kinds.Length ? kinds[slot] : "")];
    }

    internal static string WithTile(string? style, int slot, string kind)
    {
        string[] slots = TileSlots(style);
        slots[slot] = kind;
        return string.Join(",", slots);
    }

    // A face that becomes a corner's second leg has its columns on the other axis, whose lower end lies at the opposite
    // world end for every side the upgrade can turn to, so its columns swap and each tile stays where it was.
    internal static string? TurnTiles(string? key, string? style)
    {
        if (key != TilesKey) return style;
        string[] slots = TileSlots(style);
        return string.Join(",", slots[1], slots[0], slots[3], slots[2]);
    }

    internal static IEnumerable<string> TileGroups(string group, string? style)
        => TileSlots(style).Select((kind, slot) => kind == "" ? null : $"{group}-tile{slot}").OfType<string>();

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

    // Vanilla's rotateY for a stair facing, and the order +90 carries a direction through: west, south, east, north.
    internal static readonly Dictionary<string, int> StairRotationByFacing =new() { ["north"] = 0, ["west"] = 90, ["south"] = 180, ["east"] = 270 };
    private static readonly string[] TurnOrder = ["west", "south", "east", "north"];

    // Which of vanilla's stair shapes a step continues, read off the stair block's shape path; a modded shape draws as planks.
    // Loading a shape rewrites its AssetLocation in place to shapes/....json, so the path may come either way.
    internal static string StepFamily(string? shapePath)
    {
        string name = shapePath == null ? "" : shapePath[(shapePath.LastIndexOf('/') + 1)..].Replace(".json", "");
        return name.StartsWith("stonepath") ? "stonepath" : name is "normal" or "flipped" ? "basic" : "planks";
    }

    // The step drawn in its stair's frame (decision 0081): the element names are the stair-north clips, and the
    // rotation is the stair facing's rather than the wall side's, so neighbouring steps share a texture phase.
    // The hand is where the wall's panel sits once the facing's rotation is undone. A facing across the wall has none.
    // A corner has two panels: the one that lands west or east gives the hand, and the other lands north (front)
    // or south (back), the end of the step its second leg closes.
    internal static (string[] Names, float Rotation) StepMesh(string layout, string side, string? orientation, string family)
    {
        if (StepFrame(layout, side, orientation) is not { } frame) return ([], 0);
        var (vertical, hand, end, rotation) = frame;
        string prefix = $"step-{family}-{(family == "stonepath" ? "up" : vertical)}-{hand}{(end == null ? "" : "-" + end)}";
        return ([$"{prefix}-base", $"{prefix}-step"], rotation);
    }

    // Where a step sits in its stair's frame: the hand and end (null on a wall, front or back on a corner) its open
    // part is clipped to, and the facing's rotation. Null when the facing runs across the wall. The collision boxes
    // of a corner step are cut from the same frame.
    internal static (string Vertical, string Hand, string? End, int Rotation)? StepFrame(string layout, string side, string? orientation)
    {
        string[] parts = orientation?.Split('-') ?? [];
        if (parts.Length != 2 || !StairRotationByFacing.TryGetValue(parts[1], out int rotation)) return null;
        string[] turned = [.. TurnOrder.Where(face => SidingWallBlock.ClaimsFace(layout, side, face))
            .Select(face => TurnOrder[((Array.IndexOf(TurnOrder, face) - rotation / 90) % 4 + 4) % 4])];
        string? hand = turned.Contains("west") ? "left" : turned.Contains("east") ? "right" : null;
        if (hand == null) return null;
        string? end = turned.Contains("north") ? "front" : turned.Contains("south") ? "back" : null;
        return (parts[0], hand, end, rotation);
    }

    // Collision only: the mesh draws through StepMesh. "up-F" rises to a full lower half plus an upper half on the
    // F side; "down-F" is the mirror. F has to run along the wall - the wall's own normal axis (e.g. east/west for
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
