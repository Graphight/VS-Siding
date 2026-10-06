# Real Smoke flues

- Status: Draft
- Created: 2026-10-06
- Reflects: mod page comment https://mods.vintagestory.at/vssiding#cmt-245074; the mod page and shipped assets of Real Smoke 2.0.0-pre.7 (`patches/tag-porous.json`, `patches/remove-blockbehaviorchimney.json`, the handbook text in `lang/en.json`; no DLL decompiled and its source not read, as decision 0001); `blocktypes/wall.json`, `SidingModSystem.IsHostable`; vanilla `firepit.json`, `forge.json`, `clay/oven.json`, `clay/chimneycourse.json`; decisions 0001, 0002, 0003, 0019, 0020, 0035, 0043; not played with that mod

## Summary
A player vents a kitchen through trellises so smoke gets out, which costs the room its heat, and asks for an oven or hearth built into a wall or a flue made from a trapdoor.
The smoke is almost certainly the Real Smoke mod's, which the player did not name.
Nothing is known yet about how that mod's smoke meets a thin wall, so the proposal is a playtest first and a vent only if the playtest asks for one.

## Context
The request: "I use trellises in my kitchen area so smoke ventilates (non vs siding house) but this causes heating issues, it may be value added to look into how ovens or hearth spaces can be merged into your walls, or cannibalize the existing trap door behaviors to create a flue, so cooking can be done with out being exposed to the elements".

**What Real Smoke does, from its page and assets.**
Fire sources emit smoke that rises, spreads and needs a way out; dense smoke puts fires out and suffocates players.
Version 2.0.0-pre.7 covers game 1.22.0 to 1.22.7, runs on both sides, and its page counts about 240,000 downloads.
Smoke passes a block tagged `porous` or `semiporous`.
`tag-porous.json` adds `porous` to fences and their gates, ladders, the refractory grating, branchy leaves, barred and window trapdoors, and barred and crude doors, and `semiporous` to thatch roofing.
It removes vanilla's `Chimney` behaviour from `claybrickchimney` and gives chimneys its own.
The handbook text: "A valid chimney consists of non-solid blocks, up to 16 in height (configurable), capped with a chimney block", and "smoke will be restricted by anything smaller than a 4x4 hole".
Trellises are not in the tag list, so whatever lets smoke through the player's trellis is a rule the assets do not show.

**Why a Siding wall is an open question.**
The phrase "non-solid blocks" has two readings, and a Siding wall gives a different answer to each.
Every face of a wall declares `sidesolid: false`, and every face of a thin floor but its top, so that a thin panel does not cull its neighbours (decision 0002).
A rule that reads `SideSolid` would let smoke through every Siding wall, filled or not, and a Siding house would never hold smoke.
A rule that reads collision boxes would meet a filled wall's full panel, 4/16 thick with 12/16 of open cell beside it, and the "4x4 hole" test would decide what a bare frame or a wattle panel passes.
This is the mistake decision 0020 records three times over: a property set for rendering, read by a consumer nobody listed.

**A tag cannot follow the infill.**
A tag belongs to a block code, and a wall's code carries its `layout` and `side` and nothing else.
Framing, infill and finishes are block entity state (decision 0001).
So `porous` on the wall block would mark a stone-filled wall and a bare frame alike.

**What the player lacks.**
An opening that lets smoke out and keeps the room.
A trellis does the first and not the second.
A Siding wall answers vanilla's room test itself, per face and from its infill (decisions 0002, 0003), so it is one of the few blocks that could do both.

**Fires in a wall's cell.**
A firepit, a forge and a clay oven each declare `sidesolid: false` on every face, so none fails the solidity test in `IsHostable` (decision 0035).
None has been hosted in play, with or without Real Smoke.

## Design
**Play it first.**
One sealed Siding room with a firepit, under Real Smoke 2.0.0-pre.7, `/sidingroom` run before lighting it.
What to write down:
- Whether smoke stays in the room, or crosses a filled wall, a glazed cell, a bare frame, a thin floor, a deck.
- Whether a `claybrickchimney` over a gap in a thin floor draws smoke, and whether the room still seals.
- Whether a window trapdoor or a barred one, both already `porous`, keeps the room sealed when set in the roof. If one does, it is the flue the player asked for and Siding adds nothing.
- Whether a firepit, forge or oven hosts in a wall's cell, where its smoke starts from, and whether a wood layer beside it catches (decision 0043).

**If smoke crosses filled walls.**
That is a bug in the pairing and comes before any vent.
Its fix depends on which property Real Smoke reads, which the playtest shows and the assets do not.

**A vent, if nothing above already serves.**
An infill that seals and is drawn open: louvres between the timbers.
Retention comes from the infill as it does for any other (decision 0003), and `Transparent` already separates "seals" from "blocks light" (decision 0019), so a louvre that passes light needs no new field.
What it does need is for Real Smoke to see this cell as porous and the stone-filled one beside it as not.
Two routes, chosen after the playtest:
- A block code of its own for the vented wall, tagged `porous` by a patch that applies only when Real Smoke is installed. It costs a variant state, and decision 0001 exists to keep material out of variants; one state for "vented" is the smallest breach of it.
- A hook on Real Smoke's side that asks a block at a position. Its page lists an "Improved API" as planned and asks authors whose mods interact with it to get in touch.

## Alternatives considered
- **Tag the wall block `porous`.** One patch line, and every Siding wall then leaks smoke whatever fills it.
- **A flue block.** A chimney course is vanilla's, and Real Smoke already replaces its behaviour. A Siding flue would be a second chimney competing with the one that mod supports.
- **An oven or hearth as a wall layer.** The request's first idea. Hosting already puts a fire block in a wall's cell (decision 0035), so a built-in hearth is the playtest's fourth question and no new layer.
- **Simulate the heat a vent loses.** Heat beyond vanilla's room retention is out of scope for the mod.
- **Do nothing and name a vanilla workaround.** Right if a porous trapdoor keeps the room; the playtest says.

## Consequences & open questions
- Confirm with the player that the smoke is Real Smoke's, and which fire blocks their kitchen uses.
- Real Smoke 2.0.0 is in pre-release and its page says more changes are coming, so whatever the playtest finds may move before release.
- A vent that seals for heat and passes smoke is a fiction vanilla's own chimney does not need, since a chimney sits above the room. Whether that is acceptable is a call for after the playtest.
- A wall cell is 12/16 open on one side whatever fills its panel. If smoke pools in that open part, a hosted fire's smoke starts on the room side of the panel, which is where it should start; this is not checked.
- Whether the thin floor needs the same vent, as a hatch over a hearth.
