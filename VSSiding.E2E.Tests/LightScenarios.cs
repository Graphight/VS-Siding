using System.Threading.Tasks;
using Atlas.Api;
using Atlas.XUnit;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Xunit;

namespace VSSiding.E2E.Tests;

[AtlasWorld(StrictBootDiagnostics = true)]
public class LightScenarios : AtlasScenarioBase
{
    [AtlasScenario(FreshWorld = true)]
    public async Task HostedTorch_Should_LightTheRoomThenGoDark_When_ItBurnsOut()
    {
        (BlockPos cell, BlockPos inside) = await HostLitTorch();

        Block burnedOut = World.Api.World.GetBlock(World.BlockAt(cell).CodeWithVariant("state", "burnedout"));
        World.Api.World.BlockAccessor.ExchangeBlock(burnedOut.BlockId, cell);

        await World.Until(() => BlockLight(inside) == 0, 3000);
        Assert.Equal(("oak", "clay", null, null, null), WallBuilder.GuestLayers(World, cell));
    }

    [AtlasScenario(FreshWorld = true)]
    public async Task HostedTorch_Should_LightTheRoomThenGoDark_When_ItIsBroken()
    {
        (BlockPos cell, BlockPos inside) = await HostLitTorch();

        World.Api.World.BlockAccessor.BreakBlock(cell, null);

        await World.Until(() => World.BlockAt(cell).Code.Path.StartsWith("wall"), 3000);
        await World.Until(() => BlockLight(inside) == 0, 3000);
        Assert.Equal(("oak", "clay", null, null, null), WallBuilder.Layers(World, cell));
    }

    // A sealed oak and clay wall with a lit torch hosted in its cell, lighting the room-side cell next to it.
    private async Task<(BlockPos Cell, BlockPos Inside)> HostLitTorch()
    {
        BlockPos cell = World.Spawn.Offset(1, 2, 0);
        BlockPos inside = cell.WestCopy();
        World.SetBlock("game:planks-aged-ud", cell.DownCopy());

        ITestPlayer player = await WallBuilder.JoinBuilder(World);
        await WallBuilder.Raise(World, player, cell, inside, "game:plank-oak", "game:clay-blue");
        Assert.Equal(0, BlockLight(inside));

        // A saw in the off hand layers the wall instead of hosting (SidingWallBlock.OnBlockInteractStart).
        player.Entity.LeftHandItemSlot.Itemstack = null;
        await player.GiveItem("game:torch-basic-lit-up", 1);
        var selection = new BlockSelection { Position = cell.Copy(), Face = BlockFacing.EAST, HitPosition = new Vec3d(0.5, 0.5, 0.5) };
        Assert.True(World.BlockAt(cell).OnBlockInteractStart(player.Entity.World, player.Player, selection));

        Assert.StartsWith("torch-basic-lit-", World.BlockAt(cell).Code.Path);
        Assert.Equal(("oak", "clay", null, null, null), WallBuilder.GuestLayers(World, cell));

        await World.Until(() => BlockLight(inside) > 0, 3000);
        return (cell, inside);
    }

    private int BlockLight(BlockPos pos) => World.Api.World.BlockAccessor.GetLightLevel(pos, EnumLightLevelType.OnlyBlockLight);
}
