using Vintagestory.API.Common;
using Vintagestory.GameContent;
using Xunit;

namespace VSSiding.Tests;

// Behaviors answer a click in array order, so the framing behavior has to sit ahead of any behavior another mod appended (#143).
public class PlaceWallFrameTests
{
    private class OtherBehavior(CollectibleObject collObj) : CollectibleBehavior(collObj);

    private static readonly Item Stick = new();

    [Fact]
    public void AfterGroundStorable_Should_MoveFramingJustAfterGroundStorable_When_AnotherModAppendedAhead()
    {
        var processable = new OtherBehavior(Stick);
        var groundStorable = new CollectibleBehaviorGroundStorable(Stick);
        var sawhorse = new OtherBehavior(Stick);
        var framing = new PlaceWallFrame(Stick);

        Assert.Equal(
            new CollectibleBehavior[] { processable, groundStorable, framing, sawhorse },
            PlaceWallFrame.AfterGroundStorable(new CollectibleBehavior[] { processable, groundStorable, sawhorse, framing }));
    }

    [Fact]
    public void AfterGroundStorable_Should_PutFramingFirst_When_GroundStorableIsGone()
    {
        var sawhorse = new OtherBehavior(Stick);
        var framing = new PlaceWallFrame(Stick);

        Assert.Equal(
            new CollectibleBehavior[] { framing, sawhorse },
            PlaceWallFrame.AfterGroundStorable(new CollectibleBehavior[] { sawhorse, framing }));
    }
}
