# 0090 — A corner's inner faces, and its outer edge

- Status: Accepted
- Created: 2026-11-08
- Reflects: issue #154, raised from playtesting #151 (issue #139); branch `154-corner-inner-faces`; `SidingWallBlock.ResolveFinishFace`/`HostsClick`/`PeelLayer`/`DescribeFaces`/`IsFinished`/`LayerParts`/`Share`/`ShareConsumes`/`MirroredStyle`/`TryUpgradeLayout`, `SidingWallEntity.SharesLegacyBack`/`SelectiveElements`/`Ledges`, `SidingWallTexSource`'s `secondback` and `frontedge` slots, `config/materials.json`'s `plaster-diagonal`, `VSSiding.Tests/WallShapeGen` (`SecondBackOnLegTwo`, `SecondFrontCorners`, `FrontEdgeOnSecondFace`, `Element.FaceSlots`, `CornerRimPieces`, `WithDeck`), `shapes/block/wall/cornerout.json`, `VSSiding.E2E.Tests/CornerScenarios`, `FarFaceScenarios` and `JunctionScenarios`; decisions 0009, 0069, 0074, 0075, 0082, 0083, 0086

## Summary
A `cornerout`'s two inner faces take a finish and a style each, at half a wall's cost apiece.
Diagonal plaster laid on both faces of one edge mirrors round the corner, and each outer face shows its own finish out to the corner's edge.

## Context
Decision 0009 gave a corner's second leg its own front and kept one back for both legs, on the grounds that both inner faces look into the same quarter of the same cell.
Playtesting #151 (decision 0086) found the case that missed: both inner faces took one style, so diagonal plaster ran its braces the same way on both legs and could not mirror round an inside corner.
Issue #154 asked for a `secondback` on `cornerout` with its own finish and style, the way the outer faces already have `front` and `secondfront`.

Decision 0009 also parked the outer corner strip.
The front owns the outside corner's 1-voxel column, so where the two outer finishes differ, a 1/16 sliver of the front's finish shows on the second face.
It stayed parked because the fix named there was a corner element per finish style, duplicated for every style added later.

## Design
**Two inner faces.**
`SidingWallEntity.SecondBack` and `SecondBackStyle`, which a `cornerin` post already uses (decision 0082), carry a corner's second inner face, the one opposite `CorneroutSecondFace[side]`.
`ResolveFinishFace` maps a click on that face to `secondback`; the first inner face keeps `back`.
Either inner face hosts furniture (`HostsClick`), and a hit on either takes a deck or step before its finish (`PeelLayer`).
The tooltip names each inner face by its direction, on one line where the two match (`DescribeFaces`).
A finish pressed on a finished back goes onto the front, and on a finished second back onto the second front (decision 0075).
Diagonal walls and posts are unchanged.

**Shape.**
`WallShapeGen` builds a corner's room side as one back and then splits it (`SecondBackOnLegTwo`): every box of leg 2 goes to a `secondback-*` group in slot `secondback`, the `-whole` copies and the glazed rims included.
The inner corner's 1-voxel column goes with leg 2, as the weatherboard, log, shake and masonry groups already build it; it shows only on the top and bottom.
`CornerRimPieces` now assigns a back box to a leg by its group name, where it used to test the box against leg 1's slab.
The deck's ledge is cut per leg (`ledge*` and `ledgesecond*`), and `Ledges` draws each while its own inner face is bare.
`SidingWallTexSource` resolves a corner's `secondback` slot through the back's `Elements`, as `back` does.

**Cost.**
Each inner face charges and refunds half a wall's finish quantity, rounded up, at least 1 (`Share`, `LayerParts`, `ShareConsumes`); the outer faces keep a full wall's cost each.
A post's quarter is the same rule with four parts, so `PostConsumes` became `ShareConsumes`.
Each inner leg shows about half a wall's run, and two halves keep a finished corner's room side at the one charge decision 0069 made for the shared back, for any even quantity.
Rounding up means no face is free and every refund is a whole item.

**Porous corners.**
A wattle corner stops passing light once both inner faces, or both outer faces, are finished (`IsFinished`).
One inner face finished leaves the other leg's wattle bare on the room side.

