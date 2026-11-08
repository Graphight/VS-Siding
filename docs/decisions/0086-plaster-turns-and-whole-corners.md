# 0086 — Plaster turns, and corners show the whole pattern

- Status: Accepted
- Created: 2026-11-07
- Reflects: issue #139 and the reporter's reply with two screenshots; branch `139-plaster-corners`; `WallShapeGen.CornerOutElements` and `CornerRimPieces`, `shapes/block/wall/cornerout.json`, `config/materials.json` (`plaster-stripes`, `plaster-diagonal`), `WallShapeGenTests`, `FinishElementGroupsTests`, `VSSiding.E2E.Tests/FarFaceScenarios`; vanilla `plaster-fancy.json` and the textures under `block/stone/plaster/`; decision 0064

## Summary
Stripes and diagonal plaster take the Boards row's vertical and horizontal styles, and every flat finish on a `cornerout` shows its whole texture on each visible side.

## Context
Issue #139 was written about logs, but the reporter's reply showed both points are plaster: "Straight logs" and "Square logs" are vanilla's names for `plaster-stripes` and `plaster-square` (decision 0064).
Stripes ran one way only, where vanilla's `we` and `ns` orientations turn both stripes and diagonal plaster a quarter.
The screenshots' tooltips put the plaster on a corner's back faces.
There the inner legs' 12 visible voxels sampled 0..12 of the texture on one leg and 1..13 on the other, so a beam was cut off.
The outer side lost one column the same way: the second leg sampled 0..15 and the 1-voxel sliver at the corner 0..1, so the right border was missing and the left one repeated.

## Design
**Turning.**
The two plaster entries take `Styles: ["hboards", "boards"]` and copy the planks' `FloorElements`, so floors and decks turn too.
`boards` draws the pattern a quarter turn, as the boards groups already rotate their outward face; `hboards` draws it as before.
There is no new picker row, icon or group.
Plain and square plaster take no styles, since a quarter turn leaves them looking the same.

**Whole corners.**
Each inner leg maps its 12 visible voxels onto 0..16 of the texture.
The inner corner's 1-voxel column, which the second leg's box used to run through, is a box of its own with a flat uv, so neither leg's box runs on hidden behind the other's and no face samples outside 0..16.
A glazed corner's rim at that column samples 0..1, as it did before.
The outer side samples 15..16 on the sliver and 0..15 on the second leg, so the pattern runs unbroken round the corner without a squeeze.
This applies to every flat finish group on `cornerout`: plain, `boards`, `hboards`, `bark`, `hbark`.
Diagonal walls and `cornerin` posts are unchanged.

## Alternatives considered
- **A new Plaster row in the picker.** The Boards row already sets which way a finish runs, and its two flat styles already draw unturned and turned.
- **Grid-aligned inner legs, with no squeeze.** 12 visible voxels cannot hold a whole 16-voxel pattern, so a beam would still be cut, only at the inside corner instead of the open end.
- **Extrapolating the inner leg's uv across the hidden column.** It was this branch's first version. A glazed corner's rim cut at that column drew the extrapolated part, outside the texture, which the atlas fills with its neighbour.

## Consequences & open questions
- Plank boards on the inner legs are 25% narrower, since 16 texels now sit on 12 voxels.
- With Weatherboard picked, stripes and diagonal plaster take their usual look.
- A decked corner's ledge is cut at the column's edge as well, which adds boxes to `cornerout.json` but no visible seam.
- `EveryFaceSamplesInsideItsTexture` holds every face of every layout to 0..16.
- Whether the squeeze reads well on bark finishes is unconfirmed until played.
