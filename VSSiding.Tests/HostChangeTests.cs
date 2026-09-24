using System.Collections.Generic;
using HarmonyLib;
using Vintagestory.API.Common;
using Vintagestory.Client.NoObf;
using Vintagestory.Common;
using System.Linq;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;
using Vintagestory.Server;
using Xunit;
using static VSSiding.SidingModSystem;

namespace VSSiding.Tests;

public class HostChangeTests
{
    // A plain wall a guest could restore into - not replaceable by anything (Block's default
    // Replaceable of 0), same as SidingWallBlock never overrides IsReplacableBy for that case.
    private static readonly Block GuestWall = new() { BlockId = 1, Code = new AssetLocation("vssiding", "wall-oak-west") };

    // Air's real Replaceable (9999) makes IsReplacableBy return true for anything.
    private static readonly Block Air = new() { BlockId = 0, Replaceable = 9999 };
    private static readonly Block Tallgrass = new() { BlockId = 2, Replaceable = 6000 };
    private static readonly Block HostableChest = new() { BlockId = 3 };
    private static readonly Block PlainStone = new() { BlockId = 4 };

    private static bool[] Hostable() => new[] { false, false, false, true, false };

    [Fact]
    public void ClassifiesEachCase()
    {
        var expected = new Dictionary<Block, HostChange>
        {
            [Air] = HostChange.Restore,
            [Tallgrass] = HostChange.Restore,
            [HostableChest] = HostChange.Keep,
            [PlainStone] = HostChange.Drop,
        };

        var actual = new Dictionary<Block, HostChange>();
        foreach (var newBlock in expected.Keys)
        {
            actual[newBlock] = ClassifyHostChange(newBlock, GuestWall, Hostable());
        }

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void PatchTargetsStillExist()
    {
        Assert.NotNull(AccessTools.Method(typeof(WorldChunk), nameof(WorldChunk.BreakAllDecorFast)));

        // Both prefixes bind their parameters by name, so the names are part of the target.
        var neighbourUpdate = AccessTools.Method(typeof(ServerMain), nameof(ServerMain.TriggerNeighbourBlocksUpdate), new[] { typeof(BlockPos) });
        Assert.Equal(new[] { "pos" }, neighbourUpdate.GetParameters().Select(p => p.Name));
        var groundStorable = AccessTools.Method(typeof(CollectibleBehaviorGroundStorable), nameof(CollectibleBehaviorGroundStorable.Interact));
        Assert.Equal(new[] { "itemslot", "byEntity", "blockSel", "entitySel", "firstEvent", "handHandling", "handling" },
            groundStorable.GetParameters().Select(p => p.Name));
    }

    // A wall that would take the held block as a placement target, the way SidingWallBlock does
    // for anything hostable once the Hostable table is built.
    private class WillingWall : SidingWallBlock
    {
        public override bool IsReplacableBy(Block block) => true;
    }

    // A click on a wall places beyond the clicked face, as vanilla did before walls took hostable
    // blocks into their own cell, except on the open side's inner face, where it hosts; anything
    // else keeps its own answer. The torch case: a saw in the off hand skips TryHost, so this is
    // what hangs a torch on the panel's inner face.
    [Fact]
    public void ClickedWallsTakeOnlyTheirInnerFaceIntoTheirOwnCell()
    {
        var wall = new WillingWall();
        wall.VariantStrict["layout"] = "wall";
        wall.VariantStrict["side"] = "north";
        var torch = new Block { BlockId = 3 };
        BlockSelection On(BlockFacing face) => new() { Face = face };

        var actual = new[]
        {
            ClickedIsReplacableBy(wall, torch, On(BlockFacing.SOUTH)),
            ClickedIsReplacableBy(wall, torch, On(BlockFacing.NORTH)),
            ClickedIsReplacableBy(wall, torch, On(BlockFacing.UP)),
            ClickedIsReplacableBy(wall, torch, On(BlockFacing.EAST)),
            ClickedIsReplacableBy(new Block { Replaceable = 6000 }, torch, On(BlockFacing.UP)),
            ClickedIsReplacableBy(new Block(), torch, On(BlockFacing.UP)),
        };

        Assert.Equal(new[] { true, false, false, false, true, false }, actual);
    }

    [Fact]
    public void BlockBuildAsksTheClickedBlockThroughTheWallAwareCheck()
    {
        var original = AccessTools.Method(typeof(SystemMouseInWorldInteractions), "OnBlockBuild");
        var patched = BlockBuildTranspiler(PatchProcessor.GetOriginalInstructions(original)).ToList();

        var isReplacableBy = AccessTools.Method(typeof(Block), nameof(Block.IsReplacableBy));
        var clicked = AccessTools.Method(typeof(SidingModSystem), nameof(ClickedIsReplacableBy));
        Assert.DoesNotContain(patched, i => i.Calls(isReplacableBy));
        Assert.Single(patched, i => i.Calls(clicked));

        // Applying it for real makes the runtime verify the rewritten IL.
        var harmony = new Harmony("vssiding.tests.blockbuild");
        try { harmony.Patch(original, transpiler: new HarmonyMethod(typeof(SidingModSystem), nameof(BlockBuildTranspiler))); }
        finally { harmony.UnpatchAll("vssiding.tests.blockbuild"); }
    }
}
