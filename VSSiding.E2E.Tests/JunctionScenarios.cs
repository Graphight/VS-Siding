using System.Linq;
using System.Threading.Tasks;
using Atlas.Api;
using Atlas.XUnit;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Xunit;

namespace VSSiding.E2E.Tests;

// A filled wall turns into a cornerout in place where a fresh Corner press cannot board the face (decision 0069).
// Every wall here hugs the west face of its cell, so west is its front, east its back, and south the run's right end.
[AtlasWorld(StrictBootDiagnostics = true)]
public class JunctionScenarios : AtlasScenarioBase
{
    private static readonly Vec3d SouthEnd = new(0, 0.5, 0.8);
    private static readonly Vec3d NorthEnd = new(0, 0.5, 0.2);

    [AtlasScenario(FreshWorld = true)]
    public async Task FilledWall_Should_BecomeACornerMovingItsFrontFinishToTheSecondFace_When_ItsFinishedFrontIsPressedAtTheRightEnd()
    {
        (BlockPos cell, ITestPlayer player) = await RaiseFinishedDeckedWall();

        await Press(player, cell, "game:plank-oak", BlockFacing.WEST, SouthEnd);

        Assert.Equal(("cornerout", "south,west", "oak", ("oak", "clay", null, "planks-birch", "planks-birch")), Describe(cell));
    }

    [AtlasScenario(FreshWorld = true)]
    public async Task FilledWall_Should_BecomeACornerKeepingItsFrontFinish_When_ItsFinishedFrontIsPressedAtTheLeftEnd()
    {
        (BlockPos cell, ITestPlayer player) = await RaiseFinishedDeckedWall();

        await Press(player, cell, "game:plank-oak", BlockFacing.WEST, NorthEnd);

        Assert.Equal(("cornerout", "north,west", "oak", ("oak", "clay", "planks-birch", null, "planks-birch")), Describe(cell));
    }

    [AtlasScenario(FreshWorld = true)]
    public async Task FilledWall_Should_BecomeACorner_When_SticksArePressedOnItsUnfinishedFront()
    {
        (BlockPos cell, ITestPlayer player) = await RaiseWall("game:clay-blue");

        await Press(player, cell, "game:stick", BlockFacing.WEST, NorthEnd);

        Assert.Equal(("cornerout", "north,west", null, ("oak", "clay", null, null, null)), Describe(cell));
    }

    [AtlasScenario(FreshWorld = true)]
    public async Task GlazedWall_Should_TakePlanksAsItsFrontFinishKeepingItsGlass_When_PlanksArePressedOnItsFront()
    {
        (BlockPos cell, ITestPlayer player) = await RaiseWall(infill: null);
        await Press(player, cell, "game:glass-plain", BlockFacing.WEST, NorthEnd, quantity: 1);

        await Press(player, cell, "game:plank-oak", BlockFacing.WEST, NorthEnd);

        Assert.Equal(("wall", "west", null, ("oak", "glass-plain", "planks", null, null)), Describe(cell));
    }

    [AtlasScenario(FreshWorld = true)]
    public async Task GlazedWall_Should_BecomeACornerKeepingItsGlassAndFinish_When_ItsFinishedFrontIsPressedAtTheLeftEnd()
    {
        (BlockPos cell, ITestPlayer player) = await RaiseWall(infill: null);
        await Press(player, cell, "game:glass-plain", BlockFacing.WEST, NorthEnd, quantity: 1);
        await Press(player, cell, "game:plank-oak", BlockFacing.WEST, NorthEnd);

        await Press(player, cell, "game:plank-oak", BlockFacing.WEST, NorthEnd);

        Assert.Equal(("cornerout", "north,west", null, ("oak", "glass-plain", "planks", null, null)), Describe(cell));
    }

    // Vanilla repeats a held right-click, and the repeat of the press that boarded the face finds it finished.
    [AtlasScenario(FreshWorld = true)]
    public async Task FilledWall_Should_StayAWall_When_ThePressThatBoardsItsFaceIsHeld()
    {
        (BlockPos cell, ITestPlayer player) = await RaiseWall("game:clay-blue");

        await Press(player, cell, "game:plank-oak", BlockFacing.EAST, SouthEnd, held: true);

        Assert.Equal(("wall", "west", null, ("oak", "clay", null, null, "planks")), Describe(cell));
    }

    [AtlasScenario(FreshWorld = true)]
    public async Task BareFrame_Should_StayAWall_When_ThePressThatFillsItWithSticksIsHeld()
    {
        (BlockPos cell, ITestPlayer player) = await RaiseWall(infill: null);

        await Press(player, cell, "game:stick", BlockFacing.WEST, SouthEnd, quantity: 8, held: true);

        Assert.Equal(("wall", "west", null, ("oak", "wattle", null, null, null)), Describe(cell));
    }

    // Sneaking skips the wall's own interaction, and still frames the next cell (decision 0058).
    [AtlasScenario(FreshWorld = true)]
    public async Task FilledWall_Should_StayAWallBesideANewCorner_When_ItsFinishedFrontIsSneakPressed()
    {
        (BlockPos cell, ITestPlayer player) = await RaiseFinishedDeckedWall();
        player.Entity.Controls.ShiftKey = true;

        await Press(player, cell, "game:plank-oak", BlockFacing.WEST, SouthEnd);

        Assert.Equal(("wall", "cornerout"), (World.BlockAt(cell).Variant["layout"], World.BlockAt(cell.WestCopy()).Variant["layout"]));
    }

