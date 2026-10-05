# 0058 — Primitive framing

- Status: Accepted
- Created: 2026-09-30
- Reflects: mod page comments https://mods.vintagestory.at/vssiding#cmt-242349 and #cmt-242478; One-Roof 1.12.0's assets; branch `feat/primitive-framing`; `SidingWallBlock.HasBuildSignal`/`IsBuildSignal`/`MatchFraming`, `PlaceWallFrame`, `SidingFloorBlock.OnBlockInteractStart`, `config/materials.json`, `patches/stone-offhand.json`, `patches/stick-frame-behavior.json`; `RoomScenarios`; vanilla 1.22 `stone.json`, `stick.json` and `CollectibleBehaviorGroundStorable` (decompiled); decisions 0001, 0005, 0006, 0026, 0050; split on 2026-10-04 into this, `primitive-earth-layers` and `tent-walls`; unit and end-to-end tests pass; played on 2026-10-05

## Summary
Players asked for "primitive" walls, meaning walls a stone-age player can build.
The proposal adds a stick framing for walls and floors, and lets a stone in the off hand stand in for the saw as the build signal, without consuming it.
Everything after the frame stays the one shared wall system.
The new materials that were once part of this proposal are now `primitive-earth-layers` and `tent-walls`, each its own session, and neither blocks this one.

## Context
**The saw is the only metal gate.**
The build signal check, then `HasSawInOffhand`, matched only `game:saw-*`, and the cheapest saw is copper.
`PlaceWallFrame` sat only on planks, and vanilla planks come from sawing logs.
So a stone-age player could not raise a single frame, wall or floor, and nothing downstream of the frame was reachable.

**Most layers are already stone age.**
Infills `wattle`, `straw`, `clay` and `stone-{rock}`, and finishes `daub`, `daub-{color}`, `drystone-{rock}` and `shakes-{wood}` (both its shakes and logs styles, which consume a placed log a stone axe fells), need no metal to make.
Planks, brick and metal plate each need a saw, a kiln or smelting, so their own recipes keep them late.
So the frame and the signal are the whole gate: with those two, a stone-age player has a wattle and daub house.

**What was asked.**
One player asked for "a primitive frame that doesnt require a saw that can only accept certain materials? ie wattle daub, logs, rammed earth, straw, etc.", with "sticks in offhand and cattails/cordage in main hand", the "opposite of one roof primitive frames".
Of those materials, only rammed earth is missing (`primitive-earth-layers`): the logs style of `shakes-{wood}` is already a stone-age finish.
Another said primitive walls would put the mod in their baseline pack, which means it has to work from the first day.

**What One-Roof does.**
One-Roof 1.12.0 ships no primitive frames; nothing in its assets names one.
It does ship a stone hammer, knapped from flint or rock and hafted on a stick, flagged `storageFlags: 257` so it sits in the off hand while the material goes in the main hand.
That is the saw's gesture with a stone-age tool, and the requester may mean that or One-Roof's unreleased 2.0.

**The saw is not consumed.**
It takes no durability anywhere in the build flow; it only tells a right-click apart from vanilla's own, as decision 0006 set up.
Framing consumes no nails either, so there is no cost to balance a saw-free path against.

## Design
**A stick framing entry.**
A `sticks` entry in `Framings`, consuming and dropping four of `game:stick`.
Four matches wattle, so eight sticks make a sealed wall, and sticks are cheap enough that double the plank count still undercuts planks.
Its texture is `game:block/wood/bark/oak`.
The first pick was `debarked/oak`, the one vanilla draws the stick item with, but in play it could not be told from an oak plank frame: the two average within 5% of each other in brightness.
Oak bark is about 30% darker and furrowed top to bottom, which reads as unpeeled poles.
It is one more material key, read by the same block class as every other framing (decision 0001).

**`PlaceWallFrame` on sticks.**
The same behavior, patched onto `game:stick` alongside the plank patch.
Sticks are also wattle's `Consumes`, so on a bare frame a stick click matches both a framing and an infill.
Planks never meet this, since planks are a finish and not an infill, and two branches of `SidingWallBlock.OnBlockInteractStart` match the held item against `Framings` before the infill match: the deck add (with the deck lit, on a side face) and decision 0026's corner upgrade (in corner mode).
On a bare frame those two branches skip any held item that also matches an infill (`SidingWallBlock.MatchFraming`), so sticks layer wattle; a stick deck then needs a filled wall, and a corner upgrade a plank in hand, since the upgrade only applies to a bare frame.

**A plain stick click on a bare frame always fills it.**
That holds on every face, so the clash is wider than the two branches: planks on a bare frame's top stack the next course and on its side frame the next cell, both through `PlaceWallFrame`, and sticks there lay wattle.
A sneak-click skips the block's interaction and reaches `PlaceWallFrame`, which stacks or extends.
Sticks carry vanilla's `GroundStorable` ahead of `PlaceWallFrame`, but it only claims a sneak-click on a top face that answers `CanAttachBlockAt`, and a bare floor and an undecked wall both answer false there.
With Floor picked, a framing item on a wall lays a floor beside it, and that guard reads `MatchFraming` too, so sticks still fill a bare wall or a bare deck there as they did before sticks were a framing.

