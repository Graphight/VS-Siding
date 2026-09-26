# Creatures off walls

- Status: Draft
- Created: 2026-09-25
- Reflects: decisions 0008, 0020, 0035; `wall.json`'s collision box; decompiled 1.22 `Block.CanStep`, `AStar.traversable`, `EntityBehaviorControlledPhysics.FindSteppableCollisionBox`; vanilla fence and entity assets

## Summary
Animals step up onto a thin wall and walk along its 4/16 top, so a pen of siding walls does not hold them.
Most vanilla creatures step 1.1251 blocks, and our wall is 1.0 tall.
Vanilla fences solve exactly this with one JSON attribute, `canStep: false`, and the proposal gives the wall the same.

## Context
**Creatures step higher than a block.**
Vanilla's default `StepHeight` is 0.6 (`EntityBehaviorControlledPhysics.cs:63`), but 52 of vanilla's entity files override it: 66 `stepHeight: 1.1251` entries among them, a handful from 1.0001 to 1.125, ten at 2.1251, and 3.1251 for goats and the tamed elk.
So nearly every animal climbs any 1.0-tall box, and the wall's box is 1.0 tall (`wall.json:268`, `y2: 1`).

**Both the physics and the pathfinder let them.**
`FindSteppableCollisionBox` (`EntityBehaviorControlledPhysics.cs:751-791`) takes any box whose top is within `StepHeight` of the entity's feet, so a sheep walking into the panel steps onto it.
`AStar.traversable` (`AStar.cs:127-268`) treats a cell as standable when the block below it has collision boxes, and plans a step up into a colliding cell when the entity fits above the box's top; the wall passes both, so a creature's path runs up onto the wall and along it.
A one-high vanilla block wall behaves the same, but its top is a whole block wide; ours is a quarter of one, and players build pens with it.

**What vanilla fences do.**
Fences, gates, palisades, wattle fences and troughs (20 blocktypes, e.g. `fence-bamboo.json:6`) set `attributes.canStep: false`, read into `Block.CanStep` (`Block.cs:523`).
Their 1.3125 collision height is a separate matter: it stops a *jump*, not a step.

## Design
**Add `canStep: false` to `wall.json`'s `attributes`.**
No code; the flag is static per block type, and the wall is one block type.

Every consumer, from the four decompiled game DLLs:
- `AStar.cs:139`: a cell whose block below cannot be stepped on is not standable, so no path runs along a wall's top.
- `AStar.cs:218`: a colliding cell whose block cannot be stepped on is not traversable, so no path steps up into it.
- `EntityBehaviorControlledPhysics.cs:761`, `:802`: the step-up skips the block's boxes for any entity shorter than five times the block's first static collision box, i.e. shorter than 5 blocks, so a wandering or fleeing creature off any path cannot step up either, whatever its `StepHeight`.
- `:771`, `:812`: the same skip for a block sitting on top of a wall, when that block has no solid top or bottom side.

A narrow creature still walks into the open 12/16 of the cell: `AStar.cs:132` checks the node's own block for `CanStep` only when the creature collides there.

## Alternatives considered
- **Raise the box to 1.3125 like a fence.** Stops a step of 1.1251 but not a goat's 3.1251, and it stops players jumping onto a one-high wall, which they can do today. `canStep` is the part of the fence that does this job.
- **Override `GetTraversalCost` to refuse creatures.** Keeps paths off the wall but not a creature wandering or fleeing without a path, since physics never asks it.
- **Nothing: build pens two walls high.** True of vanilla block walls too, but a one-high thin wall reads as a fence to a player, and a fence should hold.

## Consequences & open questions
- Players cannot auto-step onto a wall either; their step is 0.6 and never reached a 1.0 top, so nothing changes for them, and jumping is untouched.
- A hosted cell (0035) answers with the host block's own `CanStep`: an animal can step onto a hosted chest, as onto any chest.
- Decision 0008's framing-only frame collides on posts and a top plate only, so a bare frame stays walk-through for animals as for players: that is a doorway by design, not a leak.
- The `floors-between-storeys` deck would sit on a block that cannot be stepped on, so `AStar.cs:139` would keep creatures off a deck's top; check that if both land.
- Decision 0020's consumer table is of `sidesolid`; the graduating decision should list `CanStep`'s consumers the same way, to redo on a game update.

## Stages
1. **Fix:** `canStep: false` in `wall.json`.
2. **Playtest:** a one-high pen of filled walls holding a sheep, a pig and a goat, with feed outside it; a bare-frame cell as the control; the player jumping onto the wall.
3. **Graduate.**
