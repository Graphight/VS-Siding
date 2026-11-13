using System.Threading.Tasks;
using Atlas.Api;
using Atlas.XUnit;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Xunit;

namespace VSSiding.E2E.Tests;

[AtlasWorld(StrictBootDiagnostics = true)]
public class DeckScenarios : AtlasScenarioBase
{
    [AtlasScenario(RollbackWorld = true, StrictIsolation = true)]
    public async Task Deck_Should_TakeFloorboards_When_PlanksFinishItWithFloorPicked()
        => Assert.Equal(("oak", "clay", "planks", null), await FloorboardedDeck("game:clay-blue", 4));

    // A glass deck takes a finish like any other, drawn as a rim round the glass (decision 0071).
    [AtlasScenario(RollbackWorld = true, StrictIsolation = true)]
    public async Task Deck_Should_TakeFloorboardsKeepingItsGlass_When_ItIsGlazed()
        => Assert.Equal(("oak", "glass-plain", "planks", null), await FloorboardedDeck("game:glass-plain", 1));

    private async Task<(string? Deck, string? Infill, string? Front, string? Back)> FloorboardedDeck(string infill, int infillCost)
    {
        BlockPos cell = World.Spawn.Offset(1, 2, 0);
        World.SetBlock("game:planks-aged-ud", cell.DownCopy());
        ITestPlayer player = await WallBuilder.JoinBuilder(World);
        await WallBuilder.Raise(World, player, cell, cell.WestCopy(), "game:plank-oak", "game:clay-blue");
        ItemSlot slot = player.Player.InventoryManager.ActiveHotbarSlot;

        player.Entity.WatchedAttributes.SetString("vssidingDeck", "deck");
        await player.GiveItem("game:plank-oak", 2);
        var sideSel = new BlockSelection { Position = cell.Copy(), Face = BlockFacing.EAST, HitPosition = new Vec3d(1, 0.5, 0.5) };
        Assert.True(World.BlockAt(cell).OnBlockInteractStart(player.Entity.World, player.Player, sideSel));

        // The deck's box follows the block's own boxes (SidingWallBlock.IsDeckHit).
        var deckTopSel = new BlockSelection
        {
            Position = cell.Copy(), Face = BlockFacing.UP, HitPosition = new Vec3d(0.5, 1, 0.5),
            SelectionBoxIndex = World.BlockAt(cell).SelectionBoxes.Length,
        };
        await player.GiveItem(infill, infillCost);
        Assert.True(World.BlockAt(cell).OnBlockInteractStart(player.Entity.World, player.Player, deckTopSel));

        player.Entity.WatchedAttributes.SetString("vssidingFraming", "floor");
        await player.GiveItem("game:plank-oak", 2);
        Assert.True(World.BlockAt(cell).OnBlockInteractStart(player.Entity.World, player.Player, deckTopSel));

        Assert.Null(slot.Itemstack);
        return DeckLayers(cell);
    }

    [AtlasScenario(RollbackWorld = true, StrictIsolation = true)]
    public async Task Wall_Should_TakeNoDeckAndSpendNothing_When_PlanksArePressedOnItInAnotherPlayersClaim()
    {
        BlockPos cell = World.Spawn.Offset(1, 2, 0);
        World.SetBlock("game:planks-aged-ud", cell.DownCopy());
        ITestPlayer player = await WallBuilder.JoinBuilder(World);
        await WallBuilder.Raise(World, player, cell, cell.WestCopy(), "game:plank-oak", "game:clay-blue");
        player.Entity.WatchedAttributes.SetString("vssidingDeck", "deck");
        LandClaim claim = WallBuilder.ClaimAround(World, cell);
        try
        {
            await WallBuilder.Press(World, player, cell, "game:plank-oak", BlockFacing.EAST, new Vec3d(1, 0.5, 0.5));

            Assert.Equal((((string?)null), 4), (DeckLayers(cell).Deck, player.Player.InventoryManager.ActiveHotbarSlot.Itemstack?.StackSize ?? 0));
        }
        finally
        {
            World.Api.World.Claims.Remove(claim);
        }
    }

    private async Task<(BlockPos Cell, ITestPlayer Player)> DeckedWall()
    {
        BlockPos cell = World.Spawn.Offset(1, 2, 0);
        World.SetBlock("game:planks-aged-ud", cell.DownCopy());
        ITestPlayer player = await WallBuilder.JoinBuilder(World);
        await WallBuilder.Raise(World, player, cell, cell.WestCopy(), "game:plank-oak", "game:clay-blue");

        player.Entity.WatchedAttributes.SetString("vssidingDeck", "deck");
        await player.GiveItem("game:plank-oak", 2);
        var sideSel = new BlockSelection { Position = cell.Copy(), Face = BlockFacing.EAST, HitPosition = new Vec3d(1, 0.5, 0.5) };
        Assert.True(World.BlockAt(cell).OnBlockInteractStart(player.Entity.World, player.Player, sideSel));
        return (cell, player);
    }

