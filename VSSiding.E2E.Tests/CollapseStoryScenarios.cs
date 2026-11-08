using System;
using System.Linq;
using System.Threading.Tasks;
using Atlas.Api;
using Atlas.XUnit;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Xunit;

namespace VSSiding.E2E.Tests;

// Runs with `just probe collapsestory`, the only run that loads CollapseStory 0.8.9 and its Expanded Matter dependency (#110).
// Each scenario records what CollapseStory does to Siding's blocks; the assertions pin what was observed.
// Stays on a fresh world: under rollback four scenarios fail in WallBuilder.Raise (the infill click on a framed wall is not answered true) once collapsestory is loaded.
[Trait("Category", "Probe")]
[AtlasWorld(Mods = new[] { "../../../../compat/mods/slot0.zip", "../../../../compat/mods/slot1.zip", "../../../../compat/mods/slot2.zip", "../../../../compat/mods/slot3.zip", "../../../../compat/mods/slot4.zip", "../../../../compat/mods/slot5.zip" })]
public class CollapseStoryScenarios : AtlasScenarioBase
{
    private const int SettleTicks = 200;

    private static bool CollapseStoryLoaded => Environment.GetEnvironmentVariable("SIDING_PROBE") == "collapsestory";

    private static readonly (string?, string?, string?, string?, string?) Finished = ("oak", "clay", null, null, "planks");

    private BlockPos Cell => World.Spawn.Offset(1, 2, 0);

    private string CodeAt(BlockPos pos) => World.BlockAt(pos).Code.ToShortString();

    private bool IsWall(BlockPos pos) => World.BlockAt(pos).Code is { Domain: "vssiding" } code && code.Path.StartsWith("wall");

    private async Task AsBuilder(System.Func<ITestPlayer, Task> act)
    {
        ITestPlayer player = await WallBuilder.JoinBuilder(World);
        try
        {
            await act(player);
        }
        finally
        {
            player.Player.Disconnect();
            await World.Until(() => !player.IsConnected);
        }
    }

    private async Task Raise(ITestPlayer player, BlockPos cell)
        => await WallBuilder.Raise(World, player, cell, cell.WestCopy(), "game:plank-oak", "game:clay-blue", "game:plank-oak");

    private async Task RaiseOnPlanks(ITestPlayer player, BlockPos cell)
    {
        World.SetBlock("game:planks-aged-ud", cell.DownCopy());
        await Raise(player, cell);
    }

    [AtlasScenario(FreshWorld = true)]
    public async Task Wall_Should_Stand_When_ItIsFramedFilledAndFinished()
    {
        if (!CollapseStoryLoaded) return;

        await AsBuilder(async player =>
        {
            await RaiseOnPlanks(player, Cell);
            await World.Ticks(SettleTicks);

            // Observed: the wall stays with all its layers.
            Assert.Equal((true, Finished), (IsWall(Cell), WallBuilder.Layers(World, Cell)));
        });
    }

    [AtlasScenario(FreshWorld = true)]
    public async Task CoveredBlock_Should_Fall_When_PlacedOnAWall()
    {
        if (!CollapseStoryLoaded) return;

        await AsBuilder(async player =>
        {
            await RaiseOnPlanks(player, Cell);
            World.SetBlock("game:planks-aged-ud", Cell.UpCopy());
            await World.Ticks(SettleTicks);

            // Observed: the wall stays and the planks on top of it are gone; CollapseStory does not count a wall as support.
            Assert.Equal((true, "air"), (IsWall(Cell), CodeAt(Cell.UpCopy())));
        });
    }

    [AtlasScenario(FreshWorld = true)]
    public async Task Wall_Should_Stay_When_RaisedOnAWall()
    {
        if (!CollapseStoryLoaded) return;

        await AsBuilder(async player =>
        {
            await RaiseOnPlanks(player, Cell);
            await Raise(player, Cell.UpCopy());
            await World.Ticks(SettleTicks);

            // Observed: both walls stay.
            Assert.Equal((true, true), (IsWall(Cell), IsWall(Cell.UpCopy())));
        });
    }

    [AtlasScenario(FreshWorld = true)]
    public async Task Floor_Should_Stay_When_ItSpansARoomBetweenTwoWalls()
    {
        if (!CollapseStoryLoaded) return;

        // Walls at x+0 and x+4 on planks; three floor cells between them, the planks under them broken once the floors stand.
        BlockPos west = World.Spawn.Offset(0, 2, 0);
        BlockPos[] floors = [west.EastCopy(1), west.EastCopy(2), west.EastCopy(3)];
        await AsBuilder(async player =>
        {
            foreach ((BlockPos wall, BlockFacing inside) in new[] { (west, BlockFacing.EAST), (west.EastCopy(4), BlockFacing.WEST) })
            {
                World.SetBlock("game:planks-aged-ud", wall.DownCopy());
                await WallBuilder.Raise(World, player, wall, wall.AddCopy(inside), "game:plank-oak", "game:clay-blue", "game:plank-oak");
            }
            player.Entity.WatchedAttributes.SetString("vssidingFraming", "floor");
            foreach (BlockPos floor in floors)
            {
                World.SetBlock("game:planks-aged-ud", floor.DownCopy());
                await WallBuilder.Raise(World, player, floor, floor.WestCopy(), "game:plank-oak", null);
            }
            foreach (BlockPos floor in floors) World.Api.World.BlockAccessor.BreakBlock(floor.DownCopy(), player.Player);
            await World.Ticks(SettleTicks);

            // Observed: all three floor cells stay.
            Assert.Equal(floors.Select(_ => "vssiding"), floors.Select(floor => World.BlockAt(floor).Code.Domain));
        });
    }

