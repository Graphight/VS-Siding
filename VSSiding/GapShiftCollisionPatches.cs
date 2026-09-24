using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Runtime.CompilerServices;
using HarmonyLib;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace VSSiding;

// Furniture-against-thin-walls (decision 0035): a hosted block renders shifted off its guest wall's
// panel (SidingModSystem's TesselateBlock transpiler), so it must collide and select where it's drawn, not
// where its cell's true bounds are - and it must also collide against the panel it's sitting beside,
// which its own boxes never describe. EveryOverridePatches patches GetCollisionBoxes/
// GetParticleCollisionBoxes/GetSelectionBoxes on every declaring override across loaded assemblies,
// so a mod's own block subclass is covered without knowing about it. SidingWallBlock itself is
// skipped - its own overrides already return exactly the panel's boxes, and a wall never shifts
// or hosts itself.
internal static class GapShiftCollisionPatches
{
    // A subclass override that calls base.GetCollisionBoxes runs both the base's patched method and
    // its own, which would shift twice. Counting re-entrancy per thread and only shifting at the
    // outermost frame is more robust than a bounds heuristic (e.g. "Y1 already looks shifted") -
    // that breaks the moment a shift and an unrelated box happen to look alike.
    [ThreadStatic] private static int depth;

    // Per original array, one shifted copy per exact (dx, dz) - several hostable blocks can share
    // one static box array, but each shifts by its own inset. Concurrent because physics, render
    // and main threads all query boxes and may add the same entry at once.
    private static readonly ConditionalWeakTable<Cuboidf[], ConcurrentDictionary<(double dx, double dz), Cuboidf[]>> ShiftCache = new();

    // Per (host array, panel array) pair, one concatenated copy - a per-tick path, so the append
    // is cached the same way as the shift above rather than reallocating every call.
    private static readonly ConditionalWeakTable<Cuboidf[], ConcurrentDictionary<Cuboidf[], Cuboidf[]>> CombineCache = new();

    // Per panel array, one PanelSelectionBox copy - IsPanelHit tells a click on the panel apart
    // from a click on the furniture by type, not by re-deriving which boxes came from where.
    private static readonly ConditionalWeakTable<Cuboidf[], Cuboidf[]> PanelSelectionCache = new();

    internal static void PatchAll(Harmony harmony, ICoreAPI api)
    {
        var prefix = new HarmonyMethod(typeof(GapShiftCollisionPatches), nameof(Prefix));
        var finalizer = new HarmonyMethod(typeof(GapShiftCollisionPatches), nameof(Finalizer));
        var collisionPostfix = new HarmonyMethod(typeof(GapShiftCollisionPatches), nameof(PostfixCollision));
        var particlePostfix = new HarmonyMethod(typeof(GapShiftCollisionPatches), nameof(PostfixParticleCollision));
        var selectionPostfix = new HarmonyMethod(typeof(GapShiftCollisionPatches), nameof(PostfixSelection));
        var parameterTypes = new[] { typeof(IBlockAccessor), typeof(BlockPos) };

        EveryOverridePatches.PatchEveryOverride(harmony, api, nameof(Block.GetCollisionBoxes), parameterTypes, prefix, collisionPostfix, finalizer);
        EveryOverridePatches.PatchEveryOverride(harmony, api, nameof(Block.GetParticleCollisionBoxes), parameterTypes, prefix, particlePostfix, finalizer);
        EveryOverridePatches.PatchEveryOverride(harmony, api, nameof(Block.GetSelectionBoxes), parameterTypes, prefix, selectionPostfix, finalizer);
    }

    private static void Prefix() => depth++;

    private static void Finalizer() => depth--;

    // A hosted block's own boxes are shifted off the panel; the panel itself still
    // occupies the cell and has to collide too, so its boxes are appended once the outermost
    // frame's shift is done. GetCollisionBoxes' fullBoxes mirrors the wall's own override
    // (Block.CollisionBoxes); GetParticleCollisionBoxes' mirrors ParticleCollisionBoxes ?? CollisionBoxes.
    private static void PostfixCollision(Block __instance, ref Cuboidf[] __result, object[] __args)
        => __result = ShiftAndAppendPanel(__instance, __result, __args, wall => wall.CollisionBoxes);

    private static void PostfixParticleCollision(Block __instance, ref Cuboidf[] __result, object[] __args)
        => __result = ShiftAndAppendPanel(__instance, __result, __args, wall => wall.ParticleCollisionBoxes ?? wall.CollisionBoxes);

