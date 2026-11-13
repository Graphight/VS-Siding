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

// A diagonal claims its side and the face counter-clockwise from it (decision 0066), and takes a deck over each half of its cell (decisions 0067, 0096).
[AtlasWorld(StrictBootDiagnostics = true)]
public class DiagonalScenarios : AtlasScenarioBase
{
    private static readonly Dictionary<string, string> CounterClockwise = new()
    {
        ["west"] = "north", ["south"] = "west", ["east"] = "south", ["north"] = "east",
    };

    [AtlasScenario(RollbackWorld = true, StrictIsolation = true)]
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
    [AtlasScenario(RollbackWorld = true, StrictIsolation = true)]
    public async Task Frame_Should_StoreTheDeck_When_DeckIsLitAndADiagonalIsPicked()
    {
        (BlockPos cell, _) = await RaiseFrame("diagonal", deck: true, framingCost: 3);

        Assert.Equal((("oak", null, null, null, null), "oak"), (WallBuilder.Layers(World, cell), Deck(cell)));
    }

    [AtlasScenario(RollbackWorld = true, StrictIsolation = true)]
    public async Task Diagonal_Should_TakeADeckInPlace_When_ItsSideIsClickedWithTheDeckLit()
    {
        (BlockPos cell, ITestPlayer player) = await RaiseFrame("diagonal", deck: false);

        player.Entity.WatchedAttributes.SetString("vssidingDeck", "deck");
        await player.GiveItem("game:plank-oak", 1);
        Assert.True(World.BlockAt(cell).OnBlockInteractStart(player.Entity.World, player.Player, RoomSide(cell)));

        Assert.Equal(("oak", null), (Deck(cell), player.Player.InventoryManager.ActiveHotbarSlot.Itemstack));
    }

