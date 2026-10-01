# 0054 — Hanging under thin floors

- Status: Accepted
- Created: 2026-09-30
- Reflects: branch `hanging-under-thin-floors`; `SidingFloorBlock.CanAttachBlockAt`/`CanAttach`/`GetSelectionBoxes`/`WithHangerBoxes`; `SidingModSystem.IsHanger`/`HangShift`/`ShiftTowardWall`/`SpawnShifted`/`DecalTesselationShifted`; `GapShiftCollisionPatches`; decisions 0034, 0035, 0050; vanilla's `AABBIntersectionTest`, `DecorSelectionBox`, `BlockOilLamp` and `SystemSelectedBlockOutline` read from decompiled 1.22 `VintagestoryAPI`, `VSSurvivalMod` and `VintagestoryLib`; a self-review; played once, the rest not yet

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
The vanilla chandelier lists `DOWN` in `attachableFaces` as well, so one the cell below holds up on `UP` is standing, not hung, and stays where it is.

**Mesh and boxes.**
`ShiftTowardWall` adds `HangShiftAt` to `vars.finalY` beside decision 0035's x and z shift, and its early bail-out widens from hostable to hostable-or-hanger.
`GapShiftCollisionPatches` shifts selection and particle collision boxes by the same amount; its cache key and `Shifted` take `(dx, dy, dz)`.
A hung block has no collision boxes at all, as vanilla's oil lamp has none.
A lantern's box is the full cell high, so lifted it reached 1.75 above the room's floor in a 2.75-high room, and the player, 1.85 tall, walked into it.
Its top already meets the underside, so moving it higher was not an option; only shrinking it was.

The flame and the crack overlay follow the block: `SpawnShifted` and `DecalTesselationShifted` add the same lift to decision 0035's x and z.
`BlockOilLamp` sets its particles' position itself instead of calling `Block.OnAsyncClientParticleTick`, so the same transpiler is applied to its override too.

**The raytrace finding.**
Vanilla's `AABBIntersectionTest` walks the ray cell by cell and tests only the current cell's boxes.
A hanger's boxes, lifted 12/16, now sit in the floor's cell, but a ray arriving through that cell tests the floor's boxes, not the hanger's, so it misses.
Shifting the boxes alone leaves the lantern selectable only up close or from below.

The floor has `SideSolid` on `UP`, so the raytrace calls the floor's own `GetSelectionBoxes`.
`SidingFloorBlock.GetSelectionBoxes` therefore returns the hanger's boxes too, moved down one cell into the floor's coordinates, each built as vanilla's internal `DecorSelectionBox` with `PosAdjust` of `(0, -1, 0)`.
A hit on such a box moves the selection to the cell below and reports that cell's block, so the hit selects the lantern.
A hanger hosted in a guest wall's cell (decision 0035) also takes its off-panel x and z shift in this copy.
The type is internal to the API, so it is found and constructed by reflection, and a test fails if the type, its constructor or `PosAdjust` goes missing.
If a game update moves it, the floor answers with its own boxes alone and `SidingModSystem.Start` logs one warning.
The result is cached per (hanger array, floor array, off-panel shift), as raytraces run every frame on several threads.

The floor's own boxes come first and the hanger's after.
Within one cell, vanilla replaces an earlier hit with a later decor box only when the decor box is nearer, but replaces a decor hit with any later ordinary box whatever the distance.
With the hanger's boxes first, a ray from beside the lantern looking up at it went on to meet the floor's underside, and the floor took the hit.
In this order the nearer of the two wins.
`SelectionBoxIndex` then indexes past the hanger's own boxes, which nothing reads for a lantern or chandelier: the outline draws every box unless the block asks for partial selection.

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
- A mod's ceiling block using other mechanisms than the two above hangs, if it asks `DOWN`, but unshifted.
- The floor's outline never draws the hanger's box: `SystemSelectedBlockOutline` skips every `DecorSelectionBox`.
- A player walks through a hung lantern's mesh, as through an oil lamp.
- A shifted block pokes 12/16 into the floor's cell for light and rendering; its light against decision 0034's patches is untested.
- On a game update, the patch-target recheck list gains `DecorSelectionBox.PosAdjust`, `BlockBehaviorUnstableFalling.attachableFaces` and `BlockOilLamp.OnAsyncClientParticleTick`, and the replace rule in `AABBIntersectionTest.RayIntersectsBlockSelectionBox` that the box order relies on.
- Played: a lantern hung under a thin floor in a 2.75-high room, which is how the collision came out as above.
Not yet played: small and large lanterns, an oil lamp (and its flame) and a chandelier (hung, standing on a table under a floor, and adding candles); selecting and breaking each from across the room, from below, and from beside it looking up at its top; the crack overlay while mining one; breaking the floor drops the hanger; peeling the floor's layers keeps it; light under a sealed floor with decision 0034's patches; a lantern under a plank ceiling unchanged.
