using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Atlas.Api;
using Atlas.XUnit;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Xunit;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace VSSiding.E2E.Tests;

[AtlasWorld(StrictBootDiagnostics = true)]
public class GuestWallScenarios : AtlasScenarioBase
{
    private static readonly (string?, string?, string?, string?, string?) Expected = ("oak", "clay", null, null, "planks");

    private BlockPos Cell => World.Spawn.Offset(1, 2, 0);

    [AtlasScenario(RollbackWorld = true, StrictIsolation = true)]
    public async Task C_Wall_Should_BecomeAGuest_When_ALanternIsClickedOntoIt()
    {
        BlockPos cell = Cell;
        World.SetBlock("game:planks-aged-ud", cell.DownCopy());

        ITestPlayer player = await WallBuilder.JoinBuilder(World);
        try
        {
            await WallBuilder.Raise(World, player, cell, cell.WestCopy(), "game:plank-oak", "game:clay-blue", "game:plank-oak");

            player.Entity.LeftHandItemSlot.Itemstack = null;
            ItemSlot slot = player.Player.InventoryManager.ActiveHotbarSlot;
            slot.Itemstack = WallBuilder.LanternStack(World);
            var selection = new BlockSelection { Position = cell.Copy(), Face = BlockFacing.EAST, HitPosition = new Vec3d(0.5, 0.5, 0.5) };
            Assert.True(World.BlockAt(cell).OnBlockInteractStart(player.Entity.World, player.Player, selection));

            Assert.Equal("game:lantern-large-east", World.BlockAt(cell).Code.ToString());
            Assert.Equal(Expected, WallBuilder.GuestLayers(World, cell));
            Assert.True(slot.Empty);

            await World.Ticks(10);
            Assert.Equal("game:lantern-large-east", World.BlockAt(cell).Code.ToString());
        }
        finally
        {
            player.Player.Disconnect();
            await World.Until(() => !player.IsConnected);
        }
    }

    [AtlasScenario(RollbackWorld = true, StrictIsolation = true)]
    public async Task D_Wall_Should_BecomeAGuest_When_ALanternIsPlacedOnTheFloorOfItsCell()
    {
        BlockPos cell = Cell;
        World.SetBlock("game:planks-aged-ud", cell.DownCopy());

        ITestPlayer player = await WallBuilder.JoinBuilder(World);
        try
        {
            await WallBuilder.Raise(World, player, cell, cell.WestCopy(), "game:plank-oak", "game:clay-blue", "game:plank-oak");

            ItemStack stack = WallBuilder.LanternStack(World);
            var selection = new BlockSelection { Position = cell.Copy(), Face = BlockFacing.UP, HitPosition = new Vec3d(0.5, 0.5, 0.5) };
            string failureCode = "";
            Assert.True(stack.Block.TryPlaceBlock(player.Entity.World, player.Player, stack, selection, ref failureCode));

            Assert.Equal("game:lantern-large-up", World.BlockAt(cell).Code.ToString());
            Assert.Equal(Expected, WallBuilder.GuestLayers(World, cell));

            await World.Ticks(10);
            Assert.Equal("game:lantern-large-up", World.BlockAt(cell).Code.ToString());
        }
        finally
        {
            player.Player.Disconnect();
            await World.Until(() => !player.IsConnected);
        }
    }

