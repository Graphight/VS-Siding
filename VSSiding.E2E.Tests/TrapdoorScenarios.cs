using System.Linq;
using System.Threading.Tasks;
using Atlas.Api;
using Atlas.XUnit;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;
using Xunit;

namespace VSSiding.E2E.Tests;

// A trapdoor takes a straight wall's cell as a guest, flush at the top and 12/16 wide, where vanilla refuses it (#138).
// Every wall here has its panel on the west face.
[AtlasWorld(StrictBootDiagnostics = true)]
public class TrapdoorScenarios : AtlasScenarioBase
{
    private const string Trapdoor = "game:trapdoor-solid-oak-1";

    private BlockPos Cell => World.Spawn.Offset(1, 2, 0);

    private async Task<ITestPlayer> BuilderWithWall(BlockPos cell)
    {
        World.SetBlock("game:planks-aged-ud", cell.DownCopy());
        ITestPlayer player = await WallBuilder.JoinBuilder(World);
        await WallBuilder.Raise(World, player, cell, cell.WestCopy(), "game:plank-oak", "game:clay-blue");
        return player;
    }

    private static double Round(float value) => System.Math.Round(value, 4);

    private (bool Placed, string Failure) Place(ITestPlayer player, BlockPos cell, BlockFacing face)
    {
        player.Entity.LeftHandItemSlot.Itemstack = null;
        var stack = new ItemStack(World.Api.World.GetBlock(new AssetLocation(Trapdoor)));
        var selection = new BlockSelection { Position = cell.Copy(), Face = face, HitPosition = new Vec3d(0.5, 0.5, 0.5) };
        string failureCode = "";
        bool placed = stack.Block.TryPlaceBlock(player.Entity.World, player.Player, stack, selection, ref failureCode);
        return (placed, failureCode);
    }

    [AtlasScenario(RollbackWorld = true, StrictIsolation = true)]
    public async Task Wall_Should_HostATrapdoorFlushAtTheTopAndTwelveSixteenthsWide_When_OneIsPlacedOnItsSide()
    {
        BlockPos cell = Cell;
        ITestPlayer player = await BuilderWithWall(cell);

        var (placed, _) = Place(player, cell, BlockFacing.EAST);
        Cuboidf box = World.BlockAt(cell).GetCollisionBoxes(World.Api.World.BlockAccessor, cell)[0];

        Assert.Equal((true, "trapdoor-solid-oak-1", ("oak", "clay", null, null, null), (0.25, 0.8125, 0.0, 1.0, 1.0, 1.0)),
            (placed, World.BlockAt(cell).Code.Path, WallBuilder.GuestLayers(World, cell), (Round(box.X1), Round(box.Y1), Round(box.Z1), Round(box.X2), Round(box.Y2), Round(box.Z2))));
    }

    // A click on a hosted ladder offsets into the cell past its face, often a thin floor the server's replaceable test would refuse.
    [AtlasScenario(RollbackWorld = true, StrictIsolation = true)]
    public Task TrapdoorsAndLadders_Should_SkipTheServersReplaceableTest()
    {
        bool Skips(string code) => World.Api.World.GetBlock(new AssetLocation(code)).Attributes?["ignoreServerReplaceableTest"].AsBool() == true;

        Assert.Equal((true, true, true), (Skips(Trapdoor), Skips("game:ladder-wood-aged-west"), Skips("game:ladder-rope-north")));
        return Task.CompletedTask;
    }

    // The room fill reads each face's retention; vanilla makes only a solid trapdoor airtight, a grated (window) one never.
    [AtlasScenario(RollbackWorld = true, StrictIsolation = true)]
    public async Task Hatch_Should_SealUpAndDown_When_ItIsASolidTrapdoor()
    {
        BlockPos cell = Cell;
        ITestPlayer player = await BuilderWithHatch(cell);
        int Retention(BlockFacing face) => World.BlockAt(cell).GetRetention(cell, face, EnumRetentionType.Heat);

        Assert.Equal(("trapdoor-solid-oak-1", true, true), (World.BlockAt(cell).Code.Path, Retention(BlockFacing.UP) != 0, Retention(BlockFacing.DOWN) != 0));
    }

    [AtlasScenario(RollbackWorld = true, StrictIsolation = true)]
    public async Task Wall_Should_RefuseATrapdoor_When_ItIsDecked()
    {
        BlockPos cell = Cell;
        ITestPlayer player = await BuilderWithWall(cell);
        player.Entity.WatchedAttributes.SetString("vssidingDeck", "deck");
        await player.GiveItem("game:plank-oak", 2);
        var sideSel = new BlockSelection { Position = cell.Copy(), Face = BlockFacing.EAST, HitPosition = new Vec3d(1, 0.5, 0.5) };
        Assert.True(World.BlockAt(cell).OnBlockInteractStart(player.Entity.World, player.Player, sideSel));

        var (placed, failure) = Place(player, cell, BlockFacing.EAST);

        Assert.Equal((false, "vssiding-decked", "wall"), (placed, failure, World.BlockAt(cell).FirstCodePart()));
    }

