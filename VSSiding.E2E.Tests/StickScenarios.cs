using System.Linq;
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
        ItemSlot slot = player.Player.InventoryManager.ActiveHotbarSlot;
        var groundSel = new BlockSelection { Position = cell.DownCopy(), Face = BlockFacing.UP, HitPosition = new Vec3d(0.5, 1, 0.5) };
        var handling = EnumHandHandling.NotHandled;
        slot.Itemstack!.Collectible.OnHeldInteractStart(slot, player.Entity, groundSel, null, true, ref handling);

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

    private static async Task SneakClickTop(ITestPlayer player, BlockPos cell)
    {
        await player.GiveItem("game:stick", 4);
        ItemSlot slot = player.Player.InventoryManager.ActiveHotbarSlot;
        player.Entity.Controls.ShiftKey = true;
        var topSel = new BlockSelection { Position = cell.Copy(), Face = BlockFacing.UP, HitPosition = new Vec3d(0.5, 1, 0.5) };
        var handling = EnumHandHandling.NotHandled;
        slot.Itemstack!.Collectible.OnHeldInteractStart(slot, player.Entity, topSel, null, true, ref handling);
        Assert.Equal((EnumHandHandling.PreventDefault, null), (handling, slot.Itemstack));
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
