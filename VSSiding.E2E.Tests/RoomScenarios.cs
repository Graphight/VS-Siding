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
        Assert.Equal(
            ((0, 6, 0), ("sticks", "wattle", null, null, null), EnumItemStorageFlags.General | EnumItemStorageFlags.Metallurgy | EnumItemStorageFlags.Offhand),
            (Measure(inside), WallBuilder.Layers(World, inside.NorthCopy()), stone.StorageFlags));
    }

    [AtlasScenario(FreshWorld = true)]
    public async Task Room_Should_BeACellar_When_WallsAreFilledWithClay()
    {
        (BlockPos inside, _) = await WallBuilder.BuildRoom(World, "game:clay-blue", BlockFacing.HORIZONTALS);
        Assert.Equal((4, 2, 0), Measure(inside));
    }

    [AtlasScenario(FreshWorld = true)]
    public async Task Room_Should_Leak_When_AWallIsFramedButNotFilled()
    {
        (BlockPos inside, _) = await WallBuilder.BuildRoom(World, "game:clay-blue", new[] { BlockFacing.NORTH, BlockFacing.EAST, BlockFacing.SOUTH });
        Assert.NotEqual(0, Measure(inside).Exits);
    }

    private (int Cooling, int Warm, int Exits) Measure(BlockPos inside)
    {
        var room = World.Api.ModLoader.GetModSystem<RoomRegistry>().GetRoomForPosition(inside);
        return (room.CoolingWallCount, room.NonCoolingWallCount, room.ExitCount);
    }
}
