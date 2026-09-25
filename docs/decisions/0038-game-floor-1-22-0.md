# 0038 — Game floor 1.22.0

- Status: Accepted
- Created: 2026-09-25
- Reflects: branch `game-floor-1.22.0`; played on 1.22.2 and 1.22.7, nothing older

## Summary
The game dependency drops from `1.22.7` to `1.22.0`, so every 1.22 build can load the mod.
Supersedes decision 0037's game floor; the rest of 0037 stands.

## Context
A player on `1.22.0` asked whether the mod works.
0037 set the floor at `1.22.7` because that was the only build it had been played on, and named "if that turns out to matter" as the trigger to lower it.
Most of the mod was in fact built and played on `1.22.2`; only the last few PRs moved to `1.22.7`.

## Design
**Floor `1.22.0`.**
A `modinfo.json` game dependency is a minimum with no ceiling, so there is no way to write "1.22.x"; `1.22.0` is the closest.
Nothing older than `1.22.2` has been played, and the IL the transpilers match on `1.22.0` and `1.22.1` has not been read.

What a mismatch costs is the same as on a later build (0037): every transpiler throws when its match count is off, the `try/catch` round each `harmony.Patch` call skips that one patch with a log line, and the mod runs without that fix.
The four `FieldRefAccess` lookups are still the exception that would fail the mod outright.

## Alternatives considered
- **Keep `1.22.7`.** Turns away players on a stable line the mod was mostly built on, over IL differences no one has seen.
- **Floor at `1.22.2`.** The oldest build actually played, but it still refuses the player who asked.
- **Installing `1.22.0` to check first.** Still an afternoon per build (0037); a player report does the same job.

## Consequences & open questions
- A bug report from `1.22.0` or `1.22.1` should come with its client log: a skipped patch names itself there.
