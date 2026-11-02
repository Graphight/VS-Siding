using System.Threading.Tasks;
using Atlas.Api;
using Atlas.XUnit;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Xunit;

namespace VSSiding.E2E.Tests;

// Post mode puts a post in the cell corner nearest the click, for a quarter of the framing.
[AtlasWorld(StrictBootDiagnostics = true)]
public class PostScenarios : AtlasScenarioBase
{
    [AtlasScenario(FreshWorld = true)]
    public async Task Post_Should_StandInTheNearestCornerForOnePlank_When_PlanksArePressedInPostMode()
    {
        BlockPos cell = World.Spawn.Offset(1, 2, 0);
        World.SetBlock("game:planks-aged-ud", cell.DownCopy());
        ITestPlayer player = await WallBuilder.JoinBuilder(World);
        player.Entity.WatchedAttributes.SetString("vssidingFraming", "post");
        await player.TeleportTo(cell.WestCopy());
        await player.GiveItem("game:plank-oak", 3);

        ItemSlot slot = player.Player.InventoryManager.ActiveHotbarSlot;
        var sel = new BlockSelection { Position = cell.DownCopy(), Face = BlockFacing.UP, HitPosition = new Vec3d(0.2, 1, 0.8) };
        var handling = EnumHandHandling.NotHandled;
        slot.Itemstack!.Collectible.OnHeldInteractStart(slot, player.Entity, sel, null, true, ref handling);

        int planksLeft = slot.Itemstack?.StackSize ?? 0;
        await WallBuilder.Press(World, player, cell, "game:clay-blue", BlockFacing.WEST, new Vec3d(0.5, 0.5, 0.5));

        Assert.Equal(
            ("wall-cornerin-south", EnumHandHandling.PreventDefault, 2, ("oak", null, null, null, null)),
            (World.BlockAt(cell).Code.Path, handling, planksLeft, WallBuilder.Layers(World, cell)));
    }
}