    // A lantern places through its own behaviour, which reaches the decked cell check only by asking CanPlaceBlock, for
    // the lantern as held. On the wall face a large lantern is 14/16 tall, too tall to fit under the deck (decision 0094).
    [AtlasScenario(RollbackWorld = true, StrictIsolation = true)]
    public async Task Deck_Should_StayOnItsWall_When_ALargeLanternIsHungOnTheWallFace()
    {
        (BlockPos cell, ITestPlayer player) = await DeckedWall();

        ItemStack stack = WallBuilder.LanternStack(World);
        var faceSel = new BlockSelection { Position = cell.Copy(), Face = BlockFacing.EAST, HitPosition = new Vec3d(1, 0.5, 0.5) };
        string failureCode = "";
        bool placed = stack.Block.TryPlaceBlock(player.Entity.World, player.Player, stack, faceSel, ref failureCode);

        Assert.Equal((false, "vssiding-decked", ("oak", (string?)null, (string?)null, (string?)null)), (placed, failureCode, DeckLayers(cell)));
    }

    // Standing on the floor, the same lantern is 12/16 tall and fits under the deck as it is.
    [AtlasScenario(RollbackWorld = true, StrictIsolation = true)]
    public async Task Deck_Should_StayOverALantern_When_ItStandsOnTheFloorOfTheCell()
    {
        (BlockPos cell, ITestPlayer player) = await DeckedWall();

        ItemStack stack = WallBuilder.LanternStack(World);
        var floorSel = new BlockSelection { Position = cell.Copy(), Face = BlockFacing.UP, HitPosition = new Vec3d(0.5, 0.5, 0.5) };
        string failureCode = "";
        bool placed = stack.Block.TryPlaceBlock(player.Entity.World, player.Player, stack, floorSel, ref failureCode);

        Assert.Equal((true, "lantern-large-up", ("oak", (string?)null, (string?)null, (string?)null), 0.0),
            (placed, World.BlockAt(cell).Code.Path, WallBuilder.GuestDeckLayers(World, cell), WallBuilder.HangShiftAt(World, cell)));
    }

    // A player's click on the open side, through TryHost: the torch is held standing, 14/16 tall, but goes on as the wall
    // torch for the clicked face, which fits and drops until its top meets the deck (#164).
    [AtlasScenario(RollbackWorld = true, StrictIsolation = true)]
    public async Task Deck_Should_StayOverATorch_When_APlayerPutsOneOnTheWallFace()
    {
        (BlockPos cell, ITestPlayer player) = await DeckedWall();

        player.Entity.LeftHandItemSlot.Itemstack = null;
        await WallBuilder.Press(World, player, cell, "game:torch-basic-lit-up", BlockFacing.EAST, new Vec3d(1, 0.5, 0.5), quantity: 1);

        Assert.Equal(("torch-basic-lit-east", ("oak", (string?)null, (string?)null, (string?)null), -0.15),
            (World.BlockAt(cell).Code.Path, WallBuilder.GuestDeckLayers(World, cell), System.Math.Round(WallBuilder.HangShiftAt(World, cell), 3)));
    }

    // A torch holder stands against a wall face, so the decked cell hosts it under the deck (decision 0094): the deck stays, still seals its top, and comes back with the wall.
    [AtlasScenario(RollbackWorld = true, StrictIsolation = true)]
    public async Task Deck_Should_StayAndSealItsTop_When_ATorchHolderIsHostedUnderIt()
    {
        (BlockPos cell, ITestPlayer player) = await DeckedWall();
        var deckTopSel = new BlockSelection
        {
            Position = cell.Copy(), Face = BlockFacing.UP, HitPosition = new Vec3d(0.5, 1, 0.5),
            SelectionBoxIndex = World.BlockAt(cell).SelectionBoxes.Length,
        };
        await player.GiveItem("game:clay-blue", 4);
        Assert.True(World.BlockAt(cell).OnBlockInteractStart(player.Entity.World, player.Player, deckTopSel));
        var deck = ("oak", "clay", (string?)null, (string?)null);
        Assert.Equal(deck, DeckLayers(cell));

        World.SetBlock("game:torchholder-brass-empty-west", cell);
        bool sealsHosted = World.BlockAt(cell).GetRetention(cell, BlockFacing.UP, EnumRetentionType.Heat) != 0;
        var hosted = WallBuilder.GuestDeckLayers(World, cell);

        World.Api.World.BlockAccessor.BreakBlock(cell, player.Player);
        await World.Until(() => World.BlockEntityAt<BlockEntity>(cell) != null, 100);
        bool sealsRestored = World.BlockAt(cell).GetRetention(cell, BlockFacing.UP, EnumRetentionType.Heat) != 0;

        Assert.Equal((true, deck, true, deck), (sealsHosted, hosted, sealsRestored, DeckLayers(cell)));
    }

    private (string? Deck, string? Infill, string? Front, string? Back) DeckLayers(BlockPos cell)
    {
        var tree = new TreeAttribute();
        World.BlockEntityAt<BlockEntity>(cell)!.ToTreeAttributes(tree);
        string? Read(string key) => tree.GetString(key) is { Length: > 0 } value ? value : null;
        return (Read("deck"), Read("deckinfill"), Read("deckfront"), Read("deckback"));
    }
}
