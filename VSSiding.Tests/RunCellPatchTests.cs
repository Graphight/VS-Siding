using System.Linq;
using HarmonyLib;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Xunit;

namespace VSSiding.Tests;

// More Inventorys is not referenced here, so a block with the same air check as its racks stands in.
public class RunCellPatchTests
{
    private class RackLike : Block
    {
        public override bool TryPlaceBlock(IWorldAccessor world, IPlayer byPlayer, ItemStack itemstack, BlockSelection blockSel, ref string failureCode)
        {
            if (world.BlockAccessor.GetBlockId(blockSel.Position.UpCopy()) != 0 || world.BlockAccessor.GetBlockId(blockSel.Position.EastCopy()) != 0) return false;
            return base.TryPlaceBlock(world, byPlayer, itemstack, blockSel, ref failureCode);
        }
    }

    [Fact]
    public void EveryAirCheckAsksTheRunCellRule()
    {
        var original = AccessTools.Method(typeof(RackLike), nameof(Block.TryPlaceBlock));
        var patched = SidingModSystem.RunCellTranspiler(PatchProcessor.GetOriginalInstructions(original)).ToList();

        var getBlockId = AccessTools.Method(typeof(IBlockAccessor), nameof(IBlockAccessor.GetBlockId), new[] { typeof(BlockPos) });
        var runCellId = AccessTools.Method(typeof(SidingModSystem), nameof(SidingModSystem.RunCellId));
        Assert.Equal((0, 2), (patched.Count(i => i.Calls(getBlockId)), patched.Count(i => i.Calls(runCellId))));

        // Applying it for real makes the runtime verify the rewritten IL, which the list above can't.
        var harmony = new Harmony("vssiding.tests.runcell");
        try { harmony.Patch(original, transpiler: new HarmonyMethod(typeof(SidingModSystem), nameof(SidingModSystem.RunCellTranspiler))); }
        finally { harmony.UnpatchAll("vssiding.tests.runcell"); }
    }
}
