using Xunit;

namespace VSSiding.Tests;

public class SidingWallBlockPeelTests
{
    [Fact]
    public void HitFaceFinishPeelsFirst()
        => Assert.Equal("back", SidingWallBlock.PeelLayer("back", "wattle", "daub", null, "planks"));

    [Fact]
    public void BareHitFacePeelsAnotherFinish()
        => Assert.Equal("secondfront", SidingWallBlock.PeelLayer("front", "wattle", null, "daub", "planks"));

    [Fact]
    public void EndFacePeelsFrontFirst()
        => Assert.Equal("front", SidingWallBlock.PeelLayer(null, "wattle", "daub", "daub", "planks"));

    [Fact]
    public void InfillPeelsOnceFinishesAreGone()
        => Assert.Equal("infill", SidingWallBlock.PeelLayer("front", "wattle", null, null, null));

    [Fact]
    public void FrameOnlyBreaksTheBlock()
        => Assert.Null(SidingWallBlock.PeelLayer("front", null, null, null, null));
}
