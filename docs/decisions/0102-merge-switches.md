# 0102 — Merge switches for glass and vent sheets

- Status: Accepted
- Created: 2026-11-16
- Reflects: mod page comment https://mods.vintagestory.at/vssiding#cmt-263536; branch `glass-merge-switches`; `SidingModePicker.Rows` (`vssidingMerge`), `Unmerged`, `ToggleSwitch`; `SidingWallBlock.Merges`/`ContinuesGlazing`/`NeighbourJoins`/`InnerCorners`; `SidingFloorBlock.Joins`/`Continues`/`InnerCorners`; the `InfillUnmerged` fields on `SidingWallEntity` and `SidingFloorEntity`; `MergeScenarios`; decisions 0019, 0040, 0071, 0080, 0100

## Summary
A picker row, Merge, holds two switches, up and down and side to side.
Glass or a vent laid while one is off gets bars on that axis instead of joining its neighbours.
Both lit, the default, is today's behaviour: a sheet merges every way.

## Context
A player wanted glass panes that do not merge into one large sheet, to build a greenhouse with bars between the panes.
Until now any glazed or vent cell merged with every neighbour of its own sheet kind (`ContinuesGlazing`, `SidingFloorBlock.Continues`; decisions 0019, 0071, 0100), with no way to ask for a frame between two panes.

## Design
**Two switches, not four options.**
The row has `updown` and `leftright`, each lit or off on its own, so the four settings (merge all, no vertical, no horizontal, none) come from two clicks.
The setting is two independent bits, and two switches show it that way: a lit arrow is a direction the sheet merges.
A row of four exclusive options (all, horizontal, vertical, none) would need four icons for the same two bits, and a three-icon row with nothing lit as merge all would light the restriction rather than the merge.

**The row stores what is off.**
The player attribute `vssidingMerge` is the off switches, comma-separated in `Options` order: `""`, `updown`, `leftright`, `updown,leftright`.
An empty value is merge all, so no existing player or saved cell needs a migration.
`ToggleSwitch` adds or removes the clicked option, and `IsLit` lights an option that is not in the list.
`FinishChoices` leaves the row out, since it is not a finish style.

**The setting is stamped on the cell, on infill laid as a sheet.**
When glass or a vent is laid, `SidingModePicker.Unmerged` is copied onto the entity as `InfillUnmerged` (a wall's deck and outer deck carry `DeckInfillUnmerged` and `OuterDeckInfillUnmerged`, read through `DeckUnmerged`).
Any other infill stores null, and peeling the infill clears it.
Both sheet kinds, glass and vents, take the setting.
The value is in each `MeshState`, so a cell redraws when it changes.

**Two cells join only where both allow it.**
`SidingWallBlock.Merges(unmerged, axis)` answers per cell, and a join needs `Merges` true for the cell and for its neighbour on that axis.
One pane laid with "none" inside a merge-all sheet therefore gets a frame on every side, and the neighbours' edges facing it get their rims.

**Axes.**
On a wall, `updown` is vertical and `leftright` is along the run: a wall's two ends, a corner's leg ends, a diagonal's run (`GlazedDiagonalAt`, `GlassRunsOn`).
On a floor, a wall's deck and a diagonal's outer deck, `updown` is north to south and `leftright` is east to west, the way `Joins` already names a floor's four sides.
Joists still continue between neighbouring floors regardless; only the sheet's merge is switched.

**Inside-corner fillers.**
`InnerCorners` decides whether a filler sits where the cell diagonally across carries no glazing, from checks of the cell beside against the cell across.
Those checks use the flags of the cell beside, not of the clicked cell, so the filler follows the joins the drawn neighbours make.

**No restamp click.**
To change a laid sheet, peel the layer and lay it again.
A click that rewrote the flags would need its own feedback, tooltip text and claim test, for a setting players choose while building.
The tooltip is unchanged.

## Alternatives considered
- **A world or config setting.** Rejected: one greenhouse wants bars and the house beside it wants a sheet.
- **A block variant per setting.** Rejected for the reason decision 0001 gives: it multiplies blocks, and renames every code already in saved worlds.
- **OR instead of AND.** Rejected: a pane laid with bars would then be joined by a neighbour that allows merging, and the setting would not do what it says.
- **A restamp click on a laid sheet.** Left out; see above.

## Consequences & open questions
- Mixed settings around one point can leave a bar ending against open glass, or an inside-corner filler that looks odd. The fillers keep the earlier rules, fed the new join answers; any such stub found in play is a known limit, not reworked here.
- A cell laid before this change has no value and merges every way.
- Peeling and re-laying costs the infill again to change one pane's setting.
- The row is stored per player and read when a layer is laid, so another player's setting does not alter a pane already laid.
