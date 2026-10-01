using System.Threading.Tasks;
using Atlas.Api;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Xunit;

namespace VSSiding.E2E.Tests;

internal static class WallBuilder
{
    internal static void HoldSaw(ITestPlayer player)
        => player.Entity.LeftHandItemSlot.Itemstack = new ItemStack(player.Entity.World.GetItem(new AssetLocation("game:saw-copper")));

    // The player stands outside the cell looking in, as a builder does: the wall then claims the face towards the room.
    internal static async Task Raise(IWorldSession world, ITestPlayer player, BlockPos cell, BlockPos inside, string framing, string? infill)
    {
        BlockFacing outward = BlockFacing.FromVector(cell.X - inside.X, 0, cell.Z - inside.Z);
        BlockPos outside = cell.AddCopy(outward);
        await player.TeleportTo(outside);
        player.Entity.Pos.SetPos(outside.X + 0.5, outside.Y, outside.Z + 0.5);
        await player.GiveItem(framing, 2);

        ItemSlot slot = player.Player.InventoryManager.ActiveHotbarSlot;
        var frameSel = new BlockSelection { Position = cell.DownCopy(), Face = BlockFacing.UP, HitPosition = new Vec3d(0.5, 1, 0.5) };
        var handling = EnumHandHandling.NotHandled;
        slot.Itemstack!.Collectible.OnHeldInteractStart(slot, player.Entity, frameSel, null, true, ref handling);
        Assert.Equal(EnumHandHandling.PreventDefault, handling);

        if (infill == null) return;

        await player.GiveItem(infill, 4);
        var infillSel = new BlockSelection { Position = cell.Copy(), Face = outward, HitPosition = new Vec3d(0.5, 0.5, 0.5) };
        Assert.True(world.BlockAt(cell).OnBlockInteractStart(player.Entity.World, player.Player, infillSel));
    }
}
