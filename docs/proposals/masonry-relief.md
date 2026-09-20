# Masonry finishes get relief

- Status: Draft
- Created: 2026-09-20
- Reflects: decisions 0011, 0014, 0021, 0022 and 0023; `VSSiding.Tests/WallShapeGen.cs`

## Summary
Cobblestone, brick and stone finishes are a flat 1-voxel slab with a texture on it.
The cladding finishes are not, any more.
Give masonry relief too, but from a grid of varied depths rather than the course taper that suits boards and shingles.

## Context
Decisions 0022 and 0023 gave the two wood cladding profiles modelled relief: shakes as tiles carrying depth from butt to head, weatherboard as a wedge per course.
Both work because a board and a shingle *lap* — they overlap in horizontal courses, so there is a butt edge to break and a natural direction to taper.

Masonry does not lap.
A brick wall is a grid of units sitting in a plane, with mortar between them.
Rubble and cobble are a plane of irregular lumps.
Neither has a butt edge, so neither the shake treatment nor the weatherboard one transfers: applying a course taper to brick would make it look like lapped siding painted to look like brick.

So this is a third geometric idea, which is why it is its own proposal rather than a third commit on the cladding work.

The finishes affected, from decisions 0011 and 0014: `cobblestone-{rock}`, `brick-{colour}` and whatever else keys into the masonry rows of `Finishes`/`FinishFamilies`.
Exact list to be read off `wall.json` when this is built, not guessed here.

## Design

**Relief is a grid of depths, not a taper.**
Each finish face is cut into cells, each cell set back a small amount from the outward face, zero to roughly 0.2 of a voxel.
No cell goes proud of the block bound, same rule as decision 0022.

**Brick and rubble want different grids, and that is the whole design question.**

- *Brick* is regular: a running bond, units roughly 4 voxels by 2, offset half a unit every row. The modelled grid should match the painted one, because a brick texture has visible, regular mortar lines and a mismatch would read as a mistake rather than as texture. This needs the texture measured first, exactly as decision 0022 measured the shingle course pitch off `shingles/{wood}-top.png`.
- *Cobble and rubble* are irregular. The painted joints are at arbitrary positions, so a modelled grid cannot match them — the same problem decision 0022 hit with shake joints, and the same answer applies: do not model joints, vary depth only, and let the depth step carry the texture without claiming to be a joint.

So brick may be able to align its relief to its texture and rubble may not, and those are two different jobs. Whether they share one mechanism is the thing to settle.

**Cells abut with no gaps.**
A gap opens a line straight into the wall cavity. Decision 0022 already established this.

**Cost is the reason to think before building.**
Cladding relief took `wall.json` to 68 elements and `cornerout.json` to 127.
A 4x8 brick grid is 32 cells per face; `cornerout` carries a `secondfront` copy, so one masonry profile could cost 96 boxes where a cladding profile cost 16.
Every box still declares six faces, most of them buried.
**`buried-shape-faces` should probably land before this does**, not after.

## Alternatives considered
- **Reuse the shake or weatherboard profile.** Would make brick look like lapped siding. The relief has to match how the material actually sits.
- **Leave masonry flat.** Defensible: stone laid flat against a timber frame is a rendered or faced wall, and flat is what that is. The cost of this proposal is real and the benefit is cosmetic. If the grid turns out to read as noise, this is the answer.
- **One shared grid for brick and rubble.** Cheaper, and probably wrong for at least one of them.
- **Per-cell random depths generated at mesh time.** Rejected for the same reason decision 0022 rejected it: the element table has to produce identical output every run or the golden test goes flaky.

## Consequences & open questions
- **Measure the textures first.** Decision 0022's shingle measurement is the template: find the painted joint pitch, then decide whether modelled geometry can agree with it. Do this before writing any boxes.
- Does brick's relief want to sit *proud* at the units and recessed at the mortar, rather than varying unit depths? Probably — mortar sits back from the brick face in reality. That is a different shape from the cladding work and may be cheaper: one recessed grid of mortar lines rather than per-unit boxes.
- Is it worth it at all for a face that is often against another block or in shadow?
- `UvRule` has three members after decision 0023. A grid needs u and v both mapped by position, which the run-axis machinery already does on one axis; whether it generalises cleanly or wants a fourth rule is an open question for whoever builds this.
