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

**Hosting under a deck.**
A decked cell hosts a block that attaches to a wall face (`BlockBehaviorHorizontalAttachable`, such as a wall hook or torch holder) or hangs (`IsHanger`, such as a ceiling hook): `HostsUnderDeck`, indexed by block id as `UnderDeck`.
`OpenPartRefusal(entity, underDeck)` always refuses a step, and refuses a deck unless the block is under-deck, so every other hostable block still gets "Remove the deck first".
Lanterns and chandeliers stay out: vanilla's attach rules have no face for them in a decked cell, so they hang from the deck's underside in the cell below through the lift above.
The guest record is the wall's whole entity tree, so the deck and its layers ride along.
`HostChangePrefix` clears a deck only for a step or a block that is not under-deck, and a dropped guest now drops through `SidingWallBlock.DropsOf`, deck layers included.
`HangShiftAt` returns `-PanelThickness` for an under-deck block whose guest has a deck, which lowers the mesh, boxes, particles and decals by 4/16 so the top meets the deck's underside.
`GuestSealingPatches` answers UP retention and UP/DOWN attachment for a decked guest through `RetentionOf` and `CanAttachOf`, and the deck stays walkable and restored with the wall.
A hosted deck cannot be peeled or changed until the hosted block is removed.

## Known limits
- A rug on a deck drops when something is hosted under it, and again when the wall returns (`BreakAllDecorFast`).
- A hanger's lowered boxes reach into the cell below, and a ray crossing only that cell misses them.
