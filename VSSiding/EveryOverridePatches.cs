using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Vintagestory.API.Common;

namespace VSSiding;

// Lifted out of GapShiftCollisionPatches, whose GetCollisionBoxes/GetSelectionBoxes patches were
// its only consumer until the tooltip postfix (decision 0035) needed the same trick for GetPlacedBlockInfo: patch every declaring override of a named
// method on Block and every non-abstract Block subclass in every loaded assembly, so a mod's own
// block subclass is covered without knowing about it. SidingWallBlock is always skipped - it has
// its own hand-written behaviour and never patches itself.
internal static class EveryOverridePatches
{
    // Walked once per session and shared by every consumer, rather than re-enumerating every loaded
    // assembly's types for each named method. Not once per process: each world loads its mods'
    // assemblies afresh, so a later session's block types would be missing from the first's list.
    private static List<Type>? blockTypes;

    // Called from SidingModSystem.Dispose, alongside the unpatch the next session's Start redoes.
    internal static void Forget() => blockTypes = null;

    private static List<Type> FindBlockTypes()
    {
        var types = new List<Type> { typeof(Block) };
        types.AddRange(AppDomain.CurrentDomain.GetAssemblies().SelectMany(SafeGetTypes)
            .Where(t => t.IsSubclassOf(typeof(Block)) && !t.IsAbstract && t != typeof(SidingWallBlock)));
        return types;
    }

    internal static void PatchEveryOverride(Harmony harmony, ICoreAPI api, string methodName, Type[] parameterTypes,
        HarmonyMethod? prefix, HarmonyMethod? postfix, HarmonyMethod? finalizer)
    {
        foreach (var type in blockTypes ??= FindBlockTypes())
        {
            var method = type.GetMethod(methodName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly,
                null, parameterTypes, null);
            if (method == null) continue;

            try
            {
                harmony.Patch(method, prefix: prefix, postfix: postfix, finalizer: finalizer);
            }
            catch (Exception e)
            {
                api.Logger.Warning("vssiding: patch skipped on {0}.{1}: {2}", method.DeclaringType, methodName, e);
            }
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
}
