using System.Collections.Generic;
using Xunit;

namespace VSSiding.Tests;

// AcceptsAttachment is ClaimsFace plus a straight wall's open face (furniture-against-thin-walls,
// stage 4): the panel a torch or sign standing in the gap-front cell actually presses against once
// GapShift snaps it there. A cornerout's dead space is boxed in by its two panels, so it gets
// nothing extra - only ClaimsFace's two faces accept.
public class SidingWallBlockAcceptsAttachmentTests
{
    private static readonly Dictionary<string, string> Opposite = new()
    {
        ["north"] = "south",
        ["south"] = "north",
        ["east"] = "west",
        ["west"] = "east",
    };

    private static readonly Dictionary<string, string> CorneroutSecondFace = new()
    {
        ["west"] = "north",
        ["south"] = "west",
        ["east"] = "south",
        ["north"] = "east",
    };

    private static readonly string[] Layouts = { "wall", "cornerout" };
    private static readonly string[] Sides = { "north", "east", "south", "west" };
    private static readonly string[] Faces = { "north", "east", "south", "west", "up", "down" };

    public static TheoryData<string, string, string, bool> Cases()
    {
        var data = new TheoryData<string, string, string, bool>();
        foreach (string layout in Layouts)
        {
            foreach (string side in Sides)
            {
                foreach (string face in Faces)
                {
                    bool claimed = face == side || (layout == "cornerout" && face == CorneroutSecondFace[side]);
                    bool acceptsOpenFace = layout == "wall" && face == Opposite[side];
                    data.Add(layout, side, face, claimed || acceptsOpenFace);
                }
            }
        }
        return data;
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void AcceptsClaimedAndOpenFaces(string layout, string side, string face, bool expected)
    {
        Assert.Equal(expected, SidingWallBlock.AcceptsAttachment(layout, side, face));
    }
}
