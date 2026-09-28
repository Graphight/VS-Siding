using System.Linq;
using HarmonyLib;
using Vintagestory.API.Common;
using Vintagestory.GameContent;
using Xunit;

namespace VSSiding.Tests;

// A game update that renames CanStep or changes how often the smooth finder reads it fails here,
// rather than silently leaving a wall's step unclimbable.
public class PlayerStepPatchTests
{
    [Fact]
    public void SmoothStepFinderReadsCanStepThroughThePlayerCheck()
    {
        var original = AccessTools.Method(typeof(EntityBehaviorControlledPhysics), nameof(EntityBehaviorControlledPhysics.FindSteppableCollisionboxSmooth));
        var patched = SidingModSystem.PlayerStepTranspiler(PatchProcessor.GetOriginalInstructions(original)).ToList();

        var canStep = AccessTools.Field(typeof(Block), nameof(Block.CanStep));
        var playerCanStep = AccessTools.Method(typeof(SidingModSystem), nameof(SidingModSystem.PlayerCanStep));
        Assert.Equal((0, 2), (patched.Count(i => i.LoadsField(canStep)), patched.Count(i => i.Calls(playerCanStep))));

        // Applying it for real makes the runtime verify the rewritten IL, which the list above can't.
        var harmony = new Harmony("vssiding.tests.playerstep");
        try { harmony.Patch(original, transpiler: new HarmonyMethod(typeof(SidingModSystem), nameof(SidingModSystem.PlayerStepTranspiler))); }
        finally { harmony.UnpatchAll("vssiding.tests.playerstep"); }
    }

    [Fact]
    public void PlayersStepWallsAndKeepEveryOtherBlocksOwnAnswer()
    {
        var blocks = new Block[] { new SidingWallBlock { CanStep = false }, new Block { CanStep = false }, new Block { CanStep = true } };

        Assert.Equal(new[] { true, false, true }, blocks.Select(SidingModSystem.PlayerCanStep).ToArray());
    }
}
