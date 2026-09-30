# 0051 — Thin floor finishes

- Status: Accepted
- Created: 2026-09-29
- Reflects: branch `thin-floor-finishes`; `SidingFloorBlock`, `SidingFloorEntity`, `config/materials.json`, `WallShapeGen`'s floor table; decompiled 1.22.2 `BlockBehaviorDecor`, `WorldChunk.BreakDecor` and `SystemMouseInWorldInteractions`; decisions 0007, 0019, 0027, 0035, 0045, 0050; played over two rounds

## Summary
Decision 0050 draws a floor's top and underside as plain slabs.
This gives them styles: floorboards in either direction, a lath ceiling under daub that follows the same choice, and glass floors framed like a glazed wall.
Vanilla rugs and carpets lie on a sealed floor, and come off before the floor's layers.

## Context
A wall finish names its own element per face (decision 0007) and picks a style from the picker (decision 0027).
Most of those elements are wall-shaped: weatherboard laps step outward down a vertical face, and shakes overlap downhill.
Laid flat, a weatherboard floor is a staircase of ridges, so a floor cannot take a wall finish's `Elements` as they stand.

## Design
**Floor elements per face and style.**
A finish entry gains `FloorElements` beside its wall `Elements`, mapping each face to the styles it takes and the element drawing each:

```json
"planks": { FloorElements: {
	front: { hboards: "front-hboards", boards: "front-boards" },
	back: { hboards: "back-hboards", boards: "back-boards" } } },
"daub": { FloorElements: { back: { hboards: "back-lath-hboards", boards: "back-lath-boards" } } }
```

The face keys match the entity's `Front` and `Back`, not the top and bottom the proposal named.
A face the entry leaves out draws the plain slab, so a daub top stays plain and masonry is unchanged.
It is not the wall's `Styles` list, because `materials.json` is shared by walls and floors, and a style that exists on only one of them would make the other look up a group its shape lacks.
A single floor-wide style list was the first build and had the same fault within one finish: `boards` listed for daub's lath would have drawn `front-boards`, the plank slab, on a daub top.

**Board direction from the Boards row.**
On a floor's up and down faces the uv maps to (x, z), and the plank texture's grain runs along u when left unturned.
So `hboards` runs east-west, and `boards`, which rotates the face, runs north-south.
The direction is a style, because every floor's joists run north-south (decision 0050) and the floor has no orientation of its own.
Real floorboards run across the joists, so a face with no style picked takes its `hboards` entry.
`SidingFloorEntity` stores `FrontStyle` and `BackStyle` per face, as the wall stores its own (decision 0027).
A picked style counts only if that face lists it, so weatherboard on a floor, or vertical boards while daubing a top, falls back to the default.
Restyling a face in place costs nothing, and peeling a layer clears that face's style.

**Lath ceiling.**
`back-lath-hboards` is a daub slab at y 12.5 to 13 on eight east-west battens in the framing texture, two voxels apart; `back-lath-boards` turns the battens north-south.
Lath shows only under daub, because plaster on lath is a ceiling and plaster on a floor top would be walked off.

**Glass floors.**
Glass infill is no longer refused on a floor.
The shape gains `infill-pane` at y 14 and the wall's glazing bezel laid flat: stiles on the west and east edges, rails on the north and south, the same `glazing-*` names so the wall's `SelectiveElements` draws them unchanged.
A glazed floor draws no joists, and each bezel member drops where the floor beside it is glazed too, so a run of glass is one pane framed only round its outside, as on a wall (decision 0019).
`Joins` checks all four sides for a glazed floor and only north and south for any other; infill changes now mark all four neighbours dirty.
Where glass meets an opaque floor, the glass keeps its member and the opaque floor's rim drops against it as against any framed floor.
The pane goes in the transparent pass by the wall's `SetRenderPass` split.
A glazed floor shows the `build-glazed` error on a finish, as a glazed wall does, and absorbs no light while still sealing.

**Carpets.**
Vanilla rugs, `smallcarpet` and `mediumcarpet` are `Decor` on the `up` face.
`BlockBehaviorDecor.TryPlaceBlock` asks `CanAttachBlockAt(UP)`, which a sealed floor answers true (decision 0050), so they place with no code.
Breaking them was the problem.
Vanilla breaks a face's decor before its block only in survival, a quarter second into a hit (`SystemMouseInWorldInteractions.ContinueBreakSurvival`), and in creative removes the block and its decor together.
The floor peels a layer instead of being removed, so in creative the carpet stayed while the layers under it went.
`SidingFloorBlock.OnBlockBroken` now breaks any decor on the hit face and stops there, the way furniture in a wall's cell comes off before the wall (decision 0035), and `GetSounds` plays the decor's sound on that face.
Peeling from below or a fire can still take the infill out from under a carpet, so removing the infill also breaks the decor on the top, which `CanAttachBlockAt` would no longer allow.

## Alternatives considered
- **Reuse the wall `Elements` on a floor.** Weatherboard and shakes read as ridges when laid flat.
- **Board direction from the floor's own orientation.** The first build of 0050 did this; the joists followed the player's facing and the boards followed the joists, so the grain changed with the way the player faced while framing.
- **Parquet as a floor-only board style.** Built and played: four 8×8 tiles with the grain turned on alternate ones read as a pinwheel, not parquet. Diagonal grain needs rotated elements, which decision 0007 rejected for escaping the cell and z-fighting. Patterned floors would be their own proposal.
- **A floor-wide `FloorStyles` list.** See the first section; a style belongs to a face, not a finish.
- **Glass through the ordinary joists.** The first build; the joists broke the pane into strips, and a glazed wall already drops its posts between panes.
- **Lath as its own finish material.** A second underside finish, when a lath ceiling is plaster on battens and daub is already the plaster.
- **Daub as the plain ceiling.** Plaster hides real lath, so the plain slab is accurate; the visible strips were chosen for the look.

## Consequences & open questions
- Masonry (brick, ashlar, cobble) still draws the plain slab on a floor; pavers may need their own bond.
- Lath spacing and the pane's height are guesses to tune in play.
- A glazed floor's underside lighting belongs to `thin-floor-lighting`.
- Played: board direction on both faces, restyling in place, the weatherboard fallback, the lath under daub and its direction, glass sealing a room and merging into one bezelled pane, peeling, and carpets placing on a sealed floor and breaking first in creative and survival.
- In survival, vanilla keeps breaking the block in the same hold after the carpet goes, so a soft layer such as glass can follow it within a fraction of a second; that is vanilla's behaviour on any block.
- The floor's `GetSounds` defers to vanilla's decor lookup when the hit face has decor, so breaking a carpet sounds like a carpet. Added after the second playtest and not yet played.
- Removing the infill from under a carpet, by peeling from below or by fire, is not yet played.
