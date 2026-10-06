using System.Threading.Tasks;
using Atlas.Api;
using Atlas.XUnit;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Xunit;

namespace VSSiding.E2E.Tests;

[AtlasWorld(StrictBootDiagnostics = true)]
public class EarthScenarios : AtlasScenarioBase
{
    // WallBuilder.Raise gives four of an infill and two of a finish; an earth layer costs one block.
    [AtlasScenario(FreshWorld = true)]
    public async Task Wall_Should_TakePackedAndRammedEarth_When_EachBlockClicksTheFrame()
    {
        BlockPos cell = World.Spawn.Offset(1, 2, 0);
        World.SetBlock("game:planks-aged-ud", cell.DownCopy());
        ITestPlayer player = await WallBuilder.JoinBuilder(World);
        await WallBuilder.Raise(World, player, cell, cell.WestCopy(), "game:plank-oak", null);
        ItemSlot slot = player.Player.InventoryManager.ActiveHotbarSlot;
        var outwardSel = new BlockSelection { Position = cell.Copy(), Face = BlockFacing.EAST, HitPosition = new Vec3d(0.5, 0.5, 0.5) };

        await player.GiveItem("game:packeddirt", 1);
        Assert.True(World.BlockAt(cell).OnBlockInteractStart(player.Entity.World, player.Player, outwardSel));
        Assert.Null(slot.Itemstack);

        await player.GiveItem("game:rammed-light-thinlight", 1);
        Assert.True(World.BlockAt(cell).OnBlockInteractStart(player.Entity.World, player.Player, outwardSel));
        Assert.Null(slot.Itemstack);

        Assert.Equal(("oak", "packeddirt", null, null, "rammed-thinlight"), WallBuilder.Layers(World, cell));
    }
}
