# 0036 — Trunks against thin walls

- Status: Accepted
- Created: 2026-09-24
- Reflects: branch `trunks-against-thin-walls` (commits `b9953a0`, `a85d185`, `a3b653a`, `3e06e7d`); vanilla 1.22 decompiled source

## Summary
A trunk placed along a straight wall run takes both walls' cells, and each wall becomes a guest as in decision 0035.
The controller and its `BlockMultiblock` filler are both hosts.

## Context
Decision 0035 hosts one-cell furniture in a wall's cell and excludes anything with vanilla's `Multiblock` behaviour, because the filler that behaviour drops into the second cell would replace a wall with no guest and lose that wall's layers.
A trunk (`BlockGenericTypedContainerTrunk`) spans two cells this way, and its filler forwards every question to the controller.

## Design
**The footprint rule.** `IsReplacableBy(Block)` has no position, so a wall can only answer for itself, not for its neighbour.
A postfix on `BlockBehaviorMultiblock.CanPlaceBlock` sees the whole footprint instead and refuses (`notenoughspace`) unless every cell is a `layout: wall` sharing one `side` on the trunk's long axis: a north or south trunk spans x, so it needs north- or south-claiming walls; an east or west trunk spans z.
A footprint with no wall in it is left to vanilla; wall plus air, wall plus cornerout, and walls on opposite faces are all refused.
The rule itself is a pure function, `SidingModSystem.FootprintHosts`.

**The filler is a host.** `IsHostable` admits `BlockGenericTypedContainerTrunk` and every `BlockMultiblock` filler; every other `Multiblock` block (paintings, banners, mannequins, machines) stays excluded.
A filler only ever lands on a wall after the footprint rule has already passed for a trunk, so nothing else can slip through.
`HostChangePrefix` needed no change for this: the filler's `SetBlock` over a wall writes its guest exactly as any hostable block's does, and `OnBlockRemoved` setting the filler back to air restores it.

**What turned out free.** The proposal expected work in three places that the shipped code didn't need:
- The filler is `drawtype: json` with shape `nothing`, and `ChunkTesselator` skips only `Empty`, so the filler cell still gets its own `TesselateBlock` call, and the existing postfix carries its guest panel through unmodified — no controller-draws-both.
- The trunk implements `IMultiBlockColSelBoxes`, so the filler's boxes come straight from `MBGetCollisionBoxes` with no nested call for the depth guard to skip.
- The filler forwards tooltip, retention and attachment questions to the trunk, and the existing depth guard or idempotent overwrites leave the filler's own postfix answering for the filler's guest.

**The one fix.** `GapShiftAt` resolves a filler to its controller (`ShiftSource`, `pos + OffsetInv`) and reads the controller's `FaceShiftByBlock` row, since the filler's own default-cube row would shift a full 4/16 where the trunk's 1/16 inset gives 3/16.

**Cabinets, found in the same playtest.** A cabinet's `sidesolid` is top only, and `IsHostable` refused any solid side, a rule meant for full cubes.
A solid top alone is now allowed on a block with a block entity (vanilla gives any block with `entityBehaviors` the `Generic` class), which keeps top slabs, metal sheets and linen out.
A cabinet turns its boxes by its `RotateablePlaceable` block entity's angle, so its default boxes can't say which face meets the wall; such a block takes its largest shift on every face, which is wrong only when its doors face the wall.
Its shelves (`BEBehaviorDisplay`) pick a slot by `BlockSelection.SelectionBoxId`, which the raytrace reads from a `CuboidfWithId`; the box shift in `GapShiftCollisionPatches.Shifted` returned plain `Cuboidf` copies and dropped it, so shifted boxes now keep their id.

## Alternatives considered
- **Leave trunks in the cell in front.** Today's behaviour before this decision: a trunk against a wall stands three quarters off it, the gap decision 0035 set out to close.
- **Host only the controller's cell.** The filler still needs the second wall's cell; half a trunk can't sit off one panel.
- **Allow a trunk across a corner or a change of side.** Two panels would pull one model two ways.
- **Allow a footprint of one wall and one air cell.** Refused: the shift would have to come from whichever cell holds a guest, with no wall to answer for the other.

## Consequences & open questions
- Played: a trunk hosted from both ends of a two-wall run, with both panels drawn and the trunk off them; breaking it restores both walls; `/sidingroom` counts both; the filler's collision matches the model; wall plus air, a corner and a cross-axis wall are refused; cabinets on all four sides sit off the panel with working shelves; a top slab still goes in front.
- Lid direction is not restricted; the player's facing decides it, as for any trunk. Not checked on its own in play.
- Beds stay out: a head/foot `part` pair meeting a wall head-on doesn't fit this shape.
- Tables share the cabinet's solid top but have no block entity, so they stay out.
- Recheck on a game update, alongside decision 0035's list: `BlockBehaviorMultiblock.CanPlaceBlock` (its parameter names are pinned in `HostChangeTests`), the filler's JSON drawtype, and the trunk's `IMultiBlockColSelBoxes`.
