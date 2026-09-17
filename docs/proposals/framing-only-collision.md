# Framing-only collision

- Status: Draft
- Created: 2026-09-16
- Reflects: planning session on `docs/plan-later-proposals`, reading `SidingWallBlock` and `shapes/block/wall/wall.json` as of 71c1d9c

## Summary
A stack of framed walls draws its plates only at the top and bottom of the stack, like a real stud wall, and a wall with framing but no infill collides only on its posts.
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

**Plates are split out of the `framing` element group into `framing-top` and `framing-bottom`**, in `wall.json` and `cornerout.json`.
The posts stay `framing`.

**A plate is drawn only when the cell on that side doesn't continue the frame.**
The top plate is skipped when the block above is a siding wall with the same `layout` and `side` and non-null `Framing`; the bottom plate likewise for the block below.
So a single-high frame keeps both plates, a two-high stack has a bottom plate at y=0 and a top plate at y=2 with nothing between, and the wall above a doorway gets a bottom plate that reads as a lintel.
`SelectiveElements` gains `continuesAbove` and `continuesBelow` inputs; `CacheKey` gains the same two bools.
Infill and finishes are unchanged: they already cover the full cell height.

**Neighbour changes re-tesselate.**
`SidingWallBlock.OnNeighbourBlockChange` marks the entity dirty (`MarkDirty(true)`) when the changed position is directly above or below.
Adding framing to a wall already goes through `MarkDirty`, which is a block-entity update, not a block change; the frame placement in `PlaceWallFrame` must also mark the walls above and below dirty.

**`GetCollisionBoxes` returns posts only when `Framing != null && Infill == null`.**
Every other state returns `base.GetCollisionBoxes`, the static JSON boxes.
`GetParticleCollisionBoxes` follows the same rule.
Post boxes are hand-written for `west` and rotated with `Cuboidf.RotatedCopy` around the block centre, the angles of `SidingWallEntity.RotationYDeg`, cached per layout in static fields.
Plates never collide: the bottom plate is a 1-voxel step the player walks over, and the top plate is overhead.

**Selection boxes don't change.**
The player has to be able to click the frame to add infill.

**Adding infill refuses if any entity overlaps the full slab box**, with an ingame error (`vssiding:build-occupied`), so nobody gets a wall built around them.

**Retention is untouched.**
No infill already means `GetRetention` returns 0 (decision 0003), so scan and collision agree the frame is open.

## Alternatives considered
- **Posts-only collision, plates drawn in every cell.** The first draft of this proposal. Players walk through a visible beam at head height.
- **Keep plates in the collision.** Correct for one cell; a two-high frame is impassable.
- **No collision for a framing-only wall.** You'd walk through the posts, which are visible solid timber.
- **A `built` variant axis so collision stays static JSON.** Encodes entity state in the block ID, which decision 0001 exists to avoid.

## Consequences & open questions
- Supersedes decision 0002's "no per-block-entity collision code" line; graduation should say so.
- First neighbour-dependent mesh in the mod. It's only vertical and only reads the two cells above and below, but it's the pattern auto-connecting corners would also need, so get the dirty-marking right here.
- A three-high stack is walkable too; one-high never is. That's just player height.
- Does `GetCollisionBoxes` get called client-side before the entity has synced? A missing entity must fall back to the full boxes, never posts.
- Does "continues" need to match material too, or just any framing? Proposed: any framing, since mixed-wood stacks should still be one wall.
- An unglazed `window-frames` window is framing-only, so it becomes walk-through. Plausibly right for a hole; check in playtest.
- Tests: `SelectiveElements` and a pure `ComputeCollisionBoxes(layout, side, framing, infill)`, asserted against whole expected arrays.
