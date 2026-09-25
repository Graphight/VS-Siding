using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Runtime.CompilerServices;
using HarmonyLib;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace VSSiding;

// A hosted block renders shifted off its guest wall's panel, so it collides and selects where it's
// drawn, and the panel beside it collides and selects too (decision 0035).
internal static class GapShiftCollisionPatches
{
    // An override calling base.GetCollisionBoxes runs the postfix twice; only the outermost shifts.
    [ThreadStatic] private static int depth;

    // Per original array, one shifted copy per exact (dx, dz) - several hostable blocks can share
    // one static box array, but each shifts by its own inset. Concurrent because physics, render
    // and main threads all query boxes and may add the same entry at once.
    private static readonly ConditionalWeakTable<Cuboidf[], ConcurrentDictionary<(double dx, double dz), Cuboidf[]>> ShiftCache = new();

    // Per (host array, panel array) pair, one concatenated copy: this runs every physics tick.
    private static readonly ConditionalWeakTable<Cuboidf[], ConcurrentDictionary<Cuboidf[], Cuboidf[]>> CombineCache = new();

    // Per panel array, one PanelSelectionBox copy.
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

    // Each passes the full boxes the wall's own override would start from.
    private static void PostfixCollision(Block __instance, ref Cuboidf[] __result, object[] __args)
        => __result = ShiftAndAppendPanel(__instance, __result, __args, wall => wall.CollisionBoxes);

    private static void PostfixParticleCollision(Block __instance, ref Cuboidf[] __result, object[] __args)
        => __result = ShiftAndAppendPanel(__instance, __result, __args, wall => wall.ParticleCollisionBoxes ?? wall.CollisionBoxes);

    // The panel's boxes go after the host's, so the host's own indices (which shelves and ground
    // storage use to pick a slot) are unchanged.
    private static void PostfixSelection(Block __instance, ref Cuboidf[] __result, object[] __args)
        => __result = ShiftAndAppendPanel(__instance, __result, __args, wall => wall.SelectionBoxes, MarkedForPanel);

    private static Cuboidf[] ShiftAndAppendPanel(Block instance, Cuboidf[] result, object[] args,
        System.Func<SidingWallBlock, Cuboidf[]> fullBoxesOf, System.Func<Cuboidf[], Cuboidf[]>? markPanel = null)
    {
        Shift(instance, result, args, out var shifted);
        // Hostable first: this runs on every collision query in the world. It also keeps a real
        // wall, reaching here through its own base call, from appending a copy of its own panel.
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
        // __args, because overrides don't all spell the parameters the same way.
        if (depth != 1 || result is not { Length: > 0 }
            || args[0] is not IBlockAccessor blockAccessor || args[1] is not BlockPos pos) return;

        var (dx, dz) = SidingModSystem.GapShiftAt(pos, instance);
        if (dx == 0 && dz == 0) return;

        shifted = Shifted(result, dx, dz);
    }

    internal static Cuboidf[] Shifted(Cuboidf[] original, double dx, double dz)
    {
        return ShiftCache.GetValue(original, _ => new ConcurrentDictionary<(double, double), Cuboidf[]>())
            .GetOrAdd((dx, dz), _ => original.Select(box => ShiftedBox(box, dx, dz)).ToArray());
    }

    // The raytrace reports a CuboidfWithId's Id as the selection's SelectionBoxId, and a cabinet's
    // shelves (BEBehaviorDisplay) pick their slot by it, so a shifted copy keeps it.
    private static Cuboidf ShiftedBox(Cuboidf box, double dx, double dz)
    {
        Cuboidf shifted = box.OffsetCopy((float)dx, 0, (float)dz);
        if (box is not CuboidfWithId withId) return shifted;
        return new CuboidfWithId(shifted.X1, shifted.Y1, shifted.Z1, shifted.X2, shifted.Y2, shifted.Z2) { Id = withId.Id };
    }

    // A torch or sign collides with nothing and hands back null, so a hosted one still gets the panel.
    internal static Cuboidf[] Combined(Cuboidf[]? hostBoxes, Cuboidf[] panelBoxes)
    {
        if (panelBoxes.Length == 0) return hostBoxes!;
        if (hostBoxes is not { Length: > 0 }) return panelBoxes;
        return CombineCache.GetValue(hostBoxes, _ => new ConcurrentDictionary<Cuboidf[], Cuboidf[]>())
            .GetOrAdd(panelBoxes, _ => hostBoxes.Concat(panelBoxes).ToArray());
    }

    // True when the player's selection box index lands on the panel rather than the furniture.
    internal static bool IsPanelHit(Block host, IBlockAccessor accessor, BlockSelection sel)
    {
        Cuboidf[]? boxes = host.GetSelectionBoxes(accessor, sel.Position);
        return boxes != null && sel.SelectionBoxIndex >= 0 && sel.SelectionBoxIndex < boxes.Length
            && boxes[sel.SelectionBoxIndex] is PanelSelectionBox;
    }
}

// A selection box on the guest wall's panel, told apart from the host's own boxes by type.
internal sealed class PanelSelectionBox : Cuboidf
{
    internal PanelSelectionBox(Cuboidf box) : base(box.X1, box.Y1, box.Z1, box.X2, box.Y2, box.Z2)
    {
    }
}
