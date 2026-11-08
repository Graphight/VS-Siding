using System.Threading.Tasks;
using Atlas.Api;
using Atlas.XUnit;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Xunit;

namespace VSSiding.E2E.Tests;

// Bamboo stakes are a framing and the woven bamboo infill at once, as sticks are wattle's (decision 0058).
[AtlasWorld(StrictBootDiagnostics = true)]
public class BambooScenarios : AtlasScenarioBase
{
    [AtlasScenario(FreshWorld = true)]
    public async Task Wall_Should_FillWithWovenBamboo_When_StakesClickABareBambooFrame()
    {
        (BlockPos cell, ITestPlayer player) = await RaiseBareFrame();

        player.Entity.WatchedAttributes.SetString("vssidingFraming", "floor");
        await player.GiveItem("game:bamboostakes", 4);
        var sideSel = new BlockSelection { Position = cell.Copy(), Face = BlockFacing.EAST, HitPosition = new Vec3d(0.5, 0.5, 0.5) };
        Assert.True(World.BlockAt(cell).OnBlockInteractStart(player.Entity.World, player.Player, sideSel));

        Assert.Equal(("bamboo", "bamboowoven", null, null, null), WallBuilder.Layers(World, cell));
    }

    [AtlasScenario(FreshWorld = true)]
    public async Task Frame_Should_StackOnABareBambooFrame_When_StakesSneakClickItsTop()
    {
        (BlockPos cell, ITestPlayer player) = await RaiseBareFrame();

        await player.GiveItem("game:bamboostakes", 4);
        player.Entity.Controls.ShiftKey = true;
        EnumHandHandling handling = WallBuilder.ClickTop(player, cell);

        Assert.Equal(
            (EnumHandHandling.PreventDefault, ("bamboo", null, null, null, null), ("bamboo", null, null, null, null)),
            (handling, WallBuilder.Layers(World, cell), WallBuilder.Layers(World, cell.UpCopy())));
    }

    [AtlasScenario(FreshWorld = true)]
    public async Task Wall_Should_TakeStackedBambooFinish_When_ABlockClicksAWovenFace()
    {
        (BlockPos cell, ITestPlayer player) = await RaiseBareFrame();
        player.Entity.WatchedAttributes.SetString("vssidingFraming", "floor");
        var sideSel = new BlockSelection { Position = cell.Copy(), Face = BlockFacing.EAST, HitPosition = new Vec3d(0.5, 0.5, 0.5) };
        await player.GiveItem("game:bamboostakes", 4);
        Assert.True(World.BlockAt(cell).OnBlockInteractStart(player.Entity.World, player.Player, sideSel));

        await player.GiveItem("game:stackedbamboo-ns", 2);
        Assert.True(World.BlockAt(cell).OnBlockInteractStart(player.Entity.World, player.Player, sideSel));

        Assert.Equal(("bamboo", "bamboowoven", null, null, "stackedbamboo"), WallBuilder.Layers(World, cell));
    }

    private async Task<(BlockPos Cell, ITestPlayer Player)> RaiseBareFrame()
    {
        BlockPos cell = World.Spawn.Offset(1, 2, 0);
        World.SetBlock("game:planks-aged-ud", cell.DownCopy());
        ITestPlayer player = await WallBuilder.JoinBuilder(World, "game:stone-granite");
        await WallBuilder.Raise(World, player, cell, cell.WestCopy(), "game:bamboostakes", null);
        return (cell, player);
    }
}
