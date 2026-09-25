# Documenting the just workflow

- Status: Draft
- Created: 2026-09-25
- Reflects: `justfile`, `README.md`, `CONTRIBUTING.md`, `CLAUDE.md`, `build.sh`/`build.ps1` on branch `post-release-proposals`

## Summary
`CLAUDE.md` already documents `just` as the primary workflow.
`README.md` and `CONTRIBUTING.md` still only show `./build.sh` and never mention `just` or `dotnet test`.
The proposal lists what each doc says today and what it should say, so `just` becomes the one path all three docs point to.

## Context
The `justfile` has four recipes: `build` (runs `CakeBuild`, the same as `./build.sh`), `test` (`dotnet test VSSiding.sln`), `shapes` (regenerates committed shape JSON via `SIDING_REGEN=1 dotnet test`), and `deploy` (build, then replace the installed zip in the game's `Mods` folder; the bare `just` runs `deploy`).
`build.sh` is one line, `dotnet run --project ./CakeBuild/CakeBuild.csproj -- "$@"`; `build.ps1` is its Windows twin, `dotnet run --project CakeBuild/CakeBuild.csproj -- $args`.

Today:
- `README.md`'s "Building from Source" section shows only `./build.sh`.
- `CONTRIBUTING.md`'s workflow example runs `./build.sh` before `git push`, and its "Before you open a PR" section repeats `./build.sh` alone.
- `CLAUDE.md`'s "Build & Development Commands" already leads with `just`, `just build`, `just test`, notes `just` needs installing, and only mentions `./build.sh`/`build.ps1` as the fallback that runs the same build without `just`.
- No doc mentions `just shapes`; only decision 0021 and the `justfile` comment say how the committed shapes are regenerated.

So the mod has two current, correct build paths, but only the Claude-facing doc explains both.
A contributor reading `README.md` or `CONTRIBUTING.md` never learns `just` exists, `just test` runs the test suite, or that `just deploy` is the fastest way to get a build into the game to try it.

## Design
Make `just` the primary path in all three docs, with `./build.sh`/`build.ps1` kept as the no-`just` fallback, matching what `CLAUDE.md` already does.

**`README.md`.** Replace "Building from Source" with a `just` block (`just` alone, or `just build`), a one-line "needs [`just`](https://github.com/casey/just)" note, and a line that `./build.sh` or `build.ps1` works too if `just` isn't installed.
Keep the `VINTAGE_STORY`/`Directory.Build.props.user` paragraph as-is; it applies either way.

**`CONTRIBUTING.md`.** Swap the `./build.sh` line in the branch/PR example for `just build` (a contributor testing a branch doesn't want to deploy into their live `Mods` folder mid-work).
Swap "Before you open a PR"'s `./build.sh` for `just build`, and add `just test` alongside it, since the test suite isn't mentioned anywhere in this file today.

**`CLAUDE.md`.** Add `just shapes` to the command block, with the line that it rewrites `wall.json` and `cornerout.json` from `WallShapeGen` (decision 0021).
Otherwise it's already the model for the other two, and `CONTRIBUTING.md` gets the same `just shapes` line.

Keep the wording each doc already uses for its audience (`README.md` terse and player/builder-facing, `CONTRIBUTING.md` a checklist) rather than copying `CLAUDE.md`'s prose wholesale.

## Alternatives considered
- **Leave `README.md`/`CONTRIBUTING.md` as they are, since `./build.sh` still works.** True, but it hides `just test` and `just deploy` from anyone who isn't reading `CLAUDE.md`, and a new contributor's first command shouldn't be the long way round.
- **Drop `build.sh`/`build.ps1` entirely and require `just`.** Rejected: `just` is an extra install, and `CLAUDE.md` already keeps the raw-`dotnet` fallback for exactly that reason; removing it would be a behaviour change, not a docs change.
- **Centralize build instructions in one file and link to it from the other two.** Rejected for a one-person mod: three short, audience-specific mentions are less friction than a cross-file link a reader has to follow.

## Consequences & open questions
- Three small doc edits, no code or behaviour change.

## Stages
1. Update `README.md`'s "Building from Source" section.
2. Update `CONTRIBUTING.md`'s branch/PR example and "Before you open a PR" section, and add `just shapes`.
3. Add `just shapes` to `CLAUDE.md`.
4. Graduate as a decision, as decision 0032 did for the last docs pass.
