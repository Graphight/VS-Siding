using System.Linq;
using HarmonyLib;
using Xunit;

namespace VSSiding.Tests;

public class SidingModePickerTests
{
    // A flat picker index walks the rows in order: framing (wall, corner), boards (weatherboard,
    // boards), logs (shakes, logs). Out of range comes back (-1, -1) rather than throwing.
    [Fact]
    public void LocateMapsAFlatIndexToItsRowAndOption()
    {
        Assert.Equal(
            new[] { (0, 0), (0, 1), (1, 0), (1, 1), (2, 0), (2, 1), (-1, -1), (-1, -1) },
            new[] { 0, 1, 2, 3, 4, 5, 6, -1 }.Select(SidingModePicker.Locate));
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

    // The picker reaches into GuiDialogToolMode's private members by name, so a game update that
    // renames one would leave a saw in the off hand opening no picker, with only a log line to say so.
    [Fact]
    public void TheDialogLayoutPatchStillFindsItsVanillaMembers()
    {
        SidingModePicker.PatchDialog(new Harmony("vssiding.tests.modepicker"));
    }
}
