# Release readiness

- Status: Draft
- Created: 2026-09-20
- Reflects: release polish planning, after decision 0028

## Summary
The non-code list between "feature-complete" and "published on ModDB": a mod icon, a real version number, an honest dependency floor, a save-compatibility check, and one deliberate playtest pass.

## Context
Everything else open is a feature or a document.
This is the plumbing that decides whether the first download works, and none of it is in the repo yet.

- **No `modicon.png`.** `CakeBuild/Program.cs:103` copies one *if it exists*, and it doesn't, so the mod shows as a blank tile in the mod manager and on ModDB.
- **Version is `0.1.0`.** That number says "scaffolding" to anyone reading the ModDB list.
- **`dependencies: { game: "1.22.2" }`** is the version it was built against, which is not the same claim as the oldest version it runs on. The Harmony patches target `RoomRegistry.FindRoomForPosition`, `ChunkTesselator.BuildExtendedChunkData` and `TCTCache.CalcBlockFaceLight`, and decision 0020's `sidesolid` consumer table is explicitly 1.21's. So the floor is whatever those three members last changed in — a question with a real answer.
- **Save compatibility is untested.** `SidingWallEntity` serialises five string keys by name (`ToTreeAttributes`), so a world built on 0.1.0 should load on 1.0.0, but "should" is not "did".
- **No playtest pass.** Every decision was demoed in isolation. Nobody has built a whole house.

## Design
Five items, none blocking the others:

1. **`modicon.png`** at `VSSiding/modicon.png`, which the Cake build already picks up. A wall in section — framing, infill, one finished face — reads at 64px better than a whole house does.
2. **Version to `1.0.0`** in `modinfo.json`, at the moment of publishing and not before. `just deploy` already deletes the old `vssiding_*.zip`, so a bump doesn't double-load.
3. **Dependency floor**: check when each patched member last changed, set `game` to the oldest version that still has all three, and say in the ModDB description which version it was tested on. The patches already fail soft (each `try/catch` logs and drops the fix), so a wrong guess degrades rather than crashes — which is the argument for stating the floor honestly rather than defensively.
4. **Save round-trip test**: build a world with every layer kind on the current build, note the walls, reload on the release build, confirm nothing reverts to a bare frame.
5. **One house, built for real.** Exterior walls, an interior partition, a glazed run, a cellar, a corner found late and upgraded in place. This is the pass that will surface the lighting slivers (`lighting-round-two`) and size the furniture gap (`furniture-against-thin-walls`) — so it is worth doing *before* committing to either.

   It also carries the in-play check decision 0028 asked for and nothing has yet discharged: a stacked weatherboard wall for the block boundary, a `cornerout`'s two legs, and a weatherboard wall standing beside a shake wall. 0028 shipped the positional UVs untested in play, exactly as 0023 shipped the taper untested, and that is how 0023's repeating-motif bug survived to be found by the next decision. If the sixteen slices read as too busy, 0028 names the knob: the art, not the rule.

## Alternatives considered
- **Publishing at `0.1.0` and iterating.** Honest, and it reads as abandoned-in-progress to anyone browsing.
- **A code-level save migration.** Nothing to migrate: the keys have never been renamed. Write the migration when a key changes, not in case one does.
- **Skipping the playtest and shipping.** The last three shipped bugs (decision 0020's list) were all found by looking, not by testing in isolation.
- **A CI release workflow.** `just` plus the Cake build already produce the zip; a workflow for a one-person mod publishing by hand is machinery for its own sake.

## Consequences & open questions
- Item 5 gates two other proposals, so it should probably be done first even though it's listed last.
- "Oldest version that still has all three members" may be answerable only by installing older builds. If that turns into an afternoon, set the floor at 1.22 and move on.
- ModDB page copy (description, screenshots, tags) is not tracked in the repo and will drift from the README. Accept it, or make the README the source and paste from it.
