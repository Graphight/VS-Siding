# 0086 — Plaster turns, and corners show the whole pattern

- Status: Accepted
- Created: 2026-11-07
- Reflects: branch `139-plaster-corners`; `WallShapeGen.CornerOutElements`, `shapes/block/wall/cornerout.json`, `config/materials.json` (`plaster-stripes`, `plaster-diagonal`), `WallShapeGenTests`; issue #139

## Summary
Stripes and diagonal plaster take the Boards row's vertical and horizontal styles, and every flat finish on a `cornerout` shows its whole texture on each visible side.

## Context
Issue #139 was written about logs, but the reporter's reply showed both points are plaster: "Straight logs" and "Square logs" are vanilla's names for `plaster-stripes` and `plaster-square` (decision 0064).
Stripes ran one way only.
On a corner, the inner legs' 12 visible voxels sampled 0..12 of the texture (0..13 on the `x 3..16` box), chopping a beam off, and the outer side lost its right border and repeated its left one through the 1-voxel sliver.

## Design
**Turning.**
The two plaster entries take `Styles: ["hboards", "boards"]` and copy the planks' `FloorElements`, so floors and decks turn too.
Plain and square plaster do not, since turning a symmetric pattern shows nothing.
No new picker row, icon or group: `boards` draws the pattern a quarter turn, as the boards groups already rotate their outward face.

**Whole corners.**
Each inner leg maps its visible run (4..16) onto 0..16 of the texture, extrapolated linearly across any hidden strip of the box.
The outer side samples 15..16 on the sliver and the rest on the second leg, so the pattern runs unbroken round the corner without a squeeze.
This applies to every flat finish group on `cornerout`: plain, `boards`, `hboards`, `bark`, `hbark`.
Diagonal walls and `cornerin` posts are unchanged.

## Alternatives considered
- **A new Plaster row in the picker.** Rejected: the Boards row already means "which way the grain runs".
- **Grid-aligned inner legs, with no squeeze.** Rejected: 12 visible voxels cannot hold a whole 16-voxel pattern, so a beam would still be cut.

## Consequences & open questions
- Plank boards on the inner legs are 25% narrower, since 16 voxels of texture now sit on 12.
- Glazed rims clip from these groups and follow the new uvs.
- Whether the squeeze reads well on bark finishes is unconfirmed until played.
