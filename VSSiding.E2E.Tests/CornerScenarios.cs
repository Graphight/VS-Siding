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

// A corner's two inner faces each take a finish of their own, at half a wall's cost (decision 0088).
// Every corner here is a filled wall turned at its north end, so it claims west and north, and its backs are east and south.
[AtlasWorld(StrictBootDiagnostics = true)]
public class CornerScenarios : AtlasScenarioBase
{
    private static readonly Vec3d Middle = new(0.5, 0.5, 0.5);
    private static readonly Vec3d NorthEnd = new(0, 0.5, 0.2);

    // Each inner face costs one of a finish's two planks and the front the full two; peeling every layer and then the
    // frame hands back what was paid.
    [AtlasScenario(FreshWorld = true)]
    public async Task Corner_Should_TakeAFinishOnEachInnerFaceForOnePlankAndRefundThemAll_When_PlanksAreBuiltAndPeeled()
    {
        BlockPos cell = World.Spawn.Offset(1, 2, 0);
        World.SetBlock("game:planks-aged-ud", cell.DownCopy());
        ITestPlayer player = await WallBuilder.JoinBuilder(World);
        await WallBuilder.Raise(World, player, cell, cell.WestCopy(), "game:plank-oak", "game:clay-blue");
        player.Entity.WatchedAttributes.SetString("vssidingFraming", "corner");
        await WallBuilder.Press(World, player, cell, "game:stick", BlockFacing.WEST, NorthEnd);

        // Away from the corner, so the planks it gives back stay on the ground to be counted.
        await player.TeleportTo(cell.EastCopy(6).Up());
        ItemSlot slot = player.Player.InventoryManager.ActiveHotbarSlot;
        async Task<int> Finish(BlockFacing face)
        {
            await WallBuilder.Press(World, player, cell, "game:plank-oak", face, Middle);
            return slot.Itemstack?.StackSize ?? 0;
        }

        (string?, string?, string?, string?) Layers()
        {
            var tree = new TreeAttribute();
            World.BlockEntityAt<BlockEntity>(cell)!.ToTreeAttributes(tree);
            string? Read(string key) => tree.GetString(key) is { Length: > 0 } value ? value : null;
            return (Read("infill"), Read("front"), Read("back"), Read("secondback"));
        }
        var held = (await Finish(BlockFacing.EAST), await Finish(BlockFacing.SOUTH), await Finish(BlockFacing.WEST));
        var built = (World.BlockAt(cell).Variant["layout"], Layers());

        // The server calls OnBlockBroken once per player break; BreakBlock would take the block with the last layer.
        (string?, string?, string?, string?) Peel()
        {
            World.BlockAt(cell).OnBlockBroken(player.Entity.World, cell, player.Player);
            return Layers();
        }
        var peeled = (Peel(), Peel(), Peel(), Peel());
        World.BlockAt(cell).OnBlockBroken(player.Entity.World, cell, player.Player);
        await World.Ticks(2);

        int refunded = World.Api.World.GetEntitiesAround(cell.ToVec3d().Add(0.5, 0.5, 0.5), 3, 3, entity => entity is EntityItem)
            .Cast<EntityItem>().Where(item => item.Itemstack.Collectible.Code.ToString() == "game:plank-oak").Sum(item => item.Itemstack.StackSize);

        Assert.Equal(
            (
                (3, 3, 2), ("cornerout", ("clay", "planks", "planks", "planks")),
                (
                    ("clay", (string?)null, "planks", "planks"),
                    ("clay", (string?)null, (string?)null, "planks"),
                    ("clay", (string?)null, (string?)null, (string?)null),
                    ((string?)null, (string?)null, (string?)null, (string?)null)
                ),
                "air", 6
            ),
            (held, built, peeled, World.BlockAt(cell).Code.Path, refunded));
    }
}
