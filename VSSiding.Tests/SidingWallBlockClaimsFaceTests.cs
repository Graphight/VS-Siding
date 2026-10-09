using System.Collections.Generic;
using System.Linq;
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
        { "diagonal", "west", "west", true },
        { "diagonal", "west", "north", true },
        { "diagonal", "west", "east", false },
        { "diagonal", "west", "south", false },
        { "diagonal", "south", "west", true },
        { "diagonal", "south", "north", false },
    };

    [Theory]
    [MemberData(nameof(Faces))]
    public void ClaimsOnlyThePanelledFaces(string layout, string side, string faceCode, bool expected)
    {
        Assert.Equal(expected, SidingWallBlock.ClaimsFace(layout, side, faceCode));
    }

    // diagonal-west crosses its cell from the south-west corner to the north-east one, so the run carries
    // on north-east (left, the z = 0 end of the unrotated shape) and south-west; the other sides turn with it.
    [Fact]
    public void ADiagonalsRunCarriesOnThroughItsTwoCorners()
    {
        var expected = new Dictionary<string, ((int, int), (int, int))>
        {
            ["west"] = ((1, -1), (-1, 1)),
            ["south"] = ((-1, -1), (1, 1)),
            ["east"] = ((-1, 1), (1, -1)),
            ["north"] = ((1, 1), (-1, -1)),
        };

        Assert.Equal(expected, expected.Keys.ToDictionary(side => side, side =>
        {
            var (left, right) = SidingWallBlock.DiagonalRunNeighbours(side);
            return ((left.X, left.Z), (right.X, right.Z));
        }));
    }
}
