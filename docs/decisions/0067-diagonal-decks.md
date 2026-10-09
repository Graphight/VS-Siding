# 0067 — Diagonal decks

- Status: Accepted
- Created: 2026-10-08
- Reflects: the author's questions of 2026-10-08 while planning the diagonal wall; branch `feat/diagonal-decks`; `SidingWallBlock.UnrotatedDeckBoxes`/`BuildDeckBoxes`/`AddOpenPartBoxes`/`IsDeckHit`/`DeckOccupied`/`OnBlockInteractStart` and the framing upgrade's deck check, `SidingWallEntity.OnTesselation`, `PlaceWallFrame`, `lang/en.json`, `docs/moddb.html`, `shapes/block/wall/diagonal.json`, `VSSiding.Tests/WallShapeGen`'s `DeckGroups`, `CoveredLengths` and `DiagonalDeckStrips`, `SidingFloorBlock.DeckReaches` (read, not changed); decisions 0042, 0050, 0053, 0066; `WallShapeGenTests`' pins of the deck groups to the deck boxes, of the triangle's top, of the fillers' planes and of the pole groups, `SidingWallBlockCollisionTests`' pin of the west strips, `SidingFloorTests`' `DeckReaches` rows for `diagonal`, `VSSiding.E2E.Tests/DiagonalScenarios.cs` and `RoomScenarios.cs`; the review of PR #107; unit tests (467) and e2e scenarios (37) pass; the author's play of 2026-10-08, a glazed diagonal dug in one block with a deck at ground level

## Summary
A diagonal wall (decision 0066) cuts its cell corner to corner, so the half of the cell inside the room has no floor of its own.
A diagonal takes a deck that is the inner triangle only, drawn and collided as eight strips, with the layers, peel, drops and retention of any other deck.
A ground floor under a diagonal with no digging, and a thin floor cut corner to corner, are not built.

## Context
A cell holds one block.
A thin floor sits at the top of its cell (decision 0050), and a wall's cell cannot hold one.
The wall's own floor is its deck (decisions 0042, 0053).

**An upper storey or a flat ceiling has a hole at each diagonal.**
Decision 0066 gave a diagonal no deck.
The deck's box is the cell's whole open part, and on a diagonal a square deck would stand outside the panel as a triangular ear.
So an octagonal upper storey has a triangular hole in each diagonal cell.
The cell's UP face retains nothing there, since `ComputeDeckRetention` with no deck is `ComputeRetention` with no framing key, which returns 0.
The room below is not sealed upward unless a block sits above.

**Two other places want a floor in the inner triangle.**
A ground floor under a tent has no block to dig out without leaving a pit outside the tent.
A roof or floor one level above the walls overhangs a diagonal by a triangle.
Neither is built here; see the open questions.

## Design
**The triangle is eight boxes.**
Unrotated (`west`), the panel's centre line is x + z = 16 and the room is the x + z > 16 half.
Strip k, for k = 0 to 7, is z from 2k to 2k + 2, x from 15 - 2k to 16 and y from 12 to 16, in voxels.
`SidingWallBlock.UnrotatedDeckBoxes` holds them, and `BuildDeckBoxes` turns them per side as it does every deck box.
They are the collision boxes and the selection boxes both, appended after the block's own by `AddOpenPartBoxes`.
`IsDeckHit` takes any selection index in the deck's range, so a click on any strip is a click on the deck.
Decision 0042 gave a deck one box per layout; the table now holds a list per layout, and `wall` and `cornerout` keep one each.

**The strips start one voxel to the room side of the centre line.**
Each strip's cut end then lies between 0.71 voxels either side of the centre line.
Starting on the line reaches 1.41 voxels past it, which was the proposal's version.
The frame is 1 voxel either side of the centre line, so the top plate covers the stepped edge from above, and no tooth reaches a finish's slot.
A diagonal standing on top drops that plate (decision 0008), so at a join between storeys nothing covers the edge from outside but the infill.
The infill is half a voxel either side, which the plan for this build missed.
On a filled wall with no outer finish each cut end stands 0.21 voxels proud of the infill's outer face, under the plate, from y 12 to 15.
On the room side the same 0.21 is a notch between the deck and the infill's inner face, seen from below while that side is bare.
So the proposal's question of 2-voxel against 1-voxel strips is not settled by arithmetic, and play decides whether 0.21 shows.

