# Bamboo frames

- Status: Draft
- Created: 2026-11-08
- Reflects: branch `155-fill-from-face` at `a48bebc`; `config/materials.json` (`bamboo`, `bamboopoles`), `WallShapeGen` (`WallPoleElements`, `WallPole2Elements`, `CornerPoleElements`, `FloorPoleElements`, `DiagonalElements`, `Stems`, `StemNodes`, `CornerInElements`, `PostLayer`), `SidingWallEntity.FrameElements`/`SelectiveElements`/`PostElements`; decisions 0061, 0062, 0066, 0082, 0088; the author's playtest of #157

## Summary
Should a bamboo frame draw straight bamboo poles with node rings, its own shape, instead of the stick frame's crooked poles in a bamboo texture?
This would supersede part of 0088 (the frame drawn with the `poles` groups), part of 0066 for bamboo (a diagonal draws the plain frame whatever the entry says), and part of 0082 for bamboo (a bare post's faces are flat framing layers).

## Context
0088 gave bamboo `Elements: "poles"`, so a bamboo wall, corner, floor and deck draw the rough frame 0061 and 0062 built for sticks and bone: posts cut into lengths with a swollen middle, stubs, and rope lashing, all textured `bamboo-side`.
That reads as a stick frame painted green, not as bamboo.
A bamboo diagonal and a bamboo post draw the plain frame, since `SelectiveElements` sends both to `framing` whatever the entry names, so neither looks like bamboo at all.
The pole infill (0088) already draws bamboo as poles with proud nodes at staggered heights (`Stems`, `StemNodes`), and the frame should match it.

## Design
**Straight poles with node rings.**
Every pole of a bamboo frame is straight, with no swelling, cuts or stubs.
Each carries thin proud rings at heights that vary pole to pole, as the infill's nodes do, so the frame reads as bamboo beside the pole infill.

**Rings on every pole.**
The top and bottom plates carry rings along their length as the uprights do, so the whole frame reads as bamboo.

**Two patterns, odd and even cells.**
The rings follow one of two tables, picked by the cell's parity as the rough frame's second variant is (0061).
Neighbouring cells differ, so a long run does not repeat every cell.

**Lashing kept.**
The rope lashing bands stay where the plates cross the uprights, on the `lashing` slot, as the stick frame has them.

**Walls and corners.**
A `wall` and a `cornerout` draw the new poles in place of the rough ones.

**Floors and decks.**
The joists become straight ringed poles too, so a stilt house's floor matches its walls.

**Diagonals, bamboo only.**
A bamboo diagonal draws the ringed poles along its run.
Sticks and bone diagonals keep the plain frame.

**A bare post is one fat ringed pole.**
A bare bamboo post draws a single ringed pole filling its 4 by 4 footprint, in place of the flat framing layers on its faces.
A finish on a face still covers that face as today.

**Inside the frame's thickness.**
Every new box stays within the space the frame owns (x 1 to 3 on a wall, the post's 4 by 4), so any finish still covers it.
No face shares a plane with a frame, finish or infill drawn beside it, as the stems' tests check.

**Build order: first.**
It is small, self-contained, and follows straight on from bamboo.

## Alternatives considered
- **Keep the stick frame in bamboo texture.** What 0088 shipped; after playing it the author wants bamboo to look like bamboo.
- **One fixed ring pattern.** Cheapest, but a run repeats visibly every cell.
- **Rings on the uprights only.** Fewer boxes, but the plates would read as plain sticks.
- **A post with a ringed core and flat faces.** Only the hidden core would change, so the post would look as it does now.
- **A post of two or four thin poles bundled.** Rejected for one fat pole, which reads as a single stilt.
- **Pole frames on stick and bone diagonals too.** A separate idea; diagonals of every other material keep the plain frame.

## Consequences & open questions
- The groups need a prefix of their own: not `poles` (the sticks' groups, with their own clip rule in `DeckGroups`), not `stems` (the infill's), and nothing `IsInfillElement` catches.
Whether the deck and ledge copies need a new `DeckGroups` rule is still to be read.
- `SelectiveElements` forces `framing` on a diagonal and a post, and `PostElements` always names `framing`, so both gain a path that reads the entry, for bamboo alone.
- A post's bare faces and mitres are `framing-<slot>` layers.
The fat pole replaces them while a face is bare, and how it meets a finished face beside a bare one is still to be worked out.
- How round the poles look within boxes (a core plus ridges, as the stems do, or more steps) trades looks against quads.
The shapes already grew to 11.3 MB with the stems (0088), so the count should be measured with `EachBuiltCellHandsTheTesselatorThisManyQuads`.
- Every new group name goes into each layout's `ignoreElements`, which `EveryFramingElementGroupExistsAndIsLeftOutOfTheDefaultShape` enforces.
