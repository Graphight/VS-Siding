using System;
using System.Threading.Tasks;
using Atlas.Api;
using Atlas.XUnit;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Xunit;

namespace VSSiding.E2E.Tests;

// Runs with `just probe immersivewoodworking`, the only run that loads Immersive Woodworking. Its sawhorse takes a
// plain stick click on a block's top whatever the off hand holds, so a saw or stone there has to win the click (#143).
[Trait("Category", "Probe")]
[AtlasWorld(Mods = new[] { "../../../../compat/mods/slot0.zip", "../../../../compat/mods/slot1.zip", "../../../../compat/mods/slot2.zip", "../../../../compat/mods/slot3.zip", "../../../../compat/mods/slot4.zip", "../../../../compat/mods/slot5.zip" })]
public class ImmersiveWoodworkingScenarios : AtlasScenarioBase
{
    private static bool WoodworkingLoaded => Environment.GetEnvironmentVariable("SIDING_PROBE") == "immersivewoodworking";

    [AtlasScenario(FreshWorld = true)]
    public async Task Frame_Should_RaiseWithoutASawhorse_When_SticksClickTheGroundWithASawInTheOffHand()
    {
        if (!WoodworkingLoaded) return;

        (BlockPos cell, ITestPlayer player) = await JoinBesideGround("game:saw-copper");
        try
        {
            ClickGroundTop(player, cell, 4);

            Assert.Equal(("vssiding", ("sticks", (string?)null, (string?)null, (string?)null, (string?)null)), (World.BlockAt(cell).Code.Domain, WallBuilder.Layers(World, cell)));
        }
        finally
        {
            await Leave(player);
        }
    }

    [AtlasScenario(FreshWorld = true)]
    public async Task Cell_Should_StayEmpty_When_TooFewSticksClickTheGroundWithASawInTheOffHand()
    {
        if (!WoodworkingLoaded) return;

        (BlockPos cell, ITestPlayer player) = await JoinBesideGround("game:saw-copper");
        try
        {
            ItemSlot slot = ClickGroundTop(player, cell, 1);

            Assert.Equal(("game:air", 1), (World.BlockAt(cell).Code.ToString(), slot.Itemstack?.StackSize));
        }
        finally
        {
            await Leave(player);
        }
    }

    [AtlasScenario(FreshWorld = true)]
    public async Task Sawhorse_Should_Build_When_SticksClickTheGroundWithAnEmptyOffHand()
    {
        if (!WoodworkingLoaded) return;

        (BlockPos cell, ITestPlayer player) = await JoinBesideGround(null);
        try
        {
            ItemSlot slot = ClickGroundTop(player, cell, 4);

            Assert.Equal(("immersivewoodworking:sawhorse", 3), (World.BlockAt(cell).Code.ToString(), slot.Itemstack?.StackSize));
        }
        finally
        {
            await Leave(player);
        }
    }

    // Soil under an empty cell, the ground the reports were on, with the player standing west of the cell.
    private async Task<(BlockPos Cell, ITestPlayer Player)> JoinBesideGround(string? offhand)
    {
        BlockPos cell = World.Spawn.Offset(1, 2, 0);
        World.SetBlock("game:soil-medium-none", cell.DownCopy());
        ITestPlayer player = await WallBuilder.JoinBuilder(World);
        if (offhand == null) player.Entity.LeftHandItemSlot.Itemstack = null;
        await player.TeleportTo(cell.WestCopy());
        return (cell, player);
    }

    private ItemSlot ClickGroundTop(ITestPlayer player, BlockPos cell, int sticks)
    {
        ItemSlot slot = player.Player.InventoryManager.ActiveHotbarSlot;
        slot.Itemstack = new ItemStack(World.Api.World.GetItem(new AssetLocation("game:stick")), sticks);
        var groundSel = new BlockSelection { Position = cell.DownCopy(), Face = BlockFacing.UP, HitPosition = new Vec3d(0.5, 1, 0.5) };
        var handling = EnumHandHandling.NotHandled;
        slot.Itemstack.Collectible.OnHeldInteractStart(slot, player.Entity, groundSel, null, true, ref handling);
        return slot;
    }

    private async Task Leave(ITestPlayer player)
    {
        player.Player.Disconnect();
        await World.Until(() => !player.IsConnected);
    }
}
