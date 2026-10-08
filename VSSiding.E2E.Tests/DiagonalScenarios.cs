using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Atlas.Api;
using Atlas.XUnit;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Xunit;

namespace VSSiding.E2E.Tests;

// A diagonal claims its side and the face counter-clockwise from it (decision 0066), and takes no deck.
[AtlasWorld(StrictBootDiagnostics = true)]
public class DiagonalScenarios : AtlasScenarioBase
{
    private static readonly Dictionary<string, string> CounterClockwise = new()
    {
        ["west"] = "north", ["south"] = "west", ["east"] = "south", ["north"] = "east",
    };

    [AtlasScenario(FreshWorld = true)]
    public async Task Wall_Should_RetainOnItsTwoClaimedFaces_When_FramedAndFilledAsADiagonal()
    {
        (BlockPos cell, _) = await RaiseFrame("diagonal", deck: false, infill: "game:clay-blue");

        Block wall = World.BlockAt(cell);
        string side = wall.Variant["side"];
        string claimed = string.Join(",", new[] { side, CounterClockwise[side] }.OrderBy(face => face));
        string retaining = string.Join(",", BlockFacing.HORIZONTALS
            .Where(face => wall.GetRetention(cell, face, EnumRetentionType.Heat) != 0)
            .Select(face => face.Code)
            .OrderBy(face => face));

        Assert.Equal((("diagonal", claimed), ("oak", "clay", null, null, null)), ((wall.Variant["layout"], retaining), WallBuilder.Layers(World, cell)));
    }

    // One charge of planks is all the builder is given, so a deck would leave the frame unpaid for.
    [AtlasScenario(FreshWorld = true)]
    public async Task Frame_Should_StoreNoDeck_When_DeckIsLitAndADiagonalIsPicked()
    {
        (BlockPos cell, _) = await RaiseFrame("diagonal", deck: true);

        Assert.Equal((("oak", null, null, null, null), null), (WallBuilder.Layers(World, cell), Deck(cell)));
    }

    // The in-place upgrade keeps the block entity, and a diagonal has no box for a deck it carries over.
    [AtlasScenario(FreshWorld = true)]
    public async Task Frame_Should_StayAWall_When_ADeckedFrameIsClickedWithADiagonalPicked()
    {
        (BlockPos cell, ITestPlayer player) = await RaiseFrame("wall", deck: true, framingCost: 4);

        player.Entity.WatchedAttributes.SetString("vssidingFraming", "diagonal");
        await player.GiveItem("game:plank-oak", 2);
        Block wall = World.BlockAt(cell);
        var frontSel = new BlockSelection { Position = cell.Copy(), Face = BlockFacing.FromCode(wall.Variant["side"]), HitPosition = new Vec3d(0.5, 0.5, 0.5) };
        wall.OnBlockInteractStart(player.Entity.World, player.Player, frontSel);

        Assert.Equal(("wall", "oak"), (World.BlockAt(cell).Variant["layout"], Deck(cell)));
    }

    private string? Deck(BlockPos cell)
    {
        var tree = new TreeAttribute();
        World.BlockEntityAt<BlockEntity>(cell)!.ToTreeAttributes(tree);
        return tree.GetString("deck") is { Length: > 0 } deck ? deck : null;
    }

    private async Task<(BlockPos Cell, ITestPlayer Player)> RaiseFrame(string layout, bool deck, string? infill = null, int framingCost = 2)
    {
        BlockPos cell = World.Spawn.Offset(1, 2, 0);
        World.SetBlock("game:planks-aged-ud", cell.DownCopy());
        ITestPlayer player = await WallBuilder.JoinBuilder(World);
        player.Entity.WatchedAttributes.SetString("vssidingFraming", layout);
        if (deck) player.Entity.WatchedAttributes.SetString("vssidingDeck", "deck");
        await WallBuilder.Raise(World, player, cell, cell.WestCopy(), "game:plank-oak", infill, framingCost: framingCost);
        return (cell, player);
    }
}
