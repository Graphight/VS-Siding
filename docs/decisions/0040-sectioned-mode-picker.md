# 0040 — Sectioned mode picker

- Status: Accepted
- Created: 2026-09-25
- Reflects: PR #49; `SidingModePicker.cs`; `PlaceWallFrame.cs`; `SidingWallBlock.cs`'s `OnBlockInteractStart`/`HasStyle`; `SidingWallTexSource.cs`; `WallShapeGen.cs`; `wall.json`'s `shakes-{wood}` entry; supersedes 0017's `BackTexture` and 0027's style modes

## Summary
The saw's mode picker is rows of toggles, framing (wall, corner), boards (weatherboard, boards) and logs (shakes, logs), stored on the player rather than the item stack.
A click decides frame or finish from what it hits, and logs get a style choice.
What shipped differs from the proposal in one way: the picker is attached to no item.

## Context
The proposal (`docs/proposals/sectioned-mode-picker.md`) planned a new `CollectibleBehavior` carrying `GetToolModes`/`SetToolMode`, patched onto planks and placed logs, replacing `PlaceWallFrame`'s old per-stack `toolMode` int.
That would have shown the picker only while planks or logs were held, and the picker belongs to the saw in the off hand: it should read the same whatever the main hand holds, empty included.
Vanilla's `GuiDialogToolMode` opens only when the main-hand collectible's `GetToolModes` is non-null, and a click reaches the server as a packet that calls `SetToolMode` on the server's main-hand item.
So a picker keyed to the off-hand saw has to own its opening, its compose and its sync.

## Design
**The picker hangs off the saw's hotkey, not off an item.**
`SidingModePicker.PatchDialog` prefixes `GuiDialogToolMode.OnKeyCombinationToggle` and `ComposeDialog`.
`IsOurs` (`SidingModePicker.cs`) decides whether a prefix takes over: a saw must be in the off hand (`SidingWallBlock.HasSawInOffhand`), and the main-hand item must have no tool modes of its own.
A main-hand item with its own picker, such as a chisel, keeps it; the saw only fills in when nothing else claims the hotkey, including an empty main hand.
Picks reach the server over the mod's own `vssiding-modepicker` channel (`ChannelName`, registered in `Start`/`StartServerSide`), not vanilla's tool-mode packet, because that packet targets the main-hand item.

**Rows, as proposed.**
`SidingModePicker.Rows` is `("vssidingFraming", [wall, corner], AllowNone: false)`, `("vssidingBoards", [weatherboard, boards], AllowNone: true)`, `("vssidingLogs", [shakes, logs], AllowNone: true)`.
Framing always has a choice, defaulting to `wall`; a finish row can be toggled off, meaning "use the entry's `Elements` default".
`ComposeDialogPrefix` draws a titled row per entry, and `LoadIcons` loads each option's icon twice: white for the chosen option, grey for the rest.

**Stored per player, not per stack.**
`Pick` writes to `player.Entity.WatchedAttributes`, run on both client and server the way vanilla runs `SetToolMode`.
Old per-stack `toolMode` ints are never read again: `PlaceWallFrame` no longer stores one, and nothing in `SidingWallBlock` looks for it, so a stack left in a previous build's corner mode comes back reading `wall` (`ChoiceOf`'s fallback).

**What the click hits decides frame or finish, not a mode.**
`PlaceWallFrame.OnHeldInteractStart` reads `SidingModePicker.Layout(byPlayer)` (`wall` or `cornerout`, from the framing row) to place a fresh frame; it no longer branches on a style mode.
`SidingWallBlock.OnBlockInteractStart` matches the held item against infill first, then finish, and refuses only if neither fits the clicked face; with no style-only modes, 0027's "a style mode frames nothing" rule is gone.
The style itself comes from `SidingModePicker.FinishChoices`, which yields the boards/logs row choices in row order; `OnBlockInteractStart` takes the first one the clicked finish's `Styles` lists (`HasStyle`), falling through to the entry's own default when none match or every relevant row is empty.

**`StyleTextures` replaces 0017's `BackTexture`.**
`SidingWallTexSource.ResolveTexture` looks up `entry["StyleTextures"][resolvedStyle]` before falling back to `entry["Texture"]`, keyed by style rather than by face.
`resolvedStyle` is either the caller-supplied style (`entity.FrontStyle`/`SecondFrontStyle`/`BackStyle`) or, absent one, whatever `StyleSuffix` reads off the entry's own `Elements` default for that face (e.g. `back-logs` implies style `logs`).
Keying by style rather than face is what lets `shakes-{wood}` give `front-logs` bark and `back-shakes` shingles instead of the reverse: `wall.json`'s `shakes-{wood}` entry sets `StyleTextures: { logs: "game:block/wood/debarked/{wood}" }` against a bare `Texture` of the shingle top, and `Elements: { front: "front-shakes", back: "back-logs" }`, `Styles: ["shakes", "logs"]`.

**Log geometry, built from helpers.**
`WallShapeGen.cs` builds `back-logs` and `front-logs` from one `Logs` helper, and `back-shakes` by mirroring `FrontShakesElements` across the wall's depth (`MirrorDepthX`), instead of literal element rows.
`shakes-{wood}` in `wall.json` carries `Styles: ["shakes", "logs"]`, giving logs the style choice planks got in 0027.

## Alternatives considered
- **A `CollectibleBehavior` on planks/logs, as proposed.** Vanilla would open and sync it for free, but only while planks or logs are in the main hand, and the picker is the off-hand saw's.
- **A second flat picker on logs, modes 4 and 5** (proposal). Smallest change, but keeps the disagreeing-stacks problem the sectioned picker exists to fix.
- **Headings or collapsible rows via a custom dialog** (proposal). Prefixing vanilla's dialog already gives rows and hover names.
- **Per-stack storage** (proposal). Two stacks of the same planks could disagree, and a logs-row choice would live on a different stack from a boards-row choice.

## Consequences & open questions
- Any main-hand item that later grows its own tool modes automatically takes the hotkey back from the saw's picker (`IsOurs`).
- A game update that renames `GuiDialogToolMode`'s private `capi`/`blockSele` fields or its `OnKeyCombinationToggle`/`ComposeDialog` methods breaks this patch; decision 0020's consumer sweep should cover it when next redone.
