# Quieter tooltip

- Status: Draft
- Created: 2026-10-04
- Reflects: mod page comment https://mods.vintagestory.at/vssiding#cmt-243545 and the second screenshot on #cmt-243661; `SidingWallEntity.GetBlockInfo`, `SidingWallBlock.Describe`, `SidingFloorEntity.GetBlockInfo`, `SidingFloorBlock.Describe`, `GuestTooltipPatches`, `lang/en.json`'s `blockdesc-wall-*` and `blockdesc-floor-*`; decompiled 1.22 `Block.GetPlacedBlockInfo`; decisions 0006, 0030, 0035; not yet played

## Summary
Looking at a wall prints up to a dozen lines at the top of the screen, and a player asked to see them only while building.
The proposal shows one line, whether the wall seals, unless the saw is in the off hand, and shows the full list when it is.

## Context
The complaint: "it's nice to have more info about where is insulated but I'd rather see the info when I have saw on my off hand. It's just filling top of the screen too much."

`SidingWallBlock.Describe` (decision 0030) prints a blank line, the framing, the infill, one line per group of faces and the seal line.
A deck adds four lines and a step adds one.
Vanilla's `Block.GetPlacedBlockInfo` then appends the `blockdesc-wall-*` text, a sentence describing the wall and pointing at the handbook, which wraps to two lines.
A finished wall with a deck comes to about twelve lines, on every wall a player's crosshair crosses.
Another player's screenshot, taken for a different request, shows the text across the top of the view.

Decision 0030 wrote the list for the builder: once a face is boarded the infill is hidden, and the infill decides a cellar.
A player walking through a finished house is not building, and the mod already has a signal for "building": the saw in the off hand (decision 0006).
`GetBlockInfo` is handed the player, so the entity can read that signal.

## Design
**One line without the build signal.**
`Describe` gains a flag for the short form, which prints only the seal line: "Seals the room", "Seals the room and keeps it cool" or "Does not seal the room".
That is the line the complaint itself calls worth having.

**The full list with it.**
`SidingWallEntity.GetBlockInfo` passes `SidingWallBlock.HasSawInOffhand(forPlayer)`, and the full list is unchanged.
`SidingFloorEntity` and `SidingFloorBlock.Describe` follow, and so does a hosted cell's tooltip, since `GuestTooltipPatches` calls the guest entity's `GetBlockInfo` with the same player.

**The handbook pointer follows the list.**
The `blockdesc-` sentence shows only in the full form.
It still describes the block in the creative inventory and the handbook, which read the same lang key.

## Alternatives considered
- **A config option.** A setting to find, for something the off hand already says.
- **Nothing without the saw.** The seal line is the one fact that changes how a player uses a room, and it is one line.
- **Shorten every line instead.** The list is already one line per layer; the length comes from showing it at all.
- **Sneak to expand.** Sneaking is already the gesture for placing against a wall, so the tooltip would flick open during ordinary building.

## Consequences & open questions
- How the pointer is held back: an override of `GetPlacedBlockInfo` on the two blocks that leaves the description off, or dropping the lang key and appending the sentence in `Describe`. The second loses the creative inventory text.
- Whether the HUD re-reads block info often enough that moving the saw into the off hand updates the tooltip without looking away; unverified.
- `primitive-framing` widens the build signal to a stone, so this should call the same check and follow it.
- A player who never holds a saw near a wall no longer sees the handbook link there, and that tooltip is the only in-world link to the guide.
