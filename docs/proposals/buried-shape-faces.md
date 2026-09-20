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
That is a geometric test over the element list, so it can be a build-time check or a generator pass rather than hand edits — hand-pruning 36 elements across two files is exactly how the duplicates in `shake-profile` got in.

**Selective elements complicate it, and that is the interesting part.**
A face is only buried if the element burying it is *drawn*, and `SelectiveElements` picks a different set per cell: a merged glazed cell draws no posts, so `infill-pane`'s edges are exposed there but hidden in an unmerged one.
So pruning is per element *combination*, not per element.
The safe subset is faces buried by an element that is always drawn alongside — `infill` against the posts on a plain wall, for instance.
Anything conditional needs either a per-combination mesh (which the cache already keys) or leaving alone.

## Alternatives considered
- **Prune by hand now.** No measurement, and it re-introduces exactly the hand-maintenance problem `shake-profile` documents.
- **Turn on vanilla face culling.** Decision 0002 turned `sidesolid` off precisely so a thin wall does not cull its neighbours; this is culling *within* one mesh, which vanilla does not do for us.
- **Do it inside the shape generator.** Probably the right home, which is why that generator wants its own proposal first. This one can then become a rule in it.

## Consequences & open questions
- This is a performance proposal with no measurement behind it yet. It may well close as "measured, not worth it", and it should be allowed to.
- The saving scales with how much siding is on screen, so the honest benchmark is a large build, not one wall.
- A per-combination prune multiplies the mesh cache's distinct entries; the cache is keyed on materials and joins already, so check the entry count does not blow up.
