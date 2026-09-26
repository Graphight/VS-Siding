using Xunit;

namespace VSSiding.Tests;

public class SidingWallBlockPeelTests
{
    [Fact]
    public void HitFaceFinishPeelsFirst()
        => Assert.Equal("back", SidingWallBlock.PeelLayer("back", "wattle", "daub", null, "planks", null));

    [Fact]
    public void BareHitFacePeelsAnotherFinish()
        => Assert.Equal("secondfront", SidingWallBlock.PeelLayer("front", "wattle", null, "daub", "planks", null));

    [Fact]
    public void EndFacePeelsFrontFirst()
        => Assert.Equal("front", SidingWallBlock.PeelLayer(null, "wattle", "daub", "daub", "planks", null));

    [Fact]
    public void InfillPeelsOnceFinishesAreGone()
        => Assert.Equal("infill", SidingWallBlock.PeelLayer("front", "wattle", null, null, null, null));

    [Fact]
    public void FrameOnlyBreaksTheBlock()
        => Assert.Null(SidingWallBlock.PeelLayer("front", null, null, null, null, null));

    [Fact]
    public void BackHitTakesTheDeckBeforeTheBackFinish()
        => Assert.Equal("deck", SidingWallBlock.PeelLayer("back", "wattle", null, null, "planks", "oak"));

    [Fact]
    public void EndHitTakesTheDeckBeforeTheBackFinish()
        => Assert.Equal("deck", SidingWallBlock.PeelLayer(null, null, null, null, "planks", "oak"));

    [Fact]
    public void BackHitTakesTheDeckBeforeAFrontFinish()
        => Assert.Equal("deck", SidingWallBlock.PeelLayer("back", "wattle", "daub", null, null, "oak"));

    [Fact]
    public void FrontHitKeepsTodaysOrderWithADeckPresent()
        => Assert.Equal("front", SidingWallBlock.PeelLayer("front", "wattle", "daub", null, "planks", "oak"));

    [Fact]
    public void DeckOutlivesEveryFinishAndTheInfill()
        => Assert.Equal("deck", SidingWallBlock.PeelLayer("front", "wattle", null, null, null, "oak"));

    [Fact]
    public void DeckComesOffBeforeTheFrame()
        => Assert.Equal("deck", SidingWallBlock.PeelLayer("front", null, null, null, null, "oak"));
}
