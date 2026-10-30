# Guest furniture collision

- Status: Accepted
- Created: 2026-10-23
- Reflects: decisions 0035, 0036, 0049; `GapShiftCollisionPatches.cs`; `SidingModSystem.PanelThickness`/`FaceShift`/`GapShiftAt`; decompiled 1.22.7 `CollisionTester.GenerateCollisionBoxList`; branch `modded-furniture-hosting`; `VSSiding.Tests/GapShiftCollisionPatchesTests.cs`; not played

## Summary
A hosted chest or trunk collides on its sides but lets a player walk in through its front (decision 0036, cause not investigated then).
The off-panel shift pushes the furniture's collision box past its own cell into the room-side cell, and vanilla never asks a cell the player is not already in for boxes.
The fix clamps a hosted block's shifted collision boxes to its own cell.

## Context
`GapShiftCollisionPatches.Shift` moves every box of a hosted block by `GapShiftAt`.
The shift is `PanelThickness` (4/16) less the block's own inset on the panel's face (`FaceShift`).
A chest's boxes stand about 1/16 off each face, so it shifts about 3/16, and its front face ends about 2/16 into the room-side cell.

Vanilla's `CollisionTester.GenerateCollisionBoxList` walks only the cells between `(int)(entityBox + motion)` corners: the cells the player's box already overlaps, stretched by this tick's motion.
While a player walks up to the chest from the room, their box is wholly inside the room-side cell, the chest's cell is never walked, and the protruding 2/16 of chest is not there.
By the time their box crosses into the chest's cell, it already overlaps the chest's box, and nothing pushes it back out.

From the side, along the wall run, the box does not stick out of its cell, since the shift is perpendicular to the wall.
The player's box reaches the chest's cell before it reaches the box, so the side collides.
That is the symptom decision 0036 recorded.

The panel's own boxes are not the cause.
Decision 0035 has the panel collide only on its framing, and those boxes sit inside the wall's cell.

## Design
**Clamp shifted collision boxes to the cell.**
In `ShiftAndAppendPanel`'s collision and particle-collision paths, each shifted box is clipped to 0..1 on x and z before it is combined with the panel.
A block that was not shifted is left alone.
Selection keeps the unclipped boxes, since the selection raytrace follows the ray through every cell it crosses rather than the cells the player stands in, and decision 0036's playtest found selection right.
The clamped copies are cached per shifted array in `ClampCache`, as the shifted copies are in `ShiftCache`, so the per-tick cost stays one dictionary lookup.
A `CuboidfWithId` keeps its `Id` through the clamp.

The player then stops at the cell boundary, 2/16 inside the chest's drawn front.
That is the price: a small visual overlap in exchange for a collision the tester can see.

## Alternatives considered
- **Have the room-side cell answer the protruding box.** That cell is usually air, so this means patching air's `GetCollisionBoxes`, which runs for most of the world on every physics tick.
- **Widen `GenerateCollisionBoxList`'s walk by one cell.** Fixes every overhang at once, but it is a transpiler on the hottest loop in entity physics, for every entity, to fix one block family.
- **Shift less, so the box stays in its cell.** The shift is what gets the furniture clear of the panel (decision 0035); a chest that fits in 13/16 of a cell would have to shrink.

## Consequences & open questions
- A player pressed against a hosted chest's front stands 2/16 inside its drawn model.
- Trunks follow the same path through the filler (decision 0036), so both of their cells clamp.
- A bed's foot overhangs its cell the same way (decision 0049), and its box is cut at its cell edge.
- Other entities (drifters, dropped items) walk the same `CollisionTester` and get the same fix.
- Not played. To check: walk into a hosted chest and a hosted trunk from the room, from each end of the run, and from a diagonal, and check selection still reaches the chest's full front. No Atlas scenario covers it, since a player walk against collision boxes is not cheap to assert headless.
