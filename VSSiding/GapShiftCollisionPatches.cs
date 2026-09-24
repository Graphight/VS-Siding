using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace VSSiding;

// Furniture-against-thin-walls (decision 0035 pending), stage 3: a block that renders shifted onto
// a wall's panel (SidingModSystem's TesselateBlock transpiler, stage 2) must collide and select
// where it's drawn, not where its cell's true bounds are. Every declaring override of
// GetCollisionBoxes/GetSelectionBoxes across loaded assemblies gets the same prefix/postfix, so a
// mod's own block subclass is covered without knowing about it. SidingWallBlock itself is skipped -
// its own overrides append the neighbour's already-shifted boxes separately, and a wall never
// shifts itself.
internal static class GapShiftCollisionPatches
{
    // A subclass override that calls base.GetCollisionBoxes runs both the base's patched method and
    // its own, which would shift twice. Counting re-entrancy per thread and only shifting at the
    // outermost frame is more robust than a bounds heuristic (e.g. "Y1 already looks shifted") -
    // that breaks the moment a shift and an unrelated box happen to look alike.
    [ThreadStatic] private static int depth;

    // Per original array, one shifted copy per (sign dx, sign dz) - nine slots, the centre unused.
    private static readonly ConditionalWeakTable<Cuboidf[], Cuboidf[]?[]> ShiftCache = new();

    internal static void PatchAll(Harmony harmony, ICoreAPI api)
    {
        var prefix = new HarmonyMethod(typeof(GapShiftCollisionPatches), nameof(Prefix));
        var finalizer = new HarmonyMethod(typeof(GapShiftCollisionPatches), nameof(Finalizer));
        var collisionPostfix = new HarmonyMethod(typeof(GapShiftCollisionPatches), nameof(PostfixCollision));
        var selectionPostfix = new HarmonyMethod(typeof(GapShiftCollisionPatches), nameof(PostfixSelection));

        var blockTypes = new List<Type> { typeof(Block) };
        blockTypes.AddRange(AppDomain.CurrentDomain.GetAssemblies().SelectMany(SafeGetTypes)
            .Where(t => t.IsSubclassOf(typeof(Block)) && !t.IsAbstract && t != typeof(SidingWallBlock)));

        foreach (var type in blockTypes)
        {
            PatchDeclared(harmony, api, type, nameof(Block.GetCollisionBoxes), prefix, collisionPostfix, finalizer);
            PatchDeclared(harmony, api, type, nameof(Block.GetSelectionBoxes), prefix, selectionPostfix, finalizer);
        }
    }

    private static void PatchDeclared(Harmony harmony, ICoreAPI api, Type type, string methodName,
        HarmonyMethod prefix, HarmonyMethod postfix, HarmonyMethod finalizer)
    {
        var method = type.GetMethod(methodName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly,
            null, new[] { typeof(IBlockAccessor), typeof(BlockPos) }, null);
        if (method == null) return;

        try
        {
            harmony.Patch(method, prefix: prefix, postfix: postfix, finalizer: finalizer);
        }
        catch (Exception e)
        {
            api.Logger.Warning("vssiding: gap shift collision patch skipped on {0}.{1}, furniture snapped onto a wall's panel may pass through or miss selection there: {2}",
                method.DeclaringType, methodName, e);
        }
    }

    // Tolerates a mod assembly whose client types can't load on the server (or vice versa) -
    // AppDomain enumeration must survive that to reach every other assembly's block types.
    private static IEnumerable<Type> SafeGetTypes(Assembly assembly)
    {
        try { return assembly.GetTypes(); }
        catch (ReflectionTypeLoadException e) { return e.Types.Where(t => t != null)!; }
        catch { return Type.EmptyTypes; }
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

        shifted = Shifted(result, Math.Sign(dx), Math.Sign(dz));
    }

    internal static Cuboidf[] Shifted(Cuboidf[] original, int sx, int sz)
    {
        var perDirection = ShiftCache.GetValue(original, _ => new Cuboidf[9][]);
        return perDirection[(sx + 1) * 3 + sz + 1] ??= original
            .Select(box => box.OffsetCopy(
                (float)(sx * SidingWallBlock.GapShiftDistance), 0,
                (float)(sz * SidingWallBlock.GapShiftDistance)))
            .ToArray();
    }
}
