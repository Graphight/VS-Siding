# 0066 — Diagonal walls

- Status: Accepted; no deck superseded by 0067
- Created: 2026-10-08
- Reflects: the playtest of decision 0060 on branch `feat/tent-walls`; branch `feat/diagonal-walls`; `SidingWallBlock.ClaimsTwoFaces`/`ClaimsFace`/`OpenSide`/`ResolveFinishFace`/`ResolveFramingUpgrade`/`UnrotatedFramingBoxes`/`ComputeCollisionBoxes`/`IsReplacableBy`/`TryHost`/`NeighbourJoins`/`DiagonalRunNeighbours`/`OnBlockRemoved`, `SidingWallEntity.SelectiveElements`, `SidingModePicker.Rows`/`Layout`, `PlaceWallFrame`, `SidingModSystem`'s ground-storage prefix, `RainFallFromOpenSidePrefix` and `SealedCellLightPostfix`, `VSSiding.Tests/WallShapeGen`, `blocktypes/wall.json`, `shapes/block/wall/diagonal.json`, `textures/icons/diagonal.svg`, `lang/en.json`; `WallShapeGenTests`' pin of the panel's ends and front, `VSSiding.E2E.Tests/DiagonalScenarios.cs`; decompiled `ShapeTesselator` and `CollisionTester`; decisions 0001, 0002, 0007, 0008, 0009, 0015, 0018, 0021, 0026, 0034, 0035, 0040, 0058, 0060, 0061; the review of PR #106; the author's play of the first build and of the second, 2026-10-08, the second as an octagonal room under a Roofing roof; unit tests (462) and e2e scenarios (34) pass

## Summary
A tent built from these walls is a box, since a wall runs along a cell's face and a corner turns ninety degrees.
A third framing layout, `diagonal`, is a wall that crosses its cell corner to corner, so a corner can be cut at forty-five degrees and a round tent built as an octagon.
It answers every per-face rule as a `cornerout` does; its drawing, its boxes and what may stand in its cell are its own, and the two open-side rules hold only for the half of its cell inside the panel.

## Context
Decision 0060 added bone frames and pelt and cloth infills, and the first thing built with them was a tent.
A hide tent is round, and the nearest a grid gives is an octagon: straight runs joined by diagonals.

**A diagonal closes the same two faces a corner does.**
Take a north wall that ends at a cell, and an east wall that starts one cell further on.
A `cornerout` in the cell between them puts a leg on its north face and a leg on its east face.
A diagonal in that cell runs from its north-west corner to its south-east corner, and the room is still to the south-west of it.
Vanilla's room search crosses a cell's faces, and both layouts answer the same two: north and east closed, south and west open.
So retention, the liquid barrier, attachment, `ClaimsFace` and the four orientations are a corner's, unchanged.

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

**The open side is right for half the cell.**
`OpenSide` returns the diagonal step for a `diagonal` as for a `cornerout`, and two patches take that step as the place all of the cell's open part opens onto.
`RainFallFromOpenSidePrefix` starts the search for open sky from that neighbour for any position in the cell (decision 0034), and `SealedCellLightPostfix` shows that neighbour's light on every face that samples the cell (decision 0018).
A corner's legs stand against its two claimed faces, so all of its open part is on that side.
A diagonal's panel crosses the cell, so the triangle between the panel and the two claimed faces is open, walkable and outside the room.
The ground under that triangle is drawn with the room's light, and a player standing in it hears the wind at the room's volume.
Both patches work on one answer per cell, a `BlockPos` and one light value, so the cell is not split.
The other answer would put the outdoors' light and wind on the room's half, which is what those two decisions removed.

**The straight wall, turned, run corner to corner.**
The panel is the straight wall's own boxes moved onto the cell centre (x 6 to 10) and turned 45 degrees about (8, y, 8).
The cell's corner is 8 root 2, 11.31 voxels, from the centre along the run.
A square end 2 short of that, at 9.31, has its two corners on the cell's two faces, so a panel 18.63 long stays inside its cell.
A filler 2.83 square stands in the corner behind each end, unturned, with its diagonal on the panel's end.
Its outer face is the cell's face, so a straight wall beside it and the diagonal's outer face meet as a mitre, with a 1.17 voxel ledge on the room side.
The filler is a hair under the cell's height (y 0.01 to 15.99), so its cap shares no plane with the plate over it.

**A run of diagonals is one band.**
A 4 thick wall through a cell's corner takes a triangle with 2.83 legs from each of the two cells beside that corner; kept inside its own cells it tapers to a point there.
So where the next cell on the same line holds a diagonal, each of the two draws its own pieces 2 voxels further, to the corner, in place of its filler, with half a post there that makes a 2x2 post with the neighbour's.
Their square ends lie on one line through the corner and stand in the two cells beside it.
`NeighbourJoins` answers which ends carry on, in the `left` and `right` it already returned, from `DiagonalRunNeighbours`; a diagonal facing either way on the same line counts.
That cell is no face neighbour and vanilla sends it no update, so `MarkNeighboursDirty` and `OnBlockRemoved` mark it.

**The first build's posts.**
The first build ran the panel 16 between two 4x4 posts.
In play the posts were four times a wall post's section, their caps shared a plane with the plates and flickered, and two diagonals in a run met along one vertical line.

