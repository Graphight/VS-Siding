using System.Threading.Tasks;
using Atlas.Api;
using Atlas.XUnit;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
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
}
