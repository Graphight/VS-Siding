using System.Linq;
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
        BlockPos inside = await BuildRoom("game:stick", BlockFacing.HORIZONTALS);
        Assert.Equal((0, 6, 0), Measure(inside));
    }

    [AtlasScenario(FreshWorld = true)]
    public async Task Room_Should_BeACellar_When_WallsAreFilledWithClay()
    {
        BlockPos inside = await BuildRoom("game:clay-blue", BlockFacing.HORIZONTALS);
        Assert.Equal((4, 2, 0), Measure(inside));
    }

    [AtlasScenario(FreshWorld = true)]
    public async Task Room_Should_Leak_When_AWallIsFramedButNotFilled()
    {
        BlockPos inside = await BuildRoom("game:clay-blue", new[] { BlockFacing.NORTH, BlockFacing.EAST, BlockFacing.SOUTH });
        Assert.NotEqual(0, Measure(inside).Exits);
    }

    private async Task<BlockPos> BuildRoom(string infill, BlockFacing[] filled)
    {
        BlockPos inside = World.Spawn.Offset(0, 2, 0);
        for (int dx = -1; dx <= 1; dx++)
            for (int dz = -1; dz <= 1; dz++)
                World.SetBlock("game:planks-aged-ud", inside.Offset(dx, -1, dz));

        ITestPlayer player = await World.JoinPlayer("Builder");
        WallBuilder.HoldSaw(player);
        foreach (BlockFacing side in BlockFacing.HORIZONTALS)
            await WallBuilder.Raise(World, player, inside.AddCopy(side), inside, "game:plank-oak", filled.Contains(side) ? infill : null);

        for (int dx = -1; dx <= 1; dx++)
            for (int dz = -1; dz <= 1; dz++)
                World.SetBlock("game:planks-aged-ud", inside.Offset(dx, 1, dz));
        return inside;
    }

    private (int Cooling, int Warm, int Exits) Measure(BlockPos inside)
    {
        var room = World.Api.ModLoader.GetModSystem<RoomRegistry>().GetRoomForPosition(inside);
        return (room.CoolingWallCount, room.NonCoolingWallCount, room.ExitCount);
    }
}
