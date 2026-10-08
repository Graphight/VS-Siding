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
}
