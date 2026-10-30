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
        if (oldAbsorption is int old && Block is SidingWallBlock wall && wall.AbsorptionOf(this) != old)
            wall.MarkAbsorptionChanged(worldAccessForResolve.BlockAccessor, Pos, old);
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

    // Block.GetPlacedBlockInfo calls this inside a try/catch and appends the blockdesc- line
    // after it; SidingWallBlock's override takes that line back off without the build signal.
    public override void GetBlockInfo(IPlayer forPlayer, StringBuilder dsc)
    {
        base.GetBlockInfo(forPlayer, dsc);
        string? stepName = null;
        if (Step != null && Api.World.GetBlock(new AssetLocation(Step)) is { } stepBlock)
            stepName = stepBlock.GetHeldItemName(new ItemStack(stepBlock));
        dsc.Append(SidingWallBlock.Describe(
            Framing, Infill, Deck, Block.Attributes["Framings"], Block.Attributes["Infills"],
            Block.Variant["layout"], Block.Variant["side"], Front, SecondFront, Back, Block.Attributes["Finishes"],
            SidingWallBlock.LangEntry, stepName, DeckInfill, DeckFront, DeckBack,
            SidingWallBlock.HasBuildSignal(forPlayer), Api != null && Pos != null ? Buried : null));
    }

    private bool Buried(string direction)
    {
        BlockFacing facing = BlockFacing.FromCode(direction);
        return Api.World.BlockAccessor.GetBlock(Pos.AddCopy(facing)).SideOpaque[facing.Opposite.Index];
    }

    // Everything OnTesselation reads off this entity, which is exactly what CacheKey covers.
    private ((string?, string?, string?, string?, string?, string?, string?, string?, string?, string?, string?), (string?, string?, string?, string?, string?)) MeshState
        => ((Framing, Infill, Front, SecondFront, Back, FrontStyle, SecondFrontStyle, BackStyle, Deck, Step, StepOrientation),
            (DeckInfill, DeckFront, DeckBack, DeckFrontStyle, DeckBackStyle));

    // A ToBytes/FromBytes round trip (chunk save/reload, client sync) turns a null
    // SetString value into "" - normalize back to null so "unbuilt" survives a reload.
    internal static string? NullIfEmpty(string? value) => string.IsNullOrEmpty(value) ? null : value;

    // "wall", "cornerout" and "diagonal" each have their own layered shape file (same four element
    // names - front/framing/infill/back - so selectiveElements works identically on
    // any). A cornerout wraps both claimed faces (decision 0002's CorneroutSecondFace)
    // with a shared Framing/Infill/Back but its own SecondFront (decision 0009) - the
    // second leg's front faces a different room, so it finishes independently.
    public override bool OnTesselation(ITerrainMeshPool mesher, ITesselatorAPI tesselator)
    {
        string layout = Block.Variant["layout"];
        if (layout is not ("wall" or "cornerout" or "diagonal")) return false;
        if (Api is not ICoreClientAPI capi) return false;

        var joins = ((SidingWallBlock)Block).NeighbourJoins(Api.World.BlockAccessor, Pos, Infill);

        // An empty selectiveElements array matches zero shape elements, not "no filter", and any
        // built cell names at least one - glazing merged on every side still draws its pane. So
        // empty means nothing is built, and the block's default JSON shape stands in.
        bool glazed = SidingWallBlock.IsTransparent(Infill, Block.Attributes["Infills"]);
        string side = Block.Variant["side"];
        string[]? step = Step != null && layout == "wall" ? StepElements(side, StepOrientation) : null;
        int alternate = CellAlternate(Block.Attributes, SidingWallTexSource.Alternate(Pos), Framing, Deck,
            [Infill, DeckInfill], [Front, SecondFront, Back, DeckFront, DeckBack]);
        string[] innerCorners = ((SidingWallBlock)Block).InnerCorners(Api.World.BlockAccessor, Pos, Infill, joins);
        var glassRunsOn = ((SidingWallBlock)Block).GlassRunsOn(Api.World.BlockAccessor, Pos, Infill, joins);
        string[] selectiveElements = SelectiveElements(layout, Framing, Infill, Front, SecondFront, Back, Block.Attributes["Finishes"], joins, glazed, Styles, step,
            FrameElements(Block.Attributes["Framings"], Framing, alternate), decked: Deck != null, innerCorners: innerCorners, glassRunsOn: glassRunsOn);

        // The deck is laid like a floor, in world directions, so its groups are tesselated unrotated.
        bool deckGlazed = SidingWallBlock.IsTransparent(DeckInfill, Block.Attributes["Infills"]);
        var deckJoins = SidingFloorBlock.Joins(Api.World.BlockAccessor, Pos, deckGlazed);
        string[] deckInnerCorners = SidingFloorBlock.InnerCorners(Api.World.BlockAccessor, Pos, deckGlazed, deckJoins);
        string[] deckElements = DeckElements(side, Deck, DeckInfill, DeckFront, DeckBack, Block.Attributes["Finishes"], deckJoins, (DeckFrontStyle, DeckBackStyle), deckGlazed,
            FrameElements(Block.Attributes["Framings"], Deck, alternate), Ledges(layout, Back == null, glazed, joins), deckInnerCorners);
        if (selectiveElements.Length == 0 && deckElements.Length == 0) return false;

        string cacheKey = CacheKey(layout, side, Framing, Infill, Front, SecondFront, Back, joins, Styles, Deck, Step, StepOrientation,
            DeckInfill, DeckFront, DeckBack, (DeckFrontStyle, DeckBackStyle), deckJoins, alternate, innerCorners, deckInnerCorners, glassRunsOn);

        MeshData[] meshes = ObjectCacheUtil.GetOrCreate(capi, cacheKey, () =>
        {
            Shape shape = ParsedShape(capi, $"wall/{layout}");
            var texSource = new SidingWallTexSource(
                capi, this, Block.Attributes["Framings"], Block.Attributes["Infills"], Block.Attributes["Finishes"], alternate);
            var rotation = new Vec3f(0, RotationYDeg(side), 0);
            return Meshes(tesselator, shape, texSource, rotation, selectiveElements, glazed)
                .Concat(Meshes(tesselator, shape, texSource, new Vec3f(0, 0, 0), deckElements, deckGlazed))
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
        JsonObject attributes, int hash, string? framing, string? deck, string?[] infills, string?[] finishes)
    {
        if (SidingWallTexSource.AnyVaries(attributes["Framings"], framing, deck)
            || SidingWallTexSource.AnyVaries(attributes["Infills"], infills)
            || SidingWallTexSource.AnyVaries(attributes["Finishes"], finishes)) return hash;
        return FrameElements(attributes["Framings"], framing) != "framing" || FrameElements(attributes["Framings"], deck) != "framing" ? hash % 2 : 0;
    }

    // The shape-group prefix a framing entry draws its frame with; most draw the plain "framing" groups.
    // A rough frame has a second variant, named with a trailing 2, that odd cells take.
    internal static string FrameElements(JsonObject framings, string? framing, int alternate = 0)
    {
        string frame = framing == null ? "framing" : framings[framing]["Elements"].AsString("framing");
        return frame != "framing" && alternate % 2 == 1 ? frame + "2" : frame;
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
        string[]? innerCorners = null, string[]? deckInnerCorners = null, (bool left, bool right) glassRunsOn = default)
        => $"vssiding-wall-mesh-{layout}-{side}-{framing}-{infill}-{front}-{secondFront}-{back}-{joins.above}-{joins.below}-{joins.left}-{joins.right}-{styles.front}-{styles.secondFront}-{styles.back}-{deck}-{step}-{stepOrientation}"
            + $"-{deckInfill}-{deckFront}-{deckBack}-{deckStyles.front}-{deckStyles.back}-{deckJoins.above}-{deckJoins.below}-{deckJoins.left}-{deckJoins.right}-{alternate}"
            + $"-{string.Concat(innerCorners ?? [])}-{string.Concat(deckInnerCorners ?? [])}-{glassRunsOn.left}-{glassRunsOn.right}";

    // The floor's groups for the deck, which the wall shapes carry per side as deck-{side}-{name}. The
    // deck stops a voxel short of the frame, at the slot the wall's room-side finish takes, so the shapes
    // carry the same groups again for that slot, one copy for each name Ledges gives.
    internal static string[] DeckElements(
        string side, string? deck, string? infill, string? front, string? back, JsonObject finishes,
        (bool above, bool below, bool left, bool right) joins, (string? front, string? back) styles, bool glazed,
        string frame = "framing", string[]? ledges = null, string[]? innerCorners = null)
    {
        if (deck == null) return [];
        string[] names = SidingFloorEntity.SelectiveElements(deck, infill, front, back, finishes, joins, styles, glazed, frame, innerCorners);
        string[] copies = ["deck", .. ledges ?? []];
        return copies.SelectMany(copy => names.Select(name => $"{copy}-{side}-{name}")).ToArray();
    }

    // The copies of the deck that reach into the slot the wall's room-side finish takes. While that side
    // is bare the whole slot is open, and the deck draws its ledge. A finish fills the slot, but a glazed
    // wall's is only a rim, which leaves the slot open across the glass, so there the ledge carries on in
    // the place of the rim's top: between the stiles, and out to the cell's edge where a stile has merged
    // away. A diagonal's deck draws none (0067).
    internal static string[] Ledges(string layout, bool roomSideBare, bool glazed, (bool above, bool below, bool left, bool right) joins)
    {
        if (layout == "diagonal") return [];
        if (roomSideBare) return ["ledge"];
        if (!glazed) return [];
        var ledges = new List<string> { "ledgetop" };
        if (joins.left) ledges.Add("ledgetopleft");
        if (joins.right) ledges.Add("ledgetopright");
        return ledges.ToArray();
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
        (string? front, string? secondFront, string? back) styles = default, string[]? step = null, string frame = "framing", bool decked = false,
        string[]? innerCorners = null, (bool left, bool right) glassRunsOn = default)
    {
        var names = new List<string>();
        // A diagonal's shape has no rough frame groups, so it draws the plain ones whatever the
        // framing's Elements say (decision 0066).
        bool diagonal = layout == "diagonal";
        if (diagonal) frame = "framing";
        string member = glazed ? "glazing" : frame;
        var parts = FrameParts(layout, joins, glazed, innerCorners);

        // A glazed wall's or corner's finish is a rim round the glass: its group cut to each part the frame draws
        // that the group keeps.
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

        // A corner's front finish rims leg 1, whose far end is its right, and its second front leg 2, whose is its left.
        bool cornerout = layout == "cornerout";
        if (front != null) AddFinish(FinishElement(finishes, front, "front", styles.front), cornerout ? part => !part.EndsWith("-left") : null);
        if (secondFront != null)
            AddFinish("second" + FinishElement(finishes, secondFront, "front", styles.secondFront), cornerout ? part => !part.EndsWith("-right") : null);
        if (framing != null) names.AddRange(parts.Select(part => member + part));
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
        // The top of the room side's rim lies in a deck's height, where the deck's ledge takes its place (Ledges).
        if (back != null) AddFinish(FinishElement(finishes, back, "back", styles.back), decked ? part => !part.StartsWith("-top") : null);
        // A diagonal's end is the filler in its corner, or, where the next cell carries the run on,
        // each group's own piece out to the corner and half a post there.
        if (diagonal)
        {
            string[] panel = names.ToArray();
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
                if (framing != null) names.Add(runsOn ? $"framing-join-{end}" : $"framing-{end}");
            }
        }
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