    // A bare frame holds nothing up, so the lantern takes the next face that does: the floor of the wall's cell.
    [AtlasScenario(RollbackWorld = true, StrictIsolation = true)]
    public async Task E_Lantern_Should_StandOnTheFloorOfTheCell_When_ClickedOntoABareFrame()
    {
        BlockPos cell = Cell;
        World.SetBlock("game:planks-aged-ud", cell.DownCopy());

        ITestPlayer player = await WallBuilder.JoinBuilder(World);
        try
        {
            await WallBuilder.Raise(World, player, cell, cell.WestCopy(), "game:plank-oak", null);

            player.Entity.LeftHandItemSlot.Itemstack = null;
            ItemSlot slot = player.Player.InventoryManager.ActiveHotbarSlot;
            slot.Itemstack = WallBuilder.LanternStack(World);
            var selection = new BlockSelection { Position = cell.Copy(), Face = BlockFacing.EAST, HitPosition = new Vec3d(0.5, 0.5, 0.5) };
            Assert.True(World.BlockAt(cell).OnBlockInteractStart(player.Entity.World, player.Player, selection));

            Assert.Equal("game:lantern-large-up", World.BlockAt(cell).Code.ToString());
            Assert.Equal(("oak", null, null, null, null), WallBuilder.GuestLayers(World, cell));
            Assert.True(slot.Empty);
        }
        finally
        {
            player.Player.Disconnect();
            await World.Until(() => !player.IsConnected);
        }
    }

    // Asserted with no tick between: a wall that came back a tick late was sent to clients as an empty cell first.
    [AtlasScenario(RollbackWorld = true, StrictIsolation = true)]
    public async Task F_Wall_Should_ComeBackAtOnce_When_ItsLanternIsPickedUp()
    {
        BlockPos cell = Cell;
        World.SetBlock("game:planks-aged-ud", cell.DownCopy());

        ITestPlayer player = await WallBuilder.JoinBuilder(World);
        try
        {
            await WallBuilder.Raise(World, player, cell, cell.WestCopy(), "game:plank-oak", "game:clay-blue", "game:plank-oak");

            player.Entity.LeftHandItemSlot.Itemstack = null;
            player.Player.InventoryManager.ActiveHotbarSlot.Itemstack = WallBuilder.LanternStack(World);
            var selection = new BlockSelection { Position = cell.Copy(), Face = BlockFacing.EAST, HitPosition = new Vec3d(0.5, 0.5, 0.5) };
            Assert.True(World.BlockAt(cell).OnBlockInteractStart(player.Entity.World, player.Player, selection));
            Assert.Equal("game:lantern-large-east", World.BlockAt(cell).Code.ToString());

            Assert.True(World.BlockAt(cell).OnBlockInteractStart(player.Entity.World, player.Player, selection));

            Assert.Equal("vssiding:wall-wall-west", World.BlockAt(cell).Code.ToString());
            Assert.Equal(Expected, WallBuilder.Layers(World, cell));
            Assert.Null(WallBuilder.GuestLayers(World, cell));
        }
        finally
        {
            player.Player.Disconnect();
            await World.Until(() => !player.IsConnected);
        }
    }

    [AtlasScenario(RollbackWorld = true, StrictIsolation = true)]
    public async Task G_Wall_Should_BecomeAGuest_When_ATableIsPlacedInItsCell()
    {
        BlockPos cell = Cell;
        World.SetBlock("game:planks-aged-ud", cell.DownCopy());

        ITestPlayer player = await WallBuilder.JoinBuilder(World);
        try
        {
            await WallBuilder.Raise(World, player, cell, cell.WestCopy(), "game:plank-oak", "game:clay-blue", "game:plank-oak");

            player.Entity.LeftHandItemSlot.Itemstack = null;
            ItemSlot slot = player.Player.InventoryManager.ActiveHotbarSlot;
            slot.Itemstack = new ItemStack(World.Api.World.GetBlock(new AssetLocation("game:table-normal")));
            var selection = new BlockSelection { Position = cell.Copy(), Face = BlockFacing.EAST, HitPosition = new Vec3d(0.5, 0.5, 0.5) };
            Assert.True(World.BlockAt(cell).OnBlockInteractStart(player.Entity.World, player.Player, selection));

            Assert.Equal("game:table-normal", World.BlockAt(cell).Code.ToString());
            Assert.Equal(Expected, WallBuilder.GuestLayers(World, cell));
            Assert.True(slot.Empty);
        }
        finally
        {
            player.Player.Disconnect();
            await World.Until(() => !player.IsConnected);
        }
    }
}
