# Diagonal floors and decks

- Status: Draft
- Created: 2026-10-08
- Reflects: the author's questions of 2026-10-08 while planning the diagonal wall, on branch `feat/diagonal-walls`; `SidingWallBlock.GetRetention`/`CanAttachBlockAt`/`ComputeDeckRetention`/`ComputeRetention`/`UnrotatedDeckBoxes`/`BuildDeckBoxes`/`OnBlockInteractStart`'s deck branch, `SidingWallEntity.Deck`/`DeckInfill`/`DeckElements`, `SidingFloorBlock.DeckReaches`, `VSSiding.Tests/WallShapeGen`'s `DeckGroups`/`DeckRegion`/`DiagonalElements`, `blocktypes/wall.json`'s `diagonal` layout; decisions 0042, 0050, 0051, 0053, 0066; the `face-finishes` proposal; not built, not played

## Summary
A diagonal wall cuts its cell in two, and a cell holds one block, so no floor block fits in the half that is inside the room.
Two pieces cover the three places that want one: a deck on a diagonal that is the inner triangle only, and a ground floor with no digging, which is `face-finishes` with a diagonal cut.
A thin floor cut corner to corner is left open.

## Context
The diagonal wall of decision 0066 is a panel crossing its cell corner to corner between two 4x4 posts.
One triangle of the cell is inside the room and the other is outside.

**A cell holds one block.**
A thin floor sits at the top of its cell (decision 0050), and a wall's cell cannot hold one.
The wall's own floor is its deck (decisions 0042, 0053).
Three places want a floor in the inner triangle only.

**1. An upper storey or a flat ceiling.**
A deck is not placed on a diagonal: the deck branch of `OnBlockInteractStart` returns an error for the `diagonal` layout.
The deck's box is the cell's whole open part (`UnrotatedDeckBoxes` has an entry for `wall` and for `cornerout` only), and on a diagonal that would stand outside the panel as a triangle.
So an octagonal upper storey has a triangular hole in each diagonal cell.
The cell's UP face retains nothing, since `ComputeDeckRetention` with no deck is `ComputeRetention` with no framing key, which returns 0.
The room below is not sealed upward there unless a block sits above.

**2. Ground level.**
A tent stands on the ground, and in a diagonal's cell the floor is the top face of the block below, half inside and half outside.
A straight wall gets a floor at ground level today by being dug in one cell down with a deck.
For a diagonal that replaces the whole block below, and leaves a triangular pit outside the tent.
The player does not want to dig, and the outer half must stay what it was.

**3. A roof or floor one level above the walls.**
Its square cells seal, but each overhangs a diagonal by a triangle, so the roof does not follow the octagon.

## Design
**A. A triangular deck on a diagonal, for case 1.**
The deck is the inner triangle only.
It takes a floor's layers as any deck does (decision 0053), and it seals the UP face through `ComputeDeckRetention`.
`ClaimsFace` and the sealing of the other faces are unchanged.

Shape elements are boxes, so the triangle is drawn as strips stepping along the diagonal.
The stepped edge is tucked inside the panel's thickness.
With 2-voxel strips the teeth reach at most 1.41 voxels past the panel's centre line, and the panel's outer face is 2 voxels from it, so no tooth shows outside a filled wall.
The panel in `DiagonalElements` is the straight wall's 4 voxel slab moved onto the cell centre and turned 45 degrees, which is where the 2 voxels come from.
Whether the teeth show through a bare frame, and whether 1-voxel strips are needed there, is open.

Collision and selection are the same staircase idea as the wall's own boxes, at the deck's height.
The deck is laid in world directions: `WallShapeGen` writes one set of `deck-{side}-{name}` groups per side (`DeckGroups`, `DeckRegion`), because the wall mesh turns with its side and the joists must not.
A triangle needs a group set per side, clipped to its own region, and `DeckElements` picks them as it does now.

**B. A ground-level floor with no digging, for case 2.**
The block below is not replaced.
Half of its top face is skinned.
That is the `face-finishes` proposal, a plank finish as a vanilla decor on one face of a solid block, with a diagonal cut.
The block below stays the vanilla block it was, so its outer half is unchanged by construction.

`face-finishes` draws a `surfacelayer` decor, one flat quad over the whole face.
A triangle needs either a `json` decor of thin stepped strips or a plank texture with a transparent half.
Neither has been checked, and `face-finishes` itself is not built.
A decor key carries a rotation, so one triangular variant may cover all four orientations; not checked.
The cut edge lies under the panel's base, which covers 2 voxels either side of the diagonal.

**C. Left open: a thin floor cut corner to corner, for case 3.**
`SidingFloorBlock` has no orientation today, so a cut floor means new variants.
Nobody has asked for it, and a square overhang seals.

**Order.**
The diagonal wall's own play test comes first, decision 0066's open question on walking along the staircase of boxes.
If that fails, none of this is built.
A is independent of `face-finishes`.
B waits on it.

## Alternatives considered
- **A half-and-half block that replaces the block below and remembers and redraws its outer half.** The mod would stand in for arbitrary vanilla blocks, with their drops, collision and behaviours. It is the guest wall of decision 0035 in reverse and larger.
- **A square deck on a diagonal.** It stands outside the wall as a triangular ear.
- **The diagonal wall drawing its own triangular floor skin at its base as a custom mesh.** It has no dependency on `face-finishes`, but the cells beside it still need a floor at the same height, which is what `face-finishes` is.
- **Dig in and accept the pit, or leave the triangle as bare ground.** The second is what decision 0066 ships.

## Consequences & open questions
- A diagonal's deck grows the shapes by a set of triangle groups per side, and the deck box tables gain a staircase for the `diagonal` layout. The deck branch and the placement checks that now return an error for a diagonal would change with it.
- Whether the stepped teeth show through a bare frame, and whether 1-voxel strips are needed there.
- Whether a floor run up to a triangular deck joins cleanly. `SidingFloorBlock.DeckReaches` drops a floor's rim against a deck whose area reaches the shared edge, and a triangle reaches only part of an edge; not checked.
- Whether a `json` decor of strips, or a masked texture, can be drawn as a decor on a block's top face at all, and whether one rotated variant covers four orientations. Both are `face-finishes` questions with a diagonal added.
- B inherits every open question of `face-finishes`, among them a decor drawn only where its host's face is drawn.
- A thin floor cut corner to corner (C) stays unbuilt until a player asks for it.
