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
Do them in that order, each demoable, and stop when the rest stop being visible.

(1) is a one-line change to the postfix: `Math.Min` of the open-side light and the room's. The catch is that "darker" across three packed channels is not one comparison. Pick per-channel min, and say so.

(2) is an experiment before it is a change: comment out `DoEmitSideAo`/`DoEmitSideAoByFlag`, screenshot the same sealed room and the same pre-roof corner, keep the smaller code if the pixels match.

(3) needs a decision, not a patch: either glazing joins the patched set (and then a glazed wall's own cell goes dark, which is wrong — it genuinely transmits light), or the *neighbouring* sealed cells stop sampling the glazed cell. The second is right and is a condition in the postfix, not a new patch.

(4) wants the postfix to resolve chains: if the open neighbour is itself sealed, follow to *its* open side, with a hop limit. Two hops covers every wall anyone builds.

(5) is a judgement call after (1)–(4) land, because the flat path may stop being noticeable once nothing beside it is wrong.

## Alternatives considered
- **A real light-propagation patch** so a sealed cell never stores the outdoor light at all. The honest fix, in the most update-fragile place in the engine; 0018 chose the tessellation-time swap to avoid it. Revisit only if these five resist.
- **Giving walls `lightAbsorption` for real.** Would fix the storage at the source and break glazing, which must transmit.
- **Shipping as-is.** Defensible for 1, 4 and 5 — each is a sliver or a subtlety. Not for 3, if a window beside a wall is the common case, which it is.

## Consequences & open questions
- Every item here is a Harmony patch or an override on a hot path, and each new condition in `SealedCellLightPostfix` costs on every chunk tessellation. Measure before adding the chain-follow.
- `RoomSkylightPatchTests` and `SealedCellLightTests` pin the patched members; extending the postfix means extending the pins.
- (1) and (4) both touch the same few lines; doing them together may be cheaper than doing them in order.
