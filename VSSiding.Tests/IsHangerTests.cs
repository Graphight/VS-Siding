using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;
using Xunit;

namespace VSSiding.Tests;

// The hanger table decides which blocks ride up to a thin floor's underside, so each kind is pinned here.
public class IsHangerTests
{
    private static Block Lantern(string position)
    {
        var lantern = new Block();
        lantern.VariantStrict["position"] = position;
        lantern.BlockBehaviors = new BlockBehavior[] { new BlockBehaviorOmniAttachable(lantern) { facingCode = "position" } };
        return lantern;
    }

    private static Block Chandelier(params BlockFacing[] attachableFaces)
    {
        var chandelier = new Block();
        var falling = new BlockBehaviorUnstableFalling(chandelier);
        SidingModSystem.AttachableFacesRef(falling) = attachableFaces;
        chandelier.BlockBehaviors = new BlockBehavior[] { falling };
        return chandelier;
    }

    [Fact]
    public void OnlyBlocksAttachedToTheirTopAreHangers()
    {
        var blocks = new Dictionary<string, Block>
        {
            ["hanging lantern"] = Lantern("down"),
            ["standing lantern"] = Lantern("up"),
            ["chandelier"] = Chandelier(BlockFacing.UP),
            ["falling sand"] = Chandelier(BlockFacing.DOWN),
            ["plain block"] = new Block(),
        };

        var expected = new Dictionary<string, bool>
        {
            ["hanging lantern"] = true,
            ["standing lantern"] = false,
            ["chandelier"] = true,
            ["falling sand"] = false,
            ["plain block"] = false,
        };

        var actual = new Dictionary<string, bool>();
        foreach (var (name, block) in blocks) actual[name] = SidingModSystem.IsHanger(block);

        Assert.Equal(expected, actual);
    }
}
