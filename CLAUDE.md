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
just e2e        # build, then boot the mod headless under Atlas and run the end-to-end tests
just shapes     # rewrite the committed shapes/block/wall/*.json from WallShapeGen (0021)
```
Needs [`just`](https://github.com/casey/just); recipes run on macOS and Windows.
`just build` runs the Cake build (`CakeBuild/Program.cs`): validates JSON in `VSSiding/assets/`, `dotnet publish`es Release, packages into `Releases/vssiding/`, zips it.
`./build.sh` / `build.ps1` run the same build without `just`.

Manual build: `dotnet build VSSiding/VSSiding.csproj -c Release`

Requires `VINTAGE_STORY` env var pointing at the game install, or a `Directory.Build.props.user` (gitignored, copy from `Directory.Build.props.user.example`).

`just deploy` deletes any installed `vssiding_*.zip` before copying the new one, so a version bump doesn't leave two versions of the modid loading.
The game loads the zip as-is; don't unpack it.

## Architecture

Where things live. The decisions carry the *why*; `ls docs/decisions/` is the index.

- `SidingModSystem` registers the classes and `/sidingroom`, expands the material families, and owns the Harmony patches: the `RoomRegistry` skylight sample (0015), the two sealed-floor light patches (0018, occluded in 0034, sealed thin floor cells in 0055), the rain-fall distance that drives wind volume (0034), the `KillFire` prefix that burns one wall layer instead of the block (0043), the smooth step-up transpiler that lets players step walls (0047), the `BreakAllDecorFast` prefix that restores or drops a guest wall on a host change, and on a client records the guest itself the moment furniture takes the wall's cell (#65), the server and client `TriggerNeighbourBlocksUpdate` prefixes that restore it the moment its host goes, the client one predicting the server's (#58), the chunk-load restore of a guest stranded in an empty cell (#64), the `TesselateBlock` transpiler that renders a guest's panel beside its host, the off-panel offset a hosted block shifts by (0035), the `BlockBehaviorMultiblock.CanPlaceBlock` postfix that refuses a trunk footprint that isn't a shared-side wall run (0036), the `BlockBed.TryPlaceBlock` prefix that enforces the bed footprint and retargets a panel click, the `BlockEntityBed.Position` postfix that seats the sleeper where the bed is drawn, the `DidUnmount` transpiler that keeps a sleeper getting up on the bed's side of the wall (0049), the hanger shift that lifts a lantern, lamp or chandelier to a thin floor's underside (0054), and the `BlockShapeMaterialFromAttributes.DoPlaceBlock` and `BEBehaviorShapeMaterialFromAttributes.FromTreeAttributes` postfixes that keep every such block's `MeshAngleY` in [0, 2π), global, so hosted scroll racks share slots (#82).
- `SidingWallBlock`: shape, collision, retention, liquid barrier, drops (0002), side AO on sealed cells (0016, kept in 0034), peeling one layer per break (0013) or per burnt-out fire, which only catches on a wood top layer (0043) and steps over a deck that does not burn (0060), and `OnBlockInteractStart`, which does all the layering onto a standing frame (0005/0006), the in-place corner upgrade (0026), deck (0042, which layers and peels like a floor in 0053) and step (0046), the tooltip text (0030) and hosting furniture into a wall's cell (0035).
- `SidingWallEntity`: the per-wall `Framing`/`Infill`/`Front`/`Back`/`SecondFront`/`Deck`/`Step` state and the deck's own layers (0003, 0053), its mesh, and the `GetBlockInfo` hook that tooltip text comes back through; a framing entry can name its shape groups with `Elements`, and a rough frame takes a second variant on odd cells (0061), and a deck draws its ledge, a second copy reaching the frame through the room-side finish's slot, while that side is bare (0062).
- `SidingFloorBlock` and `SidingFloorEntity`: the thin floor, the wall's layers laid flat at the top of the cell with north-south joists; not subclasses of the wall's, so no wall patch sees one. Retention and attachment on the top only, peeling, drops and tooltip through the wall's static helpers, and the rope-ladder run extension `PlaceWallFrame` calls (0050), hanging from the underside once framed, with the floor answering selection for its hanger as `DecorSelectionBox`es (0054). Finishes name `FloorElements` per face and style (board direction, a lath ceiling under daub), glass infill draws a bezelled pane in the transparent pass, decor on the hit face breaks before any layer (0051), and a stick or bone floor and deck draw pole joists through the framing's `Elements` (0062).
- `SidingWallTexSource`: resolves a material key to an atlas position at mesh-build time.
- `GuestWalls`: the guest store, holding a wall's state once furniture hosts its cell, riding chunk mod data to the client and a network channel for live edits (0035).
- `EveryOverridePatches`, `GapShiftCollisionPatches`, `GuestTooltipPatches`, `GuestSealingPatches`, `GuestLightPatches`, `PanelInteractionPatches`: the guest wall's consumer patches, covering every declaring `Block` subclass override, re-answered for a hosted cell's tooltip, sealing, light, collision, selection and swallowed interaction (0035), except a hosted light source's own cell while vanilla walks its light (0057).
- `MaterialFamilies`: `MergeShared`, laying each block's own entries over `config/materials.json` (0050), then `Expand` (0010), both called from `AssetsFinalize`.
- `PlaceWallFrame`: a `CollectibleBehavior` on every plank, on sticks (0058) and on bones (0060) that raises the initial framing of a wall or a floor (0005/0006, 0050).
- `SidingModePicker`: the saw's mode picker, opened by vanilla's tool mode hotkey whenever a saw or a stone is in the off hand, the build signal `SidingWallBlock.HasBuildSignal` reads (0058); rows of framing (wall, corner, floor), the deck toggle, board and log styles stored per player, patched into `GuiDialogToolMode` with its own pick channel, icons from 0031 (0040).
- `VSSiding.Tests/WallShapeGen`: generates the shapes `wall.json` and `cornerout.json` use (0021), and the floor's, and the rough `poles` groups (0061). It lives in the test project, not beside the assets it writes.
- `VSSiding.E2E.Tests`: Atlas scenarios that boot the built `Releases/vssiding` folder on a headless server, so `just e2e` builds first. It does not reference the mod assembly; wall and guest layers are read through the block entity's tree and `GuestWalls` by reflection (0056).

`config/materials.json` carries the `Framings`/`Infills`/`Finishes` dictionaries every one of those keys looks up; `AssetsFinalize` merges it into each block's attributes, where an entry in the block's own file wins.

**An infill marked `Transparent` seals without going opaque.** Glazing retains and dams liquid like any fill but absorbs no light, which takes its cell out of the skylight patch, side AO and both 0018 patches: they all read `GetLightAbsorption`. Adjacent glazed cells merge and take no finish. See decision 0019, which also covers why there is no `window` layout.

**Material selection is a JSON attribute dictionary read by one block class, not a block variant.** Framing, infill, and face finishes each key into their own dictionary in the block's `attributes` (e.g. `attributes.Framings.oak`, `attributes.Infills.wattle`), read at mesh-build time, not `variantgroups`, which would multiply out into a real block per combination across three crossed axes. Reverse-engineered from the released Roofing mod's shipped JSON (no source available, no DLL decompiled; the assets alone show the shape). See decision 0001.

**`*Families` dictionaries are templates, not materials.** `FramingFamilies`/`InfillFamilies`/`FinishFamilies` entries (e.g. `"{wood}"`) expand into one `Framings`/`Infills`/`Finishes` entry per matching item or block in `SidingModSystem.AssetsFinalize` (`MaterialFamilies.Expand`); explicit entries win. See decision 0010.

**Overriding a vanilla property for a rendering result means finding every consumer of it first.** Vanilla overloads its properties across unrelated systems: `sidesolid: false` was set so thin walls don't cull neighbours, and vanilla reads that same flag for retention, liquid barriers, attachment, snow, mob spawns and more. That cost three shipped bugs before anyone read the list. Only the method bodies name a consumer, so this means the decompiled `VintagestoryAPI.dll`, grepping `Block` *and* `BlockBehavior` for the field. `VintagestoryAPI.xml` carries prose and signatures only, so it can confirm a member you already suspect but can never prove one absent; reflect over the assembly instead (decision 0031's work drafted a comment denying a member that was there, caught in review). Redo it on a game update; decision 0020's table is 1.21's.

## Design docs

`docs/proposals/`: draft ideas, each sized to one session, mutable.
`docs/decisions/`: numbered, Accepted, immutable. Supersede, don't rewrite.
Read decision 0001 before touching block/material architecture.
Full convention in `docs/README.md`; PR/commit workflow in `CONTRIBUTING.md`.
`docs/moddb.html`: the ModDB description, pasted into the editor's source view. Inline styles and system fonts only, since the editor strips style blocks; edit it rather than the live page.

## Workflow

Never commit to `main`: branch, PR, squash-merge. See `CONTRIBUTING.md`.
Commit subjects: `type: subject` (`feat`/`fix`/`docs`/`chore`/`refactor`/`test`), no other prefix.
