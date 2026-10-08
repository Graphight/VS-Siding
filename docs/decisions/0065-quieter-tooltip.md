# 0065 — Quieter tooltip

- Status: Accepted
- Created: 2026-10-04
- Reflects: mod page comment https://mods.vintagestory.at/vssiding#cmt-243545 and the second screenshot on #cmt-243661; `SidingWallEntity.GetBlockInfo`, `SidingWallBlock.Describe`, `SidingWallBlock.GetPlacedBlockInfo`, `SidingFloorEntity.GetBlockInfo`, `SidingFloorBlock.Describe`, `GuestTooltipPatches`, `lang/en.json`'s `blockdesc-wall-*` and `blockdesc-floor-*`; `SidingWallTooltipTests`, `SidingFloorTests`, `VSSiding.E2E.Tests/TooltipScenarios.cs`; decompiled 1.22 `Block.GetPlacedBlockInfo` and `HudElementBlockAndEntityInfo`; decisions 0006, 0030, 0035, 0058; unit tests and e2e scenarios pass; played 2026-10-08

## Summary
Looking at a wall printed up to a dozen lines at the top of the screen, and a player asked to see them only while building.
Without the build signal the tooltip is now one line, whether the wall seals, and with the saw or a stone in the off hand the full list and the handbook sentence show as before.

## Context
The complaint: "it's nice to have more info about where is insulated but I'd rather see the info when I have saw on my off hand. It's just filling top of the screen too much."

`SidingWallBlock.Describe` (decision 0030) prints a blank line, the framing, the infill, one line per group of faces and the seal line.
A deck adds four lines and a step adds one.
Vanilla's `Block.GetPlacedBlockInfo` then appends the `blockdesc-wall-*` text, a sentence describing the wall and pointing at the handbook, which wraps to two lines.
A finished wall with a deck comes to about twelve lines, on every wall a player's crosshair crosses.

Decision 0030 wrote the list for the builder: once a face is boarded the infill is hidden, and the infill decides a cellar.
A player walking through a finished house is not building, and the mod already has a signal for "building": the saw in the off hand (decision 0006), widened to a stone in decision 0058.
`GetBlockInfo` is handed the player, so the entity can read that signal.

## Design
**One line without the build signal.**
`Describe` on the wall and on the floor takes a flag for the full form.
When it is off, the result is the translated seal line alone: "Seals the room", "Seals the room and keeps it cool" or "Does not seal the room".
There is no leading blank line and no indent.

**The full list with it.**
`SidingWallEntity.GetBlockInfo` and `SidingFloorEntity.GetBlockInfo` pass `SidingWallBlock.HasBuildSignal(forPlayer)`, and the full list is unchanged.
A hosted cell follows with no code of its own, since `GuestTooltipPatches` calls the guest entity's `GetBlockInfo` with the same player.

**The handbook sentence is held back by an override.**
`SidingWallBlock` overrides `GetPlacedBlockInfo`: the override calls the base and, when there is no build signal, removes the `blockdesc-` text from the result.
The lang key stays, so the creative inventory and the handbook, which read the same key, are untouched.
Reimplementing the base body instead would have dropped the decor, mining-tier and behavior lines it appends after the sentence.

**The HUD recomposes on its own.**
Vanilla's `HudElementBlockAndEntityInfo.Every500ms` calls `ComposeBlockInfoHud` unconditionally and recomposes when the text differs, so moving the saw into the off hand updates the tooltip within half a second.
This was read from the decompiled 1.22 DLL and seen in play.

## Alternatives considered
- **A config option.** A setting to find, for something the off hand already says.
- **Nothing without the saw.** The seal line is the one fact that changes how a player uses a room, and it is one line.
- **Shorten every line instead.** The list is already one line per layer; the length comes from showing it at all.
- **Sneak to expand.** Sneaking is already the gesture for placing against a wall, so the tooltip would flick open during ordinary building.
- **Drop the lang key and append the sentence in `Describe`.** It would lose the creative inventory and handbook text, which read the same key.

## Consequences & open questions
A player who never holds a saw near a wall no longer sees the handbook link there, and that tooltip was the only in-world link to the guide.

The floor never showed a handbook sentence at all.
Its block code is plain `floor` with no variants, and vanilla's `TranslationService` stores a key ending in `*` as a prefix (`vssiding:blockdesc-floor-`), which `vssiding:blockdesc-floor` does not start with.
So the floor takes no `GetPlacedBlockInfo` override: one was written and removed, since deleting it changed no test and no tooltip.
Fixing the key was left out of this change, and a floor whose key is made to match needs the wall's override with it.

Covered by unit facts in `SidingWallTooltipTests` and `SidingFloorTests`, and by the e2e scenarios in `TooltipScenarios.cs`.
