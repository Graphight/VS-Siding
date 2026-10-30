using System;
using System.Linq;
using System.Threading.Tasks;
using Atlas.Api;
using Atlas.XUnit;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Xunit;

namespace VSSiding.E2E.Tests;

// Runs with `just probe kevinsfurniture`, the only run that loads Kevins Furniture; the probe itself places
// the Celtic bed headboard-first, so these cover its two long sides. Its fillers stand west of a north bed.
[Trait("Category", "Probe")]
[AtlasWorld(Mods = new[] { "../../../../compat/mods/slot0.zip", "../../../../compat/mods/slot1.zip", "../../../../compat/mods/slot2.zip", "../../../../compat/mods/slot3.zip", "../../../../compat/mods/slot4.zip", "../../../../compat/mods/slot5.zip" })]
public class CelticBedScenarios : AtlasScenarioBase
{
    private static bool KevinsLoaded => Environment.GetEnvironmentVariable("SIDING_PROBE") == "kevinsfurniture";

    [AtlasScenario(FreshWorld = true)]
    public async Task CelticBed_Should_ShiftByItsFillersWalls_When_OnlyItsFillerRowIsInTheWall()
    {
        if (!KevinsLoaded) return;

        // Panel to the west, room to the east: the bed stands in the room and its fillers take the wall.
        BlockPos headFiller = World.Spawn.Offset(1, 2, 0);
        BlockPos head = headFiller.EastCopy();
        ITestPlayer player = await WallBuilder.JoinBuilder(World);
        try
        {
            await RaiseRun(player, headFiller, BlockFacing.WEST);
            Place(player, head);

            Assert.Equal(("kevinsfurniture:celticbed-head-north", "kevinsfurniture:celticbed-feet-north"), (World.BlockAt(head).Code.ToString(), World.BlockAt(head.NorthCopy()).Code.ToString()));
            Assert.True(WallBuilder.GuestLayers(World, headFiller) != null && WallBuilder.GuestLayers(World, headFiller.NorthCopy()) != null);

            // The bed's boxes are its own cell's, shifted a panel's thickness off the fillers' panels and clipped to the cell.
            var shifted = new[] { (0.25f, 0f, 0f, 1f, 0.5f, 1f) };
            Assert.Equal(new[] { shifted, shifted }, new[] { Boxes(head), Boxes(head.NorthCopy()) });

            await BreaksBackToWalls(player, head, headFiller, headFiller.NorthCopy());
        }
        finally
        {
            player.Player.Disconnect();
            await World.Until(() => !player.IsConnected);
        }
    }

    [AtlasScenario(FreshWorld = true)]
    public async Task CelticBed_Should_HostItsHeadAndFeet_When_ItsBedRowIsInTheWall()
    {
        if (!KevinsLoaded) return;

        // Panel to the east, room to the west: the head and feet take the wall and the fillers stand in the room.
        BlockPos head = World.Spawn.Offset(1, 2, 0);
        ITestPlayer player = await WallBuilder.JoinBuilder(World);
        try
        {
            await RaiseRun(player, head, BlockFacing.EAST);
            Place(player, head);

            Assert.Equal(("kevinsfurniture:celticbed-head-north", "kevinsfurniture:celticbed-feet-north"), (World.BlockAt(head).Code.ToString(), World.BlockAt(head.NorthCopy()).Code.ToString()));
            Assert.True(WallBuilder.GuestLayers(World, head) != null && WallBuilder.GuestLayers(World, head.NorthCopy()) != null);

            await BreaksBackToWalls(player, head, head, head.NorthCopy());
        }
        finally
        {
            player.Player.Disconnect();
            await World.Until(() => !player.IsConnected);
        }
    }

    // A run of two walls, the cell and the one north of it, its panel on the given face.
    private async Task RaiseRun(ITestPlayer player, BlockPos cell, BlockFacing panel)
    {
        foreach (BlockPos column in new[] { cell, cell.NorthCopy() })
        {
            World.SetBlock("game:planks-aged-ud", column.DownCopy());
            World.SetBlock("game:planks-aged-ud", column.DownCopy().AddCopy(panel.Opposite));
            await WallBuilder.Raise(World, player, column, column.AddCopy(panel), "game:plank-oak", "game:clay-blue", "game:plank-oak");
        }
    }

    // The player stands two cells north of the feet, so the bed faces south and its head lands on the given cell.
    private void Place(ITestPlayer player, BlockPos head)
    {
        BlockPos feet = head.NorthCopy();
        player.Entity.Pos.SetPos(feet.X + 0.5, feet.Y, feet.Z - 1.5);
        var stack = new ItemStack(World.Api.World.GetBlock(new AssetLocation("kevinsfurniture:celticbed-head-north")));
        var selection = new BlockSelection { Position = feet.Copy(), Face = BlockFacing.UP, HitPosition = new Vec3d(0.5, 0, 0.5) };
        string failureCode = "";
        Assert.True(stack.Block.TryPlaceBlock(player.Entity.World, player.Player, stack, selection, ref failureCode), failureCode);
    }

    private (float, float, float, float, float, float)[] Boxes(BlockPos pos)
        => World.BlockAt(pos).GetCollisionBoxes(World.Api.World.BlockAccessor, pos).Select(b => (b.X1, b.Y1, b.Z1, b.X2, b.Y2, b.Z2)).ToArray();

    private async Task BreaksBackToWalls(ITestPlayer player, BlockPos head, params BlockPos[] walls)
    {
        World.Api.World.BlockAccessor.BreakBlock(head, player.Player);
        await World.Until(() => walls.All(wall => World.BlockAt(wall).Code is { Domain: "vssiding" } code && code.Path.StartsWith("wall")), 100);
    }
}
