using System.Collections.Generic;
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

// Post mode puts a post in the cell corner nearest the click, for a quarter of the framing.
[AtlasWorld(StrictBootDiagnostics = true)]
public class PostScenarios : AtlasScenarioBase
{
    [AtlasScenario(FreshWorld = true)]
    public async Task Post_Should_StandInTheNearestCornerForOnePlank_When_PlanksArePressedInPostMode()
    {
        (BlockPos cell, ITestPlayer player, ItemSlot slot, EnumHandHandling handling) = await PlacePost(planks: 3);

        int planksLeft = slot.Itemstack?.StackSize ?? 0;
        await WallBuilder.Press(World, player, cell, "game:clay-blue", BlockFacing.WEST, new Vec3d(0.5, 0.5, 0.5));

        Assert.Equal(
            ("wall-cornerin-south", EnumHandHandling.PreventDefault, 2, ("oak", null, null, null, null)),
            (World.BlockAt(cell).Code.Path, handling, planksLeft, WallBuilder.Layers(World, cell)));
    }

    // The post's frame costs a quarter, its deck the full two planks; the deck takes infill and floorboards like a wall's,
    // and breaking the post hands back the quarter plus every deck layer in full.
    [AtlasScenario(FreshWorld = true)]
    public async Task Post_Should_TakeADeckAtFullCostAndRefundItInFull_When_TheDeckToggleIsLit()
    {
        (BlockPos cell, ITestPlayer player, ItemSlot slot, _) = await PlacePost(planks: 3, deck: true);
        int planksLeft = slot.Itemstack?.StackSize ?? 0;
        var placed = (World.BlockAt(cell).Code.Path, planksLeft, DeckLayers(cell));

        player.Entity.WatchedAttributes.SetString("vssidingDeck", "");
        var deckTopSel = new BlockSelection
        {
            Position = cell.Copy(), Face = BlockFacing.UP, HitPosition = new Vec3d(0.5, 1, 0.5),
            SelectionBoxIndex = World.BlockAt(cell).SelectionBoxes.Length,
        };
        await player.GiveItem("game:clay-blue", 4);
        World.BlockAt(cell).OnBlockInteractStart(player.Entity.World, player.Player, deckTopSel);
        player.Entity.WatchedAttributes.SetString("vssidingFraming", "floor");
        await player.GiveItem("game:plank-oak", 2);
        World.BlockAt(cell).OnBlockInteractStart(player.Entity.World, player.Player, deckTopSel);
        var layered = DeckLayers(cell);

        var drops = World.BlockAt(cell).GetDrops(player.Entity.World, cell, player.Player, 1)
            .GroupBy(stack => stack.Collectible.Code.ToString()).OrderBy(group => group.Key)
            .Select(group => $"{group.Key} x{group.Sum(stack => stack.StackSize)}");
        string dropped = string.Join(", ", drops);

        Assert.Equal(
            (
                ("wall-cornerin-south", 0, ("oak", null, null, null)),
                ("oak", "clay", "planks", null),
                "game:clay-blue x4, game:plank-oak x5"
            ),
            (placed, layered, dropped));
    }

    // A post already standing takes the deck from a click on its side with the toggle lit, for the full two planks.
    [AtlasScenario(FreshWorld = true)]
    public async Task Post_Should_TakeADeckInPlaceForTwoPlanks_When_PlanksAreClickedWithTheDeckToggleLit()
    {
        (BlockPos cell, ITestPlayer player, ItemSlot slot, _) = await PlacePost(planks: 1);

        player.Entity.WatchedAttributes.SetString("vssidingDeck", "deck");
        await player.GiveItem("game:plank-oak", 2);
        var sideSel = new BlockSelection { Position = cell.Copy(), Face = BlockFacing.EAST, HitPosition = new Vec3d(1, 0.5, 0.5) };
        bool handled = World.BlockAt(cell).OnBlockInteractStart(player.Entity.World, player.Player, sideSel);

        Assert.Equal((true, null, ("oak", null, null, null)), (handled, slot.Itemstack, DeckLayers(cell)));
    }

    // A click on either of a post's two faces hosts a table in its cell and the post becomes the guest; breaking the table brings it back.
    [AtlasScenario(FreshWorld = true)]
    public async Task Post_Should_BecomeAGuestAndComeBack_When_AFurnitureBlockIsHostedOnEitherFaceAndBroken()
    {
        (BlockPos cell, ITestPlayer player, ItemSlot slot, _) = await PlacePost(planks: 1);
        (string?, string?, string?, string?, string?) guestPost = ("oak", null, null, null, null);
        Assert.Equal(guestPost, WallBuilder.Layers(World, cell));

        var seen = new List<(string, (string?, string?, string?, string?, string?)?)>();
        foreach (BlockFacing face in new[] { BlockFacing.NORTH, BlockFacing.EAST })
        {
            player.Entity.LeftHandItemSlot.Itemstack = null;
            slot.Itemstack = new ItemStack(World.Api.World.GetBlock(new AssetLocation("game:table-normal")));
            var click = new BlockSelection { Position = cell.Copy(), Face = face, HitPosition = new Vec3d(0.5, 0.5, 0.5) };
            Assert.True(World.BlockAt(cell).OnBlockInteractStart(player.Entity.World, player.Player, click));
            seen.Add((World.BlockAt(cell).Code.ToString(), WallBuilder.GuestLayers(World, cell)));

            World.BlockAt(cell).OnBlockBroken(player.Entity.World, cell, player.Player);
            await World.Ticks(2);
            seen.Add((World.BlockAt(cell).Code.Path, WallBuilder.GuestLayers(World, cell)));
        }

        Assert.Equal(
            new (string, (string?, string?, string?, string?, string?)?)[]
            {
                ("game:table-normal", guestPost),
                ("wall-cornerin-south", null),
                ("game:table-normal", guestPost),
                ("wall-cornerin-south", null),
            },
            seen.ToArray());
    }

