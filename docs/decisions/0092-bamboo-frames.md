# 0092 — Bamboo frames

- Status: Accepted
- Created: 2026-11-08
- Reflects: branch `bamboo-frames`, from `47e49a3`; `config/materials.json` (`bamboo`, `bamboopoles`), `WallShapeGen` (`WallPoleElements`, `WallPole2Elements`, `CornerPoleElements`, `FloorPoleElements`, `DiagonalElements`, `Stems`, `StemNodes`, `CornerInElements`, `PostLayer`, `BambooPost`, `DeckGroups`), `SidingWallEntity.FrameElements`/`SelectiveElements`/`PostElements`, `WallShapeGenTests`, `SidingWallEntityTests`; decisions 0061, 0062, 0066, 0082, 0088; the author's playtest of #157; unit tests pass (`just test`); the new shapes have not been played, and `just e2e` has not been run on them

## Summary
A bamboo frame draws straight poles with node rings, in place of the stick frame's crooked poles in a bamboo texture.
This supersedes the frame drawn with the `poles` groups in 0088, the plain frame on every diagonal in 0066 (for bamboo), and the flat bare faces of a post in 0082 (for bamboo).

## Context
0088 gave bamboo `Elements: "poles"`, so a bamboo wall, corner, floor and deck drew the rough frame 0061 and 0062 built for sticks and bone: posts cut into lengths with a swollen middle, stubs, and rope lashing, all textured `bamboo-side`.
That read as a stick frame painted green.
A bamboo diagonal and a bamboo post drew the plain frame, since `SelectiveElements` sent both to `framing` whatever the entry names.
The pole infill (0088) already draws bamboo as poles with proud nodes at staggered heights (`Stems`, `StemNodes`), and the frame should match it.

## Design
**One pole builder, built as the stems are.**
A pole is a core inset a quarter voxel inside the plain member's box, a proud ridge down each exposed face, and thin proud rings.
One builder serves every layout.
Sticks and bone keep `poles`; `materials.json` switches `bamboo` to `Elements: "bamboo"`, and `FrameElements` already appends `2` on odd cells, so the second ring table is `bamboo2` with no code change.

**Rings on every pole.**
Plates, rims and joists carry rings as the uprights do, at quarter-off-grid heights from two tables (`bamboo` on even cells, `bamboo2` on odd), none on a `StemNodes` height, so no ring face meets a stem's.

**Lashing kept.**
Rope lashing stays where plates cross uprights (wall, corner) and where rims cross joists (floor), on the `lashing` slot, as the pole frame has it.

**Inside the frame's thickness.**
Every box stays within the plain member's box (x 1 to 3 on a wall, the post's 4 by 4), so any finish still covers it.
No face shares a plane with a frame, finish or infill drawn beside it; the pole tests extend the stems' thickness and plane checks to `bamboo` and `bamboo2`.

**Walls, corners, floors and decks.**
`wall`, `cornerout`, the floor and every deck and ledge copy draw the new poles.
`DeckGroups`' `CoveredLengths` rule, which read `StartsWith("poles")`, covers `bamboo` too.
A corner's tees reuse `CornerTees("bamboo")`.
The corner post is a five-box plus (`BambooPost`): a core and one box on each side of it, not the single pole the wall's uprights are.
A floor's ridges sit 0.1 in from the joist's edge rather than a quarter voxel, and a floor's rims drop the cheeks the skin of a wall's plates carries.

**Diagonals.**
A bamboo diagonal draws the ringed poles along its run, repeated cell by cell as `DiagonalStems` repeats the stems, with `-left` and `-right` copies, its corner fillers and half posts as ringed uprights.
`SelectiveElements` no longer forces `framing` on a diagonal for every entry: it picks the bamboo groups with `frame.StartsWith("bamboo")`, and the hardcoded `framing-{end}` and `framing-join-{end}` names become `{frame}-…`.
The test is on bamboo rather than on excluding `poles`, so a sticks or bone diagonal keeps the plain frame without a rule of its own.

**A bare post is one fat pole.**
A bare bamboo post is one ringed pole filling the 4 by 4, sliced along the post's existing pinwheel: a core `bamboo` and one slice `bamboo-<slot>` per face, each cut to that layer's owned body (`PostLayer`).
The fat pole has rings but no ridges, a departure from the wall's poles.
A finished face drops its slice and draws its finish layer as today.
Its `-mitre` piece lies in the previous slot's body (front, secondfront, back, secondback, cyclic), so on a bamboo post it is left out while that neighbour is bare, and no mitre quad floats over the round pole.
`PostElements` takes the frame, with `poles` still mapping to `framing`, so a sticks or bone post is unchanged.

**Quads and shape sizes.**
`EachBuiltCellHandsTheTesselatorThisManyQuads` now pins a bamboo frame on a wall, a corner and a floor, on an even and an odd cell, beside the pole frame it replaces:

| Cell | Pole frame | Bamboo frame | Bamboo frame, odd cell |
|------|-----------:|-------------:|-----------------------:|
| `wall` | 144 | 228 | 208 |
| `cornerout` | 158 | 370 | 350 |
| `floor` | 240 | 398 | 398 |

The shapes grew as follows, in bytes:

| Shape | At `47e49a3` | Now |
|-------|-------------:|----:|
| `wall.json` | 2,481,733 | 3,149,203 |
| `cornerout.json` | 3,369,514 | 3,986,651 |
| `diagonal.json` | 3,644,906 | 4,341,370 |
| `cornerin.json` | 1,613,453 | 2,245,670 |
| `floor.json` | 327,721 | 488,038 |

## Alternatives considered
- **Keep the stick frame in bamboo texture.** What 0088 shipped; after playing it the author wants bamboo to look like bamboo.
- **One fixed ring pattern.** Cheapest, but a run repeats visibly every cell.
- **Rings on the uprights only.** Fewer boxes, but the plates would read as plain sticks.
- **A post with a ringed core and flat faces.** Only the hidden core would change, so the post would look as it does now.
- **A post of two or four thin poles bundled.** Rejected for one fat pole, which reads as a single stilt.
- **Pole frames on stick and bone diagonals too.** A separate idea; diagonals of every other material keep the plain frame.
- **Excluding `poles` to pick the bamboo diagonal.** The diagonal tests `frame.StartsWith("bamboo")` instead, so a future frame prefix does not silently gain a diagonal.

## Consequences & open questions
- The new shapes have not been played.
The corner post's plus, the floor's inset ridges and the fat post's rings are judged from unit tests on thickness, planes and group names alone, and may need a pass after the author plays a bamboo run, corner, diagonal, floor, deck and a stilt of posts with one face finished.
- A bamboo wall costs 228 quads against the pole frame's 144, a corner 370 against 158, and the shapes are 14.2 MB together; a large bamboo build is the case to watch.
- A bamboo post's finished face beside a bare one meets the round pole at a plain edge, with no mitre; whether that reads as a seam is for play to say.
- Nothing stored changes, so there is no new end-to-end scenario; `just e2e` runs once before the push.
