# 0023 — Weatherboard gets a tapered board

- Status: Accepted
- Created: 2026-09-20
- Reflects: branch `weatherboard-taper`; `VSSiding.Tests/WallShapeGen.cs`; decisions 0007, 0021 and 0022

## Summary
Each weatherboard course becomes a four-step wedge, thick at its butt and tapering to its head, instead of one proud voxel over three flat ones.
A third uv rule, `Course`, samples the texture down the board so the steps do not each restart it.

## Context
Decision 0007 gave planks a weatherboard profile: per 4-voxel course, a proud 1-voxel butt at x 0..1 and a recessed 3-voxel body at x 0.5..1.
That is a single step, and it reads as a flat panel with a scored line every four voxels.

Real clapboard is not a step.
Each board is wedge-shaped in section, thick along its bottom edge where it stands proud and thin along its top edge where the next board laps over it.
That is what the profile was always meant to suggest.

Decision 0022 gave shakes modelled relief, which left weatherboard as the flatter of the two, so the distinction between them now runs the wrong way round for the one that is actually a milled board.

## Design

**Four steps per course, one voxel each, thickest at the butt.**

| band | depth from the outward face |
| --- | --- |
| y+3..y+4 (head) | 0.25 |
| y+2..y+3 | 0.5 |
| y+1..y+2 | 0.75 |
| y+0..y+1 (butt) | 1.0 |

Maximum thickness is unchanged at 1.0, so the wall's depth and the butt's shadow line are exactly where decision 0007 put them.
Only the material between butt and head is new.

**The taper is per course, never per block.**
Four wedges per wall, not one spanning all sixteen voxels.
A per-block wedge would reset to thin at every block boundary, putting a visible step in every stacked wall — the seam decision 0017's positional UVs exist to prevent.

**`UvRule.Course` samples v from the top of the box's own course.**
Decision 0007 chose flat v so every board starts its texture at v 0, which suits plank grain.
Split a course into four steps under that rule and all four restart at v 0, so the texture's top voxel repeats four times per board.
`Course` gives `v0 = courseTop - y1`, `v1 = courseTop - y0`, so the four steps sample v 3..4, 2..3, 1..2 and 0..1 — the grain runs once down the board and still restarts per board, which is what 0007 wanted.

This is the same class of bug the shake split hit, where each segment restarted u across the run.
The run-axis fix there and this one are the same idea on different axes: a box that used to be whole has to keep sampling the texture where it sits once it is cut up.

**The shake groups keep `Positional`.**
Their v is measured against the wall, not the course, because painted shingle courses have to carry across stacked walls.
Weatherboard has no such requirement — every course is identical, so course-relative v tiles cleanly.

## Alternatives considered
- **Reuse `UvRule.Positional` for the steps.** No new code, and it would spread the full 16-voxel texture over the four boards so each looked different. Rejected because decision 0007's per-board grain restart was deliberate, and four visibly different boards per block is a bigger change than the taper asked for.
- **Leave the two-box lap and only deepen the step.** Cheaper, but a deeper step is still a step; it makes the panel look thicker rather than making the board look like a board.
- **Eight half-voxel steps.** Smoother, and double the elements again for a difference measured in eighths of a voxel. Start at four; the step count is a table edit if four reads as blocky.
- **A per-block wedge.** What the sketch literally showed, and it puts a seam in every stacked wall.

## Consequences & open questions
- **Not yet checked in play.** What to look at: whether four 1-voxel steps read as a taper or as stairs; a stacked wall for the course boundary; both `cornerout` legs; and a weatherboard wall beside a shake wall, since these two profiles now both have relief and still need to look like different materials.
- Element count per weatherboard group went 8 to 16. `wall.json` has 68 elements, `cornerout.json` 127.
- Combined with decision 0022's shakes, most of both shape files is now cladding relief, and every box still declares six faces with most of them buried. The `buried-shape-faces` proposal is worth more after this than before it.
- `UvRule` now has three members and the emitter three v rules. A fourth would be the point to ask whether the rule belongs on the element or on the face.
