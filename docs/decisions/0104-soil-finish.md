# 0104 — Soil finish

- Status: Accepted
- Created: 2026-11-19
- Reflects: branch `soil-finish`; `config/materials.json`'s `Finishes["grass"]` and `["dirt"]`, `SidingWallBlock.FloorOnly`, `SidingWallTexSource.TintFaces`/`TintFinishes`, `SoilScenarios`; vanilla `JsonTesselator`, `ClientWorldMap.getColorMapData`; decisions 0001, 0075, 0103

## Summary
Grass and dirt are finishes for the top of a floor or a deck, laid from a vanilla soil block in hand.
A finish entry can name a block's colour maps, so the grass texture is tinted by the climate and season as vanilla's grass is.
A finish flagged `FloorOnly` goes on a floor's or a deck's top and nowhere else.

## Context
A diagonal at ground level can be decked on both halves (0096), and a player wanted wood on the room half and grass on the outer half, so the floor reads as the ground outside.
Vanilla's grass coverage texture is grey and gets its colour from the climate and season colour maps when the block is tessellated, and a siding mesh is built by the mod, so it had no tint.
A soil block in hand on a wall's side face would also place as vanilla does, or be taken as a finish with nowhere sensible to draw.

## Design
**Two finishes.**
`grass` consumes `game:soil-medium-*` (any grass coverage, since dug grass comes back as `soil-medium-none`) and draws `game:block/plant/grasscoverage/normal-top`.
`dirt` consumes `game:soil-low-*` and draws `game:block/soil/fertlow`.
They drop `soil-medium-none` and `soil-low-none`, one each, with `BlockMaterial: "Soil"`.
Very low, high and compost soil match nothing and place as vanilla.

**Top faces only.**
`FloorOnly: true` on the entry.
A floor's top and a deck's top, either half, take it.
A click on the underside, or the jump from a finished top to the underside (0075), falls through to `base.OnBlockInteractStart`, so vanilla places the soil block.
A wall, corner, post or diagonal panel refuses it on a resolved finish face with `vssiding:build-floor-only`, which stops a plain right-click from placing soil against a wall's side.
A sneak-click still places soil.
A wall's top face (`ResolveFinishFace` null) falls through before the refusal, as before.

**Tint from a block's colour maps.**
`ColorMapsFrom` names a block, here `game:soil-medium-normal`.
`TintFinishes` resolves that block's `ClimateColorMapResolved` and `SeasonColorMapResolved` rect indices plus one, as `ShapeTesselator` does.
`TintFaces` stamps `ClimateColorMapIds`, `SeasonColorMapIds` and `FrostableBits` on each face whose UV centroid lies in the texture's atlas rect on its atlas page.
It runs inside the mesh cache's build lambdas in `SidingFloorEntity.OnTesselation` and `SidingWallEntity.OnTesselation`.
`JsonTesselator` reads those per-face ids and takes temperature and rainfall from the `ColorMapData` the `AddMeshData` call passes, which `GetColorMapData` computes from the cell's position.
The ids do not depend on position, so the mesh cache stays valid.
Faces are found by texture, not element name, so floor tops, deck tops, `cap-*` groups and the outer deck's fascia are all tinted.

## Alternatives considered
- **Placing soil as a vanilla block on the deck.** Not a layer of the wall, so peeling and sealing would not see it.
- **A pre-tinted texture.** One colour for every climate and season.
- **Letting a wall take the finish.** There is no ground on a wall's side.
- **Growth, coverage change and a snowed texture.** Not built.

## Consequences & open questions
The slab's edge faces show tinted grass.
The top does not rotate per cell as vanilla's does.
A plain right-click of soil against a wall's side is refused, where it used to place the block; sneak-click is the way to place it there.
