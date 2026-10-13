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
    private static readonly (string?, string?, string?, string?, string?) Expected = ("oak", "clay", null, null, null);

    [AtlasScenario(FreshWorld = true)]
    public async Task HostedTorch_Should_LightTheRoomThenGoDark_When_ItBurnsOut()
    {
        (BlockPos cell, BlockPos inside) = await HostLitTorch();

        Block burnedOut = World.Api.World.GetBlock(World.BlockAt(cell).CodeWithVariant("state", "burnedout"));
        World.Api.World.BlockAccessor.ExchangeBlock(burnedOut.BlockId, cell);

        await World.Until(() => BlockLight(inside) == 0, 100);
        Assert.Equal(Expected, WallBuilder.GuestLayers(World, cell));
    }

    [AtlasScenario(FreshWorld = true)]
    public async Task HostedTorch_Should_LightTheRoomThenGoDark_When_ItIsBroken()
    {
        (BlockPos cell, BlockPos inside) = await HostLitTorch();

        World.Api.World.BlockAccessor.BreakBlock(cell, null);

        await World.Until(() => World.BlockAt(cell).Code.Path.StartsWith("wall"), 100);
        await World.Until(() => BlockLight(inside) == 0, 100);
        Assert.Equal(Expected, WallBuilder.Layers(World, cell));
    }

    [AtlasScenario(FreshWorld = true)]
    public async Task RoomLight_Should_StopAtAFrameOnceItIsFilled_And_PassAgainOnceItIsEmptied()
    {
        (BlockPos inside, ITestPlayer player) = await WallBuilder.BuildRoom(World, "game:clay-blue", [BlockFacing.NORTH, BlockFacing.SOUTH, BlockFacing.WEST]);
        BlockPos cell = inside.EastCopy();
        BlockPos outside = cell.EastCopy();

        World.SetBlock("game:lantern-large-up", inside);
        await World.Until(() => BlockLight(outside) > 0, 100);

        await player.GiveItem("game:clay-blue", 4);
        var selection = new BlockSelection { Position = cell.Copy(), Face = BlockFacing.EAST, HitPosition = new Vec3d(0.5, 0.5, 0.5) };
        Assert.True(World.BlockAt(cell).OnBlockInteractStart(player.Entity.World, player.Player, selection));
        Assert.Equal(Expected, WallBuilder.Layers(World, cell));
        await World.Until(() => BlockLight(outside) == 0, 100);

        World.Api.World.BlockAccessor.BreakBlock(cell, player.Player);
        Assert.Equal(("oak", null, null, null, null), WallBuilder.Layers(World, cell));
        await World.Until(() => BlockLight(outside) > 0, 100);
    }

    [AtlasScenario(FreshWorld = true)]
    public async Task HostedLantern_Should_LightASealedRoomButLetNoSunlightIn()
    {
        (BlockPos inside, ITestPlayer player) = await WallBuilder.BuildRoom(World, "game:clay-blue", BlockFacing.HORIZONTALS);
        BlockPos cell = inside.EastCopy();

        // BuildRoom's panels hug the room. This wall is raised again from inside, so its open side, where a hosted block sits, faces the room.
        World.SetBlock("game:air", cell);
        await WallBuilder.Raise(World, player, cell, cell.EastCopy(), "game:plank-oak", "game:clay-blue");

        // A standing lantern already in the cell, with no player to click it in.
        World.SetBlock("game:lantern-large-up", cell);
        Assert.Equal(Expected, WallBuilder.GuestLayers(World, cell));

        await World.Until(() => BlockLight(inside) > 0, 100);
        Assert.Equal(0, World.Api.World.BlockAccessor.GetLightLevel(inside, EnumLightLevelType.OnlySunLight));
    }

    [AtlasScenario(FreshWorld = true)]
    public async Task HostedTorch_Should_LightBothSides_When_TheWallIsBareWattle()
        => await HostLitTorch("game:stick", "wattle", passesLight: true);

    [AtlasScenario(FreshWorld = true)]
    public async Task BareWattle_Should_PassRoomLightUntilAFaceIsFinished()
    {
        (BlockPos inside, ITestPlayer player) = await WallBuilder.BuildRoom(World, "game:clay-blue", [BlockFacing.NORTH, BlockFacing.SOUTH, BlockFacing.WEST]);
        BlockPos cell = inside.EastCopy();
        BlockPos outside = cell.EastCopy();

        World.SetBlock("game:air", cell);
        await WallBuilder.Raise(World, player, cell, inside, "game:plank-oak", "game:stick");
        World.SetBlock("game:lantern-large-up", inside);

        await World.Until(() => BlockLight(outside) > 0, 100);

        var selection = new BlockSelection { Position = cell.Copy(), Face = BlockFacing.EAST, HitPosition = new Vec3d(0.5, 0.5, 0.5) };
        await player.GiveItem("game:daubraw-browngolden", 2);
        Assert.True(World.BlockAt(cell).OnBlockInteractStart(player.Entity.World, player.Player, selection));
        await World.Until(() => BlockLight(outside) == 0, 100);

        World.Api.World.BlockAccessor.BreakBlock(cell, player.Player);
        await World.Until(() => BlockLight(outside) > 0, 100);
        Assert.Equal(("oak", "wattle", null, null, null), WallBuilder.Layers(World, cell));
    }

    [AtlasScenario(FreshWorld = true)]
    public async Task HostedTorch_Should_StayOnItsSide_When_TheWallRunsEastToWest()
        => await HostLitTorch(open: BlockFacing.NORTH);

    // The corner claims its north and west faces, so its open side is the south-east.
    [AtlasScenario(FreshWorld = true)]
    public async Task HostedTorch_Should_LightNeitherCellBehindACorner()
    {
        BlockPos cell = World.Spawn.Offset(1, 2, 0);
        World.SetBlock("game:planks-aged-ud", cell.DownCopy());
        ITestPlayer player = await WallBuilder.JoinBuilder(World);
        await WallBuilder.Raise(World, player, cell, cell.WestCopy(), "game:plank-oak", "game:clay-blue");
        player.Entity.WatchedAttributes.SetString("vssidingFraming", "corner");
        await WallBuilder.Press(World, player, cell, "game:stick", BlockFacing.WEST, new Vec3d(0, 0.5, 0.2));
        Assert.Equal("cornerout", World.BlockAt(cell).Variant["layout"]);

        player.Entity.LeftHandItemSlot.Itemstack = null;
        await WallBuilder.Press(World, player, cell, "game:torch-basic-lit-up", BlockFacing.EAST, new Vec3d(1, 0.5, 0.5), quantity: 1);
        Assert.StartsWith("torch-basic-lit-", World.BlockAt(cell).Code.Path);

        await World.Until(() => BlockLight(cell.EastCopy()) > 0, 100);
        Assert.Equal((0, 0), (BlockLight(cell.NorthCopy()), BlockLight(cell.WestCopy())));
    }

    // A corner's two outer faces are separate layers, so the one finish a wall brings leaves it unfinished (decision 0074).
    [AtlasScenario(FreshWorld = true)]
    public async Task DaubedWattle_Should_PassRoomLightAgain_When_ItTurnsIntoACorner()
    {
        (BlockPos inside, ITestPlayer player) = await WallBuilder.BuildRoom(World, "game:clay-blue", [BlockFacing.NORTH, BlockFacing.SOUTH, BlockFacing.WEST]);
        BlockPos cell = inside.EastCopy();
        BlockPos outside = cell.EastCopy();

        World.SetBlock("game:air", cell);
        await WallBuilder.Raise(World, player, cell, inside, "game:plank-oak", "game:stick");
        await WallBuilder.Press(World, player, cell, "game:daubraw-browngolden", BlockFacing.WEST, new Vec3d(0, 0.5, 0.2), quantity: 2);
        World.SetBlock("game:lantern-large-up", inside);
        await World.Ticks(20);
        Assert.Equal((("oak", "wattle", "daub", null, null), 0), (WallBuilder.Layers(World, cell), BlockLight(outside)));

        player.Entity.WatchedAttributes.SetString("vssidingFraming", "corner");
        await WallBuilder.Press(World, player, cell, "game:plank-oak", BlockFacing.WEST, new Vec3d(0, 0.5, 0.2));

        Assert.Equal(("cornerout", ("oak", "wattle", "daub", null, null)), (World.BlockAt(cell).Variant["layout"], WallBuilder.Layers(World, cell)));
        await World.Until(() => BlockLight(outside) > 0, 100);
    }

    // A sealed oak wall, its panel on the outdoor face, with a lit torch hosted on its open side and lighting the room-side cell next to it.
    private async Task<(BlockPos Cell, BlockPos Inside)> HostLitTorch(string infillItem = "game:clay-blue", string infillKey = "clay", bool passesLight = false, BlockFacing? open = null)
    {
        open ??= BlockFacing.WEST;
        BlockPos cell = World.Spawn.Offset(1, 2, 0);
        BlockPos inside = cell.AddCopy(open);
        BlockPos outside = cell.AddCopy(open.Opposite);
        World.SetBlock("game:planks-aged-ud", cell.DownCopy());

        // Raised from the room, as a builder standing in it does: the wall claims the face away from the room.
        ITestPlayer player = await WallBuilder.JoinBuilder(World);
        await WallBuilder.Raise(World, player, cell, outside, "game:plank-oak", infillItem);
        Assert.Equal(0, BlockLight(inside));

        // A saw in the off hand layers the wall instead of hosting (SidingWallBlock.OnBlockInteractStart).
        player.Entity.LeftHandItemSlot.Itemstack = null;
        await player.GiveItem("game:torch-basic-lit-up", 1);
        var selection = new BlockSelection { Position = cell.Copy(), Face = open, HitPosition = new Vec3d(0.5, 0.5, 0.5) };
        Assert.True(World.BlockAt(cell).OnBlockInteractStart(player.Entity.World, player.Player, selection));

        Assert.StartsWith("torch-basic-lit-", World.BlockAt(cell).Code.Path);
        Assert.Equal(("oak", infillKey, null, null, null), WallBuilder.GuestLayers(World, cell));

        await World.Until(() => BlockLight(inside) > 0, 100);
        // The light stays on the open side of an opaque wall's panel (decision 0072); bare wattle passes it.
        Assert.Equal(passesLight, BlockLight(outside) > 0);
        return (cell, inside);
    }

    private int BlockLight(BlockPos pos) => World.Api.World.BlockAccessor.GetLightLevel(pos, EnumLightLevelType.OnlyBlockLight);
}
