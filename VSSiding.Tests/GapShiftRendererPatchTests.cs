using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.Client.NoObf;
using Vintagestory.GameContent;
using Xunit;

namespace VSSiding.Tests;

// The renderers that draw a hosted block outside chunk tesselation. A game update that renames a
// field or drops the anchor call fails here rather than silently leaving the draw unshifted.
public class GapShiftRendererPatchTests
{
    [Fact]
    public void AnimatableRendererShiftsRightAfterItsModelMatrixIsResetAndTakesItsLightFromTheOpenSide()
    {
        var original = AccessTools.Method(typeof(AnimatableRenderer), nameof(AnimatableRenderer.OnRenderFrame));
        var patched = SidingModSystem.AnimatableRendererTranspiler(PatchProcessor.GetOriginalInstructions(original)).ToList();

        var identity = AccessTools.Method(typeof(Mat4f), nameof(Mat4f.Identity), new[] { typeof(float[]) });
        var shift = AccessTools.Method(typeof(SidingModSystem), nameof(SidingModSystem.ShiftAnimatable));
        var ownLight = AccessTools.Method(typeof(IBlockAccessor), nameof(IBlockAccessor.GetLightRGBs), new[] { typeof(int), typeof(int), typeof(int) });
        var hostedLight = AccessTools.Method(typeof(SidingModSystem), nameof(SidingModSystem.HostedLightRGBs));
        var identityAt = patched.FindIndex(i => i.Calls(identity));

        Assert.Equal((identityAt + 2, 0, 1), (patched.FindIndex(i => i.Calls(shift)), patched.Count(i => i.Calls(ownLight)), patched.Count(i => i.Calls(hostedLight))));

        // Applying it for real makes the runtime verify the rewritten IL, which the list above can't.
        var harmony = new Harmony("vssiding.tests.animatable");
        try { harmony.Patch(original, transpiler: new HarmonyMethod(typeof(SidingModSystem), nameof(SidingModSystem.AnimatableRendererTranspiler))); }
        finally { harmony.UnpatchAll("vssiding.tests.animatable"); }
        Assert.NotNull(AccessTools.Field(typeof(AnimatableRenderer), "pos"));
        Assert.NotNull(AccessTools.Field(typeof(AnimatableRenderer), "capi"));
    }

    private class LightAccessor : DispatchProxy
    {
        internal Dictionary<(int, int, int), Vec4f> Light = new();

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod?.Name != nameof(IBlockAccessor.GetLightRGBs) || args?.Length != 3)
                throw new NotSupportedException($"{targetMethod?.Name} is not stubbed");
            return Light.TryGetValue(((int)args[0]!, (int)args[1]!, (int)args[2]!), out var light) ? light : new Vec4f();
        }
    }

    // A north wall's panel claims its north face, so a hosted block there never takes the light past it.
    [Fact]
    public void HostedLightTakesEachChannelsBrightestOpenSideAndNotTheClaimedOne()
    {
        var accessor = DispatchProxy.Create<IBlockAccessor, LightAccessor>();
        ((LightAccessor)(object)accessor).Light = new()
        {
            [(5, 60, 4)] = new Vec4f(1, 1, 1, 1),
            [(5, 60, 6)] = new Vec4f(0.5f, 0.2f, 0.1f, 0.3f),
            [(5, 61, 5)] = new Vec4f(0.1f, 0.4f, 0.1f, 0.2f),
        };

        Vec4f light = SidingModSystem.OpenSideLight(accessor, "wall", "north", 5, 60, 5, new Vec4f(0, 0, 0.2f, 0));

        Assert.Equal((0.5f, 0.4f, 0.2f, 0.3f), (light.R, light.G, light.B, light.A));
    }

    [Theory]
    [InlineData(typeof(FirepitContentsRenderer))]
    [InlineData(typeof(PotInFirepitRenderer))]
    [InlineData(typeof(BlockEntitySignRenderer))]
    public void EveryIdentityMatrixIsFollowedByTheShift(Type renderer)
    {
        var original = AccessTools.Method(renderer, "OnRenderFrame");
        var patched = SidingModSystem.RendererMatrixTranspiler(PatchProcessor.GetOriginalInstructions(original), original).ToList();

        var identity = AccessTools.Method(typeof(Matrixf), nameof(Matrixf.Identity));
        var shift = AccessTools.Method(typeof(SidingModSystem), nameof(SidingModSystem.ShiftMatrix));
        var identityAt = patched.Select((i, n) => (i, n)).Where(p => p.i.Calls(identity)).Select(p => p.n + 5).ToList();
        var shiftAt = patched.Select((i, n) => (i, n)).Where(p => p.i.Calls(shift)).Select(p => p.n).ToList();

        Assert.NotEmpty(identityAt);
        Assert.Equal(identityAt, shiftAt);
    }

    [Theory]
    [InlineData(typeof(Block))]
    [InlineData(typeof(BlockOilLamp))]
    public void ParticleTickSpawnsThroughTheShift(Type particleBlock)
    {
        var original = AccessTools.Method(particleBlock, nameof(Block.OnAsyncClientParticleTick));
        var patched = SidingModSystem.ParticleSpawnTranspiler(PatchProcessor.GetOriginalInstructions(original)).ToList();

        var spawn = AccessTools.Method(typeof(IAsyncParticleManager), nameof(IAsyncParticleManager.Spawn));
        var spawnShifted = AccessTools.Method(typeof(SidingModSystem), nameof(SidingModSystem.SpawnShifted));
        Assert.DoesNotContain(patched, i => i.Calls(spawn));
        Assert.Single(patched, i => i.Calls(spawnShifted));
    }

    [Fact]
    public void DecalTesselationRunsThroughTheShift()
    {
        var original = AccessTools.Method(typeof(SystemRenderDecals), "UpdateDecal");
        var patched = SidingModSystem.DecalTesselationTranspiler(PatchProcessor.GetOriginalInstructions(original)).ToList();

        var onDecalTesselation = AccessTools.Method(typeof(Block), nameof(Block.OnDecalTesselation));
        var shifted = AccessTools.Method(typeof(SidingModSystem), nameof(SidingModSystem.DecalTesselationShifted));
        Assert.DoesNotContain(patched, i => i.Calls(onDecalTesselation));
        Assert.Single(patched, i => i.Calls(shifted));
    }
}
