# CLAUDE.md

This file provides guidance to Claude Code when working with code in this repository.

## Project Overview

Siding is a Vintage Story mod that replaces one-block-thick solid walls with thin, layered walls: a framing material, an infill material, and a finish for each face, each picked independently. Same idea as the Roofing mod (one shared shape, material swapped via variant/config) applied to walls instead of roofs.

Framing plus infill is a complete wall: it seals rooms through vanilla's per-face retention, and the infill material decides whether it's a cooling (cellar) wall. Face finishes are appearance only. Any heat simulation beyond vanilla's room retention may become a real mechanic later if the mod takes off, but it is explicitly out of scope for now.

**Tech Stack**: C# with Vintage Story Modding API, targeting net10.0
**Build System**: Cake Build (via `./build.sh`)
**modid**: `vssiding`

## Build & Development Commands

```bash
just            # build, then install into the game's Mods folder (same as `just deploy`)
just build      # build only
just test
```
Needs [`just`](https://github.com/casey/just); recipes run on macOS and Windows.
`just build` runs the Cake build (`CakeBuild/Program.cs`): validates JSON in `VSSiding/assets/`, `dotnet publish`es Release, packages into `Releases/vssiding/`, zips it.
`./build.sh` / `build.ps1` run the same build without `just`.

Manual build: `dotnet build VSSiding/VSSiding.csproj -c Release`

Requires `VINTAGE_STORY` env var pointing at the game install, or a `Directory.Build.props.user` (gitignored, copy from `Directory.Build.props.user.example`).

`just deploy` deletes any installed `vssiding_*.zip` before copying the new one, so a version bump doesn't leave two versions of the modid loading.
The game loads the zip as-is; don't unpack it.

## Architecture

`SidingModSystem` registers two classes: `SidingWallBlock` (shape, collision, room-sealing retention, drops — decision 0002; breaking peels one layer at a time — decision 0013) and `SidingWallEntity` (per-wall `Framing`/`Infill`/`Front`/`Back`/`SecondFront` state — decision 0003). `wall.json` carries the `Framings`/`Infills`/`Finishes` material dictionaries the entity's keys look up; a saw in the off hand builds those keys up layer by layer in play (decisions 0005/0006), and in `corner` mode turns a bare frame into a `cornerout` in place (decision 0026). `SidingModSystem` also Harmony-patches `RoomRegistry`'s skylight sample so a sealed wall cell doesn't read as sky (decision 0015), and registers `/sidingroom` to print a room's counts. A sealed wall also emits side AO so its faces stop glowing with the sunlight its cell stores (decision 0016), and two more patches stop a sealed room's floor edges picking up the daylight one cell outside the wall (decision 0018). See the decisions for the *why*.

**An infill marked `Transparent` seals without going opaque.** Glazing retains and dams liquid like any fill, but absorbs no light, which takes its cell out of the skylight patch, side AO and both decision 0018 patches — they all read `GetLightAbsorption`. Adjacent glazed cells merge in every direction and take no finish; a `window` block layout was built and deleted, because merging belongs to glass rather than to a shape. `SidingWallBlock` also overrides `GetLiquidBarrierHeightOnSide`, which vanilla derives from `SideSolid` — off on every face since decision 0002, so walls leaked until decision 0019.

**Material selection is a JSON attribute dictionary read by one block class, not a block variant.** Framing, infill, and face finishes each key into their own dictionary in the block's `attributes` (e.g. `attributes.Framings.oak`, `attributes.Infills.wattle`), read at mesh-build time - not `variantgroups`, which would multiply out into a real block per combination across three crossed axes. Reverse-engineered from the released Roofing mod's shipped JSON (no source available, no DLL decompiled - the assets alone show the shape). See decision 0001.

**`*Families` dictionaries are templates, not materials.** `FramingFamilies`/`InfillFamilies`/`FinishFamilies` entries (e.g. `"{wood}"`) expand into one `Framings`/`Infills`/`Finishes` entry per matching item or block in `SidingModSystem.AssetsFinalize` (`MaterialFamilies.Expand`); explicit entries win. See decision 0010.

**Overriding a vanilla property for a rendering result means finding every consumer of it first.** Vanilla overloads its properties across unrelated systems: `sidesolid: false` was set so thin walls don't cull neighbours, and vanilla reads that same flag for retention, liquid barriers, attachment, snow, mob spawns and more — three shipped bugs before anyone read the list. Only the method bodies name a consumer, so this means the decompiled `VintagestoryAPI.dll`, grepping `Block` *and* `BlockBehavior` for the field. `VintagestoryAPI.xml` carries prose and signatures only — `SideSolid` appears in it three times, in none of its consumers' entries — so it can confirm a member you already suspect but can never enumerate them. Redo it on a game update; decision 0020's table is 1.21's.

## Design docs

`docs/proposals/` — draft ideas, each sized to one session, mutable.
`docs/decisions/` — numbered, Accepted, immutable. Supersede, don't rewrite.
Read decision 0001 before touching block/material architecture.
Full convention in `docs/README.md`; PR/commit workflow in `CONTRIBUTING.md`.

## Workflow

Never commit to `main` — branch, PR, squash-merge. See `CONTRIBUTING.md`.
Commit subjects: `type: subject` (`feat`/`fix`/`docs`/`chore`/`refactor`/`test`), no other prefix.
