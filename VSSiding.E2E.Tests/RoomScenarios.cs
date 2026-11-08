using System.Threading.Tasks;
using Atlas.Api;
using Atlas.XUnit;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;
using Xunit;

namespace VSSiding.E2E.Tests;

[AtlasWorld(StrictBootDiagnostics = true)]
public class RoomScenarios : AtlasScenarioBase
{
    [AtlasScenario(FreshWorld = true)]
    public async Task Room_Should_BeSealedAndWarm_When_WallsAreFramedAndFilledWithWattle()
    {
        (BlockPos inside, _) = await WallBuilder.BuildRoom(World, "game:stick", BlockFacing.HORIZONTALS);
        Assert.Equal((0, 6, 0), Measure(inside));
    }

    // Sticks are wattle's item too, so the second click on each bare stick frame has to fill it, not frame again.
    [AtlasScenario(FreshWorld = true)]
    public async Task Room_Should_BeSealedAndWarm_When_BuiltFromSticksWithAStoneInTheOffHand()
    {
        (BlockPos inside, _) = await WallBuilder.BuildRoom(
            World, "game:stick", BlockFacing.HORIZONTALS, framing: "game:stick", framingCost: 4, offhand: "game:stone-granite");

        var stone = World.Api.World.GetItem(new AssetLocation("game:stone-granite"));
        var saw = World.Api.World.GetItem(new AssetLocation("game:saw-copper"));
        Assert.Equal(
            ((0, 6, 0), ("sticks", "wattle", null, null, null), EnumItemStorageFlags.General | EnumItemStorageFlags.Metallurgy | EnumItemStorageFlags.Offhand, EnumItemStorageFlags.General | EnumItemStorageFlags.Offhand),
            (Measure(inside), WallBuilder.Layers(World, inside.NorthCopy()), stone.StorageFlags, saw.StorageFlags));
    }

    [AtlasScenario(FreshWorld = true)]
    public async Task Room_Should_BeACellar_When_WallsAreFilledWithClay()
    {
        (BlockPos inside, _) = await WallBuilder.BuildRoom(World, "game:clay-blue", BlockFacing.HORIZONTALS);
        Assert.Equal((4, 2, 0), Measure(inside));
    }

    // Room Climates' well-built cellar is vanilla's count: only cooling walls and no skylight (issue #137).
    [AtlasScenario(FreshWorld = true)]
    public async Task Room_Should_HaveOnlyCoolingWalls_When_ClayWallsStandOnAndUnderStone()
    {
        (BlockPos inside, _) = await WallBuilder.BuildRoom(World, "game:clay-blue", BlockFacing.HORIZONTALS, ground: "game:rock-granite");
        var room = World.Api.ModLoader.GetModSystem<RoomRegistry>().GetRoomForPosition(inside);
        Assert.Equal((0, 0, 0), (room.NonCoolingWallCount, room.SkylightCount, room.ExitCount));
    }

    [AtlasScenario(FreshWorld = true)]
    public async Task Room_Should_Leak_When_AWallIsFramedButNotFilled()
    {
        (BlockPos inside, _) = await WallBuilder.BuildRoom(World, "game:clay-blue", new[] { BlockFacing.NORTH, BlockFacing.EAST, BlockFacing.SOUTH });
        Assert.NotEqual(0, Measure(inside).Exits);
    }

    [AtlasScenario(FreshWorld = true)]
    public async Task Room_Should_LeakUpward_When_TheDeckOfADiagonalInItsWallLineIsBare()
    {
        Assert.NotEqual(0, Measure(await BuildRoomUnderADiagonal(fillDeck: false)).Exits);
    }

    [AtlasScenario(FreshWorld = true)]
    public async Task Room_Should_SealUpward_When_TheDeckOfADiagonalInItsWallLineIsFilled()
    {
        Assert.Equal(0, Measure(await BuildRoomUnderADiagonal(fillDeck: true)).Exits);
    }

