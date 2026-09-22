# Lighting, round two

- Status: Draft
- Created: 2026-09-20
- Reflects: release polish planning; the open questions left by decisions 0016, 0018 and 0019

## Summary
Close out the five lighting leftovers that decisions 0016/0018/0019 wrote down and deliberately did not chase, in order of how visible each one is in play.

## Context
Three decisions already fought the same fight: a sealed wall cell stores the sunlight flooding in from outside, and everything that samples that cell shows it.
0015 fixed the room's skylight sample, 0016 fixed the wall's own faces, 0018 fixed the floor edges by swapping a sealed cell's stored light for the light of the cell its dead space opens onto.
Each fix narrowed the leak; each one recorded what it left.

Collected from those decisions' own "Consequences & open questions":

1. **Doorway sliver.** A sealed cell whose open side is a doorway takes the doorway's daylight, so a bright sliver can show beside an opening. Seen in a night playtest, never diagnosed. 0018 names the fix: take the *darker* of the open side and the room.
2. **Side AO is probably dead code.** 0016's `DoEmitSideAo` was there to suppress the ring that 0018's flat path now ignores. If it is redundant, removing it also drops the odd pre-roof corner shading 0016 noted. Untested either way.
3. **Glazing's bright neighbour.** A glazed cell is deliberately outside both 0018 patches (everything keys off `GetLightAbsorption`), so a sealed wall next to a window borders a lit cell again. 0019 says night lighting near glazing "looked slightly off" and was left alone.
4. **Double-thickness walls read order-dependent.** If a sealed cell's open neighbour is itself a sealed wall, whichever the postfix visits first wins, so a two-cell-thick wall can light differently depending on scan order.
5. **Flat shading on sealed faces.** 0018's prefix takes the flat path, so faces onto a wall lose smooth gradation. Accepted as a cost, not yet judged in play.

## Design
Checked against the decompiled `TCTCache.CalcBlockFaceLight` before any code, and two of the five premises did not survive.
What follows is written as predictions, so the playtest checks them rather than confirms them.

(2) **Side AO is not dead code.**
In the smooth path, a ring cell that emits side AO gets its light replaced by the face's own sample, so it drops out of the corner average.
0018's postfix gives a sealed cell the light of its open side, and when the panels face *in*, the open side is outdoors.
Take side AO away and the room's floor row beside that wall averages daylight back in.
Prediction: removing side AO brings the glow back in a room whose panels face in; a panels-out room looks the same either way, so testing only that one would wrongly pass the removal.

(4) **Double walls differ by about one light level.**
The outer cell reads the inner cell either before or after its rewrite.
Before, the inner cell holds the room's light minus one, since the outer cell absorbs everything it passes on; after, it holds the room's light.
Prediction: invisible in play, no fix needed.

(1) and (3) **have no traced cause.**
The postfix only ever reads a sealed cell's open side, which runs across the wall, never along it.
So "the darker of the open side and the room" has no room to compare against, and "sealed cells stop sampling the glazed cell" describes something the code never does.
Both need a reproduction before any fix is designed.

(5) stays a judgement call in play once the others are settled.

The playtest scene: a sealed room with panels out, one with panels in, a doorway, a window beside a sealed wall, and a double-thickness wall, each at noon and midnight.
Then the same panels-in room and a pre-roof corner again on a throwaway build with side AO off.

## Alternatives considered
- **A real light-propagation patch** so a sealed cell never stores the outdoor light at all. The honest fix, in the most update-fragile place in the engine; 0018 chose the tessellation-time swap to avoid it. Revisit only if these five resist.
- **Giving walls `lightAbsorption` for real.** Would fix the storage at the source and break glazing, which must transmit.
- **Shipping as-is.** Defensible for 1, 4 and 5 — each is a sliver or a subtlety. Not for 3, if a window beside a wall is the common case, which it is.

## Consequences & open questions
- Every item here is a Harmony patch or an override on a hot path, and each new condition in `SealedCellLightPostfix` costs on every chunk tessellation. Measure before adding the chain-follow.
- `RoomSkylightPatchTests` and `SealedCellLightTests` pin the patched members; extending the postfix means extending the pins.
- (1) and (4) both touch the same few lines; doing them together may be cheaper than doing them in order.
