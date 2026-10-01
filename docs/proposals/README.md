# Proposals

An idea that's been thought through but not acted on. Mutable: edit freely, argue in the doc, change your mind.

- One file per idea: `slug.md`, no number. Numbering happens on graduation.
- Same headings as a decision (see `../README.md`), so the two diff against each other.
- Acting on one **graduates** it: the decision takes the next `NNNN` in `../decisions/`, and the proposal file is deleted.

## Open

- `atlas-integration-tests`: unit tests never run a world, so bugs in how vanilla calls the mod (saved guest walls, fire, mobs, furniture neighbours) are only caught in playtests.
The proposal adds a second test project on Atlas, a headless server inside `dotnet test`, with one scenario per bug a playtest has caught.
- `hosted-light-sources`: a torch or lantern hosted in a sealed wall's cell lights nothing, because `GuestLightPatches` raises the cell's absorption to the wall's 99 and vanilla's block-light walk subtracts the source cell's own absorption before light leaves it.
The proposal exempts the source's own cell only while vanilla spreads or removes that source's light, so sunlight still meets the sealed wall and a burnt-out torch still goes dark.
- `primitive-framing`: the copper saw is the only metal gate on building a wall, so a stone-age player cannot raise a frame.
The proposal adds a stick framing, accepts a stone in the off hand, not consumed, as a second build signal beside the saw, and adds packed and rammed earth.
- `walls-under-roofing`: a flat-topped wall leaves a stepped triangle under a Roofing gable.
The proposal adds a raked wall, its top cut to a pitch picked on the saw and measured to match Roofing's slopes, without reading Roofing's state.

## Parked

Thought through and deliberately not planned; the reason is what would have to change to revive it.

- `multiple-walls-per-cell`: a `cornerout` already covers any two adjacent faces of a cell, which is every L corner and every T-junction.
What's left is two walls on *opposite* faces of one cell, 0.75 apart, which no ordinary building needs, and the inside-corner notch, which decision 0002 already calls cosmetic.
Revive if players show a real build that needs it.
- `auto-corners`: walls picking their own corner piece from neighbours, fence-style.
This works in theory, but players building something unusual would spend their time fighting the auto-correct over the pieces they placed on purpose.
Placing corners by hand, plus decision 0026's in-place upgrade for ones found late, keeps the player in charge.
Revive only if hand-placed corners turn out to be the main complaint in play.
- `guest-furniture-collision`: a hosted chest or trunk lets a player walk in through its front, because its shifted box sticks into a cell vanilla's collision tester never asks.
The proposal is written; revive when the walk-through bothers players enough to be worth a playtest.
- `chiselling-walls`: vanilla refuses to chisel a non-cube block, and the proposal converts a wall through its collision-box path with materials rebuilt from its layers.
Revive when players ask for it; it is the largest of these by far.
