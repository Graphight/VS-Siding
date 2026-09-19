# Sealed wall glow

- Status: Accepted
- Created: 2026-09-18
- Reflects: playtest on branch `siding-cellar-strength` at 63ea9ae; vanilla 1.21 `TCTCache.CalcBlockFaceLight`, `ChunkIlluminator`, `WorldChunk.GetLightAbsorptionAt` and `BlockMicroBlock.DoEmitSideAo` (decompiled from `VintagestoryLib.dll`/`VSSurvivalMod.dll`); playtest on branch `sealed-wall-glow` at f87a9d2

## Summary
A sealed siding room reads light 0 at the player's feet, but daylight glowed on its walls, corners and floor edges.
Sealed walls now emit side AO, which stops the walls glowing; the floor edges and corners still glow, and that's the `sealed-cell-light` proposal.

## Context
Decision 0015 found that a sealed wall's own cell stores the sunlight flowing in from outside (about 23), because absorption only cuts the light a cell passes on.
The room registry no longer counts that light as sky, but it's still in the cells, and the renderer reads it.

## Design

**Side AO stops smooth lighting averaging wall cells into corners.**
With smooth shadows on, `TCTCache.CalcBlockFaceLight` checks the 8 cells around each face, and averages the stored light of any cell whose block doesn't emit side AO toward that corner.
`SidingWallBlock` overrides `DoEmitSideAo` and `DoEmitSideAoByFlag`, reading its entity through `IGeometryTester.GetCurrentBlockEntityOnSide` as vanilla's chiselled block does, and emits on every face while sealed.
Side AO is a separate flag from `SideOpaque`/`SideSolid`, so face culling (decision 0002) is unchanged.
Every face, not just the claimed one, because a corner on the far side of the cell reads the same stored light.
Side effect: a sealed wall shades neighbouring corners like any solid block, which looks odd before the roof is on.

**What the playtest showed afterwards.**
The walls stopped glowing, but floor squares along the wall base and the room's corners still glow.
Placing and breaking a block beside a dark square lit the whole row, so the glow is the engine's real light and the dark squares were stale meshes, not the other way round.
Turning the panels to the other side of the cell didn't clear the corners either.
Every `ChunkIlluminator` path reads absorption through `WorldChunk.GetLightAbsorptionAt`, which calls our per-position override, so no light passes through a sealed wall.
What's left is a face whose own light sample is a wall cell: side AO changes what's averaged in from neighbours, never that sample.

**Stale light in rooms built before #16: not fixed.**
Only the playtest save has them, and re-packing a wall's infill relights it.

## Alternatives considered
- **Split test before any code.** Decompiling showed the averaging mechanism, so the fix went first and the playtest doubled as the split test.
- **Redraw after the relight lands (the proposal's stale-meshes fix).** The playtest showed stale meshes were hiding glow, not causing it.

## Consequences & open questions
- The remaining glow needs a sealed cell to hold no light, which means a Harmony patch in the tessellator or the lighting engine; see the `sealed-cell-light` proposal.
