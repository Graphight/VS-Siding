using System.Linq;
using System.Threading.Tasks;
using Atlas.Api;
using Atlas.XUnit;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Xunit;

namespace VSSiding.E2E.Tests;

// Vanilla wallpaper is a finish on a wall's room side, not a decor (decision 0103).
// The wall hugs the west face of its cell, so west is its claimed front and east its room side.
[AtlasWorld(StrictBootDiagnostics = true)]
public class WallpaperScenarios : AtlasScenarioBase
{
    private static readonly Vec3d Middle = new(0.5, 0.5, 0.5);

    [AtlasScenario(RollbackWorld = true, StrictIsolation = true)]
    public async Task Wall_Should_TakeWallpaperAsItsBack_When_WallpaperIsPressedOnItsRoomSide()
    {
        (BlockPos cell, ITestPlayer player) = await RaiseWall();

        await WallBuilder.Press(World, player, cell, "game:wallpaper-blue4", BlockFacing.EAST, Middle, quantity: 3);

        Assert.Equal((("oak", "clay", null, null, "wallpaper-blue4"), 2), (WallBuilder.Layers(World, cell), Held(player)));
    }

    [AtlasScenario(RollbackWorld = true, StrictIsolation = true)]
    public async Task Wall_Should_DropOneWallpaper_When_ItsWallpaperIsBrokenOff()
    {
        (BlockPos cell, ITestPlayer player) = await RaiseWall();
        await WallBuilder.Press(World, player, cell, "game:wallpaper-blue4", BlockFacing.EAST, Middle);
        await player.TeleportTo(cell.EastCopy(6).Up());

        World.BlockAt(cell).OnBlockBroken(player.Entity.World, cell, player.Player);
        await World.Ticks(2);

        Assert.Equal((("oak", "clay", null, null, null), 1), (WallBuilder.Layers(World, cell), Dropped(cell, "game:wallpaper-blue4")));
    }

    [AtlasScenario(RollbackWorld = true, StrictIsolation = true)]
    public async Task Wall_Should_RefuseWallpaperDecor_When_ItIsPlacedOnASideFace()
    {
        (BlockPos cell, ITestPlayer player) = await RaiseWall();
        Block paper = World.Api.World.GetBlock(new AssetLocation("game:wallpaper-blue4"));

        // A sneak-click runs vanilla placement before the wall's own click: BlockBehaviorDecor.TryPlaceBlock.
        string failure = "";
        bool placed = paper.TryPlaceBlock(World.Api.World, player.Player, new ItemStack(paper),
            new BlockSelection { Position = cell.Copy(), Face = BlockFacing.WEST, HitPosition = new Vec3d(0, 0.5, 0.5) }, ref failure);

        Assert.Equal((false, "decorrequiressolid", null), (placed, failure, World.Api.World.BlockAccessor.GetDecor(cell, new DecorBits(BlockFacing.WEST))?.Code?.ToString()));
    }

    // The table answers for the hosted cell, through GuestSealingPatches.CanAttachPostfix.
    [AtlasScenario(RollbackWorld = true, StrictIsolation = true)]
    public async Task HostedWall_Should_RefuseWallpaperDecor_When_ItIsPlacedOnItsClaimedFace()
    {
        (BlockPos cell, ITestPlayer player) = await RaiseWall();
        player.Entity.LeftHandItemSlot.Itemstack = null;
        player.Player.InventoryManager.ActiveHotbarSlot.Itemstack = new ItemStack(World.Api.World.GetBlock(new AssetLocation("game:table-normal")));
        World.BlockAt(cell).OnBlockInteractStart(player.Entity.World, player.Player,
            new BlockSelection { Position = cell.Copy(), Face = BlockFacing.EAST, HitPosition = new Vec3d(0.5, 0.5, 0.5) });
        Block paper = World.Api.World.GetBlock(new AssetLocation("game:wallpaper-blue4"));

        string failure = "";
        bool placed = paper.TryPlaceBlock(World.Api.World, player.Player, new ItemStack(paper),
            new BlockSelection { Position = cell.Copy(), Face = BlockFacing.WEST, HitPosition = new Vec3d(0, 0.5, 0.5) }, ref failure);

        Assert.Equal(("game:table-normal", false, null), (World.BlockAt(cell).Code.ToString(), placed, World.Api.World.BlockAccessor.GetDecor(cell, new DecorBits(BlockFacing.WEST))?.Code?.ToString()));
    }

    private async Task<(BlockPos Cell, ITestPlayer Player)> RaiseWall()
    {
        BlockPos cell = World.Spawn.Offset(1, 2, 0);
        World.SetBlock("game:planks-aged-ud", cell.DownCopy());
        ITestPlayer player = await WallBuilder.JoinBuilder(World);
        player.Entity.WatchedAttributes.SetString("vssidingFraming", "wall");
        await WallBuilder.Raise(World, player, cell, cell.WestCopy(), "game:plank-oak", "game:clay-blue");
        return (cell, player);
    }

    private int Dropped(BlockPos cell, string code)
        => World.Api.World.GetEntitiesAround(cell.ToVec3d().Add(0.5, 0.5, 0.5), 3, 3, entity => entity is EntityItem)
            .Cast<EntityItem>().Where(item => item.Itemstack.Collectible.Code.ToString() == code).Sum(item => item.Itemstack.StackSize);

    private static int Held(ITestPlayer player)
        => player.Player.InventoryManager.ActiveHotbarSlot.Itemstack?.StackSize ?? 0;
}
