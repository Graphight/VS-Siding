using System;
using System.Threading.Tasks;
using Atlas.Api;
using Atlas.XUnit;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Xunit;

namespace VSSiding.E2E.Tests;

[AtlasWorld(StrictBootDiagnostics = true)]
public class AnimalScenarios : AtlasScenarioBase
{
    [AtlasScenario(FreshWorld = true)]
    public async Task Sheep_Should_StayOnTheGround_When_PushedIntoAWall()
    {
        BlockPos cell = World.Spawn.Offset(1, 2, 0);
        BlockPos inside = cell.WestCopy();
        for (int dx = -5; dx <= 4; dx++)
            for (int dz = -4; dz <= 4; dz++)
                World.SetBlock("game:planks-aged-ud", cell.Offset(dx, -1, dz));

        ITestPlayer player = await World.JoinPlayer("Builder");
        WallBuilder.HoldSaw(player);
        for (int dz = -2; dz <= 2; dz++)
            await WallBuilder.Raise(World, player, cell.SouthCopy(dz), inside.SouthCopy(dz), "game:plank-oak", "game:clay-blue");
        Assert.Equal(("oak", "clay", null, null, null), WallBuilder.Layers(World, cell));

        Entity sheep = World.SpawnEntity("game:sheep-bighorn-adult-female", cell.WestCopy(3));
        await World.Ticks(30);

        double maxY = sheep.Pos.Y;
        double maxX = sheep.Pos.X;
        for (int tick = 0; tick < 300; tick++)
        {
            sheep.Pos.Motion.X = 0.1;
            sheep.Pos.Motion.Z = 0;
            await World.Ticks(1);
            maxY = Math.Max(maxY, sheep.Pos.Y);
            maxX = Math.Max(maxX, sheep.Pos.X);
        }

        Assert.Equal((cell.Y, true), (Math.Floor(maxY), maxX > cell.X - 0.7));
    }
}
