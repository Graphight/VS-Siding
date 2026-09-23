# 0034 — Lighting, round two

- Status: Accepted
- Created: 2026-09-23
- Reflects: branch `lighting-round-two`; commits `6dfb1c9`, `2c66de1`, `f58959b`, `06f6bba`; vanilla 1.22 `TCTCache.CalcBlockFaceLight`, `ChunkTesselator.BuildExtendedChunkData`, `ChunkIlluminator.UpdateSunLight`, `BlockAccessorBase.GetDistanceToRainFall` and `WeatherSimulationSound` (decompiled); playtests in a flat creative world with the clock frozen at noon

## Summary
The lighting-round-two proposal listed five leftovers from decisions 0016, 0018 and 0019.
Playing through them turned up three real bugs, and none was the one the proposal predicted: missing AO on 0018's flat path, a relight that never ran when a wall opened up, and wind that played at full volume from inside a wall's dead space.
All three are fixed.
Side AO stays.

## Context
The proposal was checked against the decompiled tessellator before any code, and that check changed two of its five premises (commit `6dfb1c9`).
Side AO was not dead code, and the doorway and glazing fixes it suggested described things the postfix never does.
So the round became a playtest first, with each finding bisected on throwaway builds against a room left untouched and a clock frozen at noon.

## Design

**Dark squares before the roof: missing AO, not wrong light.**
A partly built room at noon showed stepped dark squares across its floor.
`/sidingroom` read 22 on dark squares, on bright squares and outside alike, so the light values were fine and the fault was in drawing.
Turning all our lighting code off cleared it; side AO alone gave a soft vanilla-like band; the 0018 pair brought the squares back.
A log line in the prefix showed it only ever fired on real wall cells, and a log in the postfix put the squares on a grid.
The "bright" squares were the dead-space floors under the walls, and the "dark" ones were ordinary floor with vanilla's corner AO.
0018's flat path returned the light with no AO at all, so the dead space came out brighter than the floor around it.
The prefix now scales the flat light by `TCTCache.occ` whenever AO is on (`SidingModSystem.Occlude`), which is what vanilla gives any face onto a cell that casts side AO.
A vanilla plank control room shaded its inner corner the same way.
A sealed room still goes fully dark, and `/sidingroom` reads 0 inside it, as in a vanilla room.

**Opening a wall never relit the room.**
`ChunkIlluminator.UpdateSunLight` returns at once when old and new absorption match, and every call we made passed 0 as the old value.
So glazing a sealed wall, or peeling its infill, reported 0 → 0 and nothing relit: the room stayed dark until an unrelated block change nudged it.
Placing infill still worked, because 0 → 99 is a real change.
Packing, peeling and the other-client sync now pass the absorption the old infill had (`SidingWallBlock.MarkAbsorptionChanged`).
This is very likely the "night lighting near glazing looked slightly off" that decision 0019 left alone.

**Wind at the wall edges of a sealed room.**
Wind and rain volume come from `GetDistanceToRainFall`, a flood fill to open sky, not from the room registry.
The fill checks only the block it steps into, never the one it leaves, because nobody stands inside a solid block.
A wall's dead space is walkable, so from there the first step went out through the panel and the wind played at full volume.
A prefix now starts the search from the cell the dead space opens onto, when the start cell is a wall that retains sound, which is the same idea as 0018's light.

**Side AO stays.**
In the smooth path a ring cell that casts side AO is swapped for the face's own sample, so it drops out of the corner average.
0018's postfix gives a sealed cell its open side's light, and with the panels facing in that is daylight.
Side AO is what keeps it out of the room's floor corners.

## Alternatives considered
- **Removing side AO, as the proposal suggested.** It would have passed a test in a panels-out room, where the rewritten light is the room's own, and then glowed in a panels-in one.
- **"The darker of the open side and the room" for the doorway sliver.** The postfix only ever reads a sealed cell's open side, which runs across the wall, so there is no room value to compare with.
- **Stopping sealed cells sampling the glazed cell.** Nothing in either patch samples a glazed neighbour; the real glazing fault was the relight above.
- **Matching each corner's AO instead of a flat `occ`.** The flat factor matched the vanilla control closely enough in play.

## Consequences & open questions
- **Panels-in walls glow at their seams.** A wall's own faces that point up, down or along the run sample the next wall cell, which holds its open side's light, and with the panels facing in that is outdoors. The fix would be a second per-cell value, the per-channel minimum of the open and panel sides, used when the face being lit belongs to a siding wall. Not done: facing the panels in is not the intended way to build.
- **The doorway sliver (0018) was not reproduced.** It may have been the relight bug, if the doorway was made by peeling infill.
- **Double-thickness walls were not tested.** Tracing the code, the two scan orders give the room's light or the room's light minus one, which should not be visible.
- `SealedCellLightTests` pins `TCTCache.occ` and `aoAndSmoothShadows`; `RoomSkylightPatchTests` pins `GetDistanceToRainFall`'s `pos` parameter, which the prefix rewrites by name.
- `GetDistanceToRainFall` also drives rain volume and some entity rendering fades, and those now read a wall's dead space as indoors too.
