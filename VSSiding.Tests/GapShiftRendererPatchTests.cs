using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.Client.NoObf;
using Vintagestory.GameContent;
using Xunit;

namespace VSSiding.Tests;

// The renderers that draw a snapped block outside chunk tesselation. A game update that renames a
// field or drops the anchor call fails here rather than silently leaving the draw unshifted.
public class GapShiftRendererPatchTests
{
    [Fact]
    public void AnimatableRendererConstructorStillTakesPos()
        => Assert.NotNull(AccessTools.Constructor(typeof(AnimatableRenderer), new[]
        {
            typeof(ICoreClientAPI), typeof(Vec3d), typeof(Vec3f), typeof(AnimatorBase),
            typeof(Dictionary<string, AnimationMetaData>), typeof(MeshData), typeof(EnumRenderStage),
        }));

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

    [Fact]
    public void ParticleTickSpawnsThroughTheShift()
    {
        var original = AccessTools.Method(typeof(Block), nameof(Block.OnAsyncClientParticleTick));
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
