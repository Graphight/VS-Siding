# Proposals

An idea that's been thought through but not acted on. Mutable: edit freely, argue in the doc, change your mind.

- One file per idea: `slug.md`, no number. Numbering happens on graduation.
- Same headings as a decision (see `../README.md`), so the two diff against each other.
- Acting on one **graduates** it: the decision takes the next `NNNN` in `../decisions/`, and the proposal file is deleted.

## Open

- `plaster-finish`: two players want a finish lighter than daub.
The proposal adds vanilla's four plaster blocks as flat finishes through two family templates, with no code.
- `quieter-tooltip`: looking at a wall prints up to a dozen lines.
The proposal shows only whether the wall seals unless the saw is in the off hand.
- `wall-junctions`: four players describe junctions they could not build, and a corner piece already covers a T and a crossing; a creative build confirmed the crossing.
The proposal says so in the guide and lets a filled wall be upgraded to a corner; a post for the inside corner follows one unglazed build, and the other new shapes wait on the players' answers.
- `stained-plank-finishes`: a player asked for dyed wood compat, and three mods store a coloured plank three ways.
The proposal confirms the one that should already match, adds two-placeholder family templates for Wood Stain, and leaves attribute stacks for Dyed Wood until asked.
- `modded-furniture-hosting`: Kevins Furniture's two cabinets declare every side solid, so the hosting rule refuses them as cubes.
The proposal adds an attribute a block can set to say whether it may be hosted, and ships it for those cabinets.
- `face-finishes`: a player framing in full logs wants floorboards over a beam and siding on a post, asked for as a fill block.
The proposal puts a plank finish on one face of any solid block as a vanilla decor block, flat, picked as a fourth option in the saw picker's first row.
- `real-smoke-flues`: a player vents a kitchen through trellises, which costs the room its heat, and asks for a flue or a hearth in a wall.
Real Smoke stops smoke at a face that dams liquid, which a filled wall already reports, so the proposal plays what reading cannot settle and adds a vented wall on that mod's chimney behaviour only if no vanilla block already serves.
- `walls-under-roofing`: a flat-topped wall leaves a stepped triangle under a Roofing gable.
The proposal adds a raked wall, its top cut to a pitch picked on the saw and measured to match Roofing's slopes, without reading Roofing's state.
- `diagonal-walls`: a tent built from these walls is a box, since a corner turns ninety degrees.
The proposal adds a third framing layout, a panel crossing its cell corner to corner, which is a `cornerout` for every rule but its drawing and its boxes; whether a staircase of collision boxes can be walked along is tested first.
- `chiselling-walls`: vanilla refuses to chisel a non-cube block, and the proposal converts a wall through its collision-box path with materials rebuilt from its layers.
Parked until players asked; three requests now lead to it, for windows, trims and a way through for axles.
It is the largest of these by far, so it is last.

## Parked

Thought through and deliberately not planned; the reason is what would have to change to revive it.

- `multiple-walls-per-cell`: a `cornerout` already covers any two adjacent faces of a cell, which is every L corner and every T-junction.
What's left is two walls on *opposite* faces of one cell, 0.75 apart, which no ordinary building needs, and the inside-corner notch, which decision 0002 already calls cosmetic.
Revive if players show a real build that needs it.
A one-wide stairwell was the first candidate (`wall-junctions`), but each slab is 0.25 thick, which leaves 0.5 between them for a player 0.6 wide.
- `auto-corners`: walls picking their own corner piece from neighbours, fence-style.
This works in theory, but players building something unusual would spend their time fighting the auto-correct over the pieces they placed on purpose.
Placing corners by hand, plus decision 0026's in-place upgrade for ones found late, keeps the player in charge.
Revive only if hand-placed corners turn out to be the main complaint in play.
- `guest-furniture-collision`: a hosted chest or trunk lets a player walk in through its front, because its shifted box sticks into a cell vanilla's collision tester never asks.
The proposal is written; revive when the walk-through bothers players enough to be worth a playtest.
