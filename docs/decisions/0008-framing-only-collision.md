# Framing-only collision

- Status: Accepted
- Created: 2026-09-16
- Reflects: planning session on `docs/plan-later-proposals`, reading `SidingWallBlock` and `shapes/block/wall/wall.json` as of 71c1d9c; graduated on branch `framing-only-collision` and revised after in-game playtest (cross-beams, top-plate collision, open/filled boundaries); supersedes decision 0002's "no per-block-entity collision code" line

## Summary
A stack of framed walls draws its plates at the top and bottom of the stack, like a real stud wall, plus a cross-beam every second cell, and a wall with framing but no infill collides only on its posts and top plate.
So a two-high frame is a door-shaped opening a player can walk through.

## Context
Decision 0002 made collision static JSON: one thickness, one box per `layout`/`side`, "no per-block-entity collision code".
The build flow (decisions 0005, 0006) now makes a framing-only wall a normal state, and it's the state a player is in while laying out a house.
Roofing lets you walk through unfinished roof frames, so players will expect it.
It also answers decision 0005's open question about reaching the back face: frame the wall, walk through, fill from whichever side you like.

Collision itself is easy: `Block.GetCollisionBoxes(IBlockAccessor, BlockPos)` is virtual and gets the position, so the block can read its own entity.

The plates are the real problem.
Every cell of `wall.json` has a top plate, a bottom plate, and two corner posts.
The gap between posts is 14/16 = 0.875 wide and a player is 0.6 wide, so width is fine.
But a player is 1.85 tall, so walking through needs a two-high stack, and stacking two cells puts the first cell's top plate and the second cell's bottom plate together as a 2-voxel beam at y=1, head height.
Dropping the plates from collision alone would let players walk through a beam they can see.

A real stud wall has one bottom plate and one top plate for the whole wall height, not one per storey of studs.
Doing the same fixes the look and the walk-through together.

## Design

**Posts run the full cell height; plates are split out of the `framing` element group and shortened to run between the posts.**
`wall.json`'s posts run y 1..15 today, between the plates.
Skipping the plates at a join would leave a 2-voxel gap in every post, the same slit problem the infill fix below solves for infill.
So posts become full height (y 0..16), and plates become their own `framing-top`/`framing-bottom` elements, shortened to run between the posts: `wall`'s plate runs z 1..15; `cornerout`'s two legs run leg1 z 3..15, leg2 x 3..15.
No post extension elements are needed, since the posts are already full height; only `infill-top`/`infill-bottom` are added, below.
Post collision boxes (see `GetCollisionBoxes` below) are full height to match.

**A plate is drawn only when the cell on that side doesn't continue the frame.**
"Continues" means non-null `Framing` with the same infill state (both open or both filled), not a material match: mixed-wood stacks are still one wall, but an open frame and a filled cell are not.
The top plate is skipped when the block above is a siding wall with the same `layout` and `side` and non-null `Framing` and matching infill state; the bottom plate likewise for the block below.
So a single-high frame keeps both plates, a two-high stack has a bottom plate at y=0 and a top plate at y=2 with nothing between, and a filled wall above an open doorway frame gets plates at the boundary that read as a lintel.
The one exception is a cross-beam: every second cell up a stack, counted from its bottom cell, keeps its top plate, so a 3-high stack has plates on top of cells [no, yes, yes] and a 4-high on [no, yes, no, yes].
That keeps a two-high doorway open while a tall wall still reads as braced framing.
Because the count runs from the bottom, adding or removing a cell marks every framed cell above it dirty, not just its neighbours.
`SelectiveElements` gains `continuesAbove` and `continuesBelow` inputs; `CacheKey` gains the same two bools.
Finishes are unchanged: the face slabs already span the full cell height.
Infill doesn't: the `infill` element runs y 1..15, between the plates, so skipping the plates at a join would leave a 2-voxel see-through slit in a filled, unfinished stack.
So `infill` gains two extension elements, `infill-top` (y 15..16) and `infill-bottom` (y 0..1), selected when infill is built and the same `continuesAbove`/`continuesBelow` input is true.
A filled two-high stack then shows one unbroken panel from y 1 to y 31.

**Neighbour changes re-tesselate.**
`SidingWallBlock.OnNeighbourBlockChange` marks the entity dirty (`MarkDirty(true)`) when the cell above changes, and marks every framed cell from here up when the cell below changes, since the cross-beam count runs from the bottom.
Setting `Framing` (in `PlaceWallFrame`) or `Infill` is a block-entity update, not a block change, so both also mark the cell below and the stack above dirty.

**`GetCollisionBoxes` returns only the framing the cell draws when `Framing != null && Infill == null`:** the posts, plus the top plate if it draws one.
Every other state returns `base.GetCollisionBoxes`, the static JSON boxes.
`GetParticleCollisionBoxes` follows the same rule.
The boxes are hand-written for `west` and rotated with `Cuboidf.RotatedCopy` around the block centre, the angles of `SidingWallEntity.RotationYDeg`, built once per layout, side and plate combination in static fields.
Top plates collide, so a one-high frame or a waist-height cross-beam blocks the player, while a two-high doorway's only top plate sits above head height.
Bottom plates never collide: standing on one lifts the player 1 voxel, into a two-high doorway's top plate.

**Selection boxes don't change.**
The player has to be able to click the frame to add infill.

**Adding infill refuses if any entity overlaps the full slab box**, with an ingame error using the `vssiding:build-occupied` lang string, so nobody gets a wall built around them.

**Retention is untouched.**
No infill already means `GetRetention` returns 0 (decision 0003), so scan and collision agree the frame is open.

**A missing block entity falls back to the full JSON collision boxes.**
`GetCollisionBoxes` only returns posts when it has an entity to read `Framing`/`Infill` from; no entity means no confidence the frame is open, so it collides as a solid cell.

## Alternatives considered
- **Posts-only collision, plates drawn in every cell.** The first draft of this proposal. Players walk through a visible beam at head height.
- **Collide on every drawn plate.** Standing on the bottom plate lifts the player into a two-high doorway's top plate, so they have to crouch.
- **No collision for a framing-only wall.** You'd walk through the posts, which are visible solid timber.
- **A `built` variant axis so collision stays static JSON.** Encodes entity state in the block ID, which decision 0001 exists to avoid.

## Consequences & open questions
- Supersedes decision 0002's "no per-block-entity collision code" line.
- First neighbour-dependent mesh in the mod. It's only vertical, but it walks down the whole stack to count cells, and it's the pattern auto-connecting corners would also need, so get the dirty-marking right here.
- A three-high stack is walkable too; one-high never is. That's just player height.
- An unglazed `window-frames` window is framing-only, so it becomes walk-through. Plausibly right for a hole; check in playtest.
