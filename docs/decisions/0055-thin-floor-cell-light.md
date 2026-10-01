# 0055 — Thin floor cell light

- Status: Accepted
- Created: 2026-09-30
- Reflects: branch `thin-floor-cell-light`; `SidingModSystem.SealedCellLightPostfix`, `SidingFloorBlock.IsSealed`/`DoEmitSideAo`/`DoEmitSideAoByFlag`; commit `ab56ea9` from decision 0052, revived; decisions 0016, 0018, 0034, 0050, 0052; a self-review; vanilla 1.22 `ChunkIlluminator.CollectLightValuesForLightSource` (decompiled); first seen and played on 2026-09-30, on the `hanging-under-thin-floors` branch

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
The mask only reaches a face whose own neighbour is the floor cell.
Smooth lighting also averages in the cells ringing that neighbour, and the cell under a wall or chest standing upstairs is the floor cell, so a lit room below would brighten the bottom corners of everything above.
A sealed floor emits side AO, as a sealed wall does (decision 0016), so those corners take ambient occlusion from the floor cell instead of its light.
A glazed floor absorbs nothing (decision 0019) and is not sealed by this test, so light still passes through it as before.
A floor cell on a chunk mesh's bottom border layer is skipped: its cell below is in the neighbouring chunk, which has this cell in its interior and rewrites it there.

## Alternatives considered
- **Make the cell store the room below's light.** That means patching vanilla's light spread on the server, which every block in the world goes through.
- **Absorb nothing, like glazing.** Decision 0052 already rejected this: sunlight would pass through every floor and break cellars (0015).

## Consequences & open questions
- A face reading the floor cell from above, such as the bottom of a block standing on the floor, now takes the room below's light; it rests on the floor's top, where it is not seen.
- With side AO, faces in the room below whose ring reaches the floor cell shade toward the ceiling, as under a vanilla plank ceiling.
- Played: neither the chandelier nor a torch in the room above lights the joists or the wall tops below the floor, and a glazed floor still passes light.
With a torch in the room below and the room above dark, nothing upstairs glows at its base, and the ceiling below shades like a plank ceiling.
