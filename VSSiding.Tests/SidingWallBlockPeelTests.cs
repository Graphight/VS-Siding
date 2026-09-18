using Xunit;

namespace VSSiding.Tests;

public class SidingWallBlockPeelTests
{
    [Fact]
    public void HitFaceFinishPeelsFirst()
        => Assert.Equal("back", SidingWallBlock.PeelLayer("back", "oak", "wattle", "daub", null, "planks"));

    [Fact]
    public void BareHitFacePeelsAnotherFinish()
        => Assert.Equal("secondfront", SidingWallBlock.PeelLayer("front", "oak", "wattle", null, "daub", "planks"));

    [Fact]
    public void EndFacePeelsFrontFirst()
        => Assert.Equal("front", SidingWallBlock.PeelLayer(null, "oak", "wattle", "daub", "daub", "planks"));

    [Fact]
    public void InfillPeelsOnceFinishesAreGone()
        => Assert.Equal("infill", SidingWallBlock.PeelLayer("front", "oak", "wattle", null, null, null));

    [Fact]
    public void FrameOnlyBreaksTheBlock()
        => Assert.Null(SidingWallBlock.PeelLayer("front", "oak", null, null, null, null));
}
