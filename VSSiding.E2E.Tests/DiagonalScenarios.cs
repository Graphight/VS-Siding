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
        BlockPos cell = await RaiseDiagonal(deck: false, infill: "game:clay-blue");

        Block wall = World.BlockAt(cell);
        string side = wall.Variant["side"];
        string claimed = string.Join(",", new[] { side, CounterClockwise[side] }.OrderBy(face => face));
        string retaining = string.Join(",", BlockFacing.HORIZONTALS
            .Where(face => wall.GetRetention(cell, face, EnumRetentionType.Heat) != 0)
            .Select(face => face.Code)
            .OrderBy(face => face));

        Assert.Equal((("diagonal", claimed), ("oak", "clay", null, null, null)), ((wall.Variant["layout"], retaining), WallBuilder.Layers(World, cell)));
    }

    [AtlasScenario(FreshWorld = true)]
    public async Task Frame_Should_StoreNoDeck_When_DeckIsLitAndADiagonalIsPicked()
    {
        BlockPos cell = await RaiseDiagonal(deck: true, infill: null);

        var tree = new TreeAttribute();
        World.BlockEntityAt<BlockEntity>(cell)!.ToTreeAttributes(tree);

        Assert.Equal((("oak", null, null, null, null), null), (WallBuilder.Layers(World, cell), tree.GetString("deck")));
    }

    private async Task<BlockPos> RaiseDiagonal(bool deck, string? infill)
    {
        BlockPos cell = World.Spawn.Offset(1, 2, 0);
        World.SetBlock("game:planks-aged-ud", cell.DownCopy());
        ITestPlayer player = await WallBuilder.JoinBuilder(World);
        player.Entity.WatchedAttributes.SetString("vssidingFraming", "diagonal");
        if (deck) player.Entity.WatchedAttributes.SetString("vssidingDeck", "deck");
        await WallBuilder.Raise(World, player, cell, cell.WestCopy(), "game:plank-oak", infill);
        return cell;
    }
}
