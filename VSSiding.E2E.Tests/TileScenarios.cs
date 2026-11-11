using System.Linq;
using System.Threading.Tasks;
using Atlas.Api;
using Atlas.XUnit;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Xunit;

namespace VSSiding.E2E.Tests;

// A fired tile goes into the slot of a face under the crosshair, one tile a slot, and a break takes one back (decision 0095).
// Every wall here hugs the west face of its cell, so west is its front and east its back; a slot is row * 2 + col, the
// west wall's col running north to south.
[AtlasWorld(StrictBootDiagnostics = true)]
public class TileScenarios : AtlasScenarioBase
{
    private const string Blue = "game:claytile-fired-plain-blue";
    private const string Red = "game:claytile-fired-center-red";
    private static readonly Vec3d SouthLow = new(0, 0.25, 0.75);
    private static readonly Vec3d NorthHigh = new(0, 0.75, 0.25);
    private static readonly Vec3d NorthLow = new(0, 0.25, 0.25);

    [AtlasScenario(RollbackWorld = true, StrictIsolation = true)]
    public async Task Wall_Should_FillTheSlotsUnderTheCrosshair_When_TilesArePressedOnItsFront()
    {
        (BlockPos cell, ITestPlayer player) = await RaiseWall(back: null);

        await WallBuilder.Press(World, player, cell, Blue, BlockFacing.WEST, SouthLow);
        await WallBuilder.Press(World, player, cell, Red, BlockFacing.WEST, NorthHigh);

        Assert.Equal((("oak", "clay", "tiles", null, (string?)null), ",tile-plain-blue,tile-center-red,", 3), (WallBuilder.Layers(World, cell), Read(cell, "frontstyle"), Held(player)));
    }

    [AtlasScenario(RollbackWorld = true, StrictIsolation = true)]
    public async Task Wall_Should_TileItsBareFrontAndKeepItsBack_When_ATileIsPressedOnItsFinishedBack()
    {
        (BlockPos cell, ITestPlayer player) = await RaiseWall();

        await WallBuilder.Press(World, player, cell, Blue, BlockFacing.EAST, NorthLow);

        Assert.Equal((("oak", "clay", "tiles", null, "planks-birch"), "tile-plain-blue,,,"), (WallBuilder.Layers(World, cell), Read(cell, "frontstyle")));
    }

    [AtlasScenario(RollbackWorld = true, StrictIsolation = true)]
    public async Task Wall_Should_RefuseAndKeepTheTile_When_ItsSlotIsFull()
    {
        (BlockPos cell, ITestPlayer player) = await RaiseWall(back: null);
        await WallBuilder.Press(World, player, cell, Blue, BlockFacing.WEST, SouthLow);

        await WallBuilder.Press(World, player, cell, Red, BlockFacing.WEST, SouthLow);

        Assert.Equal((",tile-plain-blue,,", 4), (Read(cell, "frontstyle"), Held(player)));
    }

    // Each break takes the tile under the crosshair, else the first one, and the last clears the face.
    [AtlasScenario(RollbackWorld = true, StrictIsolation = true)]
    public async Task Wall_Should_DropOneTilePerBreak_When_ItsTiledFrontIsBroken()
    {
        (BlockPos cell, ITestPlayer player) = await RaiseWall(back: null);
        await WallBuilder.Press(World, player, cell, Blue, BlockFacing.WEST, SouthLow);
        await WallBuilder.Press(World, player, cell, Red, BlockFacing.WEST, NorthHigh);
        await player.TeleportTo(cell.EastCopy(6).Up());

        (string?, string?) Break(Vec3d hit)
        {
            SetBreakSelection(new BlockSelection { Position = cell.Copy(), Face = BlockFacing.WEST, HitPosition = hit });
            World.BlockAt(cell).OnBlockBroken(player.Entity.World, cell, player.Player);
            return (Read(cell, "front"), Read(cell, "frontstyle"));
        }
        var underCrosshair = Break(SouthLow);
        var firstTile = Break(SouthLow);
        await World.Ticks(2);

        var dropped = World.Api.World.GetEntitiesAround(cell.ToVec3d().Add(0.5, 0.5, 0.5), 3, 3, entity => entity is EntityItem)
            .Cast<EntityItem>().Select(item => item.Itemstack.Collectible.Code.ToString()).Where(code => code.Contains("claytile")).Order().ToList();

        Assert.Equal(
            (((string?)"tiles", (string?)",,tile-center-red,"), ((string?)null, (string?)null), string.Join(" ", new[] { Red, Blue }.Order())),
            (underCrosshair, firstTile, string.Join(" ", dropped)));
    }