    [AtlasScenario(FreshWorld = true)]
    public async Task FilledWall_Should_StayAWall_When_ItsBareDeckIsPressedWithPlanks()
    {
        (BlockPos cell, ITestPlayer player) = await RaiseFinishedDeckedWall();

        await Press(player, cell, "game:plank-oak", BlockFacing.EAST, SouthEnd, onDeck: true);

        Assert.Equal("wall", World.BlockAt(cell).Variant["layout"]);
    }

    [AtlasScenario(FreshWorld = true)]
    public async Task FilledWall_Should_StayAWall_When_ItCarriesAStep()
    {
        (BlockPos cell, ITestPlayer player) = await RaiseWall("game:clay-blue", back: "game:plank-birch");
        await Press(player, cell, "game:plankstairs-oak-up-north-free", BlockFacing.EAST, SouthEnd, quantity: 1);

        await Press(player, cell, "game:plank-oak", BlockFacing.EAST, SouthEnd);

        Assert.Equal(("wall", "game:plankstairs-oak-up-north-free"), (World.BlockAt(cell).Variant["layout"], Read(cell, "step")));
    }

    [AtlasScenario(FreshWorld = true)]
    public async Task FilledWall_Should_StayAWall_When_AnAnimalStandsWhereTheNewLegGoes()
    {
        (BlockPos cell, ITestPlayer player) = await RaiseWall("game:clay-blue", back: "game:plank-birch");
        World.SpawnEntity("game:sheep-bighorn-adult-female", cell);
        await World.Ticks(5);

        await Press(player, cell, "game:plank-oak", BlockFacing.EAST, SouthEnd);

        Assert.Equal("wall", World.BlockAt(cell).Variant["layout"]);
    }

    private async Task<(BlockPos Cell, ITestPlayer Player)> RaiseFinishedDeckedWall()
    {
        (BlockPos cell, ITestPlayer player) = await RaiseWall("game:clay-blue", back: "game:plank-birch", deck: true);
        await Press(player, cell, "game:plank-birch", BlockFacing.WEST, SouthEnd);
        return (cell, player);
    }

    private async Task<(BlockPos Cell, ITestPlayer Player)> RaiseWall(string? infill, string? back = null, bool deck = false)
    {
        BlockPos cell = World.Spawn.Offset(1, 2, 0);
        World.SetBlock("game:planks-aged-ud", cell.DownCopy());
        ITestPlayer player = await WallBuilder.JoinBuilder(World);
        if (deck) player.Entity.WatchedAttributes.SetString("vssidingDeck", "deck");
        await WallBuilder.Raise(World, player, cell, cell.WestCopy(), "game:plank-oak", infill, back, framingCost: deck ? 4 : 2);
        player.Entity.WatchedAttributes.SetString("vssidingFraming", "corner");
        return (cell, player);
    }

    // A right-click as vanilla sends it: the wall answers first unless the player sneaks, and the held item only
    // when the wall passes the click on. A held button sends the click again with firstEvent false.
    // The deck's boxes follow the wall's own.
    private async Task Press(
        ITestPlayer player, BlockPos cell, string item, BlockFacing face, Vec3d hit, int quantity = 4, bool held = false, bool onDeck = false)
    {
        ItemSlot slot = player.Player.InventoryManager.ActiveHotbarSlot;
        slot.Itemstack = null;
        await player.GiveItem(item, quantity);
        var sel = new BlockSelection
        {
            Position = cell.Copy(), Face = face, HitPosition = hit,
            SelectionBoxIndex = onDeck ? World.BlockAt(cell).SelectionBoxes.Length : 0,
        };

        foreach (bool firstEvent in held ? new[] { true, false } : new[] { true })
        {
            if (!player.Entity.Controls.ShiftKey && World.BlockAt(cell).OnBlockInteractStart(player.Entity.World, player.Player, sel)) continue;
            var handling = EnumHandHandling.NotHandled;
            slot.Itemstack?.Collectible.OnHeldInteractStart(slot, player.Entity, sel, null, firstEvent, ref handling);
        }
    }

    private (string Layout, string Retaining, string? Deck, (string? Framing, string? Infill, string? Front, string? SecondFront, string? Back) Layers) Describe(BlockPos cell)
    {
        Block wall = World.BlockAt(cell);
        string retaining = string.Join(",", BlockFacing.HORIZONTALS
            .Where(face => wall.GetRetention(cell, face, EnumRetentionType.Heat) != 0)
            .Select(face => face.Code)
            .OrderBy(face => face));
        return (wall.Variant["layout"], retaining, Read(cell, "deck"), WallBuilder.Layers(World, cell));
    }

    private string? Read(BlockPos cell, string key)
    {
        var tree = new TreeAttribute();
        World.BlockEntityAt<BlockEntity>(cell)!.ToTreeAttributes(tree);
        return tree.GetString(key) is { Length: > 0 } value ? value : null;
    }
}