    // Each finish costs a quarter of its two planks, and peeling all four finishes and then the frame hands back what was paid.
    [AtlasScenario(FreshWorld = true)]
    public async Task Post_Should_TakeAFinishOnEachFaceForOnePlankAndRefundThemAll_When_PlanksAreBuiltAndPeeled()
    {
        (BlockPos cell, ITestPlayer player, ItemSlot slot, _) = await PlacePost(planks: 1);

        // Away from the post, so the planks it gives back stay on the ground to be counted.
        await player.TeleportTo(cell.EastCopy(6).Up());
        async Task<int> Finish(BlockFacing face, string style)
        {
            player.Entity.WatchedAttributes.SetString("vssidingBoards", style);
            await WallBuilder.Press(World, player, cell, "game:plank-oak", face, new Vec3d(0.5, 0.5, 0.5));
            return slot.Itemstack?.StackSize ?? 0;
        }

        // A post saying "south" has its fronts on the north and east faces and its backs on the south and west ones.
        int heldAfterFront = await Finish(BlockFacing.NORTH, "hboards");
        int heldAfterSecondFront = await Finish(BlockFacing.EAST, "boards");
        int heldAfterBack = await Finish(BlockFacing.SOUTH, "boards");
        int heldAfterSecondBack = await Finish(BlockFacing.WEST, "hboards");
        (string?, string?, string?, string?, string?) Faces(string suffix = "")
        {
            var tree = new TreeAttribute();
            World.BlockEntityAt<BlockEntity>(cell)!.ToTreeAttributes(tree);
            string? Read(string key) => tree.GetString(key + suffix) is { Length: > 0 } value ? value : null;
            return (Read("framing"), Read("front"), Read("secondfront"), Read("back"), Read("secondback"));
        }
        var styles = Faces("style");
        var built = Faces();

        // The server calls OnBlockBroken once per player break; BreakBlock would take the block with the last layer.
        (string?, string?, string?, string?, string?) Peel()
        {
            World.BlockAt(cell).OnBlockBroken(player.Entity.World, cell, player.Player);
            return Faces();
        }
        var peeled = (Peel(), Peel(), Peel(), Peel());
        World.BlockAt(cell).OnBlockBroken(player.Entity.World, cell, player.Player);
        await World.Ticks(2);

        int refunded = World.Api.World.GetEntitiesAround(cell.ToVec3d().Add(0.5, 0.5, 0.5), 3, 3, entity => entity is EntityItem)
            .Cast<EntityItem>().Where(item => item.Itemstack.Collectible.Code.ToString() == "game:plank-oak").Sum(item => item.Itemstack.StackSize);

        Assert.Equal(
            (
                (3, 3, 3, 3), ((string?)null, "hboards", "boards", "boards", "hboards"),
                ("oak", "planks", "planks", "planks", "planks"),
                (
                    ("oak", (string?)null, "planks", "planks", "planks"),
                    ("oak", (string?)null, (string?)null, "planks", "planks"),
                    ("oak", (string?)null, (string?)null, (string?)null, "planks"),
                    ("oak", (string?)null, (string?)null, (string?)null, (string?)null)
                ),
                "air", 5
            ),
            ((heldAfterFront, heldAfterSecondFront, heldAfterBack, heldAfterSecondBack), styles, built, peeled, World.BlockAt(cell).Code.Path, refunded));
    }

    // A post placed as a player places one: Post picked, planks pressed on the block below in the cell's south-west quarter,
    // so the post stands in that corner as `wall-cornerin-south`.
    private async Task<(BlockPos Cell, ITestPlayer Player, ItemSlot Slot, EnumHandHandling Handling)> PlacePost(int planks, bool deck = false)
    {
        BlockPos cell = World.Spawn.Offset(1, 2, 0);
        World.SetBlock("game:planks-aged-ud", cell.DownCopy());
        ITestPlayer player = await WallBuilder.JoinBuilder(World);
        player.Entity.WatchedAttributes.SetString("vssidingFraming", "post");
        if (deck) player.Entity.WatchedAttributes.SetString("vssidingDeck", "deck");
        await player.TeleportTo(cell.WestCopy());
        await player.GiveItem("game:plank-oak", planks);

        ItemSlot slot = player.Player.InventoryManager.ActiveHotbarSlot;
        var sel = new BlockSelection { Position = cell.DownCopy(), Face = BlockFacing.UP, HitPosition = new Vec3d(0.2, 1, 0.8) };
        var handling = EnumHandHandling.NotHandled;
        slot.Itemstack!.Collectible.OnHeldInteractStart(slot, player.Entity, sel, null, true, ref handling);
        return (cell, player, slot, handling);
    }

    private (string? Deck, string? Infill, string? Front, string? Back) DeckLayers(BlockPos cell)
    {
        var tree = new TreeAttribute();
        World.BlockEntityAt<BlockEntity>(cell)!.ToTreeAttributes(tree);
        string? Read(string key) => tree.GetString(key) is { Length: > 0 } value ? value : null;
        return (Read("deck"), Read("deckinfill"), Read("deckfront"), Read("deckback"));
    }
}
