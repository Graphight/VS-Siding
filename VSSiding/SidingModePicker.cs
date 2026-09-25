using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using ProtoBuf;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;
using Vintagestory.Client.NoObf;

namespace VSSiding;

// The saw's mode picker, opened by vanilla's tool mode hotkey whenever a saw is in the off hand and
// the main-hand item has no picker of its own (a chisel keeps its own). Vanilla only opens a picker
// the held item supplies and hands the click to the held item on the server, so both are ours here:
// a prefix on GuiDialogToolMode's hotkey and compose, and a channel that carries the click. Rows are
// framing (wall, corner), boards (weatherboard, boards) and logs (shakes, logs). The choice lives on
// the player, in Entity.WatchedAttributes, so it reads the same whatever is in hand.
public static class SidingModePicker
{
    // Row definitions as plain data, so a later row is one more entry here. Framing is never empty -
    // AllowNone false, defaulting to its first option - while a finish row can be toggled off, which
    // means "use the entry's Elements default" back in SidingWallBlock.
    internal static readonly (string Key, string[] Options, bool AllowNone)[] Rows =
    {
        ("vssidingFraming", new[] { "wall", "corner" }, false),
        ("vssidingBoards", new[] { "weatherboard", "boards" }, true),
        ("vssidingLogs", new[] { "shakes", "logs" }, true),
    };

    private const string ChannelName = "vssiding-modepicker";

    [ProtoContract]
    private class PickPacket
    {
        [ProtoMember(1)] public int Index;
    }

    public static void Start(ICoreAPI api)
    {
        api.Network.RegisterChannel(ChannelName).RegisterMessageType<PickPacket>();
    }

    public static void StartServerSide(ICoreServerAPI sapi)
    {
        sapi.Network.GetChannel(ChannelName).SetMessageHandler<PickPacket>((player, packet) => Pick(player, packet.Index));
    }

    // Run on both sides, as vanilla runs SetToolMode: the client's copy answers a reopen before the
    // server's sync lands. SyncedTreeAttribute.SetString marks the path dirty itself.
    private static void Pick(IPlayer player, int index)
    {
        var (row, option) = Locate(index);
        if (row < 0) return;

        var (key, options, allowNone) = Rows[row];
        player.Entity.WatchedAttributes.SetString(key, Toggle(ChoiceOf(player, row), options[option], allowNone));
    }

    // Resolved in PatchDialog rather than as static initialisers: a renamed field after a game
    // update then skips the patch and logs, instead of failing the class for its other callers.
    private static AccessTools.FieldRef<GuiDialog, ICoreClientAPI> DialogCapi = null!;
    private static AccessTools.FieldRef<GuiDialogToolMode, BlockSelection?> DialogBlockSel = null!;

    // In unscaled GUI units. Vanilla packs its rows 5 apart with no headings.
    private const double OptionGap = 12, RowGap = 14, TitleHeight = 24;

    internal static void PatchDialog(Harmony harmony)
    {
        DialogCapi = AccessTools.FieldRefAccess<GuiDialog, ICoreClientAPI>("capi");
        DialogBlockSel = AccessTools.FieldRefAccess<GuiDialogToolMode, BlockSelection?>("blockSele");
        harmony.Patch(AccessTools.Method(typeof(GuiDialogToolMode), "OnKeyCombinationToggle"),
            prefix: new HarmonyMethod(typeof(SidingModePicker), nameof(KeyTogglePrefix)));
        harmony.Patch(AccessTools.Method(typeof(GuiDialogToolMode), "ComposeDialog"),
            prefix: new HarmonyMethod(typeof(SidingModePicker), nameof(ComposeDialogPrefix)));
    }

    private static bool IsOurs(IClientPlayer player)
    {
        if (!SidingWallBlock.HasSawInOffhand(player)) return false;
        ItemSlot slot = player.InventoryManager.ActiveHotbarSlot;
        return slot?.Itemstack?.Collectible.GetToolModes(slot, player, player.CurrentBlockSelection) == null;
    }

    // Vanilla's toggle refuses unless the held item has tool modes; this opens ours instead, with
    // the same creative-only guard GuiDialog.OnKeyCombinationToggle applies to that hotkey type.
    internal static bool KeyTogglePrefix(GuiDialogToolMode __instance, ref bool __result)
    {
        ICoreClientAPI capi = DialogCapi(__instance);
        IClientPlayer player = capi.World.Player;
        if (!IsOurs(player)) return true;

        HotKey? hotKey = capi.Input.GetHotKeyByCode(__instance.ToggleKeyCombinationCode);
        if (hotKey == null || (hotKey.KeyCombinationType == HotkeyType.CreativeTool && player.WorldData.CurrentGameMode != EnumGameMode.Creative))
        {
            __result = false;
            return false;
        }

        DialogBlockSel(__instance) = player.CurrentBlockSelection?.Clone();
        __instance.Toggle();
        __result = true;
        return false;
    }

