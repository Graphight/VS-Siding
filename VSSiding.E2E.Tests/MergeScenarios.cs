using System.Threading.Tasks;
using Atlas.Api;
using Atlas.XUnit;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Xunit;

namespace VSSiding.E2E.Tests;

// The picker's merge switches that are off are stamped on a sheet when it is laid.
[AtlasWorld(StrictBootDiagnostics = true)]
public class MergeScenarios : AtlasScenarioBase
{
    [AtlasScenario(RollbackWorld = true, StrictIsolation = true)]
    public Task Wall_Should_StampTheOffSwitches_When_GlassIsLaidIntoIt()
        => Stamped(floor: false, "updown", BlockFacing.EAST);

    [AtlasScenario(RollbackWorld = true, StrictIsolation = true)]
    public Task Floor_Should_StampTheOffSwitches_When_GlassIsLaidIntoIt()
        => Stamped(floor: true, "updown,leftright", BlockFacing.UP);

    // The deck's glass takes the switches; the wall's clay below it is not a sheet and takes none.
    [AtlasScenario(RollbackWorld = true, StrictIsolation = true)]
    public async Task Deck_Should_StampTheOffSwitches_When_GlassIsLaidIntoIt()
    {
        BlockPos cell = World.Spawn.Offset(1, 2, 0);
        World.SetBlock("game:planks-aged-ud", cell.DownCopy());
        ITestPlayer player = await WallBuilder.JoinBuilder(World);
        player.Entity.WatchedAttributes.SetString("vssidingMerge", "leftright");
        await WallBuilder.Raise(World, player, cell, cell.WestCopy(), "game:plank-oak", "game:clay-blue");

        player.Entity.WatchedAttributes.SetString("vssidingDeck", "deck");
        await player.GiveItem("game:plank-oak", 2);
        var sideSel = new BlockSelection { Position = cell.Copy(), Face = BlockFacing.EAST, HitPosition = new Vec3d(1, 0.5, 0.5) };
        Assert.True(World.BlockAt(cell).OnBlockInteractStart(player.Entity.World, player.Player, sideSel));

        var deckTopSel = new BlockSelection
        {
            Position = cell.Copy(), Face = BlockFacing.UP, HitPosition = new Vec3d(0.5, 1, 0.5),
            SelectionBoxIndex = World.BlockAt(cell).SelectionBoxes.Length,
        };
        await player.GiveItem("game:glass-plain", 1);
        Assert.True(World.BlockAt(cell).OnBlockInteractStart(player.Entity.World, player.Player, deckTopSel));

        var read = WallBuilder.Reader(World.BlockEntityAt<BlockEntity>(cell)!);
        Assert.Equal(("glass-plain", "leftright", true), (read("deckinfill"), read("deckinfillunmerged"), string.IsNullOrEmpty(read("infillunmerged"))));
    }

    private async Task Stamped(bool floor, string off, BlockFacing face)
    {
        BlockPos cell = World.Spawn.Offset(1, 2, 0);
        World.SetBlock("game:planks-aged-ud", cell.DownCopy());
        ITestPlayer player = await WallBuilder.JoinBuilder(World);
        if (floor) player.Entity.WatchedAttributes.SetString("vssidingFraming", "floor");
        player.Entity.WatchedAttributes.SetString("vssidingMerge", off);
        await WallBuilder.Raise(World, player, cell, cell.WestCopy(), "game:plank-oak", null);

        await player.GiveItem("game:glass-plain", 1);
        var sel = new BlockSelection { Position = cell.Copy(), Face = face, HitPosition = new Vec3d(0.5, floor ? 1 : 0.5, 0.5) };
        Assert.True(World.BlockAt(cell).OnBlockInteractStart(player.Entity.World, player.Player, sel));

        Assert.Equal(off, WallBuilder.Reader(World.BlockEntityAt<BlockEntity>(cell)!)("infillunmerged"));
    }
}
