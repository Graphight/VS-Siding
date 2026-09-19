using System.Linq;
using System.Reflection;
using HarmonyLib;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;
using Xunit;

namespace VSSiding.Tests;

public class RoomSkylightPatchTests
{
    [Fact]
    public void ReplacesExactlyOneGetLightLevelCallWithRoomSunlight()
    {
        var original = AccessTools.Method(typeof(RoomRegistry), "FindRoomForPosition");
        var instructions = PatchProcessor.GetOriginalInstructions(original);

        var patched = SidingModSystem.RoomSkylightTranspiler(instructions).ToList();

        var getLightLevel = AccessTools.Method(typeof(IBlockAccessor), nameof(IBlockAccessor.GetLightLevel),
            new[] { typeof(BlockPos), typeof(EnumLightLevelType) });
        var roomSunlight = AccessTools.Method(typeof(SidingWallBlock), nameof(SidingWallBlock.RoomSunlight));

        Assert.DoesNotContain(patched, i => i.Calls(getLightLevel));
        Assert.Single(patched, i => i.opcode == System.Reflection.Emit.OpCodes.Call && (MethodInfo)i.operand == roomSunlight);
    }
}
