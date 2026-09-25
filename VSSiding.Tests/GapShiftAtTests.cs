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
        var controller = new Block { Code = new AssetLocation("vssiding", "trunk-controller") };
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
        var block = new Block { Code = new AssetLocation("vssiding", "wall-oak-west") };
        var accessor = DispatchProxy.Create<IBlockAccessor, ControllerAccessor>();
        var pos = new BlockPos(5, 60, 9, 0);

        Block resolved = SidingModSystem.ShiftSource(accessor, pos, block);

        Assert.Same(block, resolved);
    }
}
