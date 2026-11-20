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
}