    [AtlasScenario(RollbackWorld = true, StrictIsolation = true)]
    public async Task Corner_Should_RefuseATrapdoor()
    {
        BlockPos cell = Cell;
        ITestPlayer player = await BuilderWithWall(cell);
        player.Entity.WatchedAttributes.SetString("vssidingFraming", "corner");
        await WallBuilder.Press(World, player, cell, "game:stick", BlockFacing.WEST, new Vec3d(0, 0.5, 0.2));
        player.Entity.LeftHandItemSlot.Itemstack = null;

        var (placed, failure) = Place(player, cell, BlockFacing.EAST);

        Assert.Equal((false, "vssiding-hatch-straight", "cornerout"), (placed, failure, World.BlockAt(cell).Variant["layout"]));
    }

    [AtlasScenario(RollbackWorld = true, StrictIsolation = true)]
    public async Task Hatch_Should_KeepTheLadderAsALayerAndBeClimbable_When_ItIsPlacedOnAHostedLadder()
    {
        BlockPos cell = Cell;
        ITestPlayer player = await BuilderWithWall(cell);
        var ladder = World.Api.World.GetBlock(new AssetLocation("game:ladder-wood-aged-west"));
        var ladderSel = new BlockSelection { Position = cell.Copy(), Face = BlockFacing.EAST, HitPosition = new Vec3d(1, 0.5, 0.5) };
        string failureCode = "";
        Assert.True(ladder.TryPlaceBlock(player.Entity.World, player.Player, new ItemStack(ladder), ladderSel, ref failureCode), failureCode);

        var stack = new ItemStack(World.Api.World.GetBlock(new AssetLocation(Trapdoor)));
        var onLadder = new BlockSelection { Position = cell.EastCopy(), Face = BlockFacing.EAST, DidOffset = true, HitPosition = new Vec3d(0.5, 0.5, 0.5) };
        bool placed = stack.Block.TryPlaceBlock(player.Entity.World, player.Player, stack, onLadder, ref failureCode);

        Assert.Equal((true, "trapdoor-solid-oak-1", "game:ladder-wood-aged-west", ("oak", "clay", null, null, null), true),
            (placed, World.BlockAt(cell).Code.Path, WallBuilder.GuestLadder(World, cell), WallBuilder.GuestLayers(World, cell), World.BlockAt(cell).IsClimbable(cell)));
    }

    [AtlasScenario(RollbackWorld = true, StrictIsolation = true)]
    public async Task HostedLadder_Should_CollideOnlyAsThePanel_When_ItIsInAWallsCell()
    {
        BlockPos cell = Cell;
        ITestPlayer player = await BuilderWithWall(cell);
        PlaceLadder(player, cell, BlockFacing.EAST, false);

        Cuboidf[] boxes = World.BlockAt(cell).GetCollisionBoxes(World.Api.World.BlockAccessor, cell);

        Assert.Equal(("ladder-wood-aged-west", true, 0.25, true), (World.BlockAt(cell).Code.Path, boxes.Length > 0,
            boxes.Max(box => Round(box.X2)), World.BlockAt(cell).GetSelectionBoxes(World.Api.World.BlockAccessor, cell).Any(box => box.X2 > 0.3f)));
    }

    private async Task<ITestPlayer> BuilderWithHatch(BlockPos cell)
    {
        ITestPlayer player = await BuilderWithWall(cell);
        Place(player, cell, BlockFacing.EAST);
        return player;
    }

    private bool PlaceLadder(ITestPlayer player, BlockPos position, BlockFacing face, bool didOffset)
    {
        var ladder = World.Api.World.GetBlock(new AssetLocation("game:ladder-wood-aged-west"));
        var sel = new BlockSelection { Position = position, Face = face, DidOffset = didOffset, HitPosition = new Vec3d(0.5, 0.5, 0.5) };
        string failureCode = "";
        return ladder.TryPlaceBlock(player.Entity.World, player.Player, new ItemStack(ladder), sel, ref failureCode);
    }

    [AtlasScenario(RollbackWorld = true, StrictIsolation = true)]
    public async Task Hatch_Should_TakeTheLadderAsALayer_When_ALadderIsClickedOntoIt()
    {
        BlockPos cell = Cell;
        ITestPlayer player = await BuilderWithHatch(cell);

        bool placed = PlaceLadder(player, cell.EastCopy(), BlockFacing.EAST, true);

        Assert.Equal((true, "trapdoor-solid-oak-1", "game:ladder-wood-aged-west", true),
            (placed, World.BlockAt(cell).Code.Path, WallBuilder.GuestLadder(World, cell), World.BlockAt(cell).IsClimbable(cell)));
    }