**Stick floors.**
`PlaceWallFrame` already lays a floor when Floor is picked, and sets the floor's `Framing` to whatever key the held item matched, so sticks lay stick joists with no floor code.
The same clash shows up once: `SidingFloorBlock.OnBlockInteractStart` fills a bare floor with any held infill, so a stick click on a bare floor's top fills it with wattle where a plank click would carry the run on.
Sneaking skips the block's interaction and reaches `PlaceWallFrame`, which is the rule planks already follow on a filled floor, so a stick run is carried on with a sneak-click and the guide says so.

**A stone as a second build signal.**
`HasSawInOffhand` becomes `HasBuildSignal`, a check for either a saw or any `game:stone-*` in the off hand, and every caller (framing, layering, the floor, and the mode picker through `SidingModePicker.IsOurs`) follows.
Every player carries stones from the first minute, and few keep one in the off hand by accident, so it still reads as "building".
It is One-Roof's stone hammer without the extra item.
Vanilla's off-hand slot only takes items with the `Offhand` (256) storage flag, and `stone.json` sets `storageFlags: 5`, so stones need a patch to 261, keeping the existing flags.
It is a `replace`, where `patches/saw-offhand.json` is an `add`, since the saw sets no flags of its own.
That patch lets stones sit in the off hand for every mod, not only this one.
The stone is never consumed; its only job is gating the right-click, exactly as the saw's is.
Stones in the main hand still layer the `stone-{rock}` infill, since the two hands are read separately.

**The guide says so.**
The handbook text told players to put a saw in the off hand and that planks build the framing.
It names the stone and sticks too, with the sneak-click rule, under a "Starting in the stone age" heading, since decision 0006 already found the off-hand gesture hard to discover, and the stone-age player this is for would otherwise never learn it.
The guide had grown to one 4,800-character page, so it is split into an overview, walls and floors, linked to each other.
The cut follows walls and floors and not modern and primitive: every layer after the frame is shared, so a primitive page would repeat the layering text or run to three sentences.

**No per-framing restrictions.**
A stick frame takes every infill and finish a plank frame does.
The two signals are interchangeable as well: a stone raises a plank frame and a saw a stick one.
Neither signal costs anything, and planks still need a saw to make, so a rule tying planks to the saw would gate nothing and would need a matching rule for every plank finish.
This is a looks mod with some function, not a realism mod: a player can peel a stick frame's layers and lay better ones when they have them, or retire the frame and raise a plank one.
Late materials gate themselves through their own recipes, and by the copper age planks come faster than sticks, so the stick frame is chosen for its look rather than forced by progression.

## Alternatives considered
- **Keep one proposal for the frame and all six new materials.** The frame and signal are code and one texture; the materials are five more textures and a burn rule. Together they overran a session, and the frame alone is what was promised.
- **Flax twine in the off hand.** It needs wild flax found and processed first, and stones are in every player's inventory from the start; it would need the same storage flag patch.
- **A stone hammer like One-Roof's.** A new item, recipe and texture for what a plain stone already does.
- **Restrict a stick frame to primitive layers, as requested.** Declined: two wall rule sets to keep in step for a difference the recipes already enforce, and players can swap layers later anyway.
- **Consume the off-hand item per frame.** Plank framing consumes no fasteners, so this would make the early path cost more than the late one.
- **A stone axe as the off-hand signal.** Players hold axes for other work, so it would claim right-clicks they meant for something else.
- **No off-hand signal for sticks.** A right-click with sticks would then take every stick right-click in the game, which decision 0006's off-hand signal exists to avoid.

## Consequences & open questions
- Played: sticks and a stone build what planks and a saw do, sticks fill a bare frame with Floor picked, and the bark frame is told apart from a plank one.
- A stone makes the saw optional for every player, since either signal works with every material.
That was noticed in play and left: the saw was only ever a token, and the rule that would stop it gates nothing.
Revisit if players say it cheapens the saw; the smallest rule then is that any material consuming planks needs the saw.
- A rougher frame, with spurs and knots, was wanted and left out: every framing shares one set of shape elements, so it needs framing-specific elements from `WallShapeGen` and is its own proposal, `rough-pole-frames`.
- A stone in the off hand costs 20% more hunger while it sits there: vanilla 1.22.7's `InventoryPlayerHotbar.updateSlotStatMods` sets `hungerrate` to `OffHandHungerPenalty` (0.2) for any off-hand item with no `statModifier` attribute, and `stone.json` has none.
The saw carries the same penalty.
The other off-hand readers in the four assemblies are tool checks (hammer, tongs, wrench) and rendering, and none matches a stone.
The guide says so, since the stone path is for first-day players.
- A stick corner upgrade is not reachable with sticks in hand, since the upgrade needs a bare frame and sticks fill one.
- The end-to-end scenario sets the off-hand stack directly, so it checks the patched storage flags by value and not by moving a stone into the slot.
- `quieter-tooltip` keys the full tooltip on the build signal, so it calls `HasBuildSignal`.
