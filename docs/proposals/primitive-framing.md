# Primitive framing

- Status: Draft
- Created: 2026-09-30
- Reflects: mod page comments https://mods.vintagestory.at/vssiding#cmt-242349 and #cmt-242478; One-Roof 1.12.0's assets; `SidingWallBlock.HasSawInOffhand`, `PlaceWallFrame`, `SidingFloorBlock.OnBlockInteractStart`, `config/materials.json`; vanilla `stone.json`; decisions 0001, 0005, 0006, 0026, 0050; split on 2026-10-04 into this, `primitive-earth-layers` and `tent-walls`; not yet played

## Summary
Players asked for "primitive" walls, meaning walls a stone-age player can build.
The proposal adds a stick framing for walls and floors, and lets a stone in the off hand stand in for the saw as the build signal, without consuming it.
Everything after the frame stays the one shared wall system.
The new materials that were once part of this proposal are now `primitive-earth-layers` and `tent-walls`, each its own session, and neither blocks this one.

## Context
**The saw is the only metal gate.**
`HasSawInOffhand` matches `game:saw-*`, and the cheapest saw is copper.
`PlaceWallFrame` sits only on planks, and vanilla planks come from sawing logs.
So a stone-age player cannot raise a single frame, wall or floor, and nothing downstream of the frame is reachable.

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
A `sticks` entry in `Framings`, consuming and dropping `game:stick`, with its own texture.
It is one more material key, read by the same block class as every other framing (decision 0001).

**`PlaceWallFrame` on sticks.**
The same behavior, patched onto `game:stick` alongside the plank patch.
Sticks are also wattle's `Consumes`, so on a bare frame a stick click matches both a framing and an infill.
Planks never meet this, since planks are a finish and not an infill, and two branches of `SidingWallBlock.OnBlockInteractStart` match the held item against `Framings` before the infill match: the deck add (with the deck lit, on a side face) and decision 0026's corner upgrade (in corner mode).
On a bare frame those two branches skip any held item that also matches an infill, so sticks layer wattle; a stick deck or corner upgrade then needs a filled wall or a plank in hand.

**Stick floors.**
`PlaceWallFrame` already lays a floor when Floor is picked, and sets the floor's `Framing` to whatever key the held item matched, so sticks lay stick joists with no floor code.
The same clash shows up once: `SidingFloorBlock.OnBlockInteractStart` fills a bare floor with any held infill, so a stick click on a bare floor's top fills it with wattle where a plank click would carry the run on.
Sneaking skips the block's interaction and reaches `PlaceWallFrame`, which is the rule planks already follow on a filled floor, so a stick run is carried on with a sneak-click and the guide says so.

**A stone as a second build signal.**
`HasSawInOffhand` becomes a check for either a saw or any `game:stone-*` in the off hand, and every caller (framing, layering, the floor, and the mode picker through `SidingModePicker.IsOurs`) follows.
Every player carries stones from the first minute, and few keep one in the off hand by accident, so it still reads as "building".
It is One-Roof's stone hammer without the extra item.
Vanilla's off-hand slot only takes items with the `Offhand` (256) storage flag, and `stone.json` sets `storageFlags: 5`, so stones need a patch to 261, keeping the existing flags, as `patches/saw-offhand.json` does for the saw.
That patch lets stones sit in the off hand for every mod, not only this one.
The stone is never consumed; its only job is gating the right-click, exactly as the saw's is.
Stones in the main hand still layer the `stone-{rock}` infill, since the two hands are read separately.

**The guide says so.**
The handbook text (`gamemechanicinfo-siding-text` in `lang/en.json`) tells players to put a saw in the off hand and that planks build the framing.
It names the stone and sticks too, since decision 0006 already found the off-hand gesture hard to discover, and the stone-age player this is for would otherwise never learn it.

**No per-framing restrictions.**
A stick frame takes every infill and finish a plank frame does.
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
- Frame cost in sticks, against planks' 2 and wattle's 4.
- Texture: vanilla has no pole item, so pick a stick or bark texture that reads as lashed poles at wall scale, and as joists on a floor.
- Whether any vanilla behavior reacts to a stone held in the off hand once the slot takes it.
- `quieter-tooltip` keys the full tooltip on the build signal, so whichever lands second follows the other's check.
