using System.Linq;
using Vintagestory.API.Client;
using Xunit;

namespace VSSiding.Tests;

public class MeshCacheTests
{
    [Fact]
    public void ForgetCacheEmptiesTheCacheAndReturnsItsCount()
    {
        SidingModSystem.ForgetCache();
        SidingModSystem.Cached("a", () => new object());
        SidingModSystem.Cached("b", () => new object());

        Assert.Equal(2, SidingModSystem.ForgetCache());
        Assert.Empty(SidingModSystem.Cache);
    }

    [Fact]
    public void MeshBytesSumsTheArraysOfWallMeshesAndLoneMeshesOnly()
    {
        SidingModSystem.ForgetCache();
        MeshData Mesh() => new(false) { xyz = new float[3], Indices = new int[2], Rgba = new byte[4] };
        SidingModSystem.Cached("wall", () => new[] { Mesh(), Mesh() });
        SidingModSystem.Cached("ladder", Mesh);
        SidingModSystem.Cached("shape", () => new object());

        Assert.Equal(3 * (12 + 8 + 4), SidingModSystem.MeshBytes());
        SidingModSystem.ForgetCache();
    }

    [Fact]
    public void AFullCacheDropsEverythingButThePickerIconsOnItsNextMiss()
    {
        SidingModSystem.ForgetCache();
        SidingModSystem.Cached("icons", () => new SkillItem[0]);
        for (int i = 1; i < SidingModSystem.MaxEntries; i++) SidingModSystem.Cached($"mesh-{i}", () => new object());
        SidingModSystem.Cached("mesh-1", () => new object());
        Assert.Equal(SidingModSystem.MaxEntries, SidingModSystem.Cache.Count);

        SidingModSystem.Cached("new", () => new object());

        Assert.Equal(new[] { "icons", "new" }, SidingModSystem.Cache.Keys.OrderBy(key => key));
        SidingModSystem.ForgetCache();
    }
}
