# 0099 — Side AO from the panel side only

- Status: Accepted
- Created: 2026-11-13
- Reflects: branch `corner-ground-shadow`; commits `8e983b0`, `ce76572`; vanilla 1.22 `TCTCache.CalcBlockFaceLight` and `CornerAoRGB` (decompiled); playtested in the reported room on 2026-11-13

## Summary
The ground tile diagonally inside a sealed corner, with a sealed wall on each side, rendered darker than its neighbours with a hard edge.
Side AO now comes only from the faces a panel covers, and the sealed cell's own faces shade only the corners that touch the panel.
The reported room no longer shows the dark tile.

## Context
Decision 0016 made every sealed cell emit side AO toward every quadrant, so a sealed cell's stored light stays out of the smooth-lighting corners of the faces around it.
Decision 0034 kept that and described an emitting ring cell as "swapped for the face's own sample".
That is not what the decompiled `CalcBlockFaceLight` does: the swapped sample is never read.
The real effect of an emitting ring cell is that it is excluded from the corner average, and the corner is then scaled by `CornerAoRGB`.
That scale is `occ` (0.67) when one edge cell of the corner emits.
When both edge cells emit it is `min(occ, 1 - halfoccInverted * clamp(ground.LightAbsorption, 0, 32))`, with `halfoccInverted` 0.0196875, which is 0.37 for a ground block.
The tile diagonally inside a corner between two sealed walls has both edge cells emitting at its nook vertex, so it took 0.37 while the tiles beside it took 0.67 or 1.
Every sealed cell emitted toward every quadrant, its open side included, so that 0.37 fell on ground three quarters of a block from either panel, beside dead-space tiles at a flat 0.67.

## Design
**The sealed-cell mask carries the claimed faces.**
`sealedCells` in `SidingModSystem` holds, per cell, the `BlockFacing` flags of the faces the panel covers, and zero means not sealed.
A wall's flags are `SidingWallBlock.ClaimedFlags`, set once in `OnLoaded` from `ClaimsFace`, because they are read on every face-light call.
Floors and decks claim up.

**The flat path occludes only corners that touch the panel.**
`SealedCellFaceLightPrefix` (0018's flat path) applies `occ` to a corner only when its vertex lies on a claimed face of the sealed cell (`CornerFaces`, read from `CubeFaceVertices.blockFaceVerticesCentered`), or to all four when the cell claims the face toward the drawn one.
The floor in a wall's open part therefore grades from dark at the panel to light at its open edge, and ground against a panel shades as it does beside a full block.

**Side AO is directional.**
`SidingWallBlock.EmitsSideAo` lets a sealed cell emit toward a quadrant only when the flags include a claimed face.
A wall still emits toward a quadrant whose flags name no horizontal face, as it did before; which faces ask with such flags was not traced.
The cheap flag test runs before `IsSealed`, so most quadrants skip the block entity lookup.
`SidingFloorBlock` and the guest postfixes in `GuestLightPatches` use the same rule, with a floor claiming up and a guest taking its wall's claim.
A diagonal claims its corner's faces, so it follows the same rule through `ClaimsFace`.
A `cornerin` post takes no infill and so is never sealed; it emitted nothing before and still does not.

**Both halves ship together.**
Directional AO alone leaves the flat path's uniform `occ` on the open side, and per-corner occlusion alone leaves the ring's emission, so either alone leaves a visible seam.

0016's rule that every sealed cell emits toward every face is superseded by this decision; its purpose of keeping the stored light out of neighbouring corners is unchanged.
0034's statement about the swapped sample is corrected above.

## Alternatives considered
- **Directional AO and deleting `Occlude`.** Ground against a panel would then show no contact shade, unlike the same ground beside a full block.
- **Leaving it as vanilla corner shading.** The 0.37 nook is vanilla's answer for two emitting edges, but here it lands on ground the panel does not touch, which is the reported fault.

## Consequences & open questions
- Played: the reported room, a corner with two sealed walls.
- Not yet played: a panels-in wall, a wall on a chunk border, a hosted cell, a diagonal, a floor and a deck.
- A panels-in wall now leaves the open side's quadrants out of the emission, so its seams may glow differently from 0034's description; check in play.
- `SealedCellLightTests` pins the corner-to-vertex mapping against vanilla's `CubeFaceVertices` and the `EmitsSideAo` truth table.
