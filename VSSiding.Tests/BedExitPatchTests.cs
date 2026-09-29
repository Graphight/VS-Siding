using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;
using Xunit;

namespace VSSiding.Tests;

// A game update that changes how many spots DidUnmount tries fails here, rather than silently
// letting a sleeper get up on a wall's far side.
public class BedExitPatchTests
{
    [Fact]
    public void GettingUpChecksBothSpotsThroughThePanelCheck()
    {
        var original = AccessTools.Method(typeof(BlockEntityBed), nameof(BlockEntityBed.DidUnmount));
        var patched = SidingModSystem.BedExitTranspiler(PatchProcessor.GetOriginalInstructions(original)).ToList();

        var isColliding = AccessTools.Method(typeof(CollisionTester), nameof(CollisionTester.IsColliding),
            new[] { typeof(IBlockAccessor), typeof(Cuboidf), typeof(Vec3d), typeof(bool) });
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
            ["leaving past a north panel"] = (FootprintHostsTests.Wall("wall", "north"), null),
            ["leaving through a south panel's open side"] = (FootprintHostsTests.Wall("wall", "south"), null),
            ["leaving past an east panel"] = (FootprintHostsTests.Wall("wall", "east"), null),
            ["leaving past a cornerout's second face"] = (FootprintHostsTests.Wall("cornerout", "west"), null),
            ["landing on a south panel"] = (null, FootprintHostsTests.Wall("wall", "south")),
            ["landing in a north panel's open side"] = (null, FootprintHostsTests.Wall("wall", "north")),
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

    // DidUnmount tries each cell beside the head, then each beside the feet, one block up from the
    // bed's floor; every spot must trace back to the cell it was tried from, including the feet's
    // spot on the head cell and the two diagonal ones.
    [Fact]
    public void EverySpotTracesBackToTheBedCellItWasTriedFrom()
    {
        var head = new BlockPos(10, 5, 10);
        var feet = head.AddCopy(BlockFacing.SOUTH);

        var expected = new Dictionary<string, (BlockPos from, BlockFacing? dir)>();
        var actual = new Dictionary<string, (BlockPos from, BlockFacing? dir)>();
        foreach (var (name, cell) in new[] { ("head", head), ("feet", feet) })
        foreach (var face in BlockFacing.HORIZONTALS)
        {
            var spot = cell.ToVec3d().AddCopy(face).Add(0.5, 0.001, 0.5);
            expected[$"{name} {face.Code}"] = (cell, face);
            actual[$"{name} {face.Code}"] = SidingModSystem.BedExitStep(head, "south", spot);
        }

        Assert.Equal(expected, actual);
    }
}
