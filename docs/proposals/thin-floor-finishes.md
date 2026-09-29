# Thin floor finishes

- Status: Draft
- Created: 2026-09-29
- Reflects: split out of `thin-floor-framing`; decisions 0007, 0019, 0027, 0045; not yet played

## Summary
`thin-floor-framing` draws a floor's top and underside as plain slabs.
This proposal gives them styles: floorboards with visible boards, their direction, a lath-and-plaster ceiling, and glazed floors.

## Context
A wall finish names its own element per face (decision 0007) and picks a style from the picker (decision 0027).
Most of those elements are wall-shaped: weatherboard laps step outward down a vertical face, and shakes overlap downhill.
Laid flat, a weatherboard floor is a staircase of ridges, so a floor cannot take a wall finish's `Elements` as they stand.

## Design
**Floor elements per finish.**
A finish entry gains a floor pair beside its wall pair, e.g. `FloorElements: { top: "top-boards", bottom: "bottom-lath" }`; a finish without one draws the plain slab.
`WallShapeGen`'s floor table grows the matching groups.

**Board direction.**
Floorboards run across the joists, as in a real floor, so the floor's `side` already fixes them and no picker row is needed.

**Glazing.**
A glazed floor is a skylight in the storey below; it needs 0019's transparent pass and a decision about which light patches apply to a horizontal pane.

## Alternatives considered
- **Reuse the wall `Elements` on a floor.** Weatherboard and shakes read as ridges when laid flat.
- **A picker row for board direction.** A floor already has an orientation; a second one would contradict it.

## Consequences & open questions
- Which of the existing styles have a sensible flat form: `boards` and `hboards` do; `weatherboard` and `shakes` probably do not.
- Brick and ashlar on a floor are pavers, which may want their own bond.
