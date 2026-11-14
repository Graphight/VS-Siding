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
