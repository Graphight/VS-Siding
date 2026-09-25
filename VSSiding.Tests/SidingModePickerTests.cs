using System.Linq;
using Xunit;

namespace VSSiding.Tests;

public class SidingModePickerTests
{
    // The framing row comes first (wall, corner), the boards row second (weatherboard, boards) - a
    // flat GetToolModes/SetToolMode index walks the rows in that order. An out-of-range index (a
    // stale index from a shorter picker) comes back (-1, -1) rather than throwing.
    [Fact]
    public void LocateMapsAFlatIndexToItsRowAndOption()
    {
        Assert.Equal(
            new[] { (0, 0), (0, 1), (1, 0), (1, 1), (-1, -1), (-1, -1) },
            new[] { 0, 1, 2, 3, 4, -1 }.Select(SidingModePicker.Locate));
    }

    [Fact]
    public void ToggleClearsAnAllowNoneRowButAlwaysSetsAFramingRow()
    {
        Assert.Equal(
            new[] { "", "boards", "weatherboard", "wall" },
            new[]
            {
                SidingModePicker.Toggle("weatherboard", "weatherboard", allowNone: true),
                SidingModePicker.Toggle("weatherboard", "boards", allowNone: true),
                SidingModePicker.Toggle("", "weatherboard", allowNone: true),
                SidingModePicker.Toggle("wall", "wall", allowNone: false),
            });
    }
}