    // A one-cell room whose north wall is a decked diagonal. The roof leaves the cell over the diagonal open
    // and the corners beside it are solid, so the only way out is up through the diagonal's own cell (decision 0067).
    // The room is measured once, after the last click: the registry keeps a room's first answer until a block changes in its chunk.
    private async Task<BlockPos> BuildRoomUnderADiagonal(bool fillDeck)
    {
        BlockPos inside = World.Spawn.Offset(0, 2, 0);
        BlockPos cell = inside.NorthCopy();
        for (int dx = -1; dx <= 1; dx++)
            for (int dz = -1; dz <= 1; dz++)
                World.SetBlock("game:planks-aged-ud", inside.Offset(dx, -1, dz));

        ITestPlayer player = await WallBuilder.JoinBuilder(World);
        foreach (BlockFacing side in new[] { BlockFacing.EAST, BlockFacing.SOUTH, BlockFacing.WEST })
            await WallBuilder.Raise(World, player, inside.AddCopy(side), inside, "game:plank-oak", "game:clay-blue");

        // Raised from inside the room, so the diagonal claims the faces away from it and its open half is the room's.
        player.Entity.WatchedAttributes.SetString("vssidingFraming", "diagonal");
        player.Entity.WatchedAttributes.SetString("vssidingDeck", "deck");
        await WallBuilder.Raise(World, player, cell, cell.NorthCopy(), "game:plank-oak", "game:clay-blue", framingCost: 4);

        for (int dx = -1; dx <= 1; dx++)
        {
            for (int dz = -1; dz <= 1; dz++)
            {
                if (dx != 0 && dz != 0) World.SetBlock("game:planks-aged-ud", inside.Offset(dx, 0, dz));
                if (dx != 0 || dz != -1) World.SetBlock("game:planks-aged-ud", inside.Offset(dx, 1, dz));
            }
        }

        if (!fillDeck) return inside;

        Block wall = World.BlockAt(cell);
        var deckSel = new BlockSelection
        {
            Position = cell.Copy(), Face = BlockFacing.UP, HitPosition = new Vec3d(0.5, 1, 0.5),
            SelectionBoxIndex = wall.SelectionBoxes.Length,
        };
        await player.GiveItem("game:clay-blue", 4);
        Assert.True(wall.OnBlockInteractStart(player.Entity.World, player.Player, deckSel));
        return inside;
    }

    // Room Climates reads rooms as adjacent when one's Location grown by 1 touches the other's (issue #137).
    [AtlasScenario(FreshWorld = true)]
    public async Task Rooms_Should_BeAdjacent_When_TheyShareOneWall()
    {
        BlockPos a = World.Spawn.Offset(0, 2, 0);
        BlockPos b = a.EastCopy(2);
        for (int dx = -1; dx <= 3; dx++)
            for (int dz = -1; dz <= 1; dz++)
                World.SetBlock("game:planks-aged-ud", a.Offset(dx, -1, dz));

        ITestPlayer player = await WallBuilder.JoinBuilder(World);
        await WallBuilder.Raise(World, player, a.EastCopy(), a, "game:plank-oak", "game:clay-blue");
        foreach (BlockFacing side in new[] { BlockFacing.NORTH, BlockFacing.SOUTH, BlockFacing.WEST })
            await WallBuilder.Raise(World, player, a.AddCopy(side), a, "game:plank-oak", "game:clay-blue");
        foreach (BlockFacing side in new[] { BlockFacing.NORTH, BlockFacing.SOUTH, BlockFacing.EAST })
            await WallBuilder.Raise(World, player, b.AddCopy(side), b, "game:plank-oak", "game:clay-blue");

        for (int dx = -1; dx <= 3; dx++)
            for (int dz = -1; dz <= 1; dz++)
                World.SetBlock("game:planks-aged-ud", a.Offset(dx, 1, dz));
        // Where the shared wall meets the side walls; left open, B leaks and A does not.
        World.SetBlock("game:planks-aged-ud", a.Offset(1, 0, -1));
        World.SetBlock("game:planks-aged-ud", a.Offset(1, 0, 1));

        var registry = World.Api.ModLoader.GetModSystem<RoomRegistry>();
        Room roomA = registry.GetRoomForPosition(a);
        Room roomB = registry.GetRoomForPosition(b);
        Assert.Equal(
            (0, 0, false, true),
            (roomA.ExitCount, roomB.ExitCount, ReferenceEquals(roomA, roomB), roomA.Location.Clone().GrowBy(1, 1, 1).IntersectsOrTouches(roomB.Location)));
    }

    private (int Cooling, int Warm, int Exits) Measure(BlockPos inside)
    {
        var room = World.Api.ModLoader.GetModSystem<RoomRegistry>().GetRoomForPosition(inside);
        return (room.CoolingWallCount, room.NonCoolingWallCount, room.ExitCount);
    }
}
