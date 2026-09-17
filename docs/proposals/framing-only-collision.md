# Framing-only collision

- Status: Draft
- Created: 2026-09-16
- Reflects: planning session on `docs/plan-later-proposals`, reading `SidingWallBlock` and `shapes/block/wall/wall.json` as of 71c1d9c

## Summary
A wall with framing but no infill collides only on its corner posts, so a player can step between the studs.
Selection boxes stay the full slab, so the frame is still clickable for infill.

## Context
Decision 0002 made collision static JSON: one thickness, one box per `layout`/`side`, "no per-block-entity collision code".
The build flow (decisions 0005, 0006) now makes a framing-only wall a normal state, and it's the state a player is in while laying out a house.
Roofing lets you walk through unfinished roof frames, so players will expect it.
It also answers decision 0005's open question about reaching the back face: frame the wall, walk through, fill from whichever side you like.

The backlog entry called this an architectural decision needing stud geometry nobody had designed.
Reading the code, neither is true any more:
- `Block.GetCollisionBoxes(IBlockAccessor, BlockPos)` is virtual and gets the position, so the block can read its own entity. That's one override, not a new system.
- `wall.json` already has studs: two corner posts (`x 1..3`, `z 0..1` and `z 15..16`, in voxels) between a top and bottom plate.

Worked through: the gap between posts is 14/16 = 0.875 wide, and a player is 0.6 wide, so they fit.
The plates are the problem.
Stack two frame cells and the top plate of one and the bottom plate of the next form a 2-voxel beam at head height.
So the plates must not collide either, or nobody walks through anything taller than one block.

## Design

**`SidingWallBlock.GetCollisionBoxes` returns posts only when `Framing != null && Infill == null`.**
Every other state (unbuilt, filled, finished) returns `base.GetCollisionBoxes`, the static JSON boxes.
`GetParticleCollisionBoxes` follows the same rule so dropped items and particles don't rest on air.

**Post boxes are hand-written for the `west` orientation and rotated with `Cuboidf.RotatedCopy` around the block centre**, the same angles as `SidingWallEntity.RotationYDeg`.
Cache the four rotated arrays per layout in static fields; they never change.
`cornerout` gets the posts of both legs, including the shared corner post.

**Selection boxes don't change.**
The player has to be able to click the frame to add infill.

**Adding infill refuses if an entity overlaps the full slab box.**
Otherwise a player standing between the studs gets the wall built around them.
Send an ingame error (`vssiding:build-occupied`) and return `true`, the same shape as the existing afford check.

**Retention is untouched.**
No infill already means `GetRetention` returns 0 (decision 0003), so the scan and the collision agree the frame is open.

## Alternatives considered
- **No collision at all for a framing-only wall.** Simpler, but you'd walk through the posts, which are visible solid timber.
- **Keep plates in the collision.** Correct for a single cell, but stacked walls become impassable at head height (worked through above).
- **Collision boxes from the shape's `framing` elements.** Generic, but the shape has exactly four framing elements per layout, and the plates are the ones we don't want.
- **A `built` variant axis so collision stays static JSON.** Doubles the block count to encode entity state in the block ID, which decision 0001 exists to avoid.

## Consequences & open questions
- Supersedes decision 0002's "no per-block-entity collision code" line; graduation should say so.
- Does `GetCollisionBoxes` get called client-side before the entity has synced? A missing entity should fall back to the full boxes, never to posts.
- A player standing inside a frame when another player adds infill: the overlap check covers every entity, not just the builder.
- Once `window-frames` exists, an unglazed window is a framing-only wall, so it becomes walk-through too. That's probably right (it's a hole), but check it looks right in playtest.
- Test: `ComputeCollisionBoxes(layout, side, framing, infill)` as a pure function, asserted against whole expected `Cuboidf[]` values.
