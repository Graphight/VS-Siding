using System.Linq;
using HarmonyLib;
using Vintagestory.API.Common;
using Vintagestory.Client.NoObf;
using Xunit;

namespace VSSiding.Tests;

public class GapShiftPatchTests
{
    [Fact]
    public void InsertsExactlyOneShiftTowardWallCallAfterFinalZIsStored()
    {
        var original = AccessTools.Method(typeof(ChunkTesselator), "TesselateBlock",
            new[] { typeof(Block), typeof(int), typeof(int), typeof(int), typeof(int), typeof(int) });
        var instructions = PatchProcessor.GetOriginalInstructions(original);

        var patched = SidingModSystem.GapShiftTranspiler(instructions).ToList();

        var shiftTowardWall = AccessTools.Method(typeof(SidingModSystem), nameof(SidingModSystem.ShiftTowardWall));
        Assert.Single(patched, i => i.Calls(shiftTowardWall));
    }

    [Fact]
    public void PatchTargetsStillExist()
    {
        Assert.NotNull(AccessTools.Method(typeof(ChunkTesselator), "TesselateBlock",
            new[] { typeof(Block), typeof(int), typeof(int), typeof(int), typeof(int), typeof(int) }));
        Assert.NotNull(AccessTools.Field(typeof(TCTCache), "finalZ"));
        Assert.NotNull(AccessTools.Field(typeof(TCTCache), "finalX"));
        Assert.NotNull(AccessTools.Field(typeof(TCTCache), "extIndex3d"));
        Assert.NotNull(AccessTools.Field(typeof(ChunkTesselator), "vars"));
        Assert.NotNull(AccessTools.Field(typeof(ChunkTesselator), "currentChunkBlocksExt"));
    }
}
