using System.Threading.Tasks;
using Atlas.Api;
using Atlas.XUnit;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Xunit;

namespace VSSiding.E2E.Tests;

// Bamboo stakes are a framing and the bamboo pole infill at once, as sticks are wattle's (decision 0058).
[AtlasWorld(StrictBootDiagnostics = true)]
public class BambooScenarios : AtlasScenarioBase
{
    [AtlasScenario(FreshWorld = true)]
    public async Task Wall_Should_FillWithBambooPoles_When_StakesClickABareBambooFrameWithFloorPicked()
    {
        BlockPos cell = await FillWithStakes();

        Assert.Equal(("bamboo", "bamboopoles", null, null, null), WallBuilder.Layers(World, cell));
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
        // The run walks the way the player looks, so the floor goes on the side of the post the player looks from.
        BlockFacing behind = WallBuilder.Ahead(player).Opposite;
        BlockPos top = bottom.UpCopy(2);
        BlockPos floor = top.AddCopy(behind);

        player.Entity.WatchedAttributes.SetString("vssidingFraming", "post");
        foreach (BlockPos below in new[] { bottom.DownCopy(), bottom, bottom.UpCopy() })
            await WallBuilder.Press(World, player, below, "game:bamboostakes", BlockFacing.UP, new Vec3d(0.2, 1, 0.8));

        player.Entity.WatchedAttributes.SetString("vssidingFraming", "floor");
        await WallBuilder.Press(World, player, top, "game:bamboostakes", behind, new Vec3d(0.5, 0.5, 0.5));
        // The first press on the bare floor fills it with bamboo poles; the second extends the run.
        await WallBuilder.Press(World, player, floor, "game:bamboostakes", BlockFacing.UP, new Vec3d(0.5, 1, 0.5));
        await WallBuilder.Press(World, player, floor, "game:bamboostakes", BlockFacing.UP, new Vec3d(0.5, 1, 0.5));

        (string, string?) Post(BlockPos cell) => (World.BlockAt(cell).Code.Path, WallBuilder.Layers(World, cell).Framing);
        Assert.Equal(
            (
                ("wall-cornerin-south", "bamboo"), ("wall-cornerin-south", "bamboo"), ("wall-cornerin-south", "bamboo"),
                ("bamboo", "bamboopoles", null, null, null), ("bamboo", null, null, null)
            ),
            (Post(bottom), Post(bottom.UpCopy()), Post(top), WallBuilder.Layers(World, floor), WallBuilder.DeckLayers(World, top)));
    }

    // A post is no bare frame for stakes to fill, so with Floor picked they lay a floor beside it as planks do, deck lit or not.
    [AtlasScenario(FreshWorld = true)]
    public async Task Post_Should_LayAFloorBesideIt_When_StakesClickItsSideWithFloorPickedAndTheDeckLit()
    {
        BlockPos post = World.Spawn.Offset(1, 2, 0);
        World.SetBlock("game:planks-aged-ud", post.DownCopy());
        ITestPlayer player = await WallBuilder.JoinBuilder(World, "game:stone-granite");
        await player.TeleportTo(post.WestCopy());
        player.Entity.WatchedAttributes.SetString("vssidingFraming", "post");
        await WallBuilder.Press(World, player, post.DownCopy(), "game:bamboostakes", BlockFacing.UP, new Vec3d(0.2, 1, 0.8));

        player.Entity.WatchedAttributes.SetString("vssidingFraming", "floor");
        player.Entity.WatchedAttributes.SetString("vssidingDeck", "deck");
        await WallBuilder.Press(World, player, post, "game:bamboostakes", BlockFacing.EAST, new Vec3d(1, 0.5, 0.5));

        Assert.Equal(
            ("floor", ((string?)null, (string?)null, (string?)null, (string?)null)),
            (World.BlockAt(post.EastCopy()).Code.Path, WallBuilder.DeckLayers(World, post)));
    }

    // A bare bamboo frame, then a plain stake click on its side with Floor picked, which fills it rather than laying a floor.
    private async Task<BlockPos> FillWithStakes()
    {
        (BlockPos cell, ITestPlayer player) = await RaiseBareFrame();
        player.Entity.WatchedAttributes.SetString("vssidingFraming", "floor");
        await player.GiveItem("game:bamboostakes", 4);
        var sideSel = new BlockSelection { Position = cell.Copy(), Face = BlockFacing.EAST, HitPosition = new Vec3d(0.5, 0.5, 0.5) };
        Assert.True(World.BlockAt(cell).OnBlockInteractStart(player.Entity.World, player.Player, sideSel));
        return cell;
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
