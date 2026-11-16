using System.Linq;
using System.Threading.Tasks;
using Atlas.Api;
using Atlas.XUnit;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Xunit;

namespace VSSiding.E2E.Tests;

// A wooden trapdoor fills a frame as a vent: the cell swaps to the vent block while it holds one, still seals, and the trapdoor comes back when peeled.
[AtlasWorld(StrictBootDiagnostics = true)]
public class VentScenarios : AtlasScenarioBase
{
    private const string Trapdoor = "game:trapdoor-solid-oak-1";

    [AtlasScenario(RollbackWorld = true, StrictIsolation = true)]
    public async Task Wall_Should_BeAVentThatSealsAndGivesTheTrapdoorBack_When_ATrapdoorFillsAFrame()
    {
        BlockPos cell = World.Spawn.Offset(1, 2, 0);
        World.SetBlock("game:planks-aged-ud", cell.DownCopy());
        ITestPlayer player = await WallBuilder.JoinBuilder(World);
        await WallBuilder.Raise(World, player, cell, cell.WestCopy(), "game:plank-oak", null);

        await WallBuilder.Press(World, player, cell, Trapdoor, BlockFacing.EAST, new Vec3d(1, 0.5, 0.5), quantity: 1);
        Block vent = World.BlockAt(cell);
        var filled = (vent.FirstCodePart(), WallBuilder.Layers(World, cell).Infill, BlockFacing.HORIZONTALS.Any(face => vent.GetRetention(cell, face, EnumRetentionType.Heat) != 0));
        var drops = vent.GetDrops(player.Entity.World, cell, player.Player).Select(stack => stack.Collectible.Code.ToString()).Contains(Trapdoor);

        vent.OnBlockBroken(player.Entity.World, cell, player.Player);

        Assert.Equal((("wallvent", "trapdoor-solid-oak-1", true), true, "wall", null),
            (filled, drops, World.BlockAt(cell).FirstCodePart(), WallBuilder.Layers(World, cell).Infill));
    }

    // A trapdoor in the deck vents the whole cell, and peeling it puts the cell back on the plain wall.
    [AtlasScenario(RollbackWorld = true, StrictIsolation = true)]
    public async Task Wall_Should_BeAVentUntilPeeled_When_ATrapdoorFillsItsDeck()
    {
        BlockPos cell = World.Spawn.Offset(1, 2, 0);
        World.SetBlock("game:planks-aged-ud", cell.DownCopy());
        ITestPlayer player = await WallBuilder.JoinBuilder(World);
        await WallBuilder.Raise(World, player, cell, cell.WestCopy(), "game:plank-oak", "game:clay-blue");
        player.Entity.WatchedAttributes.SetString("vssidingDeck", "deck");
        await WallBuilder.Press(World, player, cell, "game:plank-oak", BlockFacing.EAST, new Vec3d(1, 0.5, 0.5), quantity: 2);
        player.Entity.WatchedAttributes.SetString("vssidingDeck", "");
        await WallBuilder.Press(World, player, cell, Trapdoor, BlockFacing.UP, new Vec3d(0.5, 1, 0.5), quantity: 1, onDeck: true);
        var vented = (World.BlockAt(cell).FirstCodePart(), WallBuilder.DeckLayers(World, cell).Infill);

        World.BlockAt(cell).OnBlockBroken(player.Entity.World, cell, player.Player);

        Assert.Equal((("wallvent", "trapdoor-solid-oak-1"), "wall", "clay", null),
            (vented, World.BlockAt(cell).FirstCodePart(), WallBuilder.Layers(World, cell).Infill, WallBuilder.DeckLayers(World, cell).Infill));
    }

    // A bare frame's upgrade exchanges straight to its target with no infill change to re-derive the vent, so the target
    // keeps the block's own first code part: a frame under a trapdoor deck becomes a vented diagonal.
    [AtlasScenario(RollbackWorld = true, StrictIsolation = true)]
    public async Task Frame_Should_StayAVent_When_ItsTrapdoorDeckedFrameBecomesADiagonal()
    {
        BlockPos cell = World.Spawn.Offset(1, 2, 0);
        World.SetBlock("game:planks-aged-ud", cell.DownCopy());
        ITestPlayer player = await WallBuilder.JoinBuilder(World);
        player.Entity.WatchedAttributes.SetString("vssidingDeck", "deck");
        await WallBuilder.Raise(World, player, cell, cell.WestCopy(), "game:plank-oak", null, framingCost: 4);
        player.Entity.WatchedAttributes.SetString("vssidingDeck", "");
        await WallBuilder.Press(World, player, cell, Trapdoor, BlockFacing.UP, new Vec3d(0.5, 1, 0.5), quantity: 1, onDeck: true);

        player.Entity.WatchedAttributes.SetString("vssidingFraming", "diagonal");
        await WallBuilder.Press(World, player, cell, "game:plank-oak", BlockFacing.FromCode(World.BlockAt(cell).Variant["side"]), new Vec3d(0.5, 0.5, 0.5), quantity: 2);

        Assert.Equal(("wallvent", "diagonal", "trapdoor-solid-oak-1"), (World.BlockAt(cell).FirstCodePart(), World.BlockAt(cell).Variant["layout"], WallBuilder.DeckLayers(World, cell).Infill));
    }

    [AtlasScenario(RollbackWorld = true, StrictIsolation = true)]
    public async Task Floor_Should_BeAVentThatSeals_When_ATrapdoorFillsItsTop()
    {
        BlockPos cell = World.Spawn.Offset(1, 2, 0);
        World.SetBlock("game:planks-aged-ud", cell.DownCopy());
        ITestPlayer player = await WallBuilder.JoinBuilder(World);
        await player.TeleportTo(cell.WestCopy());
        player.Entity.WatchedAttributes.SetString("vssidingFraming", "floor");
        await player.GiveItem("game:plank-oak", 2);
        WallBuilder.ClickTop(player, cell.DownCopy());

        await WallBuilder.Press(World, player, cell, Trapdoor, BlockFacing.UP, new Vec3d(0.5, 1, 0.5), quantity: 1);
        Block vent = World.BlockAt(cell);

        Assert.Equal(("floorvent", true), (vent.FirstCodePart(), vent.GetRetention(cell, BlockFacing.UP, EnumRetentionType.Heat) != 0));
    }
}
