# 0092 — Bamboo frames

- Status: Accepted
- Created: 2026-11-08
- Reflects: branch `bamboo-frames`, from `47e49a3`; `config/materials.json` (`bamboo`, `bamboopoles`), `WallShapeGen` (`WallPoleElements`, `WallPole2Elements`, `CornerPoleElements`, `FloorPoleElements`, `DiagonalElements`, `Stems`, `StemNodes`, `CornerInElements`, `PostLayer`, `BambooRings`/`BambooVariants`, `BambooPole`, `BambooPost`, `NotchedSquare`, `BambooFatPost`, `FatPostRing`, `DeckGroups`), `SidingWallEntity.FrameElements`/`SelectiveElements`/`PostElements`, `WallShapeGenTests`, `SidingWallEntityTests`; decisions 0061, 0062, 0066, 0082, 0088; the author's playtest of #157; PR #161 and its review; unit tests pass (`just test`) and the end-to-end suite passed at `e9a07ad` and again after the review's fixes (`just e2e`, 121); played cut by cut on 2026-11-08, the rings reaching into the next cell and the full-width post not yet

## Summary
A bamboo frame draws straight poles with node rings, in place of the stick frame's crooked poles in a bamboo texture.
This supersedes the frame drawn with the `poles` groups in 0088, the plain frame on every diagonal in 0066 (for bamboo), and the flat bare faces of a post in 0082 (for bamboo).

## Context
0088 gave bamboo `Elements: "poles"`, so a bamboo wall, corner, floor and deck drew the rough frame 0061 and 0062 built for sticks and bone: posts cut into lengths with a swollen middle, stubs, and rope lashing, all textured `bamboo-side`.
That read as a stick frame painted green.
A bamboo diagonal and a bamboo post drew the plain frame, since `SelectiveElements` sent both to `framing` whatever the entry names.
The pole infill (0088) already draws bamboo as poles with proud nodes at staggered heights (`Stems`, `StemNodes`), and the frame should match it.

## Design
**Poles built as the stems are.**
A pole (`BambooPole`) is a core inset a quarter voxel inside the plain member's box on its depth, a proud ridge down each of its two depth faces, and thin rings standing proud of it all round.
It draws every upright, plate, rim and joist; a post is the other shape, below.
Sticks and bone keep `poles`; `materials.json` switches `bamboo` to `Elements: "bamboo"`, and `FrameElements` already appends `2` on odd cells, so the second ring table is `bamboo2` with no code change.

**Rings all the way round.**
A ring is one box round the pole.
On depth it reaches the member's box, as the core's quarter-voxel inset leaves room for.
Across, it stands a quarter voxel past the member's box, and where the member lies on the cell's side it reaches a quarter voxel into the next cell, so the core still meets the next cell's member edge to edge.
It never reaches through the cell's top or bottom, where a decked wall's cap or the next wall up sits flush; there it stops a hair short, off the plane of the core.
The first cut ringed the depth faces alone; the author played it and asked for rings all round, accepting the proud rings' break in the frame's outline.
A second cut stepped the core in a quarter voxel at the cell's edge to keep the rings inside the cell, which left a slot through the frame wherever two members met across a cell's side or a stack's seam; the author played it and chose rings reaching into the next cell instead.

**Rings on every pole.**
Plates, rims and joists carry rings as the uprights do, from two tables (`bamboo` on even cells, `bamboo2` on odd).
The wall, corner, diagonal and post share one table per variant (`BambooVariants`); the floor keeps its own, one per joist and rim length, since its members are not a wall's.
No ring face meets a stem's or a lashing's, which the plane tests check rather than any rule on the numbers.
An upright's rings keep off the plates' heights, where a ring would share the plane of the plate's core or ridge.
A straight wall joins no neighbour left or right, so every cell draws both its uprights and they meet at every cell's side, each ring reaching into the other's; the four upright tables keep half a voxel apart so two never take one place, and a floor's north and south rims ring from separate tables for the same reason (`NoTwoBambooRingsThatMeetAcrossACellEdgeTakeOnePlace`).
Two uprights from the same table and parity can still meet where mirrored walls or a corner's end post put them side by side, and there the rings' faces fight.

**Lashing kept.**
Rope lashing stays where plates cross uprights (wall, corner) and where rims cross joists (floor), on the `lashing` slot, as the pole frame has it.

**Inside the frame's thickness.**
Every box stays within the frame's thickness (x 1 to 3 on a wall, y 13 to 15 on a floor, the post's 4 by 4), so any finish still covers it; only a ring's reach across, along the frame, leaves the plain member's box, and on a cell's side the cell.
No face shares a plane with a frame, finish or infill drawn beside it; the pole tests extend the stems' thickness and plane checks to `bamboo` and `bamboo2`, and `EveryGeneratedBoxIsNonDegenerateAndInsideTheBlock` lets a bamboo box reach a quarter voxel past the block's sides alone.

**Walls, corners, floors and decks.**
`wall`, `cornerout`, the floor and every deck and ledge copy draw the new poles.
`DeckGroups`' `CoveredLengths` rule, which read `StartsWith("poles")`, covers `bamboo` too.
A corner's tees reuse `CornerTees("bamboo")`.
The corner post (`BambooPost`) is its 2 by 2 square with a quarter voxel notched down each vertical edge, and rings filling the notches back to the square, not the single pole the wall's uprights are.
A floor's ridges sit 0.1 in from the joist's edge rather than a quarter voxel, and a floor's rims drop the cheeks the skin of a wall's plates carries.

