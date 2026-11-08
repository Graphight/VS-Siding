# 0086 — Plaster turns, and corners show the whole pattern

- Status: Accepted
- Created: 2026-11-07
- Reflects: issue #139 and the reporter's reply with two screenshots; branch `139-plaster-corners`; `WallShapeGen.CornerOutElements`, `WholeCornerBack` and `CornerRimPieces`, `SidingWallEntity.SelectiveElements`, `shapes/block/wall/cornerout.json`, `config/materials.json` (`plaster-square`, `plaster-stripes`, `plaster-diagonal`), `WallShapeGenTests`, `FinishElementGroupsTests`, `VSSiding.E2E.Tests/FarFaceScenarios`; vanilla `plaster-fancy.json` and the textures under `block/stone/plaster/`; decision 0064

## Summary
Stripes and diagonal plaster take the Boards row's vertical and horizontal styles.
On a `cornerout`, framed plaster shows its whole pattern on both inner legs, and every flat finish meets itself round the outer side.

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

**The outer side.**
The sliver samples 15..16 and the second leg 0..15, so the pattern runs unbroken round the corner at one texel per voxel.
This applies to every flat finish group on `cornerout`: plain, `boards`, `hboards`, `bark`, `hbark`.

**The inner legs.**
A flat finish keeps one texel per voxel there, and each leg samples where it lies on the grid, so a tiling finish such as planks meets the wall that leg runs into.
A framed pattern instead draws a `-whole` copy of its group, which maps each leg's 12 visible voxels onto 0..16, so its border shows on both legs.
`WholeOnCorners: true` in a finish entry picks the copy; `plaster-square`, `plaster-stripes` and `plaster-diagonal` set it.
`cornerout` carries `-whole` copies of the plain, `boards` and `hboards` back groups, the three a plaster finish can resolve to.
The inner corner's 1-voxel column, which the second leg's box used to run through, is a box of its own, so neither leg's box runs on hidden behind the other's and no face samples outside 0..16.
Diagonal walls and `cornerin` posts are unchanged.

## Alternatives considered
- **A new Plaster row in the picker.** The Boards row already sets which way a finish runs, and its two flat styles already draw unturned and turned.
- **Grid-aligned inner legs for plaster too.** 12 visible voxels cannot hold a whole 16-voxel pattern, so a beam would still be cut, only at the inside corner instead of the open end.
- **Every flat finish squeezed on the inner legs.** It was this branch's second version. Planks and bark tile, so they never lost a border, and in play their boards read 25% narrower than the wall beside them on every corner already built.
- **Extrapolating the inner leg's uv across the hidden column.** It was this branch's first version. A glazed corner's rim cut at that column drew the extrapolated part, outside the texture, which the atlas fills with its neighbour.

## Consequences & open questions
- A plank corner's inner legs keep their board width; the second leg's boards now line up with the next wall's, where main sampled them three texels off (1..13 for 4..16).
- A future finish with a framed pattern has to set `WholeOnCorners` to get the copy; tile finishes would be the first to ask.
- `cornerout.json` grows by about 3% for the copies and their glazed rims.
- With Weatherboard picked, stripes and diagonal plaster take their usual look.
- A decked corner's ledge is cut at the column's edge as well, which adds boxes to `cornerout.json` but no visible seam.
- `EveryFaceSamplesInsideItsTexture` holds every face of every layout to 0..16.
