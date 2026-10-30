using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;
using Xunit;

namespace VSSiding.Tests;

public class GapShiftAtTests
{
    // Only ShiftSource's own GetBlock(BlockPos) call needs answering, so a stdlib DispatchProxy
    // stands in for the rest of IBlockAccessor's surface, as GuestWallsTests does for IWorldAccessor.
    private class ControllerAccessor : DispatchProxy
    {
        internal Block Controller = null!;
        internal BlockPos? RequestedPos;

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod?.Name != nameof(IBlockAccessor.GetBlock) || args?.Length != 1)
                throw new System.NotSupportedException($"{targetMethod?.Name} is not stubbed");
            RequestedPos = (BlockPos)args[0]!;
            return Controller;
        }
    }

    [Fact]
    public void AFillerResolvesToTheBlockAtItsOffsetInv()
    {
        var controller = new Block();
        var filler = new BlockMultiblock { OffsetInv = new Vec3i(1, 0, 0) };
        var accessor = DispatchProxy.Create<IBlockAccessor, ControllerAccessor>();
        var proxy = (ControllerAccessor)(object)accessor;
        proxy.Controller = controller;
        var pos = new BlockPos(5, 60, 9, 0);

        Block resolved = SidingModSystem.ShiftSource(accessor, pos, filler);

        Assert.Same(controller, resolved);
        Assert.Equal(pos.AddCopy(filler.OffsetInv), proxy.RequestedPos);
    }

    [Fact]
    public void AnyOtherBlockResolvesToItself()
    {
        var block = new Block();
        var accessor = DispatchProxy.Create<IBlockAccessor, ControllerAccessor>();
        var pos = new BlockPos(5, 60, 9, 0);

        Block resolved = SidingModSystem.ShiftSource(accessor, pos, block);

        Assert.Same(block, resolved);
    }

    private static BlockBed Bed(string part, string side)
    {
        var bed = new BlockBed();
        bed.VariantStrict["part"] = part;
        bed.VariantStrict["side"] = side;
        return bed;
    }

    [Fact]
    public void OnlyABedsFeetPointAtTheirHead()
    {
        var pos = new BlockPos(5, 60, 9, 0);
        var blocks = new Dictionary<string, Block>
        {
            ["feetNorth"] = Bed("feet", "north"),
            ["feetEast"] = Bed("feet", "east"),
            ["feetSouth"] = Bed("feet", "south"),
            ["feetWest"] = Bed("feet", "west"),
            ["head"] = Bed("head", "north"),
            ["plain"] = new Block(),
        };

        var actual = blocks.ToDictionary(kv => kv.Key, kv => SidingModSystem.BedHeadPos(kv.Value, pos));

        Assert.Equal(new Dictionary<string, BlockPos?>
        {
            ["feetNorth"] = new BlockPos(5, 60, 10, 0),
            ["feetEast"] = new BlockPos(4, 60, 9, 0),
            ["feetSouth"] = new BlockPos(5, 60, 8, 0),
            ["feetWest"] = new BlockPos(6, 60, 9, 0),
            ["head"] = null,
            ["plain"] = null,
        }, actual);
    }

    private class MapAccessor : DispatchProxy
    {
        internal Dictionary<BlockPos, Block> Blocks = new();

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod?.Name != nameof(IBlockAccessor.GetBlock) || args?.Length != 1)
                throw new System.NotSupportedException($"{targetMethod?.Name} is not stubbed");
            return Blocks.TryGetValue((BlockPos)args[0]!, out var block) ? block : new Block();
        }
    }

    [Fact]
    public void EveryCellOfABedNamesTheWholeBed()
    {
        // An east Celtic bed: feet east of the head, fillers to the north. A vanilla bed and a trunk beside it.
        var head = new BlockPos(10, 60, 10, 0);
        var vanillaHead = new BlockPos(20, 60, 10, 0);
        var trunk = new BlockPos(30, 60, 10, 0);
        var cells = new Dictionary<string, (BlockPos pos, Block block)>
        {
            ["head"] = (head, FootprintHostsTests.DoubleBedHead("east", FootprintHostsTests.CelticBedMultiblock["east"])),
            ["feet"] = (head.EastCopy(), Bed("feet", "east")),
            ["filler beside the head"] = (head.NorthCopy(), new BlockMultiblock { OffsetInv = new Vec3i(0, 0, 1) }),
            ["filler beside the feet"] = (head.NorthCopy().East(), new BlockMultiblock { OffsetInv = new Vec3i(-1, 0, 1) }),
            ["vanilla feet"] = (vanillaHead.EastCopy(), Bed("feet", "east")),
            ["vanilla head"] = (vanillaHead, Bed("head", "east")),
            ["trunk filler"] = (trunk.EastCopy(), new BlockMultiblock { OffsetInv = new Vec3i(-1, 0, 0) }),
            ["trunk"] = (trunk, new Block()),
        };
        var accessor = DispatchProxy.Create<IBlockAccessor, MapAccessor>();
        ((MapAccessor)(object)accessor).Blocks = cells.Values.ToDictionary(cell => cell.pos, cell => cell.block);

        var doubleBed = new[] { head, head.EastCopy(), head.NorthCopy(), head.NorthCopy().East() };
        var vanillaBed = new[] { vanillaHead, vanillaHead.EastCopy() };
        var expected = new Dictionary<string, BlockPos[]>
        {
            ["head"] = doubleBed,
            ["feet"] = doubleBed,
            ["filler beside the head"] = doubleBed,
            ["filler beside the feet"] = doubleBed,
            ["vanilla feet"] = vanillaBed,
            ["vanilla head"] = vanillaBed,
            ["trunk filler"] = System.Array.Empty<BlockPos>(),
            ["trunk"] = System.Array.Empty<BlockPos>(),
        };

        var actual = cells.ToDictionary(kv => kv.Key, kv => SidingModSystem.BedCells(accessor, kv.Value.pos, kv.Value.block).ToArray());

        Assert.Equal(expected, actual);
    }
}
