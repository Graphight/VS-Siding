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

    // Stilts: three bamboo posts stacked in one corner, each pressed on the one below, then a floor laid beside the top
    // post, filled, and extended onto the post's cell, which a floor run reaching a post decks (decision 0084).
    [AtlasScenario(FreshWorld = true)]
    public async Task Posts_Should_CarryABambooFloorAsStilts_When_AFloorRunReachesTheTopPost()
    {
        BlockPos bottom = World.Spawn.Offset(1, 2, 0);
        World.SetBlock("game:planks-aged-ud", bottom.DownCopy());
        ITestPlayer player = await WallBuilder.JoinBuilder(World, "game:stone-granite");
        await player.TeleportTo(bottom.WestCopy(2));
        player.Entity.Pos.Yaw = 0;
        player.Entity.Pos.Pitch = 0;
        Vec3f view = player.Entity.SidedPos.GetViewVector();
        BlockFacing ahead = System.Math.Abs(view.X) >= System.Math.Abs(view.Z)
            ? (view.X > 0 ? BlockFacing.EAST : BlockFacing.WEST)
            : (view.Z > 0 ? BlockFacing.SOUTH : BlockFacing.NORTH);
        // The run walks the way the player looks, so the floor goes on the side of the post the player looks from.
        BlockPos top = bottom.UpCopy(2);
        BlockPos floor = top.AddCopy(ahead.Opposite);

        player.Entity.WatchedAttributes.SetString("vssidingFraming", "post");
        foreach (BlockPos below in new[] { bottom.DownCopy(), bottom, bottom.UpCopy() })
            await WallBuilder.Press(World, player, below, "game:bamboostakes", BlockFacing.UP, new Vec3d(0.2, 1, 0.8));

        player.Entity.WatchedAttributes.SetString("vssidingFraming", "floor");
        await WallBuilder.Press(World, player, top, "game:bamboostakes", ahead.Opposite, new Vec3d(0.5, 0.5, 0.5));
        // The first press on the bare floor fills it with woven bamboo; the second extends the run.
        await WallBuilder.Press(World, player, floor, "game:bamboostakes", BlockFacing.UP, new Vec3d(0.5, 1, 0.5));
        await WallBuilder.Press(World, player, floor, "game:bamboostakes", BlockFacing.UP, new Vec3d(0.5, 1, 0.5));

        (string, string?) Post(BlockPos cell) => (World.BlockAt(cell).Code.Path, WallBuilder.Layers(World, cell).Framing);
        Assert.Equal(
            (
                ("wall-cornerin-south", "bamboo"), ("wall-cornerin-south", "bamboo"), ("wall-cornerin-south", "bamboo"),
                ("bamboo", "bamboowoven", null, null, null), ("bamboo", null, null, null)
            ),
            (Post(bottom), Post(bottom.UpCopy()), Post(top), WallBuilder.Layers(World, floor), PostScenarios.DeckLayers(World, top)));
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
