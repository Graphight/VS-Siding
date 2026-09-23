using System.Linq;
using HarmonyLib;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.Common;
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
        Assert.Single(patched, i => i.Calls(roomSunlight));
    }

    // The rain fall prefix rewrites its argument by name, so a vanilla rename would drop the fix.
    [Fact]
    public void RainFallDistanceStillTakesPos()
    {
        var actual = AccessTools.Method(typeof(BlockAccessorBase), "GetDistanceToRainFall")
            .GetParameters().Select(p => p.Name).ToArray();
        Assert.Equal(new[] { "pos", "horziontalSearchWidth", "verticalSearchWidth" }, actual);
    }
}
