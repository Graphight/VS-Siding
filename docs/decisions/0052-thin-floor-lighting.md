# 0052 — Thin floor lighting

- Status: Withdrawn
- Created: 2026-09-29
- Reflects: branch `thin-floor-lighting`; commits `ab56ea9` (a fix, reverted in `b959b42`); vanilla 1.22 `JsonTesselator.SetUpLightRGBs` (decompiled); playtests on 2026-09-30 of an oak and red clay floor over a pit

## Summary
A sealed thin floor over a pit showed a black band under its lit underside.
The band is the underside of the floor cells that rest on solid ground at the pit's edge, reading the ground block's stored light of 0.
It only shows through the 12/16 gap between the ground and the underside, so it is left as it is.

## Context
Decision 0050's playtest saw a floor one block off the ground render its underside badly lit, and this proposal was to measure it before choosing a fix.
A sealed floor absorbs 99, so its own cell stores 0, and its underside is drawn at y 12/16, inside the cell.

## Design
**Which cell the underside reads.**
`JsonTesselator.SetUpLightRGBs` lights every face of a JSON mesh from the neighbour cell the face points at, wherever the face sits inside its own cell.
The underside is a down face, so it reads the cell below: the room's light over a pit, the ground block's 0 over solid ground.

**The playtest.**
A first guess blamed a neighbour's face pointing into the floor cell, and `ab56ea9` gave a sealed floor cell the light of the cell below, as 0018 does for a wall cell's open side.
The band did not change, because over solid ground the cell below stores 0 either way.
Digging out the block under the band lit the underside at once, and putting it back brought the band back, which pins it on the ground block.
The fix covered no case anyone had seen, so it was reverted.

**Not fixed.**
The only way to see the band is at an angle through the 12/16 gap, from a pit or cellar dug beside a floor laid on the ground.

## Alternatives considered
- **Rewrite the ground cell's stored light in 0018's postfix.** The gap opens sideways, so the light would have to come from a horizontal neighbour, and the brightest one at a house's edge is outdoor daylight, the leak 0018 was written to stop. Revive if players build cellars under ground-level floors and the band bothers them.
- **Absorb nothing, like glazing.** Sunlight would pass through every floor and the room below would count as skylit, which breaks cellars (0015).
- **Put the underside on the cell boundary.** It is the floor-at-the-bottom layout decision 0050 rejected.

## Consequences & open questions
- A glazed floor over solid ground shows the same band, for the same reason.
