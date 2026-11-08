# 0092 — Under a deck

- Status: Accepted
- Created: 2026-11-08
- Reflects: issue #133; branch `133-butchering`; `SidingWallBlock.CanAttachOf`/`GetSelectionBoxes`; `SidingModSystem.HangShift`; `SidingFloorBlock.WithHangerBelow`; `GapShiftCollisionPatches.UnshiftedSelectionBoxes`; decision 0054

## Summary
A hanger in the cell under a decked wall rises 12/16 to the deck's underside, as under a thin floor.
This changes decision 0054, which said a deck needs no lift.

## Context
0054 held that a deck sits inside the wall's own cell and so has no underside to meet.
It does: the deck's slab is the top 4/16 of its cell, so a ceiling hook or lantern in the cell below hangs 12/16 short of it.

## Design
**The wall answers `DOWN` once decked.**
`SidingWallBlock.CanAttachOf(entity, face)` holds the body of `CanAttachBlockAt`, and answers true for `DOWN` on a decked wall.
A diagonal is excluded, because its deck covers only half the cell.

**The lift.**
`HangShift` lifts a hanger when the cell above is a thin floor or a wall whose `CanAttachOf(.., DOWN)` is true, by the same `1 - PanelThickness`.

**Selection.**
The floor's hanger boxes move into `SidingFloorBlock.WithHangerBelow`, which `SidingWallBlock.GetSelectionBoxes` also calls.
The wall is not patched by `EveryOverridePatches`, so the hanger's own patch would shift the boxes a second time; `GapShiftCollisionPatches.UnshiftedSelectionBoxes` raises the depth guard around the call, and the floor uses it too.
Only the converted hanger boxes are cached, keyed on the hanger's array and shift, since the wall builds a new array of its own boxes on every call.

## Known limits
- A rug on a deck drops when something is hosted under it, and again when the wall returns (`BreakAllDecorFast`).
- A hanger's lowered boxes reach into the cell below, and a ray crossing only that cell misses them.
