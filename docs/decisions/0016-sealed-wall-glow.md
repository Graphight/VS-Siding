# Sealed wall glow

- Status: Accepted
- Created: 2026-09-18
- Reflects: playtest on branch `siding-cellar-strength` at 63ea9ae; vanilla 1.21 `TCTCache.CalcBlockFaceLight` and `BlockMicroBlock.DoEmitSideAo` (decompiled from `VintagestoryLib.dll`/`VSSurvivalMod.dll`); graduated on branch `sealed-wall-glow` at 96de0e6

## Summary
A sealed siding room reads dark at the player's feet, but daylight glows visibly at the corners and roof line.
Decompiling showed the cause is smooth lighting, and emitting side AO fixes it.

## Context
Decision 0015 found that a sealed wall's own cell stores the sunlight flowing in from outside (about 23), because absorption only cuts the light a cell passes on.
The room registry no longer counts that light as sky, but it's still in the cells.
A playtest confirmed the glow at corners is the visible symptom.

## Design

**Decompiling settled the proposal without a split test.**
`TCTCache.CalcBlockFaceLight` checks the 8 cells around each face; for each one whose block doesn't emit side AO toward that corner (checked via `DoEmitSideAoByFlag`), it averages that cell's stored light into the corner.
Our walls emitted none, so the ~23 sunlight stored in each sealed wall cell leaked into corners of neighbouring floor, ceiling, and roof faces.

**Fix: sealed walls emit side AO.**
`SidingWallBlock` overrides `DoEmitSideAo(caller, facing)` and `DoEmitSideAoByFlag(caller, vec, flags)`, reading the wall's entity through `IGeometryTester.GetCurrentBlockEntityOnSide`, exactly as vanilla's `BlockMicroBlock` does.
Return true on every face when the entity is a `SidingWallEntity` and `ComputeLightAbsorption(entity.Framing, entity.Infill, Attributes["Framings"], Attributes["Infills"]) > 0`; fall back to `base` otherwise.
Side AO is a separate flag from `SideOpaque`/`SideSolid`, so face culling (decision 0002) is unchanged.

**Side effect: sealed walls now cast ambient-occlusion shading on neighbouring corners like any solid block, including across the wall cell's empty 3/4.**

**Stale light in rooms built before #16: deliberately not fixed.**
Only the playtest save has them, and re-packing a wall's infill relights it.

## Alternatives considered
- **Stop the wall's cell storing light.** Not possible; the engine sets the stored light from the neighbour, and nothing on the block can veto it.
- **Split test before any code.** Rejected once the decompiled code showed the mechanism; the playtest after the fix doubles as the split test.

## Consequences & open questions
The in-game playtest is still pending.
If glow that a nearby place/break clears remains, stale meshes are also real (`OnInfillChanged` redraws before the queued `MarkAbsorptionChanged` relight runs) and need a follow-up redraw after the relight.
