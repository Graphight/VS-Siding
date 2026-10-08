using System;
using System.Threading.Tasks;
using Atlas.Api;
using Atlas.XUnit;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Xunit;

namespace VSSiding.E2E.Tests;

// Without the build signal a wall or floor tooltip is the seal line alone; the layer list and the handbook sentence need the saw (decision 0065).
[AtlasWorld(StrictBootDiagnostics = true)]
public class TooltipScenarios : AtlasScenarioBase
{
    private static string Lines(params string[] lines) => string.Concat(Array.ConvertAll(lines, line => line + Environment.NewLine));

    [AtlasScenario(FreshWorld = true)]
    public async Task Wall_Should_ShowLayersAndHandbookSentence_OnlyWithTheSaw()
    {
        BlockPos cell = World.Spawn.Offset(1, 2, 0);
        World.SetBlock("game:planks-aged-ud", cell.DownCopy());
        ITestPlayer player = await WallBuilder.JoinBuilder(World);
        await WallBuilder.Raise(World, player, cell, cell.WestCopy(), "game:plank-oak", "game:stick");
        Block block = World.BlockAt(cell);
        string side = block.Variant["side"];
        string unfinished = Lang.Get("vssiding:tooltip-unfinished");

        string withSaw = block.GetPlacedBlockInfo(player.Entity.World, cell, player.Player);
        player.Entity.LeftHandItemSlot.Itemstack = null;
        string withoutSaw = block.GetPlacedBlockInfo(player.Entity.World, cell, player.Player);

        Assert.Equal(
            (
                Lines(
                    "",
                    "  " + Lang.Get("vssiding:framing-oak"),
                    "  " + Lang.Get("vssiding:infill-wattle"),
                    $"  {Lang.Get("game:facing-" + side)}, {Lang.Get("game:facing-" + BlockFacing.FromCode(side).Opposite.Code)}: {unfinished}",
                    "  " + Lang.Get("vssiding:tooltip-sealed")) + Lang.GetMatching("vssiding:blockdesc-" + block.Code.Path),
                Lang.Get("vssiding:tooltip-sealed")),
            (withSaw, withoutSaw));
    }

    // The floor's code has no variant, so lang's blockdesc-floor-* never matches it and base appends no sentence.
    [AtlasScenario(FreshWorld = true)]
    public async Task Floor_Should_ShowLayers_OnlyWithTheSaw()
    {
        BlockPos cell = World.Spawn.Offset(1, 2, 0);
        World.SetBlock("game:planks-aged-ud", cell.DownCopy());
        ITestPlayer player = await WallBuilder.JoinBuilder(World);
        player.Entity.WatchedAttributes.SetString("vssidingFraming", "floor");
        await WallBuilder.Raise(World, player, cell, cell.WestCopy(), "game:plank-oak", "game:clay-blue");
        Block block = World.BlockAt(cell);
        string unfinished = Lang.Get("vssiding:tooltip-unfinished");

        string withSaw = block.GetPlacedBlockInfo(player.Entity.World, cell, player.Player);
        player.Entity.LeftHandItemSlot.Itemstack = null;
        string withoutSaw = block.GetPlacedBlockInfo(player.Entity.World, cell, player.Player);

        Assert.Equal(
            (
                Lines(
                    "",
                    "  " + Lang.Get("vssiding:framing-oak"),
                    "  " + Lang.Get("vssiding:infill-clay"),
                    "  " + Lang.Get("vssiding:tooltip-top", unfinished),
                    "  " + Lang.Get("vssiding:tooltip-underside", unfinished),
                    "  " + Lang.Get("vssiding:tooltip-sealed-cool")).TrimEnd(),
                Lang.Get("vssiding:tooltip-sealed-cool")),
            (withSaw, withoutSaw));
    }
}
