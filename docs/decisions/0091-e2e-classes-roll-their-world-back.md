# 0091 — E2E classes roll their world back

- Status: Accepted
- Created: 2026-11-08
- Reflects: issue #156; branch `156-e2e-rollback`; `VSSiding.E2E.Tests` (every scenario class, `GuestRestartScenarios`, `CollapseStoryScenarios`); decision 0056

## Summary
Every end-to-end scenario booted its own world (`FreshWorld`), so a full `just e2e` took 13 to 15 minutes, nearly all of it boots.
Scenarios now run with `RollbackWorld = true, StrictIsolation = true`: Atlas snapshots the class's world once and restores it between scenarios.
The full run (121 tests) takes about 5 minutes (5:09 at merge-ready, 5:22 on the first switched run).

## Context
A scenario that builds a wall and checks it needs a clean world, and rebooting the host gave one at the price of a boot per scenario.
Atlas can restore a snapshot instead, which reaches the world and the block entities but not in-memory state held outside them.

## Design
**Rollback by default.** All scenario classes use `RollbackWorld = true, StrictIsolation = true`, so a scenario that leaks state into the next one is reported rather than passing by luck.

**Restart pairs stay fresh.** `GuestRestartScenarios` holds the two A_ build, B_ check pairs under `MethodNameOrderer`.
A `RestartWorld` boots a new host, so the next rollback scenario would snapshot the world A_ built; those pairs keep `FreshWorld`/`RestartWorld` in a class of their own.

**CollapseStoryScenarios stays fresh.**
It runs only under `just probe` (`SIDING_PROBE=collapsestory`), outside `just e2e`.
Under rollback 4 of its 15 scenarios failed at `WallBuilder.Raise`'s `Assert.True` (the infill click was not answered true), while scenarios that pass share that path, so the failure looks order or state dependent.
The cause was not found and is open; the class keeps `FreshWorld`.

**RoomRegistry ruled out.** Vanilla's cached rooms were the first suspect, but the leak and seal diagonal scenarios measure the same cell (`Spawn.Offset(0, 2, 0)`) with opposite answers on one host, and both pass under rollback, so a stale cached room between scenarios is not the cause.
No `atlas:rollback:restored` listener was added.

**Probe.** `CompatProbeScenarios` also rolls back; one `just probe` run on a mod already in `docs/compat/` left its output unchanged.

## Consequences
A new scenario uses `RollbackWorld` and `StrictIsolation` (`CONTRIBUTING.md`); a class that keeps `FreshWorld` carries a comment naming why.
A fresh scenario never goes inside a rollback class, since its world would leak into the next snapshot.
Time before: 13 to 15 minutes (known figure, not re-measured); after: 5:09 for 121 tests.
