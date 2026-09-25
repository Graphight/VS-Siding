# 0039 — Documenting the just workflow

- Status: Accepted
- Created: 2026-09-25
- Reflects: branch `just-workflow-docs`; `justfile`, `README.md`, `CONTRIBUTING.md`, `CLAUDE.md`

## Summary
`README.md`, `CONTRIBUTING.md` and `CLAUDE.md` all lead with `just`, and keep `./build.sh`/`build.ps1` as the fallback for anyone without it.

## Context
The `justfile` has four recipes: `build` (runs `CakeBuild`, the same as `./build.sh`), `test` (`dotnet test VSSiding.sln`), `shapes` (regenerates the committed `shapes/block/wall/*.json` via `SIDING_REGEN=1 dotnet test`), and `deploy` (build, then replace the installed zip in the game's `Mods` folder; the bare `just` runs `deploy`).

Before this, only `CLAUDE.md` mentioned `just`.
`README.md`'s "Building from Source" and both build lines in `CONTRIBUTING.md` showed `./build.sh` alone, and `CONTRIBUTING.md` never mentioned the tests.
No doc named `just shapes`; only decision 0021 and the `justfile` comment said how the committed shapes are regenerated.
A contributor reading the README or CONTRIBUTING never learned that `just test` runs the suite or that `just deploy` puts a build in the game.

## Design
`just` is the primary path in all three docs, and `./build.sh`/`build.ps1` stay as the fallback, as `CLAUDE.md` already had it.

`README.md` shows `just build` and `just deploy`, says plain `just` runs `deploy`, links `just`, and names the script fallback.
The `VINTAGE_STORY`/`Directory.Build.props.user` paragraph is unchanged; it applies either way.

`CONTRIBUTING.md`'s branch example runs `just build`, not `just deploy`: a contributor mid-branch should not have their live `Mods` folder replaced.
"Before you open a PR" runs `just build` and `just test`, gives the no-`just` equivalents, names `just deploy` as the way to try a branch in game, and says to run `just shapes` after changing `WallShapeGen`.

`CLAUDE.md` gains `just shapes` in its command block.

Each doc keeps its own voice (the README terse, CONTRIBUTING a checklist) rather than copying `CLAUDE.md`'s prose.

## Alternatives considered
- **Leave `README.md`/`CONTRIBUTING.md` alone, since `./build.sh` still works.** True, but it hides `just test` and `just deploy` from anyone not reading `CLAUDE.md`, and a new contributor's first command should not be the long way round.
- **Drop `build.sh`/`build.ps1` and require `just`.** `just` is an extra install; removing the fallback would be a behaviour change, not a docs change.
- **One build-instructions file linked from the other two.** For a one-person mod, three short audience-specific mentions are less friction than a link to follow.

## Consequences & open questions
- A new `justfile` recipe now means up to three doc edits, not one.
- The proposal said `just shapes` rewrites `wall.json` and `cornerout.json`; it rewrites the shape files those block types use, and the docs say that.
