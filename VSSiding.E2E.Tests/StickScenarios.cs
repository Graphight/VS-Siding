using System.Threading.Tasks;
using Atlas.Api;
using Atlas.XUnit;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Xunit;

namespace VSSiding.E2E.Tests;

// Sticks are a framing and the wattle infill at once (decision 0058): a plain click on a bare frame fills it.
[AtlasWorld(StrictBootDiagnostics = true)]
public class StickScenarios : AtlasScenarioBase
{
    [AtlasScenario(FreshWorld = true)]
    public async Task Wall_Should_FillWithWattle_When_SticksClickABareFrameWithFloorPicked()
    {
        (BlockPos cell, ITestPlayer player) = await RaiseBareStickFrame();

        player.Entity.WatchedAttributes.SetString("vssidingFraming", "floor");
        await player.GiveItem("game:stick", 4);
        var sideSel = new BlockSelection { Position = cell.Copy(), Face = BlockFacing.EAST, HitPosition = new Vec3d(0.5, 0.5, 0.5) };
        Assert.True(World.BlockAt(cell).OnBlockInteractStart(player.Entity.World, player.Player, sideSel));

        Assert.Equal(("sticks", "wattle", null, null, null), WallBuilder.Layers(World, cell));
    }

    private async Task<(BlockPos Cell, ITestPlayer Player)> RaiseBareStickFrame()
    {
        BlockPos cell = World.Spawn.Offset(1, 2, 0);
        World.SetBlock("game:planks-aged-ud", cell.DownCopy());
        ITestPlayer player = await WallBuilder.JoinBuilder(World, "game:stone-granite");
        await WallBuilder.Raise(World, player, cell, cell.WestCopy(), "game:stick", null, framingCost: 4);
        return (cell, player);
    }
}
