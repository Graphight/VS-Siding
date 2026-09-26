# Thin floor framing

- Status: Draft
- Created: 2026-09-25
- Reflects: decisions 0001, 0002, 0020; the `floors-between-storeys` deck; a session discussion, not a player request; not yet played

## Summary
A floor built the way the walls are: a 4/16 layered panel flush with the top of its cell, placed as a new `floor` option on the picker's framing row.
Joists are the framing, pugging the infill, floorboards the top finish and lath and plaster the ceiling.

## Context
Vanilla floors are a full block of planks or a slab, which eats headroom and looks nothing like the walls.
The deck of `floors-between-storeys` closes the slot at the wall, and its flush-top 4/16 height was chosen to meet this floor.
Nobody has asked for thin floors yet; this is written down so the deck's shape does not paint it into a corner.

## Design
**Flush top, open underneath.**
The panel's top face is the whole 16×16 at the height a plank block's would be, so the block can honestly set `sidesolid UP: true` and leave the other faces false.
That is the flag the walls had to fight (0002, 0020); here most of vanilla falls out on its own:

| Consumer | Top face | Bottom face |
| --- | --- | --- |
| `CanAttachBlockAt` | rugs, torches work | nothing hangs (see `hanging-under-thin-floors`) |
| `GetRetention` | seals | the room below walks into the open part, then meets the top |
| `AllowSnowCoverage`, `CanCreatureSpawnOn` | like a plank floor | n/a |

**The same layers as a wall.**
Framing, infill and two finishes key into the same dictionaries (0001), with the panel laid flat instead of standing.

**Meeting the wall.**
A wall with a deck at the same course; the deck is the floor's rim joist.

## Alternatives considered
- **Vanilla slabs.** Already flush-top, but 8/16 thick and single-material; no joists, no ceiling finish.
- **Floor at the bottom of its cell.** Ceiling flush instead of floor, so rugs and furniture float 12/16 above the floor line. Walking surfaces matter more.

## Consequences & open questions
- The lower storey loses 4/16 of headroom in the cell holding the floor.
- The full 0020 sweep has to be redone for a horizontal panel before building; decision 0020's table is the starting point, not the answer.
- A new `layout` or a new block: decide against 0001's variant rule when this is picked up.