    [AtlasScenario(RollbackWorld = true, StrictIsolation = true)]
    public async Task Diagonal_Should_RefuseTiles_When_ATileIsPressedOnIt()
    {
        (BlockPos cell, ITestPlayer player) = await RaiseWall(back: null, layout: "diagonal");

        await WallBuilder.Press(World, player, cell, Blue, BlockFacing.WEST, NorthLow);

        Assert.Equal((("oak", "clay", (string?)null, (string?)null, (string?)null), 4), (WallBuilder.Layers(World, cell), Held(player)));
    }

    // The corner's second leg runs along x, so its columns read x where the front's read z.
    [AtlasScenario(RollbackWorld = true, StrictIsolation = true)]
    public async Task Corner_Should_TileBothLegsByTheirOwnAxis_When_TilesArePressedOnItsFronts()
    {
        (BlockPos cell, ITestPlayer player) = await RaiseWall(back: null);
        player.Entity.WatchedAttributes.SetString("vssidingFraming", "corner");
        await WallBuilder.Press(World, player, cell, "game:stick", BlockFacing.WEST, new Vec3d(0, 0.5, 0.2));

        await WallBuilder.Press(World, player, cell, Blue, BlockFacing.WEST, SouthLow);
        await WallBuilder.Press(World, player, cell, Red, BlockFacing.NORTH, new Vec3d(0.75, 0.75, 0));

        Assert.Equal(("cornerout", ",tile-plain-blue,,", ",,,tile-center-red"), (World.BlockAt(cell).Variant["layout"], Read(cell, "frontstyle"), Read(cell, "secondfrontstyle")));
    }

    // Turned at its south end the wall's front becomes the second leg, which counts its columns the other way round.
    [AtlasScenario(RollbackWorld = true, StrictIsolation = true)]
    public async Task Wall_Should_KeepItsTilesWhereTheyWere_When_ItIsUpgradedToACorner()
    {
        (BlockPos cell, ITestPlayer player) = await RaiseWall(back: null);
        await WallBuilder.Press(World, player, cell, Blue, BlockFacing.WEST, NorthLow);
        player.Entity.WatchedAttributes.SetString("vssidingFraming", "corner");

        await WallBuilder.Press(World, player, cell, "game:stick", BlockFacing.WEST, new Vec3d(0, 0.5, 0.8));

        Assert.Equal(
            ("south", (string?)null, "tiles", ",tile-plain-blue,,"),
            (World.BlockAt(cell).Variant["side"], Read(cell, "front"), Read(cell, "secondfront"), Read(cell, "secondfrontstyle")));
    }

    // A floor's slots are the world grid: row 0 the lower z, col 0 the lower x, on the top and the underside alike.
    [AtlasScenario(RollbackWorld = true, StrictIsolation = true)]
    public async Task Floor_Should_TileItsTopAndItsUndersideByTheWorldGrid_When_TilesArePressedOnThem()
    {
        (BlockPos cell, ITestPlayer player) = await RaiseFloor();

        await WallBuilder.Press(World, player, cell, Blue, BlockFacing.UP, new Vec3d(0.25, 1, 0.75));
        await WallBuilder.Press(World, player, cell, Red, BlockFacing.DOWN, new Vec3d(0.75, 0, 0.25));

        Assert.Equal((",,tile-plain-blue,", ",tile-center-red,,", "tiles", "tiles"), (Read(cell, "frontstyle"), Read(cell, "backstyle"), Read(cell, "front"), Read(cell, "back")));
    }

    [AtlasScenario(RollbackWorld = true, StrictIsolation = true)]
    public async Task Floor_Should_RefuseTheTile_When_ItsSlotIsFull()
    {
        (BlockPos cell, ITestPlayer player) = await RaiseFloor();
        await WallBuilder.Press(World, player, cell, Blue, BlockFacing.UP, new Vec3d(0.25, 1, 0.75));

        await WallBuilder.Press(World, player, cell, Red, BlockFacing.UP, new Vec3d(0.25, 1, 0.75));

        Assert.Equal((",,tile-plain-blue,", 4), (Read(cell, "frontstyle"), Held(player)));
    }

    [AtlasScenario(RollbackWorld = true, StrictIsolation = true)]
    public async Task Floor_Should_DropOneTilePerBreak_When_ItsTiledTopIsBroken()
    {
        (BlockPos cell, ITestPlayer player) = await RaiseFloor();
        await WallBuilder.Press(World, player, cell, Blue, BlockFacing.UP, new Vec3d(0.25, 1, 0.75));
        await WallBuilder.Press(World, player, cell, Red, BlockFacing.UP, new Vec3d(0.75, 1, 0.75));
        await player.TeleportTo(cell.EastCopy(6).Up());

        BreakAt(cell, BlockFacing.UP, new Vec3d(0.75, 1, 0.75), player);
        string? underCrosshair = Read(cell, "frontstyle");
        BreakAt(cell, BlockFacing.UP, new Vec3d(0.75, 1, 0.75), player);
        await World.Ticks(2);

        Assert.Equal((",,tile-plain-blue,", (string?)null, string.Join(" ", new[] { Red, Blue }.Order())), (underCrosshair, Read(cell, "front"), string.Join(" ", DroppedTiles(cell))));
    }

