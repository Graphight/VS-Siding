# Proposals

An idea that's been thought through but not acted on. Mutable: edit freely, argue in the doc, change your mind.

- One file per idea: `slug.md`, no number. Numbering happens on graduation.
- Same headings as a decision (see `../README.md`), so the two diff against each other.
- Acting on one **graduates** it: the decision takes the next `NNNN` in `../decisions/`, and the proposal file is deleted.

## Open

- `just-workflow-docs`: `README.md` and `CONTRIBUTING.md` only show `./build.sh` and raw `dotnet`, while `CLAUDE.md` already documents `just build`/`just test`/`just deploy`.
The proposal brings the other two docs in line.
- `sectioned-mode-picker`: the saw's mode picker becomes rows of toggles, framing, boards and logs, with choices stored per character.
Logs gain the style choice planks got in decision 0027.
- `opaque-infill-seam`: a two-high wall of straw, wattle, clay or rubble shows a seam at the join where decision 0008 drops the middle plate.
The infill panel's `Flat` UV rule restarts the texture at each element's own base instead of the block's own height, so the proposal switches it to `Positional`, the same fix decision 0028 already made for weatherboard.
- `fireproof-infill`: decision 0033 noted a wall still burns like a plank no matter its infill, because `GetCombustibleProperties` is never overridden and always answers with the block's own wood-plank numbers.
The proposal overrides it to reuse `HitLayerMaterial`, the same peeled-layer lookup `GetResistance`/`GetBlockMaterial` already use, so only a `Wood`-tagged layer stays flammable.

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
