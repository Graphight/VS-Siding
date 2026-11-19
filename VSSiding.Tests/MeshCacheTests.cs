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
}
