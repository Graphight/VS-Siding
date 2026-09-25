using System.Reflection;
using ProtoBuf;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Xunit;

namespace VSSiding.Tests;

public class GuestWallsTests
{
    // BlockEntity.FromTreeAttributes reads only worldAccessForResolve.Side on this path, so a
    // stdlib DispatchProxy stands in for the rest of IWorldAccessor's surface rather than a mock
    // library the test project doesn't reference.
    private class ServerWorld : DispatchProxy
    {
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
            => targetMethod?.Name == "get_Side"
                ? EnumAppSide.Server
                : throw new System.NotSupportedException($"{targetMethod?.Name} is not stubbed");
    }

    private static readonly IWorldAccessor World = DispatchProxy.Create<IWorldAccessor, ServerWorld>();

    // Every field the guest store carries has to survive the trip a chunk actually makes it on:
    // Encode (ToTreeAttributes), a protobuf round trip (chunk save, or the sync packet), and
    // FromTreeAttributes back into a transient entity.
    [Fact]
    public void RecordRoundTripsToAnEntityWithEqualState()
    {
        var block = new Block { Code = new AssetLocation("vssiding", "wall-oak-west") };
        var original = new SidingWallEntity
        {
            Block = block,
            Pos = new BlockPos(5, 60, 9, 0),
            Framing = "oak",
            Infill = "wattle",
            Front = "planks",
            SecondFront = "stone",
            Back = "daub",
            FrontStyle = "weatherboard",
            SecondFrontStyle = "boards",
            BackStyle = "coursed",
        };

        GuestWalls.GuestRecord record = GuestWalls.Encode(original);
        using var wire = new System.IO.MemoryStream();
        Serializer.Serialize(wire, record);
        wire.Position = 0;
        GuestWalls.GuestRecord reloaded = Serializer.Deserialize<GuestWalls.GuestRecord>(wire);

        var restored = new SidingWallEntity { Block = block };
        restored.FromTreeAttributes(Vintagestory.API.Datastructures.TreeAttribute.CreateFromBytes(reloaded.EntityData), World);

        Assert.Equal(
            (record.BlockCode, original.Framing, original.Infill, original.Front, original.SecondFront, original.Back,
                original.FrontStyle, original.SecondFrontStyle, original.BackStyle, original.Pos),
            (reloaded.BlockCode, restored.Framing, restored.Infill, restored.Front, restored.SecondFront, restored.Back,
                restored.FrontStyle, restored.SecondFrontStyle, restored.BackStyle, restored.Pos));
    }
}
