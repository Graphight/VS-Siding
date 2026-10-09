using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Atlas.Api;
using Atlas.XUnit;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Xunit;

namespace VSSiding.E2E.Tests;

// A diagonal claims its side and the face counter-clockwise from it (decision 0066), and takes a deck over the half of its cell on the room side.
[AtlasWorld(StrictBootDiagnostics = true)]
public class DiagonalScenarios : AtlasScenarioBase
{
    private static readonly Dictionary<string, string> CounterClockwise = new()
    {
        ["west"] = "north", ["south"] = "west", ["east"] = "south", ["north"] = "east",
    };

    [AtlasScenario(FreshWorld = true)]
    public async Task Wall_Should_RetainOnItsTwoClaimedFaces_When_FramedAndFilledAsADiagonal()
    {
        (BlockPos cell, _) = await RaiseFrame("diagonal", deck: false, infill: "game:clay-blue");

        Block wall = World.BlockAt(cell);
        string side = wall.Variant["side"];
        string claimed = string.Join(",", new[] { side, CounterClockwise[side] }.OrderBy(face => face));
        string retaining = string.Join(",", BlockFacing.HORIZONTALS
            .Where(face => wall.GetRetention(cell, face, EnumRetentionType.Heat) != 0)
            .Select(face => face.Code)
            .OrderBy(face => face));

        Assert.Equal((("diagonal", claimed), ("oak", "clay", null, null, null)), ((wall.Variant["layout"], retaining), WallBuilder.Layers(World, cell)));
    }

    // A frame and a deck are two charges of planks.
    [AtlasScenario(FreshWorld = true)]
    public async Task Frame_Should_StoreTheDeck_When_DeckIsLitAndADiagonalIsPicked()
    {
        (BlockPos cell, _) = await RaiseFrame("diagonal", deck: true, framingCost: 4);

        Assert.Equal((("oak", null, null, null, null), "oak"), (WallBuilder.Layers(World, cell), Deck(cell)));
    }

    [AtlasScenario(FreshWorld = true)]
    public async Task Diagonal_Should_TakeADeckInPlace_When_ItsSideIsClickedWithTheDeckLit()
    {
        (BlockPos cell, ITestPlayer player) = await RaiseFrame("diagonal", deck: false);

        player.Entity.WatchedAttributes.SetString("vssidingDeck", "deck");
        await player.GiveItem("game:plank-oak", 2);
        Assert.True(World.BlockAt(cell).OnBlockInteractStart(player.Entity.World, player.Player, RoomSide(cell)));

        Assert.Equal(("oak", null), (Deck(cell), player.Player.InventoryManager.ActiveHotbarSlot.Itemstack));
    }

    [AtlasScenario(FreshWorld = true)]
    public async Task Deck_Should_RetainUpward_When_ItsInfillIsLaidOnADiagonal()
    {
        (BlockPos cell, ITestPlayer player) = await RaiseFrame("diagonal", deck: true, framingCost: 4);
        Block wall = World.BlockAt(cell);
        bool bare = wall.GetRetention(cell, BlockFacing.UP, EnumRetentionType.Heat) != 0;

        var deckSel = new BlockSelection
        {
            Position = cell.Copy(), Face = BlockFacing.UP, HitPosition = new Vec3d(0.5, 1, 0.5),
            SelectionBoxIndex = wall.SelectionBoxes.Length,
        };
        await player.GiveItem("game:clay-blue", 4);
        Assert.True(wall.OnBlockInteractStart(player.Entity.World, player.Player, deckSel));

        Assert.Equal((false, "clay", true), (bare, DeckInfill(cell), World.BlockAt(cell).GetRetention(cell, BlockFacing.UP, EnumRetentionType.Heat) != 0));
    }

    // With no saw held the wall's click hosts or hands the click on; vanilla's placement asks IsReplacableBy.
    [AtlasScenario(FreshWorld = true)]
    public async Task Diagonal_Should_StayInItsCell_When_AChestIsPlacedThere()
    {
        (BlockPos cell, ITestPlayer player) = await RaiseFrame("diagonal", deck: false, infill: "game:clay-blue");

        player.Entity.LeftHandItemSlot.Itemstack = null;
        await player.GiveItem("game:chest-east", 1);
        ItemStack chest = player.Player.InventoryManager.ActiveHotbarSlot.Itemstack!;
        bool handled = World.BlockAt(cell).OnBlockInteractStart(player.Entity.World, player.Player, RoomSide(cell));
        string failure = "";
        bool placed = chest.Block.TryPlaceBlock(player.Entity.World, player.Player, chest, RoomSide(cell), ref failure);

        Assert.Equal((false, false, "diagonal", null), (handled, placed, World.BlockAt(cell).Variant["layout"], WallBuilder.GuestLayers(World, cell)));
    }

    // The in-place upgrade keeps the block entity, so the deck rides over to the diagonal.
    [AtlasScenario(FreshWorld = true)]
    public async Task Frame_Should_BecomeADiagonalKeepingItsDeck_When_ADeckedFrameIsClickedWithADiagonalPicked()
    {
        (BlockPos cell, ITestPlayer player) = await RaiseFrame("wall", deck: true, framingCost: 4);

        player.Entity.WatchedAttributes.SetString("vssidingFraming", "diagonal");
        await player.GiveItem("game:plank-oak", 2);
        Block wall = World.BlockAt(cell);
        var frontSel = new BlockSelection { Position = cell.Copy(), Face = BlockFacing.FromCode(wall.Variant["side"]), HitPosition = new Vec3d(0.5, 0.5, 0.5) };
        wall.OnBlockInteractStart(player.Entity.World, player.Player, frontSel);

        Assert.Equal(("diagonal", "oak"), (World.BlockAt(cell).Variant["layout"], Deck(cell)));
    }

    private BlockSelection RoomSide(BlockPos cell)
        => new() { Position = cell.Copy(), Face = BlockFacing.FromCode(World.BlockAt(cell).Variant["side"]).Opposite, HitPosition = new Vec3d(0.5, 0.5, 0.5) };

    private string? Deck(BlockPos cell) => Read(cell, "deck");

    private string? DeckInfill(BlockPos cell) => Read(cell, "deckinfill");

    private string? Read(BlockPos cell, string key)
    {
        var tree = new TreeAttribute();
        World.BlockEntityAt<BlockEntity>(cell)!.ToTreeAttributes(tree);
        return tree.GetString(key) is { Length: > 0 } value ? value : null;
    }

    private async Task<(BlockPos Cell, ITestPlayer Player)> RaiseFrame(string layout, bool deck, string? infill = null, int framingCost = 2)
    {
        BlockPos cell = World.Spawn.Offset(1, 2, 0);
        World.SetBlock("game:planks-aged-ud", cell.DownCopy());
        ITestPlayer player = await WallBuilder.JoinBuilder(World);
        player.Entity.WatchedAttributes.SetString("vssidingFraming", layout);
        if (deck) player.Entity.WatchedAttributes.SetString("vssidingDeck", "deck");
        await WallBuilder.Raise(World, player, cell, cell.WestCopy(), "game:plank-oak", infill, framingCost: framingCost);
        return (cell, player);
    }
}
