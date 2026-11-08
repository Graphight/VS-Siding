using System.Linq;
using System.Threading.Tasks;
using Atlas.Api;
using Atlas.XUnit;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Xunit;

namespace VSSiding.E2E.Tests;

// Sticks are a framing and the wattle infill at once (decision 0058): a plain click on a bare frame's face fills it, and on its top or an end frames on (decision 0088).
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

    [AtlasScenario(FreshWorld = true)]
    public async Task Wall_Should_FillWithWattle_When_SticksClickABareFramesFace()
    {
        (BlockPos cell, ITestPlayer player) = await RaiseBareStickFrame();

        await WallBuilder.Press(World, player, cell, "game:stick", BlockFacing.EAST, new Vec3d(1, 0.5, 0.5));

        Assert.Equal(("sticks", "wattle", null, null, null), WallBuilder.Layers(World, cell));
    }

    [AtlasScenario(FreshWorld = true)]
    public async Task Frame_Should_StackOnABareStickFrame_When_SticksClickItsTopWithoutSneaking()
    {
        (BlockPos cell, ITestPlayer player) = await RaiseBareStickFrame();

        await WallBuilder.Press(World, player, cell, "game:stick", BlockFacing.UP, new Vec3d(0.5, 1, 0.5));

        Assert.Equal(
            (("sticks", null, null, null, null), ("sticks", null, null, null, null)),
            (WallBuilder.Layers(World, cell), WallBuilder.Layers(World, cell.UpCopy())));
    }

    [AtlasScenario(FreshWorld = true)]
    public async Task Frame_Should_ExtendABareStickFrame_When_SticksClickItsEnd()
    {
        (BlockPos cell, ITestPlayer player) = await RaiseBareStickFrame();

        await WallBuilder.Press(World, player, cell, "game:stick", BlockFacing.NORTH, new Vec3d(0.5, 0.5, 0));

        Assert.Equal(
            (("sticks", null, null, null, null), ("sticks", null, null, null, null)),
            (WallBuilder.Layers(World, cell), WallBuilder.Layers(World, cell.NorthCopy())));
    }

    // Vanilla's GroundStorable sits ahead of PlaceWallFrame on sticks and answers a sneak-click first.
    [AtlasScenario(FreshWorld = true)]
    public async Task Frame_Should_StackOnABareStickFrame_When_SticksSneakClickItsTop()
    {
        (BlockPos cell, ITestPlayer player) = await RaiseBareStickFrame();

        await SneakClickTop(player, cell);

        Assert.Equal(
            (("sticks", null, null, null, null), ("sticks", null, null, null, null)),
            (WallBuilder.Layers(World, cell), WallBuilder.Layers(World, cell.UpCopy())));
    }

    [AtlasScenario(FreshWorld = true)]
    public async Task Floor_Should_CarryOn_When_SticksSneakClickABareStickFloorsTop()
    {
        BlockPos cell = World.Spawn.Offset(1, 2, 0);
        World.SetBlock("game:planks-aged-ud", cell.DownCopy());
        ITestPlayer player = await WallBuilder.JoinBuilder(World, "game:stone-granite");
        await player.TeleportTo(cell.WestCopy());
        player.Entity.WatchedAttributes.SetString("vssidingFraming", "floor");

        await player.GiveItem("game:stick", 4);
        WallBuilder.ClickTop(player, cell.DownCopy());

        await SneakClickTop(player, cell);

        // The run goes on the way the player looks, so which neighbour took the joists is not pinned.
        Assert.Equal(
            (("sticks", null, null, null, null), ("sticks", null, null, null, null)),
            (WallBuilder.Layers(World, cell), BlockFacing.HORIZONTALS
                .Select(side => cell.AddCopy(side))
                .Where(pos => World.BlockEntityAt<BlockEntity>(pos) != null)
                .Select(pos => WallBuilder.Layers(World, pos))
                .Single()));
    }

    [AtlasScenario(FreshWorld = true)]
    public async Task Floor_Should_FrameOnFromItsEdgeAndFillFromItsTop_When_SticksClickABareStickFloor()
    {
        BlockPos cell = World.Spawn.Offset(1, 2, 0);
        World.SetBlock("game:planks-aged-ud", cell.DownCopy());
        ITestPlayer player = await WallBuilder.JoinBuilder(World, "game:stone-granite");
        await player.TeleportTo(cell.WestCopy());
        player.Entity.WatchedAttributes.SetString("vssidingFraming", "floor");
        await player.GiveItem("game:stick", 4);
        WallBuilder.ClickTop(player, cell.DownCopy());

        await WallBuilder.Press(World, player, cell, "game:stick", BlockFacing.NORTH, new Vec3d(0.5, 0.9, 0));
        (string? Framing, string? Infill, string? Front, string? SecondFront, string? Back) afterEdge = WallBuilder.Layers(World, cell);
        BlockPos next = cell.NorthCopy();
        var nextLayers = World.BlockEntityAt<BlockEntity>(next) == null ? default : WallBuilder.Layers(World, next);
        await WallBuilder.Press(World, player, cell, "game:stick", BlockFacing.UP, new Vec3d(0.5, 1, 0.5));

        Assert.Equal(
            (("sticks", null, null, null, null), ("sticks", null, null, null, null), ("sticks", "wattle", null, null, null)),
            (afterEdge, nextLayers, WallBuilder.Layers(World, cell)));
    }

    [AtlasScenario(FreshWorld = true)]
    public async Task Deck_Should_FrameOnFromItsEdgeAndFillFromItsTop_When_SticksClickABareDeck()
    {
        BlockPos cell = World.Spawn.Offset(1, 2, 0);
        World.SetBlock("game:planks-aged-ud", cell.DownCopy());
        ITestPlayer player = await WallBuilder.JoinBuilder(World);
        player.Entity.WatchedAttributes.SetString("vssidingFraming", "wall");
        await WallBuilder.Raise(World, player, cell, cell.WestCopy(), "game:plank-oak", "game:clay-blue", null);
        player.Entity.WatchedAttributes.SetString("vssidingDeck", "deck");
        await WallBuilder.Press(World, player, cell, "game:plank-oak", BlockFacing.EAST, new Vec3d(1, 0.5, 0.5), quantity: 2);
        player.Entity.WatchedAttributes.SetString("vssidingFraming", "floor");

        await WallBuilder.Press(World, player, cell, "game:stick", BlockFacing.NORTH, new Vec3d(0.5, 0.9, 0), onDeck: true);
        var afterEdge = WallBuilder.DeckLayers(World, cell);
        await WallBuilder.Press(World, player, cell, "game:stick", BlockFacing.UP, new Vec3d(0.5, 1, 0.5), onDeck: true);

        Assert.Equal((("oak", null, null, null), ("oak", "wattle", null, null)), (afterEdge, WallBuilder.DeckLayers(World, cell)));
    }

    // A frame the sticks cannot pay for still claims the click, so a later stick behavior never runs in its place (#143).
    [AtlasScenario(FreshWorld = true)]
    public async Task Click_Should_StayHandled_When_TooFewSticksForAFrame()
    {
        BlockPos cell = World.Spawn.Offset(1, 2, 0);
        World.SetBlock("game:planks-aged-ud", cell.DownCopy());
        ITestPlayer player = await WallBuilder.JoinBuilder(World);

        await player.GiveItem("game:stick", 1);
        EnumHandHandling handling = WallBuilder.ClickTop(player, cell.DownCopy());

        Assert.Equal((EnumHandHandling.PreventDefault, "game:air", 1), (handling, World.BlockAt(cell).Code.ToString(), player.Player.InventoryManager.ActiveHotbarSlot.Itemstack?.StackSize));
    }

    private static async Task SneakClickTop(ITestPlayer player, BlockPos cell)
    {
        await player.GiveItem("game:stick", 4);
        player.Entity.Controls.ShiftKey = true;
        EnumHandHandling handling = WallBuilder.ClickTop(player, cell);
        Assert.Equal((EnumHandHandling.PreventDefault, null), (handling, player.Player.InventoryManager.ActiveHotbarSlot.Itemstack));
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
