using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Vintagestory.API.Common;

namespace VSSiding;

// Patches every declaring override of a named method on Block and every non-abstract Block
// subclass in every loaded assembly, so other mods' blocks are covered too (decision 0035).
// SidingWallBlock is skipped: it answers for itself.
internal static class EveryOverridePatches
{
    // Walked once per session, not per process: each world loads its mods' assemblies afresh.
    private static List<Type>? blockTypes;

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