    [AtlasScenario(RollbackWorld = true, StrictIsolation = true)]
    public async Task Hatch_Should_TakeTheLadderAsALayer_When_ALadderStackGrowsUpIntoIt()
    {
        BlockPos cell = Cell;
        ITestPlayer player = await BuilderWithHatch(cell);
        for (int below = 1; below <= 3; below++) World.SetBlock("game:ladder-wood-aged-west", cell.DownCopy(below));

        bool placed = PlaceLadder(player, cell.DownCopy(3).EastCopy(), BlockFacing.EAST, true);

        Assert.Equal((true, "trapdoor-solid-oak-1", "game:ladder-wood-aged-west"),
            (placed, World.BlockAt(cell).Code.Path, WallBuilder.GuestLadder(World, cell)));
    }

    private Cuboidf[] Opened(ITestPlayer player, BlockPos cell)
    {
        World.Api.World.BlockAccessor.GetBlockEntity(cell).GetBehavior<BEBehaviorTrapDoor>().ToggleDoorState(player.Player, true);
        return World.BlockAt(cell).GetCollisionBoxes(World.Api.World.BlockAccessor, cell);
    }

    // Hinged on the panel's edge, an open hatch hung where the ladder is and left a climber 0.15/16 to drop through.
    [AtlasScenario(RollbackWorld = true, StrictIsolation = true)]
    public async Task OpenHatch_Should_CollideOnlyAsThePanel()
    {
        BlockPos cell = Cell;
        ITestPlayer player = await BuilderWithHatch(cell);

        Cuboidf[] boxes = Opened(player, cell);

        Assert.Equal((true, 0.25), (boxes.Length > 0, boxes.Max(box => Round(box.X2))));
    }

    // A ladder on a wall's flat outer face climbs the cell beside the wall, where the floor's edge is the only face to click.
    // Vanilla lays a trapdoor clicked high on a block's side flat at the top of the cell beside it; near the ends it stands upright.
    // Closed, the hatch is not climbable, or a player standing on a trapdoor capping a ladder shaft would be held climbing.
    [AtlasScenario(RollbackWorld = true, StrictIsolation = true)]
    public async Task Trapdoor_Should_LieFlushHingedOnTheEdgeAndBeClimbableOnlyOpen_When_ClickedHighOnAFloorsEdgeOverALadder()
    {
        BlockPos floor = Cell, hole = Cell.EastCopy();
        World.SetBlock("game:planks-aged-ud", floor.DownCopy());
        World.SetBlock("game:ladder-wood-aged-west", hole.DownCopy());
        ITestPlayer player = await WallBuilder.JoinBuilder(World);
        player.Entity.WatchedAttributes.SetString("vssidingFraming", "floor");
        await WallBuilder.Raise(World, player, floor, floor.WestCopy(), "game:plank-oak", null);
        player.Entity.LeftHandItemSlot.Itemstack = null;
        var stack = new ItemStack(World.Api.World.GetBlock(new AssetLocation(Trapdoor)));
        var onEdge = new BlockSelection { Position = hole.Copy(), Face = BlockFacing.EAST, DidOffset = true, HitPosition = new Vec3d(1, 0.9, 0.5) };
        string failureCode = "";

        bool placed = stack.Block.TryPlaceBlock(player.Entity.World, player.Player, stack, onEdge, ref failureCode);
        Cuboidf closed = World.BlockAt(hole).GetCollisionBoxes(World.Api.World.BlockAccessor, hole)[0];
        bool closedClimbable = World.BlockAt(hole).IsClimbable(hole);
        Cuboidf open = Opened(player, hole)[0];

        Assert.Equal((true, "trapdoor-solid-oak-1", 0.8125, 1.0, false, 0.0, 0.1875, true),
            (placed, World.BlockAt(hole).Code.Path, Round(closed.Y1), Round(closed.Y2), closedClimbable, Round(open.X1), Round(open.X2), World.BlockAt(hole).IsClimbable(hole)));
    }

    [AtlasScenario(RollbackWorld = true, StrictIsolation = true)]
    public async Task Hatch_Should_LeaveTheLadderHostedInTheWall_When_ItIsBroken()
    {
        BlockPos cell = Cell;
        ITestPlayer player = await BuilderWithHatch(cell);
        PlaceLadder(player, cell.EastCopy(), BlockFacing.EAST, true);

        World.Api.World.BlockAccessor.BreakBlock(cell, player.Player);
        await World.Until(() => WallBuilder.GuestLadder(World, cell) == null && World.BlockAt(cell).Code.Path == "ladder-wood-aged-west", 100);

        Assert.Equal(("ladder-wood-aged-west", null, ("oak", "clay", null, null, null)),
            (World.BlockAt(cell).Code.Path, WallBuilder.GuestLadder(World, cell), WallBuilder.GuestLayers(World, cell)));
    }
}
