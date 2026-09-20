# Proposals

An idea that's been thought through but not acted on. Mutable — edit freely, argue in the doc, change your mind.

- One file per idea: `slug.md`, no number. Numbering happens on graduation.
- Same headings as a decision (see `../README.md`), so the two diff against each other.
- Acting on one **graduates** it: the decision takes the next `NNNN` in `../decisions/`, and the proposal file is deleted.

## Open

Build order — each one should be demoable in game before the next starts.

- [`sidesolid-derived-behaviour`](sidesolid-derived-behaviour.md) — `sidesolid: false` was set for rendering; vanilla reads it for seven things. Decide the five we haven't.
- [`shake-profile`](shake-profile.md) — `front-shakes` is a byte-identical copy of `front-weatherboard`; give shakes a real tapered shingle profile and delete the copies.
- [`buried-shape-faces`](buried-shape-faces.md) — every box declares six faces, including ones permanently buried; measure the cost, then prune by rule.
- [`upgrade-frame-to-corner`](upgrade-frame-to-corner.md) — corner tool mode on an existing frame turns it into a `cornerout` in place, for T-junctions found late.
- [`finish-style-choice`](finish-style-choice.md) — `weatherboard` and `boards` tool modes on planks, so partitions and room-side walls get the right face.

## Parked

Thought through and deliberately not planned; the reason is what would have to change to revive it.

- `multiple-walls-per-cell` — a `cornerout` already covers any two adjacent faces of a cell, which is every L corner and every T-junction. What's left is two walls on *opposite* faces of one cell, 0.75 apart, which no ordinary building needs, and the inside-corner notch, which decision 0002 already calls cosmetic. Revive if players show a real build that needs it.
- `furniture-against-thin-walls` — placement is gated by cell occupancy, so nothing goes in the empty 3/4 of a wall's cell. But a wall built from inside the room hugs the room-side face: furniture in the next cell sits flush against it, and the wall's own cell falls outside the room. The only real loss is one side of an interior partition. The fix (chisel-style voxel merging of a wall and another block into one entity) is far bigger than that loss. Revive if the partition case turns out to matter in play, and say so in the handbook page meanwhile.
- `auto-corners` — walls picking their own corner piece from neighbours, fence-style. Great in theory, but players building something unusual would spend their time fighting the auto-correct over the pieces they placed on purpose. Placing corners by hand, plus `upgrade-frame-to-corner` for ones found late, keeps the player in charge. Revive only if hand-placed corners turn out to be the main complaint in play.
