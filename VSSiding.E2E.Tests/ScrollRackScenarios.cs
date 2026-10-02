using System.Linq;
using System.Threading.Tasks;
using Atlas.Api;
using Atlas.XUnit;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;
using Xunit;

namespace VSSiding.E2E.Tests;

// Two racks side by side share the slots between them (#82): one opens its left slots onto the other.
[AtlasWorld(StrictBootDiagnostics = true)]
public class ScrollRackScenarios : AtlasScenarioBase
{
    // Left slots, by index into the rack's slotsHitBoxes.
    private static readonly int[] LeftSlots = [2, 7];

    [AtlasScenario(FreshWorld = true)]
    public Task Racks_Should_Join_When_HostedFacingNorth() => AssertRacksJoin(BlockFacing.NORTH);

    [AtlasScenario(FreshWorld = true)]
    public Task Racks_Should_Join_When_HostedFacingSouth() => AssertRacksJoin(BlockFacing.SOUTH);

    // outward is the wall's open side, where the racks go and the player stands, at the seam between the two cells.
    private async Task AssertRacksJoin(BlockFacing outward)
    {
        BlockPos west = World.Spawn.Offset(0, 2, 0);
        BlockPos[] cells = [west, west.EastCopy()];
        ITestPlayer player = await WallBuilder.JoinBuilder(World);
        foreach (BlockPos cell in cells)
        {
            World.SetBlock("game:planks-aged-ud", cell.DownCopy());
            await WallBuilder.Raise(World, player, cell, cell.AddCopy(outward.Opposite), "game:plank-oak", "game:clay-blue");
        }

        // A saw in the off hand layers the wall instead of hosting (SidingWallBlock.OnBlockInteractStart).
        player.Entity.LeftHandItemSlot.Itemstack = null;
        BlockPos stand = cells[1].AddCopy(outward, 2);
        await player.TeleportTo(stand);
        player.Entity.Pos.SetPos(cells[1].X, stand.Y, stand.Z + 0.5);

        var hit = new Vec3d(0.5 + outward.Normali.X * 0.5, 0.5, 0.5 + outward.Normali.Z * 0.5);
        foreach (BlockPos cell in cells)
        {
            var rack = new ItemStack(World.Api.World.GetBlock(new AssetLocation("game:scrollrack")));
            rack.Attributes.SetString("type", "normal");
            rack.Attributes.SetString("material", "aged");
            player.Entity.RightHandItemSlot.Itemstack = rack;
            var selection = new BlockSelection { Position = cell.Copy(), Face = outward, HitPosition = hit };
            Assert.True(World.BlockAt(cell).OnBlockInteractStart(player.Entity.World, player.Player, selection));
            Assert.StartsWith("scrollrack", World.BlockAt(cell).Code.Path);
        }

        var racks = cells.Select(cell => World.BlockEntityAt<BlockEntityScrollRack>(cell)!).ToArray();
        Assert.Equal(1, racks.Count(r => LeftSlots.All(r.getOrCreateUsableSlots()!.Contains)));
    }
}
