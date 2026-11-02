using System;
using System.Threading.Tasks;
using Atlas.Api;
using Atlas.XUnit;
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

        (BlockPos cell, ITestPlayer player) = await JoinBesideGround(withSaw: true);
        try
        {
            int? sticksLeft = await ClickGroundTop(player, cell, 4);

            Assert.Equal(("vssiding", ("sticks", null, null, null, null), null), (World.BlockAt(cell).Code.Domain, WallBuilder.Layers(World, cell), sticksLeft));
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

        (BlockPos cell, ITestPlayer player) = await JoinBesideGround(withSaw: true);
        try
        {
            int? sticksLeft = await ClickGroundTop(player, cell, 1);

            Assert.Equal(("game:air", 1), (World.BlockAt(cell).Code.ToString(), sticksLeft));
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

        (BlockPos cell, ITestPlayer player) = await JoinBesideGround(withSaw: false);
        try
        {
            int? sticksLeft = await ClickGroundTop(player, cell, 4);

            Assert.Equal(("immersivewoodworking:sawhorse", 3), (World.BlockAt(cell).Code.ToString(), sticksLeft));
        }
        finally
        {
            await Leave(player);
        }
    }

    // Soil under an empty cell, the ground the reports were on, with the player standing west of the cell.
    private async Task<(BlockPos Cell, ITestPlayer Player)> JoinBesideGround(bool withSaw)
    {
        BlockPos cell = World.Spawn.Offset(1, 2, 0);
        World.SetBlock("game:soil-medium-none", cell.DownCopy());
        ITestPlayer player = await WallBuilder.JoinBuilder(World);
        if (!withSaw) player.Entity.LeftHandItemSlot.Itemstack = null;
        await player.TeleportTo(cell.WestCopy());
        return (cell, player);
    }

    private static async Task<int?> ClickGroundTop(ITestPlayer player, BlockPos cell, int sticks)
    {
        await player.GiveItem("game:stick", sticks);
        WallBuilder.ClickTop(player, cell.DownCopy());
        return player.Player.InventoryManager.ActiveHotbarSlot.Itemstack?.StackSize;
    }

    private async Task Leave(ITestPlayer player)
    {
        player.Player.Disconnect();
        await World.Until(() => !player.IsConnected);
    }
}