    [AtlasScenario(FreshWorld = true)]
    public async Task Cabinet_Should_BeRefused_When_HostedOverAWall()
    {
        if (!CollapseStoryLoaded) return;
        await AssertRefusedOverAWall(Cabinet());
    }

    [AtlasScenario(FreshWorld = true)]
    public async Task ScrollRack_Should_BeRefused_When_HostedOverAWall()
    {
        if (!CollapseStoryLoaded) return;
        await AssertRefusedOverAWall(ScrollRack());
    }

    [AtlasScenario(FreshWorld = true)]
    public async Task Cabinet_Should_FallAndLeaveItsWall_When_ThePlanksUnderItsWallBreak()
    {
        if (!CollapseStoryLoaded) return;
        await AssertFallsOutOfItsWall(Cabinet(), "cabinet");
    }

    [AtlasScenario(FreshWorld = true)]
    public async Task ScrollRack_Should_FallAndLeaveItsWall_When_ThePlanksUnderItsWallBreak()
    {
        if (!CollapseStoryLoaded) return;
        await AssertFallsOutOfItsWall(ScrollRack(), "scrollrack");
    }

    [AtlasScenario(FreshWorld = true)]
    public async Task Wall_Should_Stay_When_ThePlanksUnderItAreBroken()
    {
        if (!CollapseStoryLoaded) return;

        await AsBuilder(async player =>
        {
            await RaiseOnPlanks(player, Cell);
            World.Api.World.BlockAccessor.BreakBlock(Cell.DownCopy(), player.Player);
            await World.Ticks(SettleTicks);

            // Observed: the wall stays in the air; CollapseStory does not drop it.
            Assert.Equal((true, "air"), (IsWall(Cell), CodeAt(Cell.DownCopy())));
        });
    }

    private ItemStack Cabinet()
        => new(World.Api.World.Blocks.First(b => b.Code?.Domain == "game" && b.Code.Path.StartsWith("cabinet-") && b.Code.Path.Contains("bottom")));

    private ItemStack ScrollRack()
    {
        var rack = new ItemStack(World.Api.World.GetBlock(new AssetLocation("game:scrollrack")));
        rack.Attributes.SetString("type", "normal");
        rack.Attributes.SetString("material", "aged");
        return rack;
    }

    private static BlockSelection EastFaceOf(BlockPos cell) => new() { Position = cell.Copy(), Face = BlockFacing.EAST, HitPosition = new Vec3d(0.5, 0.5, 0.5) };

    private static void Hold(ITestPlayer player, ItemStack furniture)
    {
        // A saw in the off hand layers the wall instead of hosting (SidingWallBlock.OnBlockInteractStart).
        player.Entity.LeftHandItemSlot.Itemstack = null;
        player.Entity.RightHandItemSlot.Itemstack = furniture;
    }

    // CollapseStory gives vanilla cabinets and scroll racks the UnstableFalling behavior, which neither has without it.
    // With no attachableFaces listed, its placement check needs the block below to take an attachment on its top, and a wall does not.
    private async Task AssertRefusedOverAWall(ItemStack furniture) => await AsBuilder(async player =>
    {
        await RaiseOnPlanks(player, Cell);
        await Raise(player, Cell.UpCopy());
        Hold(player, furniture);
        string failureCode = "";
        bool placed = furniture.Block.TryPlaceBlock(player.Entity.World, player.Player, furniture, EastFaceOf(Cell.UpCopy()), ref failureCode);

        // Observed: refused over a wall, so no cabinet or scroll rack is hosted in an upper storey.
        Assert.Equal((false, "requiresolidground"), (placed, failureCode));
    });

    private async Task AssertFallsOutOfItsWall(ItemStack furniture, string pathStart) => await AsBuilder(async player =>
    {
        await RaiseOnPlanks(player, Cell);
        Hold(player, furniture);
        Assert.True(World.BlockAt(Cell).OnBlockInteractStart(player.Entity.World, player.Player, EastFaceOf(Cell)));
        Assert.StartsWith(pathStart, World.BlockAt(Cell).Code.Path);

        World.Api.World.BlockAccessor.BreakBlock(Cell.DownCopy(), player.Player);
        await World.Ticks(SettleTicks);

        // Observed: the furniture falls out of the cell and the wall is back with its layers, its guest record cleared.
        Assert.Equal(("vssiding:wall-wall-west", Finished, null), (CodeAt(Cell), WallBuilder.Layers(World, Cell), WallBuilder.GuestLayers(World, Cell)));
    });
}
