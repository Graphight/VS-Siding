# 0051 — Thin floor finishes

- Status: Accepted
- Created: 2026-09-29
- Reflects: branch `thin-floor-finishes`; `SidingFloorBlock`, `SidingFloorEntity`, `SidingWallBlock.HasStyle`, `config/materials.json`, `WallShapeGen`'s floor table, `SidingModePicker`, `parquet.svg`; decisions 0007, 0019, 0027, 0045, 0050; not yet played

## Summary
Decision 0050 draws a floor's top and underside as plain slabs.
This gives them styles: floorboards with visible boards in either direction, parquet, a lath ceiling under daub, and glazed floors.
Carpets need no code.

## Context
A wall finish names its own element per face (decision 0007) and picks a style from the picker (decision 0027).
Most of those elements are wall-shaped: weatherboard laps step outward down a vertical face, and shakes overlap downhill.
Laid flat, a weatherboard floor is a staircase of ridges, so a floor cannot take a wall finish's `Elements` as they stand.

## Design
**Floor elements per finish.**
A finish entry gains `FloorElements: { front, back }` beside its wall `Elements`.
The keys match the entity's `Front` and `Back`, not the top and bottom the proposal named, so the floor's lookup mirrors the wall's.
Planks default to `front-hboards` and `back-hboards`.
Daub and `daub-{color}` name only `back: "back-lath"`, so lath is a ceiling and a daub top stays plain.
A finish without `FloorElements` draws the plain slab.
`WallShapeGen`'s floor table carries the matching groups.

**Board direction from the Boards row.**
On a floor's up and down faces the uv maps to (x, z), and the plank texture's grain runs along u when left unturned.
So `hboards` runs east-west, and `boards`, which rotates the face, runs north-south.
The direction is a style, because every floor's joists run north-south (decision 0050) and the floor has no orientation of its own.
Real floorboards run across the joists, so east-west is the default.
`FloorStyles` on the plank entries lists the styles a floor takes.
It is separate from `Styles` because `materials.json` is shared: a wall asking for a style its shape lacks would look up a missing group.
`SidingFloorEntity` stores `FrontStyle` and `BackStyle` per face, as the wall stores its own (decision 0027).
Weatherboard has no flat form, so a floor picking it falls back to east-west.
Restyling a face in place costs nothing, and peeling a layer clears that face's style.
`SidingWallBlock.HasStyle` takes the list name, so wall and floor share it.

**Lath ceiling.**
`back-lath` is a daub slab at y 12.5 to 13, on eight east-west battens in the framing texture, two voxels apart.
Lath shows only under daub because plaster on lath is a ceiling, and plaster on a floor top would be walked off.

**Parquet.**
A floor-only style on the Boards row: four 8×8 tiles, with the grain turned on alternate tiles.
Each tile samples the texture at its own x and z, which `WallShapeGen` does through `RunBoth`, so no two tiles repeat the same crop.
A wall picking parquet falls back to its default style.
The icon is `parquet.svg`, the label `toolmode-parquet`.

**Glass floors.**
Glass infill is no longer refused on a floor.
The shape gains `infill-pane` at y 14, mid-joist, and `SelectiveElements` swaps it in for the infill when glazed.
The pane goes in the transparent pass by the wall's `SetRenderPass` split (decision 0019).
A glazed floor shows the `build-glazed` error on a finish, as a glazed wall does, and absorbs no light while still sealing.
The joists always run through and the rims already drop against framed floors, so adjacent glass merges with no new join logic.

**Carpets.**
No code.
Vanilla rugs, `smallcarpet` and `mediumcarpet` are `Decor` on the `up` face.
`BlockBehaviorDecor.TryPlaceBlock` asks `CanAttachBlockAt(UP)`, which a sealed floor answers true (decision 0050).
Not yet played.

## Alternatives considered
- **Reuse the wall `Elements` on a floor.** Weatherboard and shakes read as ridges when laid flat.
- **Board direction from the floor's own orientation.** The first build of 0050 did this; the joists followed the player's facing and the boards followed the joists, so the grain changed with the way the player faced while framing.
- **Parquet in `Styles`.** A wall picking it would look up a group its shape lacks.
- **Lath as its own finish material.** A second underside finish, when a lath ceiling is plaster on battens and daub is already the plaster.
- **Daub as the plain ceiling.** Plaster hides real lath, so the plain slab is accurate; the visible strips were chosen for the look.

## Consequences & open questions
- Masonry (brick, ashlar, cobble) still draws the plain slab on a floor; pavers may need their own bond.
- Parquet tile size, lath spacing and the pane's height are guesses to tune in play.
- A glazed floor's underside lighting belongs to `thin-floor-lighting`.
- Carpets on a sealed floor are untested, so the handbook does not mention them yet.