**The turn is `rotationY: -45`.**
Vanilla's `ShapeTesselator` (decompiled) translates to `rotationOrigin`, then rotates about +y, so +90 carries west to south, as the block's `rotateY` does.
So -45 puts the wall's west-facing front on the north-west, which is outside.
A generator test pins the panel's ends inside the posts and its front on the north-west side, which settles the sign.

**Turned elements from the generator.**
`WallShapeGen`'s `Element` gains `RotationY`, `RotationOrigin` and `Offset`, and `EmitElement` writes vanilla's `rotationY` and `rotationOrigin` only when an element is turned.
`Offset` moves a box as it is written and leaves its uv where the unmoved box had it.
The frame and infill are redrawn as two halves, since a face longer than 16 samples past its texture.
Every `front` and `back` group of the straight wall repeats along the run as it does from one wall to the next: the piece at the wall's z, clipped, moved 16 either way and given its source's slice of the texture.
A board face is rotated 270, which stands u up the wall and lays v along the run; `UvLayout` gains those two rows, so a clipped board keeps its plank width.
The lengths are irrational and the file is written to four decimals.
The three earlier shapes are byte-identical, and `WallShapeGenTests` still pins every committed shape to the generator, so `just shapes` ran in the same change.

**A staircase of thirteen boxes.**
Collision and selection are thirteen overlapping 4x4 boxes in plan, k = 0 to 12, each one voxel along from the last, stepping corner to corner.
The first and the last stand in the two corners.
A bare frame collides on those two and the eleven middle boxes at the top plate's height, as a bare wall does (decision 0008).
Eight 2x2 boxes were the proposal's count; 4x4 boxes cover the panel's 4 voxel thickness.
A box does not leave its cell, so the staircase does not follow a run through a corner: the step there is 4 voxels where the rest are 1.
The first build stepped by two voxels, seven boxes, and in play a player running along the wall caught on the steps.
One voxel is the size of a chiselled step.

**One front, one back.**
A click on either claimed face resolves to `front` and on either opposite face to `back`; there is no `secondfront` (decision 0009), since the panel has one outer face.
`ResolveFinishFace` does this and `DescribeFaces` lists all four directions in two groups.

**Every finish, and a plain frame.**
The diagonal's shape carries the straight wall's finish groups under their own names, so `SelectiveElements` asks for a finish's `Elements` (decision 0007) and a picked style as it does for a wall.
The first build drew every finish as the plain slab, and in play vertical boards showed as horizontal.
A rough pole framing (decision 0061) still draws as the plain frame, and glazing takes the plain plates and no bezel.
A diagonal's frame is two plates and an end at each corner: `framing-left` and `framing-right` are the fillers, `framing-join-left` and `framing-join-right` the half posts, and each group's pieces to the corner are the group's name with `-left` or `-right`.
The block's default shape names what it draws with `selectiveElements`, where the other layouts list what they leave out.

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
- **A 16 run between two 4x4 posts.** Built first; the design says what play found.
- **A 22.6 run with square ends at every corner.** Beside a straight wall the end stands 1.41 voxels outside the wall's face.
- **Collision that follows a run through its corner.** `CollisionTester.GenerateCollisionBoxList` (decompiled) walks only the cells the entity's box and its motion overlap, so a box standing out of its cell is not tested until the entity is already in it.
- **One rotated collision box.** Vanilla has none.
- **Walls that lean inward, as a tipi's do.** A different shape problem, close to `walls-under-roofing`, and the roof is the Roofing mod's.
- **Auto-chamfer a corner from its neighbours.** `auto-corners` is parked for the same reason: the player places the piece.

## Consequences & open questions
- **Played, and accepted.**
On the first build the walls sealed, the lighting was odd as expected, and a player running along the one-voxel staircase grinds a little and comes free.
The second shape was played as an octagonal room of straight walls and diagonals under a Roofing roof, and the author accepted it.
The points below marked unplayed were not reported on.
- The staircase keeps a 4 voxel step at each corner of a run, so a player sliding along a run catches there.
- A run's end pieces are drawn in the two cells beside the corner, lit by the diagonal's own cell.
Where such a cell holds a full block they are inside it, and their top face lies in the plane of that block's top.
Whether that shows is unplayed.
- The filler is framing, so between a finished straight wall and a finished diagonal a 2.83 voxel strip of the frame shows on the outside.
- A finish's pattern restarts at each corner of a run, since each cell repeats it from its own centre.
- Whether the turned faces light and cull as an axis-aligned one's do, next to a sealed cell, is unplayed (decisions 0015, 0018, 0034).
- The triangle outside a filled diagonal takes the room's light and wind, as the design says.
How it looks is unplayed: the ground there by day beside a dark room and by night beside a lit one, and the wind standing in it.
- A run of diagonals steps one cell sideways per cell, and the cells in its inside angle are ordinary open cells; furniture does not sit flush against a diagonal.
- Stacked diagonals share plates as stacked walls do (decision 0008); a diagonal beside a straight wall drops no member.
- Lashed-pole frames and a glazing bezel on a diagonal are a later session: decision 0061's `poles-` groups and the `glazing-` groups need a diagonal copy.
- Floors and decks under a diagonal are the `diagonal-floors` proposal.