**Diagonals.**
A bamboo diagonal draws the ringed poles along its run, repeated cell by cell as `DiagonalStems` repeats the stems, with `-left` and `-right` copies, its corner fillers and half posts as ringed uprights.
`SelectiveElements` no longer forces `framing` on a diagonal for every entry: it picks the bamboo groups with `frame.StartsWith("bamboo")`, and the hardcoded `framing-{end}` and `framing-join-{end}` names become `{frame}-…`.
The test is on bamboo rather than on excluding `poles`, so a sticks or bone diagonal keeps the plain frame without a rule of its own.
A glazed bamboo diagonal draws the glazing's rails, as a glazed wall does, so its ends stay `framing-{end}` and `framing-join-{end}`.

**A bare post is one ringed pole.**
A bare bamboo post's rings fill the 4 by 4, a half voxel notched down each vertical edge so they read round, and between them the pole is the same outline a quarter voxel in all round, as a wall's pole stands inside its rings (`BambooFatPost`).
The rings meet the walls behind the post and a floor round it edge to edge, and one more closes the post's top, a node at each joint of a stack, so a floor laid onto it leaves no groove to see down.
A ring is drawn as the part of it standing out of the pole, a strip along each flat and an L in each notch (`FatPostRing`), so no ring box lies over the pole's faces once both are cut.
All of it is cut along the post's existing pinwheel into a core `bamboo` and one slice `bamboo-<slot>` per face, each that layer's owned body (`PostLayer`), so the core and slices together are exactly the notched outline between the rings and the ring's at them.
The notches stand in for the wall's ridges.
The first cut filled the whole 4 by 4 with rings flush in its notches; the author played it, found it too thick and its rings lost, and asked for it thinner with rings standing out.
A pole 3 across inside 3.5 rings followed, but it left half a voxel of air between the pole and the walls and floor that meet the 4 by 4, so the author kept the thickness and the rings took the full square instead.
A finished face drops its slice and draws its finish layer as today.
Its `-mitre` piece lies in the previous slot's body (front, secondfront, back, secondback, cyclic), so on a bamboo post it is left out while that neighbour is bare, and no mitre quad floats over the round pole.
`PostElements` takes the frame, with `poles` still mapping to `framing`, so a sticks or bone post is unchanged.

**Quads and shape sizes.**
`EachBuiltCellHandsTheTesselatorThisManyQuads` now pins a bamboo frame on a wall, a corner and a floor, on an even and an odd cell, beside the pole frame it replaces:

| Cell | Pole frame | Bamboo frame | Bamboo frame, odd cell |
|------|-----------:|-------------:|-----------------------:|
| `wall` | 144 | 168 | 168 |
| `cornerout` | 158 | 286 | 286 |
| `floor` | 240 | 338 | 338 |

The shapes grew as follows, in bytes:

| Shape | At `47e49a3` | Now |
|-------|-------------:|----:|
| `wall.json` | 2,481,733 | 3,050,690 |
| `cornerout.json` | 3,369,514 | 3,897,833 |
| `diagonal.json` | 3,644,906 | 4,261,661 |
| `cornerin.json` | 1,613,453 | 2,235,940 |
| `floor.json` | 327,721 | 462,729 |

## Alternatives considered
- **Keep the stick frame in bamboo texture.** What 0088 shipped; after playing it the author wants bamboo to look like bamboo.
- **One fixed ring pattern.** Cheapest, but a run repeats visibly every cell.
- **Rings on the uprights only.** Fewer boxes, but the plates would read as plain sticks.
- **A post with a ringed core and flat faces.** Only the hidden core would change, so the post would look as it does now.
- **A post of two or four thin poles bundled.** Rejected for one fat pole, which reads as a single stilt.
- **Pole frames on stick and bone diagonals too.** A separate idea; diagonals of every other material keep the plain frame.
- **Excluding `poles` to pick the bamboo diagonal.** A later framing with groups of its own but none in the diagonal's shape would then ask for names the shape lacks and draw no frame; testing for `bamboo` leaves it the plain frame.
- **A square fat post.** Each slice inset a quarter voxel all round, the review's first suggestion: the smallest fix for the fins the first cut left at the corners, but square where the post should read round.

## Consequences & open questions
- The author played each cut on 2026-11-08: the walls' and floors' rings and lashing read well from the first, and the rings on two sides, the 4 by 4 post with flush rings, the 3-wide post and the stepped-in edges were each redrawn as above.
The rings reaching into the next cell and the full-width post are judged from unit tests on thickness, planes, outline, ring places and group names until the next play.
- A bamboo wall costs 168 quads against the pole frame's 144, a corner 286 against 158, and the shapes are 13.9 MB together; a large bamboo build is the case to watch.
- A bamboo post's finished face beside a bare one meets the round pole at a plain edge, with no mitre; whether that reads as a seam is for play to say.
- Nothing stored changes, so there is no new end-to-end scenario; the existing suite passed once before the push.
