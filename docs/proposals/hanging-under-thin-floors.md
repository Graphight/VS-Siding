# Hanging under thin floors

- Status: Draft
- Created: 2026-09-25
- Reflects: `thin-floor-framing`; decision 0035's off-panel offset and `GapShiftCollisionPatches`; not yet played

## Summary
A thin floor's underside sits 12/16 above the cell boundary, so a lantern or chandelier hung from it is either refused or floats.
The proposal lets it hang and shifts the hung block's mesh, collision and selection up 12/16 to meet the real underside.

## Context
A block hangs from the `DOWN` face of the block above it, in the cell below.
Under a thin floor that face is open air: the floor's panel is at the top of its cell.
Answering `CanAttachBlockAt` for `DOWN` alone would let the lantern hang, but it would hang from nothing, 12/16 below the ceiling.
A deck (decision 0042) has no such underside: it sits inside the wall's own cell, so this only matters for the thin floor.

## Design
**Attach, then shift.**
`CanAttachBlockAt` answers `DOWN` on a thin floor with its framing in place.
A block hung there draws, collides and selects 12/16 higher, the way decision 0035 already shifts a hosted block off the panel: the same offset hook and `GapShiftCollisionPatches` for the boxes.

## Alternatives considered
- **Refuse hanging.** Honest and free, and the fallback until this is built.
- **Put the floor at the bottom of its cell.** Moves the problem to rugs and furniture on top (see `thin-floor-framing`).

## Consequences & open questions
- Which blocks count as hanging (lanterns, chandeliers, hooks) and whether a hanging block's own attachment test reads the face or the box.
- A shifted block pokes 12/16 into the floor's cell for light and rendering; check against 0034's light patches.
