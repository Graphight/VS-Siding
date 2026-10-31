# 0081 — Steps in the stair's frame

- Status: Accepted
- Created: 2026-10-30
- Reflects: branch `131-step-seams`, issue #131; `SidingWallEntity.StepMesh`/`StepFrame`/`StepFamily`/`CellAlternate`, `SidingWallTexSource.StairTexture`/`Varies`, `SidingWallBlock.ResolveStepLeg`/`ResolveStepOrientation`/`OpenStepBoxes`/`CornerStepBoxes`, `VSSiding.Tests/WallShapeGen` (`StepSetElements`), `shapes/block/wall/wall.json` and `cornerout.json`, `blocktypes/wall.json`, `lang/en.json`'s `build-step-diagonal`; `WallShapeGenTests`, `SidingWallEntityTests`, `SidingWallTexSourceTests`, `SidingWallBlockCollisionTests`, `SidingWallBlockBuildFlowTests`, `VSSiding.E2E.Tests/JunctionScenarios.cs`; decisions 0046, 0026; played

## Summary
A step is drawn as the vanilla stair it continues, clipped to the wall's open part, and turned by that stair's facing.
Two steps side by side, or a step beside a stair, share their plank seams.
A `cornerout` takes a step too, on the leg beside the stair.

## Context
Issue #131 reported plank seams one to two pixels apart between two steps side by side.
Two causes sat in decision 0046's step.
`WallShapeGen` pinned every face of the six step boxes at uv 0, so a tread drawn at x 4..16 sampled u 0..12 of the texture.
`SidingWallEntity.RotationYDeg` turned the mesh by the wall's side (west 0, east 180), not by the stair's facing.
Oak planks have their seams at rows 0, 11 and 22 of 32, which a 180° turn does not map onto themselves, so a step on a west wall and a step on an east wall in one flight drew the texture flipped against each other.
Vanilla stairs in one flight share a rotation, so they line up; only the step differed.

## Design
**The vanilla shapes, clipped.**
`WallShapeGen` copies the boxes and face uvs of vanilla's stair shapes, with each face's rotation (the tread's 180°) and each face vanilla disables left out: `planks-free-up` and `-down`, `normal` and `flipped`, and `stonepath-stairs-free`.
`StepSetElements` clips each box to the wall's open part in the stair-north frame, the upper tread toward z 0: x 4..16 for a wall on the left, 0..12 for one on the right.
The uv of a clipped face is vanilla's rect cut to the clip, so the texture stays anchored to the stair's frame, not the step's.
Each set emits `step-{family}-{up|down}-{left|right}-{base|step}`.
The stone path's shape nests its tread in its base, so the tread is drawn from the base's y -1 and sits at y 7..15, the top of a path block; the generator places it there.
Its base is cut to y 0..7, where vanilla's runs y -1..7: the bottom voxel lies inside the block below, so each side's uv loses its bottom texel row.
The old six `step-*` elements leave `wall.json` and the blocktype lists; `StepElements` and its boxes stay for collision under their own names.

**Family from the stair block.**
`StepFamily` reads the stored stair block's `Shape.Base` path: `stonepath*` is the stone path, `normal` and `flipped` are basic (brick, shingle, stone brick, cobble, quartz), anything else, a modded shape included, draws as planks.
The path comes either as a shape asset name or as the resolved `shapes/....json`, since loading a shape rewrites its `AssetLocation` in place, so `StepFamily` takes the last segment of either form.
A stone path step is always the upright set.

**Rotation from the stair's facing.**
`StepFrame` takes the stair orientation (`up-north` and the like) and turns the wall's claimed faces back by the facing's vanilla rotateY (north 0, west 90, south 180, east 270).
The face that lands west or east gives the hand; a facing that runs across the wall has none, and draws nothing.
`StepMesh` returns the element names for that hand and the facing's rotation, and the entity tesselates them as a third pass beside the deck's.

**The stair's per-cell texture variant.**
Vanilla picks a stair's texture alternate from a hash of the cell's position (`oak1` to `oak4`).
`SidingWallTexSource.StairTexture` answers the stair's `up` texture, or its first when it has no `up`, and `Varies` says whether it has a wildcard or alternates.
`StepTexture` passes the cell's alternate, and `CellAlternate`'s `stepVaries` keeps the position hash in the mesh cache key when the stair's texture varies, so two cells do not share one variant.

**A corner takes a step.**
A `cornerout` takes a straight step continuing one leg's stair: the wall step clipped to the corner's 12×12 open part.
The sets are cut also at the second leg's end (`-front` or `-back` in the stair frame), and `StepFrame` reads which end the second leg sits at from the side, the leg and the facing.
The flight turns in the next cell.
`SidingWallBlock.ResolveStepLeg` picks the leg: on a side face, the leg whose panel faces along the clicked face's axis; on the underside, the leg nearer the hit.
The orientation comes from the stair beside that leg, the room cell off that leg's face, when it runs along the leg, else from the player, as the wall step does.
`CornerStepBoxes` and `OpenStepBoxes` give collision and selection the same clip, cut from `StepFrame`.

**Unchanged.**
Collision and selection boxes for the wall step.
A stepped frame still refuses the in-place corner upgrade (`build-stepped`).
A diagonal takes no step, refused with `build-step-diagonal`.
Storage is unchanged, so no migration: a built step redraws in the stair's frame on load.

**Relation to 0046.**
This decision supersedes 0046's "Generated boxes, not clipped vanilla shapes" and "Wall layout only" paragraphs.
The rest of 0046 stands: the `Step` layer, storing the stair's block code, building by clicking with the stair in hand, one per cell, peeling, drops, burning and the hosting refusals.

## Alternatives considered
- **Anchor the texture to the stair's frame with position-based uvs, keeping the six boxes.** Each box would take a uv from its position in the stair's frame. Rejected: steps would line up with each other, but vanilla's plank tread samples its rows flipped (rotation 180), so a step's upper tread would still miss the plank stair beside it.
- **Clip each family's shape at mesh time.** Read the held stair's own shape when the mesh is built, so modded shapes match too. Rejected: the heaviest option, and it reopens the alternative 0046 rejected.
- **A turning corner step, an outside-shape stair round the corner.** Rejected: the flight turns in the next cell, and a straight step on one leg is what the neighbouring stair continues.
- **A landing half-slab on the corner.** Rejected: it is not a continuation of a stair.
- **Take a corner's orientation from either neighbour, whichever is found first.** Rejected: two stairs may stand beside the two legs, so the order would pick for the player. The clicked leg picks.
- **Keep the step through a corner upgrade.** Rejected: the upgrade would have to choose a leg for it, so a stepped wall keeps refusing with `build-stepped`.

## Consequences & open questions
- `wall.json` and `cornerout.json` grow by the step sets for three families, two directions, two hands and, on the corner, two ends.
- A step with a modded shape draws as planks, whatever its own shape.
- A step still draws every face with one texture, the stair's `up`, so a quartz step's sides take the top texture where a quartz stair has its own side texture; 0046's question about per-face textures stays open.
- A stone path step collides as the two halves every step uses (y 0..8 and 8..16), a voxel off its drawn tread at y 7..15.
- Played: plank steps in two mirrored walls rising the same way, up and down, where issue #131's screenshot showed the seams apart, and the seams now carry across the boundary; a step beside a vanilla plank stair; stone brick, cobble and stone path steps; a quartz step's sides; and a corner step from each leg.
