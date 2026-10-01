# Primitive framing

- Status: Draft
- Created: 2026-09-30
- Reflects: mod page comments https://mods.vintagestory.at/vssiding#cmt-242349 and #cmt-242478; `SidingWallBlock.HasSawInOffhand`, `PlaceWallFrame`, `config/materials.json`; decisions 0005, 0006, 0010; not yet played

## Summary
Two players asked for "primitive" walls, meaning walls a stone-age player can build.
The proposal adds a stick framing, and lets flax twine in the off hand stand in for the saw as the build signal, without consuming it.
Everything after the frame stays the one shared wall system.

## Context
**The saw is the only metal gate.**
`HasSawInOffhand` matches `game:saw-*`, and the cheapest saw is copper.
`PlaceWallFrame` sits only on planks, and vanilla planks come from sawing logs.
So a stone-age player cannot raise a single frame, and nothing downstream of the frame is reachable.

**Most layers are already stone age.**
Infills `wattle`, `straw`, `clay` and `stone-{rock}`, and finishes `daub`, `daub-{color}`, `drystone-{rock}` and `shakes-{wood}` (both its shakes and logs styles, which consume a placed log a stone axe fells), need no metal to make.
Planks, brick and metal plate each need a saw, a kiln or smelting, so their own recipes keep them late.

**What was asked.**
One player suggested a frame that needs no saw, built from sticks and cordage held in opposite hands, taking wattle and daub, logs or straw.
Of those, only the frame is missing: the logs style of `shakes-{wood}` is already a stone-age finish.
Another said primitive walls would put the mod in their baseline pack, which means it has to work from the first day.

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

**Flax twine as a second build signal.**
`HasSawInOffhand` becomes a check for either a saw or `game:flaxtwine` in the off hand, and every caller (framing, layering, the floor, and the mode picker through `SidingModePicker.IsOurs`) follows.
Vanilla's off-hand slot only takes items with the `Offhand` storage flag, and `flaxtwine.json` sets none, so twine needs a `storageFlags: 257` patch, as `patches/saw-offhand.json` does for the saw.
That patch lets twine sit in the off hand for every mod, not only this one.
Twine is never consumed, so one piece is all a player needs, which keeps it cheap however scarce flax is.
Its only job is gating the right-click, exactly as the saw's is.

**The guide says so.**
The handbook text (`gamemechanicinfo-siding-text` in `lang/en.json`) tells players to put a saw in the off hand and that planks build the framing.
It names twine and sticks too, since decision 0006 already found the off-hand gesture hard to discover, and the stone-age player this is for would otherwise never learn it.

**No per-framing restrictions.**
A stick frame takes every infill and finish a plank frame does.
Late materials gate themselves through their own recipes, and by the copper age planks come faster than sticks, so the stick frame is chosen for its look rather than forced by progression.
The value it adds is that look and the earlier start; it does nothing a plank frame cannot.

## Alternatives considered
- **Consume twine per frame.** Collecting twine in quantity is slow, and plank framing consumes no fasteners, so this would make the early path cost more than the late one.
- **A separate primitive wall block with only primitive layers.** Two wall systems to keep in step for a difference the recipes already enforce.
- **A stone axe as the off-hand signal.** Earlier than twine and a tool like the saw, but players hold axes for other work, so it would claim right-clicks they meant for something else more often.
- **No off-hand signal for sticks.** A right-click with sticks would then take every stick right-click in the game, which decision 0006's off-hand signal exists to avoid.

## Consequences & open questions
- Check that flax twine is craftable without metal, and whether vanilla has any other cordage worth accepting.
- Frame cost in sticks, against planks' 2 and wattle's 4.
- Texture: vanilla has no pole item, so pick a stick or bark texture that reads as lashed poles at wall scale.
- A stick frame under brick or iron plate is physically odd; add a per-framing list of allowed finishes only if players raise it.
