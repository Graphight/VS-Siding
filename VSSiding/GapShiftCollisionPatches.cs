using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Runtime.CompilerServices;
using HarmonyLib;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace VSSiding;

// Furniture-against-thin-walls (decision 0035 pending): a block that renders shifted onto a wall's
// panel (SidingModSystem's TesselateBlock transpiler) must collide and select where it's drawn, not
// where its cell's true bounds are. EveryOverridePatches patches GetCollisionBoxes/GetSelectionBoxes
// on every declaring override across loaded assemblies, so a mod's own block subclass is covered
// without knowing about it. SidingWallBlock itself is skipped - its own overrides append the
// neighbour's already-shifted boxes separately, and a wall never shifts itself.
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

    internal static void PatchAll(Harmony harmony, ICoreAPI api)
    {
        var prefix = new HarmonyMethod(typeof(GapShiftCollisionPatches), nameof(Prefix));
        var finalizer = new HarmonyMethod(typeof(GapShiftCollisionPatches), nameof(Finalizer));
        var collisionPostfix = new HarmonyMethod(typeof(GapShiftCollisionPatches), nameof(PostfixCollision));
        var selectionPostfix = new HarmonyMethod(typeof(GapShiftCollisionPatches), nameof(PostfixSelection));
        var parameterTypes = new[] { typeof(IBlockAccessor), typeof(BlockPos) };

        EveryOverridePatches.PatchEveryOverride(harmony, api, nameof(Block.GetCollisionBoxes), parameterTypes, prefix, collisionPostfix, finalizer);
        EveryOverridePatches.PatchEveryOverride(harmony, api, nameof(Block.GetSelectionBoxes), parameterTypes, prefix, selectionPostfix, finalizer);
    }

    private static void Prefix() => depth++;

    private static void Finalizer() => depth--;

    private static void PostfixCollision(Block __instance, ref Cuboidf[] __result, object[] __args)
        => Shift(__instance, __result, __args, out __result);

    private static void PostfixSelection(Block __instance, ref Cuboidf[] __result, object[] __args)
        => Shift(__instance, __result, __args, out __result);

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
}
