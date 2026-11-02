using System.Threading.Tasks;
using Atlas.Api;
using Atlas.XUnit;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Xunit;

namespace VSSiding.E2E.Tests;

[AtlasWorld(StrictBootDiagnostics = true)]
public class FloorScenarios : AtlasScenarioBase
{
    // A glass floor takes a finish on its top like any other, drawn as a rim round the glass.
    [AtlasScenario(FreshWorld = true)]
    public async Task Floor_Should_TakeFloorboardsOnItsTopKeepingItsGlass_When_ItIsGlazed()
    {
        BlockPos cell = World.Spawn.Offset(1, 2, 0);
        World.SetBlock("game:planks-aged-ud", cell.DownCopy());
        ITestPlayer player = await WallBuilder.JoinBuilder(World);
        player.Entity.WatchedAttributes.SetString("vssidingFraming", "floor");
        await WallBuilder.Raise(World, player, cell, cell.WestCopy(), "game:plank-oak", null);
        ItemSlot slot = player.Player.InventoryManager.ActiveHotbarSlot;
        var topSel = new BlockSelection { Position = cell.Copy(), Face = BlockFacing.UP, HitPosition = new Vec3d(0.5, 1, 0.5) };

        foreach (var (item, quantity) in new[] { ("game:glass-plain", 1), ("game:plank-oak", 2) })
        {
            await player.GiveItem(item, quantity);
            Assert.True(World.BlockAt(cell).OnBlockInteractStart(player.Entity.World, player.Player, topSel));
            Assert.Null(slot.Itemstack);
        }

        Assert.Equal(("oak", "glass-plain", "planks", null, null), WallBuilder.Layers(World, cell));
    }

    // A log finish has no floor shapes of its own, so a picked style reaches a floor only as the stored
    // style its texture is read by.
    [AtlasScenario(FreshWorld = true)]
    public async Task Floor_Should_KeepThePickedLogStyle_When_ALogFinishesEachFace()
    {
        BlockPos cell = World.Spawn.Offset(1, 2, 0);
        World.SetBlock("game:planks-aged-ud", cell.DownCopy());
        ITestPlayer player = await WallBuilder.JoinBuilder(World);
        player.Entity.WatchedAttributes.SetString("vssidingFraming", "floor");
        await WallBuilder.Raise(World, player, cell, cell.WestCopy(), "game:plank-oak", "game:clay-blue");
        ItemSlot slot = player.Player.InventoryManager.ActiveHotbarSlot;

        foreach (var (style, face) in new[] { ("bark", BlockFacing.UP), ("hbark", BlockFacing.DOWN) })
        {
            player.Entity.WatchedAttributes.SetString("vssidingLogs", style);
            await player.GiveItem("game:log-placed-oak-ud", 1);
            var faceSel = new BlockSelection { Position = cell.Copy(), Face = face, HitPosition = new Vec3d(0.5, 0.5, 0.5) };
            Assert.True(World.BlockAt(cell).OnBlockInteractStart(player.Entity.World, player.Player, faceSel));
        }

        var tree = new TreeAttribute();
        World.BlockEntityAt<BlockEntity>(cell)!.ToTreeAttributes(tree);
        Assert.Equal(
            (null, "shakes-oak", "bark", "shakes-oak", "hbark"),
            (slot.Itemstack, tree.GetString("front"), tree.GetString("frontstyle"), tree.GetString("back"), tree.GetString("backstyle")));
    }

    private static readonly Vec3d Middle = new(0.5, 0.5, 0.5);
    private static readonly Vec3d TopHit = new(0.5, 1, 0.5);

    // A filled wall one cell ahead of where the player looks from `beside`, with floor picked.
    private async Task<(BlockPos Beside, BlockPos Wall, BlockFacing Ahead, ITestPlayer Player)> RaiseWallAhead()
    {
        BlockPos beside = World.Spawn.Offset(1, 2, 0);
        ITestPlayer player = await WallBuilder.JoinBuilder(World);
        // The joined player keeps the yaw an earlier scenario left, and Raise's teleport resets it to 0, so the run would
        // walk a different way from the wall placed here; 0 is the yaw every later step then sees.
        player.Entity.Pos.Yaw = 0;
        player.Entity.Pos.Pitch = 0;
        Vec3f view = player.Entity.SidedPos.GetViewVector();
        BlockFacing ahead = System.Math.Abs(view.X) >= System.Math.Abs(view.Z)
            ? (view.X > 0 ? BlockFacing.EAST : BlockFacing.WEST)
            : (view.Z > 0 ? BlockFacing.SOUTH : BlockFacing.NORTH);
        BlockPos wall = beside.AddCopy(ahead);
        World.SetBlock("game:planks-aged-ud", beside.DownCopy());
        World.SetBlock("game:planks-aged-ud", wall.DownCopy());
        player.Entity.WatchedAttributes.SetString("vssidingFraming", "wall");
        await WallBuilder.Raise(World, player, wall, beside, "game:plank-oak", "game:clay-blue");
        player.Entity.WatchedAttributes.SetString("vssidingFraming", "floor");
        return (beside, wall, ahead, player);
    }

