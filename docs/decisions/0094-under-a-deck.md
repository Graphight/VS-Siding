# 0094 — Under a deck

- Status: Accepted
- Created: 2026-11-08
- Reflects: issues #133 and #164; branch `133-butchering`; `SidingWallBlock.CanAttachOf`/`GetSelectionBoxes`/`OpenPartRefusal`/`TryHost`/`TryRaise`/`DropsOf`/`RetentionOf`; `SidingModSystem.HangShift`/`HangShiftAt`/`UnderDeckDropFor`/`PlacedVariant`/`HangsOnSide`/`DeckedCellPostfix`/`MultiblockFootprintPostfix`/`HostChangePrefix`; `SidingFloorBlock.WithHangerBelow`/`WithHanger`/`OnNeighbourBlockChange`; `SidingFloorEntity.RaisedBelow`; `GapShiftCollisionPatches.UnshiftedSelectionBoxes`; `GuestSealingPatches`; `patches/compat/butchering.json`; decisions 0035, 0042, 0054; vanilla's `BlockGroundAndSideAttachable` and `BlockBehaviorOmniAttachable` read from decompiled 1.22 `VSSurvivalMod`

## Summary
A deck's underside works like a thin floor's: a hanger in the cell below rises to it.
A decked wall's cell also hosts any block small enough to fit under its deck, which keeps the deck, drawn, sealing and walkable.
The same blocks go on a wall's outer face just under a thin floor beside it, from the cell below the floor.
This changes decision 0054, which said a deck needs no lift, and decision 0042, under which a decked cell hosted nothing.

