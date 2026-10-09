# 0067 — Diagonal decks

- Status: Accepted
- Created: 2026-10-08
- Reflects: the author's questions of 2026-10-08 while planning the diagonal wall; branch `feat/diagonal-decks`; `SidingWallBlock.UnrotatedDeckBoxes`/`BuildDeckBoxes`/`AddOpenPartBoxes`/`IsDeckHit`/`DeckOccupied`/`OnBlockInteractStart` and the framing upgrade's deck check, `SidingWallEntity.OnTesselation`, `PlaceWallFrame`, `lang/en.json`, `docs/moddb.html`, `shapes/block/wall/diagonal.json`, `VSSiding.Tests/WallShapeGen`'s `DeckGroups` and `DiagonalDeckStrips`, `SidingFloorBlock.DeckReaches` (read, not changed); decisions 0042, 0050, 0053, 0066; `WallShapeGenTests`' pins of the deck groups to the deck boxes and of the triangle's top, `SidingWallBlockCollisionTests`' pin of the west strips, `SidingFloorTests`' `DeckReaches` rows for `diagonal`, `VSSiding.E2E.Tests/DiagonalScenarios.cs`; unit tests (465) and e2e scenarios (35) pass; not played

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
The frame is 1 voxel either side of the centre line, so the top plate covers the stepped edge from above, and no tooth reaches a finish's slot or the outside.
This settles the proposal's question of 2-voxel against 1-voxel strips by arithmetic: 2-voxel strips fit once they are offset.

**The shape is cut from the same strips.**
`WallShapeGen.Generate("diagonal")` adds `DeckGroups("deck", ...)` over the strips `DiagonalDeckStrips` reads from the block's own boxes, so the drawing and the collision cannot drift apart.
`DeckGroups` already took several boxes, so there is no new clipping code.
The deck is laid in world directions as any deck is, because the wall mesh turns with its side and the joists must not; the generator writes a group set per side.
`DiagonalTextures` gains the `deck*` and `lashing` slots.

**The top is cut to y 15.98.**
The triangle overlaps the panel in plan.
At y 16 its top would share a plane with the top plate and the finishes' caps, and at 15.99 with the corner fillers' caps.
Decision 0066's first build flickered on a shared plane.
Collision stays at the full height of 16.

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

**What it changes in decision 0066, which is not edited.**
Its "No deck, no step, nothing hosted" paragraph no longer holds as far as the deck goes; no step and nothing hosted still hold.
In "Picked on the saw, placed and upgraded", "with no deck" and "a frame that carries a deck is not turned" no longer hold.
Its last consequence pointed floors and decks at the `diagonal-floors` proposal; that proposal is this decision, and the ground floor moved into `face-finishes`.

## Alternatives considered
- **A square deck on a diagonal.** It stands outside the wall as a triangular ear.
- **Strips starting on the centre line.** The teeth reach 1.41 voxels past it, 0.41 beyond the plate's 1 voxel: into the outer finish's slot on a finished wall, and past the plate's edge on a bare frame.
- **1-voxel strips.** Sixteen boxes for what the one-voxel offset gets with eight, and twice the collision boxes in a cell that already holds thirteen.
- **A ledge copy, as the other layouts have.** A ledge fills the gap between a deck and the frame while the room side is bare, and the triangle leaves no gap: it already runs through that slot to the frame.
- **Keeping the decked-frame refusal.** Its reason was the missing box, and that box exists now.
- **A half-and-half block for the ground floor that replaces the block below and redraws its outer half.** The mod would stand in for arbitrary vanilla blocks, with their drops, collision and behaviours. It is the guest wall of decision 0035 in reverse and larger.
- **The diagonal drawing its own triangular floor skin at its base.** The cells beside it still need a floor at the same height, which is what `face-finishes` is.

## Consequences & open questions
**Nothing here has been played.**
The unit tests pin the boxes, the shape and the answers; the e2e scenarios pin the stored state.
Meshes, flicker and walking are invisible to both.
Play has to settle:
- The stepped edge seen from below through a bare frame.
- Flicker on a top storey seen from above, where the triangle's top lies 0.02 under the plate's.
- A floor run up to the triangle, which `DeckReaches` reads as a joined edge on each leg.
- Walking the deck's stepped edge beside a bare frame.
- The selection outline of eight boxes on top of the wall's thirteen.
- `/sidingroom` on the room below, with the UP face of the cell retaining.

**What the e2e scenarios pin.**
`DiagonalScenarios.cs` pins a diagonal raised with a deck, a deck added in place to a diagonal, a decked wall turned into a diagonal keeping its deck, and the UP face retaining once the deck's infill is laid.

**The strips are not merged.**
A joist the cut does not touch is still written as several pieces.
`diagonal.json` went from 649K to 1.4M and holds 830 deck elements.
Merging pieces is a generator change if size or quad count turns out to matter.
Only diagonal cells pay it.

**One test does not cover the diagonal.**
`WallShapeGenTests.ClippedDeckFacesKeepTheFloorsUv` is not extended to `diagonal`: it expects one element per group name, and a diagonal has up to eight.

**The triangle's top is 0.02 under a neighbouring floor's.**
A floor beside a leg lies at 16 and the triangle at 15.98.

**A ground floor with no digging is open in `face-finishes`.**
Half of the top face of the block below would be skinned; that needs a decor shape or texture nobody has checked, and it waits on the proposal.

**A thin floor cut corner to corner stays unbuilt.**
`SidingFloorBlock` has no orientation today, so a cut floor means new variants.
A square overhang seals, and nobody has asked for it.