    // A wall's deck is a floor laid on its room half, with the same world-grid slots; a break on the deck's own box takes its tile.
    [AtlasScenario(RollbackWorld = true, StrictIsolation = true)]
    public async Task Deck_Should_TakeAndGiveBackTiles_When_TilesAreLaidOnItsTop()
    {
        (BlockPos cell, ITestPlayer player) = await RaiseWall(back: null);
        player.Entity.WatchedAttributes.SetString("vssidingDeck", "deck");
        await WallBuilder.Press(World, player, cell, "game:plank-oak", BlockFacing.EAST, new Vec3d(1, 0.5, 0.5), quantity: 2);
        await WallBuilder.Press(World, player, cell, "game:clay-blue", BlockFacing.UP, new Vec3d(0.5, 1, 0.5), onDeck: true);
        await WallBuilder.Press(World, player, cell, Blue, BlockFacing.UP, new Vec3d(0.75, 1, 0.25), onDeck: true);
        await WallBuilder.Press(World, player, cell, Red, BlockFacing.DOWN, new Vec3d(0.75, 0, 0.75), onDeck: true);
        await player.TeleportTo(cell.EastCopy(6).Up());
        var laid = (Read(cell, "deckfrontstyle"), Read(cell, "deckbackstyle"));

        BreakAt(cell, BlockFacing.UP, new Vec3d(0.75, 1, 0.25), player, deck: true);
        await World.Ticks(2);

        Assert.Equal(((",tile-plain-blue,,", ",,,tile-center-red"), (string?)null, string.Join(" ", new[] { Blue }.Order())), (laid, Read(cell, "deckfront"), string.Join(" ", DroppedTiles(cell))));
    }

    private void BreakAt(BlockPos cell, BlockFacing face, Vec3d hit, ITestPlayer player, bool deck = false)
    {
        SetBreakSelection(new BlockSelection { Position = cell.Copy(), Face = face, HitPosition = hit, SelectionBoxIndex = deck ? World.BlockAt(cell).SelectionBoxes.Length : 0 });
        World.BlockAt(cell).OnBlockBroken(player.Entity.World, cell, player.Player);
    }

    private System.Collections.Generic.List<string> DroppedTiles(BlockPos cell)
        => World.Api.World.GetEntitiesAround(cell.ToVec3d().Add(0.5, 0.5, 0.5), 3, 3, entity => entity is EntityItem)
            .Cast<EntityItem>().Select(item => item.Itemstack.Collectible.Code.ToString()).Where(code => code.Contains("claytile")).Order().ToList();

    private async Task<(BlockPos Cell, ITestPlayer Player)> RaiseFloor()
    {
        BlockPos cell = World.Spawn.Offset(1, 2, 0);
        World.SetBlock("game:planks-aged-ud", cell.DownCopy());
        ITestPlayer player = await WallBuilder.JoinBuilder(World);
        player.Entity.WatchedAttributes.SetString("vssidingFraming", "floor");
        await WallBuilder.Raise(World, player, cell, cell.WestCopy(), "game:plank-oak", "game:clay-blue");
        return (cell, player);
    }

    private static int Held(ITestPlayer player) => player.Player.InventoryManager.ActiveHotbarSlot.Itemstack?.StackSize ?? 0;

    private void SetBreakSelection(BlockSelection selection)
        => World.Api.ModLoader.GetModSystem("VSSiding.SidingModSystem").GetType().Assembly.GetType("VSSiding.SidingWallBlock")!
            .GetField("ServerBreakSelection", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)!.SetValue(null, selection);

    private async Task<(BlockPos Cell, ITestPlayer Player)> RaiseWall(string? back = "game:plank-birch", string layout = "wall")
    {
        BlockPos cell = World.Spawn.Offset(1, 2, 0);
        World.SetBlock("game:planks-aged-ud", cell.DownCopy());
        ITestPlayer player = await WallBuilder.JoinBuilder(World);
        player.Entity.WatchedAttributes.SetString("vssidingFraming", layout);
        await WallBuilder.Raise(World, player, cell, cell.WestCopy(), "game:plank-oak", "game:clay-blue", back);
        return (cell, player);
    }

    private string? Read(BlockPos cell, string key)
    {
        var tree = new TreeAttribute();
        World.BlockEntityAt<BlockEntity>(cell)!.ToTreeAttributes(tree);
        return tree.GetString(key) is { Length: > 0 } value ? value : null;
    }
}