## Context
0054 held that a deck sits inside the wall's own cell and so has no underside to meet.
It does: the deck's slab is the top 4/16 of its cell, so a ceiling hook or lantern in the cell below hangs 12/16 short of it.
A decked cell refused every hosted block because a guest record kept no deck.
It does keep one: `GuestWalls` stores the wall's whole entity tree, and `HostChangePrefix` cleared the deck on purpose.
Butchering's hooks (#133) and a playtest's oil lamps and torches (#164) both needed the space under a deck.

## Design
**The wall answers `DOWN` once decked.**
`SidingWallBlock.CanAttachOf(entity, face)` holds the body of `CanAttachBlockAt`, and answers true for `DOWN` on a decked wall.
A diagonal is excluded, because its deck covers only half the cell.

**The lift.**
`HangShift` lifts a hanger when the cell above is a thin floor or a wall whose `CanAttachOf(.., DOWN)` is true, by the same `1 - PanelThickness`.

**Selection.**
The floor's hanger boxes move into `SidingFloorBlock.WithHangerBelow`, which `SidingWallBlock.GetSelectionBoxes` also calls.
`WithHanger` answers only for a raised block (a positive shift); a block lowered under a deck stays in its own cell.
The wall is not patched by `EveryOverridePatches`, so the hanger's own patch would shift the boxes a second time; `GapShiftCollisionPatches.UnshiftedSelectionBoxes` raises the depth guard around the call, and the floor uses it too.
Only the converted hanger boxes are cached, keyed on the hanger's array and shift, since the wall builds a new array of its own boxes on every call.

**What fits under a deck.**
A hostable block fits when its boxes span no more than the 12/16 under the deck, top to bottom.
It drops by however far its top rises past 12/16, so its top meets the deck's underside: a torch holder (2/16 to 14/16) drops 2/16, a wall torch about 2.4/16, and an oil lamp or a standing lantern on the floor not at all.
A shelf, a chest, a hanging lantern, a chandelier or a large wall lantern does not fit.
`UnderDeckDropFor` computes the drop once per block id into `UnderDeckDrop`, null for a block that does not fit.
A block whose boxes overstate its shape can declare `vssidingUnderDeck`: Butchering's two hooks have boxes a full cell high and hooks hanging from their top, so the compat patch declares them, and they drop 4/16.

**Which variant is judged.**
A torch (`BlockGroundAndSideAttachable`), lantern or oil lamp (`BlockBehaviorOmniAttachable`) is held standing, and its placement asks `CanPlaceBlock` of the held variant before it picks the variant for the clicked face.
`PlacedVariant` names that variant for the clicked face, and both refusal points judge it: `TryHost` for a click on the open side, and `DeckedCellPostfix` for every other placement path.
`OpenPartRefusal(entity, underDeck)` always refuses a step, and refuses a deck unless the block fits, with "Remove the deck first".
`MultiblockFootprintPostfix` asks it of every cell in a multiblock's footprint, not only the one clicked.

**Keeping the deck.**
`HostChangePrefix` clears a deck only for a step or a block that does not fit, judged on the block actually placed, and a dropped guest drops through `SidingWallBlock.DropsOf`, deck layers included.
`HangShiftAt` returns the negative drop for a fitting block whose guest has a deck, which lowers the mesh, boxes, particles and decals together.
`GuestSealingPatches` answers UP retention and UP/DOWN attachment for a decked guest through `RetentionOf` and `CanAttachOf`.
The guest's collision boxes carry the deck through `PanelCollisionBoxes`, as the wall's do, so it stays walkable.
A hosted deck cannot be peeled or changed until the hosted block is removed.

**Under a thin floor beside the wall.**
Past a wall's outer face the next cell can hold a thin floor, whose slab is that cell's top 4/16 but whose block fills the cell, so a torch clicked onto the wall there has nowhere to go.
`SidingWallBlock.TryRaise` takes that click on a horizontal face `HostsClick` leaves alone, when the held block's `PlacedVariant` fits under a deck and hangs on a side (`HangsOnSide`), the cell below the floor is free and not a wall, and the wall below the clicked cell takes it on that face.
It places the block in the cell below the floor, on that lower wall, and sets `SidingFloorEntity.RaisedBelow`, which saves and reaches clients with the floor's block entity.
`HangShiftAt` lifts a fitting block under a floor carrying the flag by `1 - UnderDeckDrop`: where a decked cell would hold it, one cell up, its top meeting the floor's underside.
The floor's `WithHangerBelow` makes it selectable, as it does a hanger.
The flag marks the one block placed this way rather than a rule of the geometry, so a torch already on the wall below a floor keeps its height.
The floor clears it in `OnNeighbourBlockChange` once the cell below holds nothing that fits under a deck, and it goes with the floor; either way the next block there sits at its own height.
`TryRaise` runs ahead of the build-signal check, so a saw in the off hand changes nothing, and places only on the server, as `TryHost` does.
The server sends each tick's block changes before its block entities, so the clicking client sets the flag itself as it answers the click, and draws the block raised from its first frame; the server sends the floor back whether the placement took or not.

## Known limits
- A rug on a deck drops when something is hosted under it, and again when the wall returns (`BreakAllDecorFast`).
- A declared block's boxes reach below its cell after the drop (Butchering's hooks by 4/16), and a ray crossing only the cell below misses them.
- A hanger under a hosted decked cell rises, but is selectable only from its own cell: the host's patched selection does not add its boxes.
- A torch, lantern or oil lamp that cannot attach on the clicked face tries the others; if the variant it lands on does not fit, the deck drops with it.
- Particles a block entity spawns itself (the hooks' blood) and a block drawn by its own renderer do not follow the drop.
- Walking on a hosted deck rests on reading the code; no scenario walks on one.
- A block raised under a thin floor takes the cell below the floor, which holds nothing else, and its light starts in that cell.
- A raised block hangs on the wall below the one clicked: breaking that wall drops it, breaking the clicked one leaves it in place.
- Only a siding wall's outer face sends a click below; a stone or plank wall beside a thin floor does not.
- An edit that skips neighbour updates leaves `RaisedBelow` set, and the next fitting block placed below rises.
- Another player's client gets the raised block before the floor's flag, and draws it in the cell below for a frame or two.