**The shape is cut from the same strips.**
`WallShapeGen.Generate("diagonal")` adds `DeckGroups("deck", ...)` over the strips `DiagonalDeckStrips` reads from the block's own boxes, so the drawing and the collision cannot drift apart.
`DeckGroups` already took several boxes, so there is no new clipping code.
The deck is laid in world directions as any deck is, because the wall mesh turns with its side and the joists must not; the generator writes a group set per side.
`DiagonalTextures` gains the `deck*` and `lashing` slots.

**The drawn triangle keeps 0.02 off every plane it would share with the panel.**
The triangle overlaps the panel in plan.
At y 16 its top would share a plane with the top plate and the finishes' caps, and at 15.99 with the corner fillers' caps, so its top is cut to 15.98.
The fillers are unturned boxes in the two corners the panel runs between, and their outer faces are the cell's own.
The strips reach the cell's faces there too, so the first build of this decision drew two faces in one plane on three of them, one on the outside of the building.
`DiagonalDeckStrips` cuts each strip 3 voxels from those two corners and pulls the piece in the corner 0.02 off the cell's faces; the rest of the strip still meets the floor beside it.
Decision 0066's first build flickered on a shared plane.
Collision stays at the full boxes.

**A pole joist is drawn where the strips together cover its width.**
`DeckGroups` dropped any pole piece that a box's edge cut across its width, since with one box that piece is a sliver of a joist or a stub whose joist is on the far side.
On a `south` or `north` diagonal the strips lie side by side across the joists, and the boundary between two strips cut the middle joist of a stick or bone deck, so the joist was not drawn.
`CoveredLengths` keeps a box's part of a pole piece along the lengths where the side's boxes together cover the piece's whole width.
One box gives what the old rule gave, and `wall.json` and `cornerout.json` are byte-identical.

**No ledge.**
The triangle always runs to the frame, through the slot of the room-side finish, which covers it once laid.
`SidingWallEntity.OnTesselation` passes `ledge: false` to `DeckElements` for a diagonal.
A bare room side shows the triangle meeting the frame with no second copy of the deck's layers reaching it.

**Floors run up to the triangle with no change.**
`SidingFloorBlock.DeckReaches` and `Joins` did not change.
The triangle's two legs are the whole of the two faces the diagonal does not claim, and it meets the claimed faces only at a corner point.
`!ClaimsFace` already answers exactly that, so `Joins` counts the deck as carrying a floor on across either leg and not across a claimed face.
A test row per face of the `west` diagonal pins the four answers.

**Everything else about a deck is the same code for every layout.**
Layers, peel, drops, fire, the tooltip, `ComputeDeckRetention` on the UP face and the `deck*` texture slots were already blind to the layout.

**Three refusals are removed.**
The `build-diagonal-deck` branch of `OnBlockInteractStart` returned an in-game error for a deck click on a diagonal; the branch and its lang string go.
`PlaceWallFrame` left a diagonal out of `withDeck`, so a picked deck was dropped when a diagonal was raised; a diagonal is now raised with the deck and costs the deck's planks as well.
The framing upgrade returned `build-decked` for a decked frame clicked with `Diagonal` picked; the reason in decision 0066 was that a diagonal had no box for the deck, and that reason is gone.

**What it changes in decision 0066.**
Decision 0066's status line now says its no-deck rule is superseded here; its text is as it was.
Its "No deck, no step, nothing hosted" paragraph no longer holds as far as the deck goes; no step and nothing hosted still hold.
In "Picked on the saw, placed and upgraded", "with no deck" and "a frame that carries a deck is not turned" no longer hold.
Its last consequence pointed floors and decks at the `diagonal-floors` proposal; that proposal is this decision, and the ground floor moved into `face-finishes`.

