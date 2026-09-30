# Thin floor finishes

- Status: Draft
- Created: 2026-09-29
- Reflects: split out of decision 0050; its first playtest; decisions 0007, 0019, 0027, 0045; not yet played

## Summary
Decision 0050 draws a floor's top and underside as plain slabs.
This proposal gives them styles: floorboards with visible boards, their direction, a lath-and-plaster ceiling, and glazed floors.

## Context
A wall finish names its own element per face (decision 0007) and picks a style from the picker (decision 0027).
Most of those elements are wall-shaped: weatherboard laps step outward down a vertical face, and shakes overlap downhill.
Laid flat, a weatherboard floor is a staircase of ridges, so a floor cannot take a wall finish's `Elements` as they stand.

## Design
**Floor elements per finish.**
A finish entry gains a floor pair beside its wall pair, e.g. `FloorElements: { top: "top-boards", bottom: "bottom-lath" }`; a finish without one draws the plain slab.
`WallShapeGen`'s floor table grows the matching groups.

**Board direction on the Boards row.**
Every floor's joists run north-south (decision 0050), so the floor has no orientation for the boards to follow.
The picker's Boards row picks it instead, the way it picks a wall's board style: `boards` lays them north-south and `hboards` east-west, stored per face like `FrontStyle` (decision 0027).
Real floorboards run across the joists, so east-west would be the default; `weatherboard` has no flat form and falls back to it.

**Glass floors.**
The first playtest wanted them: a glass roof where a Roofing roof will not do, and see-through floors over water.
A glazed floor takes glass as its infill and no finish, as a glazed wall does (decision 0019).
The pane goes in the transparent pass the way a glazed wall's does, and absorbs no light, so it stays out of whatever `thin-floor-lighting` settles for sealed floors.
Neighbouring glazed floors merge into one pane, which the joists already allow: they run through every cell, so only the rims drop.

## Alternatives considered
- **Reuse the wall `Elements` on a floor.** Weatherboard and shakes read as ridges when laid flat.
- **Board direction from the floor's own orientation.** The first build did this; the joists followed the player's facing and the boards followed the joists, so the grain changed with the way the player happened to face while framing.

## Consequences & open questions
- Which of the existing styles have a sensible flat form: `boards` and `hboards` do; `weatherboard` and `shakes` probably do not.
- Brick and ashlar on a floor are pavers, which may want their own bond.
