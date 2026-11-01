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
[Trait("Category", "Probe")]
[AtlasWorld(Mods = new[] { "../../../../compat/mods/slot0.zip", "../../../../compat/mods/slot1.zip", "../../../../compat/mods/slot2.zip", "../../../../compat/mods/slot3.zip", "../../../../compat/mods/slot4.zip", "../../../../compat/mods/slot5.zip" })]
public class CollapseStoryScenarios : AtlasScenarioBase
{
    private const int SettleTicks = 200;

    private static bool CollapseStoryLoaded => Environment.GetEnvironmentVariable("SIDING_PROBE") == "collapsestory";

    private static readonly (string?, string?, string?, string?, string?) Finished = ("oak", "clay", null, null, "planks");

    private BlockPos Cell => World.Spawn.Offset(1, 2, 0);

    private string CodeAt(BlockPos pos) => World.BlockAt(pos).Code.ToString();

    private async Task Raise(ITestPlayer player, BlockPos cell)
        => await WallBuilder.Raise(World, player, cell, cell.WestCopy(), "game:plank-oak", "game:clay-blue", "game:plank-oak");

    private static bool IsWall(Block block) => block.Code is { Domain: "vssiding" } code && code.Path.StartsWith("wall");

    private async Task Finish(ITestPlayer player)
    {
        player.Player.Disconnect();
        await World.Until(() => !player.IsConnected);
    }

    [AtlasScenario(FreshWorld = true)]
    public async Task Wall_Should_Stand_When_ItIsFramedFilledAndFinished()
    {
        if (!CollapseStoryLoaded) return;

        BlockPos cell = Cell;
        World.SetBlock("game:planks-aged-ud", cell.DownCopy());
        ITestPlayer player = await WallBuilder.JoinBuilder(World);
        try
        {
            await Raise(player, cell);
            await World.Ticks(SettleTicks);

            // Observed: the wall stays with all its layers.
            Assert.Equal((true, Finished), (IsWall(World.BlockAt(cell)), WallBuilder.Layers(World, cell)));
        }
        finally { await Finish(player); }
    }

    [AtlasScenario(FreshWorld = true)]
    public async Task CoveredBlock_Should_Fall_When_PlacedOnAWall()
    {
        if (!CollapseStoryLoaded) return;

        BlockPos cell = Cell;
        World.SetBlock("game:planks-aged-ud", cell.DownCopy());
        ITestPlayer player = await WallBuilder.JoinBuilder(World);
        try
        {
            await Raise(player, cell);
            World.SetBlock("game:planks-aged-ud", cell.UpCopy());
            await World.Ticks(SettleTicks);

            // Observed: the wall stays and the planks on top of it are gone; CollapseStory does not count a wall as support.
            Assert.Equal((true, "game:air"), (IsWall(World.BlockAt(cell)), CodeAt(cell.UpCopy())));
        }
        finally { await Finish(player); }
    }

    [AtlasScenario(FreshWorld = true)]
    public async Task Wall_Should_Stay_When_RaisedOnAWall()
    {
        if (!CollapseStoryLoaded) return;

        BlockPos cell = Cell;
        World.SetBlock("game:planks-aged-ud", cell.DownCopy());
        ITestPlayer player = await WallBuilder.JoinBuilder(World);
        try
        {
            await Raise(player, cell);
            await Raise(player, cell.UpCopy());
            await World.Ticks(SettleTicks);

            // Observed: both walls stay.
            Assert.Equal((true, true), (IsWall(World.BlockAt(cell)), IsWall(World.BlockAt(cell.UpCopy()))));
        }
        finally { await Finish(player); }
    }

    [AtlasScenario(FreshWorld = true)]
    public async Task Floor_Should_Stay_When_ItSpansARoomBetweenTwoWalls()
    {
        if (!CollapseStoryLoaded) return;

        // Walls at x+0 and x+4 on planks; three floor cells between them, their planks removed once the floors stand.
        BlockPos west = World.Spawn.Offset(0, 2, 0);
        BlockPos[] walls = [west, west.EastCopy(4)];
        BlockPos[] floors = [west.EastCopy(1), west.EastCopy(2), west.EastCopy(3)];
        ITestPlayer player = await WallBuilder.JoinBuilder(World);
        try
        {
            foreach (BlockPos wall in walls)
            {
                World.SetBlock("game:planks-aged-ud", wall.DownCopy());
                await WallBuilder.Raise(World, player, wall, wall.AddCopy(wall.X == west.X ? BlockFacing.EAST : BlockFacing.WEST), "game:plank-oak", "game:clay-blue", "game:plank-oak");
            }
            player.Entity.WatchedAttributes.SetString("vssidingFraming", "floor");
            foreach (BlockPos floor in floors)
            {
                World.SetBlock("game:planks-aged-ud", floor.DownCopy());
                await WallBuilder.Raise(World, player, floor, floor.WestCopy(), "game:plank-oak", null);
            }
            foreach (BlockPos floor in floors) World.Api.World.BlockAccessor.SetBlock(0, floor.DownCopy());
            await World.Ticks(SettleTicks);

            // Observed: all three floor cells stay.
            Assert.Equal(floors.Select(floor => true), floors.Select(floor => World.BlockAt(floor).Code is { Domain: "vssiding" }));
        }
        finally { await Finish(player); }
    }

