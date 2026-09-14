# CLAUDE.md

This file provides guidance to Claude Code when working with code in this repository.

## Project Overview

Siding is a Vintage Story mod that replaces one-block-thick solid walls with thin, layered walls: a framing material, an insulation material, and an exterior finish, each picked independently. Same idea as the Roofing mod (one shared shape, material swapped via variant/config) applied to walls instead of roofs.

Insulation is flavor/appearance only (texture, maybe cost) - it does not touch Vintage Story's real temperature simulation. That may become a real mechanic later if the mod takes off, but it is explicitly out of scope for now.

**Tech Stack**: C# with Vintage Story Modding API, targeting net10.0
**Build System**: Cake Build (via `./build.sh`)
**modid**: `vssiding`

## Build & Development Commands

```bash
./build.sh
```
Runs the Cake build (`CakeBuild/Program.cs`): validates JSON in `VSSiding/assets/`, `dotnet publish`s Release, packages into `Releases/vssiding/`, zips it.

Manual build: `dotnet build VSSiding/VSSiding.csproj -c Release`

Requires `VINTAGE_STORY` env var pointing at the game install, or a `Directory.Build.props.user` (gitignored, copy from `Directory.Build.props.user.example`).

Deploy to Vintage Story (macOS):
```bash
cp -r Releases/vssiding ~/Library/Application\ Support/VintagestoryData/Mods/
```

## Architecture

Nothing built yet - `SidingModSystem.cs` is an empty `ModSystem` entry point. This section gets filled in as real systems (wall block class, material attribute dictionaries, shape handling) land.

**Material selection is a JSON attribute dictionary read by one block class, not a block variant.** Framing, insulation, and exterior each key into their own dictionary in the block's `attributes` (e.g. `attributes.Framings.oak`, `attributes.Insulations.wool`), read at mesh-build time - not `variantgroups`, which would multiply out into a real block per combination across three crossed axes. Reverse-engineered from the released Roofing mod's shipped JSON (no source available, no DLL decompiled - the assets alone show the shape). See decision 0001.

## Design docs

`docs/proposals/` — draft ideas, each sized to one session, mutable.
`docs/decisions/` — numbered, Accepted, immutable. Supersede, don't rewrite.
Read decision 0001 before touching block/material architecture.
Full convention in `docs/README.md`; PR/commit workflow in `CONTRIBUTING.md`.

## Workflow

Never commit to `main` — branch, PR, squash-merge. See `CONTRIBUTING.md`.
Commit subjects: `type: subject` (`feat`/`fix`/`docs`/`chore`/`refactor`/`test`), no other prefix.
