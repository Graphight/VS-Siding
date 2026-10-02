using System.Threading.Tasks;
using Atlas.Api;
using Atlas.XUnit;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Xunit;

namespace VSSiding.E2E.Tests;

[AtlasWorld(StrictBootDiagnostics = true)]
public class DeckScenarios : AtlasScenarioBase
{
    [AtlasScenario(FreshWorld = true)]
    public async Task Deck_Should_TakeFloorboards_When_PlanksFinishItWithFloorPicked()
    {
        BlockPos cell = World.Spawn.Offset(1, 2, 0);
        World.SetBlock("game:planks-aged-ud", cell.DownCopy());
        ITestPlayer player = await WallBuilder.JoinBuilder(World);
        await WallBuilder.Raise(World, player, cell, cell.WestCopy(), "game:plank-oak", "game:clay-blue");
        ItemSlot slot = player.Player.InventoryManager.ActiveHotbarSlot;

        player.Entity.WatchedAttributes.SetString("vssidingDeck", "deck");
        await player.GiveItem("game:plank-oak", 2);
        var sideSel = new BlockSelection { Position = cell.Copy(), Face = BlockFacing.EAST, HitPosition = new Vec3d(1, 0.5, 0.5) };
        Assert.True(World.BlockAt(cell).OnBlockInteractStart(player.Entity.World, player.Player, sideSel));

        // The deck's box follows the block's own boxes (SidingWallBlock.IsDeckHit).
        var deckTopSel = new BlockSelection
        {
            Position = cell.Copy(), Face = BlockFacing.UP, HitPosition = new Vec3d(0.5, 1, 0.5),
            SelectionBoxIndex = World.BlockAt(cell).SelectionBoxes.Length,
        };
        await player.GiveItem("game:clay-blue", 4);
        Assert.True(World.BlockAt(cell).OnBlockInteractStart(player.Entity.World, player.Player, deckTopSel));

        player.Entity.WatchedAttributes.SetString("vssidingFraming", "floor");
        await player.GiveItem("game:plank-oak", 2);
        Assert.True(World.BlockAt(cell).OnBlockInteractStart(player.Entity.World, player.Player, deckTopSel));

        Assert.Null(slot.Itemstack);
        Assert.Equal(("oak", "clay", "planks", null), DeckLayers(cell));
    }

    private (string? Deck, string? Infill, string? Front, string? Back) DeckLayers(BlockPos cell)
    {
        var tree = new TreeAttribute();
        World.BlockEntityAt<BlockEntity>(cell)!.ToTreeAttributes(tree);
        string? Read(string key) => tree.GetString(key) is { Length: > 0 } value ? value : null;
        return (Read("deck"), Read("deckinfill"), Read("deckfront"), Read("deckback"));
    }
}
