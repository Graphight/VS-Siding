# 0106 — A Roofing roof hosts a wall in its cell

- Status: Accepted
- Created: 2026-11-24
- Reflects: mod page requests https://mods.vintagestory.at/vssiding#cmt-240611, #cmt-245601 and #cmt-250864, and the Roofing author's reply (#cmt-241451); the `walls-under-roofing` proposal, which this replaces; branch `roof-hosts-wall`; Roofing 2.1.1's assets and its public block API (no DLL decompiled, as decision 0001); `patches/compat/vsroofing.json`; `RoofCut`; `SidingModSystem.IsRoofId`, `OnWallTop`, `FaceShifts`, `UnderDeckDropFor`, `DropGuest`; `GapShiftCollisionPatches.RawCollisionBoxes`, `RoofCutAt`, `UnderRoof`; `GuestSealingPatches`; `PanelInteractionPatches.InteractPrefix`, `BreakPrefix`; `GuestWalls.Edit`; `SidingWallBlock.EntityAt`, `SwapWallBlock`, `RoofRefuses`, `RefuseBaseFill`, `HasBaseFill`, `BreakGuestFrame`; `PlaceWallFrame`; `SidingWallEntity.Tesselate`; `RoofingScenarios` and `docs/compat/vsroofing-roofs.md`; `RoofCutTests`; decisions 0035, 0067, 0093, 0101; not yet played

## Summary
A Roofing roof and a Siding wall share a cell: the roof is the cell's block and the wall its guest (decision 0035), cut under the roof's slope.
Either can go in first, the wall takes and loses layers from its panel under the roof, and mining the panel peels a layer instead of breaking the roof.

## Context
Players asked for walls that fill a gable up to the underside of a Roofing roof, and for a wall and a roof in the same cell.
The proposal planned a raked wall, its top cut to a pitch in its own cell beside the roof.
Roofing 2.1.1's shapes put the gap inside the roof's own cell instead: `glass-slope1` runs from (0,0) to (16,5.33) across the cell, so no wall in another cell reaches it.

`roof.json` sets no `sidesolid`, so `HostRefusal`'s solid-side rule kept a roof out of a wall's cell.
With the roof allowed in for a probe (`RoofingScenarios`, `just probe vsroofing`), every Roofing click except one on the wall's top put the roof in the wall's cell and made the wall a guest with its layers kept, and breaking the roof brought the wall back.
The probe also recorded the roof's boxes: each piece's collision boxes are a two-step staircase whose step corners lie on the slope line, a ridge is a flat slab one level over the pieces either side, and a corner piece's steps follow two slopes.

## Design
**The roof is hostable, and spans the cell.**
`patches/compat/vsroofing.json` sets `vssidingHostable` and a `vssidingRoof` marker on Roofing's `roof`, which `SidingModSystem.IsRoofId` reads from a per-block-id table.
Both `roof-bare-*` and `roof-filled-*` host, since thatching and the base fill each swap one for the other and the guest has to outlive that.
A roof takes no off-panel shift (`FaceShifts`) and no under-deck drop (`UnderDeckDropFor`): it covers the whole cell over the panel, and an eave piece's half-height boxes would otherwise read as something that fits under a deck.

**A roof clicked onto a wall's top still goes above.**
A prefix and finalizer on `CollectibleObject.OnHeldUseStart` set the `[ThreadStatic]` `OnWallTop` while the click is on a siding wall's top face, and `SidingWallBlock.IsReplacableBy` then answers false for a roof.
Clicks on the block below, the side neighbour or the wall's faces put the roof into the wall's cell.

**Roof first, then wall.**
A Siding frame click whose target cell holds a roof with no guest stores a fresh guest wall there on the server (`PlaceWallFrame`), facing as `HorizontalOrientable` would pick, after the land claim check, and uses up the framing.
A wall, a corner and a post can go in this way; a floor and a diagonal cannot, and a picked deck is left off.
A click on the roof's own top frames the cell above, as before.

**The cut comes from the roof's public collision boxes.**
`RoofCut` reads the slope from `GetCollisionBoxes`, called through `GapShiftCollisionPatches.RawCollisionBoxes` so the guest's panel is not appended to them, and describes the part kept as a union of convex cells, each a set of half-spaces.
`PlaneFrom` fits a line from the lowest step's low corner to the highest step's high corner, accepting each corner within 1/64 of a block of it, since the steps sit on thirds of a block.
Under a ridge the wall is cut to the peak where the slopes of the roof pieces a step down either side meet, each read through the same raw boxes; a ridge with a slope on one side only is cut by that one.
A corner piece is read from its boxes alone, not from Roofing's `CornerType` key: a hip (outer corner) keeps the one cell under both slopes, and a valley (inner corner) two cells split upright where the slopes cross.
Boxes that name no slope Siding reads leave the wall uncut.
The same reading serves all five of Roofing's slopes and all four orientations; the probe recorded gables at 3x1, 1x1 and 1x3 and corners at 1x1.

