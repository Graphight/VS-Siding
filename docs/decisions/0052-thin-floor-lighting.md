# 0052 — Thin floor lighting

- Status: Accepted
- Created: 2026-09-29
- Reflects: branch `thin-floor-lighting`; commit `ab56ea9`; the user's screenshot of an oak and red clay floor with plank finishes seen from a pit below; vanilla 1.22 `JsonTesselator.SetUpLightRGBs` (decompiled); not yet replayed in game

## Summary
A sealed thin floor seen from a pit below showed a pitch-black band under its lit underside.
The underside was fine; the band was a neighbour's face reading the floor cell's stored light, which is 0.
The fix widens decision 0018's sealed-cell postfix to floors.

## Context
A sealed floor returns 99 from `GetLightAbsorption`, as a sealed wall does, so sunlight does not pass through it into the room below and its own cell stores 0.
Its underside is drawn at y 12/16, inside the floor's own cell, not on the cell boundary.
The walls met the same shape of problem three times (0015, 0018, 0034): a face reads the stored light of a sealed cell, which is not the light a player expects to see there.
The playtest screenshot showed the floor still broken, so the proposal went ahead.

## Design
**Which cell is to blame.**
`JsonTesselator.SetUpLightRGBs` lights every face of a JSON mesh from the neighbour cell the face points at, wherever the face sits inside its own cell.
So the floor's underside, a down face at y 12/16, reads the cell below and is lit correctly.
The black band is a face of a neighbouring block that points into the floor cell's open lower 12/16, and that face reads the floor cell's stored 0.
This was settled by reading the decompiled tessellator, not by the throwaway-build split test 0018 used.

**The fix widens 0018's postfix.**
In `SidingModSystem.SealedCellLightPostfix`, a sealed `SidingFloorBlock` cell takes the light of the cell below, as a sealed wall cell takes its open side's light.
On the bottom layer of the extended array the cell below is in the neighbour chunk, which has this cell in its own interior and lights it there, so the postfix skips it.
`SidingFloorBlock.IsSealed` mirrors the wall's.

## Alternatives considered
- **Absorb nothing, like glazing.** The underside would light normally, but sunlight would pass through every floor and the room below would count as skylit, which breaks cellars (0015).
- **Put the underside on the cell boundary.** It is the floor-at-the-bottom layout decision 0050 rejected.
- **A side-AO override on the floor, as 0016 gave walls.** Nothing in the screenshot or the tessellator shows the corner ring is involved, so it was not added.

## Consequences & open questions
- The fix is unplaytested: it was written from the screenshot and the decompiled tessellator, and has not been replayed in game.
- A floor sitting directly on the ground reads the solid cell's 0 and stays as before.
  This is known and deliberately not fixed: the only view into it is a 12/16 gap between floor and ground, and the aim was that the underside of a raised room is not a black box.
- Glazed floors absorb nothing, so they need nothing.
- Decks get the same treatment once `deck-as-floor` gives them infill.