    [AtlasScenario(FreshWorld = true)]
    public async Task Cabinet_Should_BeRefusedOverAWall_And_FallWhenItsGroundGoes()
    {
        if (!CollapseStoryLoaded) return;

        Block cabinet = World.Api.World.Blocks.First(b => b.Code?.Domain == "game" && b.Code.Path.StartsWith("cabinet-") && b.Code.Path.Contains("bottom"));
        await AssertHostedFalls(new ItemStack(cabinet), "cabinet");
    }

    [AtlasScenario(FreshWorld = true)]
    public async Task ScrollRack_Should_BeRefusedOverAWall_And_FallWhenItsGroundGoes()
    {
        if (!CollapseStoryLoaded) return;

        var rack = new ItemStack(World.Api.World.GetBlock(new AssetLocation("game:scrollrack")));
        rack.Attributes.SetString("type", "normal");
        rack.Attributes.SetString("material", "aged");
        await AssertHostedFalls(rack, "scrollrack");
    }

    // CollapseStory gives vanilla cabinets and scroll racks the UnstableFalling behavior, which neither has without it.
    // Its placement check needs the block below to take an attachment on its top, and a wall does not.
    private async Task AssertHostedFalls(ItemStack furniture, string pathStart)
    {
        BlockPos lower = Cell;
        BlockPos upper = lower.UpCopy();
        World.SetBlock("game:planks-aged-ud", lower.DownCopy());
        ITestPlayer player = await WallBuilder.JoinBuilder(World);
        try
        {
            await Raise(player, lower);
            await Raise(player, upper);

            // A saw in the off hand layers the wall instead of hosting (SidingWallBlock.OnBlockInteractStart).
            player.Entity.LeftHandItemSlot.Itemstack = null;
            player.Entity.RightHandItemSlot.Itemstack = furniture;
            var onUpper = new BlockSelection { Position = upper.Copy(), Face = BlockFacing.EAST, HitPosition = new Vec3d(0.5, 0.5, 0.5) };
            string failureCode = "";
            bool placed = furniture.Block.TryPlaceBlock(player.Entity.World, player.Player, furniture, onUpper, ref failureCode);
            // Observed: refused over a wall, so no cabinet or scroll rack is hosted in an upper storey.
            Assert.Equal((false, "requiresolidground"), (placed, failureCode));

            World.Api.World.BlockAccessor.SetBlock(0, upper);
            var onLower = new BlockSelection { Position = lower.Copy(), Face = BlockFacing.EAST, HitPosition = new Vec3d(0.5, 0.5, 0.5) };
            Assert.True(World.BlockAt(lower).OnBlockInteractStart(player.Entity.World, player.Player, onLower));
            Assert.StartsWith(pathStart, World.BlockAt(lower).Code.Path);

            World.Api.World.BlockAccessor.BreakBlock(lower.DownCopy(), player.Player);
            await World.Ticks(SettleTicks);

            // Observed: the furniture falls out of the cell and the wall is back, its guest record cleared.
            Assert.Equal(("vssiding:wall-wall-west", null), (World.BlockAt(lower).Code.ToString(), WallBuilder.GuestLayers(World, lower)));
        }
        finally { await Finish(player); }
    }

    [AtlasScenario(FreshWorld = true)]
    public async Task Wall_Should_Stay_When_ThePlanksUnderItAreBroken()
    {
        if (!CollapseStoryLoaded) return;

        BlockPos cell = Cell;
        World.SetBlock("game:planks-aged-ud", cell.DownCopy());
        ITestPlayer player = await WallBuilder.JoinBuilder(World);
        try
        {
            await Raise(player, cell);
            World.Api.World.BlockAccessor.BreakBlock(cell.DownCopy(), player.Player);
            await World.Ticks(SettleTicks);

            // Observed: the wall stays in the air; CollapseStory does not drop it.
            Assert.Equal((true, "air"), (IsWall(World.BlockAt(cell)), World.BlockAt(cell.DownCopy()).Code.ToShortString()));
        }
        finally { await Finish(player); }
    }
}
