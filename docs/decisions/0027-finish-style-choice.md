# 0027 — Finish style choice

- Status: Accepted
- Created: 2026-09-20
- Reflects: branch `finish-style-choice`; `SidingWallBlock.ResolveStyle`, `ResolveLayout`, `HasStyle` and `OnBlockInteractStart`; `SidingWallEntity.FrontStyle`/`SecondFrontStyle`/`BackStyle` and `SelectiveElements`; `PlaceWallFrame`'s tool modes; `WallShapeGen`'s plank groups; decisions 0003, 0005, 0006, 0007 and 0021; the `finish-style-choice` proposal

## Summary
Planks gain two more tool modes, `weatherboard` and `boards`, that pick the plank finish's profile for the face you click.
The frame modes keep decision 0007's face-based default, so nothing changes for a player who never switches.

## Context
Decision 0003 named the faces `front` and `back` by geometry, not by inside and outside, because the mod can't know which side of a wall is indoors.
Decision 0007 then gave planks per-face geometry: weatherboard on the front, flat boards on the back.
That quietly assumes the front is outdoors.

For an outer wall built from outside, it is.
For an interior partition, both faces are indoors, and one of them gets weatherboard.
For an outer wall built from the room side — the layout that avoids the T-junction corners of decision 0026 and keeps furniture flush — the front is *indoors*, so the weatherboard lands inside the house and the flat boards outside.
The default is wrong exactly for the build style the rest of the mod recommends.

**Detecting rooms doesn't fix it.**
Vanilla only counts a room once it's fully enclosed, roof included, and players finish walls before roofing.
Deciding at finish time would call almost every face exterior.
Deciding live at mesh time would flip a wall's look when a door opens or a roof block breaks, and would make every wall's mesh depend on the room registry.

The player already knows which look they want, and planks already have a tool-mode list.

## Design

**Planks' tool modes are `wall`, `corner`, `weatherboard`, `boards`, in that order.**
`ResolveStyle` maps modes 2 and 3 to a style name and everything else to null; `ResolveLayout` returns null for anything `ResolveStyle` claims, so `PlaceWallFrame` bails before placing and a style mode never builds a frame by accident.
`ResolveLayout` still falls back to `"wall"` for a stale or out-of-range mode rather than throwing.
The layout check sits *above* `PlaceWallFrame`'s afford check, or a style-mode click with too few planks would error about a framing cost nobody was being charged.

**Frame modes: unchanged.**
Clicking a finishable face applies planks with the entry's `Elements` default, exactly as decision 0007 does, and anything else places a frame.

**Style modes: finish only, and every refusal is loud.**
`heldPlaces` — the escape hatch that lets planks fall through to `PlaceWallFrame` and held blocks fall through to vanilla placement — is switched off in a style mode, because in a style mode there is nothing to fall through *to*.
So a style-mode click on an end face, a glazed cell, a frame with no infill or an already-finished face shows its existing ingame error instead of silently doing nothing.

**Restyling the same material is free.**
A face already finished with the same finish key in the other style is restyled in place: no material charged, no break, no drop.
Only the profile changes — the boards are already on the wall — and that is what fixes an existing partition without taking it apart.
A face finished with a *different* material still errors: that is a material change, not a restyle.

**State: `FrontStyle`, `SecondFrontStyle` and `BackStyle` on `SidingWallEntity`** (tree keys `frontstyle`, `secondfrontstyle`, `backstyle`), nullable, `null` meaning the entry's `Elements` default.
Stored per face because the two faces of one wall are the choice being made.
Peeling a finish (decision 0013) clears that face's style alongside its key, so a rebuilt finish comes back at the default.

**Element names follow `{face}-{style}`,** which the existing groups already do (`front-weatherboard`, `back-boards`).
`SelectiveElements` picks `{face}-{style}` when a style is set and the entry's `Elements` default otherwise; `secondfront` still prefixes `second` onto the front name, so `boards` on a cornerout's second leg resolves to `secondfront-boards`.
The shapes gained the two missing mirrors — `back-weatherboard` and `front-boards` in both `wall.json` and `cornerout.json`, plus `secondfront-boards` — generated from `WallShapeGen` per decision 0021, and every `ignoreElements` list gained them.
`CacheKey` carries all three styles, or two differently-profiled walls would share one cached mesh.

**Which finishes have styles: an optional `Styles: ["weatherboard", "boards"]` on the finish entry.**
A style mode only matches entries listing that style, so daub and brick refuse it rather than naming an element their shape hasn't got.
`MaterialFamilies.Expand` deep-clones templates, so the per-wood plank family carries `Styles` through untouched.

## Alternatives considered
- **Detect rooms and choose automatically.** Wrong before the roof is on, and flips looks when rooms change (above).
- **A GUI dialog.** The tool-mode radial *is* the GUI, and it's already on planks.
- **Separate finish entries per style** (`planks-weatherboard`, `planks-boards`). Two entries consuming the same item still shadow each other in `MatchConsumes` without the mode, and `material-families` would double every plank entry.
- **A modifier key to toggle style.** Decision 0006 removed shift from the build flow for good reason.
- **Style modes also place frames.** Then a player in `boards` mode clicking air builds a wall, which is the confusion decision 0007 fixed.
- **Style modes fall through silently when they can't act.** A mode that does nothing and says nothing reads as a broken mod; the errors were already written.

## Consequences & open questions
- Tool modes are stored as an index on the item stack. `window-frames` plans a third frame mode; whichever ships second must append rather than insert, or stacks saved in one mode wake up in another. Worth grouping frame modes before style modes once both exist, accepting one reset.
- Shakes (decision 0022) have the same inside-a-room problem, but logs have no tool-mode behaviour. Add styles there when shakes ship and someone finds them indoors, not now.
- The mode list on planks is four (five with windows). Watch whether players find the radial crowded.
- `back-weatherboard` laps outward on +x, mirroring the front profile of decision 0023. Its cornerout second leg recedes from the concave corner, which leaves a hairline notch where the two legs' tapered courses meet; it faces into the corner and reads as a shadow.
- A style is per face, not per wall. Nothing stops weatherboard on both sides of a partition; that is the player's business.
