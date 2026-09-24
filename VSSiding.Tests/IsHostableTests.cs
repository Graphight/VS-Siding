using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;
using Vintagestory.GameContent.Mechanics;
using Xunit;

namespace VSSiding.Tests;

// Every guest consumer trusts the Hostable table, so each exclusion is pinned here.
public class IsHostableTests
{
    private class FluidBlock : Block
    {
        public override bool ForFluidsLayer => true;
    }

    private class PowerBlock : BlockMPBase
    {
        public override void DidConnectAt(IWorldAccessor world, BlockPos pos, BlockFacing face) { }
        public override bool HasMechPowerConnectorAt(IWorldAccessor world, BlockPos pos, BlockFacing face, BlockMPBase forBlock) => false;
    }

    private static T Open<T>(T block) where T : Block
    {
        block.SideSolid = new SmallBoolArray(0);
        return block;
    }

    [Fact]
    public void OnlyPlainOpenJsonBlocksAreHostable()
    {
        var bed = Open(new Block());
        bed.VariantStrict["part"] = "head";
        var pot = Open(new Block());
        pot.BlockBehaviors = new BlockBehavior[] { new BlockBehaviorUnplaceable(pot) };

        var blocks = new Dictionary<string, Block>
        {
            ["furniture"] = Open(new Block()),
            ["solid cube"] = new Block(),
            ["tall grass"] = Open(new Block { Replaceable = 6000 }),
            ["cross plant"] = Open(new Block { DrawType = EnumDrawType.Cross }),
            ["fluid"] = Open(new FluidBlock()),
            ["bed half"] = bed,
            ["unplaceable pot"] = pot,
            ["door"] = Open(new Block { BlockEntityBehaviors = new[] { new BlockEntityBehaviorType { Name = "Door" } } }),
            ["multiblock filler"] = Open(new BlockMultiblock()),
            ["mechanical power"] = Open(new PowerBlock()),
            ["siding wall"] = Open(new SidingWallBlock()),
        };

        var expected = new Dictionary<string, bool>
        {
            ["furniture"] = true,
            ["solid cube"] = false,
            ["tall grass"] = false,
            ["cross plant"] = false,
            ["fluid"] = false,
            ["bed half"] = false,
            ["unplaceable pot"] = false,
            ["door"] = false,
            ["multiblock filler"] = false,
            ["mechanical power"] = false,
            ["siding wall"] = false,
        };

        var actual = new Dictionary<string, bool>();
        foreach (var (name, block) in blocks) actual[name] = SidingModSystem.IsHostable(block);

        Assert.Equal(expected, actual);
    }
}
