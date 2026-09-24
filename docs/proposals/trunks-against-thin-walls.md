# Trunks against thin walls

- Status: Draft
- Created: 2026-09-24
- Reflects: decision 0035 as shipped on branch `furniture-against-thin-walls` (PR #41), and the decompiled 1.22 `BlockBehaviorMultiblock` and `BlockMultiblock`

## Summary
A trunk placed along a wall's open side takes both walls' cells, and each wall becomes a guest exactly as decision 0035's one-cell furniture does.
The guest machinery already works cell by cell.
What's missing is teaching it that a multiblock's filler cell belongs to its controller.

## Context
Decision 0035 hosts one-cell furniture and scoped multi-cell furniture out of its first version.
A trunk is `BlockGenericTypedContainerTrunk` with vanilla's `Multiblock` behaviour, two cells wide.
The controller cell holds the block and its entity.
`BlockBehaviorMultiblock.OnBlockPlaced` fills the second cell with a `BlockMultiblock` filler (`multiblock-...`) that forwards every question to the controller through its `OffsetInv`: boxes, interaction, breaking, retention, attachment.
`BlockBehaviorMultiblock.CanPlaceBlock` asks each footprint cell's `IsReplacableBy(controller)`.
Letting a trunk through `IsHostable` as it stands would let its filler replace the second wall with no guest and lose that wall's layers, which is why `IsHostable` excludes the behaviour (commit `60650a3`).
The trunk's model spans both cells and is drawn from the controller's cell, and its lid is an `AnimatableRenderer` at the controller's position.

## Design
**Hosting per cell.** A trunk is hostable where every footprint cell is a straight wall (`layout: wall`) and all of them share one `side`, with the trunk's long axis running along that wall.
`trunk-north` and `trunk-south` are two cells along x, so they sit along a north or south wall; `east` and `west` along an east or west wall.
`SidingWallBlock.IsReplacableBy` can check that itself, since it sees the controller's `side` variant and knows its own, so `CanPlaceBlock` passes on both cells or neither.
`HostChangePrefix` then sees the controller replace the first wall and the filler replace the second, each while that wall's entity still stands, and writes a guest for each.
For that, a filler counts as hostable when its controller (at `pos + OffsetInv`) is.

**Breaking.** `BlockBehaviorMultiblock.OnBlockRemoved` sets each filler back to air, and the existing restore brings each wall back.
Only the cell the player aimed at restores in the `TriggerNeighbourBlocksUpdate` prefix; the other goes through the deferred callback, so it may flash for a tick.

**One lookup resolves a filler to its controller.** Every consumer that reads the `Hostable` table on `__instance` today (the offset in `GapShiftAt`, boxes, sealing, light, tooltip, panel interaction) would call one helper instead.
It answers whether a cell is hosted furniture, and whose boxes and offset apply, mapping a `BlockMultiblock` to its controller.
It has to stay a table read before any chunk lookup on the hot paths.
The filler's `GetCollisionBoxes` calls the controller's nested inside it, where the depth guard skips, so the filler's own outermost frame must shift and append its cell's panel.

**Rendering.** Both walls share a side, so the trunk's model and lid shift once, from the controller, by the same distance.
The filler cell draws nothing of its own, so its guest panel has nothing to ride on; the controller's `TesselateBlock` postfix may have to draw both panels, the second offset by the filler's `Offset`.

## Alternatives considered
- **Leave trunks in the cell in front.** Today's behaviour: a trunk against a wall stands three quarters off it, the gap decision 0035 set out to close.
- **Host only the controller's cell.** The filler still needs the second wall's cell; half a trunk in the gap and half in front can't sit off one panel.
- **Allow a trunk across a corner or a change of side.** Two panels would pull the one model two ways; refused rather than solved.

## Consequences & open questions
- Every guest consumer grows a filler-to-controller step, on paths decision 0035 kept to one array read.
- Open: a footprint of one wall and one air cell, where the trunk sticks past the end of a wall run. Allowed, with the air cell holding no guest, or refused?
- Open: whether the filler cell gets any tesselation call its panel could ride, or the controller must draw both.
- Open: which `side` variant faces away from each wall, so the lid opens into the room.
- Beds stay out: a head/foot `part` pair of separate blocks, and a bed usually meets a wall head-on, which puts only one cell in the gap.
- Paintings, banners and mannequins also use `Multiblock` and stay out; they hang or stand rather than sit in the gap.

## Stages
On a new branch from `main` once PR #41 merges.
1. **Spike:** host a trunk along a two-cell wall run; answer the tesselation question, and whether breaking it restores both walls.
2. **Hosting:** the footprint rule in `IsReplacableBy`, guests for the controller and the filler, and a unit test of the rule over every orientation and side.
3. **Filler resolution:** the one helper, and every consumer on the filler cell: offset, boxes, sealing, light, tooltip, panel interaction.
4. **Rendering:** both panels drawn, and the model and lid shifted once.
5. **Graduate** as a new decision extending 0035, and update the handbook's Quirks paragraph.
