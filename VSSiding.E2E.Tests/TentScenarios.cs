using System.Threading.Tasks;
using Atlas.Api;
using Atlas.XUnit;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;
using Xunit;

namespace VSSiding.E2E.Tests;

[AtlasWorld(StrictBootDiagnostics = true)]
public class TentScenarios : AtlasScenarioBase
{
    [AtlasScenario(FreshWorld = true)]
    public async Task Wall_Should_StandOnItsBoneFrame_When_FireBurnsOffItsPelt()
    {
        (BlockPos cell, ITestPlayer player) = await RaiseBoneFrame();
        await Layer(player, cell, "game:hide-pelt-small");

        Assert.Equal(("bone", "pelt-small", null, null, null), WallBuilder.Layers(World, cell));
        Block wall = World.BlockAt(cell);

        BlockPos firePos = cell.UpCopy();
        World.SetBlock("game:fire", firePos);
        var burning = World.BlockEntityAt<BlockEntity>(firePos)!.GetBehavior<BEBehaviorBurning>();
        burning.OnFirePlaced(firePos, cell, null);
        Assert.True(burning.IsBurning);

        await World.Until(() => !burning.IsBurning, 3000);

        Assert.Equal(wall, World.BlockAt(cell));
        Assert.Equal(("bone", null, null, null, null), WallBuilder.Layers(World, cell));
    }

    [AtlasScenario(FreshWorld = true)]
    public async Task Wall_Should_TakeCloth_When_BlueClothClicksTheBoneFrame()
    {
        (BlockPos cell, ITestPlayer player) = await RaiseBoneFrame();
        await Layer(player, cell, "game:cloth-blue");

        Assert.Equal(("bone", "cloth-blue", null, null, null), WallBuilder.Layers(World, cell));
    }

    private async Task<(BlockPos Cell, ITestPlayer Player)> RaiseBoneFrame()
    {
        BlockPos cell = World.Spawn.Offset(1, 2, 0);
        World.SetBlock("game:planks-aged-ud", cell.DownCopy());
        ITestPlayer player = await WallBuilder.JoinBuilder(World);
        await WallBuilder.Raise(World, player, cell, cell.WestCopy(), "game:bone", null, framingCost: 2);
        return (cell, player);
    }

    private async Task Layer(ITestPlayer player, BlockPos cell, string item)
    {
        await player.GiveItem(item, 1);
        var outwardSel = new BlockSelection { Position = cell.Copy(), Face = BlockFacing.EAST, HitPosition = new Vec3d(0.5, 0.5, 0.5) };
        Assert.True(World.BlockAt(cell).OnBlockInteractStart(player.Entity.World, player.Player, outwardSel));
        Assert.Null(player.Player.InventoryManager.ActiveHotbarSlot.Itemstack);
    }
}
