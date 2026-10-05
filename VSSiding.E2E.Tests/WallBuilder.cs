using System;
using System.Linq;
using System.Threading.Tasks;
using Atlas.Api;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Xunit;

namespace VSSiding.E2E.Tests;

internal static class WallBuilder
{
    // Atlas's default play style joins players in creative, which skips the mod's cost checks and consumption.
    internal static async Task<ITestPlayer> JoinBuilder(IWorldSession world, string offhand = "game:saw-copper")
    {
        ITestPlayer player = await world.JoinPlayer("Builder");
        player.Player.WorldData.CurrentGameMode = EnumGameMode.Survival;
        player.Entity.LeftHandItemSlot.Itemstack = new ItemStack(player.Entity.World.GetItem(new AssetLocation(offhand)));
        return player;
    }

    // framing, infill, front, secondfront, back as the wall's entity saves them; the entity type is the mod's own, so it is read through its tree.
    internal static (string? Framing, string? Infill, string? Front, string? SecondFront, string? Back) Layers(IWorldSession world, BlockPos cell)
        => LayersOf(world.BlockEntityAt<BlockEntity>(cell)!);

    // The guest store's record for a cell, decoded the way the mod does; null when the cell holds no guest. GuestWalls is the mod's own static class, so it is reached by name.
    internal static (string? Framing, string? Infill, string? Front, string? SecondFront, string? Back)? GuestLayers(IWorldSession world, BlockPos cell)
    {
        Type guestWalls = world.Api.ModLoader.GetModSystem("VSSiding.SidingModSystem").GetType().Assembly.GetType("VSSiding.GuestWalls")!;
        var guest = (BlockEntity?)guestWalls.GetMethod("GuestAt", new[] { typeof(ICoreAPI), typeof(BlockPos) })!.Invoke(null, new object[] { world.Api, cell });
        return guest == null ? null : LayersOf(guest);
    }

    private static (string? Framing, string? Infill, string? Front, string? SecondFront, string? Back) LayersOf(BlockEntity entity)
    {
        var tree = new TreeAttribute();
        entity.ToTreeAttributes(tree);
        string? Read(string key) => tree.GetString(key) is { Length: > 0 } value ? value : null;
        return (Read("framing"), Read("infill"), Read("front"), Read("secondfront"), Read("back"));
    }

    // The player stands outside the cell looking in, as a builder does: the wall then claims the face towards the room.
    internal static async Task Raise(IWorldSession world, ITestPlayer player, BlockPos cell, BlockPos inside, string framing, string? infill, string? finish = null, int framingCost = 2)
    {
        BlockFacing outward = BlockFacing.FromVector(cell.X - inside.X, 0, cell.Z - inside.Z);
        BlockPos outside = cell.AddCopy(outward);
        await player.TeleportTo(outside);
        player.Entity.Pos.SetPos(outside.X + 0.5, outside.Y, outside.Z + 0.5);
        await player.GiveItem(framing, framingCost);

        ItemSlot slot = player.Player.InventoryManager.ActiveHotbarSlot;
        var frameSel = new BlockSelection { Position = cell.DownCopy(), Face = BlockFacing.UP, HitPosition = new Vec3d(0.5, 1, 0.5) };
        var handling = EnumHandHandling.NotHandled;
        slot.Itemstack!.Collectible.OnHeldInteractStart(slot, player.Entity, frameSel, null, true, ref handling);
        Assert.Equal(EnumHandHandling.PreventDefault, handling);
        Assert.Null(slot.Itemstack);

        if (infill == null) return;

        await player.GiveItem(infill, 4);
        var infillSel = new BlockSelection { Position = cell.Copy(), Face = outward, HitPosition = new Vec3d(0.5, 0.5, 0.5) };
        Assert.True(world.BlockAt(cell).OnBlockInteractStart(player.Entity.World, player.Player, infillSel));
        Assert.Null(slot.Itemstack);

        if (finish == null) return;

        await player.GiveItem(finish, 2);
        Assert.True(world.BlockAt(cell).OnBlockInteractStart(player.Entity.World, player.Player, infillSel));
        Assert.Null(slot.Itemstack);
    }

    // A one-cell room on a plank floor under a plank roof, walled with the framing on every side and the infill on the filled ones.
    internal static async Task<(BlockPos Inside, ITestPlayer Player)> BuildRoom(
        IWorldSession world, string infill, BlockFacing[] filled, string framing = "game:plank-oak", int framingCost = 2, string offhand = "game:saw-copper")
    {
        BlockPos inside = world.Spawn.Offset(0, 2, 0);
        for (int dx = -1; dx <= 1; dx++)
            for (int dz = -1; dz <= 1; dz++)
                world.SetBlock("game:planks-aged-ud", inside.Offset(dx, -1, dz));

        ITestPlayer player = await JoinBuilder(world, offhand);
        foreach (BlockFacing side in BlockFacing.HORIZONTALS)
            await Raise(world, player, inside.AddCopy(side), inside, framing, filled.Contains(side) ? infill : null, framingCost: framingCost);

        for (int dx = -1; dx <= 1; dx++)
            for (int dz = -1; dz <= 1; dz++)
                world.SetBlock("game:planks-aged-ud", inside.Offset(dx, 1, dz));
        return (inside, player);
    }
}
