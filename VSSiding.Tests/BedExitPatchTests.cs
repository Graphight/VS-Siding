using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;
using Xunit;

namespace VSSiding.Tests;

// A game update that changes how many spots DidUnmount tries fails here, rather than silently
// letting a sleeper get up on a wall's far side.
public class BedExitPatchTests
{
    private static SidingWallBlock Wall(string layout, string side)
    {
        var wall = new SidingWallBlock();
        wall.VariantStrict["layout"] = layout;
        wall.VariantStrict["side"] = side;
        return wall;
    }

    [Fact]
    public void GettingUpChecksBothSpotsThroughThePanelCheck()
    {
        var original = AccessTools.Method(typeof(BlockEntityBed), nameof(BlockEntityBed.DidUnmount));
        var patched = SidingModSystem.BedExitTranspiler(PatchProcessor.GetOriginalInstructions(original)).ToList();

        var isColliding = AccessTools.Method(typeof(CollisionTester), nameof(CollisionTester.IsColliding),
            new[] { typeof(Vintagestory.API.Common.IBlockAccessor), typeof(Cuboidf), typeof(Vec3d), typeof(bool) });
        var blocked = AccessTools.Method(typeof(SidingModSystem), nameof(SidingModSystem.BedExitBlocked));
        Assert.Equal((0, 2), (patched.Count(i => i.Calls(isColliding)), patched.Count(i => i.Calls(blocked))));

        // Applying it for real makes the runtime verify the rewritten IL, which the list above can't.
        var harmony = new Harmony("vssiding.tests.bedexit");
        try { harmony.Patch(original, transpiler: new HarmonyMethod(typeof(SidingModSystem), nameof(SidingModSystem.BedExitTranspiler))); }
        finally { harmony.UnpatchAll("vssiding.tests.bedexit"); }
    }

    [Fact]
    public void AStepNorthCrossesOnlyAPanelOnTheSharedFace()
    {
        var steps = new Dictionary<string, (SidingWallBlock? from, SidingWallBlock? to)>
        {
            ["no walls"] = (null, null),
            ["leaving past a north panel"] = (Wall("wall", "north"), null),
            ["leaving through a south panel's open side"] = (Wall("wall", "south"), null),
            ["leaving past an east panel"] = (Wall("wall", "east"), null),
            ["leaving past a cornerout's second face"] = (Wall("cornerout", "west"), null),
            ["landing on a south panel"] = (null, Wall("wall", "south")),
            ["landing in a north panel's open side"] = (null, Wall("wall", "north")),
        };

        var expected = new Dictionary<string, bool>
        {
            ["no walls"] = false,
            ["leaving past a north panel"] = true,
            ["leaving through a south panel's open side"] = false,
            ["leaving past an east panel"] = false,
            ["leaving past a cornerout's second face"] = true,
            ["landing on a south panel"] = true,
            ["landing in a north panel's open side"] = false,
        };

        var actual = steps.ToDictionary(kv => kv.Key, kv => SidingModSystem.StepCrossesPanel(kv.Value.from, kv.Value.to, BlockFacing.NORTH));

        Assert.Equal(expected, actual);
    }
}
