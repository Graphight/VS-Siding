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
`sealedCells` in `SidingModSystem` holds, per cell, the `BlockFacing` flags of the faces the cell's layers cover, and zero means not sealed.
For a wall that is `SidingWallBlock.SealedFlags`: the faces `ClaimsFace` answers, worked out once per block, plus up when the cell carries a deck or an outer deck, since a deck is entity state, not part of the block.
A floor claims up.

**The flat path occludes only corners that touch the panel.**
`SealedCellFaceLightPrefix` (0018's flat path) applies `occ` to a corner only when its vertex lies on a claimed face of the sealed cell (`CornerFaces`, read from `CubeFaceVertices.blockFaceVerticesCentered`), or to all four when the cell claims the face toward the drawn one.
The floor in a wall's open part therefore grades from dark at the panel to light at its open edge, and ground against a panel shades as it does beside a full block.

**Side AO is directional.**
A sealed cell emits toward a quadrant only when its flags share a face with `SealedFlags`.
In `CubeFaceVertices` every ring cell's quadrant flags name the ring cell's face toward the drawn face's front cell plus a tangent, so a horizontal face always appears; for the cell above an east face they are `N|D` and `S|D`.
The one call with up or down alone is `DoEmitSideAo` on the front cell of an up or down face, and `SealedCellFaceLightPrefix` has already answered for a sealed front cell, so a ring cell above or below a wall now emits only on the halves its panel touches.
`SidingFloorBlock` and the guest postfixes in `GuestLightPatches` use the same rule, with a floor claiming up and a guest taking its wall's flags.
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
- Played: the reported room, a panels-in wall, a wall on a chunk border, a hosted cell, a diagonal, a thin floor and a deck.
- A deck's up flag was added after that playtest, from review: a block standing beside a decked wall in the room above has not been looked at.
- `SealedCellLightTests` pins the corner-to-vertex mapping against vanilla's `CubeFaceVertices`, `SealedFlags` with and without a deck, and the overrides' answers for vanilla's real ring and front-cell flags.
- `SealedCellFaceLightPrefix`'s own corner choice has no unit test, since it needs a live `TCTCache`.