    private string? DeckOf(BlockPos cell)
    {
        var tree = new TreeAttribute();
        World.BlockEntityAt<BlockEntity>(cell)!.ToTreeAttributes(tree);
        return tree.GetString("deck") is { Length: > 0 } deck ? deck : null;
    }

    private static int Held(ITestPlayer player) => player.Player.InventoryManager.ActiveHotbarSlot.Itemstack?.StackSize ?? 0;

    [AtlasScenario(FreshWorld = true)]
    public async Task Wall_Should_TakeADeck_When_PlanksArePressedOnTheSideOfABlockBesideIt()
    {
        (BlockPos beside, BlockPos wall, BlockFacing ahead, ITestPlayer player) = await RaiseWallAhead();
        World.SetBlock("game:planks-aged-ud", beside);

        await WallBuilder.Press(World, player, beside, "game:plank-oak", ahead, Middle, quantity: 3);

        Assert.Equal(("oak", 1), (DeckOf(wall), Held(player)));
    }

    [AtlasScenario(FreshWorld = true)]
    public async Task Wall_Should_TakeADeck_When_AFloorRunIsExtendedTowardIt()
    {
        (BlockPos beside, BlockPos wall, _, ITestPlayer player) = await RaiseFloorBesideWall();

        await WallBuilder.Press(World, player, beside, "game:plank-oak", BlockFacing.UP, TopHit, quantity: 3);

        Assert.Equal((("oak", "clay", "planks", null, (string?)null), "oak", 1), (WallBuilder.Layers(World, beside), DeckOf(wall), Held(player)));
    }

    [AtlasScenario(FreshWorld = true)]
    public async Task Floor_Should_TakePlanksOnItsUnderside_When_TheWallAtTheEndOfItsRunHasADeck()
    {
        (BlockPos beside, BlockPos wall, _, ITestPlayer player) = await RaiseFloorBesideWall();
        await WallBuilder.Press(World, player, beside, "game:plank-oak", BlockFacing.UP, TopHit, quantity: 2);
        Assert.Equal("oak", DeckOf(wall));

        await WallBuilder.Press(World, player, beside, "game:plank-oak", BlockFacing.UP, TopHit, quantity: 2);

        Assert.Equal((("oak", "clay", "planks", null, "planks"), "oak"), (WallBuilder.Layers(World, beside), DeckOf(wall)));
    }

    [AtlasScenario(FreshWorld = true)]
    public async Task Floor_Should_BeLaidBesideTheWall_When_PlanksArePressedOnTheWallItself()
    {
        (BlockPos beside, BlockPos wall, BlockFacing ahead, ITestPlayer player) = await RaiseWallAhead();
        // Raise left the player standing in the cell the floor is laid into.
        await player.TeleportTo(beside.AddCopy(ahead.Opposite));

        await WallBuilder.Press(World, player, wall, "game:plank-oak", ahead, Middle, quantity: 3);

        Assert.Equal((null, "floor"), (DeckOf(wall), World.BlockAt(wall.AddCopy(ahead)).Code.Path));
    }

    // A boarded floor beside the wall, the run ending at it.
    private async Task<(BlockPos Beside, BlockPos Wall, BlockFacing Ahead, ITestPlayer Player)> RaiseFloorBesideWall()
    {
        var built = await RaiseWallAhead();
        await WallBuilder.Raise(World, built.Player, built.Beside, built.Wall, "game:plank-oak", "game:clay-blue");
        await WallBuilder.Press(World, built.Player, built.Beside, "game:plank-oak", BlockFacing.UP, TopHit, quantity: 2);
        return built;
    }
}