    // Selection appends the panel's own SelectionBoxes, marked so IsPanelHit can recognise them -
    // after the host's boxes, so the host's own indices (which shelves and ground storage use to
    // pick a slot) are unchanged.
    private static void PostfixSelection(Block __instance, ref Cuboidf[] __result, object[] __args)
        => __result = ShiftAndAppendPanel(__instance, __result, __args, wall => wall.SelectionBoxes, MarkedForPanel);

    private static Cuboidf[] ShiftAndAppendPanel(Block instance, Cuboidf[] result, object[] args,
        System.Func<SidingWallBlock, Cuboidf[]> fullBoxesOf, System.Func<Cuboidf[], Cuboidf[]>? markPanel = null)
    {
        Shift(instance, result, args, out var shifted);
        // Re-checked rather than trusted from Shift: an inner frame's shift is a no-op, and so is
        // its append - only the outermost frame acts (same depth guard as the shift itself).
        // Only a hostable block can stand over a guest, and this runs on every collision query in
        // the world, so that table read comes before any lookup. It also keeps a real wall out: it
        // reaches here through its own base.GetCollisionBoxes, and WallAt would answer the wall itself.
        if (!SidingModSystem.IsHostableId(instance.BlockId) || depth != 1
            || args[0] is not IBlockAccessor blockAccessor || args[1] is not BlockPos pos) return shifted;

        if (SidingWallBlock.WallAt(blockAccessor, pos) is not { } found) return shifted;
        var (guestWall, guestEntity) = found;

        var panelBoxes = guestWall.PanelCollisionBoxes(blockAccessor, pos, guestEntity, fullBoxesOf(guestWall));
        if (markPanel != null) panelBoxes = markPanel(panelBoxes);
        return Combined(shifted, panelBoxes);
    }

    private static Cuboidf[] MarkedForPanel(Cuboidf[] panelBoxes)
        => PanelSelectionCache.GetValue(panelBoxes, boxes => boxes.Select(box => (Cuboidf)new PanelSelectionBox(box)).ToArray());

    private static void Shift(Block instance, Cuboidf[] result, object[] args, out Cuboidf[] shifted)
    {
        shifted = result;
        // __args because BlockMultiblock and others don't all spell the parameters the same way.
        if (depth != 1 || result is not { Length: > 0 }
            || args[0] is not IBlockAccessor blockAccessor || args[1] is not BlockPos pos) return;

        var (dx, dz) = SidingModSystem.GapShiftAt(blockAccessor, pos, instance);
        if (dx == 0 && dz == 0) return;

        shifted = Shifted(result, dx, dz);
    }

    internal static Cuboidf[] Shifted(Cuboidf[] original, double dx, double dz)
    {
        return ShiftCache.GetValue(original, _ => new ConcurrentDictionary<(double, double), Cuboidf[]>())
            .GetOrAdd((dx, dz), _ => original.Select(box => box.OffsetCopy((float)dx, 0, (float)dz)).ToArray());
    }

    // A torch or sign collides with nothing and hands back null, so a hosted one still gets the panel.
    internal static Cuboidf[] Combined(Cuboidf[]? hostBoxes, Cuboidf[] panelBoxes)
    {
        if (panelBoxes.Length == 0) return hostBoxes!;
        if (hostBoxes is not { Length: > 0 }) return panelBoxes;
        return CombineCache.GetValue(hostBoxes, _ => new ConcurrentDictionary<Cuboidf[], Cuboidf[]>())
            .GetOrAdd(panelBoxes, _ => hostBoxes.Concat(panelBoxes).ToArray());
    }

    // True when the player's raytraced index (AABBIntersectionTest.RayIntersectsBlockSelectionBox,
    // which walks the same GetSelectionBoxes array in the same order) landed on the panel rather
    // than the furniture - PanelInteractionPatches uses this to swallow both interaction and
    // breaking. host.GetSelectionBoxes is already patched, so it includes the panel.
    internal static bool IsPanelHit(Block host, IBlockAccessor accessor, BlockSelection sel)
    {
        Cuboidf[]? boxes = host.GetSelectionBoxes(accessor, sel.Position);
        return boxes != null && sel.SelectionBoxIndex >= 0 && sel.SelectionBoxIndex < boxes.Length
            && boxes[sel.SelectionBoxIndex] is PanelSelectionBox;
    }
}

// A selection box that is the guest wall's panel, not the host's own bounds - Combined appends
// these after the host's boxes, and IsPanelHit tells them apart by type instead of re-deriving
// which boxes came from where.
internal sealed class PanelSelectionBox : Cuboidf
{
    internal PanelSelectionBox(Cuboidf box) : base(box.X1, box.Y1, box.Z1, box.X2, box.Y2, box.Z2)
    {
    }
}