## Alternatives considered
- **A square deck on a diagonal.** It stands outside the wall as a triangular ear.
- **Strips starting on the centre line.** The teeth reach 1.41 voxels past it, 0.41 beyond the plate's 1 voxel: into the outer finish's slot on a finished wall, and past the plate's edge on a bare frame.
- **1-voxel strips, drawn or collided.** Offset by half a voxel their cut ends lie 0.35 either side of the centre line, inside the infill's 0.5, so nothing stands proud and nothing is notched. Sixteen boxes double the deck's elements, and as collision boxes they join a cell that already holds thirteen. Not built until play says the 0.21 shows; drawing each collision strip as two 1-voxel halves would keep the boxes at eight.
- **A ledger: one turned beam along the panel at the deck's height, as thick as the frame.** It would cover the cut ends from every side, under glass, on a bare frame and at a join between storeys. Not built: the author saw the sawtooth in play and accepted it.
- **A ledge copy, as the other layouts have.** A ledge fills the gap between a deck and the frame while the room side is bare, and the triangle leaves no gap: it already runs through that slot to the frame.
- **Keeping the decked-frame refusal.** Its reason was the missing box, and that box exists now.
- **A half-and-half block for the ground floor that replaces the block below and redraws its outer half.** The mod would stand in for arbitrary vanilla blocks, with their drops, collision and behaviours. It is the guest wall of decision 0035 in reverse and larger.
- **The diagonal drawing its own triangular floor skin at its base.** The cells beside it still need a floor at the same height, which is what `face-finishes` is.

## Consequences & open questions
**Played in part, and accepted.**
The author played a glazed diagonal dug in one block, with a deck at ground level and a diagonal standing on it, on 2026-10-08.
The deck's stepped edge shows along the foot of the glass as a sawtooth: glass hides nothing, and the plate is dropped at the join.
It goes once infill is laid, and the author accepted it as it is.
The triangular pit outside the dug-in diagonal is there, as the proposal said of a ground floor.
The unit tests pin the boxes, the shape and the answers, and the e2e scenarios pin the stored state; meshes, flicker and walking are invisible to both.
The points below were not reported on:
- The stepped edge seen from below through a bare frame.
- A filled wall with no outer finish seen from outside, just under the top plate, for the cut ends standing 0.21 proud of the infill; a pelt tent is this case.
- Flicker on a top storey seen from above, where the triangle's top lies 0.02 under the plate's.
- A floor run up to the triangle, which `DeckReaches` reads as a joined edge on each leg.
- Walking the deck's stepped edge beside a bare frame.
- The selection outline of eight boxes on top of the wall's thirteen.
- `/sidingroom` on the room below, with the UP face of the cell retaining.

**The room below seals, as far as the registry goes.**
`RoomScenarios.cs` builds a one-cell room whose only way out is up through a decked diagonal's cell: vanilla's `RoomRegistry` counts exits with the deck bare and none with it filled.
Each scenario measures its room once.
In the headless server a room measured before a wall or a deck is filled keeps its first answer until another block changes in the chunk, although the fill exchanges the block for itself; a plain wall on `main` does the same, and it is not looked into here.

**The strips are not merged.**
A joist the cut does not touch is still written as several pieces.
`diagonal.json` went from 649K to 1.6M and holds 1038 deck elements.
Merging pieces is a generator change if size or quad count turns out to matter.
Only diagonal cells pay it.

**One test does not cover the diagonal.**
`WallShapeGenTests.ClippedDeckFacesKeepTheFloorsUv` is not extended to `diagonal`: it expects one element per group name, and a diagonal has up to eight.

**The triangle's top is 0.02 under a neighbouring floor's.**
A floor beside a leg lies at 16 and the triangle at 15.98.

**A ground floor with no digging is open in `face-finishes`.**
Half of the top face of the block below would be skinned; that needs a decor shape or texture nobody has checked, and it waits on the proposal.
Play confirmed what digging in leaves instead: a triangular pit outside the wall.

**A thin floor cut corner to corner stays unbuilt.**
`SidingFloorBlock` has no orientation today, so a cut floor means new variants.
A square overhang seals, and nobody has asked for it.