**Collision, selection, mesh and sealing all follow the cut.**
`ClipBoxes` cuts each panel box into eight strips along the way the cut falls, each as high as the cut at its middle, after decision 0067's deck strips, cached per panel array and cut.
While it hosts a wall, the roof's own selection boxes are cut the other way, to their part over the cut (`above`).
Vanilla's ray picks a cell's nearest box, a tie going to the first, and a roof's steps reach under its slope, so a 1x1 ridge's slab, filling the cell under its peak, stood in front of its wall from either side.
`ClipMesh` clips every quad of the wall's mesh against the cells, and `Caps` closes each element's top along the slope in that element's own texture, so the frame, infill, finishes, glazing and vents are all cut, each with a closed top.
The mesh cache key carries the cut, and every cut mesh is a new one.
On the claimed faces the liquid barrier is the smaller of the wall's and the cut's height at that face (`RoofCut.FaceHeight`), so `SideIsSolid` follows it, and a block attaching to a side face needs its attachment area under the cut (`RoofCut.Holds`).
Retention stays the wall's whole: a thatched roof closes the rest of the face.

**The wall layers and peels under the roof (a departure from 0035 for roofs).**
Decision 0035 leaves a hosted wall's layers as they are until the host goes; under a roof the panel is the only way to reach them.
A click on a roof's panel with the build signal runs the wall's own `OnBlockInteractStart` on a fresh copy of the guest (`GuestWalls.Edit`), which `SidingWallBlock.EntityAt` returns in place of the block entity the cell lacks, and stores the copy when it changed.
`SwapWallBlock` recodes the guest where a wall would exchange its block, so a trapdoor infill still turns the cell into a vent.
A deck, a step and a corner upgrade are left out under a roof, each answering `build-under-roof` (`RoofRefuses`); a corner or a post can still be framed straight into a roof's cell.

**Mining the panel peels a layer (a departure from 0093 for roofs).**
Under decision 0093 a break aimed at a hosted cell's panel breaks the host.
A prefix on every `OnBlockBroken` override runs the wall's own break on a roof's panel hit instead, so the layer under the hit comes off as on any wall.
The bare frame goes last: its record is cleared and its materials drop through `SidingModSystem.DropGuest` (none in creative), and the roof stays (`BreakGuestFrame`).
Mining the roof's own boxes breaks the roof, and the wall comes back as for any host.

**Roofing's base fill and a wall do not share a roof.**
Roofing's ctrl click with a block in hand fills the cell under the slope with a stepped chisel block, which would stand where the wall is.
On a roof holding a guest, `InteractPrefix` answers that click handled with `build-roof-fill` before `VSRoofing.RoofBlock.OnBlockInteractStart` runs, and a frame click into a roof whose entity carries the fill's `cuboids` gets the same error with the framing kept (`HasBaseFill`).
The probe found no other path the fill takes, so dropping the wall when a fill lands anyway was not built.

## Alternatives considered
- **The proposal's raked wall in its own cell.** The gap under a Roofing slope is inside the roof's cell, so a raked wall beside it fills nothing there; it would serve only vanilla stair and slab roofs.
- **Reading the slope from Roofing's block entity** (`RoofInfo`, `CornerType`, the slope picks). That is the private saved state of a mod with no public source; the collision boxes are vanilla's public block API, and the corner reading uses them alone.
- **Decompiling Roofing's DLL.** Not needed: its assets, its block's public overrides read by reflection, and the probe's recorded boxes were enough.
- **The base fill as the wall's fill** (the `chiselling-walls` route Roofing's author named). The fill is one block's material at the cell's full depth, not a framed, layered wall, and Roofing does not yet take a pre-chiselled block.
- **Dropping the wall when the base fill lands.** Held in reserve for a fill reaching the cell by a path Siding could not stop; the probe found none, so it was not built.
- **Keeping 0093's break for roofs.** A roof is not furniture moved in and out: breaking it to change a finish would mean rebuilding the roof and its thatch to put it back.
- **An exact sloped box per layer for collision.** Collision boxes are axis-aligned, so a slope is a staircase either way; eight strips match the deck's under a diagonal (0067).
- **A deck, a step and a corner upgrade under a roof, cut like the rest.** A deck lies at the top of the cell, mostly above any slope, and a gable needs neither the step nor the upgrade; left out until a player asks.

## Consequences & open questions
- Played once before merge, which found the ridge's slab in front of its wall; otherwise checked by unit tests and Probe scenarios on a headless server.
- A hosting roof answers more selection boxes than Roofing's own, so a click can carry an index Roofing never returned; the probe found its thatch click unaffected, and no other Roofing click was tried on a strip.
- Vanilla outlines only the hit box of a block whose `DoPartialSelection` answers true, so if Roofing's does, a hosting roof outlines one strip; unseen in game.
- Retention on the claimed faces is the whole wall's even under a bare roof frame, which closes nothing above the cut.
- Corner pieces were probed at the 1x1 slope only; the other slopes' corners are read by the same rule from their boxes, unseen.
- A ridge beside a corner piece reads the corner piece as no slope, so a wall under that ridge is cut by the other side alone, or not at all.
- Under a corner a strip's height is read at the box's middle across it, so a thick box's top is off by the cut's fall across it (a `shortcut:` comment in `ClipBoxes`).
- `FaceHeight` reads a face's span at its two ends: exact for a cell's cut, the lowest of its planes, and for a valley's, the higher of two, while one of the two is level along the face, as on every side face of a Roofing valley.
- While a layer is being mined off the panel, the mining time and sounds are the roof's.
- A Roofing update that changes its boxes changes the cut; a box set `RoofCut` cannot read leaves the wall uncut rather than wrong.
- A wall under a roof takes no deck or step and cannot be upgraded to a corner, so a corner under a roof has to be framed as a corner.
