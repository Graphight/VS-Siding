# Thin floor lighting

- Status: Draft
- Created: 2026-09-29
- Reflects: the first `thin-floor-framing` playtest; decisions 0015, 0016, 0018, 0034; nothing measured yet

## Summary
A sealed thin floor one block above the ground renders its underside and the space around it badly lit.
This proposal is the lighting pass for floors that 0016, 0018 and 0034 were for walls, and starts by measuring rather than guessing.

## Context
A sealed floor answers `GetLightAbsorption` with 99, as a sealed wall does, so sunlight does not pass through it into the room below.
Its underside is drawn at y 12/16, inside the floor's own cell, not on the cell boundary.
The walls met the same shape of problem three times: a sealed cell stores light flowing in from outside (0015), smooth lighting averages a face's corners with the eight cells ringing it (0018), and each fix changed which cell a face reads.
The playtest saw the problem at one height, a floor one block off the ground, and did not try others.

## Design
**Measure first, the way 0018 did.**
Freeze time, build the same floor at y+1, y+2 and y+3 over open ground and inside a sealed room, and log the light the tessellator reads for the underside's faces.
The split test that settled 0018 applies here too: blank the floor cell's light entries and see whether the picture changes, before deciding which cell is to blame.

**Candidate causes, not yet tested.**
- The underside's down faces read the floor's own cell, which is dark because it absorbs 99; the fix would be the flat path 0018 gives faces onto a sealed wall cell, reading the open cell below instead.
- The corner ring reaches the sunlit cells beside the floor at the house edge, as it did beside walls.
- One block off the ground, the open part is 12/16 of air over the ground block, so the ground's own top faces sample the floor cell too.

## Alternatives considered
- **Absorb nothing, like glazing.** The underside would light normally, but sunlight would pass through every floor and the room below would count as skylit, which breaks cellars (0015).
- **Put the underside on the cell boundary.** It is the floor-at-the-bottom layout `thin-floor-framing` rejected.

## Consequences & open questions
- Whether 0018's patches can be widened to floors or need their own, and whether 0034's occlusion applies to a horizontal panel.
- Decks get the same treatment once `deck-as-floor` gives them infill.
