# 0066 — Diagonal walls

- Status: Accepted
- Created: 2026-10-08
- Reflects: the playtest of decision 0060 on branch `feat/tent-walls`; branch `feat/diagonal-walls`; `SidingWallBlock.ClaimsTwoFaces`/`ClaimsFace`/`OpenSide`/`ResolveFinishFace`/`ResolveFramingUpgrade`/`UnrotatedFramingBoxes`/`ComputeCollisionBoxes`/`IsReplacableBy`/`TryHost`, `SidingWallEntity.SelectiveElements`, `SidingModePicker.Rows`/`Layout`, `PlaceWallFrame`, `SidingModSystem`'s ground-storage prefix, `VSSiding.Tests/WallShapeGen`, `blocktypes/wall.json`, `shapes/block/wall/diagonal.json`, `textures/icons/diagonal.svg`, `lang/en.json`; `WallShapeGenTests`' pin of the panel's ends and front, `VSSiding.E2E.Tests/DiagonalScenarios.cs`; decompiled `ShapeTesselator`; decisions 0001, 0002, 0007, 0008, 0009, 0021, 0026, 0035, 0040, 0058, 0060, 0061; unit tests (460) and e2e scenarios (32) pass; NOT PLAYED

## Summary
A tent built from these walls is a box, since a wall runs along a cell's face and a corner turns ninety degrees.
A third framing layout, `diagonal`, is a panel that crosses its cell corner to corner between two posts, so a corner can be cut at forty-five degrees and a round tent built as an octagon.
For every rule except its drawing, its boxes and what may stand in its cell it is a `cornerout`.

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
Before this decision `WallShapeGen` emitted `from`, `to` and `faces` only: no rotation and no child elements.

## Design
**A `diagonal` layout.**
A third state of the `layout` variant group in `wall.json`, beside `wall` and `cornerout`, with its own shape file `diagonal.json`.
It is four more blocks and no more material entries, since materials are attributes (decision 0001).
`diagonal-{side}` claims what `cornerout-{side}` claims, so `CorneroutSecondFace` names its second face.
`SidingWallBlock.ClaimsTwoFaces` answers for `cornerout` and `diagonal`, and the checks that read `layout == "cornerout"` for a rule the two share now call it: `ClaimsFace`, `OpenSide`, glazing joins (`NeighbourJoins`), the neighbour updates (`OnNeighbourBlockChange`, `MarkNeighboursDirty`) and the entity's `corner` flag in `SelectiveElements`.

**The run is 16, with a post in each corner.**
The proposal laid the panel along the cell's diagonal, 22.6 voxels long.
As built, two full-height 4x4 posts stand in the two corners the panel runs between, and the panel is the straight wall's own boxes moved onto the cell centre (x 6 to 10) and turned 45 degrees about (8, y, 8).
A 16 long, 4 thick slab turned that way ends inside the posts.
So no slab is split for texture tiling, no element is mitred, and a neighbouring straight wall's 4 wide end meets a post face to face, as it meets a corner's.

**The turn is `rotationY: -45`.**
Vanilla's `ShapeTesselator` (decompiled) translates to `rotationOrigin`, then rotates about +y, so +90 carries west to south, as the block's `rotateY` does.
So -45 puts the wall's west-facing front on the north-west, which is outside.
A generator test pins the panel's ends inside the posts and its front on the north-west side, which settles the sign.

**Turned elements from the generator.**
`WallShapeGen`'s `Element` gains `RotationY` and `RotationOrigin`, and `EmitElement` writes vanilla's `rotationY` and `rotationOrigin` only when an element is turned.
The three earlier shapes are byte-identical, and `WallShapeGenTests` still pins every committed shape to the generator, so `just shapes` ran in the same change.

**A staircase of seven boxes.**
Collision and selection are seven overlapping 4x4 boxes in plan, k = 0 to 6, stepping corner to corner.
The first and the last are the posts.
A bare frame collides on the posts and the five middle boxes at the top plate's height, as a bare wall does (decision 0008).
Eight 2x2 boxes were the proposal's count; seven 4x4 boxes cover the panel's 4 voxel thickness, and the first and the last are the posts' own boxes.

