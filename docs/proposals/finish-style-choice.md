# Finish style choice

- Status: Draft
- Created: 2026-09-16
- Reflects: planning session on `plan-later-proposals-continued`; decisions 0003 and 0007; `PlaceWallFrame` tool modes and `SidingWallEntity.SelectiveElements` as of 152c8fd

## Summary
Planks gain two more tool modes, `weatherboard` and `boards`, that pick the plank finish's style for the face you click.
The frame modes keep decision 0007's face-based default, so nothing changes for a player who never switches.

## Context
Decision 0003 named the faces `front` and `back` by geometry, not by inside and outside, because the mod can't know which side of a wall is indoors.
Decision 0007 then gave planks per-face geometry: weatherboard on the front, flat boards on the back.
That quietly assumes the front is outdoors.

For an outer wall built from outside, it is.
For an interior partition, both faces are indoors, and one of them gets weatherboard.
For an outer wall built from the room side (the layout that avoids T-junction corners and keeps furniture flush), the front is *indoors*, so the weatherboard lands inside the house and the flat boards outside.
So the default is wrong exactly for the build style the other proposals recommend.

**Detecting rooms doesn't fix it.**
Vanilla only counts a room once it's fully enclosed, roof included, and players finish walls before roofing.
Deciding at finish time would call almost every face exterior.
Deciding live at mesh time would flip a wall's look when a door opens or a roof block breaks, and would make every wall's mesh depend on the room registry.

The player already knows which look they want, and planks already have a tool-mode list.

## Design

**Planks' tool modes become `wall`, `corner`, `weatherboard`, `boards`.**
`PlaceWallFrame` adds the two `SkillItem`s.
`ResolveLayout` currently maps any unknown mode to `wall`; it must return `null` for style modes, and `PlaceWallFrame` places nothing for them, so a style mode never builds a frame by accident.

**Frame modes: unchanged.**
Clicking a finishable face applies planks with the entry's `Elements` default, exactly as decision 0007 does, and anything else places a frame.

**Style modes: finish only.**
Clicking an unfinished face applies the plank finish with that style.
Clicking a face already finished with planks in the other style restyles it, free, since the material doesn't change: an existing partition is fixed without breaking it.
Anything else (air, an end face, a non-plank finish, a frame with no infill) shows the matching ingame error and places nothing.

**State: `FrontStyle` and `BackStyle` on `SidingWallEntity`** (tree keys `frontstyle`, `backstyle`), nullable, `null` meaning the entry's `Elements` default.
Stored per face because the two faces of one wall are the choice being made.
If `cornerout-second-front` has shipped, `SecondFrontStyle` too.
Clearing a face's finish (breaking the wall) clears its style.

**Element names follow `{face}-{style}`,** which the existing groups already do (`front-weatherboard`, `back-boards`).
`SelectiveElements` picks `{face}-{style}` when a style is set, otherwise the `Elements` default.
The shapes gain the two missing groups, `back-weatherboard` and `front-boards`, in `wall.json` and `cornerout.json`, and the block's `ignoreElements` lists gain them too.
`CacheKey` adds both styles.

**Which finishes have styles: an optional `Styles: ["weatherboard", "boards"]` on the finish entry.**
A style mode only matches entries listing that style, so daub and brick ignore style modes, and `material-families`' per-wood plank entries carry it through the template.

## Alternatives considered
- **Detect rooms and choose automatically.** Wrong before the roof is on, and flips looks when rooms change (above).
- **A GUI dialog.** The tool-mode radial *is* the GUI, and it's already on planks.
- **Separate finish entries per style** (`planks-weatherboard`, `planks-boards`). Two entries consuming the same item still shadow each other in `MatchConsumes` without the mode, and `material-families` would double every plank entry.
- **A modifier key to toggle style.** Decision 0006 removed shift from the build flow for good reason.
- **Style modes also place frames.** Then a player in `boards` mode clicking air builds a wall, which is the confusion decision 0007 fixed.

## Consequences & open questions
- Tool modes are stored as an index on the item stack. `window-frames` plans a third frame mode; whichever ships second must append rather than insert, or stacks saved in one mode wake up in another. Worth grouping frame modes before style modes once both exist, accepting one reset.
- Shakes (decision 0017) have the same inside-a-room problem, but logs have no tool-mode behaviour. Add styles there when shakes ship and someone finds them indoors, not now.
- The mode list on planks grows to four (five with windows). Watch whether players find the radial crowded.
- Tests: `SelectiveElements` with each style set and unset, and the style-mode matching as a pure function, whole-array asserts.
