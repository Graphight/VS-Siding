# 0055 — Thin floor cell light

- Status: Accepted
- Created: 2026-09-30
- Reflects: branch `thin-floor-cell-light`; `SidingModSystem.SealedCellLightPostfix`, `SidingFloorBlock.IsSealed`; commit `ab56ea9` from decision 0052, revived; decisions 0018, 0034, 0050, 0052; vanilla 1.22 `ChunkIlluminator.CollectLightValuesForLightSource` (decompiled); a playtest on the `hanging-under-thin-floors` branch on 2026-09-30; fix not yet played

## Summary
A chandelier in the room above a sealed thin floor lit the room below it: a fading diamond on the joists and on the tops of the walls just under the ceiling.
The light stored in the floor's own cell is the room above's, and the faces pointing into the floor's open lower part read it.
At render time, a sealed floor's cell now shows the light of the cell below, as decision 0018 does for a sealed wall's cell.

## Context
Decision 0052 tried this rewrite as `ab56ea9` against a different artifact, a black band under the underside, which it did not change, and reverted it as covering no case anyone had seen.
This is that case.
The playtest: breaking the chandelier took the glow away, while re-filling the floor cell under the brightest spot and rejoining the world both left it in place, so the light is stored, not stale.

## Design
**Why the cell holds the light above it.**
Vanilla's block light spread hands a neighbour its light first and subtracts that neighbour's absorption only when the light moves on from it: `light - absorption(cell) - 1` is what the cell passes along.
A sealed floor absorbs 99, so no light gets through it, but the cell still stores what arrived from the room above.
In vanilla a cell like that is an opaque cube whose neighbours' faces are culled against it, so the stored value is never drawn.
A thin floor's cell is 12/16 open air belonging to the room below, and the joists' sides and the walls' faces in that layer point into it.

**The fix.**
`SealedCellLightPostfix`, decision 0018's postfix on `ChunkTesselator.BuildExtendedChunkData`, also rewrites each sealed floor cell's render-time light with the cell below's and marks it, so 0018's flat-path prefix keeps the smooth-lighting ring out of those faces too.
A glazed floor absorbs nothing (decision 0019) and is not sealed by this test, so light still passes through it as before.
A floor cell on a chunk mesh's bottom border layer is skipped: its cell below is in the neighbouring chunk, which has this cell in its interior and rewrites it there.

## Alternatives considered
- **Make the cell store the room below's light.** That means patching vanilla's light spread on the server, which every block in the world goes through.
- **Absorb nothing, like glazing.** Decision 0052 already rejected this: sunlight would pass through every floor and break cellars (0015).

## Consequences & open questions
- The same rewrite would also darken a face that reads the cell from above, but nothing draws one: the floor's own top sits at the cell's top and reads the cell above.
- Not yet played; the playtest is the chandelier room again, with a torch upstairs as a second source, and a glazed floor still passing light.
