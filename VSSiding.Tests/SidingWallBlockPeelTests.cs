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

    [Fact]
    public void BackHitTakesTheStepBeforeTheBackFinish()
        => Assert.Equal("step", SidingWallBlock.PeelLayer("back", "wattle", null, null, "planks", null, "game:plankstairs-oak-up-north-free"));

    [Fact]
    public void EndHitTakesTheStepBeforeTheBackFinish()
        => Assert.Equal("step", SidingWallBlock.PeelLayer(null, null, null, null, "planks", null, "game:plankstairs-oak-up-north-free"));

    [Fact]
    public void BackHitTakesTheStepBeforeAFrontFinish()
        => Assert.Equal("step", SidingWallBlock.PeelLayer("back", "wattle", "daub", null, null, null, "game:plankstairs-oak-up-north-free"));

    [Fact]
    public void FrontHitKeepsTodaysOrderWithAStepPresent()
        => Assert.Equal("front", SidingWallBlock.PeelLayer("front", "wattle", "daub", null, "planks", null, "game:plankstairs-oak-up-north-free"));

    [Fact]
    public void StepOutlivesEveryFinishAndTheInfill()
        => Assert.Equal("step", SidingWallBlock.PeelLayer("front", "wattle", null, null, null, null, "game:plankstairs-oak-up-north-free"));

    [Fact]
    public void StepComesOffBeforeTheFrame()
        => Assert.Equal("step", SidingWallBlock.PeelLayer("front", null, null, null, null, null, "game:plankstairs-oak-up-north-free"));

    [Fact]
    public void DeckTopHitPeelsTheTopFinishFirst()
        => Assert.Equal("deckfront", SidingWallBlock.DeckPeelLayer("front", "wattle", "planks", "daub"));

    [Fact]
    public void DeckUndersideHitPeelsTheUndersideFinishFirst()
        => Assert.Equal("deckback", SidingWallBlock.DeckPeelLayer("back", "wattle", "planks", "daub"));

    [Fact]
    public void DeckHitOnABareFacePeelsTheOtherFinish()
        => Assert.Equal("deckback", SidingWallBlock.DeckPeelLayer("front", "wattle", null, "daub"));

    [Fact]
    public void DeckHitPeelsInfillOnceFinishesAreGone()
        => Assert.Equal("deckinfill", SidingWallBlock.DeckPeelLayer("back", "wattle", null, null));

    [Fact]
    public void DeckHitPeelsJoistsOnceBare()
        => Assert.Equal("deck", SidingWallBlock.DeckPeelLayer("front", null, null, null));

    [Fact]
    public void DeckWithNoFacePeelsTheOutermostLayer()
    {
        Assert.Equal("deckfront", SidingWallBlock.DeckPeelLayer(null, "wattle", "planks", "daub"));
        Assert.Equal("deckback", SidingWallBlock.DeckPeelLayer(null, "wattle", null, "daub"));
        Assert.Equal("deckinfill", SidingWallBlock.DeckPeelLayer(null, "wattle", null, null));
        Assert.Equal("deck", SidingWallBlock.DeckPeelLayer(null, null, null, null));
    }

    [Fact]
    public void AnyOtherHitOnADeckedWallStillResolvesThroughTheWallOrder()
        => Assert.Equal("deck", SidingWallBlock.PeelLayer("back", "wattle", null, null, null, "oak"));
}
