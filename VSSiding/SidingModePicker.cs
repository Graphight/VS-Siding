using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

namespace VSSiding;

// Patched onto game:itemtypes/resource/plank.json - see docs/proposals/sectioned-mode-picker.md. Owns
// the saw's mode picker: a framing row (wall, corner) and a boards row (weatherboard, boards), each a
// new line in the grid (vanilla's GuiDialogToolMode starts a row at every SkillItem.Linebreak). The
// choice lives on the player, in Entity.WatchedAttributes, not the item stack - so two stacks of the
// same planks, or swapping a saw for a log, never disagree. PlaceWallFrame reads the framing row
// through Layout below and keeps placement only.
public class SidingModePicker : CollectibleBehavior
{
    // Row definitions as plain data, so a later row (logs) is one more entry here. Framing is never
    // empty - AllowNone false, defaulting to its first option - while a finish row can be toggled off,
    // which means "use the entry's Elements default" back in SidingWallBlock.
    internal static readonly (string Key, string[] Options, bool AllowNone)[] Rows =
    {
        ("vssidingFraming", new[] { "wall", "corner" }, false),
        ("vssidingBoards", new[] { "weatherboard", "boards" }, true),
    };

    private SkillItem[]? litIcons;
    private SkillItem[]? dimIcons;

    public SidingModePicker(CollectibleObject collObj) : base(collObj)
    {
    }

    public override void OnLoaded(ICoreAPI api)
    {
        base.OnLoaded(api);
        if (api is not ICoreClientAPI capi) return;

        // -1 tints white for the chosen option, as every vanilla mode picker does; grey tints the
        // rest of the row dimmed, since GuiElementSkillItemGrid draws every option the same
        // otherwise. ModeIconsTests pins each file to its mode's code.
        litIcons = LoadIcons(capi, "vssidingModePickerLitIcons", -1);
        dimIcons = LoadIcons(capi, "vssidingModePickerDimIcons", ColorUtil.ColorFromRgba(128, 128, 128, 255));
    }

    private static SkillItem[] LoadIcons(ICoreClientAPI capi, string cacheKey, int color) =>
        ObjectCacheUtil.GetOrCreate(capi, cacheKey, () =>
        {
            var items = Rows.SelectMany(row => row.Options)
                .Select(code => new SkillItem { Code = new AssetLocation(code), Name = Lang.Get($"vssiding:toolmode-{code}") })
                .Select(item => item.WithIcon(capi, capi.Gui.LoadSvgWithPadding(
                    new AssetLocation("vssiding", $"textures/icons/{item.Code.Path}.svg"), 48, 48, 5, color)))
                .ToArray();

            int index = 0;
            foreach (var row in Rows)
            {
                items[index].Linebreak = index > 0;
                index += row.Options.Length;
            }
            return items;
        });

    public override SkillItem[] GetToolModes(ItemSlot slot, IClientPlayer forPlayer, BlockSelection blockSel)
    {
        var chosen = Rows.Select((_, row) => ChoiceOf(forPlayer, row)).ToArray();
        var modes = new SkillItem[litIcons!.Length];
        for (int i = 0; i < modes.Length; i++)
        {
            var (row, option) = Locate(i);
            modes[i] = Rows[row].Options[option] == chosen[row] ? litIcons[i] : dimIcons![i];
        }
        return modes;
    }

    public override int GetToolMode(ItemSlot slot, IPlayer byPlayer, BlockSelection blockSel)
        => Array.IndexOf(Rows[0].Options, ChoiceOf(byPlayer, 0));

    public override void SetToolMode(ItemSlot slot, IPlayer byPlayer, BlockSelection blockSel, int toolMode)
    {
        var (row, option) = Locate(toolMode);
        if (row < 0) return;

        var (key, options, allowNone) = Rows[row];
        byPlayer.Entity.WatchedAttributes.SetString(key, Toggle(ChoiceOf(byPlayer, row), options[option], allowNone));
    }

    // Which row and option a flat GetToolModes/SetToolMode index falls in, rows counted in order.
    // Out of range comes back (-1, -1) rather than throwing, for a stale index from a shorter picker.
    internal static (int Row, int Option) Locate(int flatIndex)
    {
        int i = flatIndex;
        for (int row = 0; row < Rows.Length; row++)
        {
            if (i >= 0 && i < Rows[row].Options.Length) return (row, i);
            i -= Rows[row].Options.Length;
        }
        return (-1, -1);
    }

    // Clicking a lit finish option clears it (allowNone); clicking framing always sets - there's no
    // "no framing" state.
    internal static string Toggle(string current, string clicked, bool allowNone)
        => allowNone && current == clicked ? "" : clicked;

    internal static string ChoiceOf(IPlayer player, int row)
    {
        var (key, options, allowNone) = Rows[row];
        return player.Entity.WatchedAttributes.GetString(key, allowNone ? "" : options[0]) ?? (allowNone ? "" : options[0]);
    }

    // The framing row never comes back null - "corner" upgrades to cornerout, anything else frames a
    // plain wall - so a build-flow click always has somewhere to place.
    internal static string Layout(IPlayer player) => ChoiceOf(player, 0) == "corner" ? "cornerout" : "wall";

    // Every finish row after framing, in row order, whichever is currently chosen. SidingWallBlock
    // picks the first one the clicked finish's Styles lists; a row with nothing chosen contributes
    // nothing, so no finish-to-row mapping is needed here.
    internal static IEnumerable<string> FinishChoices(IPlayer player)
    {
        for (int row = 1; row < Rows.Length; row++)
        {
            string choice = ChoiceOf(player, row);
            if (choice.Length > 0) yield return choice;
        }
    }
}
