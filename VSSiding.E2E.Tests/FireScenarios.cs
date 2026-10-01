using System.Threading.Tasks;
using Atlas.Api;
using Atlas.XUnit;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;
using Xunit;

namespace VSSiding.E2E.Tests;

[AtlasWorld(StrictBootDiagnostics = true)]
public class FireScenarios : AtlasScenarioBase
{
    [AtlasScenario(FreshWorld = true)]
    public async Task Wall_Should_StandWithItsClay_When_FireBurnsOffItsPlankFinish()
    {
        BlockPos cell = World.Spawn.Offset(1, 2, 0);
        BlockPos inside = cell.WestCopy();
        World.SetBlock("game:planks-aged-ud", cell.DownCopy());

        ITestPlayer player = await WallBuilder.JoinBuilder(World);
        await WallBuilder.Raise(World, player, cell, inside, "game:plank-oak", "game:clay-blue", "game:plank-oak");

        Assert.Equal(("oak", "clay", null, null, "planks"), WallBuilder.Layers(World, cell));
        Block wall = World.BlockAt(cell);

        BlockPos firePos = cell.UpCopy();
        World.SetBlock("game:fire", firePos);
        var burning = World.BlockEntityAt<BlockEntity>(firePos)!.GetBehavior<BEBehaviorBurning>();
        burning.OnFirePlaced(firePos, cell, null);
        Assert.True(burning.IsBurning);

        await World.Until(() => !burning.IsBurning, 3000);

        Assert.Equal(wall, World.BlockAt(cell));
        Assert.Equal(("oak", "clay", null, null, null), WallBuilder.Layers(World, cell));
    }
}