    // Lays the picker out with a title over each row and room around each option. A click sets the
    // choice here and on the server, then closes the dialog, as vanilla's does.
    internal static bool ComposeDialogPrefix(GuiDialogToolMode __instance)
    {
        ICoreClientAPI capi = DialogCapi(__instance);
        IClientPlayer player = capi.World.Player;
        if (!IsOurs(player)) return true;

        // -1 tints white for the chosen option, as every vanilla mode picker does; grey dims the rest
        // of the row. ModeIconsTests pins each file to its mode's code.
        SkillItem[] lit = LoadIcons(capi, "vssidingModePickerLitIcons", -1);
        SkillItem[] dim = LoadIcons(capi, "vssidingModePickerDimIcons", ColorUtil.ColorFromRgba(128, 128, 128, 255));

        double slotSize = GuiElementPassiveItemSlot.unscaledSlotSize + GuiElementItemSlotGridBase.unscaledSlotPadding;
        double width = Math.Max(
            Rows.Max(row => row.Options.Length) * (slotSize + OptionGap),
            lit.Max(mode => CairoFont.WhiteSmallishText().GetTextExtents(mode.Name).Width / RuntimeEnv.GUIScale + 1));

        __instance.ClearComposers();
        var composer = capi.Gui.CreateCompo("toolmodeselect", ElementStdBounds.AutosizedMainDialog)
            .AddShadedDialogBG(ElementStdBounds.DialogBackground().WithFixedPadding(GuiStyle.ElementToDialogPadding), withTitleBar: false)
            .BeginChildElements();

        double y = 0;
        int index = 0;
        for (int row = 0; row < Rows.Length; row++)
        {
            string title = Lang.Get($"vssiding:toolmoderow-{Rows[row].Key["vssiding".Length..].ToLowerInvariant()}");
            composer.AddStaticText(title, CairoFont.WhiteSmallText(), ElementBounds.Fixed(0, y, width, TitleHeight));
            y += TitleHeight;

            string chosen = ChoiceOf(player, row);
            for (int option = 0; option < Rows[row].Options.Length; option++, index++)
            {
                int flat = index;
                bool isChosen = Rows[row].Options[option] == chosen;
                string gridKey = $"skillitemgrid-{flat}";
                composer.AddSkillItemGrid(new List<SkillItem> { isChosen ? lit[flat] : dim[flat] }, 1, 1, _ => OnClick(capi, __instance, flat),
                    ElementBounds.Fixed(option * (slotSize + OptionGap), y, slotSize, slotSize), gridKey);
                var grid = composer.GetSkillItemGrid(gridKey);
                grid.OnSlotOver = _ => __instance.SingleComposer.GetDynamicText("name").SetNewText(lit[flat].Name);
                if (isChosen) grid.selectedIndex = 0;
            }
            y += slotSize + RowGap;
        }

        __instance.SingleComposer = composer
            .AddDynamicText("", CairoFont.WhiteSmallishText(), ElementBounds.Fixed(0, y, width, 25), "name")
            .EndChildElements()
            .Compose();
        return false;
    }

    private static void OnClick(ICoreClientAPI capi, GuiDialogToolMode dialog, int index)
    {
        Pick(capi.World.Player, index);
        capi.Network.GetChannel(ChannelName).SendPacket(new PickPacket { Index = index });
        dialog.TryClose();
    }

    // Freed once in SidingModSystem.Dispose.
    private static SkillItem[] LoadIcons(ICoreClientAPI capi, string cacheKey, int color) =>
        ObjectCacheUtil.GetOrCreate(capi, cacheKey, () => Rows.SelectMany(row => row.Options)
            .Select(code => new SkillItem { Code = new AssetLocation(code), Name = Lang.Get($"vssiding:toolmode-{code}") })
            .Select(item => item.WithIcon(capi, capi.Gui.LoadSvgWithPadding(
                new AssetLocation("vssiding", $"textures/icons/{item.Code.Path}.svg"), 48, 48, 5, color)))
            .ToArray());

    // Which row and option a flat picker index falls in, rows counted in order. Out of range comes
    // back (-1, -1) rather than throwing, for a stale index from a shorter picker.
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
        string fallback = allowNone ? "" : options[0];
        return player.Entity.WatchedAttributes.GetString(key, fallback) ?? fallback;
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
