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
        BlockPos cell = World.Spawn.Offset(1, 2, 0);
        World.SetBlock("game:planks-aged-ud", cell.DownCopy());
        ITestPlayer player = await WallBuilder.JoinBuilder(World);
        player.Entity.WatchedAttributes.SetString("vssidingFraming", "post");
        await player.TeleportTo(cell.WestCopy());
        await player.GiveItem("game:plank-oak", 3);

        ItemSlot slot = player.Player.InventoryManager.ActiveHotbarSlot;
        var sel = new BlockSelection { Position = cell.DownCopy(), Face = BlockFacing.UP, HitPosition = new Vec3d(0.2, 1, 0.8) };
        var handling = EnumHandHandling.NotHandled;
        slot.Itemstack!.Collectible.OnHeldInteractStart(slot, player.Entity, sel, null, true, ref handling);

        int planksLeft = slot.Itemstack?.StackSize ?? 0;
        await WallBuilder.Press(World, player, cell, "game:clay-blue", BlockFacing.WEST, new Vec3d(0.5, 0.5, 0.5));

        Assert.Equal(
            ("wall-cornerin-south", EnumHandHandling.PreventDefault, 2, ("oak", null, null, null, null)),
            (World.BlockAt(cell).Code.Path, handling, planksLeft, WallBuilder.Layers(World, cell)));
    }

    // Each finish costs a quarter of its two planks, and peeling both finishes and then the frame hands back what was paid.
    [AtlasScenario(FreshWorld = true)]
    public async Task Post_Should_TakeAFinishOnEachFaceForOnePlankAndRefundThemAll_When_PlanksAreBuiltAndPeeled()
    {
        BlockPos cell = World.Spawn.Offset(1, 2, 0);
        World.SetBlock("game:planks-aged-ud", cell.DownCopy());
        ITestPlayer player = await WallBuilder.JoinBuilder(World);
        player.Entity.WatchedAttributes.SetString("vssidingFraming", "post");
        await player.TeleportTo(cell.WestCopy());
        await player.GiveItem("game:plank-oak", 1);
        ItemSlot slot = player.Player.InventoryManager.ActiveHotbarSlot;
        var sel = new BlockSelection { Position = cell.DownCopy(), Face = BlockFacing.UP, HitPosition = new Vec3d(0.2, 1, 0.8) };
        var handling = EnumHandHandling.NotHandled;
        slot.Itemstack!.Collectible.OnHeldInteractStart(slot, player.Entity, sel, null, true, ref handling);

        // Away from the post, so the planks it gives back stay on the ground to be counted.
        await player.TeleportTo(cell.EastCopy(6).Up());
        async Task<int> Finish(BlockFacing face, string style)
        {
            player.Entity.WatchedAttributes.SetString("vssidingBoards", style);
            await WallBuilder.Press(World, player, cell, "game:plank-oak", face, new Vec3d(0.5, 0.5, 0.5));
            return slot.Itemstack?.StackSize ?? 0;
        }

        // A post saying "south" has its front on the north face and its second front on the east one.
        int heldAfterFront = await Finish(BlockFacing.NORTH, "hboards");
        int heldAfterSecondFront = await Finish(BlockFacing.EAST, "boards");
        var tree = new TreeAttribute();
        World.BlockEntityAt<BlockEntity>(cell)!.ToTreeAttributes(tree);
        var styles = (tree.GetString("frontstyle"), tree.GetString("secondfrontstyle"));
        var built = WallBuilder.Layers(World, cell);

        // The server calls OnBlockBroken once per player break; BreakBlock would take the block with the last layer.
        (string?, string?, string?, string?, string?) Peel()
        {
            World.BlockAt(cell).OnBlockBroken(player.Entity.World, cell, player.Player);
            return WallBuilder.Layers(World, cell);
        }
        var afterFront = Peel();
        var afterSecondFront = Peel();
        World.BlockAt(cell).OnBlockBroken(player.Entity.World, cell, player.Player);
        await World.Ticks(2);

        int refunded = World.Api.World.GetEntitiesAround(cell.ToVec3d().Add(0.5, 0.5, 0.5), 3, 3, entity => entity is EntityItem)
            .Cast<EntityItem>().Where(item => item.Itemstack.Collectible.Code.ToString() == "game:plank-oak").Sum(item => item.Itemstack.StackSize);

        Assert.Equal(
            (
                (3, 3), ("hboards", "boards"),
                ("oak", null, "planks", "planks", null),
                ("oak", null, null, "planks", null), ("oak", null, null, null, null),
                "air", 3
            ),
            ((heldAfterFront, heldAfterSecondFront), styles, built, afterFront, afterSecondFront, World.BlockAt(cell).Code.Path, refunded));
    }
}
