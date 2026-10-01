using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;
using Xunit;

namespace VSSiding.Tests;

// The hanger table decides which blocks ride up to a thin floor's underside, so each kind is pinned here.
public class IsHangerTests
{
    private static Block Lantern(string position)
    {
        var lantern = new Block();
        lantern.VariantStrict["position"] = position;
        lantern.BlockBehaviors = new BlockBehavior[] { new BlockBehaviorOmniAttachable(lantern) { facingCode = "position" } };
        return lantern;
    }

    private static Block Chandelier(params BlockFacing[] attachableFaces)
    {
        var chandelier = new Block();
        var falling = new BlockBehaviorUnstableFalling(chandelier);
        SidingModSystem.AttachableFacesRef(falling) = attachableFaces;
        chandelier.BlockBehaviors = new BlockBehavior[] { falling };
        return chandelier;
    }

    [Fact]
    public void OnlyBlocksAttachedToTheirTopAreHangers()
    {
        var blocks = new Dictionary<string, Block>
        {
            ["hanging lantern"] = Lantern("down"),
            ["standing lantern"] = Lantern("up"),
            ["chandelier"] = Chandelier(BlockFacing.UP),
            ["two-way chandelier"] = Chandelier(BlockFacing.UP, BlockFacing.DOWN),
            ["falling sand"] = Chandelier(BlockFacing.DOWN),
            ["plain block"] = new Block(),
        };

        var expected = new Dictionary<string, bool>
        {
            ["hanging lantern"] = true,
            ["standing lantern"] = false,
            ["chandelier"] = true,
            ["two-way chandelier"] = true,
            ["falling sand"] = false,
            ["plain block"] = false,
        };

        var actual = new Dictionary<string, bool>();
        foreach (var (name, block) in blocks) actual[name] = SidingModSystem.IsHanger(block);

        Assert.Equal(expected, actual);
    }

    // Answers GetBlock(BlockPos) with the cell above or below the hanger; nothing else is stubbed.
    private class ColumnAccessor : DispatchProxy
    {
        internal Block Above = null!, Below = null!;
        internal int HangerY;

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod?.Name != nameof(IBlockAccessor.GetBlock) || args?.Length != 1)
                throw new System.NotSupportedException($"{targetMethod?.Name} is not stubbed");
            return ((BlockPos)args[0]!).Y > HangerY ? Above : Below;
        }
    }

    [Fact]
    public void OnlyAHangerHungUnderAThinFloorRises()
    {
        var floor = new SidingFloorBlock();
        var plank = new Block();
        var air = new Block { SideSolid = new SmallBoolArray(0) };
        var pos = new BlockPos(5, 60, 9, 0);

        var cases = new Dictionary<string, (Block hanger, Block above, Block below)>
        {
            ["lantern under a floor"] = (Lantern("down"), floor, plank),
            ["lantern under planks"] = (Lantern("down"), plank, air),
            ["chandelier hung under a floor"] = (Chandelier(BlockFacing.UP, BlockFacing.DOWN), floor, air),
            ["chandelier standing under a floor"] = (Chandelier(BlockFacing.UP, BlockFacing.DOWN), floor, plank),
            ["one-way chandelier over a table"] = (Chandelier(BlockFacing.UP), floor, plank),
        };

        var expected = new Dictionary<string, double>
        {
            ["lantern under a floor"] = 0.75,
            ["lantern under planks"] = 0,
            ["chandelier hung under a floor"] = 0.75,
            ["chandelier standing under a floor"] = 0,
            ["one-way chandelier over a table"] = 0.75,
        };

        var actual = cases.ToDictionary(kv => kv.Key, kv =>
        {
            var accessor = DispatchProxy.Create<IBlockAccessor, ColumnAccessor>();
            var column = (ColumnAccessor)(object)accessor;
            (column.Above, column.Below, column.HangerY) = (kv.Value.above, kv.Value.below, pos.Y);
            return SidingModSystem.HangShift(accessor, pos, kv.Value.hanger);
        });

        Assert.Equal(expected, actual);
    }
}
