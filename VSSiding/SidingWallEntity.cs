using System;
using System.Collections.Generic;
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

        // A chunk can be meshed before its block entities arrive, and nothing else redraws it
        // afterwards: OnTesselation reads null state, returns false, and the cell keeps the
        // block's default solid shape - flat slabs, no cladding relief - until something
        // unrelated disturbs it. Redrawing on the sync that brought the state is that something.
        if (Api?.Side == EnumAppSide.Client && MeshState != oldMesh)
        {
            worldAccessForResolve.BlockAccessor.MarkBlockDirty(Pos);
        }
    }

    // Block.GetPlacedBlockInfo already calls this inside a try/catch and appends the
    // blockdesc- line after it, so overriding here is the one hook rather than two.
    public override void GetBlockInfo(IPlayer forPlayer, StringBuilder dsc)
    {
        base.GetBlockInfo(forPlayer, dsc);
        dsc.Append(SidingWallBlock.Describe(
            Framing, Infill, Deck, Block.Attributes["Framings"], Block.Attributes["Infills"],
            Block.Variant["layout"], Block.Variant["side"], Front, SecondFront, Back, Block.Attributes["Finishes"],
            key => Lang.GetIfExists(key)));
    }

    // Everything OnTesselation reads off this entity, which is exactly what CacheKey covers.
    private (string?, string?, string?, string?, string?, string?, string?, string?, string?) MeshState
        => (Framing, Infill, Front, SecondFront, Back, FrontStyle, SecondFrontStyle, BackStyle, Deck);

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
        string[] selectiveElements = SelectiveElements(layout, Framing, Infill, Front, SecondFront, Back, Block.Attributes["Finishes"], joins, glazed, Styles, Deck);
        if (selectiveElements.Length == 0) return false;

        string side = Block.Variant["side"];
        string cacheKey = CacheKey(layout, side, Framing, Infill, Front, SecondFront, Back, joins, Styles, Deck);

        MeshData[] meshes = ObjectCacheUtil.GetOrCreate(capi, cacheKey, () =>
        {
            Shape shape = Shape.TryGet(capi, new AssetLocation("vssiding", $"shapes/block/wall/{layout}.json"));
            var texSource = new SidingWallTexSource(
                capi, this, Block.Attributes["Framings"], Block.Attributes["Infills"], Block.Attributes["Finishes"]);
            var rotation = new Vec3f(0, RotationYDeg(side), 0);

            if (!glazed)
                return new[] { Tesselate(tesselator, shape, texSource, rotation, selectiveElements) };

            // Glazing has to reach the transparent pool while its frame stays opaque, so the two
            // halves are tesselated and handed over separately. They are deliberately NOT merged
            // into one mesh: MeshData.AddMeshData offsets the incoming indices by the target's
            // last index value plus one, which is only the target's vertex count when that last
            // index is also its highest. Any trailing vertex without an index shifts the glass
            // indices back into the frame's vertices and smears a pane across the room.
            MeshData glass = Tesselate(tesselator, shape, texSource, rotation, Array.FindAll(selectiveElements, IsInfillElement));
            SetRenderPass(glass, EnumChunkRenderPass.Transparent);
            MeshData frame = Tesselate(tesselator, shape, texSource, rotation, Array.FindAll(selectiveElements, name => !IsInfillElement(name)));
            return new[] { frame, glass };
        });

        foreach (MeshData mesh in meshes) mesher.AddMeshData(mesh);
        return true;
    }

    private static MeshData Tesselate(
        ITesselatorAPI tesselator, Shape shape, ITexPositionSource texSource, Vec3f rotation, string[] selectiveElements)
    {
        tesselator.TesselateShape(
            "vssiding-wall", shape, out MeshData modeldata, texSource, rotation, 0, 0, 0, null, selectiveElements);
        return modeldata;
    }

    internal static bool IsInfillElement(string name) => name.StartsWith("infill");

    // TesselateShape already writes one entry per quad - ShapeElement.RenderPass, which defaults
    // to -1 and counts as opaque - so the frame half needs nothing and this only overwrites.
    // The per-quad count has to stay exact either way: AddMeshData appends the two lists in step
    // with the two vertex lists.
    private static void SetRenderPass(MeshData mesh, EnumChunkRenderPass pass)
    {
        for (int quad = 0; quad < mesh.RenderPassCount; quad++) mesh.RenderPassesAndExtraBits[quad] = (short)pass;
    }

    private (string? front, string? secondFront, string? back) Styles => (FrontStyle, SecondFrontStyle, BackStyle);

    internal static string CacheKey(
        string layout, string side, string? framing, string? infill, string? front, string? secondFront, string? back,
        (bool above, bool below, bool left, bool right) joins,
        (string? front, string? secondFront, string? back) styles = default, string? deck = null)
        => $"vssiding-wall-mesh-{layout}-{side}-{framing}-{infill}-{front}-{secondFront}-{back}-{joins.above}-{joins.below}-{joins.left}-{joins.right}-{styles.front}-{styles.secondFront}-{styles.back}-{deck}";

    // Unbuilt parts (null key) are left out so a frame-only wall shows just its frame.
    // A finish can name its own element per face (decision 0007) instead of the plain slab.
    // A join between stacked cells has no plates, so the infill extends across it (decision 0008).
    internal static string[] SelectiveElements(
        string layout, string? framing, string? infill, string? front, string? secondFront, string? back, JsonObject finishes,
        (bool above, bool below, bool left, bool right) joins, bool glazed,
        (string? front, string? secondFront, string? back) styles = default, string? deck = null)
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
        if (deck != null) names.Add("deck");
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
}
