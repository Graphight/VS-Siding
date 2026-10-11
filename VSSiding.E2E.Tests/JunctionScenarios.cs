using System.Linq;
using System.Threading.Tasks;
using Atlas.Api;
using Atlas.XUnit;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Xunit;

namespace VSSiding.E2E.Tests;

// A filled wall turns into a cornerout in place where a Corner click with planks cannot board the face (decision 0069).
[AtlasWorld(StrictBootDiagnostics = true)]
public class JunctionScenarios : AtlasScenarioBase
{
    [AtlasScenario(FreshWorld = true)]
    public async Task FilledWall_Should_BecomeACornerCarryingTheFrontFinishToItsSecondFace_When_ItsFinishedFaceIsClickedWithPlanksAndCornerPicked()
    {
        (BlockPos cell, ITestPlayer player) = await RaiseFinishedWall();

        await Click(cell, player, new Vec3d(0, 0.5, 0.8));

        Assert.Equal(("cornerout", "south,west", ("oak", "clay", null, "planks-birch", null)), Describe(cell));
    }

    [AtlasScenario(FreshWorld = true)]
    public async Task FilledWall_Should_BecomeACornerKeepingTheFrontFinish_When_TheOtherEndIsClicked()
    {
        (BlockPos cell, ITestPlayer player) = await RaiseFinishedWall();

        await Click(cell, player, new Vec3d(0, 0.5, 0.2));

        Assert.Equal(("cornerout", "north,west", ("oak", "clay", "planks-birch", null, null)), Describe(cell));
    }

    [AtlasScenario(FreshWorld = true)]
    public async Task FilledWall_Should_TakeThePlanksAsAFinishAndStayAWall_When_ItsFaceIsUnfinished()
    {
        BlockPos cell = World.Spawn.Offset(1, 2, 0);
        World.SetBlock("game:planks-aged-ud", cell.DownCopy());
        ITestPlayer player = await WallBuilder.JoinBuilder(World);
        await WallBuilder.Raise(World, player, cell, cell.WestCopy(), "game:plank-oak", "game:clay-blue");

        await Click(cell, player, new Vec3d(1, 0.5, 0.8), BlockFacing.EAST);

        Assert.Equal(("wall", "west", ("oak", "clay", null, null, "planks")), Describe(cell));
    }

    private async Task<(BlockPos Cell, ITestPlayer Player)> RaiseFinishedWall()
    {
        BlockPos cell = World.Spawn.Offset(1, 2, 0);
        World.SetBlock("game:planks-aged-ud", cell.DownCopy());
        ITestPlayer player = await WallBuilder.JoinBuilder(World);
        await WallBuilder.Raise(World, player, cell, cell.WestCopy(), "game:plank-oak", "game:clay-blue");
        await player.GiveItem("game:plank-birch", 2);
        var frontSel = new BlockSelection { Position = cell.Copy(), Face = BlockFacing.WEST, HitPosition = new Vec3d(0, 0.5, 0.5) };
        Assert.True(World.BlockAt(cell).OnBlockInteractStart(player.Entity.World, player.Player, frontSel));
        return (cell, player);
    }

    private async Task Click(BlockPos cell, ITestPlayer player, Vec3d hit, BlockFacing? face = null)
    {
        player.Entity.WatchedAttributes.SetString("vssidingFraming", "corner");
        await player.GiveItem("game:plank-oak", 2);
        var sel = new BlockSelection { Position = cell.Copy(), Face = face ?? BlockFacing.WEST, HitPosition = hit };
        World.BlockAt(cell).OnBlockInteractStart(player.Entity.World, player.Player, sel);
    }

    private (string Layout, string Retaining, (string? Framing, string? Infill, string? Front, string? SecondFront, string? Back) Layers) Describe(BlockPos cell)
    {
        Block wall = World.BlockAt(cell);
        string retaining = string.Join(",", BlockFacing.HORIZONTALS
            .Where(face => wall.GetRetention(cell, face, EnumRetentionType.Heat) != 0)
            .Select(face => face.Code)
            .OrderBy(face => face));
        return (wall.Variant["layout"], retaining, WallBuilder.Layers(World, cell));
    }
}
