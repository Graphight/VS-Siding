# 0047 — Players step walls

- Status: Accepted
- Created: 2026-09-27
- Reflects: branch `stairs-against-walls`; played; decisions 0044, 0046; `SidingModSystem.PlayerStepTranspiler`/`PlayerCanStep`; decompiled 1.22 `EntityBehaviorControlledPhysics.FindSteppableCollisionBox`/`FindSteppableCollisionboxSmooth`, `EntityBehaviorPlayerPhysics.HandleSteppingOnBlocks`

## Summary
A step (decision 0046) has the same boxes as a vanilla stair, but play found players could not walk up it.
Decision 0044's `canStep: false` refuses step-up onto any wall box, the step's included.
Players now step walls again; creatures still do not.

## Context
**`canStep` belongs to the block type, not the cell.**
`Block.CanStep` is a field read straight off the block, and every wall is one block type, so it cannot be true for a stepped cell and false for the rest.
Step-up also asks the block below any box whose block has no solid top or bottom face, and a wall has neither, so a step in a stacked column is refused by the wall under it too.

**Players and creatures step through different methods.**
`EntityBehaviorPlayerPhysics.HandleSteppingOnBlocks` is the only caller of `FindSteppableCollisionboxSmooth`; every other entity steps through `FindSteppableCollisionBox`.
Both read `CanStep` twice: once for the box's block, once for the block below it.

## Design
**Transpile the smooth finder only.**
Both of its `CanStep` reads become `PlayerCanStep`, which answers true for any `SidingWallBlock` and the block's own flag otherwise.
Creature physics and the pathfinder (`AStar`) still read `canStep: false`, so decision 0044's pens hold.

**Why this is safe for players.**
Step-up lifts at most `StepHeight`, 0.6 for a player, onto a box whose top is that close to their feet.
A panel's top is 1.0 above the floor beside it, so walls stay unclimbable, and stacked walls give no ladder; this is exactly how players behaved before 0044, which said as much.
A step's treads are 0.5 apart, so they climb like the stair beside them.

## Alternatives considered
- **Flip `canStep` per cell while a step stands.** Impossible without a second block type: the flag lives on the type.
- **A position-aware transpiler, stepped cells only, for every entity.** More IL (the list and index as well as the block), it must also clear the wall below, and animals could then climb a stepped wall onto its panel top.
- **A stepped wall as its own block variant with `canStep: true`.** A new variant group renames every existing wall's block code and breaks saves.

## Consequences & open questions
- A player whose feet are already within 0.6 of a wall's top, standing on a slab beside a one-high wall, now steps onto it instead of bumping into it. They could already jump there.
- Redo on a game update: `PlayerStepPatchTests` fails if the smooth finder stops reading `CanStep` exactly twice.
