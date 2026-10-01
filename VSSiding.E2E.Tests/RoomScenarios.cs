using System.Threading.Tasks;
using Atlas.Api;
using Atlas.XUnit;
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
