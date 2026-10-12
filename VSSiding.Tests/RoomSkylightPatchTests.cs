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

    // The hosted light source patch reads the source position by name from both methods.
    [Theory]
    [InlineData("CollectLightValuesForLightSource")]
    [InlineData(nameof(ChunkIlluminator.RemoveBlockLight))]
    public void LightSourceMethodsStillTakePos(string method)
    {
        var actual = AccessTools.Method(typeof(ChunkIlluminator), method)
            .GetParameters().Select(p => p.Name).Where(name => name is "posX" or "posY" or "posZ").ToArray();
        Assert.Equal(new[] { "posX", "posY", "posZ" }, actual);
    }

    // Harmony binds a patch's parameters to vanilla's by name: ___field to a private field, a plain
    // name to an argument. The names are read off the patch itself, so a new one is pinned too.
    [Theory]
    [InlineData("CollectPrefix", "CollectLightValuesForLightSource")]
    [InlineData("SunLightPostfix", nameof(ChunkIlluminator.UpdateSunLight))]
    public void LightPatchParametersStillNameVanillaFieldsAndArguments(string patch, string target)
    {
        var bound = AccessTools.Method(typeof(GuestLightPatches), patch).GetParameters()
            .Where(p => p.Name!.StartsWith("___") || !p.Name.StartsWith("__")).ToArray();
        var vanilla = AccessTools.Method(typeof(ChunkIlluminator), target).GetParameters();

        var actual = bound.Select(p => p.Name!.StartsWith("___")
            ? AccessTools.Field(typeof(ChunkIlluminator), p.Name[3..])?.FieldType
            : vanilla.FirstOrDefault(v => v.Name == p.Name)?.ParameterType).ToArray();
        Assert.Equal(bound.Select(p => p.ParameterType).ToArray(), actual);
    }

    // SunLightPostfix passes five ints by position, so a reorder would compile.
    [Fact]
    public void BlockLightUpdateStillTakesItsArgumentsInTheSameOrder()
    {
        var actual = AccessTools.Method(typeof(ChunkIlluminator), nameof(ChunkIlluminator.UpdateBlockLight))
            .GetParameters().Select(p => p.Name).ToArray();
        Assert.Equal(new[] { "oldLightAbsorb", "newLightAbsorb", "posX", "posY", "posZ" }, actual);
    }

    [Fact]
    public void NeighbourSlotsAreTheFourSidesAroundTheCentreOfVanillasGrid()
    {
        int width = (int)AccessTools.Field(typeof(ChunkIlluminator), "VISITED_WIDTH").GetRawConstantValue()!;
        int centre = width / 2 * (1 + width + width * width);

        var actual = BlockFacing.HORIZONTALS.Select(f => GuestLightPatches.NeighbourSlot(f.Normali.X, f.Normali.Y, f.Normali.Z)).ToArray();
        Assert.Equal(new[] { centre - width, centre + 1, centre + width, centre - 1 }, actual);
    }
}
