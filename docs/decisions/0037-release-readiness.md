# 0037 — Release readiness

- Status: Accepted
- Created: 2026-09-24
- Reflects: branch `release-readiness`; one playtest house on game 1.22.7

## Summary
The mod ships as `1.0.0` with a mod icon, a game floor of `1.22.7`, and a save round-trip and whole-house playtest behind it.

## Context
Everything between "feature-complete" and "published on ModDB" that isn't a feature: there was no `modicon.png`, the version was `0.1.0`, the game dependency named `1.22.2` though nothing had been played on it since the install moved to `1.22.7`, and every decision had been demoed in isolation.
Nobody had built a whole house or reloaded a world with every layer kind in it.

## Design
**Playtest first.**
One house on the branch build: exterior walls, an interior partition, a glazed run, a cellar, a corner upgraded in place, and a chest and a trunk hosted against walls.
It also covered the in-play check decision 0028 asked for: stacked weatherboard across the block boundary, a `cornerout`'s two legs, and weatherboard beside shake.
No defects were seen.
That is a statement about one house, not a proof; the ones still hiding will come from players.

**Save round-trip.**
The same world was saved, quit and reloaded, and every layer came back, including the wall held in the guest store behind the chest.
`0.1.0` was never published, so no player holds a world older than `1.0.0`, and a reload on the release build is the round-trip that matters.

**Mod icon.**
`VSSiding/modicon.png` is a 256px square crop of the playtest house.
The proposal wanted a wall in section, but the house is what the playtest produced, and at 64px it still reads as a timber and brick building.
`CakeBuild/Program.cs` already packages the file when it exists.

**Game floor `1.22.7`.**
The proposal asked for the oldest version that still has every patched member.
That question stopped being answerable by reading: `SidingModSystem` patches sixteen vanilla methods directly, plus `EveryOverridePatches`' fan-out, and several are transpilers that match IL, which can change between patch releases while every member keeps its name.
The only version the mod has been played on is `1.22.7`, so that is the floor.
Decision 0020's consumer sweep, last done on `1.22.2` (0033), was not redone.
The test suite, which builds against the installed `1.22.7` DLLs, and the playtest stand in for it.

The floor has no ceiling, so what a later build does matters.
Every `harmony.Patch` call sits in a `try/catch`, so a patch that no longer applies is skipped with a log line and the mod runs without that fix.
The four private-field lookups in `SidingModSystem` are the exception: `AnimatableRenderer.pos` and `capi`, `ChunkTesselator.vars` and `CollectibleObject.api`, read through `static readonly` `FieldRefAccess` fields.
Harmony throws when the field is missing, so a later build renaming one fails the type initializer, and the mod with it, rather than one fix.

**Version `1.0.0`, last.**
Bumped in its own commit right before publishing; `just deploy` already deletes the old `vssiding_*.zip`.

**The README is the ModDB copy.**
The page description is pasted from `README.md`, so the two drift only if someone edits the page directly.

## Alternatives considered
- **Publishing at `0.1.0`.** Reads as abandoned-in-progress to anyone browsing.
- **Floor at `1.22.0`.** Wider reach, resting on the transpilers matching IL nobody has looked at.
- **Installing older 1.22 builds to find the true floor.** An afternoon per build for players who mostly update within a stable line anyway.
- **A code-level save migration.** No key has ever been renamed. Write one when a key changes.
- **A CI release workflow.** `just` and the Cake build already produce the zip; a one-person mod publishing by hand doesn't need a pipeline.

## Consequences & open questions
- Players on `1.22.0` to `1.22.6` can't load the mod. If that turns out to matter, testing one older build lowers the floor.
- A game update means redoing decision 0020's consumer sweep and rerunning the tests before claiming it works.
- Moving the four field lookups behind the same `try/catch` would make a renamed field cost one fix instead of the mod. Not done for `1.0.0`; revisit if a game update renames one.
- No proposals are open.
