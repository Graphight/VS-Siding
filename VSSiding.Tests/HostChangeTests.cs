using System.Collections.Generic;
using HarmonyLib;
using Vintagestory.API.Common;
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
}