    [AtlasScenario(RollbackWorld = true, StrictIsolation = true)]
    public async Task Deck_Should_RetainUpward_When_ItsInfillIsLaidOnADiagonal()
    {
        (BlockPos cell, ITestPlayer player) = await RaiseFrame("diagonal", deck: true, framingCost: 3);
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

    // A glazed diagonal takes a finish like any glazed cell, as a rim round the glass.
    [AtlasScenario(RollbackWorld = true, StrictIsolation = true)]
    public async Task Diagonal_Should_TakeTheFinish_When_ItIsGlazedAndPlanksArePressedOnItsFront()
    {
        (BlockPos cell, ITestPlayer player) = await RaiseFrame("diagonal", deck: false);
        Block wall = World.BlockAt(cell);
        var frontSel = new BlockSelection { Position = cell.Copy(), Face = BlockFacing.FromCode(wall.Variant["side"]), HitPosition = new Vec3d(0.5, 0.5, 0.5) };
        await player.GiveItem("game:glass-plain", 1);
        Assert.True(wall.OnBlockInteractStart(player.Entity.World, player.Player, frontSel));

        await player.GiveItem("game:plank-oak", 4);
        bool handled = wall.OnBlockInteractStart(player.Entity.World, player.Player, frontSel);

        Assert.Equal((true, ("oak", "glass-plain", "planks", null, null)), (handled, WallBuilder.Layers(World, cell)));
    }

    // With no saw held the wall's click hosts or hands the click on; vanilla's placement asks IsReplacableBy.
    [AtlasScenario(RollbackWorld = true, StrictIsolation = true)]
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
    [AtlasScenario(RollbackWorld = true, StrictIsolation = true)]
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

    // A claimed face is the diagonal's outer side (decision 0096): it decks the outer triangle and leaves the inner bare.
    [AtlasScenario(RollbackWorld = true, StrictIsolation = true)]
    public Task Diagonal_Should_DeckItsOuterHalfOnly_When_ItsSideFaceIsClickedWithTheDeckLit() => OuterDeckFrom(side => side);

    [AtlasScenario(RollbackWorld = true, StrictIsolation = true)]
    public Task Diagonal_Should_DeckItsOuterHalfOnly_When_ItsOtherClaimedFaceIsClickedWithTheDeckLit() => OuterDeckFrom(side => CounterClockwise[side]);

    // An outer deck costs half a frame of planks, one of the frame's two.
    private async Task OuterDeckFrom(System.Func<string, string> face)
    {
        (BlockPos cell, ITestPlayer player) = await RaiseFrame("diagonal", deck: false);
        await AddOuterDeck(cell, player, BlockFacing.FromCode(face(World.BlockAt(cell).Variant["side"])));

        Assert.Equal(((string?)null, (string?)"oak", (ItemStack?)null), (Deck(cell), Read(cell, "outerdeck"), player.Player.InventoryManager.ActiveHotbarSlot.Itemstack));
    }

    [AtlasScenario(RollbackWorld = true, StrictIsolation = true)]
    public async Task Diagonal_Should_DeckItsOuterHalf_When_AFloorIsLaidUpToAClaimedFace()
    {
        (BlockPos cell, ITestPlayer player) = await RaiseFrame("diagonal", deck: false);
        BlockFacing claimed = BlockFacing.FromCode(CounterClockwise[World.BlockAt(cell).Variant["side"]]);
        BlockPos beside = cell.AddCopy(claimed);
        World.SetBlock("game:planks-aged-ud", beside);
        player.Entity.WatchedAttributes.SetString("vssidingFraming", "floor");

        await WallBuilder.Press(World, player, beside, "game:plank-oak", claimed.Opposite, new Vec3d(0.5, 0.5, 0.5), quantity: 2);

        Assert.Equal(((string?)null, (string?)"oak", 1), (Deck(cell), Read(cell, "outerdeck"), player.Player.InventoryManager.ActiveHotbarSlot.Itemstack?.StackSize));
    }

    [AtlasScenario(RollbackWorld = true, StrictIsolation = true)]
    public async Task Diagonal_Should_DeckBothHalvesApart_When_ClaimedAndUnclaimedFacesAreClicked()
    {
        (BlockPos cell, ITestPlayer player) = await RaiseFrame("diagonal", deck: false);
        await AddOuterDeck(cell, player, BlockFacing.FromCode(World.BlockAt(cell).Variant["side"]));
        var outerOnly = (Deck(cell), Read(cell, "outerdeck"));

        await player.GiveItem("game:plank-oak", 1);
        World.BlockAt(cell).OnBlockInteractStart(player.Entity.World, player.Player, RoomSide(cell));

        Assert.Equal(((null, "oak"), ("oak", "oak")), (outerOnly, (Deck(cell), Read(cell, "outerdeck"))));
    }

    // The inner deck's eight strips come first among the deck's boxes, then the outer's (decisions 0067, 0096).
    private const int Strips = 8;

    [AtlasScenario(RollbackWorld = true, StrictIsolation = true)]
    public async Task Deck_Should_LayerAndPeelOneHalf_When_ItsStripIsClicked()
    {
        (BlockPos cell, ITestPlayer player) = await RaiseFrame("diagonal", deck: true, framingCost: 3);
        await AddOuterDeck(cell, player, BlockFacing.FromCode(World.BlockAt(cell).Variant["side"]));
        (string?, string?, string?) Halves() => (DeckInfill(cell), Read(cell, "outerdeckinfill"), Read(cell, "outerdeck"));
        void Peel(int strip)
        {
            WallBuilder.SetServerBreakSelection(World, StripTop(cell, strip));
            World.BlockAt(cell).OnBlockBroken(player.Entity.World, cell, player.Player);
        }

        await player.GiveItem("game:clay-blue", 2);
        World.BlockAt(cell).OnBlockInteractStart(player.Entity.World, player.Player, StripTop(cell, 0));
        var innerFilled = Halves();
        await player.GiveItem("game:clay-blue", 2);
        World.BlockAt(cell).OnBlockInteractStart(player.Entity.World, player.Player, StripTop(cell, Strips));
        var bothFilled = Halves();
        Peel(Strips);
        var outerInfillPeeled = Halves();
        Peel(Strips);

        Assert.Equal(
            (("clay", null, "oak"), ("clay", "clay", "oak"), ("clay", null, "oak"), ("clay", null, null)),
            (innerFilled, bothFilled, outerInfillPeeled, Halves()));
    }

    // With Floor picked, planks on a filled deck's top board it; an outer deck answers from its own infill, not the inner's.
    [AtlasScenario(RollbackWorld = true, StrictIsolation = true)]
    public async Task OuterDeck_Should_TakeFloorboards_When_PlanksFinishItWithFloorPickedAndNoInnerDeck()
    {
        (BlockPos cell, ITestPlayer player) = await RaiseFrame("diagonal", deck: false);
        await AddOuterDeck(cell, player, BlockFacing.FromCode(World.BlockAt(cell).Variant["side"]));
        await player.GiveItem("game:clay-blue", 2);
        World.BlockAt(cell).OnBlockInteractStart(player.Entity.World, player.Player, StripTop(cell, 0));

        player.Entity.WatchedAttributes.SetString("vssidingFraming", "floor");
        await player.GiveItem("game:plank-oak", 1);
        World.BlockAt(cell).OnBlockInteractStart(player.Entity.World, player.Player, StripTop(cell, 0));

        Assert.Equal(((string?)"planks", (ItemStack?)null), (Read(cell, "outerdeckfront"), player.Player.InventoryManager.ActiveHotbarSlot.Itemstack));
    }

    private async Task AddOuterDeck(BlockPos cell, ITestPlayer player, BlockFacing claimedFace)
    {
        player.Entity.WatchedAttributes.SetString("vssidingDeck", "deck");
        await player.GiveItem("game:plank-oak", 1);
        World.BlockAt(cell).OnBlockInteractStart(player.Entity.World, player.Player, FaceSel(cell, claimedFace));
    }

    // A strip's top; with no inner deck the outer's strips come first.
    private BlockSelection StripTop(BlockPos cell, int strip)
        => new() { Position = cell.Copy(), Face = BlockFacing.UP, HitPosition = new Vec3d(0.5, 1, 0.5), SelectionBoxIndex = World.BlockAt(cell).SelectionBoxes.Length + strip };

    private BlockSelection FaceSel(BlockPos cell, BlockFacing face)
        => new() { Position = cell.Copy(), Face = face, HitPosition = new Vec3d(0.5, 0.5, 0.5) };

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