**One front, one back.**
A click on either claimed face resolves to `front` and on either opposite face to `back`; there is no `secondfront` (decision 0009), since the panel has one outer face.
`ResolveFinishFace` does this and `DescribeFaces` lists all four directions in two groups.

**Flat at first.**
On a diagonal every finish draws as the plain `front` or `back` slab whatever its `Elements` (decision 0007) or the picked style say, and a rough pole framing (decision 0061) draws as plain `framing`.
`SelectiveElements` does this.
The picked style is still stored on the entity, so it shows once modelled finishes follow.
A finish's groups are built for a 16 voxel run on a face, and the first cut draws a diagonal as `plate-{metal}` and `rammed-{pattern}` are drawn everywhere.

**Picked on the saw, placed and upgraded.**
`Diagonal` is a fourth option on the picker's framing row (decision 0040), with its own icon `diagonal.svg`, and `SidingModePicker.Layout` returns it.
`PlaceWallFrame` places it, with no deck.
A bare `wall` or `cornerout` frame clicked with `Diagonal` picked becomes a diagonal in place by decision 0026's swap, through `ResolveFramingUpgrade`: a wall at the end clicked, a corner on its own side, and nothing turns back.
The swap keeps the block entity, so a frame that carries a deck is not turned: the click returns `build-decked`, as a stepped frame returns `build-stepped`.
Decision 0058 set no per-framing restrictions, so planks, sticks and bones all frame a diagonal.

**No deck, no step, nothing hosted.**
A deck over a diagonal cell is a triangle, a step applies only to a `wall`, and the open part of the cell is a triangle no furniture fits (decision 0035).
The deck branch of `OnBlockInteractStart` returns an in-game error (`build-diagonal-deck`) for a diagonal, and the step message now names it.
`IsReplacableBy` and `TryHost` return false for a diagonal, and the ground-storage prefix in `SidingModSystem` hands the click back to vanilla, so furniture lands in the cell in front.

## Alternatives considered
- **Diagonals for bone frames only.** Where the idea came from, but it is a second rule set for one framing, which decision 0058 declined for sticks.
- **A flag on the entity of a `cornerout` block.** No new blocks and no consumer changes, but the corner's shape file would carry both drawings, and the tooltip and the finish faces would still need to tell the two apart.
- **A panel 22.6 long, from the cell's corner to its corner, which the proposal drew.** A slab longer than 16 voxels is two elements for texture tiling, and its square ends either stop short of the corner or stand into the next cell.
- **One rotated collision box.** Vanilla has none.
- **Walls that lean inward, as a tipi's do.** A different shape problem, close to `walls-under-roofing`, and the roof is the Roofing mod's.
- **Auto-chamfer a corner from its neighbours.** `auto-corners` is parked for the same reason: the player places the piece.

## Consequences & open questions
- **This has not been played.**
The proposal made walking along the staircase of boxes the test that decides whether the idea goes ahead.
The build went ahead without it because the work was done unattended.
It must be played before this is merged: walk, sprint and slide along both faces of a run of three, then `/sidingroom` in an octagon of four straight runs and four diagonals.
If the steps catch, the idea stops there.
- Whether the turned faces light and cull as an axis-aligned one's do, next to a sealed cell, is unplayed (decisions 0015, 0018, 0034), as is the join to a straight wall at each post.
- A flat finish on a diagonal beside the same finish modelled on a straight wall will show a seam in relief.
- A run of diagonals steps one cell sideways per cell, and the cells in its inside angle are ordinary open cells; furniture does not sit flush against a diagonal.
- Stacked diagonals share plates as stacked walls do (decision 0008); a diagonal beside a straight wall drops no member, since each keeps its post.
- Modelled finishes and lashed-pole frames on a diagonal are a later session: boards, brick and rubble are the same generator groups turned, and decision 0061's `poles-` groups need a diagonal copy.
- Floors and decks under a diagonal are the `diagonal-floors` proposal.
