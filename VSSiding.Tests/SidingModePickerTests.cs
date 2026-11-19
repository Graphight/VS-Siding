using System.Linq;
using HarmonyLib;
using Xunit;

namespace VSSiding.Tests;

public class SidingModePickerTests
{
    // A flat picker index walks the rows in order: framing (wall, corner, diagonal, post, floor, face), deck
    // (deck), boards (weatherboard, boards, hboards), logs (shakes, logs, bark, hbark). Out of range
    // comes back (-1, -1) rather than throwing.
    [Fact]
    public void LocateMapsAFlatIndexToItsRowAndOption()
    {
        Assert.Equal(
            new[] { (0, 0), (0, 1), (0, 2), (0, 3), (0, 4), (0, 5), (1, 0), (2, 0), (2, 1), (2, 2), (3, 0), (3, 1), (3, 2), (3, 3), (4, 0), (4, 1), (-1, -1), (-1, -1) },
            new[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, -1 }.Select(SidingModePicker.Locate));
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

    [Fact]
    public void ASwitchRowStoresTheOffOptionsInOrder()
    {
        var options = new[] { "updown", "leftright" };
        Assert.Equal(
            new[] { "leftright", "updown,leftright", "updown", "" },
            new[]
            {
                SidingModePicker.ToggleSwitch("", options, "leftright"),
                SidingModePicker.ToggleSwitch("leftright", options, "updown"),
                SidingModePicker.ToggleSwitch("updown,leftright", options, "leftright"),
                SidingModePicker.ToggleSwitch("updown", options, "updown"),
            });
    }

    [Fact]
    public void ASwitchIsLitUnlessItIsInTheOffList()
    {
        var row = SidingModePicker.Rows.Single(r => r.Key == "vssidingMerge");
        Assert.Equal(
            new[] { true, true, false, true },
            new[]
            {
                SidingModePicker.IsLit(row, "", "updown"),
                SidingModePicker.IsLit(row, "updown", "leftright"),
                SidingModePicker.IsLit(row, "updown,leftright", "leftright"),
                SidingModePicker.IsLit(row, "leftright", "updown"),
            });
    }

    [Fact]
    public void AnOptionRowLightsOnlyItsChosenOption()
    {
        var row = SidingModePicker.Rows.Single(r => r.Key == "vssidingFraming");
        Assert.Equal(
            new[] { true, false },
            new[] { SidingModePicker.IsLit(row, "wall", "wall"), SidingModePicker.IsLit(row, "wall", "corner") });
    }

    // The picker reaches into GuiDialogToolMode's private members by name, so a game update that
    // renames one would leave a saw in the off hand opening no picker, with only a log line to say so.
    [Fact]
    public void TheDialogLayoutPatchStillFindsItsVanillaMembers()
    {
        SidingModePicker.PatchDialog(new Harmony("vssiding.tests.modepicker"));
    }
}
