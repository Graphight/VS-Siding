using System;
using System.Linq;
using System.Threading.Tasks;
using Atlas.Api;
using Atlas.XUnit;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Xunit;

namespace VSSiding.E2E.Tests;

// Runs with `SIDING_PROBE=industrialstory`, the only run that loads IndustrialStory: its glass panes and coloured leaded
// panes glaze a wall or a floor one for one and come back when the glazing is peeled (#135).
[Trait("Category", "Probe")]
[AtlasWorld(Mods = new[] { "../../../../compat/mods/slot0.zip", "../../../../compat/mods/slot1.zip", "../../../../compat/mods/slot2.zip", "../../../../compat/mods/slot3.zip", "../../../../compat/mods/slot4.zip", "../../../../compat/mods/slot5.zip" })]
public class IndustrialStoryScenarios : AtlasScenarioBase
{
    private static bool IndustrialStoryLoaded => Environment.GetEnvironmentVariable("SIDING_PROBE") == "industrialstory";

    [AtlasScenario(RollbackWorld = true, StrictIsolation = true)]
    public async Task Wall_Should_GlazeWithAPaneAndRefundIt_When_AGlassPaneFillsAFrame()
        => await GlazeAndPeel("industrialstory:glass-pane-green", "glass-pane-green", false);

    [AtlasScenario(RollbackWorld = true, StrictIsolation = true)]
    public async Task Wall_Should_GlazeWithALeadPaneAndRefundIt_When_ALeadGlassPaneFillsAFrame()
        => await GlazeAndPeel("industrialstory:glass-pane-lead", "glass-pane-lead", false);

    [AtlasScenario(RollbackWorld = true, StrictIsolation = true)]
    public async Task Wall_Should_GlazeWithALeadedPaneAndRefundIt_When_AColouredLeadedPaneFillsAFrame()
        => await GlazeAndPeel("industrialstory:leadedglasspane-green-oak-ew", "leadedglasspane-green-oak", false);

    [AtlasScenario(RollbackWorld = true, StrictIsolation = true)]
    public async Task Floor_Should_GlazeWithAPane_When_AGlassPaneFillsAFloorFrame()
        => await GlazeAndPeel("industrialstory:glass-pane-plain", "glass-pane-plain", true);

    private async Task GlazeAndPeel(string held, string key, bool floor)
    {
        if (!IndustrialStoryLoaded) return;

        BlockPos cell = World.Spawn.Offset(1, 2, 0);
        World.SetBlock("game:planks-aged-ud", cell.DownCopy());
        ITestPlayer player = await WallBuilder.JoinBuilder(World);
        if (floor) player.Entity.WatchedAttributes.SetString("vssidingFraming", "floor");
        await WallBuilder.Raise(World, player, cell, cell.WestCopy(), "game:plank-oak", null);

        await player.GiveItem(held, 1);
        var sel = new BlockSelection { Position = cell.Copy(), Face = floor ? BlockFacing.UP : BlockFacing.EAST, HitPosition = new Vec3d(0.5, floor ? 1 : 0.5, 0.5) };
        bool filled = World.BlockAt(cell).OnBlockInteractStart(player.Entity.World, player.Player, sel);
        var glazed = (World.BlockAt(cell).FirstCodePart(), filled, player.Player.InventoryManager.ActiveHotbarSlot.Itemstack, WallBuilder.Layers(World, cell));

        World.BlockAt(cell).OnBlockBroken(player.Entity.World, cell, player.Player);
        await World.Ticks(2);
        // The builder stands beside the cell, so the refund lands in the hotbar rather than staying on the ground.
        int refunded = player.Player.InventoryManager.GetHotbarInventory().Where(slot => slot.Itemstack?.Collectible.Code.ToString() == held).Sum(slot => slot.StackSize)
            + World.Api.World.GetEntitiesAround(cell.ToVec3d().Add(0.5, 0.5, 0.5), 3, 3, entity => entity is EntityItem)
                .Cast<EntityItem>().Where(item => item.Itemstack.Collectible.Code.ToString() == held).Sum(item => item.Itemstack.StackSize);

        Assert.Equal(((floor ? "floor" : "wall", true, (ItemStack?)null, ("oak", key, (string?)null, (string?)null, (string?)null)), 1), (glazed, refunded));
    }
}
