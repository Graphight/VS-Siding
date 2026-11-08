# Proposals

An idea that's been thought through but not acted on. Mutable: edit freely, argue in the doc, change your mind.

- One file per idea: `slug.md`, no number. Numbering happens on graduation.
- Same headings as a decision (see `../README.md`), so the two diff against each other.
- Acting on one **graduates** it: the decision takes the next `NNNN` in `../decisions/`, and the proposal file is deleted.

## Open

- `bamboo-frames`: a bamboo frame draws the stick frame's crooked poles in a bamboo texture (0088), and a bamboo diagonal and post draw the plain frame.
The proposal gives bamboo straight poles with node rings on every pole, in two patterns by cell parity, on walls, corners, floors, decks, diagonals and posts, a bare post being one fat ringed pole.
- `tile-finishes`: a player asked for ceramic tiles as a finish.
The proposal lays vanilla's fired tiles one at a time, four slots to a face cut on the cell's grid, any colour in each, on walls, corners, floors and decks.
Decision 0075 (issue #108) changed which face a finish click lands on, and a tile follows it: a click on a finished face puts the finish on the face behind it while that face is bare.
- `real-smoke-flues`: a player vents a kitchen through trellises, which costs the room its heat, and asks for a flue or a hearth in a wall.
Real Smoke stops smoke at a face that dams liquid, which a filled wall already reports, so the proposal plays what reading cannot settle and adds a vented wall on that mod's chimney behaviour only if no vanilla block already serves.
- `walls-under-roofing`: a flat-topped wall leaves a stepped triangle under a Roofing gable.
The proposal adds a raked wall, its top cut to a pitch picked on the saw and measured to match Roofing's slopes, without reading Roofing's state.
A first cut takes framing and infill only, one generated shape per pitch as the diagonal wall's is; which cells of a Roofing gable are free for it is still to be built and looked at.
- `chiselling-walls`: vanilla refuses to chisel a non-cube block, and the proposal converts a wall through its collision-box path with materials rebuilt from its layers.
Parked until players asked; three requests now lead to it, for windows, trims and a way through for axles.
It is the largest of these by far, so it is last.

## Parked

Thought through and deliberately not planned; the reason is what would have to change to revive it.

- `multiple-walls-per-cell`: a `cornerout` already covers any two adjacent faces of a cell, which is every L corner and every T-junction.
What's left is two walls on *opposite* faces of one cell, 0.75 apart, which no ordinary building needs, and the inside-corner notch, which decision 0002 already calls cosmetic.
Revive if players show a real build that needs it.
A one-wide stairwell was the first candidate (decision 0069), but each slab is 0.25 thick, which leaves 0.5 between them for a player 0.6 wide.
- `auto-corners`: walls picking their own corner piece from neighbours, fence-style.
This works in theory, but players building something unusual would spend their time fighting the auto-correct over the pieces they placed on purpose.
Placing corners by hand, plus decision 0026's in-place upgrade for ones found late, keeps the player in charge.
Revive only if hand-placed corners turn out to be the main complaint in play.
