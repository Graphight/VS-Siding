# Proposals

An idea that's been thought through but not acted on. Mutable — edit freely, argue in the doc, change your mind.

- One file per idea: `slug.md`, no number. Numbering happens on graduation.
- Same headings as a decision (see `../README.md`), so the two diff against each other.
- Acting on one **graduates** it: the decision takes the next `NNNN` in `../decisions/`, and the proposal file is deleted.

## Open

Build order — each one should be demoable in game before the next starts.

- `release-readiness` — modicon, version, dependency floor, save round-trip, and one whole house built for real (do the playtest first, bump the version last).

## Parked

Thought through and deliberately not planned; the reason is what would have to change to revive it.

- `multiple-walls-per-cell` — a `cornerout` already covers any two adjacent faces of a cell, which is every L corner and every T-junction. What's left is two walls on *opposite* faces of one cell, 0.75 apart, which no ordinary building needs, and the inside-corner notch, which decision 0002 already calls cosmetic. Revive if players show a real build that needs it.
- `auto-corners` — walls picking their own corner piece from neighbours, fence-style. Great in theory, but players building something unusual would spend their time fighting the auto-correct over the pieces they placed on purpose. Placing corners by hand, plus decision 0026's in-place upgrade for ones found late, keeps the player in charge. Revive only if hand-placed corners turn out to be the main complaint in play.
