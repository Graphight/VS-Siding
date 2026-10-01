# 0054 — Hanging under thin floors

- Status: Accepted
- Created: 2026-09-30
- Reflects: branch `hanging-under-thin-floors`; `SidingFloorBlock.CanAttachBlockAt`/`CanAttach`/`GetSelectionBoxes`/`WithHangerBoxes`; `SidingModSystem.IsHanger`/`HangShiftAt`/`ShiftTowardWall`; `GapShiftCollisionPatches.Shifted`; decisions 0034, 0035, 0050; vanilla's `AABBIntersectionTest` and `DecorSelectionBox` read from decompiled 1.22 `VintagestoryAPI`; not yet played, built unattended

## Summary
A thin floor's underside sits 12/16 above its cell's bottom, so a lantern or chandelier hung from it was refused or floated.
This lets one hang, and shifts the hung block's mesh, boxes and selection up 12/16 to meet the real underside.

## Context
A block hangs from the `DOWN` face of the block above it, in the cell below.
Under a thin floor that face is open air: the floor's panel is at the top of its cell.
Answering `CanAttachBlockAt` for `DOWN` alone would let the lantern hang from nothing, 12/16 below the ceiling.
A deck (decision 0042) has no such underside: it sits inside the wall's own cell, so this only matters for the thin floor.

## Design
**The floor answers `DOWN` once framed.**
`SidingFloorBlock.CanAttachBlockAt` returns true for `DOWN` as soon as the floor has framing, so bare joists are enough.
`UP` is unchanged: standing on the floor still needs a seal (`ComputeRetention`), as decision 0050 set.

**Which blocks hang.**
`SidingModSystem.IsHanger` picks them out of the block list into a per-block-id table beside `Hostable`, read by `IsHangerId`.
Two vanilla mechanisms count:
- `BlockBehaviorOmniAttachable` whose variant at `facingCode` is `down`: lanterns and oil lamps.
- `BlockBehaviorUnstableFalling` with `UP` in its private `attachableFaces`, read with `AccessTools.FieldRefAccess`: the chandelier.

`HangShiftAt(pos, block)` is `1 - PanelThickness`, which is 12/16, when the block is a hanger and the block above is a `SidingFloorBlock`, and zero otherwise.

**Mesh and boxes.**
`ShiftTowardWall` adds `HangShiftAt` to `vars.finalY` beside decision 0035's x and z shift, and its early bail-out widens from hostable to hostable-or-hanger.
`GapShiftCollisionPatches` shifts collision and selection boxes by the same amount; its cache key and `Shifted` take `(dx, dy, dz)`.

**The raytrace finding.**
Vanilla's `AABBIntersectionTest` walks the ray cell by cell and tests only the current cell's boxes.
A hanger's boxes, lifted 12/16, now sit in the floor's cell, but a ray arriving through that cell tests the floor's boxes, not the hanger's, so it misses.
Shifting the boxes alone leaves the lantern selectable only up close or from below.

The floor has `SideSolid` on `UP`, so the raytrace calls the floor's own `GetSelectionBoxes`.
`SidingFloorBlock.GetSelectionBoxes` therefore returns the hanger's boxes too, moved down one cell into the floor's coordinates, each built as vanilla's internal `DecorSelectionBox` with `PosAdjust` of `(0, -1, 0)`.
A hit on such a box moves the selection to the cell below and reports that cell's block, so the hit selects the lantern.
The type is internal to the API, so it is found and constructed by reflection, and a test fails if the type, its constructor or `PosAdjust` goes missing.
The hanger's boxes come first, so `SelectionBoxIndex` names the hanger's own box.
The result is cached per (hanger array, floor array) pair, as raytraces run every frame on several threads.

`GapShiftCollisionPatches` keeps a depth guard so an override calling its base shifts once.
The floor reads the hanger's boxes from inside its own override, so the hanger's patch sees a depth above one and leaves them unshifted; the floor applies the shift itself.

## Alternatives considered
- **Refuse hanging.** Honest and free, and where it stood before this.
- **A drop rod drawn by the floor, with no shift.** Smaller, but a lamp hangs 12/16 lower than under a plank ceiling, so the same lantern sits at two heights depending on the ceiling.
- **Shift without the floor-side boxes.** Smaller still, but the raytrace finding above makes the lantern selectable only up close or from below.
- **Put the floor at the bottom of its cell.** Moves the problem to rugs and furniture on top (see decision 0050).

## Consequences & open questions
- Vines and hanging lichen also ask `DOWN` of the block above, so they now hang from a framed floor.
They are not hangers in the table, so they draw unshifted.
- A chandelier standing on the ground in a one-high room under a floor is also a hanger above a thin floor, and is lifted too.
- A mod's ceiling block using other mechanisms than the two above hangs, if it asks `DOWN`, but unshifted.
- Open: when the floor itself is selected, its outline may now also draw the hanger's box.
Vanilla keeps decor boxes in `GetBlockIntersectionBoxes`, not `GetSelectionBoxes`, and the highlight renderer was not read.
- A shifted block pokes 12/16 into the floor's cell for light and rendering; its light against decision 0034's patches is untested.
- On a game update, the patch-target recheck list gains `DecorSelectionBox.PosAdjust` and `BlockBehaviorUnstableFalling.attachableFaces`.
- Not yet played; this ran unattended.
The playtest: small and large lanterns, an oil lamp and a chandelier (hanging, and adding candles); selecting and breaking each from across the room and from below; breaking the floor drops the hanger; peeling the floor's layers keeps it; walking into one; light under a sealed floor with decision 0034's patches; a lantern under a plank ceiling unchanged; and the floor's outline when the floor itself is selected.
