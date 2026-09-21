# Proposals

An idea that's been thought through but not acted on. Mutable — edit freely, argue in the doc, change your mind.

- One file per idea: `slug.md`, no number. Numbering happens on graduation.
- Same headings as a decision (see `../README.md`), so the two diff against each other.
- Acting on one **graduates** it: the decision takes the next `NNNN` in `../decisions/`, and the proposal file is deleted.

## Open

Build order — each one should be demoable in game before the next starts.

- `handbook-page` — a Guides page in the survival handbook teaching the saw gesture, the four modes and the layers, plus a blurb on the block's own page.
- `wall-tooltip-layers` — looking at a wall names its framing, infill and finished faces, and what the next click would add.
- `mode-icons` — four SVG icons for the saw's tool modes, so the mode picker stops being four blank tiles.

## Parked

Thought through and deliberately not planned; the reason is what would have to change to revive it.

- `multiple-walls-per-cell` — a `cornerout` already covers any two adjacent faces of a cell, which is every L corner and every T-junction. What's left is two walls on *opposite* faces of one cell, 0.75 apart, which no ordinary building needs, and the inside-corner notch, which decision 0002 already calls cosmetic. Revive if players show a real build that needs it.
- `furniture-against-thin-walls` — placement is gated by cell occupancy, so nothing goes in the empty 3/4 of a wall's cell. But a wall built from inside the room hugs the room-side face: furniture in the next cell sits flush against it, and the wall's own cell falls outside the room. The only real loss is one side of an interior partition. The fix (chisel-style voxel merging of a wall and another block into one entity) is far bigger than that loss. Revive if the partition case turns out to matter in play, and say so in the handbook page meanwhile.
- `auto-corners` — walls picking their own corner piece from neighbours, fence-style. Great in theory, but players building something unusual would spend their time fighting the auto-correct over the pieces they placed on purpose. Placing corners by hand, plus decision 0026's in-place upgrade for ones found late, keeps the player in charge. Revive only if hand-placed corners turn out to be the main complaint in play.
