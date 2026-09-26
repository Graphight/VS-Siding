# Proposals

An idea that's been thought through but not acted on. Mutable: edit freely, argue in the doc, change your mind.

- One file per idea: `slug.md`, no number. Numbering happens on graduation.
- Same headings as a decision (see `../README.md`), so the two diff against each other.
- Acting on one **graduates** it: the decision takes the next `NNNN` in `../decisions/`, and the proposal file is deleted.

## Open

- `fireproof-infill`: decision 0033 noted a wall still burns like a plank no matter its infill, because `GetCombustibleProperties` is never overridden and always answers with the block's own wood-plank numbers.
The proposal overrides it to reuse `HitLayerMaterial`, the same peeled-layer lookup `GetResistance`/`GetBlockMaterial` already use, so only a `Wood`-tagged layer stays flammable.
- `stairs-against-walls`: stairs along a wall stand in the room cells and leave the wall's open 12/16 beside every step.
The proposal adds a step layer that continues the stair beside it into the wall's open part, built by clicking the wall with that stair in hand.
- `thin-floor-framing`: floors built like the walls, a 4/16 layered panel flush with the top of its cell, placed from the framing row.
Its top face can honestly be `sidesolid`, so most of vanilla's placing and attachment comes free; the deck is its rim joist.
- `hanging-under-thin-floors`: a thin floor's underside is 12/16 above the cell boundary, so hung lanterns float or are refused.
The proposal shifts the hung block's mesh and boxes up to meet it, reusing decision 0035's offset.
- `horizontal-boards`: a player asked for flat boards running sideways; today's `boards` style is one slab per face with its texture turned 90° for vertical boards (decision 0007).
The proposal adds `hboards` to the picker's boards row, the same slab with the texture unturned, and renames `boards` to "Vertical boards".
- `hosted-light-sources`: a torch or lantern hosted in a sealed wall's cell lights nothing, because `GuestLightPatches` raises the cell's absorption to the wall's 99 and vanilla's block-light walk subtracts the source cell's own absorption before light leaves it.
The proposal exempts the source's own cell only while vanilla spreads or removes that source's light, so sunlight still meets the sealed wall and a burnt-out torch still goes dark.
- `creatures-off-walls`: most vanilla animals step 1.1251 blocks, so they climb a 1.0-tall wall and walk along its thin top.
The proposal adds `canStep: false` to `wall.json`, the attribute vanilla fences use, which both the pathfinder and the step-up physics honour.

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