**Corners from before the split.**
A corner saved before this change laid one back across both legs.
`ToTreeAttributes` now writes a `splitback` flag, and a `cornerout` read without it copies `Back` and `BackStyle` onto the second inner face (`SharesLegacyBack`), as `ReadLegacyDeck` keys its own migration on `legacydeck`.
A flag is needed because, as decision 0009 noted, an old corner cannot be told from a new one left with a bare second back.
Every load path sets `Block` before `FromTreeAttributes` runs (`CreateBehaviors`, `GuestWalls.Decode`), so the layout is known when the flag is read.
The copy is made on a `cornerout` only: `ComputeDrops` refunds `SecondBack` on every layout, so a copy onto a plain wall or a post would hand out a finish nobody laid.
At half cost per leg, peeling both inner faces refunds what was paid for the shared back, or one item more for a finish whose wall quantity is odd.

**Upgrading a filled wall.**
A wall turned into a corner in place (decision 0069) keeps its back on the leg the old wall becomes, as its front does: on the first inner leg at the left end, and moved to the second at the right end, where the front moves to the second front.
The new leg's inner face is bare.
The upgrade stays free, so it moves the back and copies nothing: a copy would refund two halves rounded up for one back laid, and the upgrade can be repeated.

**Diagonal plaster mirrors.**
`plaster-diagonal` sets `MirrorsOnCorners`.
When a corner face is first laid with it and its partner face across the edge (front with second front, back with second back) holds the same finish, the new face takes the partner's other turn whatever the picker says (`MirroredStyle`): `boards` against `hboards` or no style, `hboards` against `boards` (decision 0086's two turns).
A quarter turn of a diagonal swaps which way its braces lean, so the two faces meet as mirror images.
Pressing again with a style picked restyles the face as usual, so matching braces are one press away.
Stripes plaster does not mirror: a quarter turn of stripes swaps vertical for horizontal, a different look rather than a mirror image.

**The outer edge.**
The north side of the front's corner column lies in the second face's plane.
`WallShapeGen` gives that side its own slot, `frontedge` (`FrontEdgeOnSecondFace`, through `Element.FaceSlots`), and `SidingWallTexSource` resolves it to the second front's finish and style, or to the front's while the second front is bare.
So each outer face shows its own finish out to the edge, and a corner whose two outer finishes match looks as it did.
One rule over the existing boxes reaches every style's groups, which is what decision 0009's per-style corner element could not do.
A bare front leaves the column open, so each `secondfront` group has a `-corner` copy over it (`SecondFrontCorners`), drawn while the front is bare unless a facing front's infill already fills the column (decision 0083); on a glazed corner the copy is the corner post's rim piece alone.

## Alternatives considered
- **Keep one shared back.** Leaves diagonal plaster unable to mirror, which is what issue #154 asked for.
- **Leave an old corner's second inner face bare, as decision 0009 did for `SecondFront`.** That migration was accepted before release; this one would strip half of every finished room-side corner in released worlds.
- **A full wall's cost per inner face.** Doubles what a finished corner's room side costs, and the copy onto an old corner would refund twice what was paid.
- **An exact split, the first inner face rounding up and the second down.** Keeps every corner's room side at one wall's cost, but a one-item finish's second inner face would cost and refund nothing.
- **The upgrade copying the back onto both inner legs.** The upgrade is free and repeatable, so a one-item back laid on a wall would refund 2 from the corner it becomes.
- **Mirroring every finish that turns.** Planks and stripes plaster have no lean to mirror; their quarter turn is a different pattern, which the picker already chooses.
- **Mirroring left to the picker.** Every corner would need the Boards row changed between its two faces.
- **The inner column on leg 1, or in a group of its own.** Leg 2 already owns it in the weatherboard, log, shake and masonry groups, so moving it there keeps every style's split alike.
- **A corner element per finish style for the outer strip (decision 0009's parked fix).** A face slot covers every style with one generator rule.

## Consequences & open questions
- This supersedes decision 0009's shared back and its parked corner strip, decision 0069's back charged once for both legs, and decision 0074's corner finished by its back alone; each says so in its status line.
- A finished corner draws 1 to 4 more quads, since leg 1's and leg 2's faces at the seam belong to differently named groups and `IsBuried` culls only against boxes of the same name.
- `cornerout.json` grows by about 5%, from 3.21 MB to 3.37 MB.
- A finish whose wall quantity is one item (plaster, shakes, cobblestone, polished stone, packed or rammed earth, metal plate, stained or dyed planks) costs 2 for both inner faces where the shared back cost 1.
  An old corner laid in one refunds 2 when both inner faces are peeled, one item gained once per corner from before the split.
- A filled wall upgraded to a corner peels its back for half a wall's cost rounded up, which is less than its back cost for a wall quantity of 2 or more.
- A future finish with a leaning pattern sets `MirrorsOnCorners` to mirror.
- To confirm in game: the outer edge where two different finishes meet, and diagonal plaster mirrored on both the inner and the outer pair.
