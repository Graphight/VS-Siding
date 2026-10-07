# Diagonal walls

- Status: Draft
- Created: 2026-10-06
- Reflects: the playtest of decision 0060 on branch `feat/tent-walls`; `SidingWallBlock.CorneroutSecondFace`/`ClaimsFace`/`OpenSide`/`ResolveFinishFace`/`UnrotatedFramingBoxes`/`ComputeCollisionBoxes`, `SidingModePicker.Rows`, `VSSiding.Tests/WallShapeGen`, `blocktypes/wall.json`; decisions 0001, 0002, 0007, 0008, 0009, 0021, 0026, 0035, 0040, 0058, 0060; not built, not played

## Summary
A tent built from these walls is a box, since a wall runs along a cell's face and a corner turns ninety degrees.
The proposal adds a third framing layout, a panel that crosses its cell corner to corner, so a corner can be cut at forty-five degrees and a round tent built as an octagon.
For every rule except its drawing and its boxes it is a `cornerout`.

## Context
Decision 0060 added bone frames and pelt and cloth infills, and the first thing built with them was a tent.
A hide tent is round, and the nearest a grid gives is an octagon: straight runs joined by diagonals.

**A diagonal closes the same two faces a corner does.**
Take a north wall that ends at a cell, and an east wall that starts one cell further on.
A `cornerout` in the cell between them puts a leg on its north face and a leg on its east face.
A diagonal in that cell runs from its north-west corner to its south-east corner, and the room is still to the south-west of it.
Vanilla's room search crosses a cell's faces, and both layouts answer the same two: north and east closed, south and west open.
So retention, the liquid barrier, attachment, `ClaimsFace`, `OpenSide` and the four orientations are a corner's, unchanged.

**What is not a corner's.**
The shape, the collision and selection boxes, the finish faces and what may be hosted in the cell.

**Vanilla boxes do not turn.**
A collision box and a selection box are axis-aligned, so a diagonal is a staircase of small boxes.
A shape element can be rotated about its own origin, but its ends stay square, so a rotated slab cut to the cell either leaves a triangular gap at each end or pokes into the next cell.
`WallShapeGen` emits `from`, `to` and `faces` only: no rotation and no child elements.

## Design
**A `diagonal` layout.**
A third state of the `layout` variant group in `wall.json`, beside `wall` and `cornerout`, with its own shape file `diagonal.json`.
`diagonal-{side}` cuts the corner `cornerout-{side}` would turn, so `CorneroutSecondFace` already names its second face.
Every check that reads `layout == "cornerout"` for a rule the two share moves to one helper that answers for both.
It is four more blocks and no more material entries, since materials are attributes (decision 0001).

**Posts at the two corners, square to the grid.**
The frame is two full-height posts, one in each corner the panel runs between, axis-aligned as a corner's posts are, and a top and bottom plate turned forty-five degrees between them.
The posts cover the square ends of everything turned, so no element needs a mitre.
The infill is one turned slab between the posts.
A straight wall ending at either corner meets a post, as it does at a `cornerout`.

**Turned elements from the generator.**
`WallShapeGen`'s `Element` gains a rotation about a vertical axis and an origin, emitted as vanilla's `rotationY` and `rotationOrigin`.
A diagonal's groups are a straight wall's, laid out along a run of 22.6 voxels in place of 16 and turned as one.
A texture tiles every 16 voxels, so a slab longer than that is two elements.

**One front, one back.**
The outer face is `front` and the inner face is `back`; a click on either claimed face resolves to `front` and on either opposite face to `back`.
There is no `secondfront` (decision 0009), since the panel has one outer face.

**Finishes are flat on a diagonal, at first.**
A finish's `Elements` (decision 0007) name groups built for a 16 voxel run on a face.
The first cut draws every finish on a diagonal as a flat slab in its texture, as `plate-{metal}` and `rammed-{pattern}` are drawn everywhere.
Boards, brick and rubble on a diagonal are the same generator groups at the longer run, turned, and follow once the frame is liked.

**A staircase of boxes.**
Collision and selection are eight boxes two voxels square in plan, stepping corner to corner, full height when filled and the two posts and the plate's staircase when bare (decision 0008).
A player is 0.6 wide and a step is 0.125 deep, so the steps cannot be entered, only slid along.
Whether sliding along them catches is the first thing to test, in a throwaway build, before any shape is drawn.

**Picked on the saw.**
`Diagonal` is a fourth option on the picker's framing row (decision 0040), with its own icon.
It is placed and turned as a corner is.
A bare `wall` or `cornerout` frame clicked with `Diagonal` picked becomes one in place, by decision 0026's swap.

**No deck, no step, nothing hosted.**
A deck over a diagonal cell is a triangle, a step applies only to a `wall`, and the open part of the cell is a triangle no furniture fits (decision 0035).
All three are refused on a diagonal.

**Every framing.**
Decision 0058 set no per-framing restrictions, so planks, sticks and bones all frame a diagonal.

## Alternatives considered
- **Diagonals for bone frames only.** Where the idea came from, but it is a second rule set for one framing, which decision 0058 declined for sticks.
- **A flag on the entity of a `cornerout` block.** No new blocks and no consumer changes, but the corner's shape file would carry both drawings, and the tooltip and the finish faces would still need to tell the two apart.
- **A panel centred on the diagonal.** Its ends would land in the middle of the cell's faces, where no straight wall ends.
- **One rotated collision box.** Vanilla has none.
- **Walls that lean inward, as a tipi's do.** A different shape problem, close to `walls-under-roofing`, and the roof is the Roofing mod's.
- **Auto-chamfer a corner from its neighbours.** `auto-corners` is parked for the same reason: the player places the piece.

## Consequences & open questions
- Too large for one session as written: the collision test, the frame and infill, and the picker option are the first, and modelled finishes the second.
- Whether a staircase of boxes can be walked along without catching; if not, the idea stops there.
- Whether a turned element's faces light and cull as an axis-aligned one's do, next to the sealed-cell light patches (decisions 0015, 0018, 0034).
- A run of diagonals steps one cell sideways per cell, and the cells in its inside angle are ordinary open cells; furniture set against a diagonal will not sit flush.
- A flat finish on a diagonal beside the same finish modelled on a straight wall will show a seam in relief.
- Stacked diagonals share plates as stacked walls do (decision 0008); a diagonal beside a straight wall drops no member, since each keeps its post.
- Decision 0061 names framing elements by prefix, so a diagonal needs its `poles-` groups too if both ship.
- `WallShapeGenTests` pins the committed shapes to the generator, so `just shapes` runs in the same change.
