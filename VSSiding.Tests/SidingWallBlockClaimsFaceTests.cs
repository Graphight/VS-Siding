using Xunit;

namespace VSSiding.Tests;

// ClaimsFace is what turns a wall-wide "is this sealed" into a per-face answer: retention, the
// liquid barrier and attachment (decision 0020) all route through it, and a cornerout's second
// leg is the case none of them can check without the game.
public class SidingWallBlockClaimsFaceTests
{
    public static TheoryData<string, string, string, bool> Faces => new()
    {
        { "wall", "west", "west", true },
        { "wall", "west", "east", false },
        { "wall", "west", "north", false },
        { "wall", "south", "south", true },
        { "wall", "south", "west", false },
        { "cornerout", "west", "west", true },
        { "cornerout", "west", "north", true },
        { "cornerout", "west", "east", false },
        { "cornerout", "west", "south", false },
        { "cornerout", "south", "west", true },
        { "cornerout", "south", "north", false },
    };

    [Theory]
    [MemberData(nameof(Faces))]
    public void ClaimsOnlyThePanelledFaces(string layout, string side, string faceCode, bool expected)
    {
        Assert.Equal(expected, SidingWallBlock.ClaimsFace(layout, side, faceCode));
    }
}
