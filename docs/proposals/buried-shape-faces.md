# Buried shape faces

- Status: Draft
- Created: 2026-09-20
- Reflects: repo-wide audit on branch `window-frames` at fc5caac; `shapes/block/wall/wall.json`; `SidingWallEntity.OnTesselation`

## Summary
Every box in the wall shapes declares all six faces, including faces permanently buried inside the box next to them.
Those faces become real quads in the chunk mesh, drawn every frame, invisible.
Prune them, and measure before and after rather than assuming.

## Context
The shape files were authored box by box with a full face set each — 35 of 36 elements in `wall.json` declare all six.
Many of those faces can never be seen:

- `infill` spans `z 1..15` and its `north`/`south` faces sit flat against the posts at `z 0..1` and `15..16`.
- `infill-top` and `infill-bottom` meet `infill` at `y 15` and `y 1`; both sides of each join are declared.
- The bezel members overlap the stiles deliberately (`window-frames`), so the rail faces inside a stile are drawn and never seen.
- A wall's `front` and `back` finish slabs cover the framing's outer faces whenever a finish is built.

A siding wall is a block entity mesh, so this is not one block's cost: every wall cell in view contributes its quads to the chunk mesh.
A glazed curtain wall is the worst case, since glazing also puts its pane in the transparent pass.

## Design

**Measure first, and say what "better" means.**
No profiling has been done on this mod at all, so the first commit is a measurement, not a change: quad count per wall mesh for a bare frame, a filled wall, a finished wall and a glazed wall, and a frame-time reading in a room built of each.
If the numbers are small, the finding is "not worth it" and that is a result worth writing down.

**Then prune by rule, not by eye.**
Two boxes that share a face plane, where one's face lies entirely within the other's, can both drop that face.
That is a geometric test over the element list, so it belongs in the shape generator decision 0021 added, as a pass over the element table rather than hand edits across two files.

**Selective elements complicate it, and that is the interesting part.**
A face is only buried if the element burying it is *drawn*, and `SelectiveElements` picks a different set per cell: a merged glazed cell draws no posts, so `infill-pane`'s edges are exposed there but hidden in an unmerged one.
So pruning is per element *combination*, not per element.
The safe subset is faces buried by an element that is always drawn alongside — `infill` against the posts on a plain wall, for instance.
Anything conditional needs either a per-combination mesh (which the cache already keys) or leaving alone.

## Alternatives considered
- **Prune by hand now.** No measurement, and it re-introduces exactly the hand-maintenance problem decision 0021 removed.
- **Turn on vanilla face culling.** Decision 0002 turned `sidesolid` off precisely so a thin wall does not cull its neighbours; this is culling *within* one mesh, which vanilla does not do for us.
- **Do it inside the shape generator.** That generator now exists (decision 0021), so this becomes a rule in it rather than a separate mechanism.

## Consequences & open questions
- This is a performance proposal with no measurement behind it yet. It may well close as "measured, not worth it", and it should be allowed to.
- The saving scales with how much siding is on screen, so the honest benchmark is a large build, not one wall.
- A per-combination prune multiplies the mesh cache's distinct entries; the cache is keyed on materials and joins already, so check the entry count does not blow up.

## Measured

Quads declared per built cell, counted before any pruning.
The count is `SelectiveElements` (`VSSiding/SidingWallEntity.cs`) run over each build combination, summing the `faces` entries of the elements it names in the committed shape JSON.
That is the number of quads the tesselator writes into the chunk mesh for one cell, so it is what scales with how much siding is on screen.

| Build state | `wall` | `cornerout` |
| --- | --- | --- |
| bare frame | 24 | 42 |
| frame + wattle | 30 | 54 |
| frame + wattle, mid-stack | 30 | 54 |
| + plain slab finish, both faces | 42 | 78 |
| + weatherboard, both faces | 132 | 258 |
| + shakes, both faces | 252 | 498 |
| glazed, unmerged | 26 | 46 |
| glazed, merged all round | 2 | 22 |

Whole files: `wall.json` declares 404 quads across its 36 elements, `cornerout.json` 754.
No cell draws all of them — the biggest single combination is a shakes-clad `cornerout` at 498.

**The cladding profiles are the whole cost.** A finished wall is 132 or 252 quads against a bare frame's 24, and `front-shakes` alone is 192 of them.
Anything that does not touch the profiles is rounding error.

**And the profiles bury their own faces.** A containment pass over the committed shapes says 62 of `wall.json`'s 404 quads and 130 of `cornerout.json`'s 754 sit flat against a box *of the same element name*: `front-shakes` 192 → 149, `front-weatherboard` 96 → 81, `back-logs` 30 → 26 (60 → 48 on `cornerout`).
Same name means same `selectiveElements` group, so those boxes are always drawn together — no per-combination reasoning is needed for any of it.
A shakes-clad wall goes 252 → 205, a weatherboarded one 132 → 117.

**The burials this proposal named first are the small ones.** `infill` against the posts, `framing-top`/`framing-bottom` against them, the finish slab against the framing — together about 8 quads on a bare wall and 0 on a clad one, and each needs to know whether the burying element is drawn.
So the ordering in the Design section above is backwards: the unconditional same-name rule is where the saving is, and the per-combination reasoning buys almost nothing.

**No frame-time reading was taken.** That needs the game running and a built scene, and the quad counts alone were decisive enough to act on.
It stays the honest end-to-end check if the saving ever needs defending in real frames rather than in quads.
