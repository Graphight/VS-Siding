# 0091 — E2E classes roll their world back

- Status: Accepted
- Created: 2026-11-08
- Reflects: issue #156; PR #159; branch `156-e2e-rollback`; `VSSiding.E2E.Tests` (every scenario class, `GuestRestartScenarios`, `CollapseStoryScenarios`); Atlas 0.15.1; decision 0056

## Summary
Every end-to-end scenario booted its own world (`FreshWorld`), so a full `just e2e` took 13 to 15 minutes, nearly all of it boots.
Scenarios now run with `RollbackWorld = true, StrictIsolation = true`: Atlas snapshots the class's world once and restores it between scenarios.
The full run (121 tests) takes about 5 minutes (5:09 at merge-ready, 5:22 on the first switched run).

## Context
A scenario that builds a wall and checks it needs a clean world, and rebooting the host gave one at the price of a boot per scenario.
Atlas can restore a snapshot instead, which reaches blocks, block entities, chunk moddata, savegame data and joined players, but not in-memory state held outside them: `ModSystem` fields, statics and caches, vanilla's included.

## Design
**Rollback by default.** All scenario classes use `RollbackWorld = true, StrictIsolation = true`.
`StrictIsolation` fails a scenario whose rollback degrades to a full recycle (a failed capture or restore) instead of letting it pass silently on the slower path.
It does not detect in-memory state left over from an earlier scenario; Atlas's docs say strictness changes visibility, not safety.

**Siding's own state survives a restore.** The mod's mutable statics are geometry tables, or caches keyed on objects a restore replaces (the `GuestWalls` decode tables, the `GapShiftCollisionPatches` box caches).
The mode picker's choices live in `Entity.WatchedAttributes`, which a rollback restores, and players who joined after the snapshot are removed.

**Restart pairs stay fresh.** `GuestRestartScenarios` holds the two build-then-check pairs (A_/B_ and C_/D_) under `MethodNameOrderer`.
A `RestartWorld` boots a new host, and that host's next rollback scenario would snapshot the world the build scenario left, so the pairs keep `FreshWorld`/`RestartWorld` in a class of their own.

**CollapseStoryScenarios stays fresh.**
It runs only under `just probe collapsestory`, outside `just e2e`.
Under rollback 4 of its 9 scenarios failed at `WallBuilder.Raise`'s `Assert.True` (the infill click was not answered true), while scenarios that pass share that path, so the failure looks order or state dependent.
The cause was not found and is open; the class keeps `FreshWorld`.

**Vanilla's cached rooms.** `RoomRegistry` is in-memory state a restore does not reach, and a cached room keeps its first answer until a block in its chunk changes.
Every room scenario places blocks in the room's chunk before it measures, so no cached room reaches a measurement; the leak and seal diagonal scenarios, which measure the same cell with opposite answers on one host, both pass.
Whether a restore itself clears the registry is not shown.
No `atlas:rollback:restored` listener was added; a scenario that measures a room before building in its chunk would need one, or `FreshWorld`.

**Probe classes.** `CompatProbeScenarios`, `CelticBedScenarios` and `ImmersiveWoodworkingScenarios` also roll back.
`just probe collapsestory`, `just probe kevinsfurniture` and `just probe immersivewoodworking` each passed with their mod loaded (`CollapseStoryScenarios` on `FreshWorld`, the rest under rollback), and the two committed `docs/compat/` files changed only in their probe date.

## Alternatives considered
- **Keep every class on `FreshWorld`.** Correct by construction, and the 13 to 15 minutes this decision removes.
- **Name the restart pairs to sort last in their classes.** No file moves, but each class's isolation would rest on method names, and a new scenario named after the pair would run on the restarted world.
- **Clear `RoomRegistry` on every restore up front.** No scenario needs it today, so it stays unwritten until one does.

## Consequences & open questions
A new scenario uses `RollbackWorld` and `StrictIsolation` (`CONTRIBUTING.md`); a class that keeps `FreshWorld` carries a comment naming why.
A fresh scenario never goes inside a rollback class, since its world would leak into the next snapshot.
Time before: 13 to 15 minutes (known figure, not re-measured); after: 5:09 for 121 tests.
Open: why `CollapseStoryScenarios` fails under rollback.
